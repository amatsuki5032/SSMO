using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 戦闘HUD（クライアント専用UI）
///
/// 設計意図:
/// - OnGUI ベースで最低限のステータス表示（箱人間フェーズ）
/// - 画面下部中央: 自キャラHP + 無双ゲージ
/// - 画面上部中央: ターゲットHP（最後に攻撃した/された敵）
/// - NetworkStatsHUD（左上）、DebugTestHelper（右上）と被らない配置
/// - ローカルプレイヤーのコンポーネントを参照して表示
/// </summary>
public class BattleHUD : MonoBehaviour
{
    // ============================================================
    // ターゲット通知（HitboxSystem から呼ばれる静的コールバック）
    // ============================================================

    /// <summary>
    /// ヒット通知コールバック（attackerNetId, targetNetId）
    /// HitboxSystem の NotifyHitClientRpc から呼ばれる
    /// </summary>
    public static System.Action<ulong, ulong> OnHitNotified;

    // ============================================================
    // ローカル変数
    // ============================================================

    // 自キャラの参照（接続後に取得）
    private HealthSystem _localHealth;
    private MusouGauge _localMusou;
    private ulong _localNetObjectId;

    // ターゲット情報
    private ulong _targetNetObjectId;
    private float _targetDisplayTimer; // ターゲット表示残り時間

    // GUI スタイル（初回生成・キャッシュ）
    private GUIStyle _barBgStyle;
    private GUIStyle _barFillStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _targetLabelStyle;
    private Texture2D _bgTex;
    private Texture2D _fillTex;
    private bool _stylesInitialized;

    // 定数
    private const float BAR_WIDTH = 300f;
    private const float BAR_HEIGHT = 20f;
    private const float MUSOU_BAR_HEIGHT = 14f;
    private const float TARGET_DISPLAY_DURATION = 5f; // ターゲット表示秒数
    private const float BOTTOM_MARGIN = 60f;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void OnEnable()
    {
        OnHitNotified += HandleHitNotified;
    }

    private void OnDisable()
    {
        OnHitNotified -= HandleHitNotified;
    }

    private void Update()
    {
        // ローカルプレイヤーの参照が未取得なら取得を試みる
        if (_localHealth == null)
        {
            TryFindLocalPlayer();
        }

        // ターゲット表示タイマー減少
        if (_targetDisplayTimer > 0f)
        {
            _targetDisplayTimer -= Time.deltaTime;
        }
    }

    // ============================================================
    // ローカルプレイヤー検索
    // ============================================================

