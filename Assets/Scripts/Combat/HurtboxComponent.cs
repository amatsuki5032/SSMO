using Unity.Netcode;

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
}
