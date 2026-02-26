using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if FIREBASE_FIRESTORE
using Firebase.Firestore;
#endif

/// <summary>
/// プレイヤーデータ（Firestore ドキュメント構造に対応）
/// players/{uid}/ のフィールドに1:1マッピング
/// </summary>
[Serializable]
public class PlayerData
{
    public string DisplayName;
    public int WeaponType1;         // メイン武器種（WeaponType enum のint値）
    public int WeaponType2;         // サブ武器種（ブレイクチャージ用）
    public Dictionary<string, int> Training;  // 鍛錬振り分け（atk/def/hp/musou/break）
    public int InscriptionC1;       // C1刻印番号（InscriptionType enum のint値）
    public int InscriptionC6;       // C6刻印番号
    public int ElementType;         // 装備属性（ElementType enum のint値）
    public int ElementLevel;        // 属性レベル（0-4）
    public int Wins;
    public int Losses;
    public int Kills;
    public int Deaths;

    /// <summary>新規プレイヤーのデフォルトデータを生成する</summary>
    public static PlayerData CreateDefault()
    {
        return new PlayerData
        {
            DisplayName = "Player",
            WeaponType1 = 0,  // GreatSword
            WeaponType2 = 1,  // DualBlades
            Training = new Dictionary<string, int>
            {
                { "atk", 0 },
                { "def", 0 },
                { "hp", 0 },
                { "musou", 0 },
                { "break", 0 },
            },
            InscriptionC1 = 0,
            InscriptionC6 = 0,
            ElementType = 0,   // None
            ElementLevel = 0,
            Wins = 0,
            Losses = 0,
            Kills = 0,
            Deaths = 0,
        };
    }
}

/// <summary>
/// Firestore プレイヤーデータ管理（シングルトン）
/// Firestore SDK 未導入時はインメモリストレージで動作する（開発用フォールバック）
///
/// 使用フロー:
/// - 戦闘開始時: サーバーが LoadPlayerData(uid) で読み込み → 武器種・属性・鍛錬をゲームに反映
/// - 戦闘終了時: UpdateBattleResults(uid, kills, deaths, won) で戦績を永続化
/// </summary>
public class PlayerDataManager : MonoBehaviour
{
    // ============================================================
    // シングルトン
    // ============================================================

    public static PlayerDataManager Instance { get; private set; }

    // ============================================================
    // 内部状態
    // ============================================================

    #if FIREBASE_FIRESTORE
    private FirebaseFirestore _db;
    #endif

    // インメモリキャッシュ（Firestore未導入時のフォールバック兼読み込みキャッシュ）
    private readonly Dictionary<string, PlayerData> _cache = new();

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeFirestore();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void InitializeFirestore()
    {
        #if FIREBASE_FIRESTORE
        _db = FirebaseFirestore.DefaultInstance;
        Debug.Log("[PlayerDataManager] Firestore初期化完了");
        #else
        Debug.Log("[PlayerDataManager] Firestore未導入 — インメモリストレージ使用");
        #endif
    }

    // ============================================================
    // データ読み込み
    // ============================================================

    /// <summary>
    /// プレイヤーデータを読み込む（サーバー側で呼ぶ）
    /// キャッシュにあればキャッシュから返す
    /// Firestore未導入時はデフォルト値を返す
    /// </summary>
    public async Task<PlayerData> LoadPlayerData(string uid)
    {
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[PlayerDataManager] UIDが空 — デフォルトデータを返します");
            return PlayerData.CreateDefault();
        }

        // キャッシュヒット
        if (_cache.TryGetValue(uid, out var cached))
        {
            Debug.Log($"[PlayerDataManager] キャッシュからロード: {uid}");
            return cached;
        }

