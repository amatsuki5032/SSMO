using UnityEngine;

/// <summary>
/// ダメージ計算（★サーバー側のみで実行★）
/// クライアントの値は一切信用しない
///
/// 計算フロー（docs/combat-spec.md セクション16 準拠）:
/// 1. 攻撃倍率 = モーション倍率 × 属性倍率（チャージ攻撃のみ）
/// 2. 基礎ダメージ = ATK × 攻撃倍率
/// 3. 防御計算 = 基礎ダメージ × (100 / (100 + DEF))   ※斬属性は DEF=0
/// 4. 空中補正 = 空中被弾時 ÷2
/// 5. 根性補正 = HP帯による軽減
/// 6. ガード補正 = ガード時 ×0.2
/// 7. 斬保証 / 最低保証
/// 8. クリティカル判定
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// ダメージ計算結果
    /// 斬属性はHP・無双ゲージ両方にダメージを与えるため、分離して返す
    /// </summary>
    public struct DamageResult
    {
        public int HpDamage;        // HPダメージ
        public int MusouDamage;     // 無双ゲージダメージ（斬属性のみ、通常は0）
        public int AttackerMusouCost; // 攻撃側の無双ゲージ減少（斬属性のみ、通常は0）
        public bool IsCritical;     // クリティカルが発生したか
    }

    /// <summary>
    /// メインのダメージ計算
    /// </summary>
    /// <param name="attackerATK">攻撃者の攻撃力</param>
    /// <param name="motionMultiplier">モーション倍率 (N1=0.8, C6=3.0 等)</param>
    /// <param name="defenderDEF">被弾者の防御力</param>
    /// <param name="defenderHpRatio">被弾者の現在HP割合 (0.0〜1.0)</param>
    /// <param name="element">攻撃の属性（チャージ攻撃のみ乗る）</param>
    /// <param name="elementLevel">属性レベル (0〜4、0=属性なし)</param>
    /// <param name="isAirborne">被弾者が空中か（空中補正÷2）</param>
    /// <param name="isGuarding">被弾者がガード中か（ダメージ×0.2）</param>
    public static DamageResult Calculate(
        float attackerATK,
        float motionMultiplier,
        float defenderDEF,
        float defenderHpRatio,
        ElementType element = ElementType.None,
        int elementLevel = 0,
        bool isAirborne = false,
        bool isGuarding = false)
    {
        var result = new DamageResult();

        // --- 1. 攻撃倍率 ---
        float elementMultiplier = GetElementDamageMultiplier(element, elementLevel);
        float attackMultiplier = motionMultiplier * elementMultiplier;

        // --- 2. 基礎ダメージ ---
        float baseDamage = attackerATK * attackMultiplier;

        // --- 3. 防御計算 ---
        // 斬属性は防御力無視（DEF=0として計算）
        float effectiveDEF = (element == ElementType.Slash) ? 0f : defenderDEF;
        float damage = baseDamage * (100f / (100f + effectiveDEF));

        // --- 4. 空中補正 ---
        if (isAirborne)
        {
            damage /= GameConfig.AIR_DAMAGE_DIVISOR;
        }

        // --- 5. 根性補正（HP帯によるダメージ軽減）---
        damage /= GetGutsDivisor(defenderHpRatio);

        // --- 6. ガード補正 ---
        if (isGuarding)
        {
            damage *= (1f - GameConfig.GUARD_DAMAGE_REDUCTION);
        }

        // --- 7. 斬保証 ---
        if (element == ElementType.Slash)
        {
            float slashMinDamage = GetSlashMinDamage(elementLevel);
            damage = Mathf.Max(damage, slashMinDamage);
        }

        // --- 8. クリティカル判定 ---
        if (Random.value < GameConfig.CRITICAL_RATE)
        {
            damage *= GameConfig.CRITICAL_MULTIPLIER;
            result.IsCritical = true;
        }

        // --- 最低ダメージ保証 ---
        result.HpDamage = Mathf.Max(1, Mathf.RoundToInt(damage));

        // --- 斬属性: 無双ゲージにもダメージ + 攻撃側の無双も減少 ---
        if (element == ElementType.Slash)
        {
            result.MusouDamage = result.HpDamage;
            result.AttackerMusouCost = Mathf.RoundToInt(result.HpDamage * 0.5f); // 仮値
        }

        return result;
    }

    // ============================================================
    // モーション倍率
    // ============================================================

    /// <summary>
    /// 攻撃の種類に応じたモーション倍率を返す
    /// 将来は武器種ごとに異なるテーブルに拡張予定。現在は大剣ベースの仮値
    /// </summary>
    public static float GetMotionMultiplier(int comboStep, int chargeType, bool isDash, bool isRush)
    {
        // ダッシュ攻撃
        if (isDash)
            return isRush ? 0.6f : 1.2f;

        // チャージ攻撃（combat-spec.md セクション4 準拠）
        if (chargeType > 0)
        {
            return chargeType switch
            {
                1 => 2.0f,
                2 => 1.5f,
                3 => isRush ? 0.4f : 0.4f, // C3 ラッシュ: 各ヒット 0.4
                4 => 1.8f,
                5 => 1.3f,
                6 => 3.0f,
                _ => 1.0f,
            };
        }

        // 通常攻撃（combat-spec.md セクション3 準拠）
        return comboStep switch
        {
            1 => 0.8f,
            2 => 0.9f,
            3 => 1.0f,
            4 => 1.1f,
            5 => 1.2f,
            6 => 1.5f,
            _ => 1.0f,
        };
    }

    // ============================================================
    // 属性倍率
    // ============================================================

    /// <summary>
    /// 属性レベルに応じたダメージ倍率を返す
    /// 属性なし or レベル0 の場合は 1.0（等倍）
    ///
    /// 属性同士の相性はない。倍率は属性種別×レベルで決まる
    /// </summary>
    public static float GetElementDamageMultiplier(ElementType element, int level)
    {
        if (level <= 0) return 1.0f;

        // 各属性の1レベルあたりの倍率増分
        float perLevel = element switch
        {
            ElementType.Fire    => 0.175f,  // Lv1: ×1.175, Lv4: ×1.70
            ElementType.Ice     => 0.25f,   // Lv1: ×1.25,  Lv4: ×2.00
            ElementType.Thunder => 0.50f,   // Lv1: ×1.50,  Lv4: ×3.00
            ElementType.Wind    => 0.50f,   // Lv1: ×1.50,  Lv4: ×3.00
            ElementType.Slash   => 0f,      // 斬は倍率ではなく最低保証で処理
            _                   => 0f,
        };

        return 1.0f + perLevel * level;
    }

    // ============================================================
    // 根性補正
    // ============================================================

    /// <summary>
    /// HP帯による被ダメージ軽減除数を返す
    /// 青帯 (50-100%): ÷1 / 黄帯 (20-50%): ÷1.5 / 赤帯 (0-20%): ÷2
    /// HP が低いほど硬くなる設計（逆転要素）
    /// </summary>
    public static float GetGutsDivisor(float hpRatio)
    {
        if (hpRatio > GameConfig.GUTS_BLUE_THRESHOLD)
            return 1f;
        if (hpRatio > GameConfig.GUTS_YELLOW_THRESHOLD)
            return GameConfig.GUTS_YELLOW_DIVISOR;
        return GameConfig.GUTS_RED_DIVISOR;
    }

    // ============================================================
    // 斬属性最低保証
    // ============================================================

    /// <summary>
    /// 斬属性のレベル別最低保証ダメージ
    /// </summary>
    public static float GetSlashMinDamage(int level)
    {
        return level switch
        {
            1 => 10f,
            2 => 20f,
            3 => 30f,
            4 => 40f,
            _ => 0f,
        };
    }
}
