using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 通常攻撃コンボシステム（サーバー権威）
///
/// コンボの流れ:
/// 1. □ボタン（左クリック）→ TryStartAttack() で N1 開始
/// 2. Attack ステート中に□ → コンボウィンドウ内なら次段に進む
/// 3. 入力がなくモーション終了 → コンボ終了 → Idle
///
/// コンボウィンドウ: 各攻撃モーションの最後 30% の区間
/// 先行入力: ウィンドウ前に押された攻撃入力をバッファし、ウィンドウ到達時に自動消費
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
    // サーバー側管理データ
    // ============================================================

    private CharacterStateMachine _stateMachine;
    private int _comboStep;          // 現在のコンボ段数（0 = 非攻撃）
    private float _attackTimer;      // 現在の攻撃モーションの残り時間
    private bool _comboWindowOpen;   // コンボ受付ウィンドウが開いているか
    private bool _hasBufferedAttack;    // 先行入力バッファに攻撃入力があるか
    private float _inputBufferTimer;    // 先行入力の残り有効時間（INPUT_BUFFER_SEC で初期化、0 で無効）
    private int _maxComboStep = GameConfig.MAX_COMBO_STEP_BASE;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
    }

    /// <summary>
    /// サーバーのみ: 毎 FixedUpdate でコンボタイマーを更新
    /// PlayerMovement とは独立してタイマーを進める（60Hz で安定動作）
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;
        UpdateCombo();
    }

    // ============================================================
    // 攻撃入力処理（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 攻撃入力を処理する。サーバー権威
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
    // コンボ更新（★サーバー側 FixedUpdate★）
    // ============================================================

    /// <summary>
    /// コンボタイマーを毎 FixedUpdate で更新する
    /// - タイマー減算
    /// - コンボウィンドウの開放判定
    /// - 先行入力バッファの消費
    /// - モーション終了時のコンボ終了処理
    /// </summary>
    private void UpdateCombo()
    {
        if (_comboStep == 0) return;

        // 外部要因で Attack から離脱した場合（被弾等）→ コンボリセット
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
    // 内部ヘルパー
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
    /// コンボ状態を完全にリセットする
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

    /// <summary>
    /// コンボ段数に応じた攻撃持続時間を返す
    /// 将来的に武器種ごとの持続時間テーブルに拡張可能
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
}
