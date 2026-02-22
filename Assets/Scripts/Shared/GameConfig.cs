using UnityEngine;

/// <summary>
/// ゲーム全体の定数・設定値
/// サーバーとクライアント両方で参照する共有データ
/// </summary>
public static class GameConfig
{
    // === ネットワーク ===
    public const int SERVER_TICK_RATE = 60;          // サーバーティックレート (Hz)
    public const int CLIENT_SEND_RATE = 30;          // クライアント→サーバー送信レート (Hz)
    public const int STATE_SYNC_RATE = 30;           // サーバー→クライアント配信レート (Hz)
    public const float FIXED_DELTA_TIME = 1f / SERVER_TICK_RATE; // = 0.01667秒

    // === ラグコンペンセーション ===
    public const float MAX_LAG_COMPENSATION_MS = 150f;   // 最大巻き戻し時間 (ms)
    public const int SNAPSHOT_BUFFER_SIZE = 128;          // 過去スナップショット保持数
    public const float INTERPOLATION_DELAY_MS = 100f;     // 他プレイヤー補間遅延 (ms)

    // === 対戦ルール ===
    public const int TEAM_SIZE = 4;                  // 1チームの人数
    public const int MAX_PLAYERS = TEAM_SIZE * 2;    // 最大プレイヤー数 (4v4)
    public const float MATCH_TIME_SECONDS = 300f;    // 試合時間 (5分)
    public const int RESPAWN_TIME_SECONDS = 5;       // リスポーン時間

    // === 戦闘 ===
    public const float HITSTOP_NORMAL_SEC = 0.05f;   // 通常ヒットストップ (3F@60Hz)
    public const float HITSTOP_HEAVY_SEC = 0.083f;   // 重攻撃ヒットストップ (5F@60Hz)
    public const float COMBO_INPUT_WINDOW_SEC = 0.25f; // コンボ入力受付ウィンドウ
    public const float INPUT_BUFFER_SEC = 0.15f;     // 先行入力バッファ

    // === ガード & 回避 ===
    public const float GUARD_DAMAGE_REDUCTION = 0.8f;  // ガード時ダメージカット率
    public const float JUST_GUARD_WINDOW_SEC = 0.2f;   // ジャストガード受付時間
    public const float DODGE_INVINCIBLE_SEC = 0.1f;    // 回避無敵フレーム (6F@60Hz)
    public const float DODGE_RECOVERY_SEC = 0.2f;      // 回避硬直時間
    public const int MAX_CONSECUTIVE_DODGES = 3;       // 連続回避上限

    // === 交戦距離 ===
    public const float MELEE_RANGE_MAX = 5f;         // 近接攻撃最大距離 (m)
    public const float RANGED_RANGE_MAX = 100f;      // 遠隔攻撃最大距離 (m)

    // === 無双ゲージ ===
    public const float MUSOU_GAUGE_MAX = 100f;
    public const float MUSOU_GAIN_ON_HIT = 2f;       // 攻撃ヒット時の獲得量
    public const float MUSOU_GAIN_ON_DAMAGE = 5f;    // 被ダメージ時の獲得量
    public const float MUSOU_DURATION_SEC = 4f;       // 無双乱舞持続時間
}
