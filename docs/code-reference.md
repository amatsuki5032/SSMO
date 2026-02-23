# SSMO コードリファレンス

> CLAUDE.md のファイル構成セクションと同じ順序で記載。

---

## Character/PlayerMovement.cs

**クラス名**: `PlayerMovement` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `bool IsDashing` | 連続移動時間が閾値を超えたらtrue（ダッシュ攻撃発動条件） |

### NetworkVariable
| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_netPosition` | `NetworkVariable<Vector3>` | サーバー権威の位置（他プレイヤー表示用） |
| `_netRotationY` | `NetworkVariable<float>` | サーバー権威のY回転（他プレイヤー表示用） |

### ServerRpc
| メソッド | 説明 |
|---------|------|
| `SubmitInputServerRpc(PlayerInput input)` | クライアント→サーバーへの統合入力送信 |

### ClientRpc
| メソッド | 説明 |
|---------|------|
| `ConfirmStateClientRpc(uint tick, Vector3 pos, float rotY, float vVel)` | サーバー確定状態をオーナーに返送（リコンシリエーション用） |

### 依存 (GetComponent)
- `CharacterController`
- `CharacterStateMachine`
- `ComboSystem`
- `EGSystem`
- `MusouGauge`

---

## Character/CharacterStateMachine.cs

**クラス名**: `CharacterStateMachine` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `CharacterState CurrentState` | 現在のステート（読み取り専用） |
| `StatusEffect CurrentStatusEffects` | 現在の状態異常フラグ（読み取り専用） |
| `bool IsInvincible` | 現在無敵状態か |
| `event Action<CharacterState,CharacterState> OnStateChanged` | ステート変更イベント（旧→新） |
| `bool TryChangeState(CharacterState)` | ステート遷移を試行（サーバー側） |
| `void ForceState(CharacterState)` | 強制ステート設定（バリデーションスキップ） |
| `bool CanAcceptInput(InputType)` | 現在のステートで入力受付可能か判定 |
| `bool CanMove()` | 移動可能かの簡易判定 |
| `void SetHitstunDuration(float)` | 次のHitstun遷移で使う持続時間をオーバーライド |
| `void AddStatusEffect(StatusEffect)` | 状態異常フラグを付与（サーバー側） |
| `void RemoveStatusEffect(StatusEffect)` | 状態異常フラグを解除（サーバー側） |
| `bool HasStatusEffect(StatusEffect)` | 指定の状態異常フラグがあるか判定 |

### NetworkVariable
| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_state` | `NetworkVariable<CharacterState>` | 現在のキャラクターステート |
| `_statusEffects` | `NetworkVariable<StatusEffect>` | 状態異常ビットフラグ |

### ServerRpc
| メソッド | 説明 |
|---------|------|
| `RequestStateChangeServerRpc(CharacterState)` | クライアントからステート遷移をリクエスト |

### ClientRpc
なし

### 依存 (GetComponent)
なし（自身のみ）

---

## Shared/GameConfig.cs

**クラス名**: `GameConfig` (static class)

### public メソッド / プロパティ
なし（全て `public const` 定数のみ）

### 主要な定数カテゴリ
| カテゴリ | 代表的な定数 |
|---------|-------------|
| ネットワーク | `SERVER_TICK_RATE(60)`, `FIXED_DELTA_TIME`, `MAX_LAG_COMPENSATION_MS(150)` |
| 対戦ルール | `TEAM_SIZE(4)`, `MAX_PLAYERS(8)`, `MATCH_TIME_SECONDS(300)` |
| 移動 | `MOVE_SPEED(6)`, `ROTATION_SPEED(720)`, `JUMP_FORCE(8)`, `JUMP_GRAVITY(-20)` |
| ガード | `GUARD_ANGLE(180)`, `EG_CHARGE_SEC(1.0)`, `GUARD_MOVE_SPEED_MULTIPLIER(0.5)` |
| 無双 | `MUSOU_GAUGE_MAX(100)`, `MUSOU_DURATION_SEC(4)`, `TRUE_MUSOU_HP_THRESHOLD(0.2)` |
| HP・ダメージ | `DEFAULT_MAX_HP(1000)`, `DEFAULT_ATK(100)`, `CRITICAL_RATE(0.05)` |
| 被弾リアクション | `HITSTUN_LIGHT_DURATION(0.3)`, `LAUNCH_HEIGHT(3.0)`, `KNOCKBACK_DISTANCE_H(4.0)` |
| コンボ | `MAX_COMBO_STEP_BASE(4)`, `N1〜N4_DURATION`, `C1〜C6_DURATION` |
| 予測・補間 | `PREDICTION_BUFFER_SIZE(1024)`, `INTERPOLATION_DELAY(0.1)`, `SNAP_THRESHOLD(5)` |

