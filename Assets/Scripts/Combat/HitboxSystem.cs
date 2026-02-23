using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー権威型ヒット判定システム
///
/// 攻撃ステート中にキャラ前方のカプセル領域を走査し、
/// HurtboxComponent を持つ対象とのヒットを判定する
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
    // ヒット判定（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 現在の攻撃に対応する Hitbox をチェックし、範囲内の対象にヒットを適用する
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

        // カプセル領域を計算（キャラ位置 + オフセット + 前方 × Length）
        Vector3 basePos = transform.position + transform.rotation * hitbox.Offset;
        Vector3 endPos = basePos + transform.forward * hitbox.Length;

        // Physics.OverlapCapsule で範囲内の Collider を取得
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            basePos, endPos, hitbox.Radius, _hitResults
        );

        // ヒット対象のフィルタリング
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
            Debug.Log($"[Hit] {gameObject.name} → {hurtbox.gameObject.name} ヒット");

            // TODO: DamageSystem にヒット情報を送信（M2-5b で実装）
        }
    }
}