        #if FIREBASE_FIRESTORE
        try
        {
            var docRef = _db.Collection("players").Document(uid);
            var snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var data = SnapshotToPlayerData(snapshot);
                _cache[uid] = data;
                Debug.Log($"[PlayerDataManager] Firestoreからロード: {uid}");
                return data;
            }
            else
            {
                // 新規プレイヤー: デフォルトデータを作成して保存
                var newData = PlayerData.CreateDefault();
                await SavePlayerData(uid, newData);
                Debug.Log($"[PlayerDataManager] 新規プレイヤー作成: {uid}");
                return newData;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerDataManager] Firestoreロードエラー: {e.Message}");
            return PlayerData.CreateDefault();
        }
        #else
        // Firestore未導入: デフォルトデータを生成してキャッシュ
        var defaultData = PlayerData.CreateDefault();
        _cache[uid] = defaultData;
        Debug.Log($"[PlayerDataManager] デフォルトデータ生成: {uid}");
        return defaultData;
        #endif
    }

    // ============================================================
    // データ保存
    // ============================================================

    /// <summary>
    /// プレイヤーデータを保存する（サーバー側で呼ぶ）
    /// </summary>
    public async Task SavePlayerData(string uid, PlayerData data)
    {
        if (string.IsNullOrEmpty(uid)) return;

        _cache[uid] = data;

        #if FIREBASE_FIRESTORE
        try
        {
            var docRef = _db.Collection("players").Document(uid);
            var dict = PlayerDataToDict(data);
            await docRef.SetAsync(dict);
            Debug.Log($"[PlayerDataManager] Firestore保存完了: {uid}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerDataManager] Firestore保存エラー: {e.Message}");
        }
        #else
        Debug.Log($"[PlayerDataManager] インメモリ保存: {uid}");
        await Task.CompletedTask;
        #endif
    }

    // ============================================================
    // 戦績更新
    // ============================================================

    /// <summary>
    /// 戦闘結果を更新する（戦闘終了時にサーバーが呼ぶ）
    /// </summary>
    public async Task UpdateBattleResults(string uid, int kills, int deaths, bool won)
    {
        if (string.IsNullOrEmpty(uid)) return;

        var data = await LoadPlayerData(uid);
        data.Kills += kills;
        data.Deaths += deaths;
        if (won) data.Wins++;
        else data.Losses++;

        await SavePlayerData(uid, data);
        Debug.Log($"[PlayerDataManager] 戦績更新: {uid} K+={kills} D+={deaths} Won={won}" +
                  $" (通算 {data.Kills}K/{data.Deaths}D {data.Wins}W/{data.Losses}L)");
    }

    // ============================================================
    // キャッシュ管理
    // ============================================================

    /// <summary>キャッシュをクリアする（セッション終了時等）</summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>キャッシュ済みデータを取得する（ロード済みでなければnull）</summary>
    public PlayerData GetCachedData(string uid)
    {
        return _cache.TryGetValue(uid, out var data) ? data : null;
    }

    // ============================================================
    // Firestore変換（FIREBASE_FIRESTORE定義時のみ）
    // ============================================================

    #if FIREBASE_FIRESTORE
    /// <summary>Firestoreスナップショット → PlayerData 変換</summary>
    private static PlayerData SnapshotToPlayerData(DocumentSnapshot snapshot)
    {
        var data = PlayerData.CreateDefault();

        if (snapshot.TryGetValue("displayName", out string displayName))
            data.DisplayName = displayName;
        if (snapshot.TryGetValue("weaponType1", out long wt1))
            data.WeaponType1 = (int)wt1;
        if (snapshot.TryGetValue("weaponType2", out long wt2))
            data.WeaponType2 = (int)wt2;
        if (snapshot.TryGetValue("training", out Dictionary<string, object> training))
        {
            foreach (var kv in training)
                data.Training[kv.Key] = Convert.ToInt32(kv.Value);
        }
        if (snapshot.TryGetValue("inscriptionC1", out long ic1))
            data.InscriptionC1 = (int)ic1;
        if (snapshot.TryGetValue("inscriptionC6", out long ic6))
            data.InscriptionC6 = (int)ic6;
        if (snapshot.TryGetValue("elementType", out long et))
            data.ElementType = (int)et;
        if (snapshot.TryGetValue("elementLevel", out long el))
            data.ElementLevel = (int)el;
        if (snapshot.TryGetValue("wins", out long wins))
            data.Wins = (int)wins;
        if (snapshot.TryGetValue("losses", out long losses))
            data.Losses = (int)losses;
        if (snapshot.TryGetValue("kills", out long kills))
            data.Kills = (int)kills;
        if (snapshot.TryGetValue("deaths", out long deaths))
            data.Deaths = (int)deaths;

        return data;
    }

    /// <summary>PlayerData → Firestoreドキュメント辞書 変換</summary>
    private static Dictionary<string, object> PlayerDataToDict(PlayerData data)
    {
        return new Dictionary<string, object>
        {
            { "displayName", data.DisplayName },
            { "weaponType1", data.WeaponType1 },
            { "weaponType2", data.WeaponType2 },
            { "training", data.Training },
            { "inscriptionC1", data.InscriptionC1 },
            { "inscriptionC6", data.InscriptionC6 },
            { "elementType", data.ElementType },
            { "elementLevel", data.ElementLevel },
            { "wins", data.Wins },
            { "losses", data.Losses },
            { "kills", data.Kills },
            { "deaths", data.Deaths },
            { "lastLogin", FieldValue.ServerTimestamp },
        };
    }
    #endif
}