### NetworkVariable / ServerRpc / ClientRpc / 依存
なし（static class）

---

## Shared/DamageCalculator.cs

**クラス名**: `DamageCalculator` (static class)

### 内部構造体
`DamageResult` — `HpDamage(int)`, `MusouDamage(int)`, `AttackerMusouCost(int)`, `IsCritical(bool)`

### public メソッド
| メソッド | 説明 |
|---------|------|
| `DamageResult Calculate(float atk, float motion, float def, float hpRatio, ...)` | メインのダメージ計算（属性・空中補正・根性補正・クリティカル含む） |
| `float GetMotionMultiplier(int combo, int charge, bool isDash, bool isRush)` | 攻撃種別に応じたモーション倍率を返す |
| `float GetElementDamageMultiplier(ElementType, int level)` | 属性レベルに応じたダメージ倍率を返す |
| `float GetGutsDivisor(float hpRatio)` | HP帯による根性補正除数を返す |
| `float GetSlashMinDamage(int level)` | 斬属性のレベル別最低保証ダメージ |

### NetworkVariable / ServerRpc / ClientRpc / 依存
なし（static class）

---

## Shared/CharacterState.cs

**定義**: enum / フラグ定義ファイル（クラスなし）

### enum 一覧
| enum名 | 基底型 | 説明 |
|--------|-------|------|
| `CharacterState` | `byte` | 行動ステート（Idle, Move, Attack, Guard, Musou, Hitstun, Dead 等） |
| `StatusEffect` | `byte` [Flags] | 状態異常フラグ（Electrified, Burn, Slow） |
| `AttackLevel` | `byte` | 攻撃レベル 4段階（Arrow, Normal, Charge, Musou） |
| `ArmorLevel` | `byte` | アーマー段階 5段階（None〜HyperArmor） |
| `DownType` | `byte` | ダウン種別（FaceDown, Crumble, Sprawl） |
| `InputType` | `byte` | 入力種別（Move, NormalAttack, ChargeAttack, Jump, Musou, Guard 等） |
| `HitReaction` | — | 被弾リアクション種別（Flinch, Launch, Knockback, Slam 等） |
| `ElementType` | — | 属性種別（None, Fire, Ice, Thunder, Wind, Slash） |
| `Team` | — | チーム識別（Red, Blue） |
| `WeaponType` | — | 武器種（GreatSword, DualBlades, Spear, Halberd, Fists, Bow） |

---

## Shared/PlayerInput.cs

**構造体名**: `PlayerInput` (INetworkSerializable)

### フィールド
| フィールド | 型 | 説明 |
|-----------|-----|------|
| `MoveInput` | `Vector2` | 移動方向 (H, V) |
| `JumpPressed` | `bool` | ×ボタン（押した瞬間） |
| `GuardHeld` | `bool` | L1（押しっぱなし） |
| `AttackPressed` | `bool` | □（押した瞬間） |
| `ChargePressed` | `bool` | △（押した瞬間） |
| `ChargeHeld` | `bool` | △ 長押し（EG準備用） |
| `MusouPressed` | `bool` | ○ 押した瞬間（無双発動） |
| `MusouHeld` | `bool` | ○ 長押し（無双チャージ） |
| `Tick` | `uint` | ティック番号 |

