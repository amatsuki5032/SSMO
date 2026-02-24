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

        // --- エボリューション攻撃 E6-E9 ---
        /// <summary>E6-E9 のモーション倍率（連撃Lv3+無双MAX時に解放）</summary>
        public float[] EvolutionMultipliers;
        /// <summary>E6-E9 の持続時間（秒）</summary>
        public float[] EvolutionDurations;
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

        // エボリューション E6-E9（大剣: 高威力フィニッシュ重視）
        EvolutionMultipliers = new[] { 1.3f, 1.5f, 1.7f, 2.0f },
        EvolutionDurations   = new[] { 0.5f, 0.5f, 0.55f, 0.8f }, // E9はC4モーション流用で長め
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

        // エボリューション E6-E9（双剣: 手数型・高速）
        EvolutionMultipliers = new[] { 0.9f, 1.0f, 1.2f, 1.5f },
        EvolutionDurations   = new[] { 0.35f, 0.35f, 0.35f, 0.6f },
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

        // エボリューション E6-E9（槍: リーチ突き連撃）
        EvolutionMultipliers = new[] { 1.1f, 1.3f, 1.5f, 1.8f },
        EvolutionDurations   = new[] { 0.45f, 0.45f, 0.5f, 0.7f },
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

        // エボリューション E6-E9（戟: 回転斬りフィニッシュ）
        EvolutionMultipliers = new[] { 1.2f, 1.4f, 1.6f, 1.9f },
        EvolutionDurations   = new[] { 0.45f, 0.45f, 0.5f, 0.75f },
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

        // エボリューション E6-E9（拳: 超高速ラッシュフィニッシュ）
        EvolutionMultipliers = new[] { 0.8f, 0.9f, 1.1f, 1.4f },
        EvolutionDurations   = new[] { 0.3f, 0.3f, 0.3f, 0.55f },
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

        // エボリューション E6-E9（弓: 近接フィニッシュ）
        EvolutionMultipliers = new[] { 1.1f, 1.3f, 1.5f, 1.8f },
        EvolutionDurations   = new[] { 0.5f, 0.5f, 0.55f, 0.75f },
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
    /// 武器種のE攻撃モーション倍率を返す（DamageCalculator 連携用）
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="evoStep">エボリューション段数（6-9）</param>
    /// <returns>モーション倍率。範囲外は 1.0f</returns>
    public static float GetEvolutionMultiplier(WeaponType type, int evoStep)
    {
        var p = GetWeaponParams(type);
        int idx = evoStep - 6; // E6=index0, E9=index3
        if (idx < 0 || p.EvolutionMultipliers == null || idx >= p.EvolutionMultipliers.Length) return 1.0f;
        return p.EvolutionMultipliers[idx];
    }

    /// <summary>
    /// 武器種のE攻撃持続時間を返す（ComboSystem 連携用）
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="evoStep">エボリューション段数（6-9）</param>
    /// <returns>持続時間（秒）。範囲外は 0.5f</returns>
    public static float GetEvolutionDuration(WeaponType type, int evoStep)
    {
        var p = GetWeaponParams(type);
        int idx = evoStep - 6;
        if (idx < 0 || p.EvolutionDurations == null || idx >= p.EvolutionDurations.Length) return 0.5f;
        return p.EvolutionDurations[idx];
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

    // ============================================================
    // ヒットボックスプロファイル（武器種別基本値）
    // ============================================================

    /// <summary>
    /// 武器種ごとのヒットボックス基本値
    /// N/C/D 攻撃の基本判定サイズ・フレーム情報を定義する
    /// 個別の step/chargeType に応じたスケーリングは生成メソッドで行う
    /// </summary>
    private struct HitboxProfile
    {
        // --- 通常攻撃 ---
        public float NormalRadius;      // N攻撃の基本判定半径
        public float NormalLength;      // N攻撃の基本判定長さ
        public int NormalStartFrame;    // N攻撃の基本アクティブ開始フレーム
        public int NormalActiveFrames;  // N攻撃の基本アクティブフレーム数

        // --- チャージ攻撃 ---
        public float ChargeRadius;      // C攻撃の基本判定半径
        public float ChargeLength;      // C攻撃の基本判定長さ
        public int ChargeStartFrame;    // C攻撃の基本アクティブ開始フレーム
        public int ChargeActiveFrames;  // C攻撃の基本アクティブフレーム数

        // --- ダッシュ攻撃 ---
        public float DashRadius;        // D攻撃の判定半径
        public float DashLength;        // D攻撃の判定長さ
        public int DashStartFrame;      // D攻撃のアクティブ開始フレーム
        public int DashActiveFrames;    // D攻撃のアクティブフレーム数
    }

    // 大剣: 広範囲・高威力・遅い
    private static readonly HitboxProfile _greatSwordHitbox = new()
    {
        NormalRadius = 0.5f, NormalLength = 1.5f, NormalStartFrame = 5, NormalActiveFrames = 10,
        ChargeRadius = 0.6f, ChargeLength = 2.0f, ChargeStartFrame = 8, ChargeActiveFrames = 12,
        DashRadius = 0.6f, DashLength = 2.0f, DashStartFrame = 5, DashActiveFrames = 13,
    };

    // 双剣: 狭範囲・高速・手数型
    private static readonly HitboxProfile _dualBladesHitbox = new()
    {
        NormalRadius = 0.3f, NormalLength = 0.8f, NormalStartFrame = 3, NormalActiveFrames = 6,
        ChargeRadius = 0.4f, ChargeLength = 1.0f, ChargeStartFrame = 5, ChargeActiveFrames = 8,
        DashRadius = 0.35f, DashLength = 1.0f, DashStartFrame = 3, DashActiveFrames = 10,
    };

    // 槍: 前方特化・リーチ最長・判定狭い
    private static readonly HitboxProfile _spearHitbox = new()
    {
        NormalRadius = 0.25f, NormalLength = 3.0f, NormalStartFrame = 5, NormalActiveFrames = 8,
        ChargeRadius = 0.3f, ChargeLength = 3.5f, ChargeStartFrame = 7, ChargeActiveFrames = 10,
        DashRadius = 0.3f, DashLength = 3.0f, DashStartFrame = 5, DashActiveFrames = 12,
    };

    // 戟: バランス型・回転斬り
    private static readonly HitboxProfile _halberdHitbox = new()
    {
        NormalRadius = 0.45f, NormalLength = 1.8f, NormalStartFrame = 5, NormalActiveFrames = 9,
        ChargeRadius = 0.55f, ChargeLength = 2.2f, ChargeStartFrame = 7, ChargeActiveFrames = 11,
        DashRadius = 0.5f, DashLength = 2.0f, DashStartFrame = 5, DashActiveFrames = 12,
    };

    // 拳: 超近距離・最速フレーム
    private static readonly HitboxProfile _fistsHitbox = new()
    {
        NormalRadius = 0.3f, NormalLength = 0.5f, NormalStartFrame = 2, NormalActiveFrames = 4,
        ChargeRadius = 0.35f, ChargeLength = 0.7f, ChargeStartFrame = 4, ChargeActiveFrames = 6,
        DashRadius = 0.3f, DashLength = 0.7f, DashStartFrame = 2, DashActiveFrames = 8,
    };

    // 弓: 近接は控えめ（遠距離は将来実装）
    private static readonly HitboxProfile _bowHitbox = new()
    {
        NormalRadius = 0.3f, NormalLength = 1.2f, NormalStartFrame = 5, NormalActiveFrames = 8,
        ChargeRadius = 0.35f, ChargeLength = 1.5f, ChargeStartFrame = 7, ChargeActiveFrames = 10,
        DashRadius = 0.35f, DashLength = 1.5f, DashStartFrame = 5, DashActiveFrames = 11,
    };

    /// <summary>武器種に対応するヒットボックスプロファイルを返す</summary>
    private static HitboxProfile GetHitboxProfile(WeaponType type)
    {
        return type switch
        {
            WeaponType.GreatSword => _greatSwordHitbox,
            WeaponType.DualBlades => _dualBladesHitbox,
            WeaponType.Spear => _spearHitbox,
            WeaponType.Halberd => _halberdHitbox,
            WeaponType.Fists => _fistsHitbox,
            WeaponType.Bow => _bowHitbox,
            _ => _greatSwordHitbox,
        };
    }

    // ============================================================
    // ヒットボックス生成メソッド（HitboxData 用）
    // ============================================================

    /// <summary>
    /// 武器種の通常攻撃ヒットボックスを生成する
    /// step が大きいほど範囲が広がり、アクティブフレームが遅く・長くなる
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="step">コンボ段数（1-6）</param>
    public static HitboxData GetNormalHitbox(WeaponType type, int step)
    {
        var p = GetHitboxProfile(type);
        // step ごとのスケーリング: 1段ごとに +10% 範囲, +1F 開始遅延, +2F 持続延長
        float scale = 1.0f + (step - 1) * 0.1f;
        int start = p.NormalStartFrame + (step - 1);
        int active = p.NormalActiveFrames + (step - 1) * 2;
        return new HitboxData
        {
            Radius = p.NormalRadius * scale,
            Length = p.NormalLength * scale,
            Offset = new UnityEngine.Vector3(0f, 1f, 0f),
            ActiveStartFrame = start,
            ActiveEndFrame = start + active,
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    /// <summary>
    /// 武器種のチャージ攻撃ヒットボックスを生成する
    /// chargeType ごとに特性が異なる（C3=小範囲ラッシュ、C4=広範囲吹き飛ばし、C6=最大範囲）
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="chargeType">チャージ技番号（1-6）</param>
    public static HitboxData GetChargeHitbox(WeaponType type, int chargeType)
    {
        var p = GetHitboxProfile(type);
        // C技ごとの範囲スケール（C3は小さめ、C4/C6は大きめ）
        float typeScale = chargeType switch
        {
            1 => 1.0f,  // C1: 基本
            2 => 1.1f,  // C2: やや広い（打ち上げ）
            3 => 0.8f,  // C3: ラッシュ初段なので小さめ
            4 => 1.4f,  // C4: 吹き飛ばし・広範囲
            5 => 1.5f,  // C5: かなり広い
            6 => 1.8f,  // C6: 最大範囲
            _ => 1.0f
        };
        int startOffset = chargeType switch
        {
            1 => 2,   // C1: やや遅め
            2 => 0,   // C2: 基本
            3 => -3,  // C3: 早め
            4 => 2,   // C4: 遅め
            5 => 0,   // C5: 基本
            6 => 4,   // C6: 最も遅い
            _ => 0
        };
        int start = p.ChargeStartFrame + startOffset;
        int active = (int)(p.ChargeActiveFrames * typeScale);
        return new HitboxData
        {
            Radius = p.ChargeRadius * typeScale,
            Length = p.ChargeLength * typeScale,
            Offset = new UnityEngine.Vector3(0f, chargeType == 2 ? 0.8f : 1f, 0f),
            ActiveStartFrame = start,
            ActiveEndFrame = start + active,
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    /// <summary>
    /// 武器種のC3ラッシュ（追加ヒット）ヒットボックスを生成する
    /// ラッシュヒットは即座にアクティブ（短い区間）
    /// </summary>
    public static HitboxData GetC3RushHitbox(WeaponType type)
    {
        var p = GetHitboxProfile(type);
        return new HitboxData
        {
            Radius = p.ChargeRadius * 0.8f,
            Length = p.ChargeLength * 0.8f,
            Offset = new UnityEngine.Vector3(0f, 1f, 0f),
            ActiveStartFrame = 2,
            ActiveEndFrame = 2 + (int)(p.ChargeActiveFrames * 0.5f),
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    /// <summary>
    /// 武器種のダッシュ攻撃ヒットボックスを生成する
    /// </summary>
    public static HitboxData GetDashHitbox(WeaponType type)
    {
        var p = GetHitboxProfile(type);
        return new HitboxData
        {
            Radius = p.DashRadius,
            Length = p.DashLength,
            Offset = new UnityEngine.Vector3(0f, 1f, 0f),
            ActiveStartFrame = p.DashStartFrame,
            ActiveEndFrame = p.DashStartFrame + p.DashActiveFrames,
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    /// <summary>
    /// 武器種のダッシュラッシュ（追加ヒット）ヒットボックスを生成する
    /// ラッシュヒットは即座にアクティブ（短い区間）
    /// </summary>
    public static HitboxData GetDashRushHitbox(WeaponType type)
    {
        var p = GetHitboxProfile(type);
        return new HitboxData
        {
            Radius = p.DashRadius,
            Length = p.DashLength,
            Offset = new UnityEngine.Vector3(0f, 1f, 0f),
            ActiveStartFrame = 2,
            ActiveEndFrame = 2 + (int)(p.DashActiveFrames * 0.5f),
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    /// <summary>
    /// 武器種のジャンプ攻撃ヒットボックスを生成する
    /// </summary>
    public static HitboxData GetJumpAttackHitbox(WeaponType type)
    {
        var p = GetHitboxProfile(type);
        return new HitboxData
        {
            Radius = p.NormalRadius * 0.9f,
            Length = p.NormalLength * 0.9f,
            Offset = new UnityEngine.Vector3(0f, 0.5f, 0f),
            ActiveStartFrame = p.NormalStartFrame,
            ActiveEndFrame = p.NormalStartFrame + p.NormalActiveFrames,
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    /// <summary>
    /// 武器種のエボリューション攻撃ヒットボックスを生成する
    /// E6=N1流用, E7=N2流用, E8=N3流用, E9=C4流用（フィニッシュ）
    /// チャージ攻撃レベルとして扱うため、チャージプロファイルベースでスケーリング
    /// </summary>
    /// <param name="type">武器種</param>
    /// <param name="evoStep">エボリューション段数（6-9）</param>
    public static HitboxData GetEvolutionHitbox(WeaponType type, int evoStep)
    {
        var p = GetHitboxProfile(type);
        // E6-E8: N攻撃モーション流用（段数ごとにスケーリング）
        // E9: C4モーション流用（フィニッシュ、広範囲）
        if (evoStep == 9)
        {
            // E9 = C4ヒットボックス流用
            return GetChargeHitbox(type, 4);
        }

        // E6=N1相当, E7=N2相当, E8=N3相当 だが、チャージ攻撃レベルなので少し広め
        int normalStep = evoStep - 5; // E6→1, E7→2, E8→3
        float scale = 1.0f + (normalStep - 1) * 0.1f;
        float evoScale = 1.15f; // チャージ攻撃レベルなので通常より15%広い
        int start = p.NormalStartFrame + (normalStep - 1);
        int active = p.NormalActiveFrames + (normalStep - 1) * 2;
        return new HitboxData
        {
            Radius = p.NormalRadius * scale * evoScale,
            Length = p.NormalLength * scale * evoScale,
            Offset = new UnityEngine.Vector3(0f, 1f, 0f),
            ActiveStartFrame = start,
            ActiveEndFrame = start + active,
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    // ============================================================
    // 刻印パラメータ（C1/C6 モーション変更）
    // ============================================================

    /// <summary>
    /// 刻印に応じた C1 の倍率を返す
    /// </summary>
    public static float GetInscriptionC1Multiplier(InscriptionType inscription)
    {
        return inscription switch
        {
            InscriptionType.Thrust => GameConfig.INSCRIPTION_C1_THRUST_MULT,
            InscriptionType.Formation => GameConfig.INSCRIPTION_C1_FORMATION_MULT,
            InscriptionType.Crush => GameConfig.INSCRIPTION_C1_CRUSH_MULT,
            InscriptionType.Shield => GameConfig.INSCRIPTION_C1_SHIELD_MULT,
            _ => GameConfig.INSCRIPTION_C1_THRUST_MULT,
        };
    }

    /// <summary>
    /// 刻印に応じた C1 の持続時間を返す
    /// </summary>
    public static float GetInscriptionC1Duration(InscriptionType inscription)
    {
        return inscription switch
        {
            InscriptionType.Thrust => GameConfig.INSCRIPTION_C1_THRUST_DURATION,
            InscriptionType.Formation => GameConfig.INSCRIPTION_C1_FORMATION_DURATION,
            InscriptionType.Crush => GameConfig.INSCRIPTION_C1_CRUSH_DURATION,
            InscriptionType.Shield => GameConfig.INSCRIPTION_C1_SHIELD_DURATION,
            _ => GameConfig.INSCRIPTION_C1_THRUST_DURATION,
        };
    }

    /// <summary>
    /// 刻印に応じた C6 の倍率を返す
    /// </summary>
    public static float GetInscriptionC6Multiplier(InscriptionType inscription)
    {
        return inscription switch
        {
            InscriptionType.Thrust => GameConfig.INSCRIPTION_C6_THRUST_MULT,
            InscriptionType.Formation => GameConfig.INSCRIPTION_C6_FORMATION_MULT,
            InscriptionType.Crush => GameConfig.INSCRIPTION_C6_CRUSH_MULT,
            InscriptionType.Shield => GameConfig.INSCRIPTION_C6_SHIELD_MULT,
            InscriptionType.Conquer => GameConfig.INSCRIPTION_C6_CONQUER_MULT,
            InscriptionType.Guard => GameConfig.INSCRIPTION_C6_GUARD_MULT,
            _ => GameConfig.INSCRIPTION_C6_THRUST_MULT,
        };
    }

    /// <summary>
    /// 刻印に応じた C6 の持続時間を返す
    /// </summary>
    public static float GetInscriptionC6Duration(InscriptionType inscription)
    {
        return inscription switch
        {
            InscriptionType.Thrust => GameConfig.INSCRIPTION_C6_THRUST_DURATION,
            InscriptionType.Formation => GameConfig.INSCRIPTION_C6_FORMATION_DURATION,
            InscriptionType.Crush => GameConfig.INSCRIPTION_C6_CRUSH_DURATION,
            InscriptionType.Shield => GameConfig.INSCRIPTION_C6_SHIELD_DURATION,
            InscriptionType.Conquer => GameConfig.INSCRIPTION_C6_CONQUER_DURATION,
            InscriptionType.Guard => GameConfig.INSCRIPTION_C6_GUARD_DURATION,
            _ => GameConfig.INSCRIPTION_C6_THRUST_DURATION,
        };
    }

    /// <summary>
    /// 刻印に応じた C1 ヒットボックスを生成する
    /// 刻印タイプごとに範囲特性が異なる
    /// </summary>
    public static HitboxData GetInscriptionC1Hitbox(WeaponType type, InscriptionType inscription)
    {
        var p = GetHitboxProfile(type);
        // 刻印ごとのスケール: 突=前方特化、陣=広範囲、砕=狭い高威力、盾=標準
        float radiusScale = inscription switch
        {
            InscriptionType.Thrust => 0.8f,     // 突: 狭め
            InscriptionType.Formation => 1.6f,   // 陣: 広範囲
            InscriptionType.Crush => 0.7f,       // 砕: 最も狭い
            InscriptionType.Shield => 1.0f,      // 盾: 標準
            _ => 1.0f,
        };
        float lengthScale = inscription switch
        {
            InscriptionType.Thrust => 1.5f,      // 突: 前方に長い
            InscriptionType.Formation => 1.2f,    // 陣: やや長め
            InscriptionType.Crush => 0.9f,        // 砕: 短い
            InscriptionType.Shield => 1.0f,       // 盾: 標準
            _ => 1.0f,
        };
        int start = p.ChargeStartFrame + 2; // C1は少し遅め
        int active = (int)(p.ChargeActiveFrames * 1.0f);
        return new HitboxData
        {
            Radius = p.ChargeRadius * radiusScale,
            Length = p.ChargeLength * lengthScale,
            Offset = new UnityEngine.Vector3(0f, 1f, 0f),
            ActiveStartFrame = start,
            ActiveEndFrame = start + active,
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    /// <summary>
    /// 刻印に応じた C6 ヒットボックスを生成する
    /// C6は最大級の攻撃なので全体的に大きめ
    /// </summary>
    public static HitboxData GetInscriptionC6Hitbox(WeaponType type, InscriptionType inscription)
    {
        var p = GetHitboxProfile(type);
        // 刻印ごとのスケール: 突=前方特化、陣=最大範囲、砕=高威力中範囲、盾=標準、覇=超範囲、衛=小さめ高速
        float radiusScale = inscription switch
        {
            InscriptionType.Thrust => 1.2f,
            InscriptionType.Formation => 2.0f,
            InscriptionType.Crush => 1.4f,
            InscriptionType.Shield => 1.5f,
            InscriptionType.Conquer => 2.2f,    // 覇: 最大範囲
            InscriptionType.Guard => 1.0f,      // 衛: コンパクト
            _ => 1.5f,
        };
        float lengthScale = inscription switch
        {
            InscriptionType.Thrust => 2.0f,
            InscriptionType.Formation => 1.5f,
            InscriptionType.Crush => 1.3f,
            InscriptionType.Shield => 1.5f,
            InscriptionType.Conquer => 1.8f,
            InscriptionType.Guard => 1.2f,
            _ => 1.5f,
        };
        int startOffset = inscription switch
        {
            InscriptionType.Guard => 2,     // 衛: 早め
            InscriptionType.Conquer => 6,   // 覇: 遅め
            InscriptionType.Crush => 5,     // 砕: やや遅め
            _ => 4,                         // 標準
        };
        int start = p.ChargeStartFrame + startOffset;
        int active = (int)(p.ChargeActiveFrames * 1.8f); // C6は長め
        return new HitboxData
        {
            Radius = p.ChargeRadius * radiusScale,
            Length = p.ChargeLength * lengthScale,
            Offset = new UnityEngine.Vector3(0f, 1f, 0f),
            ActiveStartFrame = start,
            ActiveEndFrame = start + active,
            MultiHit = false,
            MaxHitCount = 1
        };
    }

    /// <summary>
    /// 武器種のジャンプチャージヒットボックスを生成する
    /// </summary>
    public static HitboxData GetJumpChargeHitbox(WeaponType type)
    {
        var p = GetHitboxProfile(type);
        return new HitboxData
        {
            Radius = p.ChargeRadius * 1.2f,
            Length = p.ChargeLength * 1.2f,
            Offset = new UnityEngine.Vector3(0f, 0.5f, 0f),
            ActiveStartFrame = p.ChargeStartFrame,
            ActiveEndFrame = p.ChargeStartFrame + (int)(p.ChargeActiveFrames * 1.2f),
            MultiHit = false,
            MaxHitCount = 1
        };
    }
}
