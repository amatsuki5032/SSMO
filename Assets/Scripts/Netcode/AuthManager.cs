using System;
using System.Text;
using Unity.Netcode;
using UnityEngine;
#if FIREBASE_AUTH
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
#endif

/// <summary>
/// プレイヤー認証管理（シングルトン）
/// Firebase Auth による匿名認証を行い、UID を管理する
/// Firebase SDK未導入時はダミーUIDで動作する（開発用フォールバック）
///
/// 接続フロー:
/// 1. Awake: シングルトン初期化
/// 2. Start: Firebase初期化 → 匿名認証開始
/// 3. 認証成功 → OnAuthStateChanged(true) 発火
/// 4. HelloNetwork が Host/Client ボタンを有効化
/// 5. Client接続時: UIDをペイロードとして送信
/// 6. Server側: ConnectionApprovalCallback でUID検証
/// </summary>
public class AuthManager : MonoBehaviour
{
    // ============================================================
    // シングルトン
    // ============================================================

    public static AuthManager Instance { get; private set; }

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>認証済みプレイヤーのUID（未認証時はnull）</summary>
    public string CurrentUid { get; private set; }

    /// <summary>認証済みかどうか</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(CurrentUid);

    /// <summary>認証状態変更イベント（true=認証済み, false=未認証/失敗）</summary>
    public event Action<bool> OnAuthStateChanged;

    // ============================================================
    // 内部状態
    // ============================================================

    // 初期化中フラグ（タイムアウト監視用）
    private bool _isInitializing;
    private float _initStartTime;

    #if FIREBASE_AUTH
    private FirebaseAuth _auth;
    #endif

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

    private void Start()
    {
        InitializeAuth();
    }

    private void Update()
    {
        // 認証タイムアウト監視
        if (_isInitializing && Time.time - _initStartTime > GameConfig.AUTH_TIMEOUT_SEC)
        {
            _isInitializing = false;
            Debug.LogError("[AuthManager] 認証タイムアウト");
            OnAuthStateChanged?.Invoke(false);
        }
    }

    private void OnDestroy()
    {
        #if FIREBASE_AUTH
        if (_auth != null)
            _auth.StateChanged -= HandleFirebaseAuthStateChanged;
        #endif
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 初期化
    // ============================================================

    private void InitializeAuth()
    {
        _isInitializing = true;
        _initStartTime = Time.time;

        #if FIREBASE_AUTH
        // Firebase SDK初期化 → 依存関係チェック → 匿名認証
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                _auth = FirebaseAuth.DefaultInstance;
                _auth.StateChanged += HandleFirebaseAuthStateChanged;

                // 既にログイン済みならUIDを復元、そうでなければ匿名認証
                if (_auth.CurrentUser != null)
                {
                    CurrentUid = _auth.CurrentUser.UserId;
                    _isInitializing = false;
                    Debug.Log($"[AuthManager] 既存セッション復元: UID={CurrentUid}");
                    OnAuthStateChanged?.Invoke(true);
                }
                else
                {
                    SignInAnonymously();
                }
            }
            else
            {
                _isInitializing = false;
                Debug.LogError($"[AuthManager] Firebase依存関係エラー: {task.Result}");
                OnAuthStateChanged?.Invoke(false);
            }
        });
        #else
        // Firebase未導入: ダミーUIDで動作（開発用フォールバック）
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        CurrentUid = $"dev_{deviceId.Substring(0, Mathf.Min(8, deviceId.Length))}";
        _isInitializing = false;
        Debug.Log($"[AuthManager] Firebase未導入 — ダミーUID使用: {CurrentUid}");
        OnAuthStateChanged?.Invoke(true);
        #endif
    }

    // ============================================================
    // Firebase認証（FIREBASE_AUTH定義時のみ有効）
    // ============================================================

    #if FIREBASE_AUTH
    /// <summary>匿名認証を実行する</summary>
    private void SignInAnonymously()
    {
        Debug.Log("[AuthManager] 匿名認証開始...");
        _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            _isInitializing = false;

            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError($"[AuthManager] 匿名認証失敗: {task.Exception}");
                OnAuthStateChanged?.Invoke(false);
                return;
            }

            CurrentUid = task.Result.User.UserId;
            Debug.Log($"[AuthManager] 匿名認証成功: UID={CurrentUid}");
            OnAuthStateChanged?.Invoke(true);
        });
    }

    /// <summary>Firebase認証状態変更コールバック</summary>
    private void HandleFirebaseAuthStateChanged(object sender, EventArgs e)
    {
        var user = _auth.CurrentUser;
        if (user != null)
        {
            CurrentUid = user.UserId;
            OnAuthStateChanged?.Invoke(true);
        }
        else
        {
            CurrentUid = null;
            OnAuthStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// トークンを強制リフレッシュする（長時間プレイ時に呼ぶ）
    /// </summary>
    public void RefreshToken()
    {
        if (_auth?.CurrentUser == null) return;
        _auth.CurrentUser.TokenAsync(forceRefresh: true).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[AuthManager] トークンリフレッシュ失敗: {task.Exception}");
                return;
            }
            Debug.Log("[AuthManager] トークンリフレッシュ成功");
        });
    }
    #endif

    // ============================================================
    // 接続ペイロード（クライアント→サーバー UID送信用）
    // ============================================================

    /// <summary>
    /// 接続リクエストに含めるUIDペイロードを取得する
    /// NetworkManager.NetworkConfig.ConnectionData に設定する
    /// </summary>
    public byte[] GetConnectionPayload()
    {
        return Encoding.UTF8.GetBytes(CurrentUid ?? "");
    }

    /// <summary>
    /// 接続リクエストからUIDを抽出する（サーバー側で使用）
    /// </summary>
    public static string ExtractUidFromPayload(byte[] payload)
    {
        if (payload == null || payload.Length == 0) return null;
        return Encoding.UTF8.GetString(payload);
    }

    // ============================================================
    // サーバー側接続承認（ConnectionApprovalCallback）
    // ============================================================

    /// <summary>
    /// NetworkManager の ConnectionApprovalCallback を設定する
    /// Host/Server起動前に呼び出す
    /// </summary>
    public void SetupConnectionApproval()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback = ApproveConnection;
        Debug.Log("[AuthManager] ConnectionApprovalCallback 登録完了");
    }

    /// <summary>
    /// クライアント接続時のUID検証（サーバー側）
    /// UIDが空なら接続拒否
    /// </summary>
    private void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        string uid = ExtractUidFromPayload(request.Payload);

        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning($"[AuthManager] 接続拒否: UID未送信 (clientId={request.ClientNetworkId})");
            response.Approved = false;
            response.Reason = "認証情報がありません";
            return;
        }

        // TODO: Firebase Admin SDK によるトークン検証（将来: Cloud Function経由）

        Debug.Log($"[AuthManager] 接続承認: UID={uid} (clientId={request.ClientNetworkId})");
        response.Approved = true;
        response.CreatePlayerObject = true;
    }
}
