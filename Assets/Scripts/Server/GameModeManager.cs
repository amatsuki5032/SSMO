using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ゲームモード管理（サーバー権威型）
///
/// 設計意図:
/// - ゲームフェーズを3段階で管理: WaitingForPlayers → InProgress → GameOver
/// - 開始条件: 両チームに MIN_PLAYERS_TO_START / 2 人以上（仮: 合計2人以上）
/// - 終了条件: タイマー0 or 片方スコアが勝利（仮）
/// - チーム別撃破数スコアを NetworkVariable で全クライアントに同期
/// - 終了時に ClientRpc で勝敗を通知
/// </summary>
public class GameModeManager : NetworkBehaviour
{
    // ============================================================
    // シングルトン
    // ============================================================

    public static GameModeManager Instance { get; private set; }

    // ============================================================
    // 同期変数（サーバー書き込み、全員読み取り）
    // ============================================================

    /// <summary>ゲームフェーズ</summary>
    private readonly NetworkVariable<GamePhase> _phase = new(
        GamePhase.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>残り時間（秒）</summary>
    private readonly NetworkVariable<float> _remainingTime = new(
        GameConfig.MATCH_TIME_SECONDS,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>赤チーム撃破数</summary>
    private readonly NetworkVariable<int> _redKills = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>青チーム撃破数</summary>
    private readonly NetworkVariable<int> _blueKills = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>勝利チーム（GameOver時に設定。-1=未決定, 0=Red, 1=Blue, 2=Draw）</summary>
    private readonly NetworkVariable<int> _winnerTeam = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    public GamePhase Phase => _phase.Value;
    public float RemainingTime => _remainingTime.Value;
    public int RedKills => _redKills.Value;
    public int BlueKills => _blueKills.Value;
    public int WinnerTeam => _winnerTeam.Value;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameModeManager] 重複インスタンスを破棄");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _phase.Value = GamePhase.WaitingForPlayers;
            _remainingTime.Value = GameConfig.MATCH_TIME_SECONDS;
            _redKills.Value = 0;
            _blueKills.Value = 0;
            _winnerTeam.Value = -1;
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    // ============================================================
    // 毎フレーム更新（サーバー専用）
    // ============================================================

    private void Update()
    {
        if (!IsServer) return;
        if (!IsSpawned) return;

        switch (_phase.Value)
        {
            case GamePhase.WaitingForPlayers:
                CheckStartCondition();
                break;
            case GamePhase.InProgress:
                UpdateTimer();
                break;
            // GameOver: 何もしない
        }
    }

    // ============================================================
    // フェーズ処理
    // ============================================================

    /// <summary>
    /// 開始条件チェック: 両チームに1人以上いれば試合開始
    /// </summary>
    private void CheckStartCondition()
    {
        if (TeamManager.Instance == null) return;

        int redCount = TeamManager.Instance.GetTeamCount(Team.Red);
        int blueCount = TeamManager.Instance.GetTeamCount(Team.Blue);

        // 両チームに1人以上（合計 MIN_PLAYERS_TO_START 以上）
        if (redCount >= 1 && blueCount >= 1 && (redCount + blueCount) >= GameConfig.MIN_PLAYERS_TO_START)
        {
            StartMatch();
        }
    }

    /// <summary>
    /// 試合開始
    /// </summary>
    private void StartMatch()
    {
        _phase.Value = GamePhase.InProgress;
        _remainingTime.Value = GameConfig.MATCH_TIME_SECONDS;
        Debug.Log("[GameModeManager] 試合開始！");
        NotifyGameStartClientRpc();
    }

    /// <summary>
    /// タイマー更新。0になったら試合終了
    /// </summary>
    private void UpdateTimer()
    {
        _remainingTime.Value -= Time.deltaTime;

        if (_remainingTime.Value <= 0f)
        {
            _remainingTime.Value = 0f;
            EndMatch();
        }
    }

    /// <summary>
    /// 試合終了。スコア比較で勝敗決定
    /// </summary>
    private void EndMatch()
    {
        _phase.Value = GamePhase.GameOver;

        // 勝敗判定: 撃破数が多い方が勝ち
        int winner;
        if (_redKills.Value > _blueKills.Value)
            winner = 0; // Red 勝利
        else if (_blueKills.Value > _redKills.Value)
            winner = 1; // Blue 勝利
        else
            winner = 2; // 引き分け

        _winnerTeam.Value = winner;

        string resultText = winner switch
        {
            0 => "Red チーム勝利",
            1 => "Blue チーム勝利",
            _ => "引き分け"
        };
        Debug.Log($"[GameModeManager] 試合終了！ {resultText} (Red:{_redKills.Value} vs Blue:{_blueKills.Value})");

        NotifyGameOverClientRpc(winner);
    }

    // ============================================================
    // 公開 API（サーバー側から呼ばれる）
    // ============================================================

    /// <summary>
    /// プレイヤー撃破時にスコアを加算する
    /// HealthSystem の Dead 遷移時に呼ぶ想定
    /// </summary>
    /// <param name="killerClientId">撃破したプレイヤーの clientId</param>
    public void AddKill(ulong killerClientId)
    {
        if (!IsServer) return;
        if (_phase.Value != GamePhase.InProgress) return;
        if (TeamManager.Instance == null) return;

        Team killerTeam = TeamManager.Instance.GetPlayerTeam(killerClientId);
        if (killerTeam == Team.Red)
            _redKills.Value++;
        else
            _blueKills.Value++;

        Debug.Log($"[GameModeManager] {killerTeam} チームが撃破 (Red:{_redKills.Value} vs Blue:{_blueKills.Value})");
    }

    // ============================================================
    // ClientRpc（全クライアントに通知）
    // ============================================================

    /// <summary>
    /// 試合開始を全クライアントに通知
    /// </summary>
    [ClientRpc]
    private void NotifyGameStartClientRpc()
    {
        Debug.Log("[GameModeManager] 試合開始通知を受信");
    }

    /// <summary>
    /// 試合終了を全クライアントに通知
    /// </summary>
    /// <param name="winner">0=Red, 1=Blue, 2=Draw</param>
    [ClientRpc]
    private void NotifyGameOverClientRpc(int winner)
    {
        string resultText = winner switch
        {
            0 => "Red チーム勝利！",
            1 => "Blue チーム勝利！",
            _ => "引き分け！"
        };
        Debug.Log($"[GameModeManager] 試合終了通知: {resultText}");
    }
}
