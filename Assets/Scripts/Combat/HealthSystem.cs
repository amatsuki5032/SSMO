using Unity.Netcode;
using UnityEngine;

/// <summary>
/// HP管理（サーバー権威型）
///
/// - NetworkVariable で HP を全クライアントに同期
/// - ダメージ適用はサーバー側でのみ実行
/// - HP 0 で Dead ステートに遷移
/// </summary>
[RequireComponent(typeof(CharacterStateMachine))]
public class HealthSystem : NetworkBehaviour
{
    // ============================================================
    // 同期変数（サーバー書き込み、全員読み取り）
    // ============================================================

    private readonly NetworkVariable<int> _currentHp = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> _maxHp = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    public int CurrentHp => _currentHp.Value;
    public int MaxHp => _maxHp.Value;

    // ============================================================
    // 参照
    // ============================================================

    private CharacterStateMachine _stateMachine;

    // リスポーンタイマー（サーバーのみ。Dead遷移後にカウントダウンして自動リスポーン）
    private float _respawnTimer;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _maxHp.Value = GameConfig.DEFAULT_MAX_HP;
            _currentHp.Value = GameConfig.DEFAULT_MAX_HP;
        }
    }

    // ============================================================
    // リスポーンタイマー（★サーバー側で実行★）
    // ============================================================

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (_respawnTimer <= 0f) return;

        _respawnTimer -= GameConfig.FIXED_DELTA_TIME;
        if (_respawnTimer > 0f) return;

        _respawnTimer = 0f;
        ExecuteRespawn();
    }

    /// <summary>
    /// Dead後の自動リスポーンを実行する
    /// SpawnManager.RespawnPlayer で HP全回復・無双MAX・強化リセット・テレポートを行う
    /// </summary>
    private void ExecuteRespawn()
    {
        var networkObject = GetComponent<NetworkObject>();
        if (networkObject == null) return;

        if (SpawnManager.Instance != null)
        {
            Debug.Log($"[HP] {gameObject.name} 自動リスポーン実行");
            SpawnManager.Instance.RespawnPlayer(networkObject);
        }
        else
        {
            // SpawnManager がない場合のフォールバック（テスト環境等）
            Debug.LogWarning("[HP] SpawnManager が見つかりません。最小限のリスポーンを実行");
            FullHeal();
            _stateMachine.ForceState(CharacterState.Idle);
        }
    }

    // ============================================================
    // ダメージ適用（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// ダメージを適用してHPを減少させる
    /// HP が 0 以下になったら Dead ステートに遷移する
    /// </summary>
    /// <param name="damage">適用するダメージ量（正の値）</param>
    public void TakeDamage(int damage)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[HP] TakeDamage はサーバー側でのみ実行可能");
            return;
        }

        if (damage <= 0) return;

        // 既に死亡済みならスキップ
        if (_stateMachine.CurrentState == CharacterState.Dead) return;

        _currentHp.Value = Mathf.Max(0, _currentHp.Value - damage);

        Debug.Log($"[HP] {gameObject.name} が {damage} ダメージ → 残HP: {_currentHp.Value}/{_maxHp.Value}");

        // HP 0 → 死亡 → リスポーンタイマー開始
        if (_currentHp.Value <= 0)
        {
            Debug.Log($"[HP] {gameObject.name} 死亡");
            if (_stateMachine.TryChangeState(CharacterState.Dead))
            {
                _respawnTimer = GameConfig.RESPAWN_DELAY;
            }
        }
    }

    // ============================================================
    // HP回復（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// HPを全回復する（デバッグ・リスポーン用）
    /// </summary>
    public void FullHeal()
    {
        if (!IsServer) return;
        _currentHp.Value = _maxHp.Value;
    }

    /// <summary>
    /// 指定量だけHPを回復する（拠点回復等で使用）
    /// 最大HPを超えない
    /// </summary>
    /// <param name="amount">回復量（正の値）</param>
    public void Heal(int amount)
    {
        if (!IsServer) return;
        if (amount <= 0) return;
        if (_stateMachine.CurrentState == CharacterState.Dead) return;

        _currentHp.Value = Mathf.Min(_currentHp.Value + amount, _maxHp.Value);
    }

    // ============================================================
    // HP比率（根性補正判定用）
    // ============================================================

    /// <summary>
    /// 現在HP / 最大HP を返す（0.0〜1.0）
    /// 根性補正の判定に使用する
    /// </summary>
    public float GetHpRatio()
    {
        if (_maxHp.Value <= 0) return 0f;
        return (float)_currentHp.Value / _maxHp.Value;
    }
}
