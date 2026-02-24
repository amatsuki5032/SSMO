using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 状態異常マネージャー（★サーバー権威★）
///
/// 状態異常（燃焼・鈍足・感電・凍結）の付与・解除・tick処理を一元管理する。
/// CharacterStateMachine の StatusEffect フラグと連携して状態を同期する。
///
/// 燃焼: 持続ダメージ（HPを0にはしない）。タイマー満了で解除
/// 鈍足: 移動速度低下 + ジャンプ不可。タイマー満了で解除
/// 感電: 受け身不可。攻撃を受けなければ自然解除 / コンボ上限で解除 / ダウンで解除
/// 凍結: Freeze ステートへ強制遷移（確率発動）。CharacterStateMachine のタイマーで解除
/// </summary>
public class StatusEffectManager : NetworkBehaviour
{
    // ============================================================
    // 参照
    // ============================================================

    private CharacterStateMachine _stateMachine;
    private HealthSystem _healthSystem;

    // ============================================================
    // 状態異常タイマー（サーバーのみ使用）
    // ============================================================

    private float _burnTimer;           // 燃焼残り時間
    private float _burnTickTimer;       // 燃焼ダメージ適用までの残り時間
    private float _slowTimer;           // 鈍足残り時間
    private float _electrifiedTimer;    // 感電残り時間（攻撃なし時の自然解除タイマー）
    private int _electrifiedComboCount; // 感電中に受けたコンボ数

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>感電中か（ReactionSystem が受け身判定に使用）</summary>
    public bool IsElectrified => _stateMachine != null && _stateMachine.HasStatusEffect(StatusEffect.Electrified);

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
        _healthSystem = GetComponent<HealthSystem>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // ダウンステートへの遷移で感電を解除するためのイベント登録
            _stateMachine.OnStateChanged += OnStateChangedServer;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            _stateMachine.OnStateChanged -= OnStateChangedServer;
        }
    }

    /// <summary>
    /// サーバーのみ: 毎 FixedUpdate で状態異常 tick 処理
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;

        UpdateBurn();
        UpdateSlow();
        UpdateElectrified();
    }

    // ============================================================
    // ステート変更コールバック（サーバーのみ）
    // ============================================================

    /// <summary>
    /// ダウンステートへの遷移で感電を自動解除する
    /// </summary>
    private void OnStateChangedServer(CharacterState oldState, CharacterState newState)
    {
        if (!IsServer) return;

        // ダウン系ステートに入ったら感電を解除
        if (newState == CharacterState.FaceDownDown ||
            newState == CharacterState.CrumbleDown ||
            newState == CharacterState.SprawlDown)
        {
            if (_electrifiedTimer > 0f)
            {
                ClearElectrified();
                Debug.Log($"[StatusEffect] {gameObject.name}: 感電解除（ダウン遷移）");
            }
        }
    }

    // ============================================================
    // 状態異常付与（サーバー専用。HitboxSystem から呼ばれる）
    // ============================================================

    /// <summary>
    /// 属性に応じた状態異常を付与する（サーバー専用）
    /// チャージ攻撃ヒット時に HitboxSystem から呼ばれる
    /// </summary>
    /// <param name="element">攻撃の属性</param>
    /// <param name="level">属性レベル（1〜4）</param>
    /// <param name="isTargetAirborne">被弾者が空中か（雷の気絶判定に使用）</param>
    public void ApplyElementEffect(ElementType element, int level, bool isTargetAirborne = false)
    {
        if (!IsServer) return;
        if (element == ElementType.None || level <= 0) return;

        switch (element)
        {
            case ElementType.Fire:
                ApplyBurn();
                break;
            case ElementType.Wind:
                ApplySlow();
                break;
            case ElementType.Ice:
                TryApplyFreeze();
                break;
            case ElementType.Thunder:
                ApplyElectrified();
                // 地上ヒット時のみ気絶も付与（空中では気絶しない）
                if (!isTargetAirborne)
                    ApplyStun();
                break;
            // Slash は状態異常なし（ダメージ計算で処理済み）
        }
    }

    /// <summary>
    /// 感電中にヒットを受けた時のコンボカウント増加（HitboxSystem から呼ばれる）
    /// コンボ上限に達したら感電を解除する
    /// </summary>
    public void OnElectrifiedHit()
    {
        if (!IsServer) return;
        if (_electrifiedTimer <= 0f) return;

        _electrifiedComboCount++;
        // 攻撃を受けるたびに自然解除タイマーをリセット（攻撃し続ける限り感電継続）
        _electrifiedTimer = GameConfig.ELECTRIFIED_DURATION;

        if (_electrifiedComboCount >= GameConfig.ELECTRIFIED_MAX_COMBO)
        {
            ClearElectrified();
            Debug.Log($"[StatusEffect] {gameObject.name}: 感電解除（コンボ上限 {GameConfig.ELECTRIFIED_MAX_COMBO}）");
        }
    }

    // ============================================================
    // 燃焼（炎属性）
    // ============================================================

    /// <summary>
    /// 燃焼を付与する。既に燃焼中の場合はタイマーをリセット（上書き）
    /// </summary>
    private void ApplyBurn()
    {
        _burnTimer = GameConfig.BURN_DURATION;
        _burnTickTimer = GameConfig.BURN_TICK_INTERVAL;
        _stateMachine.AddStatusEffect(StatusEffect.Burn);

        Debug.Log($"[StatusEffect] {gameObject.name}: 燃焼付与（{GameConfig.BURN_DURATION}秒）");
    }

    /// <summary>
    /// 燃焼の tick 処理
    /// - 一定間隔で持続ダメージを適用
    /// - HPを0にはできない（最低HP1で止める）
    /// - タイマー満了で解除
    /// </summary>
    private void UpdateBurn()
    {
        if (_burnTimer <= 0f) return;

        _burnTimer -= GameConfig.FIXED_DELTA_TIME;

        // タイマー満了 → 燃焼解除
        if (_burnTimer <= 0f)
        {
            _burnTimer = 0f;
            _burnTickTimer = 0f;
            _stateMachine.RemoveStatusEffect(StatusEffect.Burn);
            Debug.Log($"[StatusEffect] {gameObject.name}: 燃焼解除（時間切れ）");
            return;
        }

        // 持続ダメージ tick
        _burnTickTimer -= GameConfig.FIXED_DELTA_TIME;
        if (_burnTickTimer <= 0f)
        {
            _burnTickTimer += GameConfig.BURN_TICK_INTERVAL;

            if (_healthSystem != null)
            {
                // 1tick あたりのダメージ = DPS × tick間隔
                int tickDamage = Mathf.RoundToInt(GameConfig.BURN_DAMAGE_PER_SEC * GameConfig.BURN_TICK_INTERVAL);

                // HPを0にはできない（最低HP1で止める）
                int currentHp = _healthSystem.CurrentHp;
                if (currentHp <= 1) return; // 既にHP1以下なら燃焼ダメージなし

                int safeDamage = Mathf.Min(tickDamage, currentHp - 1);
                if (safeDamage > 0)
                {
                    _healthSystem.TakeDamage(safeDamage);
                    Debug.Log($"[StatusEffect] {gameObject.name}: 燃焼ダメージ {safeDamage}" +
                              $"（残りHP: {_healthSystem.CurrentHp}）");
                }
            }
        }
    }

    // ============================================================
    // 鈍足（風属性）
    // ============================================================

    /// <summary>
    /// 鈍足を付与する。既に鈍足中の場合はタイマーをリセット（上書き）
    /// </summary>
    private void ApplySlow()
    {
        _slowTimer = GameConfig.SLOW_DURATION;
        _stateMachine.AddStatusEffect(StatusEffect.Slow);

        Debug.Log($"[StatusEffect] {gameObject.name}: 鈍足付与（{GameConfig.SLOW_DURATION}秒）");
    }

    /// <summary>
    /// 鈍足の tick 処理
    /// タイマー満了で解除
    /// 移動速度制限・ジャンプ不可は PlayerMovement 側で Slow フラグを参照して適用
    /// </summary>
    private void UpdateSlow()
    {
        if (_slowTimer <= 0f) return;

        _slowTimer -= GameConfig.FIXED_DELTA_TIME;

        if (_slowTimer <= 0f)
        {
            _slowTimer = 0f;
            _stateMachine.RemoveStatusEffect(StatusEffect.Slow);
            Debug.Log($"[StatusEffect] {gameObject.name}: 鈍足解除（時間切れ）");
        }
    }

    // ============================================================
    // 凍結（氷属性）
    // ============================================================

    /// <summary>
    /// 凍結を確率で付与する。成功なら Freeze ステートへ強制遷移
    /// 凍結のタイマー管理は CharacterStateMachine が行う（FREEZE_DURATION で自動解除→Idle）
    /// </summary>
    private void TryApplyFreeze()
    {
        // 既に凍結中なら無視
        if (_stateMachine.CurrentState == CharacterState.Freeze) return;

        // 確率判定
        if (Random.value > GameConfig.FREEZE_PROBABILITY) return;

        // Freeze ステートへ強制遷移（CharacterStateMachine のタイマーで FREEZE_DURATION 後に Idle へ）
        _stateMachine.ForceState(CharacterState.Freeze);

        Debug.Log($"[StatusEffect] {gameObject.name}: 凍結発動！（{GameConfig.FREEZE_DURATION}秒）");
    }

    // ============================================================
    // 感電（雷属性）
    // ============================================================

    /// <summary>
    /// 感電を付与する。既に感電中の場合はタイマーをリセット（上書き）
    /// 感電中は受け身が取れなくなる（ReactionSystem が IsElectrified を参照）
    /// </summary>
    private void ApplyElectrified()
    {
        _electrifiedTimer = GameConfig.ELECTRIFIED_DURATION;
        _electrifiedComboCount = 0;
        _stateMachine.AddStatusEffect(StatusEffect.Electrified);

        Debug.Log($"[StatusEffect] {gameObject.name}: 感電付与（受け身不可）");
    }

    /// <summary>
    /// 感電の tick 処理
    /// 攻撃を受けなければ自然解除タイマーで解除
    /// （攻撃を受けるたびに OnElectrifiedHit でタイマーリセットされる）
    /// </summary>
    private void UpdateElectrified()
    {
        if (_electrifiedTimer <= 0f) return;

        _electrifiedTimer -= GameConfig.FIXED_DELTA_TIME;

        if (_electrifiedTimer <= 0f)
        {
            ClearElectrified();
            Debug.Log($"[StatusEffect] {gameObject.name}: 感電解除（自然解除）");
        }
    }

    /// <summary>感電状態をクリアする（内部ヘルパー）</summary>
    private void ClearElectrified()
    {
        _electrifiedTimer = 0f;
        _electrifiedComboCount = 0;
        _stateMachine.RemoveStatusEffect(StatusEffect.Electrified);
    }

    // ============================================================
    // 気絶（雷属性・地上のみ）
    // ============================================================

    /// <summary>
    /// 気絶を付与する。Stun ステートへ強制遷移
    /// 気絶のタイマー管理は CharacterStateMachine が行う（STUN_DURATION で自動解除→Idle）
    /// 地上で雷属性を受けた時のみ発動（空中では気絶しない）
    /// </summary>
    private void ApplyStun()
    {
        // 既に気絶中なら無視
        if (_stateMachine.CurrentState == CharacterState.Stun) return;

        // Stun ステートへ強制遷移（CharacterStateMachine のタイマーで STUN_DURATION 後に Idle へ）
        _stateMachine.ForceState(CharacterState.Stun);

        Debug.Log($"[StatusEffect] {gameObject.name}: 気絶発動！（{GameConfig.STUN_DURATION}秒）");
    }

    // ============================================================
    // 外部からの強制解除（リスポーン時等）
    // ============================================================

    /// <summary>
    /// 全状態異常を解除する（サーバー専用）
    /// リスポーン時に SpawnManager から呼ばれる
    /// </summary>
    public void ClearAllEffects()
    {
        if (!IsServer) return;

        _burnTimer = 0f;
        _burnTickTimer = 0f;
        _slowTimer = 0f;
        _electrifiedTimer = 0f;
        _electrifiedComboCount = 0;

        _stateMachine.RemoveStatusEffect(StatusEffect.Burn);
        _stateMachine.RemoveStatusEffect(StatusEffect.Slow);
        _stateMachine.RemoveStatusEffect(StatusEffect.Electrified);
    }
}
