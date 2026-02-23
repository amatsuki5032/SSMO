# M2-6a: ダメージ計算 + HP 同期

## 概要
ヒット確定後のダメージ計算をサーバー側で実行し、HP を全クライアントに同期する。
DamageCalculator.cs（M0で作成済み）を活用する。

## 事前確認
- DamageCalculator.cs の既存コードを確認（M0 で作成済み）
- HitboxSystem.cs のヒット確定処理を確認
- GameConfig.cs の既存定数を確認
- docs/combat-spec.md セクション16（ダメージ計算式）を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === HP・ダメージ ===
public const int DEFAULT_MAX_HP = 1000;        // デフォルト最大HP（仮値）
public const int DEFAULT_ATK = 100;            // デフォルト攻撃力（仮値）
public const int DEFAULT_DEF = 50;             // デフォルト防御力（仮値）
public const float CRITICAL_RATE = 0.05f;      // クリティカル率 5%
public const float CRITICAL_MULTIPLIER = 1.5f; // クリティカル倍率
public const float AIR_DAMAGE_MULTIPLIER = 0.5f; // 空中補正（ダメージ半減）
public const float GUARD_DAMAGE_MULTIPLIER = 0.2f; // ガード時ダメージ倍率

// 根性補正
public const float GUTS_YELLOW_THRESHOLD = 0.5f;  // HP50%以下で黄色
public const float GUTS_RED_THRESHOLD = 0.2f;     // HP20%以下で赤
public const float GUTS_YELLOW_DIVISOR = 1.5f;    // 黄帯の除数
public const float GUTS_RED_DIVISOR = 2.0f;       // 赤帯の除数
```

---

## 2. DamageCalculator.cs の修正・拡張

### CalculateDamage() ★サーバー側で実行
combat-spec.md セクション16 の計算式に準拠:

```
1. 攻撃倍率 = モーション倍率（ComboSystem から取得）
2. 基礎ダメージ = ATK × 攻撃倍率
3. 防御計算 = 基礎ダメージ × (100 / (100 + DEF))
4. 空中補正 = 空中被弾時は ×0.5
5. 根性補正 = HP青:÷1 / HP黄:÷1.5 / HP赤:÷2
6. ガード補正 = ガード時 ×0.2 / 非ガード ×1.0
7. クリティカル = 5%確率で ×1.5
8. 最低ダメージ保証 = max(最終ダメージ, 1)
```

### 入力パラメータ
- int atk: 攻撃者の攻撃力
- float motionMultiplier: モーション倍率
- int def: 被弾者の防御力
- bool isAirborne: 空中被弾か
- float targetHpRatio: 被弾者のHP比率（根性補正用）
- bool isGuarding: ガード中か

### 戻り値
- int: 最終ダメージ値

---

## 3. HealthSystem.cs 新規作成（Assets/Scripts/Combat/）

### クラス設計
- NetworkBehaviour を継承
- [RequireComponent(typeof(CharacterStateMachine))]

### NetworkVariable
- `NetworkVariable<int> _currentHp`: 現在HP（サーバー書き込み、全員読み取り）
- `NetworkVariable<int> _maxHp`: 最大HP

### メソッド

#### `TakeDamage(int damage)` ★サーバー側で実行
- _currentHp.Value -= damage
- HP <= 0 → 死亡処理（CharacterStateMachine を Dead に遷移）
- Console ログ: `[HP] {name} が {damage} ダメージ → 残HP: {hp}`

#### `GetHpRatio()` → float
- 現在HP / 最大HP を返す（根性補正の判定用）

---

## 4. HitboxSystem.cs の修正

### ヒット確定後の処理
- ヒット対象の HealthSystem を取得
- DamageCalculator.CalculateDamage() でダメージ計算
- HealthSystem.TakeDamage() でダメージ適用
- NotifyDamageClientRpc() でクライアントに通知

---

## 5. NetworkPlayer Prefab への追加
- HealthSystem コンポーネントを追加

---

## 6. テスト内容
1. **ParrelSync で2人接続** → 攻撃ヒットで `[HP] ... ダメージ` ログ
2. **HPが減る** → NetworkVariable 経由で両側で確認
3. **HP0** → Dead ステートに遷移
4. **根性補正**: HP50%以下で黄帯ダメージ軽減、20%以下で赤帯
5. **既存動作維持**

---

## 7. 完了条件
- [ ] サーバー側でダメージ計算が実行される
- [ ] HP が NetworkVariable で全クライアントに同期される
- [ ] 根性補正が正しく適用される
- [ ] HP0 で Dead ステートに遷移
- [ ] 既存動作が壊れていない
- [ ] git commit & push: "M2-6a: ダメージ計算 + HP同期"