### public メソッド
| メソッド | 説明 |
|---------|------|
| `NetworkSerialize<T>(BufferSerializer<T>)` | INetworkSerializable 実装 |

### NetworkVariable / ServerRpc / ClientRpc / 依存
なし（構造体）

---

## Combat/ComboSystem.cs

**クラス名**: `ComboSystem` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `int ComboStep` | 現在のコンボ段数（0=非攻撃） |
| `int AttackSequence` | 攻撃セグメント番号（HitboxSystem用、新攻撃ごとにインクリメント） |
| `int ChargeType` | 現在のチャージ技番号（0=非チャージ、1=C1...） |
| `float SegmentElapsed` | 現在の攻撃セグメント経過時間（HitboxSystem用） |
| `bool IsDashAttacking` | ダッシュ攻撃中か |
| `bool IsRush` | ラッシュ中か（C3ラッシュ or ダッシュラッシュ） |
| `void TryStartAttack()` | 通常攻撃入力を処理（サーバー権威） |
| `void TryStartCharge(Vector2 moveInput)` | チャージ攻撃入力を処理（サーバー権威） |
| `void TryStartDashAttack()` | ダッシュ攻撃入力を処理（サーバー権威） |
| `static float GetAttackDuration(int step)` | コンボ段数に応じた通常攻撃持続時間を返す |
| `static float GetChargeDuration(int chargeType)` | チャージ技番号に応じた持続時間を返す |

### NetworkVariable
| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_networkComboStep` | `NetworkVariable<byte>` | 現在のコンボ段数（UI・他プレイヤー表示用） |

### ServerRpc / ClientRpc
なし

### 依存 (GetComponent)
- `CharacterStateMachine`

---

## Combat/HitboxSystem.cs

**クラス名**: `HitboxSystem` (NetworkBehaviour)

### public メソッド / プロパティ
なし（全てprivate。FixedUpdateで自動実行）

### NetworkVariable
なし

### ServerRpc
なし

### ClientRpc
| メソッド | 説明 |
|---------|------|
| `NotifyHitClientRpc(ulong attackerNetId, ulong targetNetId, Vector3 hitPos)` | ヒット確定を全クライアントに通知（エフェクト用） |
| `NotifyDamageClientRpc(ulong targetNetId, int damage, bool isCritical)` | ダメージ確定を全クライアントに通知 |

### 依存 (GetComponent)
- `ComboSystem`
- `CharacterStateMachine`
- (ヒット対象から) `HurtboxComponent`, `ReactionSystem`, `HealthSystem`, `MusouGauge`, `EGSystem`, `CharacterController`, `ArmorSystem`（via `hurtbox.GetComponent`）

---

## Combat/HitboxData.cs

**構造体名**: `HitboxData` (struct)

### フィールド
| フィールド | 型 | 説明 |
|-----------|-----|------|
| `Radius` | `float` | 判定半径 |
| `Length` | `float` | 判定長さ（前方方向） |
| `Offset` | `Vector3` | キャラ中心からのオフセット（ローカル座標） |
| `ActiveStartFrame` | `int` | アクティブ開始フレーム（0始まり） |
| `ActiveEndFrame` | `int` | アクティブ終了フレーム |
| `MultiHit` | `bool` | 多段ヒットか |
| `MaxHitCount` | `int` | 多段の場合の最大ヒット数 |

### public メソッド
| メソッド | 説明 |
|---------|------|
| `static HitboxData GetHitboxData(int comboStep, int chargeType, bool isDash, bool isRush)` | 攻撃状態に応じたHitboxDataを返す |

### NetworkVariable / ServerRpc / ClientRpc / 依存
なし（純粋データ構造体）

---

## Combat/HurtboxComponent.cs

**クラス名**: `HurtboxComponent` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `bool IsInvincible()` | 現在無敵状態か（サーバー側判定用） |
| `bool IsGuarding()` | ガード中か（Guard/GuardMove/EGPrepare/EGReady） |
| `bool IsGuardingAgainst(Vector3 attackerPos)` | 攻撃者に対してガードが有効か（正面180度判定） |

### NetworkVariable / ServerRpc / ClientRpc
なし

### 依存 (GetComponent)
- `CharacterStateMachine`

---

## Combat/HealthSystem.cs

**クラス名**: `HealthSystem` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `int CurrentHp` | 現在HP（読み取り専用） |
| `int MaxHp` | 最大HP（読み取り専用） |
| `void TakeDamage(int damage)` | ダメージを適用しHP減少、HP0でDead遷移（サーバー側） |
| `void FullHeal()` | HP全回復（デバッグ・リスポーン用、サーバー側） |
| `float GetHpRatio()` | 現在HP/最大HP を返す（根性補正判定用） |

### NetworkVariable
| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_currentHp` | `NetworkVariable<int>` | 現在HP |
| `_maxHp` | `NetworkVariable<int>` | 最大HP |

