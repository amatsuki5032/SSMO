using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー権威型キャラクターステートマシン
///
/// 設計方針:
/// - NetworkVariable でステートと状態異常を全クライアントに同期
/// - ステート遷移の最終権限はサーバーが持つ
/// - クライアントは ServerRpc で遷移をリクエストするのみ
/// - タイマー系ステート（のけぞり・ダウン・気絶・凍結等）はサーバー FixedUpdate で自動遷移
/// - 無敵管理もサーバーのみ
///
/// 参照: docs/m2-1-instruction.md, docs/combat-spec.md セクション22
/// </summary>
public class CharacterStateMachine : NetworkBehaviour
{
    // ============================================================
    // 同期変数
    // ============================================================

    /// <summary>
    /// 現在のキャラクターステート。サーバーのみ書き込み可能
    /// 初期値は OnNetworkSpawn() で設定する（Spawn前の .Value 代入は NGO 警告の原因になる）
    /// </summary>
    private readonly NetworkVariable<CharacterState> _state = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// 状態異常フラグ。ステートとは別にビットフラグで管理（感電・燃焼・鈍足は他ステートと共存）
    /// 初期値は OnNetworkSpawn() で設定する
    /// </summary>
    private readonly NetworkVariable<StatusEffect> _statusEffects = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>現在のステート（読み取り専用）</summary>
    public CharacterState CurrentState => _state.Value;

    /// <summary>現在の状態異常フラグ（読み取り専用）</summary>
    public StatusEffect CurrentStatusEffects => _statusEffects.Value;

    // ============================================================
    // イベント
    // ============================================================

    /// <summary>
    /// ステートが変わった時に他のコンポーネントが反応するためのイベント
    /// 引数: (旧ステート, 新ステート)
    /// サーバー・クライアント両方で発火する（NetworkVariable.OnValueChanged 経由）
    /// </summary>
    public event System.Action<CharacterState, CharacterState> OnStateChanged;

    // ============================================================
    // 内部状態（サーバーのみ使用）
    // ============================================================

    // ステート持続タイマー（のけぞり・ダウン・気絶・凍結等の自動遷移用）
    private float _stateTimer;

    // 無敵フレームカウンター（ジャンプ離陸・受け身）
    private int _invincibleFrames;

    // 現在無敵状態か（外部から参照用）
    private bool _isInvincible;

    // 次の Hitstun 遷移時に使う持続時間オーバーライド（0 = デフォルト使用）
    // ReactionSystem が軽/重を指定してから TryChangeState する
    private float _hitstunOverride;

    /// <summary>
    /// 次の Hitstun 遷移で使用する持続時間を設定する（サーバー側のみ）
    /// TryChangeState(Hitstun) の直前に呼ぶ。遷移後に自動で 0 にリセットされる
    /// </summary>
    public void SetHitstunDuration(float duration)
    {
        _hitstunOverride = duration;
    }

    /// <summary>
    /// 現在無敵状態か。サーバーが管理する
    /// Musou/TrueMusou/Getup は全フレーム無敵
    /// Jump/AirRecover はフレーム数で管理
    /// </summary>
    public bool IsInvincible => _isInvincible;

    // ============================================================
    // ライフサイクル
    // ============================================================

    public override void OnNetworkSpawn()
    {
        // サーバーのみ: NetworkVariable の初期値を設定
        // Spawn 後に代入することで "NetworkVariable is written to before spawned" 警告を回避
        if (IsServer)
        {
            _state.Value = CharacterState.Idle;
            _statusEffects.Value = StatusEffect.None;
        }

        // ステート変更のコールバック登録（全クライアント + サーバー）
        _state.OnValueChanged += HandleStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        _state.OnValueChanged -= HandleStateChanged;
    }

    /// <summary>
    /// FixedUpdate: サーバーのみでタイマー管理と自動遷移を実行
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;

