using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー権威型プレイヤー移動 + クライアント予測 & リコンシリエーション
///
/// アーキテクチャ:
/// 1. クライアント（IsOwner）: FixedUpdate で入力取得 → ローカル予測 → ServerRpc 送信
/// 2. サーバー: 入力受信 → 権威的な移動計算 → NetworkVariable 更新 → ClientRpc で確定状態返送
/// 3. クライアント（IsOwner）: サーバー確定状態と予測を比較 → ズレたら巻き戻し＋再シミュレーション
/// 4. 他プレイヤー（!IsOwner）: 補間表示（100ms遅延で滑らかに表示）
///
/// 参考: Gabriel Gambetta "Client-Side Prediction and Server Reconciliation"
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStateMachine))]
public class PlayerMovement : NetworkBehaviour
{
    // ============================================================
    // データ構造
    // ============================================================

    // 入力データは PlayerInput（Assets/Scripts/Shared/PlayerInput.cs）を使用

    /// <summary>
    /// 移動状態のスナップショット。リコンシリエーション時の比較・復元に使う
    /// </summary>
    private struct MoveState
    {
        public Vector3 Position;
        public float RotationY;
        public float VerticalVelocity;
        public bool IsJumping;          // リコンシリエーションリプレイ用
        public Vector3 JumpLaunchDir;   // リコンシリエーションリプレイ用
    }

    /// <summary>
    /// 補間用スナップショット。他プレイヤー表示の滑らか補間に使う
    /// サーバーから受信した位置・回転をタイムスタンプ付きで保持する
    /// </summary>
    private struct InterpolationState
    {
        public double Timestamp;
        public Vector3 Position;
        public float RotationY;
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
    private CharacterStateMachine _stateMachine;
    private float _verticalVelocity;

    // --- ジャンプ ---
    private Vector3 _jumpLaunchDir;  // 離陸時の水平方向（ジャンプ中維持、方向転換不可）
    private bool _isJumping;         // ジャンプ中フラグ（クライアント予測 + サーバー権威）

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
    private bool _jumpPressed;      // ジャンプ（押した瞬間のみ、消費後リセット）
    private bool _guardHeld;        // ガード（押しっぱなし）

    // --- クライアント予測用リングバッファ ---
    // 過去の入力と予測結果を保持し、リコンシリエーション時のリプレイに使う
    private PlayerInput[] _inputBuffer;
    private MoveState[] _stateBuffer;

    // --- リコンシリエーション ---
    // サーバーから最後に受信した確定ティック。古いパケットの破棄に使用
    private uint _lastServerTick;

    // --- 補間用リングバッファ（!IsOwner 用）---
    // サーバーから受信した状態を時系列順に保持し、
    // 100ms 遅延で2点間を Lerp することで滑らかに表示する
    private InterpolationState[] _interpBuffer;
    private int _interpCount;      // バッファ内の有効エントリ数
    private int _interpWriteIdx;   // 次の書き込み位置

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _stateMachine = GetComponent<CharacterStateMachine>();
        if (_stateMachine == null)
        {
            Debug.LogError($"[PlayerMovement] {gameObject.name}: CharacterStateMachine が見つかりません");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _netPosition.Value = transform.position;
            _netRotationY.Value = transform.eulerAngles.y;

            // ラグコンペンセーション用にプレイヤーを登録
            LagCompensationManager.Instance.RegisterPlayer(OwnerClientId, transform);
        }

        // オーナーのみ予測バッファを確保（他プレイヤーのインスタンスでは不要）
        if (IsOwner)
        {
            _inputBuffer = new PlayerInput[GameConfig.PREDICTION_BUFFER_SIZE];
            _stateBuffer = new MoveState[GameConfig.PREDICTION_BUFFER_SIZE];
        }

