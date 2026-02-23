using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー権威型ヒット判定システム（ラグコンペンセーション対応）
///
/// 攻撃ステート中にキャラ前方のカプセル領域を走査し、
/// HurtboxComponent を持つ対象とのヒットを判定する
///
/// ラグコンペンセーション:
/// - 攻撃者の推定ビュータイム（現在時刻 - RTT/2 - 補間遅延）まで
///   全敵プレイヤーの位置を巻き戻してからヒット判定を実行
/// - 最大補正時間は MAX_LAG_COMPENSATION_MS（150ms）
/// - 巻き戻し → 判定 → 復元 は using (Rewind) スコープで自動管理
///
/// 判定ルール:
/// - サーバー側でのみ実行
/// - 1攻撃1ヒット（同じ攻撃セグメント中、同じ対象には1回だけ）
/// - 無敵状態の対象にはヒットしない
/// - 自分自身にはヒットしない
/// </summary>
[RequireComponent(typeof(ComboSystem))]
public class HitboxSystem : NetworkBehaviour
{
    // ============================================================
    // 参照
    // ============================================================

    private ComboSystem _comboSystem;

    // ============================================================
    // ヒット管理
    // ============================================================

    // 現在の攻撃セグメントでヒット済みの対象（NetworkObjectId で管理）
    private readonly HashSet<ulong> _hitTargetsThisAttack = new();

    // 前回チェック時の攻撃シーケンス番号（新攻撃検知用）
    private int _lastAttackSequence;

