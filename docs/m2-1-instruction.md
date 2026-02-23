# M2-1: キャラクターステートマシン実装指示

## 概要
サーバー権威型のキャラクターステートマシンを実装する。
全ステート遷移の最終権限はサーバーが持つ。

## 事前確認
- `Assets/Scripts/Shared/CharacterState.cs` の既存内容を確認すること
- `Assets/Scripts/Shared/GameConfig.cs` の既存内容を確認すること
- `Assets/Scripts/Character/PlayerMovement.cs` の既存内容を確認すること
- `docs/combat-spec.md` のセクション22（ステート一覧）を参照すること

---

## 1. CharacterState.cs の更新
**パス**: `Assets/Scripts/Shared/CharacterState.cs`

既存の enum を以下に置き換える。M2で使用するステートのみ（M4以降のものはコメントで予約）。

```csharp
// キャラクターの行動ステート
// ★ サーバーが最終遷移権限を持つ ★
public enum CharacterState : byte  // byte で NetworkVariable 帯域節約
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

    // === 状態異常（重複可能なのでフラグで管理。ステートとしては凍結のみ） ===
    Freeze = 70,        // 凍結（約2秒行動不能→解除モーション）

    // === 死亡 ===
    Dead = 80,
}
```

### 状態異常フラグ（ステートとは別にビットフラグで管理）
```csharp
[System.Flags]
public enum StatusEffect : byte
{
    None        = 0,
    Electrified = 1 << 0,  // 感電: 受け身不可
    Burn        = 1 << 1,  // 燃焼: 持続ダメージ
    Slow        = 1 << 2,  // 鈍足: 移動低下+ジャンプ不可
}
```

### 攻撃レベル（アーマー貫通判定用）
```csharp
public enum AttackLevel : byte
{
    Arrow = 1,      // 雑魚の矢
    Normal = 2,     // 通常攻撃 (N)
    Charge = 3,     // チャージ (C) / エボリューション (E)
    Musou = 4,      // 無双乱舞
}
```

### アーマー段階
```csharp
public enum ArmorLevel : byte
{
    None = 1,            // 通常: 全てのけぞる
    ArrowResist = 2,     // 矢耐性
    NormalResist = 3,    // N耐性（特定モーション中）
    SuperArmor = 4,      // SA: チャージ耐性
    HyperArmor = 5,      // HA: 無双耐性
}
```

### ダウン種別
```csharp
public enum DownType : byte
{
    FaceDown = 0,    // 前のめり: 追撃→のけぞり
    Crumble = 1,     // 崩れ落ち: 追撃→浮く（長）
    Sprawl = 2,      // 仰向け: 追撃→浮く（短）
}
```

### 既存の WeaponType, ElementType はそのまま維持
- WeaponType に変更なし
- ElementType は5種+None であることを確認（炎/氷/雷/風/斬）

---

## 2. CharacterStateMachine.cs の新規作成
**パス**: `Assets/Scripts/Character/CharacterStateMachine.cs`

### 設計方針
- NetworkBehaviour を継承
- `NetworkVariable<CharacterState>` でステートを同期
- `NetworkVariable<StatusEffect>` で状態異常フラグを同期
- ステート遷移は **サーバーのみが実行**
- クライアントはステート遷移を **リクエスト**（ServerRpc）するのみ
- クライアント予測: ローカルで仮遷移→サーバー確定で補正

### 主要API
```csharp
// ★ サーバー側で実行 ★
// ステート遷移のリクエスト。バリデーション後に遷移
public bool TryChangeState(CharacterState newState)

// 現在のステートで特定の入力が受付可能か判定
public bool CanAcceptInput(InputType input)

// 状態異常フラグの付与/解除（サーバーのみ）
public void AddStatusEffect(StatusEffect effect)
public void RemoveStatusEffect(StatusEffect effect)
public bool HasStatusEffect(StatusEffect effect)
```

### InputType enum（入力の種類）
```csharp
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
```

### ステート遷移ルール（CanAcceptInput のロジック）

