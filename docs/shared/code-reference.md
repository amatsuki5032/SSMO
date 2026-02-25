# SSMO コードリファレンス

> 自動生成ドキュメント。CLAUDE.md のファイル構成セクション順に記載。

---

## Character/

### CameraController.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `CameraController : MonoBehaviour`（ネットワーク同期なし） |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `float Yaw` | カメラの水平回転角（PlayerMovement がカメラ基準移動方向を計算するために使用） |
| `void Initialize(Transform)` | 追従対象を設定しカーソルロック |
| `bool IsDebugFreeCamera` | デバッグ俯瞰カメラが有効か（Editor限定） |

**デバッグ操作**: `T` キーで俯瞰フリーカメラ切替（Editor限定。旧 BackQuote から変更）

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。オーナー専用のローカルカメラ）

**依存（GetComponent）**

なし（PlayerMovement から動的に生成・Initialize される）

---

### PlayerMovement.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `PlayerMovement : NetworkBehaviour` |
| 実行順序 | `[DefaultExecutionOrder(-10)]` — ComboSystem より先に入力処理を実行 |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `bool IsDashing` | 連続移動時間が閾値超過でダッシュ状態か返す（プロパティ） |
| `WeaponType CurrentWeaponType` | 現在の武器種（読み取り専用プロパティ、NetworkVariable同期） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_netPosition` | `NetworkVariable<Vector3>` | サーバー権威の位置（他プレイヤー表示用） |
| `_netRotationY` | `NetworkVariable<float>` | サーバー権威のY回転（他プレイヤー表示用） |
| `_netWeaponType` | `NetworkVariable<WeaponType>` | プレイヤーの武器種（サーバー権威） |

**ServerRpc / ClientRpc**

| メソッド名 | 種別 | 説明 |
|-----------|------|------|
| `SubmitInputServerRpc(PlayerInput)` | ServerRpc | クライアント→サーバーへ統合入力送信 |
| `ConfirmStateClientRpc(uint, Vector3, float, float)` | ClientRpc | サーバー確定状態をオーナーに返送（リコンシリエーション用） |

**備考**: `OnNetworkSpawn()` でサーバー側が `SpawnManager.GetSpawnPosition()` を呼び、チーム別スポーン地点にテレポートする（CharacterController 一時無効化→position設定→再有効化）

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterController` | 物理移動 |
| `CharacterStateMachine` | ステート判定・遷移 |
| `ComboSystem` | 攻撃入力の委譲 |
| `EGSystem` | EG入力処理の委譲 |
| `MusouGauge` | 無双入力処理の委譲 |
| `EnhancementRing` | 仙箪強化リング発動の委譲 |
| `TeamManager.Instance` | 微弱ロックオン時の敵味方判定 |

---

