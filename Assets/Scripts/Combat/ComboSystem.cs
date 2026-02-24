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
// 実行順序: PlayerMovement(-10) → CharacterStateMachine(0) → ComboSystem(10)
// PlayerMovement が入力をディスパッチした後にタイマーを更新する
[DefaultExecutionOrder(10)]
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

    /// <summary>
    /// 連撃強化レベル（0〜3、サーバー権威）
    /// Lv0: N4まで（デフォルト）
    /// Lv1: N5まで解放
    /// Lv2: N6まで解放
    /// Lv3: エボリューション攻撃解放（M4-3bで実装）
    /// 仙箪強化で段階的に解放される。死亡時にリセット
    /// </summary>
    private readonly NetworkVariable<int> _comboEnhanceLevel = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>現在のコンボ段数（読み取り専用）</summary>
    public int ComboStep => _networkComboStep.Value;

    /// <summary>連撃強化レベル（0〜3。読み取り専用）</summary>
    public int ComboEnhanceLevel => _comboEnhanceLevel.Value;

    // ============================================================
    // サーバー側管理データ — 通常攻撃
    // ============================================================

    private CharacterStateMachine _stateMachine;
    private PlayerMovement _playerMovement;
    private int _comboStep;             // 現在のコンボ段数（0 = 非攻撃）
    private float _attackTimer;         // 現在の攻撃モーションの残り時間
    private bool _comboWindowOpen;      // コンボ受付ウィンドウが開いているか
    private bool _hasBufferedAttack;    // 先行入力バッファに攻撃入力があるか
    private float _inputBufferTimer;    // 先行入力の残り有効時間（INPUT_BUFFER_SEC で初期化、0 で無効）
    private int _maxComboStep = GameConfig.MAX_COMBO_STEP_BASE; // 連撃強化レベルに応じて動的に変化

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
    // HitboxSystem 向け公開プロパティ
    // ============================================================

    // 攻撃セグメント番号: 新しい攻撃（段・ラッシュヒット含む）ごとにインクリメント
    // HitboxSystem がこの値の変化を検知してヒット済みリストをリセットする
    private int _attackSequence;

    // 現在の攻撃セグメントの経過時間（秒）
    // HitboxSystem がアクティブフレーム判定に使用
    private float _segmentElapsed;

    /// <summary>攻撃セグメント番号（HitboxSystem 用）</summary>
    public int AttackSequence => _attackSequence;

    /// <summary>現在のチャージ技番号（HitboxSystem 用）</summary>
    public int ChargeType => _chargeType;

    /// <summary>現在の攻撃セグメント経過時間（HitboxSystem 用）</summary>
    public float SegmentElapsed => _segmentElapsed;

    /// <summary>ラッシュ中か（C3ラッシュ or ダッシュラッシュ）</summary>
    public bool IsRush => (_chargeType == 3 && _rushHitCount > 0)
                       || (_isDashAttacking && _dashRushHitCount > 0);

    // ============================================================
    // サーバー側管理データ — エボリューション攻撃
    // ============================================================

    private bool _isEvolution;  // エボリューション攻撃中フラグ（E6-E9）
    private MusouGauge _musouGauge;

    /// <summary>エボリューション攻撃中か（HitboxSystem 用。E はチャージ攻撃レベル）</summary>
    public bool IsEvolution => _isEvolution;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
        _playerMovement = GetComponent<PlayerMovement>();
        _musouGauge = GetComponent<MusouGauge>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 初期状態: 連撃強化なし
            _comboEnhanceLevel.Value = 0;
            _maxComboStep = GameConfig.MAX_COMBO_STEP_BASE;
        }

        // クライアント側: NetworkVariable の変更を監視して _maxComboStep を同期
        _comboEnhanceLevel.OnValueChanged += (oldVal, newVal) =>
        {
            _maxComboStep = GetMaxComboStep(newVal);
        };
    }

    /// <summary>現在の武器種を取得する</summary>
    private WeaponType GetWeaponType()
    {
        return _playerMovement != null ? _playerMovement.CurrentWeaponType : WeaponType.GreatSword;
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
        UpdateSegmentTimer();
    }

    // ============================================================
    // 通常攻撃入力処理（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 通常攻撃入力を処理する。サーバー権威
    /// - Idle/Move → N1 開始
    /// - Attack + コンボウィンドウ → 次の段に進む
    /// - Attack + ウィンドウ前 → 先行入力バッファに保存
    /// - N5 完了 + 連撃Lv3 + 無双MAX → E6 に移行（エボリューション）
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
            int effectiveMax = GetEffectiveMaxStep();

            if (_comboWindowOpen && _comboStep < effectiveMax)
            {
                int nextStep = _comboStep + 1;

                // N5 → E6 移行判定（エボリューション開始）
                if (nextStep == 6 && !_isEvolution && CanStartEvolution())
                {
                    _isEvolution = true;
                    Debug.Log($"[Combo] {gameObject.name}: エボリューション開始！");
                }

                StartComboStep(nextStep);
            }
            else if (!_comboWindowOpen && _comboStep < effectiveMax)
            {
                // ウィンドウ前: 先行入力バッファに保存（150ms 有効）
                _hasBufferedAttack = true;
                _inputBufferTimer = GameConfig.INPUT_BUFFER_SEC;
                Debug.Log($"[Combo] {gameObject.name}: 先行入力バッファリング");
            }
            // effectiveMax 以上: 最大段数なので受け付けない
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
                _attackSequence++;
                _segmentElapsed = 0f;
                Debug.Log($"[Combo] {gameObject.name}: C3 ラッシュ {_rushHitCount}hit");
            }
            return;
        }

        // 新しいチャージ攻撃の開始: CanAcceptInput で入力受付を判定
        if (!_stateMachine.CanAcceptInput(InputType.ChargeAttack)) return;

        // ガード系ステートでは△はEG用（チャージ攻撃にはならない）
        if (current == CharacterState.Guard || current == CharacterState.GuardMove
            || current == CharacterState.EGPrepare || current == CharacterState.EGReady)
            return;

        // チャージタイプ決定: _comboStep に応じて C1〜C6
        // バッファされた□入力がある場合、実効コンボ段数を+1して計算
        // これにより□□□△で必ず C4 が出る（バッファ消費タイミングに依存しない）
        // _comboStep == 0 → C1（Idle/Move から直接）
        // _comboStep == 1 → C2（N1 から派生）
        // _comboStep == 2 → C3（N2 から派生）
        // _comboStep == 3 → C4（N3 から派生）
        // 最終段（無強化N4 / 連撃1回N5 / 連撃2回N6）からはチャージ派生不可
        int effectiveStep = _comboStep;
        if (_hasBufferedAttack && effectiveStep < _maxComboStep)
        {
            effectiveStep++;
        }

        // 最終段からはチャージ派生不可
        // 例: 無強化時はN4が最終段→C5不可（C5にはN5解放=連撃強化1回が必要）
        if (effectiveStep >= _maxComboStep)
            return;

        int chargeType = (effectiveStep == 0) ? 1 : effectiveStep + 1;

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
                int effectiveMax = GetEffectiveMaxStep();
                if (_hasBufferedAttack && _comboStep < effectiveMax)
                {
                    int nextStep = _comboStep + 1;

                    // N5 → E6 移行判定（バッファ消費時のエボリューション開始）
                    if (nextStep == 6 && !_isEvolution && CanStartEvolution())
                    {
                        _isEvolution = true;
                        Debug.Log($"[Combo] {gameObject.name}: エボリューション開始（バッファ）！");
                    }

                    string label = (_isEvolution && nextStep >= 6) ? $"E{nextStep}" : $"N{nextStep}";
                    Debug.Log($"[Combo] {gameObject.name}: バッファ消費 → {label}");
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
                _attackSequence++;
                _segmentElapsed = 0f;
                Debug.Log($"[Combo] {gameObject.name}: Dラッシュ {_dashRushHitCount}hit");
            }
            return;
        }

        // 新規ダッシュ攻撃開始
        if (!_stateMachine.CanAcceptInput(InputType.NormalAttack)) return;

        // N コンボをリセット（ダッシュ攻撃は N コンボとは別系統）
        ResetCombo();

        _isDashAttacking = true;
        _dashAttackTimer = WeaponData.GetWeaponParams(GetWeaponType()).DashAttackDuration;
        _dashRushHitCount = 0;
        _attackSequence++;
        _segmentElapsed = 0f;

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
    /// 新しいコンボ段を開始する（N攻撃・E攻撃共通）
    /// </summary>
    private void StartComboStep(int step)
    {
        _comboStep = step;
        _attackTimer = GetAttackDuration(step);
        _comboWindowOpen = false;
        _hasBufferedAttack = false;
        _inputBufferTimer = 0f;
        _networkComboStep.Value = (byte)step;
        _attackSequence++;
        _segmentElapsed = 0f;

        string label = (_isEvolution && step >= 6) ? $"E{step}" : $"N{step}";
        Debug.Log($"[Combo] {gameObject.name}: {label} 開始");
    }

    /// <summary>
    /// 通常攻撃コンボ状態をリセットする（エボリューションもリセット）
    /// </summary>
    private void ResetCombo()
    {
        _comboStep = 0;
        _attackTimer = 0f;
        _comboWindowOpen = false;
        _hasBufferedAttack = false;
        _inputBufferTimer = 0f;
        _isEvolution = false;
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
        _attackSequence++;
        _segmentElapsed = 0f;

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
    // セグメント経過時間更新
    // ============================================================

    /// <summary>
    /// 攻撃中であればセグメント経過時間を進める（HitboxSystem のアクティブフレーム判定用）
    /// </summary>
    private void UpdateSegmentTimer()
    {
        if (_comboStep > 0 || _chargeType > 0 || _isDashAttacking)
        {
            _segmentElapsed += GameConfig.FIXED_DELTA_TIME;
        }
    }

    // ============================================================
    // 持続時間テーブル（武器種対応）
    // ============================================================

    /// <summary>
    /// コンボ段数に応じた攻撃持続時間を返す（N攻撃 or エボリューション、武器種対応）
    /// </summary>
    private float GetAttackDuration(int step)
    {
        // エボリューション攻撃 E6-E9
        if (_isEvolution && step >= 6)
            return WeaponData.GetEvolutionDuration(GetWeaponType(), step);
        return WeaponData.GetNormalDuration(GetWeaponType(), step);
    }

    /// <summary>
    /// チャージ技番号に応じた持続時間を返す（武器種から取得）
    /// </summary>
    private float GetChargeDuration(int chargeType)
    {
        return WeaponData.GetChargeDuration(GetWeaponType(), chargeType);
    }

    // ============================================================
    // エボリューション攻撃（★サーバー側で実行★）
    // ============================================================

    /// <summary>エボリューション攻撃の最大段数（E9）</summary>
    private const int MAX_EVOLUTION_STEP = 9;

    /// <summary>
    /// エボリューション攻撃の発動条件を満たすか判定する
    /// 条件: 連撃強化Lv3 + 無双ゲージMAX
    /// </summary>
    private bool CanStartEvolution()
    {
        if (_comboEnhanceLevel.Value < GameConfig.MAX_COMBO_ENHANCE_LEVEL) return false;
        return _musouGauge != null && _musouGauge.IsGaugeFull;
    }

    /// <summary>
    /// 現在のコンボの実効最大段数を返す
    /// エボリューション中は E9(=9)、N5でエボリューション開始可能なら9、それ以外は _maxComboStep
    /// </summary>
    private int GetEffectiveMaxStep()
    {
        if (_isEvolution) return MAX_EVOLUTION_STEP;

        // N5 時点でエボリューション発動可能なら、E9 まで継続可能
        if (_comboStep == 5 && CanStartEvolution()) return MAX_EVOLUTION_STEP;

        return _maxComboStep;
    }

    // ============================================================
    // 連撃強化（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 連撃強化レベルに応じた最大コンボ段数を返す
    /// Lv0: N4, Lv1: N5, Lv2+: N6
    /// </summary>
    private int GetMaxComboStep(int enhanceLevel)
    {
        return enhanceLevel switch
        {
            0 => GameConfig.MAX_COMBO_STEP_BASE,  // N4
            1 => GameConfig.MAX_COMBO_STEP_BASE + 1, // N5
            _ => GameConfig.MAX_COMBO_STEP_BASE + 2, // N6（Lv2以上）
        };
    }

    /// <summary>
    /// 連撃強化を+1する（仙箪強化から呼ばれる。サーバー専用）
    /// Lv3が上限。Lv3ではエボリューション攻撃が解放される（M4-3b）
    /// </summary>
    public void EnhanceCombo()
    {
        if (!IsServer) return;

        int newLevel = _comboEnhanceLevel.Value + 1;
        if (newLevel > GameConfig.MAX_COMBO_ENHANCE_LEVEL) return;

        _comboEnhanceLevel.Value = newLevel;
        _maxComboStep = GetMaxComboStep(newLevel);
        Debug.Log($"[Combo] {gameObject.name}: 連撃強化 Lv{newLevel}（最大N{_maxComboStep}）");
    }

    /// <summary>
    /// 全強化をリセットする（死亡時に呼ばれる。サーバー専用）
    /// </summary>
    public void ResetEnhancements()
    {
        if (!IsServer) return;

        _comboEnhanceLevel.Value = 0;
        _maxComboStep = GameConfig.MAX_COMBO_STEP_BASE;
        Debug.Log($"[Combo] {gameObject.name}: 連撃強化リセット（N{_maxComboStep}まで）");
    }
}
