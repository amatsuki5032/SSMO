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

    /// <summary>
    /// 所持仙箪数（サーバー権威）
    /// NPC兵士撃破時にドロップされた仙箪アイテムを拾うと+1
    /// SENTAN_REQUIRED_FOR_ENHANCE 個で連撃強化1回分
    /// 死亡時もリセットしない（試合中は永続）
    /// </summary>
    private readonly NetworkVariable<int> _sentanCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// C1 刻印種別（サーバー権威）
    /// C1 発動時にこの刻印に応じたモーションパラメータを使用する
    /// </summary>
    private readonly NetworkVariable<byte> _c1Inscription = new(
        (byte)InscriptionType.Thrust,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// C6 刻印種別（サーバー権威）
    /// C6 発動時にこの刻印に応じたモーションパラメータを使用する
    /// </summary>
    private readonly NetworkVariable<byte> _c6Inscription = new(
        (byte)InscriptionType.Thrust,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>連撃強化レベル（0〜3。読み取り専用）</summary>
    public int ComboEnhanceLevel => _comboEnhanceLevel.Value;

    /// <summary>所持仙箪数（読み取り専用）</summary>
    public int SentanCount => _sentanCount.Value;

    /// <summary>C1 刻印種別（読み取り専用）</summary>
    public InscriptionType C1Inscription => (InscriptionType)_c1Inscription.Value;

    /// <summary>C6 刻印種別（読み取り専用）</summary>
    public InscriptionType C6Inscription => (InscriptionType)_c6Inscription.Value;

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
    // サーバー側管理データ — ジャンプ攻撃
    // ============================================================

    private bool _isJumpAttacking;      // JA（ジャンプ通常攻撃）中フラグ
    private bool _isJumpCharging;       // JC（ジャンプチャージ攻撃）中フラグ
    private float _jumpAttackTimer;     // JA/JC モーションの残り時間

    /// <summary>ジャンプ攻撃中か（HitboxSystem 用）</summary>
    public bool IsJumpAttacking => _isJumpAttacking;

    /// <summary>ジャンプチャージ中か（HitboxSystem 用）</summary>
    public bool IsJumpCharging => _isJumpCharging;

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
    // サーバー側管理データ — ブレイクチャージ（武器2攻撃）
    // ============================================================

    /// <summary>
    /// 武器2の武器種（サーバー権威）
    /// デフォルトは大剣。武器2選択UIはM6で実装。デバッグヘルパーで変更可能
    /// </summary>
    private readonly NetworkVariable<WeaponType> _weapon2Type = new(
        WeaponType.GreatSword,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>武器2の武器種（読み取り専用）</summary>
    public WeaponType Weapon2Type => _weapon2Type.Value;

    private bool _isBreakCharging;      // ブレイクチャージ中フラグ
    private float _breakChargeTimer;    // ブレイクチャージモーションの残り時間
    private int _breakRushStack;        // ブレイクラッシュスタック数（連続BC回数）
    private float _breakRushTimer;      // ブレイクラッシュウィンドウタイマー
    private int _breakChargeVariant;    // ブレイクチャージの種類: 1=BC(地上), 2=DBC(ダッシュ), 3=JBC(空中)

    /// <summary>ブレイクチャージ中か（HitboxSystem 用）</summary>
    public bool IsBreakCharging => _isBreakCharging;

    /// <summary>ブレイクチャージの種類（1=BC, 2=DBC, 3=JBC）</summary>
    public int BreakChargeVariant => _breakChargeVariant;

    /// <summary>ブレイクラッシュスタック数（攻撃力ボーナス計算用）</summary>
    public int BreakRushStack => _breakRushStack;

    /// <summary>ブレイクラッシュATKボーナス倍率（1.0 + BREAK_RUSH_ATK_BONUS * stack）</summary>
    public float BreakRushAtkMultiplier => 1.0f + GameConfig.BREAK_RUSH_ATK_BONUS * _breakRushStack;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
        _playerMovement = GetComponent<PlayerMovement>();
        _musouGauge = GetComponent<MusouGauge>();

        if (_stateMachine == null)
            Debug.LogError($"[ComboSystem] {gameObject.name}: CharacterStateMachine が見つかりません");
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 初期状態: 連撃強化なし、仙箪0個
            _comboEnhanceLevel.Value = 0;
            _sentanCount.Value = 0;
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
        UpdateJumpAttack();
        UpdateBreakCharge();
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
    // ジャンプ攻撃入力処理（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// ジャンプ通常攻撃（JA）を開始する。サーバー権威
    /// Jump ステート中に □ で発動。着地まで入力不可
    /// </summary>
    public void TryStartJumpAttack()
    {
        if (!IsServer) return;
        if (!_stateMachine.CanAcceptInput(InputType.NormalAttack)) return;

        // 他の攻撃状態をリセット
        ResetCombo();
        ResetCharge();
        ResetDashAttack();

        _isJumpAttacking = true;
        _isJumpCharging = false;
        // JA の持続時間: 武器種の N1 持続時間を流用（JA 専用パラメータがないため）
        _jumpAttackTimer = WeaponData.GetWeaponParams(GetWeaponType()).NormalDurations[0];
        _attackSequence++;
        _segmentElapsed = 0f;

        _stateMachine.TryChangeState(CharacterState.JumpAttack);
        Debug.Log($"[Combo] {gameObject.name}: JA（ジャンプ攻撃）開始");
    }

    /// <summary>
    /// ジャンプチャージ攻撃（JC）を開始する。サーバー権威
    /// Jump ステート中に △ で発動。着地まで入力不可
    /// </summary>
    public void TryStartJumpCharge()
    {
        if (!IsServer) return;
        if (!_stateMachine.CanAcceptInput(InputType.ChargeAttack)) return;

        // 他の攻撃状態をリセット
        ResetCombo();
        ResetCharge();
        ResetDashAttack();

        _isJumpAttacking = false;
        _isJumpCharging = true;
        // JC の持続時間: 武器種の C1 持続時間を流用（JC 専用パラメータがないため）
        _jumpAttackTimer = WeaponData.GetWeaponParams(GetWeaponType()).ChargeDurations[0];
        _attackSequence++;
        _segmentElapsed = 0f;

        _stateMachine.TryChangeState(CharacterState.JumpAttack);
        Debug.Log($"[Combo] {gameObject.name}: JC（ジャンプチャージ）開始");
    }

    // ============================================================
    // ジャンプ攻撃更新（★サーバー側 FixedUpdate★）
    // ============================================================

    /// <summary>
    /// JA/JC タイマーを毎 FixedUpdate で更新する
    /// タイマー満了で Jump に遷移（まだ空中なので着地判定は PlayerMovement に任せる）
    /// </summary>
    private void UpdateJumpAttack()
    {
        if (!_isJumpAttacking && !_isJumpCharging) return;

        // 外部要因で JumpAttack から離脱した場合（被弾等）→ リセット
        if (_stateMachine.CurrentState != CharacterState.JumpAttack)
        {
            ResetJumpAttack();
            return;
        }

        _jumpAttackTimer -= GameConfig.FIXED_DELTA_TIME;

        if (_jumpAttackTimer <= 0f)
        {
            string label = _isJumpCharging ? "JC" : "JA";
            Debug.Log($"[Combo] {gameObject.name}: {label} 終了");
            ResetJumpAttack();
            _stateMachine.TryChangeState(CharacterState.Jump);
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

    /// <summary>
    /// ジャンプ攻撃状態をリセットする
    /// </summary>
    private void ResetJumpAttack()
    {
        _isJumpAttacking = false;
        _isJumpCharging = false;
        _jumpAttackTimer = 0f;
    }

    // ============================================================
    // セグメント経過時間更新
    // ============================================================

    /// <summary>
    /// 攻撃中であればセグメント経過時間を進める（HitboxSystem のアクティブフレーム判定用）
    /// </summary>
    private void UpdateSegmentTimer()
    {
        if (_comboStep > 0 || _chargeType > 0 || _isDashAttacking || _isBreakCharging)
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
    /// チャージ技番号に応じた持続時間を返す（武器種・刻印から取得）
    /// C1/C6は刻印に応じたパラメータを使用する
    /// </summary>
    private float GetChargeDuration(int chargeType)
    {
        // C1: 刻印パラメータ使用
        if (chargeType == 1)
            return WeaponData.GetInscriptionC1Duration(C1Inscription);
        // C6: 刻印パラメータ使用
        if (chargeType == 6)
            return WeaponData.GetInscriptionC6Duration(C6Inscription);
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
    /// 仙箪カウントはリセットしない（試合中は永続）
    /// </summary>
    public void ResetEnhancements()
    {
        if (!IsServer) return;

        _comboEnhanceLevel.Value = 0;
        _maxComboStep = GameConfig.MAX_COMBO_STEP_BASE;
        Debug.Log($"[Combo] {gameObject.name}: 連撃強化リセット（N{_maxComboStep}まで）");
    }

    /// <summary>
    /// 仙箪を追加する（SentanItem から呼ばれる。サーバー専用）
    /// </summary>
    /// <param name="count">追加数</param>
    public void AddSentan(int count)
    {
        if (!IsServer) return;
        if (count <= 0) return;

        _sentanCount.Value += count;
        Debug.Log($"[Combo] {gameObject.name}: 仙箪取得 → 所持数 {_sentanCount.Value}");
    }

    // ============================================================
    // 刻印設定（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// C1 刻印を設定する（サーバー専用。UIから呼ばれる想定）
    /// C1 は突/陣/砕/盾の4種のみ
    /// </summary>
    public void SetC1Inscription(InscriptionType type)
    {
        if (!IsServer) return;
        // C1 は突/陣/砕/盾の4種のみ（覇・衛はC6専用）
        if (type == InscriptionType.Conquer || type == InscriptionType.Guard) return;

        _c1Inscription.Value = (byte)type;
        Debug.Log($"[Combo] {gameObject.name}: C1刻印 → {type}");
    }

    /// <summary>
    /// C6 刻印を設定する（サーバー専用。UIから呼ばれる想定）
    /// C6 は全6種対応
    /// </summary>
    public void SetC6Inscription(InscriptionType type)
    {
        if (!IsServer) return;

        _c6Inscription.Value = (byte)type;
        Debug.Log($"[Combo] {gameObject.name}: C6刻印 → {type}");
    }

    // ============================================================
    // ブレイクチャージ入力処理（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// ブレイクチャージ入力を処理する。サーバー権威
    /// 状況に応じて武器2のパラメータで攻撃する。3パターン:
    ///   地上通常 (BC)  → 武器2の C3
    ///   ダッシュ中 (DBC) → 武器2の D
    ///   空中 (JBC)      → 武器2の JC
    /// 連続発動でブレイクラッシュ（ATK+10%スタック）
    /// </summary>
    /// <param name="isDashing">ダッシュ状態か</param>
    /// <param name="isAirborne">空中か</param>
    /// <param name="moveInput">向き設定用の移動入力</param>
    public void TryStartBreakCharge(bool isDashing, bool isAirborne, Vector2 moveInput)
    {
        if (!IsServer) return;

        // ブレイクチャージ中は受け付けない（連打防止）
        if (_isBreakCharging) return;

        // 入力受付判定
        if (!_stateMachine.CanAcceptInput(InputType.BreakCharge)) return;

        // 空中ブレイクチャージ（JBC）: Jump ステート中のみ
        if (isAirborne)
        {
            StartBreakCharge(3, moveInput); // JBC = 武器2のJC
            return;
        }

        // ダッシュブレイクチャージ（DBC）
        if (isDashing)
        {
            StartBreakCharge(2, moveInput); // DBC = 武器2のD
            return;
        }

        // 地上ブレイクチャージ（BC）
        StartBreakCharge(1, moveInput); // BC = 武器2のC3
    }

    /// <summary>
    /// ブレイクチャージを開始する
    /// </summary>
    /// <param name="variant">1=BC(地上), 2=DBC(ダッシュ), 3=JBC(空中)</param>
    /// <param name="moveInput">向き設定用の移動入力</param>
    private void StartBreakCharge(int variant, Vector2 moveInput)
    {
        // 他の攻撃状態をリセット
        ResetCombo();
        ResetCharge();
        ResetDashAttack();

        _isBreakCharging = true;
        _breakChargeVariant = variant;
        _breakChargeTimer = GetBreakChargeDuration(variant);
        _attackSequence++;
        _segmentElapsed = 0f;

        // ブレイクラッシュ: ウィンドウ内の連続BCならスタック加算
        if (_breakRushTimer > 0f && _breakRushStack < GameConfig.BREAK_RUSH_MAX_STACK)
        {
            _breakRushStack++;
            Debug.Log($"[BreakCharge] {gameObject.name}: ブレイクラッシュ {_breakRushStack}スタック（ATK+{_breakRushStack * GameConfig.BREAK_RUSH_ATK_BONUS * 100}%）");
        }
        else
        {
            _breakRushStack = 0; // ウィンドウ外 → リセット
        }

        _stateMachine.TryChangeState(CharacterState.BreakCharge);

        // 開始時のスティック方向で向きを設定
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        string[] variantNames = { "", "BC(地上)", "DBC(ダッシュ)", "JBC(空中)" };
        Debug.Log($"[BreakCharge] {gameObject.name}: {variantNames[variant]} 開始（武器2: {_weapon2Type.Value}）");
    }

    // ============================================================
    // ブレイクチャージ更新（★サーバー側 FixedUpdate★）
    // ============================================================

    /// <summary>
    /// ブレイクチャージタイマーを毎 FixedUpdate で更新する
    /// </summary>
    private void UpdateBreakCharge()
    {
        // ブレイクラッシュウィンドウタイマー減算（攻撃中でなくても進める）
        if (_breakRushTimer > 0f)
        {
            _breakRushTimer -= GameConfig.FIXED_DELTA_TIME;
            if (_breakRushTimer <= 0f)
            {
                _breakRushTimer = 0f;
                _breakRushStack = 0;
            }
        }

        if (!_isBreakCharging) return;

        // 外部要因で BreakCharge から離脱した場合（被弾等）→ リセット
        if (_stateMachine.CurrentState != CharacterState.BreakCharge)
        {
            ResetBreakCharge(false); // ラッシュウィンドウは維持
            return;
        }

        _breakChargeTimer -= GameConfig.FIXED_DELTA_TIME;

        if (_breakChargeTimer <= 0f)
        {
            string[] variantNames = { "", "BC", "DBC", "JBC" };
            Debug.Log($"[BreakCharge] {gameObject.name}: {variantNames[_breakChargeVariant]} 終了");
            ResetBreakCharge(true); // 正常終了: ラッシュウィンドウ開始
            _stateMachine.TryChangeState(CharacterState.Idle);
        }
    }

    /// <summary>
    /// ブレイクチャージ状態をリセットする
    /// </summary>
    /// <param name="startRushWindow">true: ブレイクラッシュウィンドウを開始</param>
    private void ResetBreakCharge(bool startRushWindow)
    {
        _isBreakCharging = false;
        _breakChargeTimer = 0f;
        _breakChargeVariant = 0;

        if (startRushWindow)
        {
            // 正常終了時: 次のBC入力をブレイクラッシュとして扱うウィンドウを開始
            _breakRushTimer = GameConfig.BREAK_RUSH_WINDOW;
        }
    }

    // ============================================================
    // ブレイクチャージ — 持続時間・倍率（武器2パラメータ参照）
    // ============================================================

    /// <summary>
    /// ブレイクチャージの種類に応じた持続時間を返す（武器2のパラメータ参照）
    /// </summary>
    private float GetBreakChargeDuration(int variant)
    {
        var w2 = WeaponData.GetWeaponParams(_weapon2Type.Value);
        return variant switch
        {
            1 => w2.ChargeDurations[2],   // BC = 武器2の C3 持続時間
            2 => w2.DashAttackDuration,    // DBC = 武器2の D 持続時間
            3 => w2.ChargeDurations[0],    // JBC = 武器2の C1 持続時間（JC用の独立パラメータがないため C1 流用）
            _ => GameConfig.DEFAULT_BREAK_CHARGE_DURATION,
        };
    }

    /// <summary>
    /// ブレイクチャージのモーション倍率を返す（武器2のパラメータ参照。DamageCalculator から呼ばれる）
    /// </summary>
    public float GetBreakChargeMultiplier()
    {
        var w2 = WeaponData.GetWeaponParams(_weapon2Type.Value);
        return _breakChargeVariant switch
        {
            1 => w2.ChargeMultipliers[2],      // BC = 武器2の C3 倍率
            2 => w2.DashAttackMultiplier,       // DBC = 武器2の D 倍率
            3 => w2.JumpChargeMultiplier,       // JBC = 武器2の JC 倍率
            _ => 1.0f,
        };
    }

    /// <summary>
    /// 武器2の武器種を設定する（サーバー専用。デバッグヘルパー/UIから呼ばれる）
    /// </summary>
    public void SetWeapon2Type(WeaponType type)
    {
        if (!IsServer) return;

        _weapon2Type.Value = type;
        Debug.Log($"[BreakCharge] {gameObject.name}: 武器2 → {type}");
    }
}