### CharacterStateMachine.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `CharacterStateMachine : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `CharacterState CurrentState` | 現在のステート（読み取り専用プロパティ） |
| `StatusEffect CurrentStatusEffects` | 現在の状態異常フラグ（読み取り専用プロパティ） |
| `bool IsInvincible` | 現在無敵状態か |
| `event Action<CharacterState, CharacterState> OnStateChanged` | ステート変更イベント（旧, 新） |
| `bool TryChangeState(CharacterState)` | ステート遷移を試行する（サーバー側） |
| `void ForceState(CharacterState)` | バリデーションをスキップして強制遷移（サーバー側） |
| `bool CanAcceptInput(InputType)` | 現在のステートで入力が受付可能か判定 |
| `bool CanMove()` | 移動可能か簡易判定 |
| `void SetHitstunDuration(float)` | 次の Hitstun 遷移時の持続時間を設定 |
| `void AddStatusEffect(StatusEffect)` | 状態異常フラグを付与（サーバー側） |
| `void RemoveStatusEffect(StatusEffect)` | 状態異常フラグを解除（サーバー側） |
| `bool HasStatusEffect(StatusEffect)` | 指定の状態異常フラグがあるか判定 |
| `void SetSpawnInvincibility()` | スポーン無敵を設定する（リスポーン時。サーバー専用。独立タイマー3秒） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_state` | `NetworkVariable<CharacterState>` | 現在のキャラクターステート |
| `_statusEffects` | `NetworkVariable<StatusEffect>` | 状態異常ビットフラグ |

**ServerRpc / ClientRpc**

| メソッド名 | 種別 | 説明 |
|-----------|------|------|
| `RequestStateChangeServerRpc(CharacterState)` | ServerRpc | クライアントからステート遷移をリクエスト |

**依存（GetComponent）**

なし（自己完結）

---

## Shared/

### GameConfig.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `GameConfig`（static クラス） |

ゲーム全体の定数・設定値を定義する静的クラス。NetworkVariable / RPC / GetComponent なし。

**主要定数グループ**

| グループ | 代表的な定数 |
|---------|-------------|
| ネットワーク | `SERVER_TICK_RATE(60)`, `CLIENT_SEND_RATE(30)`, `FIXED_DELTA_TIME` |
| ラグコンペンセーション | `MAX_LAG_COMPENSATION_MS(150)`, `SNAPSHOT_BUFFER_SIZE(128)` |
| 対戦ルール | `TEAM_SIZE(4)`, `MAX_PLAYERS(8)`, `MATCH_TIME_SECONDS(300)`, `SPAWN_POINTS_PER_TEAM(2)`, `RESPAWN_DELAY(0)`, `MIN_PLAYERS_TO_START(2)` |
| マップ | `MAP_SIZE(100)`, `MAP_HALF(50)`, `WALL_HEIGHT(10)`, `BASE_SIZE(3)`, `BASE_POSITIONS[5]` (readonly Vector3[]) |
| スポーン座標 | `TEAM_RED_SPAWN_POS_1/2` (readonly Vector3), `TEAM_BLUE_SPAWN_POS_1/2` (readonly Vector3) |
| 戦闘 | `INPUT_BUFFER_SEC(0.15)`, `COMBO_WINDOW_RATIO(0.3)` |
| ガード | `GUARD_ANGLE(180)`, `EG_CHARGE_SEC(1.0)`, `GUARD_KNOCKBACK_DISTANCE(0.1)` |
| ジャンプ | `JUMP_FORCE(8)`, `JUMP_GRAVITY(-20)` |
| 無双ゲージ | `MUSOU_GAUGE_MAX(100)`, `MUSOU_DURATION_SEC(4)` |
| HP・ダメージ | `DEFAULT_MAX_HP(1000)`, `DEFAULT_ATK(100)`, `DEFAULT_DEF(50)` |
| コンボ | `MAX_COMBO_STEP_BASE(4)`, `N1〜N4_DURATION`, `C1〜C6_DURATION` |
| リアクション | `HITSTUN_LIGHT_DURATION(0.3)`, `LAUNCH_HEIGHT(3.0)`, `KNOCKBACK_DISTANCE_H(4.0)` |
| カメラ | `CAMERA_DISTANCE(3.0)`, `CAMERA_HEIGHT(2.0)`, `CAMERA_SENSITIVITY(2.0)`, `CAMERA_MIN/MAX_PITCH(-10/60)` |
| 予測・補間 | `PREDICTION_BUFFER_SIZE(1024)`, `INTERPOLATION_DELAY(0.1)` |
| 拠点システム | `BASE_COUNT(5)`, `BASE_CAPTURE_TIME(10)`, `BASE_CAPTURE_RADIUS(5)`, `BASE_HP_REGEN_RATE(20)` |
| NPC兵士 | `NPC_SPAWN_INTERVAL(5)`, `NPC_MAX_PER_BASE(3)`, `NPC_MOVE_SPEED(2)`, `NPC_HP(100)`, `NPC_ATK(20)`, `NPC_SCALE(0.6)`, `NPC_DESPAWN_DELAY(1.5)`, `NPC_SPAWN_OFFSET(3)`, `NPC_DETECT_RANGE(8)`, `NPC_ATTACK_RANGE(1.5)`, `NPC_ATK_INTERVAL(1.5)`, `NPC_DETECT_INTERVAL(0.167)` |
| ミニマップ | `MINIMAP_SIZE(200)`, `MINIMAP_RANGE(50)` |
| 属性倍率 | `ELEMENT_FIRE_MULT_PER_LV(0.175)`, `ELEMENT_ICE_MULT_PER_LV(0.25)`, `ELEMENT_THUNDER_MULT_PER_LV(0.50)`, `ELEMENT_WIND_MULT_PER_LV(0.50)`, `SLASH_MIN_DAMAGE[]` (readonly int[]{0,10,20,30,40}) |
| 連撃強化 | `MAX_COMBO_ENHANCE_LEVEL(3)` |
| 鍛錬 | `TRAINING_ATK_PER_LEVEL(5)`, `TRAINING_DEF_PER_LEVEL(5)`, `TRAINING_HP_PER_LEVEL(50)`, `TRAINING_MUSOU_PER_LEVEL(5)`, `TRAINING_BREAK_PER_LEVEL(5)`, `DEFAULT_TRAINING_MAX(24)` |
| 究極強化 | `ULTIMATE_DURATION(30)`, `ULTIMATE_ATK_MULT(1.5)`, `ULTIMATE_DEF_MULT(1.5)`, `ULTIMATE_SPEED_MULT(1.2)` |
| 刻印 | `INSCRIPTION_C1_THRUST/FORMATION/CRUSH/SHIELD_MULT/DURATION`, `INSCRIPTION_C6_THRUST/FORMATION/CRUSH/SHIELD/CONQUER/GUARD_MULT/DURATION` |
| 仙箪 | `SENTAN_DROP_RATE(1.0)`, `SENTAN_PICKUP_RADIUS(2.0)`, `SENTAN_LIFETIME(30)`, `SENTAN_REQUIRED_FOR_ENHANCE(7)` |
| 仙箪強化リング | `SENTAN_SLOTS(7)`, `RING_ROTATION_SPEED(2)`, `ATK_BUFF_PER_ENHANCE(0.1)`, `DEF_BUFF_PER_ENHANCE(0.1)` |
| ブレイクチャージ | `BREAK_RUSH_ATK_BONUS(0.1)`, `BREAK_RUSH_MAX_STACK(5)`, `BREAK_RUSH_WINDOW(2.0)` |
| 燃焼 | `BURN_DAMAGE_PER_SEC(10)`, `BURN_TICK_INTERVAL(0.5)`, `BURN_DURATION(5)` |
| 鈍足 | `SLOW_DURATION(5)`, `SLOW_SPEED_MULT(0.5)` |
| 凍結 | `FREEZE_PROBABILITY(0.3)`, `FREEZE_DURATION(2.0)` |
| 気絶 | `STUN_DURATION(3.0)` |
| 感電 | `ELECTRIFIED_DURATION(2.0)`, `ELECTRIFIED_MAX_COMBO(10)` |
| のけぞり物理 | `HITSTUN_DECEL_RATE(10)`, `SLAM_GRAVITY_MULTIPLIER(3)` |
| 無双前進 | `MUSOU_HIT_INTERVAL_FRAMES(6)` |
| ダッシュラッシュ | `DASH_RUSH_ADVANCE_RATIO(0.3)` |
| 斬属性 | `SLASH_ATTACKER_MUSOU_COST_RATIO(0.5)` |
| 拠点HP回復 | `BASE_HP_REGEN_INTERVAL(1)` |
| スポーン無敵 | `SPAWN_INVINCIBLE_SEC(3)` |
| NPC停止 | `NPC_STOP_DISTANCE_SQ(1)` |
| ブレイクチャージデフォルト | `DEFAULT_BREAK_CHARGE_DURATION(0.5)` |
| 微弱ロックオン | `SOFT_TARGET_RANGE(10)`, `SOFT_TARGET_ANGLE(90)` |
| 攻撃前進距離 | `ADVANCE_N1〜N4(0.3)`, `ADVANCE_C1(0.5)`, `ADVANCE_C4(1.0)`, `ADVANCE_DASH_ATTACK(1.5)`, `ADVANCE_MUSOU_HIT(0.15)` |

---

### DamageCalculator.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `DamageCalculator`（static クラス） |

**内部構造体**

| 名前 | 説明 |
|------|------|
| `DamageResult` | 計算結果（HpDamage, MusouDamage, AttackerMusouCost, IsCritical） |

**主要 public メソッド**

| 名前 | 説明 |
|------|------|
| `DamageResult Calculate(float, float, float, float, ElementType, int, bool)` | メインのダメージ計算（ATK×倍率→防御→空中→根性→斬保証→クリ） |
| `float GetMotionMultiplier(int, int, bool, bool, WeaponType, bool, InscriptionType, InscriptionType)` | 攻撃種別に応じたモーション倍率を返す（武器種・エボリューション・刻印対応） |
| `float GetElementDamageMultiplier(ElementType, int)` | 属性レベルに応じたダメージ倍率を返す |
| `float GetGutsDivisor(float)` | HP帯による根性補正除数を返す |
| `float GetSlashMinDamage(int)` | 斬属性のレベル別最低保証ダメージを返す |

NetworkVariable / RPC / GetComponent なし。

---

### WeaponData.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `WeaponData`（static クラス） |

**内部構造体**

| 名前 | 説明 |
|------|------|
| `WeaponParams` | 武器種パラメータ一式（MoveSpeed, JumpHeight, AttackRange, NormalMultipliers[], ChargeDurations[] 等） |

**主要 public メソッド / フィールド**

| 名前 | 説明 |
|------|------|
| `WeaponParams GetWeaponParams(WeaponType)` | 武器種に対応するパラメータを返す |
| `float GetNormalMultiplier(WeaponType, int)` | 武器種のN攻撃モーション倍率を返す |
| `float GetNormalDuration(WeaponType, int)` | 武器種のN攻撃持続時間を返す |
| `float GetChargeMultiplier(WeaponType, int)` | 武器種のC攻撃モーション倍率を返す |
| `float GetChargeDuration(WeaponType, int)` | 武器種のC攻撃持続時間を返す |
| `float GetDashMultiplier(WeaponType, bool)` | 武器種のダッシュ攻撃モーション倍率を返す |
| `HitboxData GetNormalHitbox(WeaponType, int)` | 武器種のN攻撃ヒットボックスを生成 |
| `HitboxData GetChargeHitbox(WeaponType, int)` | 武器種のC攻撃ヒットボックスを生成 |
| `HitboxData GetC3RushHitbox(WeaponType)` | 武器種のC3ラッシュヒットボックスを生成 |
| `HitboxData GetDashHitbox(WeaponType)` | 武器種のダッシュ攻撃ヒットボックスを生成 |
| `HitboxData GetDashRushHitbox(WeaponType)` | 武器種のダッシュラッシュヒットボックスを生成 |
| `HitboxData GetJumpAttackHitbox(WeaponType)` | 武器種のジャンプ攻撃ヒットボックスを生成 |
| `HitboxData GetJumpChargeHitbox(WeaponType)` | 武器種のジャンプチャージヒットボックスを生成 |
| `float GetEvolutionMultiplier(WeaponType, int)` | 武器種のE攻撃モーション倍率を返す（E6-E9） |
| `float GetEvolutionDuration(WeaponType, int)` | 武器種のE攻撃持続時間を返す（E6-E9） |
| `HitboxData GetEvolutionHitbox(WeaponType, int)` | 武器種のエボリューション攻撃ヒットボックスを生成（E6-E9） |
| `float GetInscriptionC1Multiplier(InscriptionType)` | 刻印C1のモーション倍率を返す |
| `float GetInscriptionC1Duration(InscriptionType)` | 刻印C1の持続時間を返す |
| `float GetInscriptionC6Multiplier(InscriptionType)` | 刻印C6のモーション倍率を返す |
| `float GetInscriptionC6Duration(InscriptionType)` | 刻印C6の持続時間を返す |
| `HitboxData GetInscriptionC1Hitbox(WeaponType, InscriptionType)` | 刻印C1のヒットボックスを生成 |
| `HitboxData GetInscriptionC6Hitbox(WeaponType, InscriptionType)` | 刻印C6のヒットボックスを生成 |
| `GreatSword` | 大剣パラメータ（static readonly） |
| `DualBlades` | 双剣パラメータ（static readonly） |
| `Spear` | 槍パラメータ（static readonly） |
| `Halberd` | 戟パラメータ（static readonly） |
| `Fists` | 拳パラメータ（static readonly） |
| `Bow` | 弓パラメータ（static readonly） |

NetworkVariable / RPC / GetComponent なし。

---

### TrainingSystem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `TrainingSystem`（static クラス） |

**内部構造体**

| 名前 | 説明 |
|------|------|
| `TrainingAllocation` | 鍛錬振り分け構造体（AtkLevel, BreakLevel, DefLevel, HpLevel, MusouLevel） |

**主要 public メソッド**

| 名前 | 説明 |
|------|------|
| `int CalcCost(int)` | 指定段階までの累積コスト（三角数: n×(n+1)÷2） |
| `int CalcTotalCost(TrainingAllocation)` | 振り分け全体の合計コスト |
| `bool IsValid(TrainingAllocation, int)` | 振り分けが上限内か判定 |
| `float CalcStatBonus(int, float)` | 段階に応じたボーナス値（level × basePerLevel） |
| `float GetAtkBonus(TrainingAllocation)` | ATKボーナスを返す |
| `float GetDefBonus(TrainingAllocation)` | DEFボーナスを返す |
| `float GetHpBonus(TrainingAllocation)` | HPボーナスを返す |
| `float GetMusouBonus(TrainingAllocation)` | 無双ゲージボーナスを返す |
| `float GetBreakBonus(TrainingAllocation)` | 破壊力ボーナスを返す |

NetworkVariable / RPC / GetComponent なし。

---

### CharacterState.cs

| 項目 | 内容 |
|------|------|
| ファイル種別 | enum 定義集（クラスなし） |

**定義一覧**

| enum 名 | 説明 |
|---------|------|
| `CharacterState : byte` | キャラクター行動ステート（Idle, Move, Attack, ... Dead） |
| `StatusEffect : byte` | 状態異常ビットフラグ（Electrified, Burn, Slow） |
| `AttackLevel : byte` | 攻撃レベル（Arrow=1, Normal=2, Charge=3, Musou=4） |
| `ArmorLevel : byte` | アーマー段階（None=1 〜 HyperArmor=5） |
| `DownType : byte` | ダウン種別（FaceDown, Crumble, Sprawl） |
| `InputType : byte` | 入力種別（Move, NormalAttack, ChargeAttack, Jump, Musou, Guard 等） |
| `HitReaction` | 被弾リアクション種別（Flinch, Launch, Slam, Knockback 等） |
| `ElementType` | 属性種別（None, Fire, Ice, Thunder, Wind, Slash） |
| `Team` | チーム識別（Red, Blue） |
| `BaseStatus : byte` | 拠点の所属チーム状態（Neutral=0, Red=1, Blue=2） |
| `GamePhase : byte` | ゲームフェーズ（WaitingForPlayers, InProgress, GameOver） |
| `WeaponType` | 武器種（GreatSword, DualBlades, Spear, Halberd, Fists, Bow） |
| `InscriptionType : byte` | 刻印種別（Thrust=突, Formation=陣, Crush=砕, Shield=盾, Conquer=覇, Guard=衛） |

---

### PlayerInput.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `PlayerInput`（struct, `INetworkSerializable`） |

**フィールド**

| 名前 | 型 | 説明 |
|------|-----|------|
| `MoveInput` | `Vector2` | 移動方向 (H, V) |
| `JumpPressed` | `bool` | ×ボタン（押した瞬間） |
| `GuardHeld` | `bool` | L1（押しっぱなし） |
| `AttackPressed` | `bool` | □（押した瞬間） |
| `ChargePressed` | `bool` | △（押した瞬間） |
| `ChargeHeld` | `bool` | △ 長押し（EG準備用） |
| `MusouPressed` | `bool` | ○（押した瞬間） |
| `MusouHeld` | `bool` | ○ 長押し（無双チャージ用） |
| `EnhancePressed` | `bool` | R1（Iキー）仙箪強化リング発動 |
| `BreakPressed` | `bool` | L2（Hキー）ブレイクチャージ（武器2攻撃） |
| `Tick` | `uint` | ティック番号 |

**主要 public メソッド**

| 名前 | 説明 |
|------|------|
| `NetworkSerialize<T>(BufferSerializer<T>)` | NGO シリアライズ実装 |

依存なし。

---

## Combat/

### ComboSystem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `ComboSystem : NetworkBehaviour` |
| 実行順序 | `[DefaultExecutionOrder(10)]` — PlayerMovement の入力処理後にタイマー更新 |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `int ComboStep` | 現在のコンボ段数（0=非攻撃、プロパティ） |
| `int AttackSequence` | 攻撃セグメント番号（HitboxSystem用。新攻撃ごとにインクリメント） |
| `int ChargeType` | 現在のチャージ技番号（0=非チャージ、1=C1…） |
| `float SegmentElapsed` | 現在の攻撃セグメント経過時間（HitboxSystem用） |
| `bool IsDashAttacking` | ダッシュ攻撃中か |
| `bool IsRush` | ラッシュ中か（C3ラッシュ or ダッシュラッシュ） |
| `void TryStartAttack()` | 通常攻撃入力を処理（サーバー権威） |
| `int ComboEnhanceLevel` | 連撃強化レベル（0〜3。読み取り専用プロパティ。サーバー権威） |
| `void TryStartCharge(Vector2)` | チャージ攻撃入力を処理（サーバー権威。最終段からは派生不可） |
| `bool IsEvolution` | エボリューション攻撃中か（E6-E9。HitboxSystem用。読み取り専用プロパティ） |
| `int SentanCount` | 所持仙箪数（読み取り専用プロパティ。サーバー権威） |
| `void EnhanceCombo()` | 連撃強化を+1する（仙箪強化用。サーバー専用。Lv3上限） |
| `void ResetEnhancements()` | 全強化をリセットする（死亡時リセット。仙箪カウントはリセットしない。サーバー専用） |
| `void AddSentan(int)` | 仙箪を追加する（SentanItem から呼ばれる。サーバー専用） |
| `void TryStartDashAttack()` | ダッシュ攻撃入力を処理（サーバー権威） |
| `InscriptionType C1Inscription` | C1刻印種別（読み取り専用プロパティ） |
| `InscriptionType C6Inscription` | C6刻印種別（読み取り専用プロパティ） |
| `void SetC1Inscription(InscriptionType)` | C1刻印を設定する（突/陣/砕/盾の4種。サーバー専用） |
| `void SetC6Inscription(InscriptionType)` | C6刻印を設定する（全6種。サーバー専用） |
| `WeaponType Weapon2Type` | 武器2の武器種（読み取り専用プロパティ。サーバー権威） |
| `bool IsBreakCharging` | ブレイクチャージ中か（HitboxSystem用。読み取り専用プロパティ） |
| `int BreakChargeVariant` | ブレイクチャージの種類（1=BC地上, 2=DBCダッシュ, 3=JBC空中） |
| `bool IsJumpAttacking` | ジャンプ攻撃(JA)中か（HitboxSystem用。読み取り専用プロパティ） |
| `bool IsJumpCharging` | ジャンプチャージ(JC)中か（HitboxSystem用。読み取り専用プロパティ） |
| `void TryStartJumpAttack()` | ジャンプ通常攻撃(JA)を開始する（サーバー権威。Jump中に□） |
| `void TryStartJumpCharge()` | ジャンプチャージ攻撃(JC)を開始する（サーバー権威。Jump中に△） |
| `int BreakRushStack` | ブレイクラッシュスタック数（0〜5。連続BC回数） |
| `float BreakRushAtkMultiplier` | ブレイクラッシュATKボーナス倍率（1.0 + 0.1 * stack） |
| `float GetBreakChargeMultiplier()` | ブレイクチャージのモーション倍率を返す（武器2パラメータ参照） |
| `void TryStartBreakCharge(bool, bool, Vector2)` | ブレイクチャージ入力を処理（サーバー権威。isDashing/isAirborne/moveInput） |
| `void SetWeapon2Type(WeaponType)` | 武器2の武器種を設定する（サーバー専用） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_networkComboStep` | `NetworkVariable<byte>` | 現在のコンボ段数（UI・他プレイヤー表示用） |
| `_comboEnhanceLevel` | `NetworkVariable<int>` | 連撃強化レベル（0〜3。サーバー権威） |
| `_sentanCount` | `NetworkVariable<int>` | 所持仙箪数（サーバー権威。死亡時リセットなし） |
| `_c1Inscription` | `NetworkVariable<byte>` | C1刻印種別（サーバー権威） |
| `_c6Inscription` | `NetworkVariable<byte>` | C6刻印種別（サーバー権威） |
| `_weapon2Type` | `NetworkVariable<WeaponType>` | 武器2の武器種（サーバー権威） |

**ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | ステート判定・遷移 |
| `PlayerMovement` | 武器種取得（WeaponData参照用） |

---

### HitboxSystem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `HitboxSystem : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

なし（全て private。FixedUpdate で自動実行）

**NetworkVariable**

なし

**ServerRpc / ClientRpc**

| メソッド名 | 種別 | 説明 |
|-----------|------|------|
| `NotifyHitClientRpc(ulong, ulong, Vector3)` | ClientRpc | ヒット確定を全クライアントに通知（エフェクト用） |
| `NotifyDamageClientRpc(ulong, int, bool)` | ClientRpc | ダメージ確定を全クライアントに通知 |

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `ComboSystem` | 攻撃状態・セグメント経過の参照 |
| `CharacterStateMachine` | 無双ステート判定（攻撃レベル決定用） |
| `CharacterController` | 攻撃前進移動 |
| `PlayerMovement` | 武器種取得（HitboxData/DamageCalculator連携用） |
| `ElementSystem` | 攻撃時の属性情報取得（チャージ攻撃のみ属性付与） |

※ ヒット対象（`hurtbox.GetComponent`）から以下も取得:
`HurtboxComponent`, `ReactionSystem`, `HealthSystem`, `MusouGauge`, `EGSystem`, `CharacterStateMachine`, `CharacterController`, `StatusEffectManager`

