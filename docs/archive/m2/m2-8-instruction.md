# M2-8: 無双乱舞 + ゲージシステム

## 概要
無双ゲージの管理と無双乱舞の発動を実装する。
ゲージMAXで ○（中クリック or Q）→ 無双乱舞発動（無敵 + 連続攻撃）。

## 事前確認
- CharacterStateMachine.cs の Musou/MusouCharge ステートを確認
- ComboSystem.cs を確認
- docs/combat-spec.md セクション10（無双ゲージ）、セクション15（無双乱舞）を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === 無双ゲージ ===
public const float MUSOU_GAUGE_MAX = 100f;              // 無双ゲージ最大値
public const float MUSOU_GAIN_ON_HIT = 3f;              // 攻撃ヒット時のゲージ増加
public const float MUSOU_GAIN_ON_DAMAGED = 5f;          // 被弾時のゲージ増加
public const float MUSOU_CHARGE_RATE = 15f;             // ○長押しチャージ速度/秒
public const float MUSOU_DURATION = 4.0f;               // 無双乱舞持続時間
public const float TRUE_MUSOU_DURATION = 5.0f;          // 真・無双乱舞持続時間
public const float TRUE_MUSOU_HP_THRESHOLD = 0.2f;      // 真無双発動HP閾値（20%以下）
```

---

## 2. MusouGauge.cs 新規作成（Assets/Scripts/Combat/）

### クラス設計
- NetworkBehaviour を継承

### NetworkVariable
- `NetworkVariable<float> _currentGauge`: 現在ゲージ量（サーバー書き込み）
- `NetworkVariable<bool> _isMusouActive`: 無双発動中か

### メソッド

#### `AddGauge(float amount)` ★サーバー側
- ゲージを加算（最大 MUSOU_GAUGE_MAX でクランプ）

#### `ConsumeGauge(float amount)` ★サーバー側
- ゲージを消費（最小 0 でクランプ）

#### `TryActivateMusou()` ★サーバー側
- ゲージが MAX 未満 → 失敗
- ステートが Idle/Move/Hitstun のいずれか → 発動可能
- 気絶/凍結中 → 発動不可
- のけぞり中 → 発動可能（無双脱出）
- ゲージを全消費
- ステートを Musou に遷移
- _isMusouActive = true
- HP20%以下なら真・無双乱舞（持続時間が長い）
- Console ログ: `[Musou] 無双乱舞発動！` or `[Musou] 真・無双乱舞発動！`

#### `UpdateMusou()` ★サーバー側（FixedUpdate）
- Musou ステート中のみ処理
- _musouTimer を減算
- タイマー終了 → Idle に遷移、_isMusouActive = false
- Console ログ: `[Musou] 無双終了`

#### `ProcessMusouCharge(bool musouHeld)` ★サーバー側
- ○ 長押し（ゲージMAX未満時）→ MusouCharge ステートに遷移
- ゲージを MUSOU_CHARGE_RATE × deltaTime で増加
- ○ 離し → Idle に遷移
- ゲージMAX → Idle に遷移

---

## 3. PlayerMovement.cs の修正
- 中クリック or Q キー → PlayerInput.MusouPressed = true（押した瞬間）
- PlayerInput に `bool MusouHeld` を追加（長押し = MusouCharge 用）
- MusouPressed → MusouGauge.TryActivateMusou()
- MusouHeld → MusouGauge.ProcessMusouCharge()

---

## 4. 無双中の特性
- **無敵**: CharacterStateMachine.IsInvincible() が true を返す
- **移動不可**: Musou ステート中は固定位置（将来モーション依存で移動追加）
- **ガード可能**（敵がガードすればダメージ軽減される）

---

## 5. ゲージ増加トリガー（HitboxSystem / HealthSystem 連携）
- 攻撃がヒット → 攻撃者のゲージ += MUSOU_GAIN_ON_HIT
- ダメージを受けた → 被弾者のゲージ += MUSOU_GAIN_ON_DAMAGED

---

## 6. NetworkPlayer Prefab への追加
- MusouGauge コンポーネントを追加

---

## 7. テスト内容
1. **攻撃ヒット** → ゲージ増加ログ
2. **被弾** → ゲージ増加ログ
3. **Q長押し** → MusouCharge でゲージ増加
4. **ゲージMAXで Q** → `[Musou] 無双乱舞発動！` → 一定時間後終了
5. **無双中に攻撃を受ける** → ダメージなし（無敵）
6. **HP20%以下で無双** → 真・無双乱舞
7. **気絶中に Q** → 発動しない
8. **のけぞり中に Q** → 無双脱出

---

## 8. 完了条件
- [ ] 無双ゲージが増減する
- [ ] ゲージMAXで無双発動
- [ ] 無双中は無敵
- [ ] 真・無双乱舞がHP20%以下で発動
- [ ] のけぞりから無双脱出可能
- [ ] 気絶中は発動不可
- [ ] MusouCharge でゲージ溜め
- [ ] サーバー権威でゲージ・発動管理
- [ ] 既存動作が壊れていない
- [ ] git commit & push: "M2-8: 無双乱舞 + ゲージシステム"
