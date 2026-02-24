/// <summary>
/// 武器種ごとのパラメータデータ（定数データ）
///
/// 各武器種のモーション倍率・持続時間・移動速度・射程等を一元管理する。
/// 定数データのため ScriptableObject は使わず static クラスで管理。
///
/// 参照: docs/shared/combat-spec.md セクション21「武器種」
///
/// 武器種特性:
///   大剣: 広範囲・高威力・遅い
///   双剣: 手数型・連撃コンボ・速い
///   槍  : リーチ長・突き特化
///   戟  : 打ち上げ・回転斬り・バランス型
///   拳  : 超近距離ラッシュ・最速
///   弓  : 遠距離射撃・牽制
/// </summary>
public static class WeaponData
{
    // ============================================================
    // WeaponParams 構造体
    // ============================================================

    /// <summary>
    /// 武器種ごとのパラメータ一式
    /// </summary>
    public struct WeaponParams
    {
        // --- 移動 ---
        /// <summary>移動速度 (m/s)</summary>
        public float MoveSpeed;
        /// <summary>ダッシュ速度 (m/s)。通常は MoveSpeed と同じ（将来差別化可能）</summary>
        public float DashSpeed;

        // --- ジャンプ ---
        /// <summary>ジャンプ高さ (m)</summary>
        public float JumpHeight;
        /// <summary>滞空時間 (秒)</summary>
        public float AirTime;

        // --- 攻撃基本 ---
        /// <summary>基本リーチ (m)</summary>
        public float AttackRange;
        /// <summary>攻撃速度倍率（1.0 基準。大きいほど速い → 持続時間が短くなる）</summary>
        public float AttackSpeed;

        // --- 通常攻撃 N1-N6 ---
        /// <summary>N1-N6 のモーション倍率（ダメージ計算用）</summary>
        public float[] NormalMultipliers;
        /// <summary>N1-N6 の持続時間（秒）</summary>
        public float[] NormalDurations;

        // --- チャージ攻撃 C1-C6 ---
        /// <summary>C1-C6 のモーション倍率（ダメージ計算用）</summary>
        public float[] ChargeMultipliers;
        /// <summary>C1-C6 の持続時間（秒）</summary>
        public float[] ChargeDurations;

        // --- ダッシュ攻撃 ---
        /// <summary>ダッシュ攻撃のモーション倍率</summary>
        public float DashAttackMultiplier;
        /// <summary>ダッシュ攻撃の持続時間（秒）</summary>
        public float DashAttackDuration;

        // --- ジャンプ攻撃 ---
        /// <summary>ジャンプ攻撃 (JA) のモーション倍率</summary>
        public float JumpAttackMultiplier;
        /// <summary>ジャンプチャージ (JC) のモーション倍率</summary>
        public float JumpChargeMultiplier;
    }

    // ============================================================
    // 武器種パラメータ取得
    // ============================================================

    /// <summary>
    /// 武器種に対応するパラメータを返す
    /// </summary>
    public static WeaponParams GetWeaponParams(WeaponType type)
    {
        return type switch
        {
            WeaponType.GreatSword => GreatSword,
            WeaponType.DualBlades => DualBlades,
            WeaponType.Spear => Spear,
            WeaponType.Halberd => Halberd,
            WeaponType.Fists => Fists,
            WeaponType.Bow => Bow,
            _ => GreatSword,
        };
    }

    // ============================================================
    // 大剣（基準武器種。他はこの値を元に調整）
    // 射程3m, 速度遅, 威力★★★★★, ジャンプ低, 移動遅
    // ============================================================

    public static readonly WeaponParams GreatSword = new()
    {
        // 移動: 遅め
        MoveSpeed = 5.0f,
        DashSpeed = 5.0f,

        // ジャンプ: 低い
        JumpHeight = 2.5f,
        AirTime = 0.5f,

        // 攻撃基本: 広範囲・高威力・遅い
        AttackRange = 3.0f,
        AttackSpeed = 0.8f,

        // 通常攻撃 N1-N6（combat-spec セクション3 準拠）
        NormalMultipliers = new[] { 0.8f, 0.9f, 1.0f, 1.1f, 1.2f, 1.5f },
        NormalDurations   = new[] { 0.5f, 0.5f, 0.55f, 0.65f, 0.55f, 0.7f },

        // チャージ攻撃 C1-C6（combat-spec セクション4 準拠）
        ChargeMultipliers = new[] { 2.0f, 1.5f, 0.4f, 1.8f, 1.3f, 3.0f },
        ChargeDurations   = new[] { 0.7f, 0.6f, 0.5f, 0.8f, 0.7f, 1.0f },

        // ダッシュ攻撃
        DashAttackMultiplier = 1.2f,
        DashAttackDuration = 0.6f,

        // ジャンプ攻撃
        JumpAttackMultiplier = 0.8f,
        JumpChargeMultiplier = 1.5f,
    };

