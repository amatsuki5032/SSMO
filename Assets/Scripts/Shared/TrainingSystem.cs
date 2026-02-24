/// <summary>
/// 鍛錬システム（ステータス振り分け計算ロジック）
///
/// 鍛錬ポイントを5つのステータスに振り分ける仕組み。
/// 三角数コスト: n段階目のコスト = n × (n+1) ÷ 2
/// → 低段階は安く、高段階ほどコスト急増でバランス調整
///
/// static クラス（計算ロジックのみ。状態保持はサーバー側で別途管理）
/// UIでの振り分け画面はM6、戦闘反映はM5で実装予定
/// </summary>
public static class TrainingSystem
{
    // ============================================================
    // データ構造
    // ============================================================

    /// <summary>
    /// 鍛錬振り分け構造体
    /// 各ステータスへの振り分け段階を保持する
    /// </summary>
    public struct TrainingAllocation
    {
        /// <summary>攻撃力段階</summary>
        public int AtkLevel;
        /// <summary>破壊力段階</summary>
        public int BreakLevel;
        /// <summary>防御力段階</summary>
        public int DefLevel;
        /// <summary>体力段階</summary>
        public int HpLevel;
        /// <summary>無双段階</summary>
        public int MusouLevel;
    }

    // ============================================================
    // コスト計算
    // ============================================================

    /// <summary>
    /// 指定段階までの累積コスト（三角数）を返す
    /// コスト = level × (level + 1) ÷ 2
    /// 例: Lv0=0, Lv1=1, Lv2=3, Lv3=6, Lv4=10, Lv5=15
    /// </summary>
    /// <param name="level">振り分け段階（0以上）</param>
    /// <returns>累積コスト。負値は0を返す</returns>
    public static int CalcCost(int level)
    {
        if (level <= 0) return 0;
        return level * (level + 1) / 2;
    }

    /// <summary>
    /// 振り分け全体の合計コストを返す
    /// 各ステータスの三角数コストの合計
    /// </summary>
    public static int CalcTotalCost(TrainingAllocation alloc)
    {
        return CalcCost(alloc.AtkLevel)
             + CalcCost(alloc.BreakLevel)
             + CalcCost(alloc.DefLevel)
             + CalcCost(alloc.HpLevel)
             + CalcCost(alloc.MusouLevel);
    }

    /// <summary>
    /// 振り分けが上限内かどうかを判定する
    /// 合計コストが maxPoints 以下、かつ各段階が0以上であること
    /// </summary>
    /// <param name="alloc">振り分け</param>
    /// <param name="maxPoints">鍛錬ポイント上限（武器種ごとに異なる）</param>
    /// <returns>有効な振り分けなら true</returns>
    public static bool IsValid(TrainingAllocation alloc, int maxPoints)
    {
        // 負値チェック
        if (alloc.AtkLevel < 0 || alloc.BreakLevel < 0 ||
            alloc.DefLevel < 0 || alloc.HpLevel < 0 || alloc.MusouLevel < 0)
            return false;

        return CalcTotalCost(alloc) <= maxPoints;
    }

    // ============================================================
    // ステータスボーナス計算
    // ============================================================

    /// <summary>
    /// 段階に応じたボーナス値を返す
    /// ボーナス = level × basePerLevel（線形）
    /// </summary>
    /// <param name="level">振り分け段階</param>
    /// <param name="basePerLevel">1段階あたりのボーナス値</param>
    /// <returns>ボーナス値。levelが0以下なら0</returns>
    public static float CalcStatBonus(int level, float basePerLevel)
    {
        if (level <= 0) return 0f;
        return level * basePerLevel;
    }

    /// <summary>
    /// 振り分けからATKボーナスを返す
    /// </summary>
    public static float GetAtkBonus(TrainingAllocation alloc)
    {
        return CalcStatBonus(alloc.AtkLevel, GameConfig.TRAINING_ATK_PER_LEVEL);
    }

    /// <summary>
    /// 振り分けからDEFボーナスを返す
    /// </summary>
    public static float GetDefBonus(TrainingAllocation alloc)
    {
        return CalcStatBonus(alloc.DefLevel, GameConfig.TRAINING_DEF_PER_LEVEL);
    }

    /// <summary>
    /// 振り分けからHPボーナスを返す
    /// </summary>
    public static float GetHpBonus(TrainingAllocation alloc)
    {
        return CalcStatBonus(alloc.HpLevel, GameConfig.TRAINING_HP_PER_LEVEL);
    }

    /// <summary>
    /// 振り分けから無双ゲージボーナスを返す
    /// </summary>
    public static float GetMusouBonus(TrainingAllocation alloc)
    {
        return CalcStatBonus(alloc.MusouLevel, GameConfig.TRAINING_MUSOU_PER_LEVEL);
    }

    /// <summary>
    /// 振り分けから破壊力ボーナスを返す
    /// </summary>
    public static float GetBreakBonus(TrainingAllocation alloc)
    {
        return CalcStatBonus(alloc.BreakLevel, GameConfig.TRAINING_BREAK_PER_LEVEL);
    }
}
