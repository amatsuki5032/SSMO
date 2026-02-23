# M2-2c: ダッシュ判定 + ガード移動

## 概要
連続移動時間によるダッシュ判定と、Shift によるガード + ガード移動を実装する。

## 事前確認
- PlayerMovement.cs の現在のコードを確認（M2-2b で変更済み）
- CharacterStateMachine.cs を確認
- GameConfig.cs の既存定数を確認
- docs/combat-spec.md セクション5（ダッシュ）、セクション8（ガード）を参照

---

## 1. ダッシュ判定（移動時間トラッキング）

### GameConfig.cs に定数追加
```csharp
public const float DASH_ATTACK_MOVE_TIME = 1.5f;  // ダッシュ発動に必要な連続移動時間（既存なら確認のみ）
```

### PlayerMovement.cs に追加
- `_moveTime` フィールド（float）: 連続移動時間を追跡
- 移動入力がある間 FixedUpdate で `_moveTime += Time.fixedDeltaTime`
- 移動入力なし → `_moveTime = 0`
- `public bool IsDashing => _moveTime >= GameConfig.DASH_ATTACK_MOVE_TIME`
- ★ サーバー側で `_moveTime` を管理（チート防止）
- ダッシュ状態になったら Console に `[Dash] ダッシュ状態` のログ（デバッグ用、1回だけ）

### 注意
- ダッシュ攻撃の発動自体は M2-4b で実装
- ここでは **判定のトラッキングのみ**

---

## 2. ガード

### GameConfig.cs に定数追加
```csharp
public const float GUARD_MOVE_SPEED_MULTIPLIER = 0.5f;  // ガード移動速度倍率（既存なら確認のみ）
```

### 入力処理
- PlayerInput.GuardHeld == true のとき:
  - サーバー: ステートが Idle/Move → Guard に遷移
  - Guard 中に移動入力あり → GuardMove に遷移
  - GuardMove 中に移動入力なし → Guard に戻る
- PlayerInput.GuardHeld == false のとき:
  - Guard/GuardMove → Idle に遷移

### ガード移動の制限
- **移動速度**: 通常の GUARD_MOVE_SPEED_MULTIPLIER 倍（50%）
- **方向固定**: ガード開始時の向き（Y回転）を保持。ガード中は向きが変わらない
- 移動方向は全方向OK（前後左右斜め）

### ステート遷移
```
Idle/Move → [Shift押す] → Guard
Guard → [WASD入力] → GuardMove
GuardMove → [WASD離す] → Guard
Guard/GuardMove → [Shift離す] → Idle
```

---

## 3. CharacterStateMachine.cs の確認

以下が正しく動作するか確認：
- Guard ステートで CanAcceptInput(Move) → true（GuardMove遷移用）
- Guard/GuardMove → Idle の遷移が許可されている
- Guard 中に Jump は不可

---

## 4. テスト内容
1. **ダッシュ判定**: WASD を 1.5秒以上押し続ける → Console に `[Dash]` ログ
2. **ダッシュリセット**: キーを離す → 再度押して 1.5秒未満は非ダッシュ
3. **ガード**: Shift 押す → `Idle → Guard` ログ。離す → `Guard → Idle` ログ
4. **ガード移動**: Shift + WASD → `Guard → GuardMove` ログ。移動速度が遅い
5. **ガード中方向固定**: ガード移動中にキャラの向きが変わらない
6. **ガード中ジャンプ不可**: Shift + Space でジャンプしない
7. **既存動作維持**: 通常移動・ジャンプが壊れていない

---

## 5. 完了条件
- [ ] ダッシュ判定が動作（1.5秒以上移動でIsDashing == true）
- [ ] ガード（Shift）でステート遷移
- [ ] ガード移動で速度が50%に低下
- [ ] ガード中は向き固定
- [ ] ガード中ジャンプ不可
- [ ] 既存の移動・ジャンプが壊れていない
- [ ] git commit & push: "M2-2c: ダッシュ判定 + ガード移動"