※ NPC兵士ヒット対象（`GetComponent<NPCSoldier>`）: `NPCSoldier.TakeDamage()` で簡易ダメージ適用（ガード・リアクションなし）

---

### SentanItem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `SentanItem : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

なし（全て private。OnTriggerEnter で自動取得、FixedUpdate で寿命管理）

**主な機能**
- NPC兵士死亡時にドロップされる仙箪アイテム
- プレイヤーが SENTAN_PICKUP_RADIUS 内に入ると自動取得（OnTriggerEnter）
- 取得時に ComboSystem.AddSentan(1) で仙箪カウント+1
- SENTAN_LIFETIME 秒経過で自動消滅（サーバー権威）
- 金色の小さな球体で視覚表現（CreateVisual）
- SphereCollider（trigger）と Kinematic Rigidbody を Awake で自動追加

**NetworkVariable / ServerRpc / ClientRpc**

なし

**依存（GetComponent — トリガー対象から取得）**

| 取得先 | 用途 |
|--------|------|
| `ComboSystem`（プレイヤー） | 仙箪カウント加算 |

---

### EnhancementRing.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `EnhancementRing : NetworkBehaviour` |
| 属性 | `[RequireComponent(typeof(ComboSystem))]` |

**内部 enum**

| 名前 | 説明 |
|------|------|
| `SlotEffect` | スロット効果種別（AtkUp, DefUp, MoveUp, SubGeneral, ComboEnhance） |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `bool IsRingActive` | リング回転中か（読み取り専用） |
| `int RingPosition` | 現在のリング位置 0〜6（読み取り専用） |
| `int EnhanceCount` | 発動済み回数（読み取り専用） |
| `int AtkBuffCount` | 攻撃バフ回数（読み取り専用） |
| `int DefBuffCount` | 防御バフ回数（読み取り専用） |
| `float AtkMultiplier` | ATK倍率 = 1.0 + バフ回数 × 0.1 |
| `float DefMultiplier` | DEF倍率 = 1.0 + バフ回数 × 0.1 |
| `void TryActivateSlot()` | R1入力によるスロット発動（サーバー権威） |
| `void ResetAllEnhancements()` | 全強化リセット（死亡時。ComboSystem.ResetEnhancements も内部で呼ぶ。サーバー専用） |
| `string GetSlotName(int)` | スロット効果名を返す（UI用） |

