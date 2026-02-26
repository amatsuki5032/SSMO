using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ネットワーク接続UIスクリプト
/// 空の GameObject にアタッチして使用
///
/// 接続モード:
/// - Relay接続（本番用）: RelayManager 経由でNAT越え接続
/// - Direct接続（Editor用）: localhost:7777 直結（ParrelSync テスト用）
///
/// AuthManager による認証完了後にボタンが有効化される
/// </summary>
public class HelloNetwork : MonoBehaviour
{
    // ============================================================
    // 内部状態
    // ============================================================

    // 接続処理の状態管理（async操作をOnGUIから安全に扱うため）
    private enum ConnectionState { None, Connecting, Connected, Failed }
    private ConnectionState _connectionState = ConnectionState.None;
    private string _connectionMessage = "";

    // JoinCode入力フィールド（クライアント用）
    private string _joinCodeInput = "";

    // ============================================================
    // ライフサイクル
    // ============================================================

    void Start()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[SSMO] Client connected! ID: {clientId}");
        _connectionState = ConnectionState.Connected;
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[SSMO] Client disconnected! ID: {clientId}");
        if (!NetworkManager.Singleton.IsServer)
            _connectionState = ConnectionState.None;
    }

    // ============================================================
    // GUI
    // ============================================================

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 500));

        if (!NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsServer)
        {
            DrawLobbyUI();
        }
        else
        {
            DrawConnectedUI();
        }

        GUILayout.EndArea();
    }

    /// <summary>未接続時のロビーUI</summary>
    private void DrawLobbyUI()
    {
        GUILayout.Label("=== SSMO ===");
        GUILayout.Space(10);

        // 認証チェック
        bool isAuth = AuthManager.Instance != null && AuthManager.Instance.IsAuthenticated;
        if (!isAuth)
        {
            GUILayout.Label("認証中...");
            return;
        }

        GUILayout.Label($"UID: {AuthManager.Instance.CurrentUid}");
        GUILayout.Space(5);

        // 接続処理中の表示
        if (_connectionState == ConnectionState.Connecting)
        {
            GUILayout.Label(_connectionMessage);
            return;
        }

        if (_connectionState == ConnectionState.Failed)
        {
            GUILayout.Label(_connectionMessage);
            GUILayout.Space(5);
            if (GUILayout.Button("戻る", GUILayout.Height(30)))
                _connectionState = ConnectionState.None;
            return;
        }

        // --- Relay接続（本番用）---
        GUILayout.Label("--- Relay接続 ---");

        if (GUILayout.Button("Host (Relay)", GUILayout.Height(40)))
        {
            StartRelayHost();
        }

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label("JoinCode:", GUILayout.Width(70));
        _joinCodeInput = GUILayout.TextField(_joinCodeInput, 8);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Join (Relay)", GUILayout.Height(40)))
        {
            StartRelayClient(_joinCodeInput.Trim());
        }

        GUILayout.Space(15);

        // --- Direct接続（Editor デバッグ用）---
        #if UNITY_EDITOR
        GUILayout.Label("--- Direct接続 (Editor) ---");

        if (GUILayout.Button("Direct Host", GUILayout.Height(30)))
        {
            SetupConnectionData();
            AuthManager.Instance?.SetupConnectionApproval();
            NetworkManager.Singleton.StartHost();
        }

        GUILayout.Space(3);

        if (GUILayout.Button("Direct Client", GUILayout.Height(30)))
        {
            SetupConnectionData();
            NetworkManager.Singleton.StartClient();
        }
        #endif
    }

    /// <summary>接続後の情報表示UI</summary>
    private void DrawConnectedUI()
    {
        string mode = NetworkManager.Singleton.IsHost ? "Host" :
                      NetworkManager.Singleton.IsServer ? "Server" : "Client";

        GUILayout.Label($"=== SSMO [{mode}] ===");
        GUILayout.Label($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
        GUILayout.Label($"Local Client ID: {NetworkManager.Singleton.LocalClientId}");
        GUILayout.Label($"Transport: {NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetType().Name}");

        if (AuthManager.Instance != null)
            GUILayout.Label($"UID: {AuthManager.Instance.CurrentUid}");

        // Relay接続中ならJoinCodeを表示（ホスト側のみ）
        if (RelayManager.Instance != null && RelayManager.Instance.IsRelayActive &&
            !string.IsNullOrEmpty(RelayManager.Instance.JoinCode) &&
            NetworkManager.Singleton.IsHost)
        {
            GUILayout.Space(5);
            GUILayout.Label($"JoinCode: {RelayManager.Instance.JoinCode}");
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
        {
            NetworkManager.Singleton.Shutdown();
            _connectionState = ConnectionState.None;
        }
    }

    // ============================================================
    // Relay接続処理（async）
    // ============================================================

    /// <summary>Relay経由でHostを開始する</summary>
    private async void StartRelayHost()
    {
        if (RelayManager.Instance == null)
        {
            // RelayManager がなければ Direct にフォールバック
            Debug.LogWarning("[HelloNetwork] RelayManager が見つかりません — Direct接続にフォールバック");
            SetupConnectionData();
            AuthManager.Instance?.SetupConnectionApproval();
            NetworkManager.Singleton.StartHost();
            return;
        }

        _connectionState = ConnectionState.Connecting;
        _connectionMessage = "Relay作成中...";

        var joinCode = await RelayManager.Instance.CreateRelay();

        if (joinCode != null)
        {
            SetupConnectionData();
            AuthManager.Instance?.SetupConnectionApproval();
            NetworkManager.Singleton.StartHost();
            Debug.Log($"[HelloNetwork] Relay Host開始: JoinCode={joinCode}");
        }
        else
        {
            // Relay失敗 — Direct にフォールバック（開発中）
            Debug.LogWarning("[HelloNetwork] Relay作成失敗 — Direct接続にフォールバック");
            SetupConnectionData();
            AuthManager.Instance?.SetupConnectionApproval();
            NetworkManager.Singleton.StartHost();
        }
    }

    /// <summary>Relay経由でClientとして参加する</summary>
    private async void StartRelayClient(string joinCode)
    {
        if (string.IsNullOrEmpty(joinCode))
        {
            _connectionState = ConnectionState.Failed;
            _connectionMessage = "JoinCodeを入力してください";
            return;
        }

        if (RelayManager.Instance == null)
        {
            Debug.LogWarning("[HelloNetwork] RelayManager が見つかりません — Direct接続にフォールバック");
            SetupConnectionData();
            NetworkManager.Singleton.StartClient();
            return;
        }

        _connectionState = ConnectionState.Connecting;
        _connectionMessage = $"Relay参加中... (Code: {joinCode})";

        bool success = await RelayManager.Instance.JoinRelay(joinCode);

        if (success)
        {
            SetupConnectionData();
            NetworkManager.Singleton.StartClient();
            Debug.Log($"[HelloNetwork] Relay Client開始: JoinCode={joinCode}");
        }
        else
        {
            _connectionState = ConnectionState.Failed;
            _connectionMessage = "Relay参加に失敗しました";
        }
    }

    // ============================================================
    // ヘルパー
    // ============================================================

    /// <summary>接続前にUID認証ペイロードをConnectionDataに設定する</summary>
    private void SetupConnectionData()
    {
        if (AuthManager.Instance != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionData =
                AuthManager.Instance.GetConnectionPayload();
        }
    }
}
