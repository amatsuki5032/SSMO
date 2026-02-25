using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NPC兵士スポーン管理（サーバー権威型）
///
/// 設計意図:
/// - 制圧済み拠点から定期的にNPC兵士をスポーン
/// - 各拠点ごとに最大 NPC_MAX_PER_BASE 体まで
/// - スポーンしたNPCは最も近い敵拠点方向へ移動
/// - 拠点の制圧状況が変われば、スポーンチームも自動的に変わる
/// - 中立拠点からはスポーンしない
/// - NPCSoldier Prefab はエディタで作成し、[SerializeField] で参照
/// </summary>
public class NPCSpawner : NetworkBehaviour
{
    // ============================================================
    // 設定
    // ============================================================

    [Header("NPC Prefab（エディタで設定）")]
    [SerializeField] private GameObject _npcSoldierPrefab;

    [Header("仙箪 Prefab（エディタで設定）")]
    [SerializeField] private GameObject _sentanItemPrefab;

    // ============================================================
    // シングルトン
    // ============================================================

    public static NPCSpawner Instance { get; private set; }

    // ============================================================
    // サーバー側ローカル変数
    // ============================================================

    // 各拠点ごとのスポーンタイマー
    private readonly Dictionary<int, float> _spawnTimers = new();

    // 各拠点ごとの生存NPC一覧（デスポーン時に自動除去するためリスト管理）
    private readonly Dictionary<int, List<NPCSoldier>> _aliveNpcs = new();