**NetworkVariable**

| 名前 | 型 | 説明 |
|------|-----|------|
| `_isRingActive` | `NetworkVariable<bool>` | リング回転中フラグ |
| `_ringPosition` | `NetworkVariable<int>` | 現在のリング位置 |
| `_enhanceCount` | `NetworkVariable<int>` | 発動済み回数 |
| `_atkBuffCount` | `NetworkVariable<int>` | ATKバフ回数 |
| `_defBuffCount` | `NetworkVariable<int>` | DEFバフ回数 |

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `ComboSystem` | 仙箪カウント参照・連撃強化 |
| `UltimateEnhancement` | 究極強化の発動・死亡時リセット |

---

### UltimateEnhancement.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `UltimateEnhancement : NetworkBehaviour` |
| 属性 | `[RequireComponent(typeof(ArmorSystem))]` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `bool IsUltimateActive` | 究極強化発動中か（読み取り専用） |
| `float UltimateRemainingTime` | 残り時間（秒。読み取り専用） |
| `float AtkMultiplier` | ATK倍率（発動中1.5、非発動時1.0） |
| `float DefMultiplier` | DEF倍率（発動中1.5、非発動時1.0） |
| `float SpeedMultiplier` | 移動速度倍率（発動中1.2、非発動時1.0） |
| `void Activate()` | 究極強化を発動する（サーバー専用。30秒バフ） |
| `void Deactivate()` | 究極強化を解除する（時間切れ・死亡時） |
| `void ResetOnDeath()` | 死亡時のリセット |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_isUltimateActive` | `NetworkVariable<bool>` | 究極強化発動中フラグ |
| `_ultimateRemainingTime` | `NetworkVariable<float>` | 残り時間（秒） |

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `ArmorSystem` | アーマーレベル+1の適用/復元 |

---

### HitboxData.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `HitboxData`（struct） |

**フィールド**

| 名前 | 型 | 説明 |
|------|-----|------|
| `Radius` | `float` | 判定半径 |
| `Length` | `float` | 判定長さ（前方方向） |
| `Offset` | `Vector3` | キャラ中心からのオフセット（ローカル座標） |
| `ActiveStartFrame` | `int` | アクティブ開始フレーム（0始まり） |
| `ActiveEndFrame` | `int` | アクティブ終了フレーム |
| `MultiHit` | `bool` | 多段ヒットか |
| `MaxHitCount` | `int` | 多段の場合の最大ヒット数 |

**主要 public メソッド**

| 名前 | 説明 |
|------|------|
| `static HitboxData GetHitboxData(int, int, bool, bool, WeaponType, bool, InscriptionType, InscriptionType)` | 攻撃状態に応じた HitboxData を返す（WeaponData に委譲、刻印対応） |

NetworkVariable / RPC / GetComponent なし。

---

### HurtboxComponent.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `HurtboxComponent : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `bool IsInvincible()` | 現在無敵状態か（サーバー側判定用） |
| `bool IsGuarding()` | ガード中か（Guard/GuardMove/EGPrepare/EGReady） |
| `bool IsGuardingAgainst(Vector3)` | 攻撃者に対してガードが有効か（正面180度判定） |

**NetworkVariable / ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | 無敵・ガードステート判定 |

---

### HealthSystem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `HealthSystem : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `int CurrentHp` | 現在HP（読み取り専用プロパティ） |
| `int MaxHp` | 最大HP（読み取り専用プロパティ） |
| `void TakeDamage(int)` | ダメージ適用。HP0で Dead 遷移（サーバー側） |
| `void FullHeal()` | HP全回復（デバッグ・リスポーン用、サーバー側） |
| `void Heal(int)` | 指定量HP回復（拠点回復等、サーバー側） |
| `float GetHpRatio()` | 現在HP / 最大HP を返す（根性補正判定用） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_currentHp` | `NetworkVariable<int>` | 現在HP |
| `_maxHp` | `NetworkVariable<int>` | 最大HP |

**ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | Dead 遷移 |

---

### ReactionSystem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `ReactionSystem : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `static HitReaction GetReactionType(int, int, bool)` | 攻撃種別からリアクションタイプを決定 |
| `void ApplyReaction(HitReaction, Vector3, int, int, AttackLevel)` | 被弾者にリアクション適用（ステート遷移+物理速度設定、サーバー側） |
| `void ResetReactionPhysics()` | リアクション物理をリセット（リスポーン等） |
| `bool IsAirborne()` | 被弾者が空中状態か判定 |

