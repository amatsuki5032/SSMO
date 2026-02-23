using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 被弾判定コンポーネント
/// 各プレイヤーの NetworkPlayer Prefab に付与する
/// CharacterController の Collider を判定に使用（追加 Collider は不要）
/// HitboxSystem がこのコンポーネントを検索してヒット対象を特定する
/// </summary>
[RequireComponent(typeof(CharacterStateMachine))]
public class HurtboxComponent : NetworkBehaviour
{
    private CharacterStateMachine _stateMachine;

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
    }

    /// <summary>
    /// 現在無敵状態か。サーバー側の判定に使用
    /// 無双乱舞・起き上がり・ジャンプ離陸等の無敵をチェック
    /// </summary>
    public bool IsInvincible()
    {
        return _stateMachine != null && _stateMachine.IsInvincible;
    }

    /// <summary>
    /// ガード中かどうか（Guard / GuardMove / EGPrepare / EGReady）
    /// EG準備中・完成中もガードは有効
    /// </summary>
    public bool IsGuarding()
    {
        if (_stateMachine == null) return false;
        var state = _stateMachine.CurrentState;
        return state == CharacterState.Guard
            || state == CharacterState.GuardMove
            || state == CharacterState.EGPrepare
            || state == CharacterState.EGReady;
    }

    /// <summary>
    /// 攻撃者の位置に対してガードが有効か判定する（サーバー側）
    /// 正面180度（±90度）以内の攻撃のみガード成功
    /// 背面・側面からの攻撃（めくり）はガード貫通
    /// </summary>
    /// <param name="attackerPosition">攻撃者のワールド座標</param>
    /// <returns>true: ガード成功 / false: めくり（ガード貫通）</returns>
    public bool IsGuardingAgainst(Vector3 attackerPosition)
    {
        if (!IsGuarding()) return false;

        // 被弾者の正面方向と攻撃者への方向を比較
        Vector3 toAttacker = attackerPosition - transform.position;
        toAttacker.y = 0f; // 水平方向のみで判定
        if (toAttacker.sqrMagnitude < 0.001f) return false; // 重なっている場合はガード貫通

        float angle = Vector3.Angle(transform.forward, toAttacker.normalized);
        return angle <= GameConfig.GUARD_ANGLE * 0.5f; // 正面180度 = ±90度
    }
}