    // シーン内の全拠点キャッシュ
    private BasePoint[] _bases;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NPCSpawner] 重複インスタンスを破棄");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Prefab 未設定チェック
        if (_npcSoldierPrefab == null)
        {
            Debug.LogError("[NPCSpawner] NPCSoldier Prefab が未設定です。エディタで設定してください。");
            return;
        }

        // 拠点をキャッシュ（MapGenerator で生成済み前提）
        _bases = FindObjectsByType<BasePoint>(FindObjectsSortMode.None);

        if (_bases.Length == 0)
        {
            Debug.LogWarning("[NPCSpawner] BasePoint が見つかりません。MapGenerator が先に実行されていることを確認してください。");
            return;
        }

        // 各拠点のタイマーとNPCリストを初期化
        foreach (var bp in _bases)
        {
            _spawnTimers[bp.BaseIndex] = 0f;
            _aliveNpcs[bp.BaseIndex] = new List<NPCSoldier>();
        }

        Debug.Log($"[NPCSpawner] 初期化完了。拠点数: {_bases.Length}");
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    // ============================================================
    // 定期スポーン（サーバー専用）
    // ============================================================

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (_bases == null) return;

        foreach (var bp in _bases)
        {
            // 中立拠点からはスポーンしない
            if (bp.Status == BaseStatus.Neutral) continue;

            int idx = bp.BaseIndex;

            // 死亡済みNPCをリストから除去
            CleanupDeadNpcs(idx);

            // 上限チェック
            if (_aliveNpcs[idx].Count >= GameConfig.NPC_MAX_PER_BASE) continue;

            // タイマー更新
            _spawnTimers[idx] += GameConfig.FIXED_DELTA_TIME;
            if (_spawnTimers[idx] < GameConfig.NPC_SPAWN_INTERVAL) continue;

            // スポーン実行
            _spawnTimers[idx] = 0f;
            SpawnNPC(bp);
        }
    }

    // ============================================================
    // NPC スポーン
    // ============================================================

    /// <summary>
    /// 拠点からNPC兵士を1体スポーンする
    /// 拠点の所属チームのNPCが、最も近い敵拠点方向へ移動する
    /// </summary>
    private void SpawnNPC(BasePoint sourceBase)
    {
        Team npcTeam = (sourceBase.Status == BaseStatus.Red) ? Team.Red : Team.Blue;

        // スポーン位置: 拠点中心から敵方向に少しオフセット
        Vector3 spawnPos = sourceBase.transform.position;
        spawnPos.y = 0.5f; // 地面より少し上

        // 敵方向にオフセット（拠点上にスポーンすると制圧判定に干渉しうるため）
        Vector3 targetPos = FindNearestEnemyBasePosition(sourceBase, npcTeam);
        Vector3 toEnemy = (targetPos - spawnPos);
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude > 0.1f)
        {
            spawnPos += toEnemy.normalized * GameConfig.NPC_SPAWN_OFFSET;
        }

        // Prefab をインスタンス化してスポーン
        var npcObj = Instantiate(_npcSoldierPrefab, spawnPos, Quaternion.identity);
        npcObj.transform.localScale = Vector3.one * GameConfig.NPC_SCALE;

        var netObj = npcObj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[NPCSpawner] NPCSoldier Prefab に NetworkObject がありません");
            Destroy(npcObj);
            return;
        }

        // サーバー所有でスポーン
        netObj.Spawn();

        // 初期設定（スポーン後に NetworkVariable を設定）
        var soldier = npcObj.GetComponent<NPCSoldier>();
        if (soldier != null)
        {
            soldier.Initialize(npcTeam, sourceBase.BaseIndex, targetPos);
            _aliveNpcs[sourceBase.BaseIndex].Add(soldier);

            Debug.Log($"[NPCSpawner] NPC兵士スポーン: 拠点{sourceBase.BaseIndex} ({npcTeam}) " +
                      $"→ 目標={targetPos} (現在{_aliveNpcs[sourceBase.BaseIndex].Count}/{GameConfig.NPC_MAX_PER_BASE})");
        }
    }

    // ============================================================
    // ターゲット選定
    // ============================================================

    /// <summary>
    /// 最も近い敵チームの拠点座標を返す
    /// 敵拠点がない場合（全拠点制圧済み等）はマップ中央を返す
    /// </summary>
    private Vector3 FindNearestEnemyBasePosition(BasePoint sourceBase, Team npcTeam)
    {
        BaseStatus enemyStatus = (npcTeam == Team.Red) ? BaseStatus.Blue : BaseStatus.Red;
        float nearestDist = float.MaxValue;
        Vector3 nearestPos = GameConfig.BASE_POS_CENTER;
        bool found = false;

        foreach (var bp in _bases)
        {
            // 敵拠点 or 中立拠点を目標候補にする（中立も奪いに行く）
            if (bp.Status != enemyStatus && bp.Status != BaseStatus.Neutral) continue;
            if (bp.BaseIndex == sourceBase.BaseIndex) continue; // 自分自身は除外

            float dist = Vector3.Distance(sourceBase.transform.position, bp.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestPos = bp.transform.position;
                found = true;
            }
        }

        // 全拠点が味方の場合: 敵の初期スポーン位置へ向かう
        if (!found)
        {
            nearestPos = (npcTeam == Team.Red)
                ? GameConfig.TEAM_BLUE_SPAWN_POS_1  // 赤軍NPCは青軍方面へ
                : GameConfig.TEAM_RED_SPAWN_POS_1;  // 青軍NPCは赤軍方面へ
        }

        return nearestPos;
    }

    // ============================================================
    // 仙箪アイテムスポーン
    // ============================================================

    /// <summary>
    /// NPC死亡地点に仙箪アイテムをスポーンする
    /// NPCSoldier.OnDeath() から呼ばれる（サーバー専用）
    /// </summary>
    /// <param name="position">ドロップ位置</param>
    public void SpawnSentanItem(Vector3 position)
    {
        if (!IsServer) return;

        if (_sentanItemPrefab == null)
        {
            Debug.LogWarning("[NPCSpawner] SentanItem Prefab が未設定です");
            return;
        }

        // ドロップ確率判定
        if (Random.value > GameConfig.SENTAN_DROP_RATE) return;

        var sentanObj = Instantiate(_sentanItemPrefab, position, Quaternion.identity);
        var netObj = sentanObj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[NPCSpawner] SentanItem Prefab に NetworkObject がありません");
            Destroy(sentanObj);
            return;
        }

        netObj.Spawn();
        Debug.Log($"[NPCSpawner] 仙箪ドロップ: {position}");
    }

    // ============================================================
    // NPC管理
    // ============================================================

    /// <summary>
    /// 死亡・デスポーン済みのNPCをリストから除去
    /// </summary>
    private void CleanupDeadNpcs(int baseIndex)
    {
        if (!_aliveNpcs.ContainsKey(baseIndex)) return;

        var list = _aliveNpcs[baseIndex];
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null || list[i].IsDead || !list[i].IsSpawned)
            {
                list.RemoveAt(i);
            }
        }
    }
}