### ServerRpc / ClientRpc
なし

### 依存 (GetComponent)
- `CharacterStateMachine`

---

## Combat/ReactionSystem.cs

**クラス名**: `ReactionSystem` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `static HitReaction GetReactionType(int comboStep, int chargeType, bool isDash)` | 攻撃種別からリアクションタイプを決定 |
| `void ApplyReaction(HitReaction, Vector3 attackerPos, int combo, int charge, AttackLevel)` | 被弾者にリアクションを適用（ステート遷移+物理速度設定、サーバー側） |
| `void ResetReactionPhysics()` | リアクション物理をリセット（リスポーン等） |
| `bool IsAirborne()` | 被弾者が空中状態か判定 |

### NetworkVariable / ServerRpc / ClientRpc
なし

### 依存 (GetComponent)
- `CharacterStateMachine`
- `CharacterController`
- `ArmorSystem`

---

## Combat/ArmorSystem.cs

**クラス名**: `ArmorSystem` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `ArmorLevel CurrentArmorLevel` | 現在のアーマー段階（読み取り専用） |
| `void SetArmorLevel(ArmorLevel)` | アーマー段階を設定（サーバー側） |
| `bool ShouldFlinch(AttackLevel)` | 攻撃を受けた時にのけぞるか判定（サーバー側） |