    // Physics.OverlapCapsule 用の事前確保バッファ（GC 回避）
    private readonly Collider[] _hitResults = new Collider[GameConfig.MAX_HIT_TARGETS_PER_FRAME];

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _comboSystem = GetComponent<ComboSystem>();
    }

    /// <summary>
    /// サーバーのみ: 毎 FixedUpdate でヒット判定
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;
        CheckHitbox();
    }

    // ============================================================
    // ヒット判定（★サーバー側で実行 + ラグコンペンセーション★）
    // ============================================================

    /// <summary>
    /// 現在の攻撃に対応する Hitbox をチェックし、範囲内の対象にヒットを適用する
    /// 攻撃者のビュータイムまで敵を巻き戻してから判定する
    /// </summary>
    private void CheckHitbox()
    {
        // 攻撃シーケンスの変化を検知 → ヒット済みリストをリセット
        int currentSequence = _comboSystem.AttackSequence;
        if (currentSequence != _lastAttackSequence)
        {
            _hitTargetsThisAttack.Clear();
            _lastAttackSequence = currentSequence;
        }

        // 攻撃中でなければスキップ
        int comboStep = _comboSystem.ComboStep;
        int chargeType = _comboSystem.ChargeType;
        bool isDash = _comboSystem.IsDashAttacking;
        bool isRush = _comboSystem.IsRush;

        if (comboStep == 0 && chargeType == 0 && !isDash) return;

        // 現在の攻撃に対応する HitboxData を取得
        HitboxData hitbox = HitboxData.GetHitboxData(comboStep, chargeType, isDash, isRush);
        if (hitbox.ActiveEndFrame == 0) return; // データが無い

        // 現在のフレーム番号を計算（経過時間 → フレーム）
        float elapsed = _comboSystem.SegmentElapsed;
        int currentFrame = Mathf.FloorToInt(elapsed / GameConfig.FIXED_DELTA_TIME);

        // アクティブフレーム外ならスキップ
        if (currentFrame < hitbox.ActiveStartFrame || currentFrame > hitbox.ActiveEndFrame) return;

        // ラグコンペンセーション: 攻撃者のビュータイムを計算
        // ホスト（サーバー兼クライアント）は巻き戻し不要（RTT=0）
        double viewTime = NetworkManager.Singleton.ServerTime.Time;
        bool needsRewind = !IsOwnedByServer;

        if (needsRewind)
        {
            // リモートクライアントの場合: RTT/2 + 補間遅延分だけ過去を参照
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            ulong clientId = OwnerClientId;
            double rttSec = transport.GetCurrentRtt(clientId) / 1000.0;
            double clientViewTime = NetworkManager.Singleton.ServerTime.Time - (rttSec / 2.0);
            viewTime = LagCompensationManager.Instance.EstimateViewTime(clientViewTime);

            double rewindMs = (NetworkManager.Singleton.ServerTime.Time - viewTime) * 1000.0;
            Debug.Log($"[LagComp] {gameObject.name}: 巻き戻し {rewindMs:F1}ms (RTT={rttSec * 1000.0:F1}ms)");
        }

        // カプセル領域を計算（攻撃者自身の位置は巻き戻さない）
        Vector3 basePos = transform.position + transform.rotation * hitbox.Offset;
        Vector3 endPos = basePos + transform.forward * hitbox.Length;

        // 巻き戻しスコープ内で OverlapCapsule を実行
        // using ブロックを抜けると自動的に全プレイヤーが元の位置に復元される
        int hitCount;
        if (needsRewind)
        {
            using (LagCompensationManager.Instance.Rewind(viewTime))
            {
                hitCount = Physics.OverlapCapsuleNonAlloc(
                    basePos, endPos, hitbox.Radius, _hitResults
                );
                ProcessHits(hitCount);
            }
        }
        else
        {
            // ホストは巻き戻し不要（現在位置でそのまま判定）
            hitCount = Physics.OverlapCapsuleNonAlloc(
                basePos, endPos, hitbox.Radius, _hitResults
            );
            ProcessHits(hitCount);
        }
    }

    /// <summary>
    /// OverlapCapsule の結果をフィルタリングしてヒットを確定する
    /// 巻き戻しスコープ内で呼ばれる（敵位置が過去に戻った状態）
    /// </summary>
    private void ProcessHits(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            var hurtbox = _hitResults[i].GetComponent<HurtboxComponent>();
            if (hurtbox == null) continue;

            // 自分自身を除外
            if (hurtbox.NetworkObjectId == NetworkObjectId) continue;

            // 無敵状態を除外
            if (hurtbox.IsInvincible()) continue;

            // 1攻撃1ヒット: 既にヒット済みならスキップ
            if (_hitTargetsThisAttack.Contains(hurtbox.NetworkObjectId)) continue;

            // ヒット確定
            _hitTargetsThisAttack.Add(hurtbox.NetworkObjectId);

            int comboStep = _comboSystem.ComboStep;
            int chargeType = _comboSystem.ChargeType;
            bool isDash = _comboSystem.IsDashAttacking;

            Debug.Log($"[Hit] {gameObject.name} → {hurtbox.gameObject.name} ヒット確定" +
                      $" (N{comboStep}/C{chargeType}/D={isDash})");

            // 全クライアントにヒット通知（エフェクト表示用）
            Vector3 hitPoint = hurtbox.transform.position;
            NotifyHitClientRpc(NetworkObjectId, hurtbox.NetworkObjectId, hitPoint);

            // ガード方向判定: 正面180度以内ならガード成功、背面はめくり
            bool isGuardSuccess = hurtbox.IsGuardingAgainst(transform.position);

            if (isGuardSuccess)
            {
                // EGReady 中にガード成功 → EGカウンター発動
                var targetEG = hurtbox.GetComponent<EGSystem>();
                if (targetEG != null && targetEG.IsEGReady)
                {
                    var attackerReaction = GetComponent<ReactionSystem>();
                    targetEG.OnEGCounter(transform, attackerReaction);
                    // EGカウンター時はダメージも無し（カウンターで反撃するため）
                    continue;
                }

                Debug.Log($"[Guard] {hurtbox.gameObject.name} ガード成功（{gameObject.name} の攻撃）");

                // ガード成功: リアクション無し（Guard ステート維持）+ ガードノックバック
                ApplyGuardKnockback(hurtbox.transform, transform.position);
            }
            else
            {
                // ガード失敗（めくり）or 非ガード: 通常リアクション適用
                if (hurtbox.IsGuarding())
                {
                    Debug.Log($"[Guard] めくり！ {hurtbox.gameObject.name} のガードを貫通");
                }

                var targetReaction = hurtbox.GetComponent<ReactionSystem>();
                if (targetReaction != null)
                {
                    HitReaction reaction = ReactionSystem.GetReactionType(comboStep, chargeType, isDash);
                    targetReaction.ApplyReaction(reaction, transform.position, comboStep, chargeType);
                }
            }

            // ダメージ計算・HP減少
            var targetHealth = hurtbox.GetComponent<HealthSystem>();
            if (targetHealth != null)
            {
                bool isRush = _comboSystem.IsRush;
                float motionMultiplier = DamageCalculator.GetMotionMultiplier(comboStep, chargeType, isDash, isRush);

                // 被弾者のステートから空中かを判定
                var targetStateMachine = hurtbox.GetComponent<CharacterStateMachine>();
                bool isAirborne = targetStateMachine != null &&
                    (targetStateMachine.CurrentState == CharacterState.Launch ||
                     targetStateMachine.CurrentState == CharacterState.AirHitstun ||
                     targetStateMachine.CurrentState == CharacterState.AirRecover ||
                     targetStateMachine.CurrentState == CharacterState.JumpAttack ||
                     targetStateMachine.CurrentState == CharacterState.Jump);

                var damageResult = DamageCalculator.Calculate(
                    GameConfig.DEFAULT_ATK,
                    motionMultiplier,
                    GameConfig.DEFAULT_DEF,
                    targetHealth.GetHpRatio(),
                    isAirborne: isAirborne,
                    isGuarding: isGuardSuccess
                );

                targetHealth.TakeDamage(damageResult.HpDamage);

                // ダメージ通知（クリティカル情報を含む）
                NotifyDamageClientRpc(hurtbox.NetworkObjectId, damageResult.HpDamage, damageResult.IsCritical);
            }
        }
    }

    // ============================================================
    // ガードノックバック
    // ============================================================

    /// <summary>
    /// ガード成功時にわずかに後退させる（CharacterController.Move）
    /// 攻撃者の反対方向に GUARD_KNOCKBACK_DISTANCE 分だけ押す
    /// </summary>
    private void ApplyGuardKnockback(Transform defenderTransform, Vector3 attackerPosition)
    {
        Vector3 knockDir = defenderTransform.position - attackerPosition;
        knockDir.y = 0f;
        if (knockDir.sqrMagnitude < 0.001f)
            knockDir = -defenderTransform.forward;
        knockDir.Normalize();

        var cc = defenderTransform.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.Move(knockDir * GameConfig.GUARD_KNOCKBACK_DISTANCE);
        }
    }

    // ============================================================
    // クライアント通知
    // ============================================================

    /// <summary>
    /// ヒット確定を全クライアントに通知する
    /// クライアント側でヒットエフェクト・SE を再生する
    /// </summary>
    /// <param name="attackerNetId">攻撃者の NetworkObjectId</param>
    /// <param name="targetNetId">被弾者の NetworkObjectId</param>
    /// <param name="hitPosition">ヒット位置（エフェクト表示用）</param>
    [ClientRpc]
    private void NotifyHitClientRpc(ulong attackerNetId, ulong targetNetId, Vector3 hitPosition)
    {
        // TODO: ヒットエフェクト・SE の再生（M2-6 以降で実装）
        Debug.Log($"[Hit-Client] ヒット通知受信: attacker={attackerNetId} → target={targetNetId}" +
                  $" pos={hitPosition}");
    }

    /// <summary>
    /// ダメージ確定を全クライアントに通知する
    /// クライアント側でダメージ数値表示等に使用
    /// </summary>
    [ClientRpc]
    private void NotifyDamageClientRpc(ulong targetNetId, int damage, bool isCritical)
    {
        string critText = isCritical ? " ★CRITICAL★" : "";
        Debug.Log($"[Damage-Client] target={targetNetId} damage={damage}{critText}");
    }
}
