# M2-2: 移動リファクタ + ジャンプ + ダッシュ判定 + ガード移動

## 概要
既存の PlayerMovement.cs を拡張し、ステートマシンと連動した移動システムを構築する。
ジャンプ、ダッシュ判定（移動時間トラッキング）、ガード移動を追加する。

## 事前確認
- `Assets/Scripts/Character/PlayerMovement.cs` の既存コードを確認
- `Assets/Scripts/Character/CharacterStateMachine.cs` の API を確認
- `Assets/Scripts/Shared/GameConfig.cs` の既存定数を確認
- `docs/combat-spec.md` のセクション2（移動）、セクション6（ジャンプ）、セクション8（ガード）を参照

---

## 1. 移動ステート連動

### 現状
- PlayerMovement は入力があれば常に移動できる
- ステートマシンとの連動が最小限

### 変更内容
- 移動入力処理の前に `_stateMachine.CanAcceptInput(InputType.Move)` をチェック
- ステートに応じた移動制御:

| ステート | 移動 | 備考 |
|---------|------|------|
| Idle | 可 | 通常速度 |
| Move | 可 | 通常速度 |
| Guard | 不可（GuardMoveで可） | |
| GuardMove | 可（制限あり） | 正面向き固定、速度低下 |
| Attack/Charge | 不可 | |
| Jump | 不可（空中慣性のみ） | 方向転換不可 |
| Hitstun | 不可 | |
| ダウン系 | 不可 | |
| MusouCharge | 不可（その場停止） | |
| Musou | 不可（無双モーション依存） | |

---

## 2. ジャンプ実装

### 入力
- × (Space) でジャンプ
- 左スティック (WASD) 同時押しでその方向にジャンプ

### 仕様（combat-spec.md セクション6 準拠）
- **2段ジャンプなし**
- **ジャンプ中は方向転換不可**（離陸時の方向で飛ぶ）
- **ジャンプ中ガード不可**
- **離陸時に無敵フレームあり**（GameConfig.JUMP_INVINCIBLE_FRAMES = 4F）
- ジャンプの高さ・滞空時間は将来武器種で変動（今は固定値）

### サーバー権威の実装
1. クライアント: Space入力 → `RequestJumpServerRpc()` 送信
2. サーバー: ステートチェック（Idle/Move のみジャンプ可）→ ジャンプ許可
3. サーバー: ステートを Jump に遷移 → 初速度を設定
4. サーバー: FixedUpdate で重力適用 → 着地判定
5. サーバー: 着地 → Idle に遷移
6. クライアント予測: ローカルでも即座にジャンプ実行（サーバー否認時ロールバック）

### 無敵フレーム管理
- ★ サーバーのみが管理
- Jump ステートの最初の JUMP_INVINCIBLE_FRAMES フレームは無敵
- CharacterStateMachine の IsInvincible() で判定

### ジャンプパラメータ（GameConfig に追加）
```csharp
public const float JUMP_FORCE = 8f;              // ジャンプ初速（仮値）
public const float JUMP_GRAVITY = -20f;           // ジャンプ中の重力（通常重力と別）
public const float JUMP_HEIGHT = 3f;              // 目標ジャンプ高さ（参考値）
public const float JUMP_DURATION = 0.6f;          // 目標滞空時間（参考値）
```

### 着地判定
- CharacterController.isGrounded を使用
- サーバー側で着地判定 → Jump ステートから Idle に遷移

---

## 3. ダッシュ判定（移動時間トラッキング）

### 仕様（combat-spec.md セクション5 準拠）
- **一定時間（DASH_ATTACK_MOVE_TIME = 1.5秒）以上連続移動でダッシュ状態**
- 移動距離ではなく **移動時間** で判定（鈍足でも発動可能）
- ダッシュ状態で□を押すとダッシュ攻撃（D）になる（攻撃はM2-4で実装）

### 実装内容（M2-2 では判定のみ）
- `_moveTime` フィールドを追加（連続移動時間を追跡）
- 移動入力がある間 `_moveTime` を加算
- 移動入力がなくなったら `_moveTime` をリセット
- `IsDashing` プロパティ: `_moveTime >= GameConfig.DASH_ATTACK_MOVE_TIME`
- ★ サーバー側で _moveTime を管理（チート防止）

