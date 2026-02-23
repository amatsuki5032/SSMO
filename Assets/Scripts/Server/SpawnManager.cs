using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// チーム別スポーン地点管理（サーバー権威）
///
/// 設計意図:
/// - チームごとに2つのスポーンポイントを持ち、ラウンドロビンで交互に使用
/// - リスポーン時は前回と異なる拠点を使用（交互拠点制限）
/// - リスポーン時にHP全回復 + 無双ゲージMAX（combat-spec準拠）
/// - TeamManager と連携してチーム情報を取得する
/// </summary>
public class SpawnManager : NetworkBehaviour
{
    // ============================================================
    // シングルトン
    // ============================================================

    public static SpawnManager Instance { get; private set; }

    // ============================================================
    // スポーンポイント
    // ============================================================

    // チームごとのスポーン座標（GameConfig の仮値を初期値として使用）
    // 将来マップに Transform[] を配置する場合はここを差し替える
    private Vector3[] _redSpawnPoints;
    private Vector3[] _blueSpawnPoints;

    // プレイヤーごとの前回スポーンインデックス（交互拠点制限用）
    private readonly Dictionary<ulong, int> _lastSpawnIndex = new();

    // チームごとのラウンドロビンカウンター（初回スポーン分散用）
    private int _redNextIndex;
    private int _blueNextIndex;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SpawnManager] 重複インスタンスを破棄");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // GameConfig の仮座標でスポーンポイントを初期化
        _redSpawnPoints = new Vector3[]
        {
            GameConfig.TEAM_RED_SPAWN_POS_1,
            GameConfig.TEAM_RED_SPAWN_POS_2,
        };
        _blueSpawnPoints = new Vector3[]
        {
            GameConfig.TEAM_BLUE_SPAWN_POS_1,
            GameConfig.TEAM_BLUE_SPAWN_POS_2,
        };
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 接続イベントで初回スポーンを処理
            NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        }
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    // ============================================================
    // 接続ハンドラ
    // ============================================================

    /// <summary>
    /// 接続時に初回スポーン位置を設定
    /// TeamManager のチーム割り当て後に呼ばれることを前提とする
    /// （同一フレーム内で TeamManager → SpawnManager の順にコールバック登録）
    /// </summary>
    private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData eventData)
    {
        if (!IsServer) return;

        if (eventData.EventType == ConnectionEvent.ClientConnected)
        {
            // 切断時のキャッシュをクリア
            // （再接続した場合は新規扱い）
        }
        else if (eventData.EventType == ConnectionEvent.ClientDisconnected)
        {
            _lastSpawnIndex.Remove(eventData.ClientId);
        }
    }

    // ============================================================
    // 公開 API
    // ============================================================

    /// <summary>
    /// 指定プレイヤーのスポーン位置を取得する
    /// 初回: ラウンドロビンで分散配置
    /// リスポーン: 前回と異なるポイントを使用（交互拠点制限）
    /// </summary>
    /// <param name="clientId">対象プレイヤーの clientId</param>
    /// <param name="team">所属チーム</param>
    /// <returns>スポーン座標</returns>
    public Vector3 GetSpawnPosition(ulong clientId, Team team)
    {
        Vector3[] points = (team == Team.Red) ? _redSpawnPoints : _blueSpawnPoints;
        int pointCount = points.Length;

        int spawnIndex;
        if (_lastSpawnIndex.TryGetValue(clientId, out int lastIndex))
        {
            // リスポーン: 前回と異なるポイントを選択
            spawnIndex = (lastIndex + 1) % pointCount;
        }
        else
        {
            // 初回: ラウンドロビンで分散
            if (team == Team.Red)
            {
                spawnIndex = _redNextIndex % pointCount;
                _redNextIndex++;
            }
            else
            {
                spawnIndex = _blueNextIndex % pointCount;
                _blueNextIndex++;
            }
        }

        _lastSpawnIndex[clientId] = spawnIndex;

        Debug.Log($"[SpawnManager] Client {clientId} ({team}) → SpawnPoint[{spawnIndex}] = {points[spawnIndex]}");
        return points[spawnIndex];
    }

    /// <summary>
    /// プレイヤーをリスポーンさせる（サーバー専用）
    /// HP全回復 + 無双ゲージMAX + Idle状態に遷移 + スポーン位置にテレポート
    /// combat-spec準拠: 「リスポーン時は無双ゲージMAX」
    /// </summary>
    /// <param name="playerObject">対象プレイヤーの NetworkObject</param>
    public void RespawnPlayer(NetworkObject playerObject)
    {
        if (!IsServer)
        {
            Debug.LogError("[SpawnManager] RespawnPlayer はサーバー専用");
            return;
        }

        ulong clientId = playerObject.OwnerClientId;

        // TeamManager からチーム取得
        if (TeamManager.Instance == null)
        {
            Debug.LogError("[SpawnManager] TeamManager が見つかりません");
            return;
        }
        Team team = TeamManager.Instance.GetPlayerTeam(clientId);

        // スポーン位置を決定
        Vector3 spawnPos = GetSpawnPosition(clientId, team);

        // CharacterController を一時無効化してテレポート
        // （CharacterController が有効だと transform.position 直接設定が効かない）
        var controller = playerObject.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        playerObject.transform.position = spawnPos;
        playerObject.transform.rotation = Quaternion.Euler(0f, team == Team.Red ? 90f : -90f, 0f);

        if (controller != null) controller.enabled = true;

        // ステートを Idle にリセット
        var stateMachine = playerObject.GetComponent<CharacterStateMachine>();
        if (stateMachine != null)
            stateMachine.ForceState(CharacterState.Idle);

        // HP全回復
        var health = playerObject.GetComponent<HealthSystem>();
        if (health != null)
            health.FullHeal();

        // 無双ゲージMAX（combat-spec: リスポーン時MAX）
        var musou = playerObject.GetComponent<MusouGauge>();
        if (musou != null)
        {
            float deficit = musou.MaxGauge - musou.CurrentGauge;
            if (deficit > 0f)
                musou.AddGauge(deficit);
        }

        // ReactionSystem の物理状態リセット（打ち上げ中に死んだ場合の残留速度を消す）
        var reaction = playerObject.GetComponent<ReactionSystem>();
        if (reaction != null)
            reaction.ResetReactionPhysics();

        Debug.Log($"[SpawnManager] Client {clientId} リスポーン完了 → {spawnPos} (HP全回復 / 無双MAX)");
    }
}
