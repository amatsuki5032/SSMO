# M2-7b: エマージェンシーガード（EG）

## 概要
ガード中に △（右クリック）を約1秒押し込みで EG 準備完了。
EG 中に攻撃を受けるとカウンター発動。無双ゲージを消費する。

## 事前確認
- ガード判定（M2-7a）を確認
- CharacterStateMachine.cs を確認
- docs/combat-spec.md セクション9（EG）を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === エマージェンシーガード ===
public const float EG_CHARGE_TIME = 1.0f;          // EG準備に必要な押し込み時間
public const float EG_MUSOU_DRAIN_RATE = 5f;        // EG維持中の無双ゲージ減少量/秒
public const float EG_COUNTER_MUSOU_COST = 20f;     // EGカウンター発動時の消費量
public const float EG_COUNTER_KNOCKBACK = 8f;       // EGカウンター吹き飛ばし力
```

---

## 2. EGSystem.cs 新規作成（Assets/Scripts/Combat/）

### クラス設計
- NetworkBehaviour を継承
- CharacterStateMachine、MusouGauge（M2-8で作成）への参照
- ★ MusouGauge がまだない場合は仮の float _musouGauge で代用

### サーバー側フィールド
- `_egChargeTimer`: △押し込み時間
- `_isEGReady`: EG準備完了か
- `_isEGActive`: EG発動中か

### NetworkVariable
- `NetworkVariable<bool> _networkEGActive`: EG状態を同期

### ProcessEG(bool chargeHeld, bool guardHeld) ★サーバー側
- Guard ステート中に chargeHeld（右クリック長押し）→ _egChargeTimer 加算
- _egChargeTimer >= EG_CHARGE_TIME → _isEGReady = true
- EG準備完了中:
  - 無双ゲージを EG_MUSOU_DRAIN_RATE × deltaTime で減少
  - ゲージ0 or ガード解除 or △離し → EG解除
- Console ログ: `[EG] 準備完了` / `[EG] 解除`

### OnEGCounter(Vector3 attackerPosition) ★サーバー側
- EG準備完了中に攻撃を受けた → カウンター発動
- 無双ゲージを EG_COUNTER_MUSOU_COST 消費
- 攻撃者に吹き飛ばしリアクション適用
- Console ログ: `[EG] カウンター発動！`

---

## 3. HitboxSystem.cs の修正

### ガード判定にEG判定を追加
- ガード成功時に EGSystem._isEGReady をチェック
- EG準備完了 → OnEGCounter() を呼ぶ（通常ガードではなくカウンター）

---

## 4. PlayerMovement.cs の修正
- Guard 中に右クリック長押し（ChargePressed を持続送信）
- PlayerInput に ChargeHeld（長押し）を追加するか、ChargePressed の送信を修正
  - 注意: ChargePressed は「押した瞬間」だったが、EG は「押し続け」が必要
  - PlayerInput に `bool ChargeHeld` フィールドを追加（Input.GetMouseButton(1)）

---

## 5. テスト内容
1. **Shift + 右クリック長押し1秒** → `[EG] 準備完了` ログ
2. **EG中に攻撃を受ける** → `[EG] カウンター発動！` + 攻撃者が吹き飛ぶ
3. **EG中にShift離し** → `[EG] 解除`
4. **EG中に右クリック離し** → `[EG] 解除`
5. **既存動作維持**

---

## 6. 完了条件
- [ ] EG 準備（1秒チャージ）が動作する
- [ ] EG 中にカウンターが発動する
- [ ] EG 解除条件が全て機能する
- [ ] 無双ゲージ消費（仮実装 or M2-8連携）
- [ ] サーバー権威で判定
- [ ] 既存動作が壊れていない
- [ ] git commit & push: "M2-7b: エマージェンシーガード"
