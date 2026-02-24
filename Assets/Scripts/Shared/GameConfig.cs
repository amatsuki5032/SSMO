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
    public const float RESPAWN_DELAY = 0f;            // リスポーン遅延（0 = 即復活）
    public const int MIN_PLAYERS_TO_START = 2;        // 試合開始に必要な最低人数（両チームに1人以上）
    public const int SPAWN_POINTS_PER_TEAM = 2;      // チームごとのスポーンポイント数

    // === マップ ===
    public const float MAP_SIZE = 100f;                  // マップサイズ (m)。100×100の正方形
    public const float MAP_HALF = MAP_SIZE / 2f;         // マップ半分 (端の座標)
    public const float WALL_HEIGHT = 10f;                // 外壁の高さ (m)
    public const float BASE_SIZE = 3f;                   // 拠点キューブのサイズ (m)

    // 拠点座標 5箇所（中央1 + 赤側2 + 青側2）
    // 赤軍: マップ西側 (X負方向)、青軍: マップ東側 (X正方向)
    public static readonly Vector3 BASE_POS_CENTER    = new Vector3(  0f, 0f,   0f);
    public static readonly Vector3 BASE_POS_RED_1     = new Vector3(-35f, 0f, -15f);
    public static readonly Vector3 BASE_POS_RED_2     = new Vector3(-35f, 0f,  15f);
    public static readonly Vector3 BASE_POS_BLUE_1    = new Vector3( 35f, 0f, -15f);
    public static readonly Vector3 BASE_POS_BLUE_2    = new Vector3( 35f, 0f,  15f);

    // 拠点座標配列（MapGenerator で一括参照用）
    public static readonly Vector3[] BASE_POSITIONS = new Vector3[]
    {
        BASE_POS_CENTER,
        BASE_POS_RED_1,
        BASE_POS_RED_2,
        BASE_POS_BLUE_1,
        BASE_POS_BLUE_2,
    };

    // === 拠点システム ===
    public const int BASE_COUNT = 5;                       // 拠点数
    public const float BASE_CAPTURE_TIME = 10f;            // 制圧に必要な秒数
    public const float BASE_CAPTURE_RADIUS = 5f;           // 制圧エリア半径 (m)
    public const float BASE_HP_REGEN_RATE = 20f;           // 味方拠点内HP回復量/秒

    // === スポーン座標（マップ配置に連動）===
    // 赤軍: 西側拠点の後方、青軍: 東側拠点の後方
    public static readonly Vector3 TEAM_RED_SPAWN_POS_1  = new Vector3(-40f, 1f, -15f);
    public static readonly Vector3 TEAM_RED_SPAWN_POS_2  = new Vector3(-40f, 1f,  15f);
    public static readonly Vector3 TEAM_BLUE_SPAWN_POS_1 = new Vector3( 40f, 1f, -15f);
    public static readonly Vector3 TEAM_BLUE_SPAWN_POS_2 = new Vector3( 40f, 1f,  15f);

    // === 戦闘 ===
    // ※ ヒットストップなし（常時戦闘が流れるスピード感を重視）
    public const float INPUT_BUFFER_SEC = 0.15f;     // 先行入力バッファ (150ms)
    // コンボ受付ウィンドウ: 各攻撃モーションの最後30%フレーム（モーション依存）

    // === ガード ===
    // ガード成功時はダメージ完全カット（0ダメージ）。崩し手段はめくり（背面攻撃）のみ
    public const float GUARD_ANGLE = 180f;             // ガード有効角度（正面180度）
    public const float EG_CHARGE_SEC = 1.0f;           // エレメンタルガード準備時間（△押し込み）
    public const float EG_MUSOU_DRAIN_RATE = 5f;         // EG維持中の無双ゲージ減少量/秒
    public const float EG_COUNTER_MUSOU_COST = 20f;      // EGカウンター発動時の無双ゲージ消費量
    public const float EG_COUNTER_KNOCKBACK = 8f;        // EGカウンター吹き飛ばし力
    public const float EG_COUNTER_DURATION = 0.5f;       // EGカウンター持続時間（秒）
    public const float GUARD_MOVE_SPEED_MULTIPLIER = 0.5f; // ガード移動速度倍率（50%）
    public const float GUARD_KNOCKBACK_DISTANCE = 0.1f;    // ガード成功時のノックバック距離 (m)（瞬間適用のため小さめ）

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
    public const float MUSOU_GAIN_ON_HIT = 3f;       // 攻撃ヒット時のゲージ増加
    public const float MUSOU_GAIN_ON_DAMAGE = 5f;    // 被ダメージ時のゲージ増加
    public const float MUSOU_CHARGE_RATE = 15f;       // ○長押しチャージ速度/秒
    public const float MUSOU_DURATION_SEC = 4f;       // 無双乱舞持続時間
    public const float TRUE_MUSOU_DURATION_SEC = 5f;  // 真・無双乱舞持続時間
    public const float TRUE_MUSOU_HP_THRESHOLD = 0.2f; // 真無双発動HP閾値（20%以下）
    // 無双ゲージ初期値: 戦闘開始時 0 / リスポーン時 MAX

    // === 根性補正（HP帯によるダメージ軽減）===
    public const float GUTS_BLUE_THRESHOLD = 0.5f;    // 青帯 (50-100%): ÷1
    public const float GUTS_YELLOW_THRESHOLD = 0.2f;  // 黄帯 (20-50%):  ÷1.5
    // 赤帯 (0-20%): ÷2
    public const float GUTS_YELLOW_DIVISOR = 1.5f;
    public const float GUTS_RED_DIVISOR = 2f;

    // === HP・ダメージ ===
    public const int DEFAULT_MAX_HP = 1000;            // デフォルト最大HP（仮値）
    public const int DEFAULT_ATK = 100;                // デフォルト攻撃力（仮値）
    public const int DEFAULT_DEF = 50;                 // デフォルト防御力（仮値）
    public const float CRITICAL_RATE = 0.05f;          // クリティカル率 5%
    public const float CRITICAL_MULTIPLIER = 1.5f;     // クリティカル倍率

    // === 属性倍率（1レベルあたりの倍率増分）===
    // 属性レベル別倍率: 1.0 + (perLv × level)
    public const float ELEMENT_FIRE_MULT_PER_LV    = 0.175f;  // 炎: Lv1=×1.175, Lv4=×1.70
    public const float ELEMENT_ICE_MULT_PER_LV     = 0.25f;   // 氷: Lv1=×1.25,  Lv4=×2.00
    public const float ELEMENT_THUNDER_MULT_PER_LV = 0.50f;   // 雷: Lv1=×1.50,  Lv4=×3.00
    public const float ELEMENT_WIND_MULT_PER_LV    = 0.50f;   // 風: Lv1=×1.50,  Lv4=×3.00
    // 斬属性: レベル別最低保証ダメージ（index=レベル。0は未使用）
    public static readonly int[] SLASH_MIN_DAMAGE = new int[] { 0, 10, 20, 30, 40 };

    // === 空中補正 ===
    public const float AIR_DAMAGE_DIVISOR = 2f;       // 空中被弾ダメージ ÷2

    // === ダッシュ攻撃 ===
    public const float DASH_ATTACK_MOVE_TIME = 1.5f;  // ダッシュ攻撃発動に必要な連続移動時間 (秒、仮値)
    public const float DASH_ATTACK_DURATION = 0.6f;   // D 持続時間（仮値）
    public const float DASH_RUSH_DURATION = 0.25f;    // Dラッシュ追加ヒット間隔
    public const int DASH_RUSH_MAX_HITS = 6;          // Dラッシュ最大追加ヒット数

    // === カメラ ===
    public const float CAMERA_DISTANCE = 3.0f;           // カメラ距離（後方 m）
    public const float CAMERA_HEIGHT = 2.0f;             // カメラ高さ（上方 m）
    public const float CAMERA_SENSITIVITY = 2.0f;        // マウス感度
    public const float CAMERA_MIN_PITCH = -10f;          // 垂直回転下限（度）
    public const float CAMERA_MAX_PITCH = 60f;           // 垂直回転上限（度）
    public const float CAMERA_COLLISION_RADIUS = 0.2f;   // 壁衝突検出の SphereCast 半径
    public const float CAMERA_MIN_DISTANCE = 0.5f;       // 壁衝突時の最小距離

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
    public const float HITSTUN_LIGHT_DURATION = 0.3f;    // のけぞり（軽）: N攻撃
    public const float HITSTUN_HEAVY_DURATION = 0.5f;    // のけぞり（重）: C1等

    // のけぞりノックバック
    public const float FLINCH_KNOCKBACK_DISTANCE = 0.5f; // のけぞり時の後方移動距離 (m)

    // 打ち上げ
    public const float LAUNCH_HEIGHT = 3.0f;             // 打ち上げ高さ (m)
    public const float LAUNCH_DURATION = 1.0f;           // 打ち上げ受け身不能時間 (秒)

    // 空中ヒット
    public const float AIR_HITSTUN_KNOCKBACK_H = 0.3f;   // 空中ヒット 水平ノックバック距離 (m)
    public const float AIR_HITSTUN_KNOCKBACK_V = 0.5f;   // 空中ヒット 上方ノックバック距離 (m)

    // 吹き飛ばし（C4等: 放物線で飛ぶ）
    public const float KNOCKBACK_DISTANCE_H = 4.0f;      // 吹き飛ばし 水平距離 (m)
    public const float KNOCKBACK_HEIGHT = 1.0f;           // 吹き飛ばし 上昇高さ (m)
    public const float KNOCKBACK_FORCE = 5f;             // 吹き飛ばし力 (m/s)（レガシー互換用）

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

    // === 燃焼（炎属性）===
    public const float BURN_DAMAGE_PER_SEC = 10f;       // 燃焼ダメージ/秒
    public const float BURN_TICK_INTERVAL = 0.5f;        // 燃焼ダメージ適用間隔（秒）
    public const float BURN_DURATION = 5f;               // 燃焼持続時間（秒）

    // === 鈍足（風属性）===
    public const float SLOW_DURATION = 5f;               // 鈍足持続時間（秒）
    public const float SLOW_SPEED_MULT = 0.5f;           // 鈍足時の移動速度倍率（50%）

    // ジャンプ離陸無敵フレーム数
    public const int JUMP_INVINCIBLE_FRAMES = 4;

    // 受け身後の無敵フレーム数
    public const int AIR_RECOVER_INVINCIBLE_FRAMES = 6;

    // 起き上がり中は全フレーム無敵（GETUP_DURATION 全体）

    // EG準備時間（秒）— EG_CHARGE_SEC と同値だが意味を明確にするため別名
    public const float EG_PREPARE_TIME = 1.0f;

    // コンボ受付ウィンドウ（モーション末尾の割合）
    public const float COMBO_WINDOW_RATIO = 0.3f;

    // === M2-5a: ヒット判定 ===
    public const float DEFAULT_HITBOX_RADIUS = 0.5f;     // デフォルト Hitbox 半径
    public const float DEFAULT_HITBOX_LENGTH = 1.5f;     // デフォルト Hitbox 長さ（カプセル）
    public const float DEFAULT_HURTBOX_RADIUS = 0.4f;    // デフォルト Hurtbox 半径
    public const float DEFAULT_HURTBOX_HEIGHT = 1.8f;    // デフォルト Hurtbox 高さ
    public const int MAX_HIT_TARGETS_PER_FRAME = 30;     // 1フレームあたり最大ヒット数

    // === M2-3a: 通常攻撃コンボ ===
    public const int MAX_COMBO_STEP_BASE = 4;    // 無強化での最大コンボ段数（N1〜N4）

    // 各コンボ段の持続時間（秒）— 大剣デフォルト値。武器種別は WeaponData.cs を参照
    public const float N1_DURATION = 0.5f;
    public const float N2_DURATION = 0.5f;
    public const float N3_DURATION = 0.55f;
    public const float N4_DURATION = 0.65f;

    // === M2-4a: チャージ攻撃 ===
    // 持続時間 — 大剣デフォルト値。武器種別は WeaponData.cs を参照
    public const float C1_DURATION = 0.7f;
    public const float C2_DURATION = 0.6f;
    public const float C3_DURATION = 0.5f;
    public const float C3_RUSH_DURATION = 0.2f;      // C3 ラッシュ追加ヒット間隔（全武器共通）
    public const int C3_RUSH_MAX_HITS = 8;           // C3 ラッシュ最大追加ヒット数（全武器共通）
    public const float C4_DURATION = 0.8f;
    public const float C5_DURATION = 0.7f;
    public const float C6_DURATION = 1.0f;

    // === NPC兵士 ===
    public const float NPC_SPAWN_INTERVAL = 5f;       // スポーン間隔（秒）
    public const int NPC_MAX_PER_BASE = 3;             // 各拠点からの最大スポーン数
    public const float NPC_MOVE_SPEED = 2f;            // NPC移動速度 (m/s)
    public const int NPC_HP = 100;                      // NPC HP
    public const int NPC_ATK = 20;                      // NPC攻撃力
    public const float NPC_SCALE = 0.6f;               // NPC表示スケール（プレイヤー比）
    public const float NPC_DESPAWN_DELAY = 1.5f;       // 死亡後デスポーンまでの遅延（秒）
    public const float NPC_SPAWN_OFFSET = 3f;          // 拠点中心からのスポーンオフセット（m）
    public const float NPC_DETECT_RANGE = 8f;          // 敵検出範囲（m）
    public const float NPC_ATTACK_RANGE = 1.5f;        // 攻撃範囲（m）
    public const float NPC_ATK_INTERVAL = 1.5f;        // 攻撃間隔（秒）
    public const float NPC_DETECT_INTERVAL = 0.167f;   // 敵スキャン間隔（秒。約10フレーム@60Hz）

    // === ミニマップ ===
    public const float MINIMAP_SIZE = 200f;              // ミニマップ表示サイズ (px)
    public const float MINIMAP_RANGE = 50f;              // ミニマップ表示範囲 (m、プレイヤー中心からの半径)

    // === M2-Visual-B: 攻撃前進距離 (m) ===
    // アクティブフレーム中にキャラ前方へ移動する合計距離
    public const float ADVANCE_N1 = 0.3f;
    public const float ADVANCE_N2 = 0.3f;
    public const float ADVANCE_N3 = 0.3f;
    public const float ADVANCE_N4 = 0.3f;
    public const float ADVANCE_C1 = 0.5f;
    public const float ADVANCE_C2 = 0.3f;
    public const float ADVANCE_C3_RUSH = 0.1f;       // C3 ラッシュ各ヒット
    public const float ADVANCE_C4 = 1.0f;
    public const float ADVANCE_C5 = 0.3f;
    public const float ADVANCE_DASH_ATTACK = 1.5f;
    public const float ADVANCE_MUSOU_HIT = 0.15f;    // 無双乱舞各ヒット
}
