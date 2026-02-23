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

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。オーナー専用のローカルカメラ）

**依存（GetComponent）**

なし（PlayerMovement から動的に生成・Initialize される）

---

### PlayerMovement.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `PlayerMovement : NetworkBehaviour` |

**主要 public メソッド / プロパティ**

| 名前 | 説明 |
|------|------|
| `bool IsDashing` | 連続移動時間が閾値超過でダッシュ状態か返す（プロパティ） |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_netPosition` | `NetworkVariable<Vector3>` | サーバー権威の位置（他プレイヤー表示用） |
| `_netRotationY` | `NetworkVariable<float>` | サーバー権威のY回転（他プレイヤー表示用） |

**ServerRpc / ClientRpc**

| メソッド名 | 種別 | 説明 |
|-----------|------|------|
| `SubmitInputServerRpc(PlayerInput)` | ServerRpc | クライアント→サーバーへ統合入力送信 |
| `ConfirmStateClientRpc(uint, Vector3, float, float)` | ClientRpc | サーバー確定状態をオーナーに返送（リコンシリエーション用） |

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterController` | 物理移動 |
| `CharacterStateMachine` | ステート判定・遷移 |
| `ComboSystem` | 攻撃入力の委譲 |
| `EGSystem` | EG入力処理の委譲 |
| `MusouGauge` | 無双入力処理の委譲 |

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
| 対戦ルール | `TEAM_SIZE(4)`, `MAX_PLAYERS(8)`, `MATCH_TIME_SECONDS(300)`, `SPAWN_POINTS_PER_TEAM(2)`, `RESPAWN_DELAY(0)` |
| スポーン座標 | `TEAM_RED_SPAWN_POS_1/2` (readonly Vector3), `TEAM_BLUE_SPAWN_POS_1/2` (readonly Vector3) |
| 戦闘 | `INPUT_BUFFER_SEC(0.15)`, `COMBO_WINDOW_RATIO(0.3)` |
| ガード | `GUARD_ANGLE(180)`, `EG_CHARGE_SEC(1.0)`, `GUARD_KNOCKBACK_DISTANCE(0.3)` |
| ジャンプ | `JUMP_FORCE(8)`, `JUMP_GRAVITY(-20)` |
| 無双ゲージ | `MUSOU_GAUGE_MAX(100)`, `MUSOU_DURATION_SEC(4)` |
| HP・ダメージ | `DEFAULT_MAX_HP(1000)`, `DEFAULT_ATK(100)`, `DEFAULT_DEF(50)` |
| コンボ | `MAX_COMBO_STEP_BASE(4)`, `N1〜N4_DURATION`, `C1〜C6_DURATION` |
| リアクション | `HITSTUN_LIGHT_DURATION(0.3)`, `LAUNCH_HEIGHT(3.0)`, `KNOCKBACK_DISTANCE_H(4.0)` |
| カメラ | `CAMERA_DISTANCE(3.0)`, `CAMERA_HEIGHT(2.0)`, `CAMERA_SENSITIVITY(2.0)`, `CAMERA_MIN/MAX_PITCH(-10/60)` |
| 予測・補間 | `PREDICTION_BUFFER_SIZE(1024)`, `INTERPOLATION_DELAY(0.1)` |
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
| `float GetMotionMultiplier(int, int, bool, bool)` | 攻撃種別に応じたモーション倍率を返す |
| `float GetElementDamageMultiplier(ElementType, int)` | 属性レベルに応じたダメージ倍率を返す |
| `float GetGutsDivisor(float)` | HP帯による根性補正除数を返す |
| `float GetSlashMinDamage(int)` | 斬属性のレベル別最低保証ダメージを返す |

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
| `WeaponType` | 武器種（GreatSword, DualBlades, Spear, Halberd, Fists, Bow） |

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
| `void TryStartCharge(Vector2)` | チャージ攻撃入力を処理（サーバー権威） |
| `void TryStartDashAttack()` | ダッシュ攻撃入力を処理（サーバー権威） |
| `static float GetAttackDuration(int)` | コンボ段数に応じた通常攻撃持続時間を返す |
| `static float GetChargeDuration(int)` | チャージ技番号に応じた持続時間を返す |

**NetworkVariable**

| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_networkComboStep` | `NetworkVariable<byte>` | 現在のコンボ段数（UI・他プレイヤー表示用） |

**ServerRpc / ClientRpc**

なし

**依存（GetComponent）**

| 取得先 | 用途 |
|--------|------|
| `CharacterStateMachine` | ステート判定・遷移 |

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

※ ヒット対象（`hurtbox.GetComponent`）から以下も取得:
`HurtboxComponent`, `ReactionSystem`, `HealthSystem`, `MusouGauge`, `EGSystem`, `CharacterStateMachine`, `CharacterController`

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
| `static HitboxData GetHitboxData(int, int, bool, bool)` | 攻撃状態に応じた HitboxData を返す |

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
| `bool ShouldFlinch(AttackLevel)` | 攻撃を受けた時にのけぞるか判定（サーバー側） |

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
| `void ConsumeGauge(float)` | ゲージを消費（EGカウンター・EG維持等、サーバー側） |
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

**NetworkVariable / ServerRpc / ClientRpc**

なし（MonoBehaviour。NetworkBehaviour ではない）

**依存（GetComponent）**

なし（`NetworkManager.Singleton` を直接参照）

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
| `TeamManager.Instance` | チーム情報取得 |

---

## Debug/

### DebugTestHelper.cs

| 項目 | 内容 |
|------|------|
| クラス名 | `DebugTestHelper : NetworkBehaviour`（`#if UNITY_EDITOR` 限定） |

**主要 public メソッド / プロパティ**

なし（全て private。Host の自プレイヤー上でのみ動作）

**デバッグキー操作**

| キー | 機能 |
|------|------|
| F1 | 相手を Hitstun トグル |
| F2 | 相手を Launch トグル |
| F3 | 自分の無双ゲージ MAX |
| F4 | 相手を EG展開トグル（強制維持+ゲージ補充） |
| F5 | 相手を自分の正面2mに瞬間移動 |
| F6 | 相手にガード状態を強制トグル |
| F9 | 全員のHP全回復 + Dead復活 |
| F10 | 相手のアーマー段階を+1（ループ） |
| F12 | GUI表示トグル |

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
