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
    /// 現在の攻撃状態に応じた HitboxData を返す（武器種・刻印対応）
    /// WeaponData のヒットボックスプロファイルから生成する
    /// C1/C6は刻印に応じたヒットボックスを使用
    /// </summary>
    public static HitboxData GetHitboxData(int comboStep, int chargeType, bool isDashAttacking, bool isRush, WeaponType weaponType = WeaponType.GreatSword, bool isEvolution = false, InscriptionType c1Inscription = InscriptionType.Thrust, InscriptionType c6Inscription = InscriptionType.Thrust)
    {
        if (isDashAttacking)
            return isRush ? WeaponData.GetDashRushHitbox(weaponType) : WeaponData.GetDashHitbox(weaponType);
        if (chargeType > 0)
        {
            // C1: 刻印ヒットボックス使用
            if (chargeType == 1)
                return WeaponData.GetInscriptionC1Hitbox(weaponType, c1Inscription);
            // C6: 刻印ヒットボックス使用
            if (chargeType == 6)
                return WeaponData.GetInscriptionC6Hitbox(weaponType, c6Inscription);
            return (chargeType == 3 && isRush) ? WeaponData.GetC3RushHitbox(weaponType) : WeaponData.GetChargeHitbox(weaponType, chargeType);
        }
        // エボリューション攻撃（E6-E9）
        if (isEvolution && comboStep >= 6)
            return WeaponData.GetEvolutionHitbox(weaponType, comboStep);
        if (comboStep > 0)
            return WeaponData.GetNormalHitbox(weaponType, comboStep);
        return default;
    }
}