        UpdateStateTimer();
        UpdateInvincibility();
    }

    // ============================================================
    // ステート遷移（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// ステート遷移を試行する。バリデーション後に遷移
    /// サーバー側でのみ呼び出すこと
    /// </summary>
    /// <param name="newState">遷移先ステート</param>
    /// <returns>遷移が成功したか</returns>
    public bool TryChangeState(CharacterState newState)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[StateMachine] TryChangeState はサーバー側でのみ実行可能");
            return false;
        }

        // 同じステートへの遷移は無視
        if (_state.Value == newState) return false;

        // Dead からは復帰しない（リスポーンは別処理）
        if (_state.Value == CharacterState.Dead) return false;

        // 遷移を実行
        CharacterState oldState = _state.Value;
        _state.Value = newState;

        // 新ステートに応じたタイマー・無敵フレームを設定
        InitializeStateTimer(newState);
        InitializeInvincibility(newState);

        // Hitstun オーバーライドは遷移後にリセット（次回の遷移で再利用されないように）
        if (newState == CharacterState.Hitstun)
            _hitstunOverride = 0f;

        return true;
    }

    /// <summary>
    /// 強制的にステートを設定する（リスポーン等、バリデーションをスキップしたい場合）
    /// サーバー側でのみ呼び出すこと
    /// </summary>
    public void ForceState(CharacterState newState)
    {
        if (!IsServer) return;

        _state.Value = newState;
        InitializeStateTimer(newState);
        InitializeInvincibility(newState);
    }

    // ============================================================
    // 入力受付判定
    // ============================================================

    /// <summary>
    /// 現在のステートで特定の入力が受付可能か判定する
    /// サーバー・クライアント両方で呼べる（予測用にクライアントでも使用）
    ///
    /// 遷移ルール（docs/m2-1-instruction.md セクション2 準拠）:
    /// Idle        → 全入力受付
    /// Move        → 攻撃/ガード/ジャンプ/無双
    /// Attack      → 次段N(□) / チャージ(△)
    /// Charge      → (将来: ブレイクL2)
    /// DashAttack  → ラッシュ(□)
    /// DashRush    → □連打で継続
    /// Guard       → 移動 / EG(△) / 解除
    /// GuardMove   → ガード解除 / EG(△)
    /// EGPrepare   → L1+△維持 / 解除
    /// EGReady     → 維持 / 解除
    /// EGCounter   → 入力不可（自動）
    /// Jump        → JA(□) / JC(△)
    /// JumpAttack  → 入力不可
    /// MusouCharge → 離す→Idle / MAXで○→Musou
    /// Musou       → 入力不可（無敵）
    /// TrueMusou   → 入力不可（無敵）
    /// Hitstun     → 無双のみ受付（脱出）
    /// Launch      → 受け身(×)のみ（不能時間後）
    /// AirHitstun  → 受け身(×)のみ（不能時間後）
    /// AirRecover  → 入力不可（着地まで）
    /// 全ダウン系  → 入力不可（起き上がりまで）
    /// Getup       → 入力不可（無敵）
    /// Freeze      → 入力不可
    /// Dead        → 入力不可
    /// </summary>
    public bool CanAcceptInput(InputType input)
    {
        return _state.Value switch
        {
            // === 基本行動 ===
            CharacterState.Idle => true, // 全入力受付

            CharacterState.Move => input switch
            {
                InputType.NormalAttack => true,
                InputType.ChargeAttack => true,
                InputType.Jump => true,
                InputType.Musou => true,
                InputType.Guard => true,
                _ => input == InputType.Move, // 移動も継続可
            },

            // === 攻撃 ===
            CharacterState.Attack => input switch
            {
                InputType.NormalAttack => true,   // 次段コンボ
                InputType.ChargeAttack => true,   // チャージ派生
                _ => false,
            },

            CharacterState.Charge => input == InputType.ChargeAttack, // C3ラッシュ用

            CharacterState.DashAttack => input == InputType.NormalAttack, // ラッシュ派生

            CharacterState.DashRush => input == InputType.NormalAttack, // □連打継続

            // === ジャンプ ===
            CharacterState.Jump => input switch
            {
                InputType.NormalAttack => true,   // JA
                InputType.ChargeAttack => true,   // JC
                _ => false,
            },

            CharacterState.JumpAttack => false, // 着地まで入力不可

            // === 防御 ===
            CharacterState.Guard => input switch
            {
                InputType.Move => true,           // ガード移動
                InputType.ChargeAttack => true,   // EG準備（△）
                InputType.Guard => true,          // ガード維持/解除
                _ => false,
            },

            CharacterState.GuardMove => input switch
            {
                InputType.ChargeAttack => true,   // EG準備（△）
                InputType.Guard => true,          // ガード解除
                InputType.Move => true,           // 移動継続
                _ => false,
            },

            CharacterState.EGPrepare => input == InputType.Guard, // 維持/解除

            CharacterState.EGReady => input == InputType.Guard,   // 維持/解除

            CharacterState.EGCounter => false, // 自動遷移、入力不可

            // === 無双 ===
            CharacterState.MusouCharge => input == InputType.Musou, // ○で発動/離すでキャンセル

            CharacterState.Musou => false,     // 無敵中、入力不可
            CharacterState.TrueMusou => false, // 無敵中、入力不可

            // === 被弾 ===
            CharacterState.Hitstun => input == InputType.Musou, // 無双で脱出

            // Launch/AirHitstun: 受け身のみ（不能時間後）
            // 不能時間の判定は呼び出し側（コンボシステム）が行う
            // 感電中は受け身不可（StatusEffect.Electrified フラグで判定）
            CharacterState.Launch => input == InputType.Jump && !HasStatusEffect(StatusEffect.Electrified),
            CharacterState.AirHitstun => input == InputType.Jump && !HasStatusEffect(StatusEffect.Electrified),

            CharacterState.AirRecover => false, // 着地まで入力不可
            CharacterState.Slam => false,       // 叩きつけ中、入力不可

            // === ダウン系 ===
            CharacterState.FaceDownDown => false,
            CharacterState.CrumbleDown => false,
            CharacterState.SprawlDown => false,
            CharacterState.Stun => false,
            CharacterState.Getup => false,      // 起き上がり無敵中

            // === 状態異常 ===
            CharacterState.Freeze => false,     // 凍結中、入力不可

            // === 死亡 ===
            CharacterState.Dead => false,

            _ => false,
        };
    }

    /// <summary>
    /// 現在のステートで移動が可能かを簡易判定する
    /// PlayerMovement から呼ばれるヘルパー
    /// </summary>
    public bool CanMove()
    {
        return _state.Value switch
        {
            CharacterState.Idle => true,
            CharacterState.Move => true,
            CharacterState.GuardMove => true,
            _ => false,
        };
    }

    // ============================================================
    // 状態異常フラグ管理（★サーバーのみ★）
    // ============================================================

    /// <summary>状態異常フラグを付与する</summary>
    public void AddStatusEffect(StatusEffect effect)
    {
        if (!IsServer) return;
        _statusEffects.Value |= effect;
    }

    /// <summary>状態異常フラグを解除する</summary>
    public void RemoveStatusEffect(StatusEffect effect)
    {
        if (!IsServer) return;
        _statusEffects.Value &= ~effect;
    }

    /// <summary>指定の状態異常フラグがあるか判定する</summary>
    public bool HasStatusEffect(StatusEffect effect)
    {
        return (_statusEffects.Value & effect) != 0;
    }

    // ============================================================
    // ステート遷移リクエスト（クライアント → サーバー）
    // ============================================================

    /// <summary>
    /// クライアントからステート遷移をリクエストする
    /// サーバーがバリデーション後に遷移を実行する
    /// </summary>
    [ServerRpc]
    public void RequestStateChangeServerRpc(CharacterState requestedState)
    {
        TryChangeState(requestedState);
    }

    // ============================================================
    // タイマー管理（サーバーのみ）
    // ============================================================

    /// <summary>
    /// 新ステートに応じたタイマーを初期化する
    /// タイマー満了時は自動的に次のステートに遷移する
    /// </summary>
    private void InitializeStateTimer(CharacterState state)
    {
        _stateTimer = state switch
        {
            CharacterState.Hitstun => _hitstunOverride > 0f ? _hitstunOverride : GameConfig.HITSTUN_DURATION,
            CharacterState.AirHitstun => GameConfig.HITSTUN_LIGHT_DURATION,
            CharacterState.Launch => GameConfig.LAUNCH_DURATION,
            CharacterState.FaceDownDown => GameConfig.FACEDOWN_DOWN_DURATION,
            CharacterState.CrumbleDown => GameConfig.CRUMBLE_DOWN_DURATION,
            CharacterState.SprawlDown => GameConfig.SPRAWL_DOWN_DURATION,
            CharacterState.Getup => GameConfig.GETUP_DURATION,
            CharacterState.Stun => GameConfig.STUN_DURATION,
            CharacterState.Freeze => GameConfig.FREEZE_DURATION,
            CharacterState.Musou => GameConfig.MUSOU_DURATION_SEC,
            CharacterState.TrueMusou => GameConfig.TRUE_MUSOU_DURATION_SEC,
            _ => 0f,
        };
    }

    /// <summary>
    /// タイマーを毎FixedUpdateで減算し、満了時に自動遷移する
    /// </summary>
    private void UpdateStateTimer()
    {
        if (_stateTimer <= 0f) return;

        _stateTimer -= GameConfig.FIXED_DELTA_TIME;

        if (_stateTimer > 0f) return;

        // タイマー満了 → 自動遷移
        _stateTimer = 0f;

        switch (_state.Value)
        {
            // のけぞり → Idle（立ち復帰）
            case CharacterState.Hitstun:
                TryChangeState(CharacterState.Idle);
                break;

            // 空中ヒット → SprawlDown（タイマー満了フォールバック。通常は着地判定で遷移）
            case CharacterState.AirHitstun:
                TryChangeState(CharacterState.SprawlDown);
                break;

            // 打ち上げ → ダウン（受け身不能時間終了後に着地判定で遷移するが、
            // タイマー満了はフォールバック：地面に戻っていればダウンに移行）
            case CharacterState.Launch:
                TryChangeState(CharacterState.SprawlDown);
                break;

            // ダウン → 起き上がり
            case CharacterState.FaceDownDown:
            case CharacterState.CrumbleDown:
            case CharacterState.SprawlDown:
                TryChangeState(CharacterState.Getup);
                break;

            // 起き上がり → Idle
            case CharacterState.Getup:
                TryChangeState(CharacterState.Idle);
                break;

            // 気絶 → Idle
            case CharacterState.Stun:
                TryChangeState(CharacterState.Idle);
                break;

            // 凍結 → Idle（解除モーションは将来対応）
            case CharacterState.Freeze:
                TryChangeState(CharacterState.Idle);
                break;

            // 無双乱舞終了 → Idle
            case CharacterState.Musou:
            case CharacterState.TrueMusou:
                TryChangeState(CharacterState.Idle);
                break;
        }
    }

    // ============================================================
    // 無敵管理（サーバーのみ）
    // ============================================================

    /// <summary>
    /// 新ステートに応じた無敵を初期化する
    /// </summary>
    private void InitializeInvincibility(CharacterState state)
    {
        switch (state)
        {
            // 完全無敵ステート（持続時間中ずっと無敵 → タイマーで管理）
            case CharacterState.Musou:
            case CharacterState.TrueMusou:
            case CharacterState.Getup:
                _isInvincible = true;
                _invincibleFrames = -1; // -1 = タイマー管理（ステート終了まで無敵）
                break;

            // フレーム数で管理する無敵
            case CharacterState.Jump:
                _isInvincible = true;
                _invincibleFrames = GameConfig.JUMP_INVINCIBLE_FRAMES;
                break;

            case CharacterState.AirRecover:
                _isInvincible = true;
                _invincibleFrames = GameConfig.AIR_RECOVER_INVINCIBLE_FRAMES;
                break;

            // その他: 無敵解除
            default:
                _isInvincible = false;
                _invincibleFrames = 0;
                break;
        }
    }

    /// <summary>
    /// 無敵フレームを毎FixedUpdateで減算する
    /// </summary>
    private void UpdateInvincibility()
    {
        // -1 はステート終了まで無敵（タイマー管理）なので減算しない
        if (_invincibleFrames <= 0) return;

        _invincibleFrames--;
        if (_invincibleFrames <= 0)
        {
            _isInvincible = false;
        }
    }

    // ============================================================
    // コールバック
    // ============================================================

    /// <summary>
    /// NetworkVariable のステート変更コールバック
    /// サーバー・クライアント両方で発火する
    /// </summary>
    private void HandleStateChanged(CharacterState oldState, CharacterState newState)
    {
        Debug.Log($"[StateMachine] {gameObject.name}: {oldState} → {newState}");
        OnStateChanged?.Invoke(oldState, newState);
    }
}
