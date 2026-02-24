/// <summary>
/// キャラクターの行動ステート
/// ★ サーバーが最終遷移権限を持つ ★
/// byte で NetworkVariable の帯域を節約する
/// 詳細な遷移表は docs/combat-spec.md セクション22 を参照
/// </summary>
public enum CharacterState : byte
{
    // === 基本行動 ===
    Idle = 0,
    Move = 1,

    // === 攻撃 ===
    Attack = 10,        // 通常攻撃 N1〜N6（コンボ段数は別変数で管理）
    Charge = 11,        // チャージ攻撃 C1〜C6
    DashAttack = 12,    // ダッシュ攻撃
    DashRush = 13,      // ダッシュラッシュ（D→□連打）
    // Evolution = 14,  // M4: エボリューション E6〜E9
    // BreakCharge = 15,// M4: ブレイクチャージ

    // === ジャンプ ===
    Jump = 20,
    JumpAttack = 21,    // JA / JC

    // === 防御 ===
    Guard = 30,
    GuardMove = 31,
    EGPrepare = 32,     // EG準備中（ガードは有効）
    EGReady = 33,       // EG完成（カウンター待ち）
    EGCounter = 34,     // EGカウンター発動中

    // === 無双 ===
    MusouCharge = 40,   // ○長押しでゲージ溜め
    Musou = 41,         // 無双乱舞（無敵）
    TrueMusou = 42,     // 真・無双乱舞（HP赤時、無敵）
    // GekiMusou = 43,  // M4: 激・無双乱舞

    // === 被弾 ===
    Hitstun = 50,       // のけぞり（無双で脱出可）
    Launch = 51,        // 打ち上げられ中（空中、受け身不能）
    AirHitstun = 52,    // 空中追撃中
    AirRecover = 53,    // 受け身成功
    Slam = 54,          // 叩きつけ中

    // === ダウン ===
    FaceDownDown = 60,  // 前のめりダウン（追撃→のけぞり）
    CrumbleDown = 61,   // 崩れ落ちダウン（長、追撃→浮く）
    SprawlDown = 62,    // 仰向けダウン（短、追撃→浮く）
    Stun = 63,          // 気絶（地上のみ、約3秒）
    Getup = 64,         // 起き上がりモーション（無敵）

    // === 状態異常（重複可能なのでフラグで管理。ステートとしては凍結のみ）===
    Freeze = 70,        // 凍結（約2秒行動不能→解除モーション）

    // === 死亡 ===
    Dead = 80,
}

/// <summary>
/// 状態異常フラグ（ステートとは別にビットフラグで管理）
/// 感電・燃焼・鈍足は他ステートと共存するため、ステートではなくフラグ管理
/// </summary>
[System.Flags]
public enum StatusEffect : byte
{
    None        = 0,
    Electrified = 1 << 0,  // 感電: 受け身不可
    Burn        = 1 << 1,  // 燃焼: 持続ダメージ（HP0にはしない）
    Slow        = 1 << 2,  // 鈍足: 移動低下+ジャンプ不可
}

/// <summary>
/// 攻撃レベル（のけぞらせる力）
/// 攻撃レベル > アーマー段階 → のけぞる
/// </summary>
public enum AttackLevel : byte
{
    Arrow = 1,      // 雑魚の矢
    Normal = 2,     // 通常攻撃 (N)
    Charge = 3,     // チャージ攻撃 (C) / エボリューション (E)
    Musou = 4,      // 無双乱舞
}

/// <summary>
/// アーマー段階（のけぞり耐性）
/// ダメージは常に通る。アーマーはのけぞり無効化のみ
/// </summary>
public enum ArmorLevel : byte
{
    None = 1,            // 通常: 全てのけぞる
    ArrowResist = 2,     // 矢耐性
    NormalResist = 3,    // N耐性（特定モーション中）
    SuperArmor = 4,      // SA: チャージ耐性
    HyperArmor = 5,      // HA: 無双耐性
}

/// <summary>
/// ダウン種別
/// 種類によって起き上がり速度と追撃時のリアクションが異なる
/// </summary>
public enum DownType : byte
{
    FaceDown = 0,    // 前のめり: 追撃→のけぞり（地上ハメルート）
    Crumble = 1,     // 崩れ落ち: 追撃→浮く（長い）
    Sprawl = 2,      // 仰向け: 追撃→浮く（短い）
}

/// <summary>
/// 入力種別（CharacterStateMachine.CanAcceptInput で使用）
/// 各ステートがどの入力を受け付けるかの判定に使う
/// </summary>
public enum InputType : byte
{
    Move,
    NormalAttack,   // □
    ChargeAttack,   // △
    Jump,           // ×
    Musou,          // ○
    Guard,          // L1
    BreakCharge,    // L2（M4）
    Enhance,        // R1（M4）
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
/// 拠点の所属チーム状態
/// Neutral = どちらにも属していない、Red/Blue = 制圧済み
/// </summary>
public enum BaseStatus : byte
{
    Neutral = 0,
    Red = 1,
    Blue = 2,
}

/// <summary>
/// ゲームフェーズ（GameModeManager で使用）
/// </summary>
public enum GamePhase : byte
{
    WaitingForPlayers = 0,  // プレイヤー待ち
    InProgress = 1,         // 試合中
    GameOver = 2,           // 試合終了
}

/// <summary>
/// 刻印種別（C1/C6 モーション変更）
/// C1: 突/陣/砕/盾 の4種
/// C6: 突/陣/砕/盾/覇/衛 の6種
/// </summary>
public enum InscriptionType : byte
{
    Thrust,     // 突 - 突進系
    Formation,  // 陣 - 範囲系
    Crush,      // 砕 - 高威力単体
    Shield,     // 盾 - 防御付き攻撃
    Conquer,    // 覇 - C6専用・超範囲
    Guard,      // 衛 - C6専用・カウンター系
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
