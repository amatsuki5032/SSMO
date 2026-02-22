using UnityEngine;

/// <summary>
/// ダメージ計算（サーバー側で実行）
/// クライアントの値は一切信用しない
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// 基本ダメージ計算
    /// </summary>
    /// <param name="attackerATK">攻撃者の攻撃力</param>
    /// <param name="motionMultiplier">モーション倍率 (N1=0.8, C6=3.0 等)</param>
    /// <param name="defenderDEF">被弾者の防御力</param>
    /// <param name="elementMultiplier">属性相性倍率 (有利=1.2, 不利=0.8, 等倍=1.0)</param>
    /// <param name="isGuarding">ガード中か</param>
    /// <param name="isJustGuard">ジャストガード成功か</param>
    /// <returns>最終ダメージ値</returns>
    public static int Calculate(
        float attackerATK,
        float motionMultiplier,
        float defenderDEF,
        float elementMultiplier = 1.0f,
        bool isGuarding = false,
        bool isJustGuard = false)
    {
        // 基礎ダメージ
        float baseDamage = attackerATK * motionMultiplier;

        // 属性補正
        baseDamage *= elementMultiplier;

        // 防御計算: ATK / (ATK + DEF) 式 → DEF が高いほど軽減
        float defenseMultiplier = 100f / (100f + defenderDEF);
        float damage = baseDamage * defenseMultiplier;

        // クリティカル判定 (5%確率で1.5倍)
        if (Random.value < 0.05f)
        {
            damage *= 1.5f;
        }

        // ガード補正
        if (isJustGuard)
        {
            damage = 0f;
        }
        else if (isGuarding)
        {
            damage *= (1f - GameConfig.GUARD_DAMAGE_REDUCTION);
        }

        // 最低ダメージ保証 (1)
        return Mathf.Max(1, Mathf.RoundToInt(damage));
    }

    /// <summary>
    /// 属性相性テーブル
    /// 火 > 風 > 雷 > 氷 > 火
    /// </summary>
    public static float GetElementMultiplier(ElementType attacker, ElementType defender)
    {
        if (attacker == ElementType.None || defender == ElementType.None)
            return 1.0f;

        // 有利属性
        if ((attacker == ElementType.Fire  && defender == ElementType.Wind) ||
            (attacker == ElementType.Wind  && defender == ElementType.Thunder) ||
            (attacker == ElementType.Thunder && defender == ElementType.Ice) ||
            (attacker == ElementType.Ice   && defender == ElementType.Fire))
        {
            return 1.2f;
        }

        // 不利属性
        if ((defender == ElementType.Fire  && attacker == ElementType.Wind) ||
            (defender == ElementType.Wind  && attacker == ElementType.Thunder) ||
            (defender == ElementType.Thunder && attacker == ElementType.Ice) ||
            (defender == ElementType.Ice   && attacker == ElementType.Fire))
        {
            return 0.8f;
        }

        // 同属性 or その他
        return 1.0f;
    }
}

/// <summary>
/// 属性タイプ
/// </summary>
public enum ElementType
{
    None,
    Fire,    // 火
    Ice,     // 氷
    Thunder, // 雷
    Wind     // 風
}
