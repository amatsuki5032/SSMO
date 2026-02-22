/// <summary>
/// キャラクターステート
/// サーバーが遷移を管理し、クライアントは予測遷移する
/// 詳細な遷移表は docs/combat-spec.md セクション22 を参照
/// </summary>
public enum CharacterState
{
    // --- 基本 ---
    Idle,           // 待機 → 全アクション入力受付
    Move,           // 移動中 → 攻撃・ガード・ジャンプ受付
    Jump,           // ジャンプ中 → JA/JC/ブレイク受付。方向転換不可
    JumpAttack,     // 空中攻撃中 (JA/JC)

    // --- 攻撃 ---
    Attack,         // 通常攻撃中 (N) → 次段□ / チャージ△ / ブレイクL2
    Charge,         // チャージ攻撃中 (C) → C3はラッシュ△連打
    DashAttack,     // ダッシュ攻撃 (D) → ラッシュ□ / ブレイクL2
    DashRush,       // ダッシュラッシュ → □連打で継続
    BreakCharge,    // ブレイクチャージ → 連続ブレイク可

    // --- 防御 ---
    Guard,          // ガード中 (L1) → 正面180度、ダメージ80%カット
    GuardMove,      // ガード移動 → 正面向いたまま移動
    EGCharging,     // EG準備中 → L1+△押しっぱなし（ガード有効のまま）
    EGReady,        // EG完成 → カウンター待機（無双ゲージ徐々に消費）
    EGCounter,      // EGカウンター発動 → 属性別反撃

    // --- 無双 ---
    MusouCharge,    // 無双チャージ → ○長押しでゲージ溜め（移動不可・ガードなし）
    Musou,          // 無双乱舞 → 無敵・入力不可（ガード可能な攻撃）
    TrueMusou,      // 真・無双乱舞 → HP赤(20%以下)で発動、炎属性付与

    // --- 被弾 ---
    Hitstun,        // のけぞり → 行動不能（無双で脱出可）
    Launch,         // 打ち上げ → 受け身不能時間中は行動不能
    AirHitstun,     // 空中被弾 → 追撃中（空中補正÷2）
    AirRecover,     // 空中受け身 → ×で脱出（短い無敵）
    Slam,           // 叩きつけ → SprawlDown へ遷移

    // --- ダウン（4種）---
    FaceDownDown,   // 前のめりダウン → 追撃でのけぞり（地上ハメルート）
    CrumbleDown,    // 崩れ落ちダウン（長い）→ 追撃で浮く
    SprawlDown,     // 仰向けダウン（短い）→ 追撃で浮く
    Stun,           // 気絶（地上のみ約3秒）→ 追撃で浮く

    // --- 復帰 ---
    Getup,          // 起き上がり → 必ず発生、無敵

    // --- 状態異常（他ステートと重複可能）---
    Freeze,         // 凍結 (氷) → 約2秒行動不能
    // Electrified / Burn / Slow はフラグで管理（ステートと共存するため）

    // --- 死亡 ---
    Dead,           // 死亡 → 即リスポーン（交互拠点制限あり）
}

/// <summary>
/// 被弾リアクション種別
/// サーバーがヒット判定時に決定する
/// アーマー段階と攻撃レベルの比較でのけぞり有無を判定
/// </summary>
public enum HitReaction
{
    None,
    Flinch,         // のけぞり（通常攻撃）
    Launch,         // 打ち上げ (C2/C5 系)
    Slam,           // 叩きつけ（空中から地面へ）
    Knockback,      // 吹き飛ばし (C4 系、巻き込みあり)
    FaceDown,       // 前のめりダウン
    Crumble,        // 崩れ落ちダウン（炎燃焼EG等）
    Stun,           // 気絶（地上で雷属性ヒット時）
}

/// <summary>
/// 状態異常フラグ（ステートと共存するため別管理）
/// </summary>
[System.Flags]
public enum StatusEffect
{
    None        = 0,
    Burn        = 1 << 0,  // 燃焼 (炎) - 持続ダメージ、HP0にはしない
    Freeze      = 1 << 1,  // 凍結 (氷) - 確率発動、約2秒行動不能
    Electrified = 1 << 2,  // 感電 (雷) - 受け身不可
    Slow        = 1 << 3,  // 鈍足 (風) - 移動低下+ジャンプ不可
}

/// <summary>
/// アーマー段階（のけぞり耐性）
/// ダメージは常に通る。アーマーはのけぞり無効化のみ
/// </summary>
public enum ArmorLevel
{
    Normal = 1,     // 通常（全てのけぞる）
    Arrow = 2,      // 矢耐性（雑魚の矢でのけぞらない）
    NAttack = 3,    // N耐性（通常攻撃でのけぞらない）
    Super = 4,      // SA（チャージでものけぞらない）
    Hyper = 5,      // HA（無双でものけぞらない）
}

/// <summary>
/// 攻撃レベル（のけぞらせる力）
/// 攻撃レベル > アーマー段階 → のけぞる
/// </summary>
public enum AttackLevel
{
    Arrow = 1,      // 雑魚の矢
    Normal = 2,     // 通常攻撃 (N)
    Charge = 3,     // チャージ攻撃 (C) / エボリューション (E)
    Musou = 4,      // 無双乱舞
}

/// <summary>
/// 属性種別（5種 + 無属性）
/// 属性同士の相性なし。チャージ攻撃にのみ属性が乗る
/// </summary>
public enum ElementType
{
    None,       // 無属性
    Fire,       // 炎 - 燃焼（持続ダメ）
    Ice,        // 氷 - 凍結（行動不能）
    Thunder,    // 雷 - 感電+気絶（高火力+拘束）
    Wind,       // 風 - 鈍足（機動力奪取）
    Slash,      // 斬 - 防御無視+HP&無双ダメ（諸刃）
}

/// <summary>
/// チーム識別
/// </summary>
public enum Team
{
    Red,    // 赤軍
    Blue,   // 青軍
}

/// <summary>
/// 武器種
/// </summary>
public enum WeaponType
{
    GreatSword,  // 大剣 - 広範囲・高威力・遅い
    DualBlades,  // 双剣 - 手数型・連撃コンボ
    Spear,       // 槍   - リーチ長・突き特化
    Halberd,     // 戟   - 打ち上げ・回転斬り
    Fists,       // 拳   - 超近距離ラッシュ
    Bow,         // 弓   - 遠距離射撃・牽制
}
