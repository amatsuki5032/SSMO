using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー権威型プレイヤー移動 + クライアント予測 & リコンシリエーション
///
/// アーキテクチャ:
/// 1. クライアント（IsOwner）: FixedUpdate で入力取得 → ローカル予測 → ServerRpc 送信
/// 2. サーバー: 入力受信 → 権威的な移動計算 → NetworkVariable 更新 → ClientRpc で確定状態返送
/// 3. クライアント（IsOwner）: サーバー確定状態と予測を比較 → ズレたら巻き戻し＋再シミュレーション
/// 4. 他プレイヤー（!IsOwner）: NetworkVariable を直接反映（M1-4 で補間に変更予定）
///
/// 参考: Gabriel Gambetta "Client-Side Prediction and Server Reconciliation"
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    // ============================================================
    // データ構造
    // ============================================================

    /// <summary>
    /// 移動入力データ。RPC でシリアライズ可能にする
    /// ティック番号を付与することで、サーバーからの確定応答と対応付ける
    /// </summary>
    private struct MoveInput : INetworkSerializable
    {
        public uint Tick;
        public float Horizontal;
        public float Vertical;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Horizontal);
            serializer.SerializeValue(ref Vertical);
        }
    }

    /// <summary>
    /// 移動状態のスナップショット。リコンシリエーション時の比較・復元に使う
    /// </summary>
    private struct MoveState
    {
        public Vector3 Position;
        public float RotationY;
        public float VerticalVelocity;
    }

    // ============================================================
    // 同期変数（他プレイヤー表示用）
    // ============================================================

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

    // ============================================================
    // ローカル状態
    // ============================================================

    private CharacterController _controller;
    private float _verticalVelocity;

    // --- ティックカウンター ---
    // NGO の ServerTime.Tick ではなく自前カウンターを使う理由:
    // 予測・リコンシリエーションでは入力のシーケンス番号として使うだけなので、
    // サーバーとの時刻同期は不要。クライアントが送ったティック番号を
    // サーバーがそのままエコーバックする仕組み
    private uint _currentTick;

    // --- 入力バッファ（Update → FixedUpdate 橋渡し）---
    // Update() は可変FPSで呼ばれるため、最新の入力を保持して FixedUpdate() で消費する
    private float _inputH;
    private float _inputV;

    // --- クライアント予測用リングバッファ ---
    // 過去の入力と予測結果を保持し、リコンシリエーション時のリプレイに使う
    private MoveInput[] _inputBuffer;
    private MoveState[] _stateBuffer;

    // --- リコンシリエーション ---
    // サーバーから最後に受信した確定ティック。古いパケットの破棄に使用
    private uint _lastServerTick;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _netPosition.Value = transform.position;
            _netRotationY.Value = transform.eulerAngles.y;
        }

        // オーナーのみ予測バッファを確保（他プレイヤーのインスタンスでは不要）
        if (IsOwner)
        {
            _inputBuffer = new MoveInput[GameConfig.PREDICTION_BUFFER_SIZE];
            _stateBuffer = new MoveState[GameConfig.PREDICTION_BUFFER_SIZE];
        }
    }

    /// <summary>
    /// Update: 入力取得（可変FPS）
    /// FixedUpdate は固定間隔のため、フレーム毎の入力をここで取得して保持する
    /// </summary>
    private void Update()
    {
        if (!IsOwner) return;

        _inputH = Input.GetAxisRaw("Horizontal");
        _inputV = Input.GetAxisRaw("Vertical");
    }

    /// <summary>
    /// FixedUpdate: ゲームロジック（60Hz固定）
    /// 全ての移動計算はここで行う。描画FPSに依存しない決定論的シミュレーション
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsOwner) return;

        ProcessOwnerTick();
    }

    /// <summary>
    /// LateUpdate: 他プレイヤー表示更新
    /// M1-4 で補間（Interpolation）に差し替え予定
    /// </summary>
    private void LateUpdate()
    {
        if (IsOwner) return;

        transform.position = _netPosition.Value;
        transform.rotation = Quaternion.Euler(0f, _netRotationY.Value, 0f);
    }

    // ============================================================
    // オーナー処理（クライアント予測）
    // ============================================================

    /// <summary>
    /// オーナーの1ティック処理
    /// ホストとリモートクライアントで処理を分岐する
    /// </summary>
    private void ProcessOwnerTick()
    {
        MoveInput input = new MoveInput
        {
            Tick = _currentTick,
            Horizontal = _inputH,
            Vertical = _inputV
        };

        if (IsServer)
        {
            // --- ホスト（サーバー兼オーナー）---
            // サーバーが直接計算するので予測もリコンシリエーションも不要
            // 自分自身が権威なのでズレが発生しない
            ApplyMovement(input.Horizontal, input.Vertical);
            _netPosition.Value = transform.position;
            _netRotationY.Value = transform.eulerAngles.y;
        }
        else
        {
            // --- リモートクライアント ---
            // Step 1: サーバーに入力を送信
            SubmitMoveInputServerRpc(input);

            // Step 2: サーバーの応答を待たずにローカルで即座に移動計算（予測）
            //         これによりRTT分の入力遅延を隠蔽する
            ApplyMovement(input.Horizontal, input.Vertical);

            // Step 3: 入力と予測結果をリングバッファに保存
            //         リコンシリエーション時にリプレイするために必要
            int idx = (int)(_currentTick % GameConfig.PREDICTION_BUFFER_SIZE);
            _inputBuffer[idx] = input;
            _stateBuffer[idx] = new MoveState
            {
                Position = transform.position,
                RotationY = transform.eulerAngles.y,
                VerticalVelocity = _verticalVelocity
            };
        }

        _currentTick++;
    }

    // ============================================================
    // 共通移動計算
    // ============================================================

    /// <summary>
    /// 移動計算の共通処理
    /// クライアント予測・サーバー権威計算・リコンシリエーションリプレイの全てでこのメソッドを使う
    /// 同じ入力 → 同じ結果を返すことが予測精度の鍵
    /// deltaTime は GameConfig.FIXED_DELTA_TIME を固定使用（決定論的シミュレーション）
    /// </summary>
    private void ApplyMovement(float horizontal, float vertical)
    {
        // 入力バリデーション（クライアント値を信用しない）
        horizontal = Mathf.Clamp(horizontal, -1f, 1f);
        vertical = Mathf.Clamp(vertical, -1f, 1f);

        Vector3 inputDir = new Vector3(horizontal, 0f, vertical);

        // 斜め移動で速度が √2 倍にならないよう正規化
        if (inputDir.sqrMagnitude > 1f)
        {
            inputDir.Normalize();
        }

        // 重力処理
        if (_controller.isGrounded)
        {
            _verticalVelocity = GameConfig.GROUND_STICK_FORCE;
        }
        else
        {
            _verticalVelocity += GameConfig.GRAVITY * GameConfig.FIXED_DELTA_TIME;
        }

        // 移動（固定デルタタイム使用で決定論的）
        Vector3 velocity = inputDir * GameConfig.MOVE_SPEED;
        velocity.y = _verticalVelocity;
        _controller.Move(velocity * GameConfig.FIXED_DELTA_TIME);

        // 回転（入力方向に向かって滑らかに回転）
        if (inputDir.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;
            float currentY = transform.eulerAngles.y;
            float newY = Mathf.MoveTowardsAngle(
                currentY, targetAngle,
                GameConfig.ROTATION_SPEED * GameConfig.FIXED_DELTA_TIME
            );
            transform.rotation = Quaternion.Euler(0f, newY, 0f);
        }
    }

    // ============================================================
    // サーバー処理
    // ============================================================

    /// <summary>
    /// クライアント → サーバーへの入力送信
    /// サーバーは権威として移動を計算し、確定状態をクライアントに返す
    /// </summary>
    [ServerRpc]
    private void SubmitMoveInputServerRpc(MoveInput input)
    {
        // サーバー権威で移動計算
        ApplyMovement(input.Horizontal, input.Vertical);

        // 他プレイヤー表示用に NetworkVariable を更新
        _netPosition.Value = transform.position;
        _netRotationY.Value = transform.eulerAngles.y;

        // オーナーに確定状態を返送（リコンシリエーション用）
        // ClientRpc は全クライアントに届くが、処理するのはオーナーのみ
        // TODO: ClientRpcParams で送信先をオーナーに限定して帯域を節約する
        ConfirmStateClientRpc(
            input.Tick,
            transform.position,
            transform.eulerAngles.y,
            _verticalVelocity
        );
    }

    // ============================================================
    // リコンシリエーション
    // ============================================================

    /// <summary>
    /// サーバーからの確定状態受信
    /// 予測位置とサーバー確定位置を比較し、閾値を超えていたら巻き戻し＋再シミュレーション
    /// </summary>
    [ClientRpc]
    private void ConfirmStateClientRpc(
        uint tick, Vector3 serverPosition, float serverRotationY, float serverVerticalVelocity)
    {
        // リコンシリエーションはリモートクライアントのオーナーのみ実行
        // - サーバー（ホスト含む）: 自身が権威なので不要
        // - 非オーナー: NetworkVariable で表示更新するため不要
        if (!IsOwner || IsServer) return;

        // 古いパケットは無視（ネットワーク順序の入れ替わり対策）
        if (tick < _lastServerTick) return;
        _lastServerTick = tick;

        // 該当ティックの予測状態をリングバッファから取得
        int idx = (int)(tick % GameConfig.PREDICTION_BUFFER_SIZE);

        // バッファのティックが一致しない場合はバッファが一周して上書きされている
        // （極端に古いパケットか、バッファサイズ不足）
        if (_inputBuffer[idx].Tick != tick) return;

        // 予測位置とサーバー確定位置の誤差を比較
        float posError = Vector3.Distance(_stateBuffer[idx].Position, serverPosition);

        // 許容誤差内なら修正不要（予測が正しかった）
        if (posError <= GameConfig.RECONCILIATION_THRESHOLD) return;

        // --- 巻き戻し（Rewind）---
        // サーバーの確定状態にスナップ
        transform.position = serverPosition;
        transform.rotation = Quaternion.Euler(0f, serverRotationY, 0f);
        _verticalVelocity = serverVerticalVelocity;

        // --- 再シミュレーション（Replay）---
        // 確定ティック以降の保存済み入力を順番に再適用
        // CharacterController.Move() を高速ループで回す（描画は発生しない）
        uint replayTick = tick + 1;
        while (replayTick < _currentTick)
        {
            int replayIdx = (int)(replayTick % GameConfig.PREDICTION_BUFFER_SIZE);

            // バッファの整合性チェック
            if (_inputBuffer[replayIdx].Tick != replayTick) break;

            ApplyMovement(_inputBuffer[replayIdx].Horizontal, _inputBuffer[replayIdx].Vertical);

            // リプレイ結果でバッファを更新（次回のリコンシリエーションに備える）
            _stateBuffer[replayIdx] = new MoveState
            {
                Position = transform.position,
                RotationY = transform.eulerAngles.y,
                VerticalVelocity = _verticalVelocity
            };

            replayTick++;
        }
    }
}
