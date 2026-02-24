using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 仙箪強化リングHUD（クライアント専用UI）
///
/// 設計意図:
/// - OnGUI ベースで仙箪カウントとリングスロット表示（箱人間フェーズ）
/// - 画面右下: 仙箪所持数 + リング状態
/// - リング回転中はスロット一覧を表示、現在位置をハイライト
/// - ローカルプレイヤーの EnhancementRing / ComboSystem を参照
/// </summary>
public class EnhancementRingHUD : MonoBehaviour
{
    // ============================================================
    // ローカル変数
    // ============================================================

    private EnhancementRing _localRing;
    private ComboSystem _localCombo;

    // GUI スタイル
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _slotStyle;
    private GUIStyle _activeSlotStyle;
    private bool _stylesInitialized;

    // 定数
    private const float PANEL_WIDTH = 200f;
    private const float PANEL_HEIGHT = 160f;
    private const float RIGHT_MARGIN = 10f;
    private const float BOTTOM_MARGIN = 100f;
    private const float SLOT_SIZE = 24f;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Update()
    {
        if (_localRing == null)
        {
            TryFindLocalPlayer();
        }
    }

    /// <summary>
    /// ローカルプレイヤーの EnhancementRing / ComboSystem を取得する
    /// </summary>
    private void TryFindLocalPlayer()
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsConnectedClient) return;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(localClientId, out var client))
            return;
        if (client.PlayerObject == null) return;

        _localRing = client.PlayerObject.GetComponent<EnhancementRing>();
        _localCombo = client.PlayerObject.GetComponent<ComboSystem>();
    }

    // ============================================================
    // GUI 描画
    // ============================================================

    private void OnGUI()
    {
        if (_localRing == null || _localCombo == null) return;

        InitStyles();

        float x = Screen.width - PANEL_WIDTH - RIGHT_MARGIN;
        float y = Screen.height - BOTTOM_MARGIN - PANEL_HEIGHT;

        // パネル背景
        GUI.Box(new Rect(x, y, PANEL_WIDTH, PANEL_HEIGHT), GUIContent.none, _boxStyle);

        float innerX = x + 8f;
        float innerY = y + 8f;

        // 仙箪所持数
        int sentanCount = _localCombo.SentanCount;
        int required = GameConfig.SENTAN_REQUIRED_FOR_ENHANCE * (_localRing.EnhanceCount + 1);
        GUI.Label(new Rect(innerX, innerY, PANEL_WIDTH - 16f, 20f),
            $"Sentan: {sentanCount} / {required}", _labelStyle);
        innerY += 24f;

        // 強化段階
        GUI.Label(new Rect(innerX, innerY, PANEL_WIDTH - 16f, 20f),
            $"Enhance: {_localRing.EnhanceCount}  ATK+{_localRing.AtkBuffCount * 10}%  DEF+{_localRing.DefBuffCount * 10}%",
            _labelStyle);
        innerY += 24f;

        // 連撃Lv
        GUI.Label(new Rect(innerX, innerY, PANEL_WIDTH - 16f, 20f),
            $"Combo Lv: {_localCombo.ComboEnhanceLevel}", _labelStyle);
        innerY += 28f;

        // リング状態
        if (_localRing.IsRingActive)
        {
            GUI.Label(new Rect(innerX, innerY, PANEL_WIDTH - 16f, 20f),
                "Ring [E] to activate!", _labelStyle);
            innerY += 24f;

            // スロット表示
            DrawSlots(innerX, innerY);
        }
        else
        {
            GUI.Label(new Rect(innerX, innerY, PANEL_WIDTH - 16f, 20f),
                "Ring: waiting...", _labelStyle);
        }
    }

    /// <summary>
    /// 7スロットを横並びで表示。現在位置をハイライト
    /// </summary>
    private void DrawSlots(float startX, float startY)
    {
        int currentPos = _localRing.RingPosition;

        for (int i = 0; i < GameConfig.SENTAN_SLOTS; i++)
        {
            float slotX = startX + i * (SLOT_SIZE + 2f);
            bool isActive = (i == currentPos);
            GUIStyle style = isActive ? _activeSlotStyle : _slotStyle;

            string label = _localRing.GetSlotName(i);
            GUI.Box(new Rect(slotX, startY, SLOT_SIZE, SLOT_SIZE), label, style);
        }
    }

    // ============================================================
    // スタイル初期化
    // ============================================================

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // 半透明パネル背景
        var bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.5f));
        bgTex.Apply();

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = bgTex },
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        // 通常スロット（灰色背景）
        var slotTex = new Texture2D(1, 1);
        slotTex.SetPixel(0, 0, new Color(0.3f, 0.3f, 0.3f, 0.8f));
        slotTex.Apply();

        _slotStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = slotTex, textColor = Color.white },
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        // アクティブスロット（金色背景）
        var activeTex = new Texture2D(1, 1);
        activeTex.SetPixel(0, 0, new Color(1f, 0.84f, 0f, 0.9f));
        activeTex.Apply();

        _activeSlotStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = activeTex, textColor = Color.black },
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
    }
}
