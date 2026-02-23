using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 通常攻撃コンボ + チャージ攻撃システム（サーバー権威）
///
/// 通常攻撃（N コンボ）:
/// 1. □（左クリック）→ TryStartAttack() で N1 開始
/// 2. Attack ステート中に□ → コンボウィンドウ内なら次段に進む
/// 3. 入力がなくモーション終了 → コンボ終了 → Idle
///
/// チャージ攻撃（C 派生）:
/// 1. △（右クリック）→ TryStartCharge() で C 技に派生
/// 2. Idle/Move → C1、N1中 → C2、N2中 → C3 ...
/// 3. C3 は△連打でラッシュ追加ヒット
/// 4. チャージ終了後は必ず Idle に戻る
///
/// ダッシュ攻撃（D）:
/// 1. 1.5秒以上移動（ダッシュ状態）で□ → TryStartDashAttack() で D 開始
/// 2. D 中に□連打でダッシュラッシュ（追加ヒット）
/// 3. D/ラッシュ終了後は必ず Idle に戻る
///
/// コンボウィンドウ: 各攻撃モーションの最後 30% の区間
/// 先行入力: ウィンドウ前に押された攻撃入力をバッファし、ウィンドウ到達時に自動消費（150ms）
/// </summary>
[RequireComponent(typeof(CharacterStateMachine))]
public class ComboSystem : NetworkBehaviour
{
    // ============================================================
    // 同期変数
    // ============================================================

    /// <summary>
    /// 現在のコンボ段数。全クライアントに同期（UI・他プレイヤー表示用）
    /// 0 = 非攻撃、1 = N1, 2 = N2 ...
    /// </summary>
    private readonly NetworkVariable<byte> _networkComboStep = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>現在のコンボ段数（読み取り専用）</summary>
    public int ComboStep => _networkComboStep.Value;

    // ============================================================
    // サーバー側管理データ — 通常攻撃
    // ============================================================

    private CharacterStateMachine _stateMachine;
    private int _comboStep;             // 現在のコンボ段数（0 = 非攻撃）
    private float _attackTimer;         // 現在の攻撃モーションの残り時間
    private bool _comboWindowOpen;      // コンボ受付ウィンドウが開いているか
    private bool _hasBufferedAttack;    // 先行入力バッファに攻撃入力があるか
    private float _inputBufferTimer;    // 先行入力の残り有効時間（INPUT_BUFFER_SEC で初期化、0 で無効）
    private int _maxComboStep = GameConfig.MAX_COMBO_STEP_BASE;

    // ============================================================
    // サーバー側管理データ — チャージ攻撃
    // ============================================================

    private int _chargeType;       // 現在のチャージ技番号（0 = 非チャージ、1 = C1 ...）
    private float _chargeTimer;    // チャージ攻撃モーションの残り時間
    private int _rushHitCount;     // C3 ラッシュの追加ヒット数

    // ============================================================
    // サーバー側管理データ — ダッシュ攻撃
    // ============================================================

    private bool _isDashAttacking;      // ダッシュ攻撃中フラグ
    private float _dashAttackTimer;     // ダッシュ攻撃モーションの残り時間
    private int _dashRushHitCount;      // ダッシュラッシュの追加ヒット数

