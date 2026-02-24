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
    private CharacterStateMachine _stateMachine;
    private CharacterController _characterController;
    private PlayerMovement _playerMovement;
    private ElementSystem _elementSystem;

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
        _stateMachine = GetComponent<CharacterStateMachine>();
        _characterController = GetComponent<CharacterController>();
        _playerMovement = GetComponent<PlayerMovement>();
        _elementSystem = GetComponent<ElementSystem>();
    }

    /// <summary>現在の武器種を取得する</summary>
    private WeaponType GetWeaponType()
    {
        return _playerMovement != null ? _playerMovement.CurrentWeaponType : WeaponType.GreatSword;
    }

    /// <summary>
    /// サーバーのみ: 毎 FixedUpdate でヒット判定 + 無双前進移動
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;
        CheckHitbox();
        ApplyMusouAdvance();
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

        // 現在の攻撃に対応する HitboxData を取得（武器種リーチ反映）
        HitboxData hitbox = HitboxData.GetHitboxData(comboStep, chargeType, isDash, isRush, GetWeaponType());
        if (hitbox.ActiveEndFrame == 0) return; // データが無い

        // 現在のフレーム番号を計算（経過時間 → フレーム）
        float elapsed = _comboSystem.SegmentElapsed;
        int currentFrame = Mathf.FloorToInt(elapsed / GameConfig.FIXED_DELTA_TIME);

        // アクティブフレーム外ならスキップ
        if (currentFrame < hitbox.ActiveStartFrame || currentFrame > hitbox.ActiveEndFrame) return;

        // 攻撃前進移動: アクティブフレーム中にキャラ前方へ移動
        // 合計距離をアクティブフレーム数で按分し、毎フレーム均等に移動する
        ApplyAttackAdvance(comboStep, chargeType, isDash, isRush, hitbox);

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

            // プレイヤーヒット判定
            if (hurtbox != null)
            {
                ProcessPlayerHit(hurtbox);
                continue;
            }

            // NPC兵士ヒット判定
            var npcSoldier = _hitResults[i].GetComponent<NPCSoldier>();
            if (npcSoldier != null)
            {
                ProcessNPCHit(npcSoldier);
                continue;
            }
        }
    }

    // ============================================================
    // プレイヤーヒット処理
    // ============================================================

    /// <summary>
    /// プレイヤー（HurtboxComponent持ち）へのヒット処理
    /// ガード判定・リアクション・ダメージ計算の全処理を含む
    /// </summary>
    private void ProcessPlayerHit(HurtboxComponent hurtbox)
    {
        // 自分自身を除外
        if (hurtbox.NetworkObjectId == NetworkObjectId) return;

        // 無敵状態を除外
        if (hurtbox.IsInvincible()) return;

        // 1攻撃1ヒット: 既にヒット済みならスキップ
        if (_hitTargetsThisAttack.Contains(hurtbox.NetworkObjectId)) return;

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
                // EGカウンター時はダメージもノックバックも無し（カウンターで反撃するため）
                return;
            }

            Debug.Log($"[Guard] {hurtbox.gameObject.name} ガード成功（{gameObject.name} の攻撃）→ ダメージ0");

            // ガード成功: ダメージ完全カット + ノックバックのみ
            // EG準備中はノックバックなし（その場で完全に受け止める）
            bool isEGState = hurtbox.GetComponent<EGSystem>()?.IsInEGState ?? false;
            if (!isEGState)
            {
                ApplyGuardKnockback(hurtbox.transform, transform.position);
            }

            // 攻撃者の無双ゲージ増加（ガードされても攻撃ヒット扱い）
            var attackerGaugeOnGuard = GetComponent<MusouGauge>();
            if (attackerGaugeOnGuard != null)
                attackerGaugeOnGuard.AddGauge(GameConfig.MUSOU_GAIN_ON_HIT);

            return; // ダメージ0 → HP減少・ダメージ通知をスキップ
        }

        // ガード失敗（めくり）or 非ガード: 通常リアクション適用
        if (hurtbox.IsGuarding())
        {
            Debug.Log($"[Guard] めくり！ {hurtbox.gameObject.name} のガードを貫通");
        }

        var targetReaction = hurtbox.GetComponent<ReactionSystem>();
        if (targetReaction != null)
        {
            HitReaction reaction = ReactionSystem.GetReactionType(comboStep, chargeType, isDash);
            AttackLevel attackLevel = GetAttackLevel(comboStep, chargeType);
            targetReaction.ApplyReaction(reaction, transform.position, comboStep, chargeType, attackLevel);
        }

        // ダメージ計算・HP減少（ガード成功時はここに到達しない）
        var targetHealth = hurtbox.GetComponent<HealthSystem>();
        if (targetHealth != null)
        {
            bool isRush = _comboSystem.IsRush;
            float motionMultiplier = DamageCalculator.GetMotionMultiplier(comboStep, chargeType, isDash, isRush, GetWeaponType());

            // 被弾者のステートから空中かを判定
            var targetStateMachine = hurtbox.GetComponent<CharacterStateMachine>();
            bool isAirborne = targetStateMachine != null &&
                (targetStateMachine.CurrentState == CharacterState.Launch ||
                 targetStateMachine.CurrentState == CharacterState.AirHitstun ||
                 targetStateMachine.CurrentState == CharacterState.AirRecover ||
                 targetStateMachine.CurrentState == CharacterState.JumpAttack ||
                 targetStateMachine.CurrentState == CharacterState.Jump);

            // 属性情報取得（チャージ攻撃の場合のみ属性が乗る）
            ElementType attackElement = ElementType.None;
            int attackElementLevel = 0;
            if (_elementSystem != null)
                _elementSystem.GetAttackElement(chargeType, out attackElement, out attackElementLevel);

            var damageResult = DamageCalculator.Calculate(
                GameConfig.DEFAULT_ATK,
                motionMultiplier,
                GameConfig.DEFAULT_DEF,
                targetHealth.GetHpRatio(),
                element: attackElement,
                elementLevel: attackElementLevel,
                isAirborne: isAirborne
            );

            targetHealth.TakeDamage(damageResult.HpDamage);

            // 斬属性: 無双ゲージにもダメージ + 攻撃側も無双減少
            if (damageResult.MusouDamage > 0)
            {
                // 被弾側: 外部要因によるゲージ減少（ReduceGauge）
                var targetGaugeForSlash = hurtbox.GetComponent<MusouGauge>();
                if (targetGaugeForSlash != null)
                    targetGaugeForSlash.ReduceGauge(damageResult.MusouDamage);

                // 攻撃側: 斬属性のデメリットとしてゲージ消費（ConsumeGauge）
                var attackerGaugeForSlash = GetComponent<MusouGauge>();
                if (attackerGaugeForSlash != null)
                    attackerGaugeForSlash.ConsumeGauge(damageResult.AttackerMusouCost);
            }

            // ダメージ通知（クリティカル情報を含む）
            NotifyDamageClientRpc(hurtbox.NetworkObjectId, damageResult.HpDamage, damageResult.IsCritical);

            // 無双ゲージ増加: 攻撃者ヒット + 被弾者ダメージ
            var attackerGauge = GetComponent<MusouGauge>();
            if (attackerGauge != null)
                attackerGauge.AddGauge(GameConfig.MUSOU_GAIN_ON_HIT);

            var targetGauge = hurtbox.GetComponent<MusouGauge>();
            if (targetGauge != null)
                targetGauge.AddGauge(GameConfig.MUSOU_GAIN_ON_DAMAGE);

            // 属性による状態異常付与（チャージ攻撃のみ。属性がNoneなら何もしない）
            if (attackElement != ElementType.None)
            {
                var targetStatusEffect = hurtbox.GetComponent<StatusEffectManager>();
                if (targetStatusEffect != null)
                    targetStatusEffect.ApplyElementEffect(attackElement, attackElementLevel, isAirborne);
            }

            // 感電中のコンボカウント更新（受け身不可の解除条件）
            {
                var targetStatusEffect = hurtbox.GetComponent<StatusEffectManager>();
                if (targetStatusEffect != null && targetStatusEffect.IsElectrified)
                    targetStatusEffect.OnElectrifiedHit();
            }
        }
    }

    // ============================================================
    // NPC兵士ヒット処理
    // ============================================================

    /// <summary>
    /// NPC兵士（NPCSoldier）へのヒット処理
    /// ガード・リアクション・アーマーなし。シンプルにダメージのみ適用
    /// 味方NPCへのフレンドリーファイアは無効
    /// </summary>
    private void ProcessNPCHit(NPCSoldier npcSoldier)
    {
        // 死亡済みNPCは除外
        if (npcSoldier.IsDead) return;

        // 1攻撃1ヒット: 既にヒット済みならスキップ
        if (_hitTargetsThisAttack.Contains(npcSoldier.NetworkObjectId)) return;

        // 味方NPC除外（フレンドリーファイア防止）
        if (TeamManager.Instance != null)
        {
            Team attackerTeam = TeamManager.Instance.GetPlayerTeam(OwnerClientId);
            if (npcSoldier.SoldierTeam == attackerTeam) return;
        }

        // ヒット確定
        _hitTargetsThisAttack.Add(npcSoldier.NetworkObjectId);

        int comboStep = _comboSystem.ComboStep;
        int chargeType = _comboSystem.ChargeType;
        bool isDash = _comboSystem.IsDashAttacking;
        bool isRush = _comboSystem.IsRush;

        Debug.Log($"[Hit-NPC] {gameObject.name} → {npcSoldier.gameObject.name} ヒット確定" +
                  $" (N{comboStep}/C{chargeType}/D={isDash})");

        // NPC向け簡易ダメージ計算: ATK × モーション倍率（ガード・根性補正なし）
        float motionMultiplier = DamageCalculator.GetMotionMultiplier(comboStep, chargeType, isDash, isRush, GetWeaponType());
        int damage = Mathf.Max(1, Mathf.RoundToInt(GameConfig.DEFAULT_ATK * motionMultiplier));

        npcSoldier.TakeDamage(damage);

        // ヒット通知
        Vector3 hitPoint = npcSoldier.transform.position;
        NotifyHitClientRpc(NetworkObjectId, npcSoldier.NetworkObjectId, hitPoint);
        NotifyDamageClientRpc(npcSoldier.NetworkObjectId, damage, false);

        // 攻撃者の無双ゲージ増加（NPC撃破でもゲージが溜まる）
        var attackerGauge = GetComponent<MusouGauge>();
        if (attackerGauge != null)
            attackerGauge.AddGauge(GameConfig.MUSOU_GAIN_ON_HIT);
    }

    // ============================================================
    // 攻撃レベル判定
    // ============================================================

    /// <summary>
    /// 攻撃種別に応じた攻撃レベルを返す（アーマー判定用）
    /// 無双中は Musou、チャージは Charge、それ以外は Normal
    /// </summary>
    private AttackLevel GetAttackLevel(int comboStep, int chargeType)
    {
        // 無双中は攻撃レベル4（最高）
        var state = _stateMachine.CurrentState;
        if (state == CharacterState.Musou || state == CharacterState.TrueMusou)
            return AttackLevel.Musou;

        // チャージ攻撃は攻撃レベル3
        if (chargeType > 0)
            return AttackLevel.Charge;

        // 通常攻撃・ダッシュ攻撃は攻撃レベル2
        return AttackLevel.Normal;
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

        // BattleHUD にターゲット通知
        BattleHUD.OnHitNotified?.Invoke(attackerNetId, targetNetId);
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

    // ============================================================
    // 攻撃前進移動（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// アクティブフレーム中にキャラ前方へ前進移動を適用する
    /// 合計距離をアクティブフレーム数で按分し、毎フレーム均等に移動
    /// CheckHitbox のアクティブフレーム判定通過後に呼ばれる
    /// </summary>
    private void ApplyAttackAdvance(int comboStep, int chargeType, bool isDash, bool isRush, HitboxData hitbox)
    {
        if (_characterController == null) return;

        float totalDistance = GetAdvanceDistance(comboStep, chargeType, isDash, isRush);
        if (totalDistance <= 0f) return;

        int activeFrames = hitbox.ActiveEndFrame - hitbox.ActiveStartFrame + 1;
        if (activeFrames <= 0) return;

        // 毎フレームの移動量 = 合計距離 / アクティブフレーム数
        float perFrameDistance = totalDistance / activeFrames;
        _characterController.Move(transform.forward * perFrameDistance);
    }

    /// <summary>
    /// 無双乱舞中の前進移動（毎FixedUpdate）
    /// 無双はComboSystemを経由しないため独立して処理する
    /// </summary>
    private void ApplyMusouAdvance()
    {
        if (_characterController == null) return;

        var state = _stateMachine.CurrentState;
        if (state != CharacterState.Musou && state != CharacterState.TrueMusou) return;

        // 無双中は毎フレーム一定量ずつ前進（各ヒット0.15mを60Hzで按分）
        // 仮のヒット間隔: 6フレーム（≒0.1秒に1ヒット）
        float perFrameDistance = GameConfig.ADVANCE_MUSOU_HIT / 6f;
        _characterController.Move(transform.forward * perFrameDistance);
    }

    /// <summary>
    /// 攻撃種別に応じた前進距離を返す
    /// </summary>
    private static float GetAdvanceDistance(int comboStep, int chargeType, bool isDash, bool isRush)
    {
        // ダッシュ攻撃（ラッシュ追加ヒットも同じ距離）
        if (isDash)
            return isRush ? GameConfig.ADVANCE_DASH_ATTACK * 0.3f : GameConfig.ADVANCE_DASH_ATTACK;

        // チャージ攻撃
        if (chargeType > 0)
        {
            if (chargeType == 3 && isRush)
                return GameConfig.ADVANCE_C3_RUSH;

            return chargeType switch
            {
                1 => GameConfig.ADVANCE_C1,
                2 => GameConfig.ADVANCE_C2,
                3 => GameConfig.ADVANCE_C3_RUSH, // C3 初段もラッシュと同じ
                4 => GameConfig.ADVANCE_C4,
                5 => GameConfig.ADVANCE_C5,
                _ => 0f,
            };
        }

        // 通常攻撃
        return comboStep switch
        {
            1 => GameConfig.ADVANCE_N1,
            2 => GameConfig.ADVANCE_N2,
            3 => GameConfig.ADVANCE_N3,
            4 => GameConfig.ADVANCE_N4,
            _ => 0f,
        };
    }
}
