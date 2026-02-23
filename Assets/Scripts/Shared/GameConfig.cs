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
    // ※ ヒットストップなし（常時戦闘が流れるスピード感を重視）
    public const float INPUT_BUFFER_SEC = 0.15f;     // 先行入力バッファ (150ms)
    // コンボ受付ウィンドウ: 各攻撃モーションの最後30%フレーム（モーション依存）

    // === ガード ===
    public const float GUARD_DAMAGE_REDUCTION = 0.8f;  // ガード時ダメージカット率（80%カット = ×0.2）
    public const float GUARD_ANGLE = 180f;             // ガード有効角度（正面180度）
    public const float EG_CHARGE_SEC = 1.0f;           // エレメンタルガード準備時間（△押し込み）
    public const float GUARD_MOVE_SPEED_MULTIPLIER = 0.5f; // ガード移動速度倍率（50%）

    // === ジャンプ ===
    public const float JUMP_FORCE = 8f;                // ジャンプ初速 (m/s)
    public const float JUMP_GRAVITY = -20f;            // ジャンプ中の重力（将来武器種で変動可能にするため GRAVITY と別管理）
    public const float JUMP_HEIGHT = 3f;               // 目標ジャンプ高さ (m)（参考値、武器種で変動）
    public const float JUMP_DURATION = 0.6f;           // 目標滞空時間 (秒)（参考値、武器種で変動）
    public const float JUMP_INVINCIBLE_SEC = 0.067f;   // ジャンプ離陸無敵 (4F@60Hz、仮値)

    // === 交戦距離 ===
    public const float MELEE_RANGE_MAX = 5f;         // 近接攻撃最大距離 (m)
    public const float RANGED_RANGE_MAX = 100f;      // 遠隔攻撃最大距離 (m)

    // === 無双ゲージ ===
    public const float MUSOU_GAUGE_MAX = 100f;
    public const float MUSOU_GAIN_ON_HIT = 2f;       // 攻撃ヒット時の獲得量（倍率依存、仮値）
    public const float MUSOU_GAIN_ON_DAMAGE = 5f;    // 被ダメージ時の獲得量（倍率依存、仮値）
    public const float MUSOU_DURATION_SEC = 4f;       // 無双乱舞持続時間
    // 無双ゲージ初期値: 戦闘開始時 0 / リスポーン時 MAX

    // === 根性補正（HP帯によるダメージ軽減）===
    public const float GUTS_BLUE_THRESHOLD = 0.5f;    // 青帯 (50-100%): ÷1
    public const float GUTS_YELLOW_THRESHOLD = 0.2f;  // 黄帯 (20-50%):  ÷1.5
    // 赤帯 (0-20%): ÷2
    public const float GUTS_YELLOW_DIVISOR = 1.5f;
    public const float GUTS_RED_DIVISOR = 2f;

    // === 空中補正 ===
    public const float AIR_DAMAGE_DIVISOR = 2f;       // 空中被弾ダメージ ÷2

    // === ダッシュ攻撃 ===
    public const float DASH_ATTACK_MOVE_TIME = 1.5f;  // ダッシュ攻撃発動に必要な連続移動時間 (秒、仮値)

    // === M1: Movement ===
    public const float MOVE_SPEED = 6f;               // 移動速度 (m/s)
    public const float ROTATION_SPEED = 720f;          // 回転速度 (deg/s)
    public const float GRAVITY = -20f;                 // 重力加速度 (m/s²)
    public const float GROUND_STICK_FORCE = -2f;       // 接地時の下向き力（斜面での浮き防止）

    // === M1: Prediction & Reconciliation ===
    public const int PREDICTION_BUFFER_SIZE = 1024;        // 予測用リングバッファサイズ（約17秒分 @60Hz）
    public const float RECONCILIATION_THRESHOLD = 0.01f;   // リコンシリエーション発動閾値 (m)

    // === M1-4: Interpolation（他プレイヤー補間表示）===
    public const float INTERPOLATION_DELAY = 0.1f;         // 補間遅延 (秒)。表示時刻を100ms遅らせる
    public const float SNAP_THRESHOLD = 5f;                // スナップ閾値 (m)。これ以上離れたら補間せず瞬間移動
    public const int INTERPOLATION_BUFFER_SIZE = 32;       // 補間用リングバッファサイズ（約1秒分 @30Hz）

    // === M2-1: 戦闘パラメータ ===

    // のけぞり持続時間（秒）
    public const float HITSTUN_DURATION = 0.4f;

    // ダウン持続時間（秒）
    public const float FACEDOWN_DOWN_DURATION = 0.8f;
    public const float CRUMBLE_DOWN_DURATION = 1.2f;
    public const float SPRAWL_DOWN_DURATION = 0.5f;

    // 起き上がりモーション時間（秒）
    public const float GETUP_DURATION = 0.5f;

    // 気絶持続時間（秒）
    public const float STUN_DURATION = 3.0f;

    // 凍結持続時間（秒）
    public const float FREEZE_DURATION = 2.0f;

    // 感電持続時間（攻撃なしの場合、秒）
    public const float ELECTRIFIED_DURATION = 2.0f;

    // 感電解除コンボ数
    public const int ELECTRIFIED_MAX_COMBO = 10;

    // ジャンプ離陸無敵フレーム数
    public const int JUMP_INVINCIBLE_FRAMES = 4;

    // 受け身後の無敵フレーム数
    public const int AIR_RECOVER_INVINCIBLE_FRAMES = 6;

    // 起き上がり中は全フレーム無敵（GETUP_DURATION 全体）

    // EG準備時間（秒）— EG_CHARGE_SEC と同値だが意味を明確にするため別名
    public const float EG_PREPARE_TIME = 1.0f;

    // コンボ受付ウィンドウ（モーション末尾の割合）
    public const float COMBO_WINDOW_RATIO = 0.3f;

    // === M2-3a: 通常攻撃コンボ ===
    public const int MAX_COMBO_STEP_BASE = 4;    // 無強化での最大コンボ段数（N1〜N4）

    // 各コンボ段の持続時間（秒）※仮値。将来アニメーションに合わせて調整
    public const float N1_DURATION = 0.5f;
    public const float N2_DURATION = 0.5f;
    public const float N3_DURATION = 0.55f;
    public const float N4_DURATION = 0.65f;

    // === M2-4a: チャージ攻撃 ===
    public const float C1_DURATION = 0.7f;           // C1 持続時間（仮値）
    public const float C2_DURATION = 0.6f;           // C2 打ち上げ
    public const float C3_DURATION = 0.5f;           // C3 ラッシュ初段
    public const float C3_RUSH_DURATION = 0.2f;      // C3 ラッシュ追加ヒット間隔
    public const int C3_RUSH_MAX_HITS = 8;           // C3 ラッシュ最大追加ヒット数
    public const float C4_DURATION = 0.8f;           // C4 吹き飛ばし
    public const float C5_DURATION = 0.7f;           // C5 チャージシュート
    public const float C6_DURATION = 1.0f;           // C6 最大技
}
