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
// 実行順序: PlayerMovement(-10) → CharacterStateMachine(0) → ComboSystem(10)
// 入力処理（PlayerMovement）がタイマー更新（ComboSystem）より先に走ることを保証
// これによりコンボタイマー満了とチャージ入力が同一フレームに重なった場合のレースコンディションを防ぐ
[DefaultExecutionOrder(-10)]
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
        public bool IsGuarding;         // ガード予測用
        public float GuardRotationY;    // ガード方向固定用
        public float MoveTime;          // ダッシュ判定用
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

    /// <summary>
    /// プレイヤーの武器種（サーバー権威、全クライアント参照用）
    /// 現時点では全プレイヤーが大剣。武器選択UIはM4後半で追加
    /// </summary>
    private readonly NetworkVariable<WeaponType> _netWeaponType = new(
        WeaponType.GreatSword,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>現在の武器種（読み取り専用）</summary>
    public WeaponType CurrentWeaponType => _netWeaponType.Value;

    // ============================================================
    // ローカル状態
    // ============================================================

    private CharacterController _controller;
    private CharacterStateMachine _stateMachine;
    private ComboSystem _comboSystem;
    private EGSystem _egSystem;
    private MusouGauge _musouGauge;
    private CameraController _cameraController; // オーナーのみ使用（カメラ基準移動用）
    private float _verticalVelocity;

    // --- ジャンプ ---
    private Vector3 _jumpLaunchDir;  // 離陸時の水平方向（ジャンプ中維持、方向転換不可）
    private bool _isJumping;         // ジャンプ中フラグ（クライアント予測 + サーバー権威）

    // --- ダッシュ判定 ---
    private float _moveTime;        // 連続移動時間（サーバー管理、クライアント予測用）
    private bool _wasDashing;       // ダッシュログ1回出力用

    /// <summary>
    /// ダッシュ状態か。連続移動時間が閾値を超えたら true
    /// M2-4b でダッシュ攻撃発動の条件として使う
    /// </summary>
    public bool IsDashing => _moveTime >= GameConfig.DASH_ATTACK_MOVE_TIME;

    // --- ガード ---
    private bool _isGuarding;       // ガード中フラグ（クライアント予測 + サーバー権威）
    private float _guardRotationY;  // ガード開始時の向き（ガード中は固定）

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
    private bool _attackPressed;    // 攻撃（押した瞬間のみ、消費後リセット）
    private bool _chargePressed;   // チャージ攻撃（押した瞬間のみ、消費後リセット）
    private bool _chargeHeld;      // チャージ長押し（EG準備用）
    private bool _musouPressed;    // 無双（押した瞬間のみ、消費後リセット）
    private bool _musouHeld;       // 無双長押し（MusouCharge用）

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
        _comboSystem = GetComponent<ComboSystem>();
        _egSystem = GetComponent<EGSystem>();
        _musouGauge = GetComponent<MusouGauge>();
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

            // 3人称カメラを生成してアタッチ（オーナー専用）
            SetupCamera();
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

        // オーナー: カメラ破棄
        if (IsOwner && _cameraController != null)
        {
            Destroy(_cameraController.gameObject);
            _cameraController = null;
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

        // 攻撃は押した瞬間のみ true（FixedUpdate で消費されるまで保持）
        if (Input.GetMouseButtonDown(0))
            _attackPressed = true;

        // チャージ攻撃（右クリック）も押した瞬間のみ true
        if (Input.GetMouseButtonDown(1))
            _chargePressed = true;

        // チャージ長押し（EG準備用）
        _chargeHeld = Input.GetMouseButton(1);

        // 無双（Q or 中クリック）: 押した瞬間
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(2))
            _musouPressed = true;

        // 無双長押し（MusouCharge 用）
        _musouHeld = Input.GetKey(KeyCode.Q) || Input.GetMouseButton(2);
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
        // WASD 入力をカメラの forward/right 基準のワールド方向に変換
        // サーバーはこのワールド方向をそのまま ApplyMovement で使う
        Vector2 moveInput = ConvertToCameraRelative(_inputH, _inputV);

        // 全入力を生のまま構造体に格納（フィルタリングはサーバーが行う）
        PlayerInput input = new PlayerInput
        {
            MoveInput = moveInput,
            JumpPressed = _jumpPressed,
            GuardHeld = _guardHeld,
            AttackPressed = _attackPressed,
            ChargePressed = _chargePressed,
            ChargeHeld = _chargeHeld,
            MusouPressed = _musouPressed,
            MusouHeld = _musouHeld,
            Tick = _currentTick
        };

        // 瞬間入力は消費後リセット（1ティックのみ有効）
        _jumpPressed = false;
        _attackPressed = false;
        _chargePressed = false;
        _musouPressed = false;

        if (IsServer)
        {
            // --- ホスト（サーバー兼オーナー）---
            ProcessGuard(input, true);
            if (_egSystem != null) _egSystem.ProcessEG(input.ChargeHeld, input.GuardHeld);
            ProcessJump(input, true);
            ProcessDashTracking(input);
            if (input.AttackPressed && _comboSystem != null)
            {
                // ダッシュ状態 or ダッシュ攻撃中 → ダッシュ攻撃/ラッシュ優先
                if (IsDashing || _comboSystem.IsDashAttacking)
                {
                    _comboSystem.TryStartDashAttack();
                    _moveTime = 0f;
                    _wasDashing = false;
                }
                else
                {
                    _comboSystem.TryStartAttack();
                }
            }
            if (input.ChargePressed && _comboSystem != null)
                _comboSystem.TryStartCharge(input.MoveInput);
            // 無双入力処理
            if (_musouGauge != null)
            {
                if (input.MusouPressed) _musouGauge.TryActivateMusou();
                _musouGauge.ProcessMusouCharge(input.MusouHeld);
            }
            Vector2 move = GetEffectiveMove(input);
            float speedMul = GetSpeedMultiplier();
            if (!_isJumping && !_isGuarding) UpdateMoveState(move.x, move.y);
            ApplyMovement(move.x, move.y, speedMul);
            ApplyAttackRotation(input);
            CheckLanding(true);
            _netPosition.Value = transform.position;
            _netRotationY.Value = transform.eulerAngles.y;
        }
        else
        {
            // --- リモートクライアント ---
            // サーバーに生入力を送信
            SubmitInputServerRpc(input);

            // クライアント予測: ローカルで即座にガード・ジャンプ・ダッシュ判定を実行
            ProcessGuard(input, false);
            ProcessJump(input, false);
            ProcessDashTracking(input);
            Vector2 move = GetEffectiveMove(input);
            float speedMul = GetSpeedMultiplier();
            ApplyMovement(move.x, move.y, speedMul);
            CheckLanding(false);

            int idx = (int)(_currentTick % GameConfig.PREDICTION_BUFFER_SIZE);
            _inputBuffer[idx] = input;
            _stateBuffer[idx] = new MoveState
            {
                Position = transform.position,
                RotationY = transform.eulerAngles.y,
                VerticalVelocity = _verticalVelocity,
                IsJumping = _isJumping,
                JumpLaunchDir = _jumpLaunchDir,
                IsGuarding = _isGuarding,
                GuardRotationY = _guardRotationY,
                MoveTime = _moveTime
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
    // 攻撃中の向き調整
    // ============================================================

    /// <summary>
    /// Attack ステート中に移動入力がある場合、位置は動かさず向き（Y回転）だけ変更する
    /// サーバー側で実行し、回転は NetworkVariable 経由でクライアントに同期される
    /// </summary>
    private void ApplyAttackRotation(PlayerInput input)
    {
        if (_stateMachine == null || _stateMachine.CurrentState != CharacterState.Attack) return;
        if (input.MoveInput.sqrMagnitude <= 0.01f) return;

        Vector3 dir = new Vector3(input.MoveInput.x, 0f, input.MoveInput.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float currentY = transform.eulerAngles.y;
        float newY = Mathf.MoveTowardsAngle(
            currentY, targetAngle,
            GameConfig.ROTATION_SPEED * GameConfig.FIXED_DELTA_TIME
        );
        transform.rotation = Quaternion.Euler(0f, newY, 0f);
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
        // ガード中・鈍足中はジャンプ不可（CanAcceptInput でも弾かれるが、予測精度のため早期リターン）
        if (!input.JumpPressed || _isJumping || _isGuarding) return;

        // 鈍足（Slow）中はジャンプ不可
        if (_stateMachine != null && _stateMachine.HasStatusEffect(StatusEffect.Slow)) return;

        bool canJump = _stateMachine != null
            && _stateMachine.CanAcceptInput(InputType.Jump);
        if (!canJump) return;

        _isJumping = true;
        // 武器種ごとのジャンプ力（JumpHeight と重力から初速を導出）
        float jumpHeight = WeaponData.GetWeaponParams(_netWeaponType.Value).JumpHeight;
        _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(GameConfig.JUMP_GRAVITY) * jumpHeight);

        // 離陸時の水平方向を保存（ジャンプ中は方向転換不可）
        _jumpLaunchDir = new Vector3(input.MoveInput.x, 0f, input.MoveInput.y);
        if (_jumpLaunchDir.sqrMagnitude > 1f)
            _jumpLaunchDir.Normalize();

        if (isServerAuthority)
            _stateMachine.TryChangeState(CharacterState.Jump);
    }

    /// <summary>
    /// 有効な移動入力を返す
    /// ジャンプ中は離陸時方向、ガード中は生入力（速度倍率は ApplyMovement で適用）、
    /// それ以外はステートに応じた入力フィルタリング
    /// </summary>
    private Vector2 GetEffectiveMove(PlayerInput input)
    {
        if (_isJumping)
            return new Vector2(_jumpLaunchDir.x, _jumpLaunchDir.z);

        // ガード中は移動入力をそのまま返す（Guard → GuardMove 遷移はサーバーが管理）
        if (_isGuarding)
            return input.MoveInput;

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
    // カメラ
    // ============================================================

    /// <summary>
    /// 3人称カメラを生成し、CameraController を初期化する（オーナー専用）
    /// MainCamera が既にあればそれを流用し、なければ新規作成する
    /// </summary>
    private void SetupCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // 既存 MainCamera に CameraController を追加（重複防止チェック）
            _cameraController = mainCam.gameObject.GetComponent<CameraController>();
            if (_cameraController == null)
                _cameraController = mainCam.gameObject.AddComponent<CameraController>();
        }
        else
        {
            // MainCamera がない場合は新規作成
            var camObj = new GameObject("PlayerCamera");
            camObj.tag = "MainCamera";
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            _cameraController = camObj.AddComponent<CameraController>();
        }
        _cameraController.Initialize(transform);
    }

    /// <summary>
    /// WASD 生入力をカメラの forward/right 基準のワールド方向に変換する
    /// カメラがない場合（サーバー等）は生入力をそのまま返す
    ///
    /// 変換後の Vector2 は (worldX, worldZ) であり、
    /// ApplyMovement で new Vector3(x, 0, z) としてそのまま使われる
    /// </summary>
    private Vector2 ConvertToCameraRelative(float rawH, float rawV)
    {
        // カメラがない場合（ホストサーバー等の特殊ケース）は生入力をそのまま使用
        if (_cameraController == null)
            return new Vector2(rawH, rawV);

        // 入力がない場合は早期リターン
        if (rawH * rawH + rawV * rawV < 0.001f)
            return Vector2.zero;

        // カメラの Yaw からワールド方向の forward/right を計算（Y成分を除去）
        float yawRad = _cameraController.Yaw * Mathf.Deg2Rad;
        // forward = (sin(yaw), 0, cos(yaw))
        Vector3 camForward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
        // right = (cos(yaw), 0, -sin(yaw))
        Vector3 camRight = new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));

        // カメラ基準のワールド方向を計算
        Vector3 worldDir = camForward * rawV + camRight * rawH;

        // 正規化（斜め入力で速度が√2倍にならないように）
        if (worldDir.sqrMagnitude > 1f)
            worldDir.Normalize();

        return new Vector2(worldDir.x, worldDir.z);
    }

    // ============================================================
    // ガード処理
    // ============================================================

    /// <summary>
    /// ガード状態の開始・維持・解除を処理する
    /// サーバー権威でステート遷移、クライアントは予測フラグのみ更新
    /// ガード中は向き固定（_guardRotationY を保持）
    /// </summary>
    private void ProcessGuard(PlayerInput input, bool isServerAuthority)
    {
        bool hasMove = input.MoveInput.sqrMagnitude > 0.01f;

        if (input.GuardHeld && !_isJumping)
        {
            if (!_isGuarding)
            {
                // ガード開始: Idle/Move からのみ受付
                bool canGuard = _stateMachine == null
                    || _stateMachine.CanAcceptInput(InputType.Guard);
                if (!canGuard) return;

                _isGuarding = true;
                _guardRotationY = transform.eulerAngles.y;

                if (isServerAuthority)
                    _stateMachine.TryChangeState(CharacterState.Guard);
            }
            else if (isServerAuthority)
            {
                // ガード中: Guard ↔ GuardMove 切替
                CharacterState current = _stateMachine.CurrentState;
                if (hasMove && current == CharacterState.Guard)
                    _stateMachine.TryChangeState(CharacterState.GuardMove);
                else if (!hasMove && current == CharacterState.GuardMove)
                    _stateMachine.TryChangeState(CharacterState.Guard);
            }
        }
        else if (_isGuarding)
        {
            // ガード解除
            _isGuarding = false;

            if (isServerAuthority)
            {
                CharacterState current = _stateMachine.CurrentState;
                if (current == CharacterState.Guard || current == CharacterState.GuardMove)
                    _stateMachine.TryChangeState(CharacterState.Idle);
            }
        }
    }

    // ============================================================
    // 速度倍率計算
    // ============================================================

    /// <summary>
    /// 現在の速度倍率を計算する
    /// ガード移動・鈍足の倍率を乗算で適用する
    /// </summary>
    private float GetSpeedMultiplier()
    {
        float mul = 1f;
        if (_isGuarding)
            mul *= GameConfig.GUARD_MOVE_SPEED_MULTIPLIER;
        if (_stateMachine != null && _stateMachine.HasStatusEffect(StatusEffect.Slow))
            mul *= GameConfig.SLOW_SPEED_MULT;
        return mul;
    }

    // ============================================================
    // ダッシュ判定
    // ============================================================

    /// <summary>
    /// 連続移動時間をトラッキングする
    /// DASH_ATTACK_MOVE_TIME 以上移動し続けるとダッシュ状態（IsDashing == true）
    /// ダッシュ攻撃の発動自体は M2-4b で実装。ここではトラッキングのみ
    /// </summary>
    private void ProcessDashTracking(PlayerInput input)
    {
        bool hasMove = input.MoveInput.sqrMagnitude > 0.01f;

        if (hasMove && !_isGuarding && !_isJumping)
        {
            _moveTime += GameConfig.FIXED_DELTA_TIME;

            if (IsDashing && !_wasDashing)
            {
                Debug.Log($"[Dash] {gameObject.name}: ダッシュ状態");
                _wasDashing = true;
            }
        }
        else
        {
            _moveTime = 0f;
            _wasDashing = false;
        }
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
    /// <param name="speedMultiplier">速度倍率。ガード移動時は 0.5</param>
    private void ApplyMovement(float horizontal, float vertical, float speedMultiplier = 1f)
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

        // ガード中はキャラクターのローカル座標基準で移動方向を変換
        // W = キャラ正面、S = 背面、A = 左、D = 右
        // TODO: ガード開始時にカメラをキャラクター正面にスナップ（カメラシステム実装後）
        if (_isGuarding)
        {
            Quaternion guardRot = Quaternion.Euler(0f, _guardRotationY, 0f);
            inputDir = guardRot * inputDir;
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

        // 移動（武器種ごとの速度を使用、固定デルタタイム使用で決定論的）
        float moveSpeed = WeaponData.GetWeaponParams(_netWeaponType.Value).MoveSpeed;
        Vector3 velocity = inputDir * (moveSpeed * speedMultiplier);
        velocity.y = _verticalVelocity;
        _controller.Move(velocity * GameConfig.FIXED_DELTA_TIME);

        // 回転
        if (_isGuarding)
        {
            // ガード中は向き固定（ガード開始時の Y 回転を維持）
            transform.rotation = Quaternion.Euler(0f, _guardRotationY, 0f);
        }
        else if (!_isJumping && inputDir.sqrMagnitude > 0.01f)
        {
            // 通常: 入力方向に向かって滑らかに回転
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
        // ガード処理（サーバー権威）
        ProcessGuard(input, true);

        // EG 処理（サーバー権威）
        if (_egSystem != null) _egSystem.ProcessEG(input.ChargeHeld, input.GuardHeld);

        // ジャンプ処理（サーバー権威）
        ProcessJump(input, true);

        // ダッシュ判定トラッキング
        ProcessDashTracking(input);

        // 攻撃入力 → コンボシステムに委譲（サーバー権威）
        if (input.AttackPressed && _comboSystem != null)
        {
            // ダッシュ状態 or ダッシュ攻撃中 → ダッシュ攻撃/ラッシュ優先
            if (IsDashing || _comboSystem.IsDashAttacking)
            {
                _comboSystem.TryStartDashAttack();
                _moveTime = 0f;
                _wasDashing = false;
            }
            else
            {
                _comboSystem.TryStartAttack();
            }
        }
        if (input.ChargePressed && _comboSystem != null)
            _comboSystem.TryStartCharge(input.MoveInput);

        // 無双入力処理（サーバー権威）
        if (_musouGauge != null)
        {
            if (input.MusouPressed) _musouGauge.TryActivateMusou();
            _musouGauge.ProcessMusouCharge(input.MusouHeld);
        }

        // 移動入力の決定（ジャンプ中は離陸方向、ガード中は生入力を使用）
        Vector2 move = GetEffectiveMove(input);
        float speedMul = _isGuarding ? GameConfig.GUARD_MOVE_SPEED_MULTIPLIER : 1f;

        // Idle ↔ Move ステート遷移（ジャンプ中・ガード中は不要）
        if (!_isJumping && !_isGuarding) UpdateMoveState(move.x, move.y);

        // サーバー権威で移動計算
        ApplyMovement(move.x, move.y, speedMul);

        // 攻撃中の向き調整（位置は動かさず向きだけ更新）
        ApplyAttackRotation(input);

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

        // ジャンプ・ガード・ダッシュ状態を復元（確定ティックのバッファから取得）
        _isJumping = _stateBuffer[idx].IsJumping;
        _jumpLaunchDir = _stateBuffer[idx].JumpLaunchDir;
        _isGuarding = _stateBuffer[idx].IsGuarding;
        _guardRotationY = _stateBuffer[idx].GuardRotationY;
        _moveTime = _stateBuffer[idx].MoveTime;
        _wasDashing = IsDashing;

        // --- 再シミュレーション（Replay）---
        // 確定ティック以降の保存済み入力を順番に再適用
        // ジャンプ・ガード・ダッシュ判定も含めて完全にリプレイする
        uint replayTick = tick + 1;
        while (replayTick < _currentTick)
        {
            int replayIdx = (int)(replayTick % GameConfig.PREDICTION_BUFFER_SIZE);

            // バッファの整合性チェック
            if (_inputBuffer[replayIdx].Tick != replayTick) break;

            PlayerInput replayInput = _inputBuffer[replayIdx];
            ProcessGuard(replayInput, false);
            ProcessJump(replayInput, false);
            ProcessDashTracking(replayInput);
            Vector2 move = GetEffectiveMove(replayInput);
            float speedMul = GetSpeedMultiplier();
            ApplyMovement(move.x, move.y, speedMul);
            CheckLanding(false);

            // リプレイ結果でバッファを更新（次回のリコンシリエーションに備える）
            _stateBuffer[replayIdx] = new MoveState
            {
                Position = transform.position,
                RotationY = transform.eulerAngles.y,
                VerticalVelocity = _verticalVelocity,
                IsJumping = _isJumping,
                JumpLaunchDir = _jumpLaunchDir,
                IsGuarding = _isGuarding,
                GuardRotationY = _guardRotationY,
                MoveTime = _moveTime
            };

            replayTick++;
        }
    }
}
