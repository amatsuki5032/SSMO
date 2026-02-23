# M2-4b: ダッシュ攻撃 + ダッシュラッシュ

## 概要
ダッシュ状態（1.5秒以上移動）で左クリック（□）を押すとダッシュ攻撃（D）が出る。
D 中に □ 連打でダッシュラッシュ（追加攻撃）。
アニメーション・Hitbox なし。ステート遷移とタイマーのみ。

## 事前確認
- ComboSystem.cs の現在のコードを確認（M2-4a で変更済み）
- PlayerMovement.cs の IsDashing プロパティを確認
- GameConfig.cs の既存定数を確認
- docs/combat-spec.md セクション5（ダッシュ攻撃）を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === ダッシュ攻撃 ===
public const float DASH_ATTACK_DURATION = 0.6f;     // D 持続時間（仮値）
public const float DASH_RUSH_DURATION = 0.25f;       // Dラッシュ追加ヒット間隔
public const int DASH_RUSH_MAX_HITS = 6;              // Dラッシュ最大追加ヒット数
```

---

## 2. ComboSystem.cs にダッシュ攻撃を追加

### TryStartDashAttack() ★サーバー側で実行
- PlayerMovement.IsDashing == true かつ AttackPressed == true のとき発動
- ステートを DashAttack に遷移
- _dashAttackTimer に DASH_ATTACK_DURATION を設定
- ダッシュ状態（_moveTime）をリセット
- Console ログ: `[Combo] D（ダッシュ攻撃）開始`

### ダッシュ攻撃 vs 通常攻撃の優先判定
- AttackPressed が来たとき:
  1. IsDashing == true → TryStartDashAttack()
  2. IsDashing == false → TryStartAttack()（通常コンボ）
- この判定はサーバー側で行う

### UpdateDashAttack() ★サーバー側で実行
- DashAttack ステート中のみ処理
- _dashAttackTimer を減算
- D 中に AttackPressed（□）→ ダッシュラッシュに移行
  - _isDashRushing = true
  - _dashRushHitCount++
  - タイマーを DASH_RUSH_DURATION にリセット
  - Console ログ: `[Combo] Dラッシュ {hitCount}hit`
- □ が押されなかった or _dashRushHitCount >= DASH_RUSH_MAX_HITS → 終了
- タイマー終了 → Idle に遷移
- Console ログ: `[Combo] D 終了`

### ダッシュ攻撃後
- D / Dラッシュ終了後は Idle に戻る
- D から N コンボには移行しない

---

## 3. PlayerMovement.cs の修正

### サーバー側処理の修正
- AttackPressed の処理順序を変更:
  1. IsDashing チェック → TryStartDashAttack()
  2. それ以外 → TryStartAttack()

---

## 4. CharacterStateMachine.cs の確認

以下が正しく動くか確認：
- Move → DashAttack の遷移が許可されている
- DashAttack → Idle の遷移が許可されている
- DashAttack 中に CanAcceptInput(NormalAttack) → true（ラッシュ用）

---

## 5. テスト内容
1. **1.5秒以上移動 → 左クリック** → `[Combo] D（ダッシュ攻撃）開始`（通常のN1ではない）
2. **D 中に左クリック連打** → `Dラッシュ 1hit` `2hit` ... と続く
3. **ダッシュ未満で左クリック** → 通常の N1 が出る
4. **D 終了後** → Idle に戻る（N コンボに移行しない）
5. **既存動作維持**: 通常コンボ・チャージ・ジャンプ・ガードが壊れていない

---

## 6. 完了条件
- [ ] ダッシュ状態で □ → ダッシュ攻撃が出る
- [ ] 非ダッシュで □ → 通常 N1 が出る
- [ ] D 中に □ 連打でラッシュ
- [ ] D/ラッシュ終了後は Idle に戻る
- [ ] サーバー権威でダッシュ判定
- [ ] 既存動作が壊れていない
- [ ] git commit & push: "M2-4b: ダッシュ攻撃 + ダッシュラッシュ"
