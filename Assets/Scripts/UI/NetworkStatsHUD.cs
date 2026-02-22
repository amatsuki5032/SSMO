using Unity.Netcode;
using UnityEngine;

/// <summary>
/// デバッグ用ネットワーク統計HUD
///
/// 画面左上に RTT(ms) と PacketLoss(%) を表示する
/// OnGUI で描画するためパフォーマンスへの影響を最小限にするよう
/// 更新頻度を 0.5秒おきに制限している
/// </summary>
public class NetworkStatsHUD : MonoBehaviour
{
    // --- 表示設定 ---
    private const float UPDATE_INTERVAL = 0.5f; // 表示更新間隔（秒）
    private const float BOX_WIDTH = 220f;
    private const float BOX_HEIGHT = 60f;
    private const float MARGIN = 10f;
    private const float LINE_HEIGHT = 20f;

    // --- 表示用キャッシュ ---
    private float _nextUpdateTime;
    private string _rttText = "";
    private string _packetLossText = "";
    private bool _isConnected;

    // --- スタイル ---
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private bool _stylesInitialized;

    private void InitializeStyles()
    {
        // 半透明黒の背景（視認性確保）
        Texture2D bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
        bgTex.Apply();

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = bgTex }
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            normal = { textColor = Color.white },
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        _stylesInitialized = true;
    }

    private void Update()
    {
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + UPDATE_INTERVAL;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            _isConnected = false;
            return;
        }

        _isConnected = true;

        // NGO の NetworkTransport から RTT を取得
        // サーバー自身の場合は RTT = 0
        if (nm.IsServer && !nm.IsClient)
        {
            _rttText = "RTT: 0 ms (Server)";
            _packetLossText = "";
        }
        else
        {
            // クライアント（またはホスト）の場合
            var transport = nm.NetworkConfig.NetworkTransport;
            ulong rtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);
            _rttText = $"RTT: {rtt} ms";

            // PacketLoss は Unity Transport の統計から直接取得する手段が限られるため
            // 現段階では表示枠のみ用意し、将来 Multiplayer Tools の統計に差し替える
            _packetLossText = "Loss: -- %";
        }
    }

    private void OnGUI()
    {
        if (!_stylesInitialized)
        {
            InitializeStyles();
        }

        Rect boxRect = new Rect(MARGIN, MARGIN, BOX_WIDTH, BOX_HEIGHT);
        GUI.Box(boxRect, GUIContent.none, _boxStyle);

        if (!_isConnected)
        {
            Rect labelRect = new Rect(MARGIN + 8f, MARGIN + 8f, BOX_WIDTH, LINE_HEIGHT);
            GUI.Label(labelRect, "Not Connected", _labelStyle);
            return;
        }

        float y = MARGIN + 8f;
        float x = MARGIN + 8f;

        GUI.Label(new Rect(x, y, BOX_WIDTH, LINE_HEIGHT), _rttText, _labelStyle);
        y += LINE_HEIGHT;

        if (!string.IsNullOrEmpty(_packetLossText))
        {
            GUI.Label(new Rect(x, y, BOX_WIDTH, LINE_HEIGHT), _packetLossText, _labelStyle);
        }
    }
}
