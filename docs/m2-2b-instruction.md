# M2-2b: ジャンプ実装

## 概要
Space キーでジャンプを実装する。サーバー権威型。
離陸時の無敵フレームはサーバーのみが管理。

## 事前確認
- PlayerMovement.cs の現在のコードを確認（M2-2a で変更済み）
- CharacterStateMachine.cs の TryChangeState / CanAcceptInput を確認
- GameConfig.cs の既存定数を確認
- docs/combat-spec.md セクション6（ジャンプ）を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === ジャンプ ===
public const float JUMP_FORCE = 8f;            // ジャンプ初速
public const float JUMP_GRAVITY = -20f;         // ジャンプ中の重力
public const int JUMP_INVINCIBLE_FRAMES = 4;    // 離陸無敵フレーム数（既存なら確認のみ）
```

---

## 2. PlayerMovement.cs にジャンプ処理を追加

### クライアント側（入力）
- PlayerInput.JumpPressed が true のとき、ジャンプリクエスト
- クライアント予測: ローカルでも即座にジャンプ実行

### サーバー側
1. JumpPressed を受信
2. ステートチェック: Idle / Move のみジャンプ可（CanAcceptInput で判定）
3. ステートを Jump に遷移（TryChangeState）
4. 垂直速度に JUMP_FORCE を設定
5. FixedUpdate で JUMP_GRAVITY を適用
6. CharacterController.isGrounded で着地判定 → Idle に遷移

### ジャンプ中の制限
- **方向転換不可**: 離陸時の移動方向を保持、ジャンプ中は移動入力を無視
- **ガード不可**: Jump ステート中は GuardHeld を無視
- 空中慣性: 離陸時の水平速度を維持（新しい移動入力は受け付けない）

### 着地処理
- サーバー: isGrounded == true && 垂直速度 <= 0 → Idle に遷移
- 垂直速度をリセット

---

## 3. CharacterStateMachine.cs の確認

以下が既に実装されているか確認し、なければ追加：
- Jump ステートで CanAcceptInput が Move を false で返すこと
- Jump → Idle の遷移が許可されていること
- Jump ステートの無敵フレーム管理（IsInvincible メソッド）

---

## 4. テスト内容
1. Space でジャンプ → 空中 → 着地で Idle に戻る
2. Console に `[StateMachine] ... Idle → Jump` と `Jump → Idle` が出る
3. ジャンプ中に WASD で方向が変わらない
4. ジャンプ中に Shift（ガード）が効かない
5. 既存の移動（WASD）が壊れていないこと

---

## 5. 完了条件
- [ ] Space でジャンプできる
- [ ] ジャンプ中は方向転換不可
- [ ] 着地で Idle に戻る
- [ ] ステート遷移ログが出る
- [ ] 既存の移動が壊れていない
- [ ] git commit & push: "M2-2b: ジャンプ実装"
