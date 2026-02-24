using UnityEngine;

/// <summary>
/// 攻撃ごとの Hitbox パラメータ
/// 武器種別のヒットボックスデータは WeaponData から取得する
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
    /// WeaponData のヒットボックスプロファイルから生成する
    /// </summary>
    public static HitboxData GetHitboxData(int comboStep, int chargeType, bool isDashAttacking, bool isRush, WeaponType weaponType = WeaponType.GreatSword, bool isEvolution = false)
    {
        if (isDashAttacking)
            return isRush ? WeaponData.GetDashRushHitbox(weaponType) : WeaponData.GetDashHitbox(weaponType);
        if (chargeType > 0)
            return (chargeType == 3 && isRush) ? WeaponData.GetC3RushHitbox(weaponType) : WeaponData.GetChargeHitbox(weaponType, chargeType);
        // エボリューション攻撃（E6-E9）
        if (isEvolution && comboStep >= 6)
            return WeaponData.GetEvolutionHitbox(weaponType, comboStep);
        if (comboStep > 0)
            return WeaponData.GetNormalHitbox(weaponType, comboStep);
        return default;
    }
}
