using UnityEngine;

/// <summary>
/// 攻撃ごとの Hitbox パラメータ
/// 将来は武器種ごとに異なるテーブルに拡張。現在は共通の仮値
/// </summary>
public struct HitboxData
{
    public float Radius;           // 判定半径
    public float Length;           // 判定長さ（前方方向）
    public Vector3 Offset;        // キャラ中心からのオフセット（ローカル座標）
    public int ActiveStartFrame;  // アクティブ開始フレーム（0始まり）
    public int ActiveEndFrame;    // アクティブ終了フレーム
    public bool MultiHit;         // 多段ヒットか
    public int MaxHitCount;       // 多段の場合の最大ヒット数

    /// <summary>
    /// 現在の攻撃状態に応じた HitboxData を返す（武器種対応）
    /// 武器種のAttackRangeに基づきリーチをスケーリングする
    /// </summary>
    public static HitboxData GetHitboxData(int comboStep, int chargeType, bool isDashAttacking, bool isRush, WeaponType weaponType = WeaponType.GreatSword)
    {
        HitboxData hitbox;
        if (isDashAttacking)
            hitbox = isRush ? DashRushHitbox() : DashHitbox();
        else if (chargeType > 0)
            hitbox = (chargeType == 3 && isRush) ? C3RushHitbox() : ChargeHitbox(chargeType);
        else if (comboStep > 0)
            hitbox = NormalHitbox(comboStep);
        else
            return default;

        // 武器種のリーチに応じてスケーリング
        ApplyWeaponScale(ref hitbox, weaponType);
        return hitbox;
    }

    /// <summary>
    /// 武器種のAttackRangeに基づいてヒットボックスをスケーリングする
    /// 大剣（3.0m）を基準に、Length/Radius をリーチ比率で調整する
    /// 弓の100mは射撃用のため、近接スケールは最大2倍に制限
    /// </summary>
    private static void ApplyWeaponScale(ref HitboxData hitbox, WeaponType weaponType)
    {
        float baseRange = WeaponData.GreatSword.AttackRange; // 3.0m
        float weaponRange = WeaponData.GetWeaponParams(weaponType).AttackRange;
        float rangeScale = Mathf.Min(weaponRange / baseRange, 2.0f);

        hitbox.Length *= rangeScale;
        hitbox.Radius *= rangeScale;
    }

    // ============================================================
    // 通常攻撃（N1〜N4）
    // ============================================================

    private static HitboxData NormalHitbox(int step)
    {
        // 仮値: 武器種共通。将来はWeaponDataから取得
        return step switch
        {
            1 => new HitboxData
            {
                Radius = 0.5f, Length = 1.5f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 5, ActiveEndFrame = 15,
                MultiHit = false, MaxHitCount = 1
            },
            2 => new HitboxData
            {
                Radius = 0.5f, Length = 1.5f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 5, ActiveEndFrame = 15,
                MultiHit = false, MaxHitCount = 1
            },
            3 => new HitboxData
            {
                Radius = 0.6f, Length = 1.8f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 6, ActiveEndFrame = 18,
                MultiHit = false, MaxHitCount = 1
            },
            4 => new HitboxData
            {
                Radius = 0.6f, Length = 2.0f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 8, ActiveEndFrame = 22,
                MultiHit = false, MaxHitCount = 1
            },
            _ => default,
        };
    }

    // ============================================================
    // チャージ攻撃（C1〜C6）
    // ============================================================

    private static HitboxData ChargeHitbox(int chargeType)
    {
        return chargeType switch
        {
            1 => new HitboxData
            {
                Radius = 0.6f, Length = 1.8f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 10, ActiveEndFrame = 25,
                MultiHit = false, MaxHitCount = 1
            },
            2 => new HitboxData
            {
                Radius = 0.6f, Length = 2.0f,
                Offset = new Vector3(0f, 0.8f, 0f),
                ActiveStartFrame = 8, ActiveEndFrame = 20,
                MultiHit = false, MaxHitCount = 1
            },
            3 => new HitboxData
            {
                Radius = 0.5f, Length = 1.5f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 5, ActiveEndFrame = 15,
                MultiHit = false, MaxHitCount = 1
            },
            4 => new HitboxData
            {
                Radius = 0.7f, Length = 2.5f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 10, ActiveEndFrame = 28,
                MultiHit = false, MaxHitCount = 1
            },
            5 => new HitboxData
            {
                Radius = 0.8f, Length = 3.0f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 8, ActiveEndFrame = 25,
                MultiHit = false, MaxHitCount = 1
            },
            6 => new HitboxData
            {
                Radius = 1.0f, Length = 3.5f,
                Offset = new Vector3(0f, 1f, 0f),
                ActiveStartFrame = 12, ActiveEndFrame = 35,
                MultiHit = false, MaxHitCount = 1
            },
            _ => default,
        };
    }

    // ============================================================
    // C3 ラッシュ（追加ヒット）
    // ============================================================

    private static HitboxData C3RushHitbox()
    {
        // ラッシュヒットは即座にアクティブ（短い区間）
        return new HitboxData
        {
            Radius = 0.5f, Length = 1.5f,
            Offset = new Vector3(0f, 1f, 0f),
            ActiveStartFrame = 2, ActiveEndFrame = 8,
            MultiHit = false, MaxHitCount = 1
        };
    }

    // ============================================================
    // ダッシュ攻撃
    // ============================================================

    private static HitboxData DashHitbox()
    {
        return new HitboxData
        {
            Radius = 0.6f, Length = 2.0f,
            Offset = new Vector3(0f, 1f, 0f),
            ActiveStartFrame = 5, ActiveEndFrame = 18,
            MultiHit = false, MaxHitCount = 1
        };
    }

    // ============================================================
    // ダッシュラッシュ（追加ヒット）
    // ============================================================

    private static HitboxData DashRushHitbox()
    {
        return new HitboxData
        {
            Radius = 0.6f, Length = 2.0f,
            Offset = new Vector3(0f, 1f, 0f),
            ActiveStartFrame = 2, ActiveEndFrame = 10,
            MultiHit = false, MaxHitCount = 1
        };
    }
}