    // ============================================================
    // 双剣
    // 射程1.5m, 速度速, 威力★★, ジャンプ高, 移動速
    // ============================================================

    public static readonly WeaponParams DualBlades = new()
    {
        MoveSpeed = 7.0f,
        DashSpeed = 7.0f,

        JumpHeight = 3.5f,
        AirTime = 0.7f,

        AttackRange = 1.5f,
        AttackSpeed = 1.3f,

        // 手数型: 威力低め・持続短い
        NormalMultipliers = new[] { 0.5f, 0.5f, 0.6f, 0.6f, 0.7f, 0.9f },
        NormalDurations   = new[] { 0.35f, 0.35f, 0.35f, 0.4f, 0.35f, 0.5f },

        ChargeMultipliers = new[] { 1.2f, 1.0f, 0.3f, 1.2f, 0.9f, 2.0f },
        ChargeDurations   = new[] { 0.5f, 0.45f, 0.35f, 0.6f, 0.5f, 0.8f },

        DashAttackMultiplier = 0.8f,
        DashAttackDuration = 0.45f,

        JumpAttackMultiplier = 0.5f,
        JumpChargeMultiplier = 1.0f,
    };

    // ============================================================
    // 槍
    // 射程4.5m, 速度中, 威力★★★, ジャンプ中, 移動中
    // ============================================================

    public static readonly WeaponParams Spear = new()
    {
        MoveSpeed = 6.0f,
        DashSpeed = 6.0f,

        JumpHeight = 3.0f,
        AirTime = 0.6f,

        AttackRange = 4.5f,
        AttackSpeed = 1.0f,

        // リーチ長い分、威力は中程度
        NormalMultipliers = new[] { 0.7f, 0.8f, 0.8f, 0.9f, 1.0f, 1.2f },
        NormalDurations   = new[] { 0.45f, 0.45f, 0.5f, 0.55f, 0.5f, 0.6f },

        ChargeMultipliers = new[] { 1.6f, 1.3f, 0.35f, 1.5f, 1.1f, 2.5f },
        ChargeDurations   = new[] { 0.65f, 0.55f, 0.45f, 0.7f, 0.65f, 0.9f },

        DashAttackMultiplier = 1.0f,
        DashAttackDuration = 0.55f,

        JumpAttackMultiplier = 0.7f,
        JumpChargeMultiplier = 1.3f,
    };

    // ============================================================
    // 戟
    // 射程3.5m, 速度中, 威力★★★★, ジャンプ中, 移動中
    // ============================================================

    public static readonly WeaponParams Halberd = new()
    {
        MoveSpeed = 6.0f,
        DashSpeed = 6.0f,

        JumpHeight = 3.0f,
        AirTime = 0.6f,

        AttackRange = 3.5f,
        AttackSpeed = 1.0f,

        // 打ち上げ・回転斬り得意。威力やや高め
        NormalMultipliers = new[] { 0.7f, 0.8f, 0.9f, 1.0f, 1.1f, 1.3f },
        NormalDurations   = new[] { 0.45f, 0.45f, 0.5f, 0.6f, 0.5f, 0.65f },

        ChargeMultipliers = new[] { 1.8f, 1.4f, 0.35f, 1.6f, 1.2f, 2.8f },
        ChargeDurations   = new[] { 0.65f, 0.55f, 0.45f, 0.75f, 0.65f, 0.95f },

        DashAttackMultiplier = 1.1f,
        DashAttackDuration = 0.55f,

        JumpAttackMultiplier = 0.7f,
        JumpChargeMultiplier = 1.4f,
    };

    // ============================================================
    // 拳
    // 射程1m, 速度最速, 威力★★, ジャンプ最高, 移動最速
    // ============================================================