**NetworkVariable / ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | ステート遷移・空中判定 |
| `CharacterController` | リアクション物理移動 |
| `ArmorSystem` | のけぞり判定（アーマー比較） |

---

### ArmorSystem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `ArmorSystem : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `ArmorLevel CurrentArmorLevel` | 現在のアーマー段階（読み取り専用プロパティ） |
| `void SetArmorLevel(ArmorLevel)` | アーマー段階を設定（サーバー側） |
| `bool ShouldFlinch(AttackLevel)` | 攻撃を受けた時にのけぞるか判定（攻撃レベル >= アーマー段階でのけぞる）（サーバー側） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_armorLevel` | `NetworkVariable<byte>` | 現在のアーマー段階 |

**ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

なし

---

### EGSystem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `EGSystem : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `bool IsEGReady` | EG準備完了状態か（EGReady ステート） |
| `bool IsInEGState` | EG関連ステートか（EGPrepare/EGReady/EGCounter） |
| `bool DebugForceEG` | デバッグ用EG強制維持フラグ（Editor限定） |
| `void ProcessEG(bool, bool)` | 毎ティックのEG入力処理（chargeHeld, guardHeld。サーバー側） |
| `void OnEGCounter(Transform, ReactionSystem)` | EGカウンター発動（攻撃者を吹き飛ばし。サーバー側） |

**NetworkVariable / ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | ステート判定・遷移 |
| `MusouGauge` | ゲージ消費（EG維持・カウンター） |

---

### StatusEffectManager.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `StatusEffectManager : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `bool IsElectrified` | 感電中か（ReactionSystem が受け身判定に使用。読み取り専用プロパティ） |
| `void ApplyElementEffect(ElementType, int, bool)` | 属性に応じた状態異常を付与する（サーバー専用。isTargetAirborne で気絶判定） |
| `void OnElectrifiedHit()` | 感電中にヒットを受けた時のコンボカウント増加（上限で感電解除） |
| `void ClearAllEffects()` | 全状態異常を解除する（サーバー専用。リスポーン時等） |

**NetworkVariable**

なし（CharacterStateMachine の StatusEffect フラグ経由で同期）

**ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | 状態異常フラグの付与/解除 |
| `HealthSystem` | 燃焼ダメージ適用 |

---