        // リモートクライアント上の他プレイヤー: 補間バッファを初期化
        // サーバー（ホスト含む）は ApplyMovement で直接 transform を更新するため不要
        if (!IsOwner && !IsServer)
        {
            _interpBuffer = new InterpolationState[GameConfig.INTERPOLATION_BUFFER_SIZE];

            // 初期状態をバッファに記録（OnValueChanged は初期値では発火しないため）
            _interpBuffer[0] = new InterpolationState
            {
                Timestamp = NetworkManager.Singleton.ServerTime.Time,
                Position = _netPosition.Value,
                RotationY = _netRotationY.Value
            };
            _interpWriteIdx = 1;
            _interpCount = 1;

            _netPosition.OnValueChanged += OnNetPositionChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        // サーバー: ラグコンペンセーション登録解除
        if (IsServer)
        {
            LagCompensationManager.Instance.UnregisterPlayer(OwnerClientId);
        }

        // コールバック解除（メモリリーク防止）
        if (!IsOwner && !IsServer)
        {
            _netPosition.OnValueChanged -= OnNetPositionChanged;
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

        // ジャンプは押した瞬間のみ true にする（FixedUpdate で消費されるまで保持）
        if (Input.GetKeyDown(KeyCode.Space))
            _jumpPressed = true;

        // ガードは押しっぱなしで true
        _guardHeld = Input.GetKey(KeyCode.LeftShift);
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
    /// LateUpdate: 他プレイヤーの補間表示（描画FPS で実行）
    /// 100ms 遅延で過去の2状態間を線形補間し、30Hz の状態更新を滑らかに表示する
    /// </summary>
    private void LateUpdate()
    {
        // オーナーはクライアント予測で表示するため補間不要
        if (IsOwner) return;

        // サーバー（ホスト含む）は ApplyMovement で直接 transform を更新しているため補間不要
        if (IsServer) return;

        ApplyInterpolation();
    }

    // ============================================================
    // 補間処理（他プレイヤー表示用）
    // ============================================================

    /// <summary>
    /// NetworkVariable の位置変更コールバック
    /// サーバーから受信した状態をタイムスタンプ付きでリングバッファに記録する
    /// </summary>
    private void OnNetPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        _interpBuffer[_interpWriteIdx] = new InterpolationState
        {
            Timestamp = NetworkManager.Singleton.ServerTime.Time,
            Position = newValue,
            RotationY = _netRotationY.Value
        };
        _interpWriteIdx = (_interpWriteIdx + 1) % GameConfig.INTERPOLATION_BUFFER_SIZE;
        if (_interpCount < GameConfig.INTERPOLATION_BUFFER_SIZE)
            _interpCount++;
    }

    /// <summary>
    /// 補間表示の適用
    /// 表示時刻（現在 - 100ms）を挟む2つの状態スナップショットを見つけ、
    /// 線形補間で滑らかに表示する
    /// </summary>
    private void ApplyInterpolation()
    {
        // バッファが空なら NetworkVariable の値を直接適用（安全策）
        if (_interpCount == 0)
        {
            transform.position = _netPosition.Value;
            transform.rotation = Quaternion.Euler(0f, _netRotationY.Value, 0f);
            return;
        }

        // 表示時刻 = 現在のサーバー時刻 - 補間遅延（100ms）
        // 過去の状態を表示することで、パケット間の補間が可能になる
        double renderTime = NetworkManager.Singleton.ServerTime.Time - GameConfig.INTERPOLATION_DELAY;

        // バッファから補間対象の2状態を探す
        // 最古のエントリから順に走査し、renderTime を挟む区間を見つける
        int oldestIdx = (_interpWriteIdx - _interpCount + GameConfig.INTERPOLATION_BUFFER_SIZE)
                        % GameConfig.INTERPOLATION_BUFFER_SIZE;

        InterpolationState before = default;
        InterpolationState after = default;
        bool found = false;

        for (int i = 0; i < _interpCount - 1; i++)
        {
            int currIdx = (oldestIdx + i) % GameConfig.INTERPOLATION_BUFFER_SIZE;
            int nextIdx = (oldestIdx + i + 1) % GameConfig.INTERPOLATION_BUFFER_SIZE;

            if (_interpBuffer[currIdx].Timestamp <= renderTime
                && renderTime <= _interpBuffer[nextIdx].Timestamp)
            {
                before = _interpBuffer[currIdx];
                after = _interpBuffer[nextIdx];
                found = true;
                break;
            }
        }

        // 補間対象が見つからない場合（バッファ不足・パケットロス）
        // 最新の状態をそのまま適用する安全策
        if (!found)
        {
            int newestIdx = (_interpWriteIdx - 1 + GameConfig.INTERPOLATION_BUFFER_SIZE)
                            % GameConfig.INTERPOLATION_BUFFER_SIZE;
            transform.position = _interpBuffer[newestIdx].Position;
            transform.rotation = Quaternion.Euler(0f, _interpBuffer[newestIdx].RotationY, 0f);
            return;
        }

        // スナップ閾値チェック: 距離が大きすぎる場合は瞬間移動（テレポート対策）
        if (Vector3.Distance(before.Position, after.Position) > GameConfig.SNAP_THRESHOLD)
        {
            transform.position = after.Position;
            transform.rotation = Quaternion.Euler(0f, after.RotationY, 0f);
            return;
        }

        // 2状態間の線形補間
        double duration = after.Timestamp - before.Timestamp;
        float t = (duration > 0.0)
            ? Mathf.Clamp01((float)((renderTime - before.Timestamp) / duration))
            : 1f;

        transform.position = Vector3.Lerp(before.Position, after.Position, t);
        transform.rotation = Quaternion.Slerp(
            Quaternion.Euler(0f, before.RotationY, 0f),
            Quaternion.Euler(0f, after.RotationY, 0f),
            t
        );
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
        // 全入力を生のまま構造体に格納（フィルタリングはサーバーが行う）
        PlayerInput input = new PlayerInput
        {
            MoveInput = new Vector2(_inputH, _inputV),
            JumpPressed = _jumpPressed,
            GuardHeld = _guardHeld,
            AttackPressed = false,  // M2-3で実装
            ChargePressed = false,  // M2-4で実装
            MusouPressed = false,   // M2-8で実装
            Tick = _currentTick
        };

        // ジャンプ入力は消費後リセット（1ティックのみ有効）
        _jumpPressed = false;

        if (IsServer)
        {
            // --- ホスト（サーバー兼オーナー）---
            ProcessJump(input, true);
            Vector2 move = GetEffectiveMove(input);
            if (!_isJumping) UpdateMoveState(move.x, move.y);
            ApplyMovement(move.x, move.y);
            CheckLanding(true);
            _netPosition.Value = transform.position;
            _netRotationY.Value = transform.eulerAngles.y;
        }
        else
        {
            // --- リモートクライアント ---
            // サーバーに生入力を送信
            SubmitInputServerRpc(input);

            // クライアント予測: ローカルでも即座にジャンプ実行
            ProcessJump(input, false);
            Vector2 move = GetEffectiveMove(input);
            ApplyMovement(move.x, move.y);
            CheckLanding(false);

            int idx = (int)(_currentTick % GameConfig.PREDICTION_BUFFER_SIZE);
            _inputBuffer[idx] = input;
            _stateBuffer[idx] = new MoveState
            {
                Position = transform.position,
                RotationY = transform.eulerAngles.y,
                VerticalVelocity = _verticalVelocity,
                IsJumping = _isJumping,
                JumpLaunchDir = _jumpLaunchDir
            };
        }

        _currentTick++;
    }

    /// <summary>
    /// Idle ↔ Move のステート遷移を管理する（サーバーのみ）
    /// 入力がある → Move、入力がない → Idle
    /// </summary>
    private void UpdateMoveState(float horizontal, float vertical)
    {
        if (_stateMachine == null) return;

        bool hasInput = (horizontal * horizontal + vertical * vertical) > 0.01f;
        CharacterState current = _stateMachine.CurrentState;

        if (hasInput && current == CharacterState.Idle)
        {
            _stateMachine.TryChangeState(CharacterState.Move);
        }
        else if (!hasInput && current == CharacterState.Move)
        {
            _stateMachine.TryChangeState(CharacterState.Idle);
        }
    }

    // ============================================================
    // ジャンプ処理
    // ============================================================

    /// <summary>
    /// ジャンプ開始判定。サーバー権威でステート遷移、クライアントは予測実行
    /// Idle/Move のときのみジャンプ可。離陸時の方向を保持する
    /// </summary>
    private void ProcessJump(PlayerInput input, bool isServerAuthority)
    {
        if (!input.JumpPressed || _isJumping) return;

        bool canJump = _stateMachine != null
            && _stateMachine.CanAcceptInput(InputType.Jump);
        if (!canJump) return;

        _isJumping = true;
        _verticalVelocity = GameConfig.JUMP_FORCE;

        // 離陸時の水平方向を保存（ジャンプ中は方向転換不可）
        _jumpLaunchDir = new Vector3(input.MoveInput.x, 0f, input.MoveInput.y);
        if (_jumpLaunchDir.sqrMagnitude > 1f)
            _jumpLaunchDir.Normalize();

        if (isServerAuthority)
            _stateMachine.TryChangeState(CharacterState.Jump);
    }

    /// <summary>
    /// ジャンプ中は離陸時の方向を使い、地上では通常の入力を使う
    /// </summary>
    private Vector2 GetEffectiveMove(PlayerInput input)
    {
        if (_isJumping)
            return new Vector2(_jumpLaunchDir.x, _jumpLaunchDir.z);

        bool canMove = _stateMachine == null || _stateMachine.CanMove();
        return canMove ? input.MoveInput : Vector2.zero;
    }

    /// <summary>
    /// 着地判定。isGrounded かつ落下中ならジャンプ終了
    /// サーバー権威でステート遷移、クライアントは予測フラグのみ更新
    /// </summary>
    private void CheckLanding(bool isServerAuthority)
    {
        if (!_isJumping) return;
        if (!_controller.isGrounded || _verticalVelocity > 0f) return;

        _isJumping = false;
        _jumpLaunchDir = Vector3.zero;

        if (isServerAuthority)
            _stateMachine.TryChangeState(CharacterState.Idle);
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
        // ジャンプ発動フレームは isGrounded=true だが JUMP_FORCE を上書きしてはいけない
        if (_controller.isGrounded && !_isJumping)
        {
            _verticalVelocity = GameConfig.GROUND_STICK_FORCE;
        }
        else if (!_controller.isGrounded)
        {
            _verticalVelocity += GameConfig.JUMP_GRAVITY * GameConfig.FIXED_DELTA_TIME;
        }

        // 移動（固定デルタタイム使用で決定論的）
        Vector3 velocity = inputDir * GameConfig.MOVE_SPEED;
        velocity.y = _verticalVelocity;
        _controller.Move(velocity * GameConfig.FIXED_DELTA_TIME);

        // 回転（入力方向に向かって滑らかに回転）
        // ジャンプ中は方向転換不可（離陸時の向きを維持）
        if (!_isJumping && inputDir.sqrMagnitude > 0.01f)
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
    /// クライアント → サーバーへの統合入力送信
    /// サーバーは権威として移動計算・ステート遷移を行い、確定状態をクライアントに返す
    /// </summary>
    [ServerRpc]
    private void SubmitInputServerRpc(PlayerInput input)
    {
        // ジャンプ処理（サーバー権威）
        ProcessJump(input, true);

        // 移動入力の決定（ジャンプ中は離陸方向を使用）
        Vector2 move = GetEffectiveMove(input);

        // Idle ↔ Move ステート遷移（ジャンプ中は不要）
        if (!_isJumping) UpdateMoveState(move.x, move.y);

        // サーバー権威で移動計算
        ApplyMovement(move.x, move.y);

        // 着地判定
        CheckLanding(true);

        // 他プレイヤー表示用に NetworkVariable を更新
        _netPosition.Value = transform.position;
        _netRotationY.Value = transform.eulerAngles.y;

        // オーナーに確定状態を返送（リコンシリエーション用）
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

        // ジャンプ状態を復元（確定ティックのバッファから取得）
        _isJumping = _stateBuffer[idx].IsJumping;
        _jumpLaunchDir = _stateBuffer[idx].JumpLaunchDir;

        // --- 再シミュレーション（Replay）---
        // 確定ティック以降の保存済み入力を順番に再適用
        // ジャンプ開始・着地も含めて完全にリプレイする
        uint replayTick = tick + 1;
        while (replayTick < _currentTick)
        {
            int replayIdx = (int)(replayTick % GameConfig.PREDICTION_BUFFER_SIZE);

            // バッファの整合性チェック
            if (_inputBuffer[replayIdx].Tick != replayTick) break;

            PlayerInput replayInput = _inputBuffer[replayIdx];
            ProcessJump(replayInput, false);
            Vector2 move = GetEffectiveMove(replayInput);
            ApplyMovement(move.x, move.y);
            CheckLanding(false);

            // リプレイ結果でバッファを更新（次回のリコンシリエーションに備える）
            _stateBuffer[replayIdx] = new MoveState
            {
                Position = transform.position,
                RotationY = transform.eulerAngles.y,
                VerticalVelocity = _verticalVelocity,
                IsJumping = _isJumping,
                JumpLaunchDir = _jumpLaunchDir
            };

            replayTick++;
        }
    }
}
