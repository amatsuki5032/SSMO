using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 状態異常マネージャー（★サーバー権威★）
///
/// 状態異常（燃焼・鈍足・感電等）の付与・解除・tick処理を一元管理する。
/// CharacterStateMachine の StatusEffect フラグと連携して状態を同期する。
///
/// 燃焼: 持続ダメージ（HPを0にはしない）。地上行動可能になったら解除
/// 鈍足: 移動速度低下 + ジャンプ不可。時間経過で解除
/// 感電: 受け身不可（M4-2c で実装）
/// 凍結: ステート遷移で処理（CharacterStateMachine が管理）
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

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
        _healthSystem = GetComponent<HealthSystem>();
    }

    /// <summary>
    /// サーバーのみ: 毎 FixedUpdate で状態異常 tick 処理
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;

        UpdateBurn();
        UpdateSlow();
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
    public void ApplyElementEffect(ElementType element, int level)
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
            // Ice, Thunder は M4-2c で実装
            // Slash は状態異常なし（ダメージ計算で処理済み）
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

        _stateMachine.RemoveStatusEffect(StatusEffect.Burn);
        _stateMachine.RemoveStatusEffect(StatusEffect.Slow);
        _stateMachine.RemoveStatusEffect(StatusEffect.Electrified);
    }
}
