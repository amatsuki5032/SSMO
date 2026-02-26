using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
#if UNITY_RELAY
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
#endif

/// <summary>
/// Unity Relay Service 接続管理（シングルトン）
/// NAT越え接続のための Relay サーバーを仲介する
///
/// ホスト: CreateRelay() → JoinCode取得 → StartHost
/// クライアント: JoinRelay(code) → StartClient
///
/// Relay SDK 未導入時は localhost 直結にフォールバック
/// ParrelSync テスト時も localhost 直結で使用（Relay不要）
/// </summary>
public class RelayManager : MonoBehaviour
{
    // ============================================================
    // シングルトン
    // ============================================================

    public static RelayManager Instance { get; private set; }

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>ホスト側のJoinCode（クライアントに伝える接続コード）</summary>
    public string JoinCode { get; private set; }

    /// <summary>Relay経由の接続が有効か</summary>
    public bool IsRelayActive { get; private set; }

    // ============================================================
    // 内部状態
    // ============================================================

    private bool _isUgsInitialized;

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
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // UGS初期化（Relay SDK導入時のみ）
    // ============================================================

    /// <summary>
    /// Unity Gaming Services を初期化する（Relay/Authentication）
    /// 初回のみ実行、2回目以降はスキップ
    /// </summary>
    private async Task InitializeUGS()
    {
        #if UNITY_RELAY
        if (_isUgsInitialized) return;

        await UnityServices.InitializeAsync();

        // Unity Authentication（Firebase Authとは別のUGS認証）
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[RelayManager] UGS認証完了: PlayerId={AuthenticationService.Instance.PlayerId}");
        }

        _isUgsInitialized = true;
        #else
        _isUgsInitialized = true;
        await Task.CompletedTask;
        #endif
    }

    // ============================================================
    // ホスト: Relay作成
    // ============================================================

    /// <summary>
    /// Relay サーバーにアロケーションを作成し、JoinCodeを取得する
    /// 成功後に NetworkManager.StartHost() を呼ぶ
    /// </summary>
    /// <returns>JoinCode（成功時）/ null（失敗時またはRelay未導入時）</returns>
    public async Task<string> CreateRelay()
    {
        #if UNITY_RELAY
        try
        {
            await InitializeUGS();

            // maxConnections = 自分以外の最大接続数
            int maxConnections = GameConfig.MAX_PLAYERS - 1;
            Debug.Log($"[RelayManager] Relay作成中... (maxConnections={maxConnections})");

            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // UnityTransport に Relay サーバー情報を設定
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[RelayManager] UnityTransport が見つかりません");
                return null;
            }

            var relayData = new RelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayData);

            JoinCode = joinCode;
            IsRelayActive = true;

            Debug.Log($"[RelayManager] Relay作成成功: JoinCode={joinCode}");
            return joinCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayManager] Relay作成失敗: {e.Message}");
            return null;
        }
        #else
        Debug.Log("[RelayManager] Relay未導入 — localhost直結を使用");
        await Task.CompletedTask;
        return null;
        #endif
    }

    // ============================================================
    // クライアント: Relay参加
    // ============================================================

    /// <summary>
    /// JoinCodeを使ってRelay サーバーに参加する
    /// 成功後に NetworkManager.StartClient() を呼ぶ
    /// </summary>
    /// <param name="joinCode">ホストから受け取ったJoinCode</param>
    /// <returns>true: 成功 / false: 失敗またはRelay未導入</returns>
    public async Task<bool> JoinRelay(string joinCode)
    {
        #if UNITY_RELAY
        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning("[RelayManager] JoinCodeが空です");
            return false;
        }

        try
        {
            await InitializeUGS();

            Debug.Log($"[RelayManager] Relay参加中... (JoinCode={joinCode})");

            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // UnityTransport に Relay サーバー情報を設定
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[RelayManager] UnityTransport が見つかりません");
                return false;
            }

            var relayData = new RelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayData);

            IsRelayActive = true;
            JoinCode = joinCode;

            Debug.Log($"[RelayManager] Relay参加成功");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayManager] Relay参加失敗: {e.Message}");
            return false;
        }
        #else
        Debug.Log("[RelayManager] Relay未導入 — localhost直結を使用");
        await Task.CompletedTask;
        return false;
        #endif
    }
}
