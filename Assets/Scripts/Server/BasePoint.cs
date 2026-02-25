using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 拠点システム（サーバー権威型）
///
/// 設計意図:
/// - 各拠点は SphereCollider (isTrigger) でエリア検出
/// - OnTriggerStay でエリア内プレイヤーを毎フレーム収集
/// - 制圧判定: 片方チームのみエリア内 → ゲージ上昇 → 制圧完了
///   両チーム混在時はゲージ停止（中立化もしない）
/// - 味方拠点内のプレイヤーにHP自動回復（毎秒）
/// - NetworkVariable で所属チーム・制圧ゲージを全クライアントに同期
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class BasePoint : NetworkBehaviour
{
    // ============================================================
    // 設定
    // ============================================================

    [Header("拠点設定")]
    [SerializeField] private int _baseIndex;  // 拠点番号（0-4、MapGenerator で設定）

    // ============================================================
    // 同期変数（サーバー書き込み、全員読み取り）
    // ============================================================

    /// <summary>拠点の所属チーム</summary>
    private readonly NetworkVariable<BaseStatus> _status = new(
        BaseStatus.Neutral,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// 制圧ゲージ（0〜1）
    /// 正方向 = Redが制圧中、負方向 = Blueが制圧中
    /// 絶対値が1.0に達したら制圧完了
    /// </summary>
    private readonly NetworkVariable<float> _captureProgress = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    public BaseStatus Status => _status.Value;
    public float CaptureProgress => _captureProgress.Value;
    public int BaseIndex => _baseIndex;

    // ============================================================
    // サーバー側ローカル変数
    // ============================================================

    // エリア内プレイヤーをフレーム毎に収集（GC回避のため再利用）
    private readonly HashSet<ulong> _redPlayersInArea = new();
    private readonly HashSet<ulong> _bluePlayersInArea = new();

    // HP回復用タイマー（1秒間隔）
    private float _regenTimer;

    // ============================================================
    // ライフサイクル
    // ============================================================

    public override void OnNetworkSpawn()
    {
        // SphereCollider をエリア検出用に設定
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = GameConfig.BASE_CAPTURE_RADIUS;
    }

    // ============================================================
    // トリガー検出（サーバー側のみ処理）
    // ============================================================

    /// <summary>
    /// エリア内に留まっているプレイヤーを毎物理フレームで収集
    /// FixedUpdate 後にまとめて判定する
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;

        // NetworkObject を持つプレイヤーのみ対象
        var netObj = other.GetComponent<NetworkObject>();
        if (netObj == null) return;

        // TeamManager からチームを取得
        if (TeamManager.Instance == null) return;
        var team = TeamManager.Instance.GetPlayerTeam(netObj.OwnerClientId);

        // Dead 状態のプレイヤーは除外
        var stateMachine = other.GetComponent<CharacterStateMachine>();
        if (stateMachine != null && stateMachine.CurrentState == CharacterState.Dead)
            return;

        if (team == Team.Red)
            _redPlayersInArea.Add(netObj.OwnerClientId);
        else
            _bluePlayersInArea.Add(netObj.OwnerClientId);
    }

    // ============================================================
    // 毎フレーム更新（サーバー専用）
    // ============================================================

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (!IsSpawned) return;

        UpdateCapture();
        UpdateHpRegen();

        // 次フレーム用にクリア（OnTriggerStay で再収集される）
        _redPlayersInArea.Clear();
        _bluePlayersInArea.Clear();
    }

    // ============================================================
    // 制圧判定
    // ============================================================

    /// <summary>
    /// 制圧ゲージの更新
    /// - 片方チームのみ → そのチーム方向にゲージ上昇
    /// - 両チーム混在 or 誰もいない → ゲージ停止
    /// </summary>
    private void UpdateCapture()
    {
        int redCount = _redPlayersInArea.Count;
        int blueCount = _bluePlayersInArea.Count;

        // 誰もいない or 両チーム混在 → 変化なし
        if ((redCount == 0 && blueCount == 0) || (redCount > 0 && blueCount > 0))
            return;

        float delta = GameConfig.FIXED_DELTA_TIME / GameConfig.BASE_CAPTURE_TIME;

        if (redCount > 0)
        {
            // Redが制圧中: 正方向に進行
            float newProgress = _captureProgress.Value + delta;

            if (newProgress >= 1f)
            {
                // Red制圧完了
                _captureProgress.Value = 0f;
                if (_status.Value != BaseStatus.Red)
                {
                    _status.Value = BaseStatus.Red;
                    Debug.Log($"[BasePoint] 拠点{_baseIndex} が Red に制圧された");
                }
            }
            else
            {
                _captureProgress.Value = newProgress;
            }
        }
        else // blueCount > 0
        {
            // Blueが制圧中: 負方向に進行
            float newProgress = _captureProgress.Value - delta;

            if (newProgress <= -1f)
            {
                // Blue制圧完了
                _captureProgress.Value = 0f;
                if (_status.Value != BaseStatus.Blue)
                {
                    _status.Value = BaseStatus.Blue;
                    Debug.Log($"[BasePoint] 拠点{_baseIndex} が Blue に制圧された");
                }
            }
            else
            {
                _captureProgress.Value = newProgress;
            }
        }
    }

    // ============================================================
    // HP自動回復
    // ============================================================

    /// <summary>
    /// 味方チームが所有する拠点内のプレイヤーにHP回復を適用
    /// 1秒間隔で回復（毎フレーム回復すると細かすぎるため）
    /// </summary>
    private void UpdateHpRegen()
    {
        // 中立拠点では回復しない
        if (_status.Value == BaseStatus.Neutral) return;

        _regenTimer += GameConfig.FIXED_DELTA_TIME;
        if (_regenTimer < GameConfig.BASE_HP_REGEN_INTERVAL) return;
        _regenTimer -= GameConfig.BASE_HP_REGEN_INTERVAL;

        // 拠点所属チームの味方プレイヤーのみ回復
        HashSet<ulong> allyPlayers = _status.Value == BaseStatus.Red
            ? _redPlayersInArea
            : _bluePlayersInArea;

        if (allyPlayers.Count == 0) return;

        int healAmount = Mathf.RoundToInt(GameConfig.BASE_HP_REGEN_RATE);

        foreach (ulong clientId in allyPlayers)
        {
            // NetworkManager から該当プレイヤーの NetworkObject を取得
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    GetPlayerNetworkObjectId(clientId), out var playerObj))
                continue;

            var health = playerObj.GetComponent<HealthSystem>();
            if (health == null) continue;

            // 死亡中は回復しない
            var stateMachine = playerObj.GetComponent<CharacterStateMachine>();
            if (stateMachine != null && stateMachine.CurrentState == CharacterState.Dead)
                continue;

            // HP が満タンならスキップ
            if (health.CurrentHp >= health.MaxHp) continue;

            health.Heal(healAmount);
        }
    }

    /// <summary>
    /// clientId から対応するプレイヤーの NetworkObjectId を取得する
    /// ConnectedClients から PlayerObject を参照する
    /// </summary>
    private ulong GetPlayerNetworkObjectId(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
                return client.PlayerObject.NetworkObjectId;
        }
        return 0;
    }

    // ============================================================
    // 公開 API
    // ============================================================

    /// <summary>
    /// 拠点番号を設定する（MapGenerator から呼ばれる）
    /// </summary>
    public void SetBaseIndex(int index)
    {
        _baseIndex = index;
    }

    /// <summary>
    /// 拠点の初期所属を設定する（サーバー専用）
    /// ゲーム開始時にチーム側拠点の初期状態を設定するのに使用
    /// </summary>
    public void SetInitialStatus(BaseStatus status)
    {
        // spawn前（MapGeneratorのAwake）でも初期値設定を許可する
        // NetworkVariable は spawn 前に設定した値が初期値として同期される
        if (IsSpawned && !IsServer) return;
        _status.Value = status;
        _captureProgress.Value = 0f;
        Debug.Log($"[BasePoint] 拠点{_baseIndex} 初期所属: {status}");
    }
}