### NetworkVariable
| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_armorLevel` | `NetworkVariable<byte>` | 現在のアーマー段階 |

### ServerRpc / ClientRpc
なし

### 依存 (GetComponent)
なし

---

## Combat/EGSystem.cs

**クラス名**: `EGSystem` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `bool IsEGReady` | EG準備完了状態か（EGReadyステート） |
| `bool IsInEGState` | EG関連ステートか（EGPrepare/EGReady/EGCounter） |
| `bool DebugForceEG` | デバッグ用EG強制維持フラグ（Editor限定） |
| `void ProcessEG(bool chargeHeld, bool guardHeld)` | 毎ティックのEG入力処理（サーバー側） |
| `void OnEGCounter(Transform attackerTransform, ReactionSystem attackerReaction)` | EGカウンター発動（攻撃者を吹き飛ばし、サーバー側） |

### NetworkVariable
なし

### ServerRpc / ClientRpc
なし

### 依存 (GetComponent)
- `CharacterStateMachine`
- `MusouGauge`

---

## Combat/MusouGauge.cs

**クラス名**: `MusouGauge` (NetworkBehaviour)

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `float CurrentGauge` | 現在のゲージ量（読み取り専用） |
| `float MaxGauge` | 最大ゲージ量 |
| `bool IsMusouActive` | 無双発動中か |
| `bool IsGaugeFull` | ゲージMAXか |
| `void AddGauge(float amount)` | ゲージを加算（攻撃ヒット・被弾等、サーバー側） |
| `void ConsumeGauge(float amount)` | ゲージを消費（EGカウンター・EG維持等、サーバー側） |
| `bool TryActivateMusou()` | 無双乱舞の発動を試みる（サーバー側） |
| `void ProcessMusouCharge(bool musouHeld)` | 無双チャージ処理（○長押し、サーバー側） |

### NetworkVariable
| 変数名 | 型 | 説明 |
|--------|-----|------|
| `_currentGauge` | `NetworkVariable<float>` | 現在のゲージ量 |
| `_isMusouActive` | `NetworkVariable<bool>` | 無双発動中フラグ |

### ServerRpc / ClientRpc
なし

### 依存 (GetComponent)
- `CharacterStateMachine`
- `HealthSystem`

---

## Netcode/LagCompensationManager.cs

**クラス名**: `LagCompensationManager` (MonoBehaviour, シングルトン)

### 内部構造体
`RewindScope` (readonly struct, IDisposable) — usingブロックで巻き戻し→自動復元

### public メソッド / プロパティ
| メソッド / プロパティ | 説明 |
|----------------------|------|
| `static LagCompensationManager Instance` | 遅延初期化シングルトン |
| `void RegisterPlayer(ulong clientId, Transform)` | プレイヤーをラグ補正対象に登録 |
| `void UnregisterPlayer(ulong clientId)` | プレイヤーをラグ補正対象から解除 |
| `RewindScope Rewind(double timestamp)` | 指定時刻まで全プレイヤーを巻き戻す（usingスコープ） |
| `double EstimateViewTime(double clientReportedTime)` | 攻撃者の推定表示時刻を計算 |
| `int RewindOverlapSphere(double ts, Vector3 origin, float radius, Collider[] results, int layer)` | 巻き戻し状態でスフィアオーバーラップ実行 |

### NetworkVariable / ServerRpc / ClientRpc
なし（MonoBehaviour。NetworkBehaviourではない）

### 依存 (GetComponent)
なし（シングルトン。外部からTransformを受け取る）

---

## Netcode/HelloNetwork.cs

**クラス名**: `HelloNetwork` (MonoBehaviour)

### public メソッド / プロパティ
なし（全てprivate。OnGUIで接続UIを表示）

### 主な機能
- Host / Client / Dedicated Server の接続ボタン表示
- 接続中のクライアント数・ローカルID・トランスポート名を表示
- Disconnectボタン

### NetworkVariable / ServerRpc / ClientRpc
なし（MonoBehaviour。NetworkBehaviourではない）

### 依存 (GetComponent)
なし（`NetworkManager.Singleton` を直接参照）

---

## UI/NetworkStatsHUD.cs

**クラス名**: `NetworkStatsHUD` (MonoBehaviour)

### public メソッド / プロパティ
なし（全てprivate。OnGUIでRTT・PacketLossを表示）

### 主な機能
- 画面左上にRTT(ms)とPacketLoss(%)を表示
- 更新頻度0.5秒おき（パフォーマンス配慮）
- 半透明黒背景で視認性確保

### NetworkVariable / ServerRpc / ClientRpc
なし（MonoBehaviour。NetworkBehaviourではない）

### 依存 (GetComponent)
なし（`NetworkManager.Singleton` を直接参照）

---

## Debug/DebugTestHelper.cs

**クラス名**: `DebugTestHelper` (NetworkBehaviour, `#if UNITY_EDITOR` 限定)

### public メソッド / プロパティ
なし（全てprivate。Host自プレイヤー上でのみ動作）

### デバッグキー操作
| キー | 機能 |
|------|------|
| F1 | 相手を Hitstun トグル |
| F2 | 相手を Launch トグル |
| F3 | 自分の無双ゲージ MAX |
| F4 | 相手を EG展開トグル（強制維持+ゲージ補充） |
| F5 | 相手を自分の正面2mに瞬間移動 |
| F6 | 相手にガード状態を強制トグル |
| F9 | 全員のHP全回復 + Dead復活 |
| F10 | 相手のアーマー段階を1上げる（ループ） |
| F12 | GUI表示トグル |

### NetworkVariable / ServerRpc / ClientRpc
なし

### 依存 (GetComponent — 対象プレイヤーから取得)
- `CharacterStateMachine`
- `HealthSystem`
- `MusouGauge`
- `EGSystem`
- `ArmorSystem`
- `CharacterController`
