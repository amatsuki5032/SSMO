using Unity.Netcode;
using UnityEngine;

/// <summary>
/// スコアボードHUD（クライアント専用UI）
///
/// 設計意図:
/// - OnGUI ベースで画面上部にタイマーとスコアを表示
/// - 画面上部中央: 残り時間（分:秒）
/// - タイマーの左右: 赤チーム撃破数 vs 青チーム撃破数
/// - GameOver 時に画面中央に勝敗テキストを大きく表示
/// - GameModeManager の NetworkVariable を参照（読み取りのみ）
/// </summary>
public class ScoreboardHUD : MonoBehaviour
{
    // GUIスタイル（初回生成・キャッシュ）
    private GUIStyle _timerStyle;
    private GUIStyle _scoreRedStyle;
    private GUIStyle _scoreBlueStyle;
    private GUIStyle _vsStyle;
    private GUIStyle _resultStyle;
    private GUIStyle _resultBgStyle;
    private GUIStyle _phaseStyle;
    private Texture2D _resultBgTex;
    private bool _stylesInitialized;

    // 定数
    private const float TIMER_Y = 8f;
    private const float SCORE_Y = 4f;
    private const float SCORE_WIDTH = 60f;
    private const float TIMER_WIDTH = 100f;

    // ============================================================
    // GUI 描画
    // ============================================================

    private void OnGUI()
    {
        if (GameModeManager.Instance == null) return;

        InitStyles();

        switch (GameModeManager.Instance.Phase)
        {
            case GamePhase.WaitingForPlayers:
                DrawWaiting();
                break;
            case GamePhase.InProgress:
                DrawScoreboard();
                break;
            case GamePhase.GameOver:
                DrawScoreboard();
                DrawResult();
                break;
        }
    }

    // ============================================================
    // 待機中表示
    // ============================================================

    /// <summary>
    /// プレイヤー待ち表示
    /// </summary>
    private void DrawWaiting()
    {
        float cx = Screen.width / 2f;
        GUI.Label(
            new Rect(cx - 100f, TIMER_Y, 200f, 30f),
            "Waiting for players...",
            _phaseStyle
        );
    }

    // ============================================================
    // スコアボード表示
    // ============================================================

    /// <summary>
    /// 画面上部: タイマー + チーム撃破数
    /// </summary>
    private void DrawScoreboard()
    {
        float cx = Screen.width / 2f;

        // タイマー（中央）
        float remaining = GameModeManager.Instance.RemainingTime;
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        string timeText = $"{minutes}:{seconds:D2}";

        GUI.Label(
            new Rect(cx - TIMER_WIDTH / 2f, TIMER_Y, TIMER_WIDTH, 30f),
            timeText,
            _timerStyle
        );

        // 赤チームスコア（タイマーの左）
        int redKills = GameModeManager.Instance.RedKills;
        GUI.Label(
            new Rect(cx - TIMER_WIDTH / 2f - SCORE_WIDTH - 10f, SCORE_Y, SCORE_WIDTH, 30f),
            redKills.ToString(),
            _scoreRedStyle
        );

        // VS（タイマーの下）
        GUI.Label(
            new Rect(cx - 10f, TIMER_Y + 22f, 20f, 20f),
            "vs",
            _vsStyle
        );

        // 青チームスコア（タイマーの右）
        int blueKills = GameModeManager.Instance.BlueKills;
        GUI.Label(
            new Rect(cx + TIMER_WIDTH / 2f + 10f, SCORE_Y, SCORE_WIDTH, 30f),
            blueKills.ToString(),
            _scoreBlueStyle
        );
    }

    // ============================================================
    // 勝敗結果表示
    // ============================================================

    /// <summary>
    /// GameOver 時に画面中央に勝敗テキストを表示
    /// </summary>
    private void DrawResult()
    {
        int winner = GameModeManager.Instance.WinnerTeam;
        if (winner < 0) return;

        // 自チームとの比較で勝敗テキストを決定
        string resultText;
        Color resultColor;

        if (winner == 2)
        {
            resultText = "DRAW";
            resultColor = Color.yellow;
        }
        else
        {
            // ローカルプレイヤーのチームを取得
            Team localTeam = Team.Red;
            if (NetworkManager.Singleton != null && TeamManager.Instance != null)
            {
                localTeam = TeamManager.Instance.GetPlayerTeam(NetworkManager.Singleton.LocalClientId);
            }

            bool isWin = (winner == 0 && localTeam == Team.Red) ||
                         (winner == 1 && localTeam == Team.Blue);
            resultText = isWin ? "VICTORY!" : "DEFEAT";
            resultColor = isWin ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f);
        }

        float w = 400f;
        float h = 80f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f - 50f;

        // 半透明背景
        GUI.Box(new Rect(x - 20f, y - 10f, w + 40f, h + 20f), GUIContent.none, _resultBgStyle);

        // 勝敗テキスト
        _resultStyle.normal.textColor = resultColor;
        GUI.Label(new Rect(x, y, w, h), resultText, _resultStyle);
    }

    // ============================================================
    // スタイル初期化
    // ============================================================

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _timerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };

        _scoreRedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(1f, 0.3f, 0.3f) },
        };

        _scoreBlueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.3f, 0.5f, 1f) },
        };

        _vsStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
        };

        _phaseStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) },
        };

        _resultBgTex = new Texture2D(1, 1);
        _resultBgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
        _resultBgTex.Apply();

        _resultBgStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = _resultBgTex },
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
        };

        _resultStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
    }
}
