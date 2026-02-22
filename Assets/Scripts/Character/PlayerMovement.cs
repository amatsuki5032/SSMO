using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー権威型のプレイヤー移動
///
/// 設計方針:
/// - 移動の最終決定権はサーバーが持つ。クライアントの値は一切信用しない
/// - NetworkTransform は使わず NetworkVariable で自前同期する
///   → M1-3 でクライアント予測・リコンシリエーションを追加するため
/// - 現段階（M1-2）はサーバー権威のみ。予測なしのため入力遅延が体感される
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    // --- 同期変数 ---
    // サーバーのみ書き込み可能。全クライアントが読み取る
    private readonly NetworkVariable<Vector3> _netPosition = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> _netRotationY = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // --- ローカル参照 ---
    private CharacterController _controller;
    private float _verticalVelocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // サーバー: 初期位置を NetworkVariable に反映
        if (IsServer)
        {
            _netPosition.Value = transform.position;
            _netRotationY.Value = transform.eulerAngles.y;
        }
    }

    private void Update()
    {
        // オーナークライアントのみ入力を送信
        // IsOwner はホスト（サーバー兼クライアント）の場合も true になる
        if (!IsOwner) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 入力がある場合のみ ServerRpc を送信（帯域節約）
        if (h != 0f || v != 0f)
        {
            SubmitMoveInputServerRpc(h, v);
        }
    }

    private void LateUpdate()
    {
        // 他プレイヤー（非オーナー）は NetworkVariable の値を直接反映
        // M1-3 で補間（Interpolation）を追加する箇所
        if (IsOwner) return;

        transform.position = _netPosition.Value;
        transform.rotation = Quaternion.Euler(0f, _netRotationY.Value, 0f);
    }

    /// <summary>
    /// クライアント → サーバーへの移動入力送信
    /// サーバー側で入力バリデーション → 移動計算 → NetworkVariable 更新
    /// </summary>
    [ServerRpc]
    private void SubmitMoveInputServerRpc(float horizontal, float vertical)
    {
        // --- 入力バリデーション ---
        // クライアントの値を信用しない。不正値を弾く
        horizontal = Mathf.Clamp(horizontal, -1f, 1f);
        vertical = Mathf.Clamp(vertical, -1f, 1f);

        Vector3 inputDir = new Vector3(horizontal, 0f, vertical);

        // 斜め移動で速度が √2 倍にならないよう正規化
        if (inputDir.sqrMagnitude > 1f)
        {
            inputDir.Normalize();
        }

        // --- 重力処理 ---
        if (_controller.isGrounded)
        {
            // 接地中は小さな下向き力で地面に吸着させる（斜面での浮き防止）
            _verticalVelocity = GameConfig.GROUND_STICK_FORCE;
        }
        else
        {
            // 空中では重力を加算
            _verticalVelocity += GameConfig.GRAVITY * Time.deltaTime;
        }

        // --- 移動計算 ---
        Vector3 moveVelocity = inputDir * GameConfig.MOVE_SPEED;
        moveVelocity.y = _verticalVelocity;

        _controller.Move(moveVelocity * Time.deltaTime);

        // --- 回転処理 ---
        // 入力方向に向かって滑らかに回転
        if (inputDir.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;
            float currentY = transform.eulerAngles.y;
            float newY = Mathf.MoveTowardsAngle(currentY, targetAngle, GameConfig.ROTATION_SPEED * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, newY, 0f);
        }

        // --- NetworkVariable に書き込み ---
        // サーバーが計算した正しい位置・回転を全クライアントに配信
        _netPosition.Value = transform.position;
        _netRotationY.Value = transform.eulerAngles.y;
    }
}
