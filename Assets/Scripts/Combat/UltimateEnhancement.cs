using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 究極強化システム（サーバー権威型）
///
/// 連撃強化3回達成（Lv3）後、4回目以降の仙箪7個で発動する超強力バフ。
/// 30秒間の効果:
///   - ATK × 1.5
///   - DEF × 1.5
///   - 移動速度 × 1.2
///   - アーマーレベル +1
///
/// 死亡時・時間切れで効果解除。
/// 武器種ごとの個別効果はM6以降で実装（現在は全武器共通）。
/// </summary>
[RequireComponent(typeof(ArmorSystem))]
public class UltimateEnhancement : NetworkBehaviour
{
    // ============================================================
    // 同期変数
    // ============================================================

    /// <summary>究極強化発動中か</summary>
    private readonly NetworkVariable<bool> _isUltimateActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>究極強化の残り時間（秒）</summary>
    private readonly NetworkVariable<float> _ultimateRemainingTime = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>究極強化発動中か</summary>
    public bool IsUltimateActive => _isUltimateActive.Value;

    /// <summary>究極強化の残り時間（秒）</summary>
    public float UltimateRemainingTime => _ultimateRemainingTime.Value;

    /// <summary>究極強化中のATK倍率（非発動時は1.0）</summary>
    public float AtkMultiplier => _isUltimateActive.Value ? GameConfig.ULTIMATE_ATK_MULT : 1f;

    /// <summary>究極強化中のDEF倍率（非発動時は1.0）</summary>
    public float DefMultiplier => _isUltimateActive.Value ? GameConfig.ULTIMATE_DEF_MULT : 1f;

    /// <summary>究極強化中の移動速度倍率（非発動時は1.0）</summary>
    public float SpeedMultiplier => _isUltimateActive.Value ? GameConfig.ULTIMATE_SPEED_MULT : 1f;

    // ============================================================
    // サーバーローカル変数
    // ============================================================

    private ArmorSystem _armorSystem;
    private ArmorLevel _originalArmorLevel; // 発動前のアーマーレベル保存

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _armorSystem = GetComponent<ArmorSystem>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _isUltimateActive.Value = false;
            _ultimateRemainingTime.Value = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (!_isUltimateActive.Value) return;

        // 残り時間を減算
        _ultimateRemainingTime.Value -= GameConfig.FIXED_DELTA_TIME;

        if (_ultimateRemainingTime.Value <= 0f)
        {
            Deactivate();
        }
    }

    // ============================================================
    // 発動・解除（サーバー権威）
    // ============================================================

    /// <summary>
    /// 究極強化を発動する（サーバー専用。EnhancementRing から呼ばれる）
    /// </summary>
    public void Activate()
    {
        if (!IsServer) return;
        if (_isUltimateActive.Value) return; // 重複発動防止

        _isUltimateActive.Value = true;
        _ultimateRemainingTime.Value = GameConfig.ULTIMATE_DURATION;

        // アーマーレベル+1（上限はHyperArmor）
        if (_armorSystem != null)
        {
            _originalArmorLevel = _armorSystem.CurrentArmorLevel;
            ArmorLevel newLevel = (ArmorLevel)Mathf.Min(
                (int)_originalArmorLevel + 1,
                (int)ArmorLevel.HyperArmor
            );
            _armorSystem.SetArmorLevel(newLevel);
        }

        Debug.Log($"[Ultimate] {gameObject.name}: 究極強化発動！（{GameConfig.ULTIMATE_DURATION}秒）" +
                  $" ATK×{GameConfig.ULTIMATE_ATK_MULT} DEF×{GameConfig.ULTIMATE_DEF_MULT}" +
                  $" SPD×{GameConfig.ULTIMATE_SPEED_MULT}");
    }

    /// <summary>
    /// 究極強化を解除する（時間切れ・死亡時）
    /// </summary>
    public void Deactivate()
    {
        if (!IsServer) return;
        if (!_isUltimateActive.Value) return;

        _isUltimateActive.Value = false;
        _ultimateRemainingTime.Value = 0f;

        // アーマーレベルを元に戻す
        if (_armorSystem != null)
        {
            _armorSystem.SetArmorLevel(_originalArmorLevel);
        }

        Debug.Log($"[Ultimate] {gameObject.name}: 究極強化終了");
    }

    /// <summary>
    /// 死亡時のリセット（EnhancementRing.ResetAllEnhancements から呼ばれる）
    /// </summary>
    public void ResetOnDeath()
    {
        Deactivate();
    }
}