    public static readonly WeaponParams Fists = new()
    {
        MoveSpeed = 8.0f,
        DashSpeed = 8.0f,

        JumpHeight = 4.0f,
        AirTime = 0.8f,

        AttackRange = 1.0f,
        AttackSpeed = 1.5f,

        // 超高速ラッシュ: 威力低い・持続最短
        NormalMultipliers = new[] { 0.4f, 0.4f, 0.5f, 0.5f, 0.6f, 0.8f },
        NormalDurations   = new[] { 0.3f, 0.3f, 0.3f, 0.35f, 0.3f, 0.45f },

        ChargeMultipliers = new[] { 1.0f, 0.8f, 0.25f, 1.0f, 0.8f, 1.8f },
        ChargeDurations   = new[] { 0.45f, 0.4f, 0.3f, 0.55f, 0.45f, 0.7f },

        DashAttackMultiplier = 0.7f,
        DashAttackDuration = 0.4f,

        JumpAttackMultiplier = 0.5f,
        JumpChargeMultiplier = 0.9f,
    };

    // ============================================================
    // 弓
    // 射程100m, 速度遅, 威力★★★, ジャンプ低, 移動中
    // ============================================================

    public static readonly WeaponParams Bow = new()
    {
        MoveSpeed = 6.0f,
        DashSpeed = 6.0f,

        JumpHeight = 2.5f,
        AirTime = 0.5f,

        AttackRange = 100.0f, // 遠距離射撃
        AttackSpeed = 0.8f,

        // 近接モーションは遅めで中威力（主力は弓射撃）
        NormalMultipliers = new[] { 0.6f, 0.7f, 0.8f, 0.9f, 1.0f, 1.2f },
        NormalDurations   = new[] { 0.5f, 0.5f, 0.55f, 0.6f, 0.55f, 0.65f },

        ChargeMultipliers = new[] { 1.6f, 1.3f, 0.35f, 1.5f, 1.1f, 2.5f },
        ChargeDurations   = new[] { 0.7f, 0.6f, 0.5f, 0.75f, 0.65f, 0.95f },

        DashAttackMultiplier = 1.0f,
        DashAttackDuration = 0.55f,

        JumpAttackMultiplier = 0.7f,
        JumpChargeMultiplier = 1.2f,
    };

    // ============================================================
    // ヘルパーメソッド
    // ============================================================

    /// <summary>
    /// 武器種のN攻撃モーション倍率を返す（DamageCalculator 連携用）
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="step">コンボ段数（1-6）</param>
    /// <returns>モーション倍率。範囲外は 1.0f</returns>
    public static float GetNormalMultiplier(WeaponType type, int step)
    {
        var p = GetWeaponParams(type);
        int idx = step - 1;
        if (idx < 0 || idx >= p.NormalMultipliers.Length) return 1.0f;
        return p.NormalMultipliers[idx];
    }

    /// <summary>
    /// 武器種のN攻撃持続時間を返す（ComboSystem 連携用）
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="step">コンボ段数（1-6）</param>
    /// <returns>持続時間（秒）。範囲外は 0.5f</returns>
    public static float GetNormalDuration(WeaponType type, int step)
    {
        var p = GetWeaponParams(type);
        int idx = step - 1;
        if (idx < 0 || idx >= p.NormalDurations.Length) return 0.5f;
        return p.NormalDurations[idx];
    }

    /// <summary>
    /// 武器種のC攻撃モーション倍率を返す（DamageCalculator 連携用）
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="chargeType">チャージ技番号（1-6）</param>
    /// <returns>モーション倍率。範囲外は 1.0f</returns>
    public static float GetChargeMultiplier(WeaponType type, int chargeType)
    {
        var p = GetWeaponParams(type);
        int idx = chargeType - 1;
        if (idx < 0 || idx >= p.ChargeMultipliers.Length) return 1.0f;
        return p.ChargeMultipliers[idx];
    }

    /// <summary>
    /// 武器種のC攻撃持続時間を返す（ComboSystem 連携用）
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="chargeType">チャージ技番号（1-6）</param>
    /// <returns>持続時間（秒）。範囲外は 0.7f</returns>
    public static float GetChargeDuration(WeaponType type, int chargeType)
    {
        var p = GetWeaponParams(type);
        int idx = chargeType - 1;
        if (idx < 0 || idx >= p.ChargeDurations.Length) return 0.7f;
        return p.ChargeDurations[idx];
    }

    /// <summary>
    /// 武器種のダッシュ攻撃モーション倍率を返す
    /// </summary>
    public static float GetDashMultiplier(WeaponType type, bool isRush)
    {
        var p = GetWeaponParams(type);
        // ラッシュ追加ヒットは倍率半分
        return isRush ? p.DashAttackMultiplier * 0.5f : p.DashAttackMultiplier;
    }
}