### 注意
- ダッシュ攻撃の発動自体はM2-4で実装
- ここではダッシュ状態の判定トラッキングのみ

---

## 4. ガード移動

### 仕様（combat-spec.md セクション8 準拠）
- L1 (Shift) 押しっぱなしでガード
- ガード中に移動入力 → GuardMove ステート
- **正面を向いたまま移動**（向きは固定）
- **移動方向は正面・真横・後方のみ**（斜め入力は最も近い方向にスナップ）
- **移動速度は通常の50%**（仮値）

### 入力処理
1. クライアント: Shift入力 → `RequestGuardServerRpc()` 送信
2. サーバー: Idle/Move のみガード可 → Guard ステートに遷移
3. クライアント: Shift + WASD → `RequestGuardMoveServerRpc(direction)` 送信
4. サーバー: Guard 中のみ GuardMove 可 → 移動処理（速度制限あり）
5. クライアント: Shift 離す → `RequestGuardReleaseServerRpc()` 送信
6. サーバー: Idle に遷移

### ガード方向（正面180度）
- ガード成功判定はM2-7で実装
- ここではステート遷移と移動制限のみ

### ガード移動のパラメータ（GameConfig に追加）
```csharp
public const float GUARD_MOVE_SPEED_MULTIPLIER = 0.5f;  // ガード中移動速度倍率
```

---

## 5. 入力の ServerRpc 統合

### 現状
- `SubmitInputServerRpc(Vector2 moveInput)` で移動入力のみ送信

### 変更
- 入力構造体を拡張して全入力を1つの ServerRpc で送信（帯域効率）

```csharp
public struct PlayerInput : INetworkSerializable
{
    public Vector2 MoveInput;       // 移動方向
    public bool JumpPressed;        // ×ボタン（押した瞬間）
    public bool GuardHeld;          // L1（押しっぱなし）
    public bool AttackPressed;      // □（M2-3で使用）
    public bool ChargePressed;      // △（M2-4で使用）
    public bool MusouPressed;       // ○（M2-8で使用）
    public uint Tick;               // ティック番号

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MoveInput);
        serializer.SerializeValue(ref JumpPressed);
        serializer.SerializeValue(ref GuardHeld);
        serializer.SerializeValue(ref AttackPressed);
        serializer.SerializeValue(ref ChargePressed);
        serializer.SerializeValue(ref MusouPressed);
        serializer.SerializeValue(ref Tick);
    }
}
```

- M2-3以降で使うフィールドは今は false 固定で送信
- 将来の入力追加時にプロトコル変更が不要

---

## 6. テスト内容
1. **移動 + ステート連動**: WASDで Idle↔Move が切り替わる（既存動作維持）
2. **ジャンプ**: Spaceでジャンプ → 着地で Idle に戻る
3. **ジャンプ中移動不可**: ジャンプ中にWASDで方向が変わらない
4. **ダッシュ判定**: 1.5秒以上移動するとConsoleに "[Dash] ダッシュ状態" のようなログ
5. **ガード**: Shift押しで Guard ステート → 離すで Idle
6. **ガード移動**: Shift + WASD で GuardMove（速度が遅い）
7. **ガード中方向固定**: ガード移動中にキャラの向きが変わらない
8. **ParrelSync**: 2人接続でジャンプ・ガードが同期される

---

## 7. 完了条件
- [ ] ジャンプが動作する（Space → 空中 → 着地）
- [ ] ジャンプ中は方向転換不可
- [ ] ジャンプの無敵フレームがサーバーで管理されている
- [ ] ダッシュ判定（移動時間トラッキング）が動作
- [ ] ガード（Shift）でステート遷移
- [ ] ガード移動（Shift+WASD）で速度制限付き移動
- [ ] PlayerInput 構造体で入力を統合
- [ ] ParrelSync テストで同期確認
- [ ] コンパイルエラーなし
- [ ] git commit & push: "M2-2: 移動リファクタ + ジャンプ + ダッシュ判定 + ガード移動"