```
Idle        → 全入力受付
Move        → 攻撃/ガード/ジャンプ/無双
Attack      → 次段N(□) / チャージ(△) / (将来: ブレイクL2)
Charge      → ラッシュ△(C3のみ) / (将来: ブレイクL2)
DashAttack  → ラッシュ(□) / (将来: ブレイクL2)
DashRush    → □連打で継続
Guard       → 移動 / EG(△) / 解除
GuardMove   → ガード解除 / EG(△)
EGPrepare   → L1+△維持 / 解除
EGReady     → 維持 / 解除
EGCounter   → 入力不可（自動）
Jump        → JA(□) / JC(△)
JumpAttack  → 入力不可
MusouCharge → 離す→Idle / MAXで○→Musou
Musou       → 入力不可（無敵）
TrueMusou   → 入力不可（無敵）
Hitstun     → 無双のみ受付（脱出）
Launch      → 受け身(×)のみ（不能時間後）
AirHitstun  → 受け身(×)のみ（不能時間後）
AirRecover  → 入力不可（着地まで）
全ダウン系   → 入力不可（起き上がりまで）
Getup       → 入力不可（無敵）
Freeze      → 入力不可
Dead        → 入力不可
```

### ステート遷移時のコールバック
```csharp
// ステートが変わった時に他のコンポーネントが反応するためのイベント
public event System.Action<CharacterState, CharacterState> OnStateChanged;
// 引数: (旧ステート, 新ステート)
```

### ステート持続時間の管理
- 各ステートに `_stateTimer` を持つ
- のけぞり・ダウン・気絶・凍結等はタイマーで自動遷移
- タイマーは FixedUpdate で減算（サーバーのみ）

### 無敵状態の管理
- 以下のステートは **サーバーのみが管理する無敵**:
  - Musou / TrueMusou（完全無敵）
  - Getup（起き上がり無敵）
  - Jump の離陸数フレーム
  - AirRecover の数フレーム

---

## 3. PlayerMovement.cs の修正
**パス**: `Assets/Scripts/Character/PlayerMovement.cs`

### 変更内容
- CharacterStateMachine への参照を追加
- 移動処理の前に `CanAcceptInput(InputType.Move)` をチェック
- ステートが Move 以外の時は移動入力を無視
- GuardMove 時は移動方向を制限（正面/真横/後方のみ）

**注意**: PlayerMovement.cs の既存のクライアント予測・リコンシリエーション・補間のコードは壊さないこと。ステートチェックを追加するだけ。

---

## 4. GameConfig.cs への定数追加
**パス**: `Assets/Scripts/Shared/GameConfig.cs`

```csharp
// === M2 戦闘パラメータ ===

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

// EG準備時間（秒）
public const float EG_PREPARE_TIME = 1.0f;

// ダッシュ攻撃発動に必要な移動時間（秒）
public const float DASH_ATTACK_MOVE_TIME = 1.5f;

// 先行入力バッファ（秒）
public const float INPUT_BUFFER_TIME = 0.15f;

// コンボ受付ウィンドウ（モーション末尾の割合）
public const float COMBO_WINDOW_RATIO = 0.3f;
```

---

## 5. テスト内容
1. ParrelSync で Host + Client 起動
2. 移動中に各ステートへの遷移が正しく動作するか確認
   - Idle → Move → Idle
   - Idle → Guard → GuardMove → Idle
   - Idle → Jump → Idle（着地）
3. サーバー権威の確認
   - クライアント側でステートを直接変更できないことを確認
   - ステートの NetworkVariable が全クライアントに同期されることを確認
4. 無効な遷移が拒否されることを確認
   - Attack 中に Move が受け付けられないこと
   - ダウン中に攻撃が受け付けられないこと

---

## 6. 完了条件
- [ ] CharacterState enum が combat-spec.md のステート一覧と一致
- [ ] CharacterStateMachine.cs がサーバー権威で動作
- [ ] ステート遷移ルールが正しく実装されている
- [ ] PlayerMovement.cs がステートに応じて移動を制御
- [ ] ParrelSync テストでステート同期を確認
- [ ] NetworkVariable で全クライアントにステートが同期される
- [ ] git commit & push: "M2-1: キャラクターステートマシン"
