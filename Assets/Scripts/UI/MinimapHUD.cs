using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ミニマップ（クライアント専用UI）
///
/// 設計意図:
/// - OnGUI ベースで画面右下にミニマップを表示
/// - 自分（白）、味方（青）、敵（赤）のプレイヤー位置をドット表示
/// - 拠点は所属チーム色の四角で表示（中立 = 灰色）
/// - MINIMAP_RANGE 内のみ表示（プレイヤー中心）
/// - Mキーで全体マップ⇔ミニマップを切替
/// </summary>
public class MinimapHUD : MonoBehaviour
{
    // ============================================================
    // 表示モード
    // ============================================================

    private bool _isFullMap;   // true = 全体マップ表示

    // GUIスタイル（初回生成・キャッシュ）
    private GUIStyle _bgStyle;
    private Texture2D _bgTex;
    private Texture2D _dotTex;
    private bool _stylesInitialized;

    // 定数
    private const float MARGIN = 10f;           // 画面端からの余白 (px)
    private const float DOT_SIZE = 6f;          // プレイヤードットサイズ (px)
    private const float SELF_DOT_SIZE = 8f;     // 自キャラドットサイズ (px)
    private const float BASE_ICON_SIZE = 10f;   // 拠点アイコンサイズ (px)
    private const float FULL_MAP_SIZE = 400f;   // 全体マップ表示サイズ (px)

    // ============================================================
    // Update: キー入力
    // ============================================================

    private void Update()
    {
        // Mキーで全体マップ⇔ミニマップ切替
        if (Input.GetKeyDown(KeyCode.M))
        {
            _isFullMap = !_isFullMap;
        }
    }

