/// <summary>
/// キャラクターステート
/// サーバーが遷移を管理し、クライアントは予測遷移する
/// </summary>
public enum CharacterState
{
    Idle,        // 待機 → 全アクション入力受付
    Move,        // 移動中 → 攻撃・ガード・回避受付
    Attack,      // 通常攻撃中 → 次段入力 or チャージ受付
    Charge,      // チャージ攻撃中 → 一部キャンセル可
    Guard,       // ガード中 → 回避でキャンセル可
    Dash,        // 回避/ステップ中 → 無敵F→硬直
    Musou,       // 無双乱舞中 → 無敵・入力不可
    Awakening,   // 覚醒発動中 → 短い演出
    Hitstun,     // のけぞり/よろめき → 行動不能
    Launch,      // 打ち上げ空中状態 → 行動不能
    AirHitstun,  // 空中被弾 → 追撃を受けている
    Slam,        // 叩きつけ → ダウンへ遷移
    Down,        // ダウン状態 → 起き上がり無敵あり
    Dead,        // 死亡 → リスポーン待ち
}

/// <summary>
/// 被弾リアクション種別
/// サーバーがヒット判定時に決定する
/// </summary>
public enum HitReaction
{
    None,
    Flinch,      // のけぞり (軽攻撃)
    Stagger,     // よろめき (重攻撃)
    Launch,      // 打ち上げ (C2系)
    Slam,        // 叩きつけ (空中から地面へ)
    Knockback,   // 吹き飛ばし (C4系)
    Crumple,     // 崩れ落ち (C1/ガード崩し)
    Stun,        // 気絶 (投げ系)
}

/// <summary>
/// チーム識別
/// </summary>
public enum Team
{
    Red,   // 赤軍
    Blue,  // 青軍
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
    Fists,       // 拳   - 超近距離ラッシュ・投げ
    Bow,         // 弓   - 遠距離射撃・牽制
}