### ElementSystem.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `ElementSystem : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `ElementType CurrentElement` | 現在の装備属性（読み取り専用プロパティ） |
| `int ElementLevel` | 現在の属性レベル（0=なし、1〜4。読み取り専用プロパティ） |
| `void SetElement(ElementType, int)` | 装備属性を設定する（サーバー専用） |
| `void GetAttackElement(int, out ElementType, out int)` | チャージ攻撃時の属性情報を取得（chargeType > 0 でのみ属性付与） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_elementType` | `NetworkVariable<ElementType>` | 装備属性の種別 |
| `_elementLevel` | `NetworkVariable<int>` | 属性レベル（1〜4） |

**ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

なし（自己完結）

---

### MusouGauge.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `MusouGauge : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `float CurrentGauge` | 現在のゲージ量（読み取り専用プロパティ） |
| `float MaxGauge` | 最大ゲージ量（= MUSOU_GAUGE_MAX） |
| `bool IsMusouActive` | 無双発動中か |
| `bool IsGaugeFull` | ゲージMAXか |
| `void AddGauge(float)` | ゲージを加算（攻撃ヒット・被弾等、サーバー側） |
| `void ConsumeGauge(float)` | ゲージを消費（EGカウンター・EG維持・斬属性の攻撃側コスト等、サーバー側） |
| `void ReduceGauge(float)` | 外部からゲージを減少させる（斬属性の被弾側ダメージ等、サーバー側） |
| `bool TryActivateMusou()` | 無双乱舞の発動を試みる（サーバー側） |
| `void ProcessMusouCharge(bool)` | 無双チャージ処理（○長押し中に毎ティック、サーバー側） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_currentGauge` | `NetworkVariable<float>` | 現在のゲージ量 |
| `_isMusouActive` | `NetworkVariable<bool>` | 無双発動中フラグ |

**ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | ステート判定・遷移 |
| `HealthSystem` | 真・無双判定（HP比率取得） |

---

## Netcode/

### LagCompensationManager.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `LagCompensationManager : MonoBehaviour`（シングルトン） |

**内部構造体**

| 名前 | 説明 |
|------|------|
| `RewindScope` | readonly struct, IDisposable。using ブロックで巻き戻し→自動復元 |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `static LagCompensationManager Instance` | 遅延初期化シングルトン |
| `void RegisterPlayer(ulong, Transform)` | プレイヤーをラグ補正対象に登録 |
| `void UnregisterPlayer(ulong)` | プレイヤーをラグ補正対象から解除 |
| `RewindScope Rewind(double)` | 指定時刻まで全プレイヤーを巻き戻す（usingスコープ） |
| `double EstimateViewTime(double)` | 攻撃者の推定表示時刻を計算 |
| `int RewindOverlapSphere(double, Vector3, float, Collider[], int)` | 巻き戻し状態でスフィアオーバーラップ実行 |

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。NetworkBehaviour ではない）

**依存（GetComponent）**

なし（シングルトン。外部から Transform を受け取る）

---

### HelloNetwork.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `HelloNetwork : MonoBehaviour` |

**主要 public メソッド / プロパティ**

なし（全て private。OnGUI で Host/Client/Server 接続UI を表示）

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。NetworkBehaviour ではない）

**依存（GetComponent）**

なし（`NetworkManager.Singleton` を直接参照）

---

## UI/

### NetworkStatsHUD.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `NetworkStatsHUD : MonoBehaviour` |

**主要 public メソッド / プロパティ**

なし（全て private。OnGUI で RTT・PacketLoss を画面左上に表示）

**主な機能**
- 0.5秒間隔で RTT(ms) / PacketLoss(%) を更新表示
- 半透明黒背景で視認性確保

---

### BattleHUD.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `BattleHUD : MonoBehaviour`（クライアント専用UI） |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `static Action<ulong, ulong> OnHitNotified` | ヒット通知コールバック（HitboxSystem から呼ばれる） |

**主な機能**
- 画面下部中央: 自キャラHP バー（青/黄/赤の3帯色変化）
- 画面下部中央: 無双ゲージバー（MAX時金色）
- 画面上部中央: ターゲットHP（最後に攻撃した/された敵）
- プレイヤー・NPC両対応のターゲット表示
- OnGUI ベース（NetworkStatsHUD左上・DebugTestHelper右上と非重複）

**依存**

| 取得先 | 用途 |
|--------|------|
| `HealthSystem`（ローカルプレイヤー） | 自キャラHP表示 |
| `MusouGauge`（ローカルプレイヤー） | 無双ゲージ表示 |
| `HealthSystem` / `NPCSoldier`（ターゲット） | ターゲットHP表示 |

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。NetworkBehaviour ではない）

**依存（GetComponent）**

なし（`NetworkManager.Singleton` を直接参照）

---

### MinimapHUD.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `MinimapHUD : MonoBehaviour`（クライアント専用UI） |

**主要 public メソッド / プロパティ**

なし（全て private。OnGUI でミニマップを描画）

**主な機能**
- 画面右下にミニマップ表示（OnGUI ベース）
- 自分（白）・味方（青）・敵（赤）のプレイヤー位置をドット表示
- 拠点の所属チーム色（赤/青/灰）を四角で表示
- Mキーで全体マップ⇔ミニマップ切替
- ワールド座標→ミニマップ座標変換

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。NetworkBehaviour ではない）

**依存**

| 取得先 | 用途 |
|--------|------|
| `TeamManager.Instance` | プレイヤーのチーム判定 |
| `HealthSystem`（スポーン済みオブジェクト） | プレイヤー識別 |
| `BasePoint[]`（FindObjectsByType） | 拠点位置・所属表示 |

---

### ScoreboardHUD.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `ScoreboardHUD : MonoBehaviour`（クライアント専用UI） |

**主要 public メソッド / プロパティ**

なし（全て private。OnGUI でスコアボードを描画）

**主な機能**
- 画面上部中央: 残り時間タイマー（分:秒）
- タイマー左右: 赤チーム撃破数 vs 青チーム撃破数
- WaitingForPlayers 時は「Waiting for players...」表示
- GameOver 時に画面中央に勝敗テキスト（VICTORY!/DEFEAT/DRAW）

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。NetworkBehaviour ではない）

**依存**

| 取得先 | 用途 |
|--------|------|
| `GameModeManager.Instance` | フェーズ・タイマー・スコア・勝敗情報 |
| `TeamManager.Instance` | ローカルプレイヤーのチーム判定 |

---

### EnhancementRingHUD.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `EnhancementRingHUD : MonoBehaviour`（クライアント専用UI） |

**主要 public メソッド / プロパティ**

なし（全て private。OnGUI で仙箪カウント・リングスロットを描画）

**主な機能**
- 画面右下に仙箪所持数・強化段階・連撃Lv表示
- リング回転中はスロット一覧を横並び表示、現在位置を金色ハイライト
- ローカルプレイヤーの EnhancementRing / ComboSystem を参照

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。NetworkBehaviour ではない）

**依存**

| 取得先 | 用途 |
|--------|------|
| `EnhancementRing`（ローカルプレイヤー） | リング状態・バフ回数表示 |
| `ComboSystem`（ローカルプレイヤー） | 仙箪カウント・連撃Lv表示 |

---

## Server/

### TeamManager.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `TeamManager : NetworkBehaviour`（シングルトン） |

**内部構造体**

| 名前 | 説明 |
|------|------|
| `TeamAssignment` | struct, `INetworkSerializable`。ClientId + TeamId のペア |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `static TeamManager Instance` | シングルトン |
| `bool IsTeamAssigned(ulong)` | 指定プレイヤーにチームが割り当て済みか |
| `Team GetPlayerTeam(ulong)` | 指定プレイヤーのチームを取得 |
| `List<ulong> GetTeamMembers(Team)` | 指定チームのメンバー一覧を取得（コピー） |
| `int GetTeamCount(Team)` | 指定チームの現在の人数を取得 |
| `bool IsSameTeam(ulong, ulong)` | 2プレイヤーが同チームか判定（フレンドリーファイア防止用） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_teamAssignments` | `NetworkList<TeamAssignment>` | 全プレイヤーのチーム割り当て（サーバーのみ書き込み可） |

**ServerRpc / ClientRpc**

なし（OnClientConnected / OnClientDisconnected コールバックでサーバー側処理）

**依存（GetComponent）**

なし（`NetworkManager.Singleton` を直接参照）

---

### SpawnManager.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `SpawnManager : NetworkBehaviour`（シングルトン） |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `static SpawnManager Instance` | シングルトン |
| `Vector3 GetSpawnPosition(ulong, Team)` | チーム別スポーン位置を取得（初回ラウンドロビン / リスポーン交互拠点） |
| `void RespawnPlayer(NetworkObject)` | リスポーン実行（テレポート + HP全回復 + 無双MAX + Idle遷移、サーバー専用） |

**NetworkVariable / ServerRpc / ClientRpc**

なし（サーバー側ローカル処理のみ）

**依存（GetComponent — 対象プレイヤーから取得）**

| 取得先 | 用途 |
|--------|------|
| `CharacterController` | テレポート時の一時無効化 |
| `CharacterStateMachine` | Idle 強制遷移 |
| `HealthSystem` | HP全回復 |
| `MusouGauge` | 無双ゲージMAX |
| `ReactionSystem` | リアクション物理リセット |
| `StatusEffectManager` | 全状態異常クリア |
| `EnhancementRing` | 全強化リセット（死亡時バフ消失） |
| `ComboSystem` | 連撃強化リセット（EnhancementRing 未装備時のフォールバック） |
| `TeamManager.Instance` | チーム情報取得 |

---

### MapGenerator.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `MapGenerator : MonoBehaviour` |

**主要 public メソッド / プロパティ**

なし（全て private。Awake でマップ全体を自動生成）

**主な機能**
- 100m × 100m の地面（Plane）生成
- 外壁 4辺（見えない壁。Renderer 無効化）
- 拠点 5箇所（中央1 + 赤2 + 青2。色分け立方体 + Trigger Collider）
- 障害物 7個（カメラ壁貫通テスト用の箱）
- 全オブジェクトは MapRoot 配下にヒエラルキー整理

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。ネットワーク非依存）

**依存（GetComponent）**

なし（GameConfig の定数を参照）