    // ============================================================
    // GUI 描画
    // ============================================================

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsConnectedClient) return;

        InitStyles();

        // ローカルプレイヤーの位置を取得
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        Vector3 localPos = GetPlayerPosition(localClientId);
        if (localPos == Vector3.zero && !HasPlayerObject(localClientId)) return;

        // マップパラメータ
        float mapSize = _isFullMap ? FULL_MAP_SIZE : GameConfig.MINIMAP_SIZE;
        float mapRange = _isFullMap ? GameConfig.MAP_HALF : GameConfig.MINIMAP_RANGE;

        // ミニマップ: 右下、全体マップ: 画面中央
        float mapX, mapY;
        if (_isFullMap)
        {
            mapX = (Screen.width - mapSize) / 2f;
            mapY = (Screen.height - mapSize) / 2f;
        }
        else
        {
            mapX = Screen.width - mapSize - MARGIN;
            mapY = Screen.height - mapSize - MARGIN;
        }

        // 背景描画
        GUI.Box(new Rect(mapX, mapY, mapSize, mapSize), GUIContent.none, _bgStyle);

        // ワールド座標の中心（ミニマップ: プレイヤー中心、全体マップ: ワールド原点）
        Vector3 center = _isFullMap ? Vector3.zero : localPos;

        // 拠点を描画
        DrawBasePoints(mapX, mapY, mapSize, mapRange, center);

        // 全プレイヤーを描画
        DrawPlayers(mapX, mapY, mapSize, mapRange, center, localClientId);
    }

    // ============================================================
    // プレイヤー描画
    // ============================================================

    /// <summary>
    /// 全プレイヤー（自分・味方・敵）をドットで描画
    /// </summary>
    private void DrawPlayers(float mapX, float mapY, float mapSize, float mapRange,
        Vector3 center, ulong localClientId)
    {
        if (TeamManager.Instance == null) return;
        Team localTeam = TeamManager.Instance.GetPlayerTeam(localClientId);

        // 全スポーン済みオブジェクトを走査
        foreach (var kvp in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
        {
            var netObj = kvp.Value;
            if (netObj == null) continue;

            // プレイヤーのみ対象（HealthSystem を持つ = プレイヤー）
            var health = netObj.GetComponent<HealthSystem>();
            if (health == null) continue;

            Vector3 worldPos = netObj.transform.position;
            if (!WorldToMinimap(worldPos, center, mapRange, mapSize, out float mx, out float my))
                continue;

            ulong ownerId = netObj.OwnerClientId;
            bool isSelf = (ownerId == localClientId);

            // 色決定: 自分=白、味方=青、敵=赤
            Color dotColor;
            float dotSize;
            if (isSelf)
            {
                dotColor = Color.white;
                dotSize = SELF_DOT_SIZE;
            }
            else
            {
                Team otherTeam = TeamManager.Instance.GetPlayerTeam(ownerId);
                dotColor = (otherTeam == localTeam)
                    ? new Color(0.3f, 0.5f, 1f)   // 味方: 青
                    : new Color(1f, 0.3f, 0.3f);   // 敵: 赤
                dotSize = DOT_SIZE;
            }

            DrawDot(mapX + mx, mapY + my, dotSize, dotColor);
        }
    }

    // ============================================================
    // 拠点描画
    // ============================================================

    /// <summary>
    /// 拠点を所属チーム色の四角で描画
    /// </summary>
    private void DrawBasePoints(float mapX, float mapY, float mapSize, float mapRange,
        Vector3 center)
    {
        // BasePoint を全検索（シーン内の少数オブジェクトなので FindObjectsByType で十分）
        var bases = FindObjectsByType<BasePoint>(FindObjectsSortMode.None);

        foreach (var bp in bases)
        {
            Vector3 worldPos = bp.transform.position;
            if (!WorldToMinimap(worldPos, center, mapRange, mapSize, out float mx, out float my))
                continue;

            // 拠点の色: Red=赤、Blue=青、Neutral=灰
            Color baseColor;
            switch (bp.Status)
            {
                case BaseStatus.Red:
                    baseColor = new Color(1f, 0.3f, 0.3f);
                    break;
                case BaseStatus.Blue:
                    baseColor = new Color(0.3f, 0.5f, 1f);
                    break;
                default: // Neutral
                    baseColor = new Color(0.6f, 0.6f, 0.6f);
                    break;
            }

            DrawSquare(mapX + mx, mapY + my, BASE_ICON_SIZE, baseColor);
        }
    }

    // ============================================================
    // 座標変換
    // ============================================================

    /// <summary>
    /// ワールド座標 → ミニマップ上のローカル座標 (px) に変換
    /// X→ミニマップX、Z→ミニマップY（Y軸は上が北=Z+）
    /// </summary>
    /// <returns>範囲内なら true</returns>
    private bool WorldToMinimap(Vector3 worldPos, Vector3 center, float range, float mapSize,
        out float mx, out float my)
    {
        // center からの相対位置
        float dx = worldPos.x - center.x;
        float dz = worldPos.z - center.z;

        // 範囲外チェック
        if (Mathf.Abs(dx) > range || Mathf.Abs(dz) > range)
        {
            mx = 0;
            my = 0;
            return false;
        }

        // 正規化 (-1〜1) → ピクセル座標
        // X: 左=-range → 右=+range
        // Z: 上=+range → 下=-range（画面Yは下向きなので反転）
        mx = ((dx / range) * 0.5f + 0.5f) * mapSize;
        my = ((-dz / range) * 0.5f + 0.5f) * mapSize;
        return true;
    }

    // ============================================================
    // プレイヤー位置取得ヘルパー
    // ============================================================

    /// <summary>
    /// clientId のプレイヤー位置を取得
    /// </summary>
    private Vector3 GetPlayerPosition(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
                return client.PlayerObject.transform.position;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// clientId の PlayerObject が存在するか
    /// </summary>
    private bool HasPlayerObject(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            return client.PlayerObject != null;
        return false;
    }

    // ============================================================
    // 描画ヘルパー
    // ============================================================

    /// <summary>
    /// 指定位置にドット（円を四角で近似）を描画
    /// </summary>
    private void DrawDot(float cx, float cy, float size, Color color)
    {
        _dotTex.SetPixel(0, 0, color);
        _dotTex.Apply();
        GUI.DrawTexture(new Rect(cx - size / 2f, cy - size / 2f, size, size), _dotTex);
    }

    /// <summary>
    /// 指定位置に四角アイコンを描画
    /// </summary>
    private void DrawSquare(float cx, float cy, float size, Color color)
    {
        _dotTex.SetPixel(0, 0, color);
        _dotTex.Apply();
        GUI.DrawTexture(new Rect(cx - size / 2f, cy - size / 2f, size, size), _dotTex);
    }

    // ============================================================
    // スタイル初期化
    // ============================================================

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // 背景テクスチャ（半透明黒）
        _bgTex = new Texture2D(1, 1);
        _bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.5f));
        _bgTex.Apply();

        _bgStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = _bgTex },
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
        };

        // ドット描画用テクスチャ（色は描画時に変更）
        _dotTex = new Texture2D(1, 1);
        _dotTex.SetPixel(0, 0, Color.white);
        _dotTex.Apply();
    }
}
