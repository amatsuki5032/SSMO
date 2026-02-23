# M2-7a: ガード判定 + ガード方向判定

## 概要
ガード中に攻撃を受けた場合、正面180度の攻撃を防御する判定を実装する。
ガード成功時はダメージ80%カット。背面・側面からの攻撃（めくり）はガード貫通。

## 事前確認
- CharacterStateMachine.cs の Guard/GuardMove ステートを確認
- ReactionSystem.cs / HealthSystem.cs を確認
- docs/combat-spec.md セクション8（ガード）を参照

---

## 1. ガード方向判定

### IsGuardingAgainst(Vector3 attackerPosition) ★サーバー側で実行
- ガード中のキャラの正面方向（transform.forward）と攻撃者の方向を比較
- 攻撃者が正面180度以内 → ガード成功
- 攻撃者が背面 → めくり（ガード貫通）

```csharp
Vector3 toAttacker = (attackerPosition - transform.position).normalized;
float angle = Vector3.Angle(transform.forward, toAttacker);
bool isGuardSuccess = angle <= 90f; // 正面180度 = ±90度
```

---

## 2. HitboxSystem.cs の修正

### ヒット確定時のガード判定フロー
1. 被弾者が Guard/GuardMove ステートか確認
2. Guard 中なら IsGuardingAgainst() で方向判定
3. ガード成功 → ダメージ × GUARD_DAMAGE_MULTIPLIER (0.2)
4. ガード失敗（めくり）→ 通常ダメージ + リアクション
5. Console ログ: `[Guard] ガード成功 / [Guard] めくり！`

### ガード成功時のリアクション
- ガード成功時はのけぞりなし（Guard ステート維持）
- ガードノックバック: わずかに後退（仮: 0.3m）

---

## 3. テスト内容
1. **正面からの攻撃をガード** → ダメージ軽減 + `[Guard] ガード成功` ログ
2. **背面からの攻撃** → ガード貫通 + `[Guard] めくり！` ログ + 通常リアクション
3. **ガード中の移動** → ガード判定が維持される
4. **非ガード時** → 通常ダメージ + リアクション
5. **既存動作維持**

---

## 4. 完了条件
- [ ] 正面180度のガードが機能する
- [ ] めくり（背面攻撃）がガードを貫通する
- [ ] ガード成功時ダメージ80%カット
- [ ] ガード成功時のけぞらない
- [ ] サーバー権威で判定
- [ ] 既存動作が壊れていない
- [ ] git commit & push: "M2-7a: ガード判定"