    /// <summary>ダッシュ攻撃中か（PlayerMovement からの参照用）</summary>
    public bool IsDashAttacking => _isDashAttacking;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
    }

    /// <summary>
    /// サーバーのみ: 毎 FixedUpdate でコンボ・チャージ・ダッシュ攻撃タイマーを更新
    /// PlayerMovement とは独立してタイマーを進める（60Hz で安定動作）
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;
        UpdateCombo();
        UpdateCharge();
        UpdateDashAttack();
    }

    // ============================================================
    // 通常攻撃入力処理（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 通常攻撃入力を処理する。サーバー権威
    /// - Idle/Move → N1 開始
    /// - Attack + コンボウィンドウ → 次の段に進む
    /// - Attack + ウィンドウ前 → 先行入力バッファに保存
    /// </summary>
    public void TryStartAttack()
    {
        if (!IsServer) return;

        CharacterState current = _stateMachine.CurrentState;

        if (_comboStep == 0)
        {
            // 非攻撃状態からの開始: CanAcceptInput で Idle/Move 等を判定
            if (!_stateMachine.CanAcceptInput(InputType.NormalAttack)) return;

            StartComboStep(1);
            _stateMachine.TryChangeState(CharacterState.Attack);
        }
        else if (current == CharacterState.Attack)
        {
            if (_comboWindowOpen && _comboStep < _maxComboStep)
            {
                // コンボウィンドウ内: 次の段に進む
                StartComboStep(_comboStep + 1);
            }
            else if (!_comboWindowOpen && _comboStep < _maxComboStep)
            {
                // ウィンドウ前: 先行入力バッファに保存（150ms 有効）
                _hasBufferedAttack = true;
                _inputBufferTimer = GameConfig.INPUT_BUFFER_SEC;
                Debug.Log($"[Combo] {gameObject.name}: 先行入力バッファリング");
            }
            // _comboStep >= _maxComboStep: 最大段数なので受け付けない
        }
    }

    // ============================================================
    // チャージ攻撃入力処理（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// チャージ攻撃入力を処理する。サーバー権威
    /// - Idle/Move（_comboStep==0）→ C1
    /// - Attack（N1中）→ C2、（N2中）→ C3 ...
    /// - Charge（C3中）→ ラッシュ追加ヒット
    /// </summary>
    /// <param name="moveInput">チャージ開始時の向き設定に使う移動入力</param>
    public void TryStartCharge(Vector2 moveInput)
    {
        if (!IsServer) return;

        CharacterState current = _stateMachine.CurrentState;

        // C3 ラッシュ継続: Charge ステート中に△で追加ヒット
        if (current == CharacterState.Charge && _chargeType == 3)
        {
            if (_rushHitCount < GameConfig.C3_RUSH_MAX_HITS)
            {
                _rushHitCount++;
                _chargeTimer = GameConfig.C3_RUSH_DURATION;
                Debug.Log($"[Combo] {gameObject.name}: C3 ラッシュ {_rushHitCount}hit");
            }
            return;
        }

        // 新しいチャージ攻撃の開始: CanAcceptInput で入力受付を判定
        if (!_stateMachine.CanAcceptInput(InputType.ChargeAttack)) return;

        // チャージタイプ決定: _comboStep に応じて C1〜C5
        // _comboStep == 0 → C1（Idle/Move から直接）
        // _comboStep == 1 → C2（N1 から派生）
        // _comboStep == 2 → C3（N2 から派生）
        // _comboStep == 3 → C4（N3 から派生）
        // _comboStep == 4 → C5（N4 から派生）
        int chargeType = (_comboStep == 0) ? 1 : _comboStep + 1;

        StartCharge(chargeType, moveInput);
    }

    // ============================================================
    // 通常攻撃更新（★サーバー側 FixedUpdate★）
    // ============================================================

    /// <summary>
    /// 通常攻撃コンボタイマーを毎 FixedUpdate で更新する
    /// - タイマー減算
    /// - コンボウィンドウの開放判定
    /// - 先行入力バッファの消費・タイムアウト
    /// - モーション終了時のコンボ終了処理
    /// </summary>
    private void UpdateCombo()
    {
        if (_comboStep == 0) return;

        // 外部要因で Attack から離脱した場合（被弾・チャージ派生等）→ コンボリセット
        if (_stateMachine.CurrentState != CharacterState.Attack)
        {
            ResetCombo();
            return;
        }

        _attackTimer -= GameConfig.FIXED_DELTA_TIME;

        // 先行入力バッファのタイムアウト処理（150ms 経過で破棄）
        if (_hasBufferedAttack)
        {
            _inputBufferTimer -= GameConfig.FIXED_DELTA_TIME;
            if (_inputBufferTimer <= 0f)
            {
                _hasBufferedAttack = false;
                _inputBufferTimer = 0f;
            }
        }

        // コンボウィンドウ判定: モーション残りが持続時間の COMBO_WINDOW_RATIO 以下で開放
        if (!_comboWindowOpen)
        {
            float duration = GetAttackDuration(_comboStep);
            float windowTime = duration * GameConfig.COMBO_WINDOW_RATIO;
            if (_attackTimer <= windowTime)
            {
                _comboWindowOpen = true;

                // 先行入力バッファを消費
                if (_hasBufferedAttack && _comboStep < _maxComboStep)
                {
                    int nextStep = _comboStep + 1;
                    Debug.Log($"[Combo] {gameObject.name}: バッファ消費 → N{nextStep}");
                    StartComboStep(nextStep);
                    return;
                }
            }
        }

        // モーション終了: コンボ受付ウィンドウ中に入力がなかった → コンボ終了
        if (_attackTimer <= 0f)
        {
            Debug.Log($"[Combo] {gameObject.name}: コンボ終了");
            ResetCombo();
            _stateMachine.TryChangeState(CharacterState.Idle);
        }
    }

    // ============================================================
    // チャージ攻撃更新（★サーバー側 FixedUpdate★）
    // ============================================================

    /// <summary>
    /// チャージ攻撃タイマーを毎 FixedUpdate で更新する
    /// タイマー満了で Idle に遷移。C3 ラッシュ中は△入力がないと終了
    /// </summary>
    private void UpdateCharge()
    {
        if (_chargeType == 0) return;

        // 外部要因で Charge から離脱した場合（被弾等）→ チャージリセット
        if (_stateMachine.CurrentState != CharacterState.Charge)
        {
            ResetCharge();
            return;
        }

        _chargeTimer -= GameConfig.FIXED_DELTA_TIME;

        if (_chargeTimer <= 0f)
        {
            Debug.Log($"[Combo] {gameObject.name}: C{_chargeType} 終了");
            ResetCharge();
            _stateMachine.TryChangeState(CharacterState.Idle);
        }
    }

    // ============================================================
    // ダッシュ攻撃入力処理（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// ダッシュ攻撃入力を処理する。サーバー権威
    /// - ダッシュ状態で□ → D 開始
    /// - DashAttack 中に□ → ダッシュラッシュ追加ヒット
    /// </summary>
    public void TryStartDashAttack()
    {
        if (!IsServer) return;

        CharacterState current = _stateMachine.CurrentState;

        // ダッシュラッシュ継続: DashAttack 中に□で追加ヒット
        if (_isDashAttacking && current == CharacterState.DashAttack)
        {
            if (_dashRushHitCount < GameConfig.DASH_RUSH_MAX_HITS)
            {
                _dashRushHitCount++;
                _dashAttackTimer = GameConfig.DASH_RUSH_DURATION;
                Debug.Log($"[Combo] {gameObject.name}: Dラッシュ {_dashRushHitCount}hit");
            }
            return;
        }

        // 新規ダッシュ攻撃開始
        if (!_stateMachine.CanAcceptInput(InputType.NormalAttack)) return;

        // N コンボをリセット（ダッシュ攻撃は N コンボとは別系統）
        ResetCombo();

        _isDashAttacking = true;
        _dashAttackTimer = GameConfig.DASH_ATTACK_DURATION;
        _dashRushHitCount = 0;

        _stateMachine.TryChangeState(CharacterState.DashAttack);
        Debug.Log($"[Combo] {gameObject.name}: D（ダッシュ攻撃）開始");
    }

    // ============================================================
    // ダッシュ攻撃更新（★サーバー側 FixedUpdate★）
    // ============================================================

    /// <summary>
    /// ダッシュ攻撃タイマーを毎 FixedUpdate で更新する
    /// タイマー満了で Idle に遷移
    /// </summary>
    private void UpdateDashAttack()
    {
        if (!_isDashAttacking) return;

        // 外部要因で DashAttack から離脱した場合（被弾等）→ リセット
        if (_stateMachine.CurrentState != CharacterState.DashAttack)
        {
            ResetDashAttack();
            return;
        }

        _dashAttackTimer -= GameConfig.FIXED_DELTA_TIME;

        if (_dashAttackTimer <= 0f)
        {
            Debug.Log($"[Combo] {gameObject.name}: D 終了");
            ResetDashAttack();
            _stateMachine.TryChangeState(CharacterState.Idle);
        }
    }

    // ============================================================
    // 内部ヘルパー — 通常攻撃
    // ============================================================

    /// <summary>
    /// 新しいコンボ段を開始する
    /// </summary>
    private void StartComboStep(int step)
    {
        _comboStep = step;
        _attackTimer = GetAttackDuration(step);
        _comboWindowOpen = false;
        _hasBufferedAttack = false;
        _inputBufferTimer = 0f;
        _networkComboStep.Value = (byte)step;
        Debug.Log($"[Combo] {gameObject.name}: N{step} 開始");
    }

    /// <summary>
    /// 通常攻撃コンボ状態をリセットする
    /// </summary>
    private void ResetCombo()
    {
        _comboStep = 0;
        _attackTimer = 0f;
        _comboWindowOpen = false;
        _hasBufferedAttack = false;
        _inputBufferTimer = 0f;
        _networkComboStep.Value = 0;
    }

    // ============================================================
    // 内部ヘルパー — チャージ攻撃
    // ============================================================

    /// <summary>
    /// チャージ攻撃を開始する。N コンボをリセットし Charge ステートに遷移
    /// </summary>
    /// <param name="chargeType">チャージ技番号（1=C1, 2=C2 ...）</param>
    /// <param name="moveInput">開始時の向き設定用移動入力</param>
    private void StartCharge(int chargeType, Vector2 moveInput)
    {
        // N コンボをリセット（チャージ派生で N コンボは終了）
        ResetCombo();

        _chargeType = chargeType;
        _chargeTimer = GetChargeDuration(chargeType);
        _rushHitCount = 0;

        // ステートを Charge に遷移
        _stateMachine.TryChangeState(CharacterState.Charge);

        // チャージ開始時のスティック方向で向きを設定（その後は固定）
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        Debug.Log($"[Combo] {gameObject.name}: C{chargeType} 開始");
    }

    /// <summary>
    /// チャージ攻撃状態をリセットする
    /// </summary>
    private void ResetCharge()
    {
        _chargeType = 0;
        _chargeTimer = 0f;
        _rushHitCount = 0;
    }

    // ============================================================
    // 内部ヘルパー — ダッシュ攻撃
    // ============================================================

    /// <summary>
    /// ダッシュ攻撃状態をリセットする
    /// </summary>
    private void ResetDashAttack()
    {
        _isDashAttacking = false;
        _dashAttackTimer = 0f;
        _dashRushHitCount = 0;
    }

    // ============================================================
    // 持続時間テーブル
    // ============================================================

    /// <summary>
    /// コンボ段数に応じた通常攻撃持続時間を返す
    /// </summary>
    public static float GetAttackDuration(int step)
    {
        return step switch
        {
            1 => GameConfig.N1_DURATION,
            2 => GameConfig.N2_DURATION,
            3 => GameConfig.N3_DURATION,
            4 => GameConfig.N4_DURATION,
            _ => 0.5f, // 安全策: 未定義段はデフォルト値
        };
    }

    /// <summary>
    /// チャージ技番号に応じた持続時間を返す
    /// </summary>
    public static float GetChargeDuration(int chargeType)
    {
        return chargeType switch
        {
            1 => GameConfig.C1_DURATION,
            2 => GameConfig.C2_DURATION,
            3 => GameConfig.C3_DURATION,
            4 => GameConfig.C4_DURATION,
            5 => GameConfig.C5_DURATION,
            6 => GameConfig.C6_DURATION,
            _ => 0.7f, // 安全策: 未定義はデフォルト値
        };
    }
}