    /// <summary>
    /// ローカルプレイヤーの HealthSystem / MusouGauge を取得する
    /// 接続前やスポーン前は null のままスキップ
    /// </summary>
    private void TryFindLocalPlayer()
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsConnectedClient) return;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                GetLocalPlayerNetObjectId(localClientId), out var playerObj))
            return;

        _localHealth = playerObj.GetComponent<HealthSystem>();
        _localMusou = playerObj.GetComponent<MusouGauge>();
        _localNetObjectId = playerObj.NetworkObjectId;
    }

    /// <summary>
    /// localClientId から対応する PlayerObject の NetworkObjectId を取得
    /// </summary>
    private ulong GetLocalPlayerNetObjectId(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
                return client.PlayerObject.NetworkObjectId;
        }
        return 0;
    }

    // ============================================================
    // ターゲット追跡
    // ============================================================

    /// <summary>
    /// ヒット通知を受けてターゲットを更新する
    /// 自分が攻撃した → ターゲット = 被弾者
    /// 自分が被弾した → ターゲット = 攻撃者
    /// </summary>
    private void HandleHitNotified(ulong attackerNetId, ulong targetNetId)
    {
        if (_localNetObjectId == 0) return;

        if (attackerNetId == _localNetObjectId)
        {
            // 自分が攻撃した → 被弾者をターゲットに
            _targetNetObjectId = targetNetId;
            _targetDisplayTimer = TARGET_DISPLAY_DURATION;
        }
        else if (targetNetId == _localNetObjectId)
        {
            // 自分が被弾した → 攻撃者をターゲットに
            _targetNetObjectId = attackerNetId;
            _targetDisplayTimer = TARGET_DISPLAY_DURATION;
        }
    }

    // ============================================================
    // GUI 描画
    // ============================================================

    private void OnGUI()
    {
        if (_localHealth == null) return;

        InitStyles();

        DrawSelfStatus();
        DrawTargetStatus();
    }

    /// <summary>
    /// 画面下部中央: 自キャラHP + 無双ゲージ
    /// </summary>
    private void DrawSelfStatus()
    {
        float centerX = Screen.width / 2f - BAR_WIDTH / 2f;
        float baseY = Screen.height - BOTTOM_MARGIN;

        // === 無双ゲージバー（HP の下）===
        if (_localMusou != null)
        {
            float musouRatio = _localMusou.CurrentGauge / _localMusou.MaxGauge;
            float musouY = baseY;

            // 背景
            GUI.Box(new Rect(centerX, musouY, BAR_WIDTH, MUSOU_BAR_HEIGHT), GUIContent.none, _barBgStyle);

            // ゲージ（MAX時は金色、通常は緑）
            Color musouColor = _localMusou.IsGaugeFull
                ? new Color(1f, 0.85f, 0.2f) // 金色
                : new Color(0.2f, 0.8f, 0.2f); // 緑
            SetFillColor(musouColor);
            GUI.Box(new Rect(centerX, musouY, BAR_WIDTH * musouRatio, MUSOU_BAR_HEIGHT), GUIContent.none, _barFillStyle);

            // ラベル
            string musouText = $"Musou: {_localMusou.CurrentGauge:F0}/{_localMusou.MaxGauge:F0}";
            if (_localMusou.IsGaugeFull) musouText += " MAX!";
            GUI.Label(new Rect(centerX, musouY, BAR_WIDTH, MUSOU_BAR_HEIGHT), musouText, _labelStyle);

            baseY = musouY - 4f; // HPバーとの間隔
        }

        // === HPバー ===
        float hpRatio = _localHealth.GetHpRatio();
        float hpY = baseY - BAR_HEIGHT;

        // 背景
        GUI.Box(new Rect(centerX, hpY, BAR_WIDTH, BAR_HEIGHT), GUIContent.none, _barBgStyle);

        // HP帯による色変化
        Color hpColor = GetHpColor(hpRatio);
        SetFillColor(hpColor);
        GUI.Box(new Rect(centerX, hpY, BAR_WIDTH * hpRatio, BAR_HEIGHT), GUIContent.none, _barFillStyle);

        // ラベル
        string hpText = $"HP: {_localHealth.CurrentHp}/{_localHealth.MaxHp}";
        GUI.Label(new Rect(centerX, hpY, BAR_WIDTH, BAR_HEIGHT), hpText, _labelStyle);
    }

    /// <summary>
    /// 画面上部中央: ターゲットHP
    /// 最後に攻撃した敵 or 攻撃された敵を表示
    /// </summary>
    private void DrawTargetStatus()
    {
        if (_targetDisplayTimer <= 0f) return;
        if (_targetNetObjectId == 0) return;

        // ターゲットの NetworkObject を取得
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                _targetNetObjectId, out var targetObj))
        {
            // デスポーン済み → 表示クリア
            _targetDisplayTimer = 0f;
            return;
        }

        float centerX = Screen.width / 2f - BAR_WIDTH / 2f;
        float topY = 50f;

        // ターゲット名
        string targetName = targetObj.gameObject.name;

        // プレイヤーのHP表示
        var targetHealth = targetObj.GetComponent<HealthSystem>();
        if (targetHealth != null)
        {
            // 名前ラベル
            GUI.Label(new Rect(centerX, topY - 20f, BAR_WIDTH, 20f), targetName, _targetLabelStyle);

            // HPバー
            float hpRatio = targetHealth.GetHpRatio();
            GUI.Box(new Rect(centerX, topY, BAR_WIDTH, BAR_HEIGHT), GUIContent.none, _barBgStyle);

            Color hpColor = GetHpColor(hpRatio);
            SetFillColor(hpColor);
            GUI.Box(new Rect(centerX, topY, BAR_WIDTH * hpRatio, BAR_HEIGHT), GUIContent.none, _barFillStyle);

            string hpText = $"HP: {targetHealth.CurrentHp}/{targetHealth.MaxHp}";
            GUI.Label(new Rect(centerX, topY, BAR_WIDTH, BAR_HEIGHT), hpText, _labelStyle);
            return;
        }

        // NPC兵士のHP表示
        var targetNpc = targetObj.GetComponent<NPCSoldier>();
        if (targetNpc != null)
        {
            GUI.Label(new Rect(centerX, topY - 20f, BAR_WIDTH, 20f), targetName, _targetLabelStyle);

            float hpRatio = (float)targetNpc.CurrentHp / GameConfig.NPC_HP;
            GUI.Box(new Rect(centerX, topY, BAR_WIDTH, BAR_HEIGHT), GUIContent.none, _barBgStyle);

            Color hpColor = GetHpColor(hpRatio);
            SetFillColor(hpColor);
            GUI.Box(new Rect(centerX, topY, BAR_WIDTH * hpRatio, BAR_HEIGHT), GUIContent.none, _barFillStyle);

            string hpText = $"HP: {targetNpc.CurrentHp}/{GameConfig.NPC_HP}";
            GUI.Label(new Rect(centerX, topY, BAR_WIDTH, BAR_HEIGHT), hpText, _labelStyle);
        }
    }

    // ============================================================
    // ヘルパー
    // ============================================================

    /// <summary>
    /// HP比率に応じた色を返す（combat-spec準拠: 青/黄/赤）
    /// </summary>
    private static Color GetHpColor(float ratio)
    {
        if (ratio > GameConfig.GUTS_BLUE_THRESHOLD)
            return new Color(0.2f, 0.6f, 1f);   // 青（50-100%）
        if (ratio > GameConfig.GUTS_YELLOW_THRESHOLD)
            return new Color(1f, 0.85f, 0.2f);  // 黄（20-50%）
        return new Color(1f, 0.2f, 0.2f);       // 赤（0-20%）
    }

    /// <summary>
    /// フィルバーのテクスチャ色を動的変更
    /// </summary>
    private void SetFillColor(Color color)
    {
        if (_fillTex != null)
        {
            _fillTex.SetPixel(0, 0, color);
            _fillTex.Apply();
        }
    }

    /// <summary>
    /// GUIStyle を初回のみ生成してキャッシュ
    /// </summary>
    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // 背景テクスチャ（半透明黒）
        _bgTex = new Texture2D(1, 1);
        _bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
        _bgTex.Apply();

        // フィルテクスチャ（色は描画時に変更）
        _fillTex = new Texture2D(1, 1);
        _fillTex.SetPixel(0, 0, Color.white);
        _fillTex.Apply();

        _barBgStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = _bgTex },
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
        };

        _barFillStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = _fillTex },
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };

        _targetLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.9f, 0.5f) },
        };
    }
}
