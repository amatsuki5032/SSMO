using Unity.Netcode;
using UnityEngine;

/// <summary>
/// M0: 最初のネットワーク動作確認スクリプト
/// 空の GameObject にアタッチして使用
/// Host / Client / Server ボタンで接続テスト
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
    /// </summary>
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));

        if (!NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsServer)
        {
            GUILayout.Label("=== SSMO Network Test ===");
            GUILayout.Space(10);

            if (GUILayout.Button("Host (Server + Client)", GUILayout.Height(40)))
                NetworkManager.Singleton.StartHost();

            GUILayout.Space(5);

            if (GUILayout.Button("Client", GUILayout.Height(40)))
                NetworkManager.Singleton.StartClient();

            GUILayout.Space(5);

            if (GUILayout.Button("Dedicated Server", GUILayout.Height(40)))
                NetworkManager.Singleton.StartServer();
        }
        else
        {
            string mode = NetworkManager.Singleton.IsHost ? "Host" :
                          NetworkManager.Singleton.IsServer ? "Server" : "Client";

            GUILayout.Label($"=== SSMO [{mode}] ===");
            GUILayout.Label($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
            GUILayout.Label($"Local Client ID: {NetworkManager.Singleton.LocalClientId}");
            GUILayout.Label($"Transport: {NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetType().Name}");

            GUILayout.Space(10);

            if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.EndArea();
    }
}
