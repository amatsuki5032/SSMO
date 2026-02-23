using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// チーム管理（サーバー権威）
/// プレイヤー接続時にチームを自動振り分け（人数均等化）し、
/// NetworkList で全クライアントにチーム所属を同期する
///
/// 設計意図:
/// - NetworkList<TeamAssignment> で clientId → team のマッピングを同期
///   byte にシリアライズして帯域を節約する
/// - サーバーのみが書き込み権限を持つ（チート防止）
/// - 切断時は自動でリストから削除し、再接続時に再割り当て
/// </summary>
public class TeamManager : NetworkBehaviour
{
    // ============================================================
    // データ構造
    // ============================================================

    /// <summary>
    /// チーム割り当てエントリ（NetworkList 用）
    /// INetworkSerializable で帯域効率の良いシリアライズ
    /// </summary>
    public struct TeamAssignment : INetworkSerializable, System.IEquatable<TeamAssignment>
    {
        public ulong ClientId;
        public byte TeamId;  // 0 = Red, 1 = Blue（Team enum に対応）

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref TeamId);
        }

        public bool Equals(TeamAssignment other)
        {
            return ClientId == other.ClientId && TeamId == other.TeamId;
        }
    }

    // ============================================================
    // シングルトン
    // ============================================================

    public static TeamManager Instance { get; private set; }

    // ============================================================
    // 同期データ
    // ============================================================

    /// <summary>
    /// 全プレイヤーのチーム割り当て（サーバーのみ書き込み可）
    /// クライアントは読み取りでチーム情報を取得する
    /// </summary>
    private NetworkList<TeamAssignment> _teamAssignments;

    // サーバー側の高速ルックアップ用ローカルキャッシュ
    private readonly Dictionary<ulong, Team> _teamCache = new();
    private readonly List<ulong> _redTeam = new();
    private readonly List<ulong> _blueTeam = new();

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TeamManager] 重複インスタンスを破棄");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // NetworkList は Awake で初期化する必要がある（OnNetworkSpawn より前）
        _teamAssignments = new NetworkList<TeamAssignment>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // サーバー: 接続・切断コールバックを登録
            // NGO 2.x では OnConnectionEvent に統合されている
            NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;

            // ホスト自身が既に接続済みの場合を処理
            // （OnConnectionEvent はホスト起動時に発火しない場合がある）
            if (NetworkManager.Singleton.LocalClientId == NetworkManager.ServerClientId)
            {
                AssignTeam(NetworkManager.Singleton.LocalClientId);
            }
        }

        // クライアント: NetworkList の変更を監視してローカルキャッシュを同期
        _teamAssignments.OnListChanged += OnTeamAssignmentsChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        }

        _teamAssignments.OnListChanged -= OnTeamAssignmentsChanged;
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    // ============================================================
    // 接続・切断ハンドラ（サーバー専用）
    // ============================================================

    /// <summary>
    /// NGO 2.x 統合接続イベントハンドラ
    /// Connected / Disconnected を EventType で振り分ける
    /// </summary>
    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData eventData)
    {
        if (!IsServer) return;

        switch (eventData.EventType)
        {
            case ConnectionEvent.ClientConnected:
                OnClientConnected(eventData.ClientId);
                break;
            case ConnectionEvent.ClientDisconnected:
                OnClientDisconnected(eventData.ClientId);
                break;
        }
    }

    /// <summary>
    /// プレイヤー接続時: チームを自動振り分け
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        // 最大人数チェック
        if (_teamCache.Count >= GameConfig.MAX_PLAYERS)
        {
            Debug.LogWarning($"[TeamManager] 最大プレイヤー数 ({GameConfig.MAX_PLAYERS}) に達しているため {clientId} を拒否");
            NetworkManager.Singleton.DisconnectClient(clientId);
            return;
        }

        AssignTeam(clientId);
    }

    /// <summary>
    /// プレイヤー切断時: チームから除去
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        // NetworkList から該当エントリを削除
        for (int i = 0; i < _teamAssignments.Count; i++)
        {
            if (_teamAssignments[i].ClientId == clientId)
            {
                _teamAssignments.RemoveAt(i);
                break;
            }
        }

        // ローカルキャッシュからも削除
        if (_teamCache.TryGetValue(clientId, out Team team))
        {
            _teamCache.Remove(clientId);
            if (team == Team.Red)
                _redTeam.Remove(clientId);
            else
                _blueTeam.Remove(clientId);

            Debug.Log($"[TeamManager] Client {clientId} が {team} チームから離脱 (Red:{_redTeam.Count} / Blue:{_blueTeam.Count})");
        }
    }

    // ============================================================
    // チーム振り分けロジック（サーバー専用）
    // ============================================================

    /// <summary>
    /// 人数均等化でチームを割り当てる
    /// 同数の場合は Red 優先（ホストが最初に Red になる）
    /// </summary>
    private void AssignTeam(ulong clientId)
    {
        // 既に割り当て済みならスキップ
        if (_teamCache.ContainsKey(clientId)) return;

        // 人数が少ない方に割り当て（同数なら Red）
        Team assignedTeam = (_redTeam.Count <= _blueTeam.Count) ? Team.Red : Team.Blue;

        // チームサイズ上限チェック
        if (assignedTeam == Team.Red && _redTeam.Count >= GameConfig.TEAM_SIZE)
            assignedTeam = Team.Blue;
        else if (assignedTeam == Team.Blue && _blueTeam.Count >= GameConfig.TEAM_SIZE)
            assignedTeam = Team.Red;

        // NetworkList に追加（全クライアントに同期される）
        _teamAssignments.Add(new TeamAssignment
        {
            ClientId = clientId,
            TeamId = (byte)assignedTeam
        });

        // ローカルキャッシュ更新
        _teamCache[clientId] = assignedTeam;
        if (assignedTeam == Team.Red)
            _redTeam.Add(clientId);
        else
            _blueTeam.Add(clientId);

        Debug.Log($"[TeamManager] Client {clientId} → {assignedTeam} チーム (Red:{_redTeam.Count} / Blue:{_blueTeam.Count})");
    }

    // ============================================================
    // NetworkList 変更コールバック
    // ============================================================

    /// <summary>
    /// NetworkList の変更をローカルキャッシュに反映（クライアント用）
    /// サーバーは AssignTeam/OnClientDisconnected で直接キャッシュを更新済み
    /// </summary>
    private void OnTeamAssignmentsChanged(NetworkListEvent<TeamAssignment> changeEvent)
    {
        if (IsServer) return;  // サーバーは既にキャッシュ済み

        // キャッシュを全再構築（追加・削除・変更に対応）
        _teamCache.Clear();
        _redTeam.Clear();
        _blueTeam.Clear();

        for (int i = 0; i < _teamAssignments.Count; i++)
        {
            var entry = _teamAssignments[i];
            Team team = (Team)entry.TeamId;
            _teamCache[entry.ClientId] = team;
            if (team == Team.Red)
                _redTeam.Add(entry.ClientId);
            else
                _blueTeam.Add(entry.ClientId);
        }
    }

    // ============================================================
    // 公開 API
    // ============================================================

    /// <summary>
    /// 指定プレイヤーのチームを取得
    /// </summary>
    /// <param name="clientId">対象のクライアントID</param>
    /// <returns>所属チーム。未割り当ての場合は Team.Red を返す（安全策）</returns>
    public Team GetPlayerTeam(ulong clientId)
    {
        if (_teamCache.TryGetValue(clientId, out Team team))
            return team;

        Debug.LogWarning($"[TeamManager] Client {clientId} のチーム情報が見つかりません（デフォルト: Red）");
        return Team.Red;
    }

    /// <summary>
    /// 指定チームのメンバー一覧を取得
    /// </summary>
    /// <param name="team">対象チーム</param>
    /// <returns>該当チームの clientId リスト（コピー）</returns>
    public List<ulong> GetTeamMembers(Team team)
    {
        return team == Team.Red
            ? new List<ulong>(_redTeam)
            : new List<ulong>(_blueTeam);
    }

    /// <summary>
    /// 指定チームの現在の人数を取得
    /// </summary>
    public int GetTeamCount(Team team)
    {
        return team == Team.Red ? _redTeam.Count : _blueTeam.Count;
    }

    /// <summary>
    /// 2つのプレイヤーが同じチームかどうかを判定
    /// 味方へのフレンドリーファイア防止に使用
    /// </summary>
    public bool IsSameTeam(ulong clientIdA, ulong clientIdB)
    {
        return GetPlayerTeam(clientIdA) == GetPlayerTeam(clientIdB);
    }
}
