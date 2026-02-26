using Unity.Netcode;
using UnityEngine;

/// <summary>
/// M0: ネットワーク接続UIスクリプト
/// 空の GameObject にアタッチして使用
/// Host / Client / Server ボタンで接続テスト
/// AuthManager による認証完了後にボタンが有効化される
/// </summary>
public class HelloNetwork : MonoBehaviour
{
    /// <summary>
    /// NetworkManager のコールバックを登録する（接続・切断通知）
    /// </summary>
    void Start()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    /// <summary>
    /// コールバック解除（メモリリーク防止）
    /// </summary>
    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    /// <summary>クライアント接続時のログ出力</summary>
    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[SSMO] Client connected! ID: {clientId}");
    }

    /// <summary>クライアント切断時のログ出力</summary>
    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[SSMO] Client disconnected! ID: {clientId}");
    }

    /// <summary>
    /// 接続UI（Host/Client/Server ボタン）と接続中の情報表示
    /// 認証未完了時は「認証中...」を表示してボタンを無効化
    /// </summary>
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));

        if (!NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsServer)
        {
            GUILayout.Label("=== SSMO Network Test ===");
            GUILayout.Space(10);

            // 認証状態チェック: AuthManager が存在し認証済みでないとボタン無効
            bool isAuth = AuthManager.Instance != null && AuthManager.Instance.IsAuthenticated;

            if (!isAuth)
            {
                GUILayout.Label("認証中...");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"UID: {AuthManager.Instance.CurrentUid}");
            GUILayout.Space(5);

            if (GUILayout.Button("Host (Server + Client)", GUILayout.Height(40)))
            {
                SetupConnectionData();
                AuthManager.Instance.SetupConnectionApproval();
                NetworkManager.Singleton.StartHost();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Client", GUILayout.Height(40)))
            {
                SetupConnectionData();
                NetworkManager.Singleton.StartClient();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Dedicated Server", GUILayout.Height(40)))
            {
                AuthManager.Instance.SetupConnectionApproval();
                NetworkManager.Singleton.StartServer();
            }
        }
        else
        {
            string mode = NetworkManager.Singleton.IsHost ? "Host" :
                          NetworkManager.Singleton.IsServer ? "Server" : "Client";

            GUILayout.Label($"=== SSMO [{mode}] ===");
            GUILayout.Label($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
            GUILayout.Label($"Local Client ID: {NetworkManager.Singleton.LocalClientId}");
            GUILayout.Label($"Transport: {NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetType().Name}");

            if (AuthManager.Instance != null)
                GUILayout.Label($"UID: {AuthManager.Instance.CurrentUid}");

            GUILayout.Space(10);

            if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.EndArea();
    }

    /// <summary>
    /// 接続前にUID認証ペイロードをConnectionDataに設定する
    /// </summary>
    private void SetupConnectionData()
    {
        if (AuthManager.Instance != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionData =
                AuthManager.Instance.GetConnectionPayload();
        }
    }
}
