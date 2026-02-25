using UnityEngine;

/// <summary>
/// 3人称カメラ（クライアント専用・MonoBehaviour）
///
/// 設計意図:
/// - ネットワーク同期不要のためMonoBehaviour（NetworkBehaviourではない）
/// - IsOwner のプレイヤーにのみ PlayerMovement.OnNetworkSpawn で動的にアタッチする
/// - マウスでカメラ回転、プレイヤーの後方上方から追従
/// - SphereCast で壁衝突検出し、カメラを手前に寄せる
/// - LateUpdate で処理（キャラ移動後にカメラを追従させる）
///
/// カメラ座標系:
/// - _yaw: 水平回転角（Y軸。0=北、90=東、マウスX軸で操作）
/// - _pitch: 垂直回転角（X軸。0=水平、正=上向き、マウスY軸で操作）
/// - プレイヤー位置 + 高さオフセット を注視点とし、そこから _yaw/_pitch 方向に CAMERA_DISTANCE 離れた位置にカメラを配置
/// </summary>
public class CameraController : MonoBehaviour
{
    // ============================================================
    // 追従対象
    // ============================================================

    private Transform _target;

    // ============================================================
    // カメラ状態
    // ============================================================

    private float _yaw;     // 水平回転角（度）
    private float _pitch;   // 垂直回転角（度）

#if UNITY_EDITOR
    // ============================================================
    // デバッグ俯瞰カメラ（Editor限定）
    // ============================================================

    private bool _debugFreeCamera;
    private Vector3 _freeCamPos;
    private float _freeCamYaw;
    private float _freeCamPitch;

    private const float DEBUG_CAM_SPEED = 20f;
    private const float DEBUG_CAM_FAST_MULT = 3f;       // Shift押し高速移動
    private const float DEBUG_CAM_SCROLL_SPEED = 30f;    // マウスホイール高さ変更速度

    /// <summary>デバッグ俯瞰カメラが有効かどうか</summary>
    public bool IsDebugFreeCamera => _debugFreeCamera;
#endif

    // ============================================================
    // 初期化
    // ============================================================

    /// <summary>
    /// 追従対象を設定する。PlayerMovement.OnNetworkSpawn から呼ばれる
    /// </summary>
    public void Initialize(Transform target)
    {
        _target = target;

        // 初期カメラ向きをキャラクターの背面方向に合わせる
        _yaw = target.eulerAngles.y;
        _pitch = 10f; // やや上から見下ろす初期角度

        // カーソルをロック（FPS/TPS スタイル）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// カメラの水平回転角を返す（PlayerMovement がカメラ基準の移動方向を計算するために使用）
    /// </summary>
    public float Yaw => _yaw;

    // ============================================================
    // 更新
    // ============================================================

    private void LateUpdate()
    {
#if UNITY_EDITOR
        // デバッグ俯瞰カメラ切替（BackQuote ` キー）
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            _debugFreeCamera = !_debugFreeCamera;
            if (_debugFreeCamera)
            {
                // プレイヤー上空30mから俯瞰開始
                _freeCamPos = (_target != null ? _target.position : transform.position) + Vector3.up * 30f;
                _freeCamYaw = _yaw;
                _freeCamPitch = 60f; // 下向き（俯瞰視点）
            }
            Debug.Log($"[Camera] デバッグ俯瞰カメラ: {(_debugFreeCamera ? "ON" : "OFF")}");
        }

        if (_debugFreeCamera)
        {
            UpdateDebugFreeCamera();
            return; // 通常カメラロジックをスキップ
        }
#endif

        if (_target == null) return;

        // --- マウス入力でカメラ回転 ---
        float mouseX = Input.GetAxis("Mouse X") * GameConfig.CAMERA_SENSITIVITY;
        float mouseY = Input.GetAxis("Mouse Y") * GameConfig.CAMERA_SENSITIVITY;

        _yaw += mouseX;
        _pitch -= mouseY; // マウス上 → pitch 減少（上を向く）
        _pitch = Mathf.Clamp(_pitch, GameConfig.CAMERA_MIN_PITCH, GameConfig.CAMERA_MAX_PITCH);

        // --- 注視点 = プレイヤー位置 + 高さオフセット ---
        Vector3 lookAtPoint = _target.position + Vector3.up * GameConfig.CAMERA_HEIGHT;

        // --- カメラ位置を yaw/pitch から計算 ---
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 offset = rotation * Vector3.back * GameConfig.CAMERA_DISTANCE;
        Vector3 desiredPosition = lookAtPoint + offset;

        // --- 壁衝突検出（SphereCast）---
        // 注視点からカメラ方向へ SphereCast して、障害物があればカメラを手前に寄せる
        Vector3 direction = desiredPosition - lookAtPoint;
        float distance = direction.magnitude;

        if (Physics.SphereCast(
                lookAtPoint,
                GameConfig.CAMERA_COLLISION_RADIUS,
                direction.normalized,
                out RaycastHit hit,
                distance))
        {
            // 衝突点の手前にカメラを配置（最小距離を保証）
            float clampedDist = Mathf.Max(hit.distance, GameConfig.CAMERA_MIN_DISTANCE);
            desiredPosition = lookAtPoint + direction.normalized * clampedDist;
        }

        // --- カメラ適用 ---
        transform.position = desiredPosition;
        transform.LookAt(lookAtPoint);
    }

#if UNITY_EDITOR
    // ============================================================
    // デバッグ俯瞰カメラ更新
    // ============================================================

    /// <summary>
    /// 俯瞰フリーカメラの更新処理
    /// WASD: 水平移動（yaw方向基準、ピッチに関係なく水平面を移動）
    /// マウス: 視点回転
    /// マウスホイール: 高さ変更
    /// Shift: 高速移動
    /// </summary>
    private void UpdateDebugFreeCamera()
    {
        // マウスで回転
        float mouseX = Input.GetAxis("Mouse X") * GameConfig.CAMERA_SENSITIVITY;
        float mouseY = Input.GetAxis("Mouse Y") * GameConfig.CAMERA_SENSITIVITY;
        _freeCamYaw += mouseX;
        _freeCamPitch -= mouseY;
        _freeCamPitch = Mathf.Clamp(_freeCamPitch, -89f, 89f);

        // WASD で水平移動（yaw方向基準、俯瞰でもWは前方＝北方向に移動）
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 flatForward = Quaternion.Euler(0f, _freeCamYaw, 0f) * Vector3.forward;
        Vector3 flatRight = Quaternion.Euler(0f, _freeCamYaw, 0f) * Vector3.right;

        float speed = DEBUG_CAM_SPEED;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= DEBUG_CAM_FAST_MULT;

        _freeCamPos += (flatForward * v + flatRight * h) * speed * Time.deltaTime;

        // マウスホイールで高さ変更
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _freeCamPos.y += scroll * DEBUG_CAM_SCROLL_SPEED;

        // カメラ適用
        transform.position = _freeCamPos;
        transform.rotation = Quaternion.Euler(_freeCamPitch, _freeCamYaw, 0f);
    }
#endif

    // ============================================================
    // クリーンアップ
    // ============================================================

    private void OnDestroy()
    {
        // カーソルロック解除（エディタ終了時等）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