---

### BasePoint.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `BasePoint : NetworkBehaviour` |
| 属性 | `[RequireComponent(typeof(SphereCollider))]` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `BaseStatus Status` | 拠点の所属チーム（読み取り専用プロパティ） |
| `float CaptureProgress` | 制圧ゲージ（-1〜1。正=Red、負=Blue） |
| `int BaseIndex` | 拠点番号（0-4） |
| `void SetBaseIndex(int)` | 拠点番号を設定（MapGenerator から呼ばれる） |
| `void SetInitialStatus(BaseStatus)` | 拠点の初期所属を設定（サーバー専用） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_status` | `NetworkVariable<BaseStatus>` | 拠点の所属チーム |
| `_captureProgress` | `NetworkVariable<float>` | 制圧ゲージ（-1〜1） |

**ServerRpc / ClientRpc**

なし（FixedUpdate + OnTriggerStay でサーバー側処理）

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `SphereCollider` | エリア検出（OnTriggerStay） |
| `TeamManager.Instance` | プレイヤーのチーム判定 |
| `HealthSystem`（対象プレイヤー） | HP回復 |
| `CharacterStateMachine`（対象プレイヤー） | 死亡判定 |

---

### NPCSoldier.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `NPCSoldier : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `Team SoldierTeam` | 所属チーム（読み取り専用プロパティ） |
| `int CurrentHp` | 現在HP（読み取り専用プロパティ） |
| `int SpawnBaseIndex` | スポーン元拠点番号 |
| `bool IsDead` | 死亡フラグ |
| `void Initialize(Team, int, Vector3)` | サーバー専用初期設定（チーム・拠点番号・移動先） |
| `void TakeDamage(int)` | ダメージ適用（サーバー専用。HP0で仙箪ドロップ→デスポーン） |

**AI行動（サーバー専用・FixedUpdate）**

| 処理 | 説明 |
|------|------|
| `ScanForEnemy()` | NPC_DETECT_INTERVAL間隔でOverlapSphereNonAlloc。敵プレイヤー/敵NPCを検出 |
| `TryAttack()` | NPC_ATK_INTERVAL間隔。前方OverlapSphereで敵にNPC_ATKダメージ。ガード判定あり |
| `MoveToward(Vector3)` | 目標地点へ水平直線移動 |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_team` | `NetworkVariable<byte>` | 所属チーム |
| `_currentHp` | `NetworkVariable<int>` | 現在HP |

**ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

なし（NetworkTransform で位置同期。Prefab に手動追加）

---

### NPCSpawner.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `NPCSpawner : NetworkBehaviour`（シングルトン） |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `static NPCSpawner Instance` | シングルトン |
| `void SpawnSentanItem(Vector3)` | NPC死亡地点に仙箪アイテムをスポーンする（サーバー専用。ドロップ確率判定あり） |

**設定**

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `_npcSoldierPrefab` | `GameObject` | NPCSoldier Prefab（エディタで設定） |
| `_sentanItemPrefab` | `GameObject` | SentanItem Prefab（エディタで設定） |

**NetworkVariable / ServerRpc / ClientRpc**

なし（サーバー側ローカル処理のみ）

**依存**

| 取得先 | 用途 |
|--------|------|
| `BasePoint[]`（FindObjectsByType） | 拠点情報取得（スポーン元・ターゲット） |
| `TeamManager.Instance` | フレンドリーファイア判定（HitboxSystem経由） |

---

### GameModeManager.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `GameModeManager : NetworkBehaviour`（シングルトン） |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `static GameModeManager Instance` | シングルトン |
| `GamePhase Phase` | 現在のゲームフェーズ（読み取り専用プロパティ） |
| `float RemainingTime` | 残り時間（秒、読み取り専用プロパティ） |
| `int RedKills` | 赤チーム撃破数（読み取り専用プロパティ） |
| `int BlueKills` | 青チーム撃破数（読み取り専用プロパティ） |
| `int WinnerTeam` | 勝利チーム（-1=未決定, 0=Red, 1=Blue, 2=Draw） |
| `void AddKill(ulong)` | 撃破スコア加算（サーバー側。killerClientId のチームに+1） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_phase` | `NetworkVariable<GamePhase>` | ゲームフェーズ |
| `_remainingTime` | `NetworkVariable<float>` | 残り時間（秒） |
| `_redKills` | `NetworkVariable<int>` | 赤チーム撃破数 |
| `_blueKills` | `NetworkVariable<int>` | 青チーム撃破数 |
| `_winnerTeam` | `NetworkVariable<int>` | 勝利チーム |

**ServerRpc / ClientRpc**

| メソッド名 | 種別 | 説明 |
|-----------|------|------|
| `NotifyGameStartClientRpc()` | ClientRpc | 試合開始を全クライアントに通知 |
| `NotifyGameOverClientRpc(int)` | ClientRpc | 試合終了を全クライアントに通知（winner） |

**依存**

| 取得先 | 用途 |
|--------|------|
| `TeamManager.Instance` | チーム人数・撃破チーム判定 |

---

## Debug/

### DebugTestHelper.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `DebugTestHelper : NetworkBehaviour`（`#if UNITY_EDITOR` 限定） |

**主要 public メソッド / プロパティ**

なし（全て private。Host の自プレイヤー上でのみ動作）

**デバッグキー操作（使用頻度順に再配置済み）**

| キー | 機能 |
|------|------|
| F1 | 相手を正面2mに瞬間移動 |
| F2 | 全員HP全回復 + Dead復活 |
| F3 | 自分の無双ゲージ MAX |
| F4 | 自分のHPを20%に設定（真無双テスト用） |
| F5 | 相手にガード状態を強制トグル |
| F6 | 相手を背面2mに移動（めくりテスト用） |
| F7 | 相手を Hitstun トグル |
| F8 | 相手を EG展開トグル（強制維持+ゲージ補充） |
| F9 | 相手を Launch トグル |
| F10 | 相手のアーマー段階を+1（ループ） |
| F11 | 自分の武器2を変更（ループ） |
| F12 | GUI表示トグル |
| T | 俯瞰フリーカメラ トグル（CameraController） |

**NetworkVariable / ServerRpc / ClientRpc**

なし

**依存（GetComponent — 対象プレイヤーから取得）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | ステート強制遷移 |
| `HealthSystem` | HP全回復 |
| `MusouGauge` | ゲージMAX・EG用ゲージ補充 |
| `EGSystem` | EG強制維持フラグ設定 |
| `ArmorSystem` | アーマー段階変更 |
| `CharacterController` | テレポート時の一時無効化 |
