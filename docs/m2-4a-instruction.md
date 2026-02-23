# M2-4a: チャージ攻撃（C1〜C6 派生）

## 概要
通常攻撃コンボ中に右クリック（△）を押すとチャージ攻撃に派生する仕組みを実装する。
アニメーション・Hitbox なし。ステート遷移とタイマーのみ。

## 事前確認
- ComboSystem.cs の現在のコードを確認
- CharacterStateMachine.cs の Charge ステートを確認
- GameConfig.cs の既存定数を確認
- docs/combat-spec.md セクション4（チャージ攻撃）を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === チャージ攻撃 ===
public const float C1_DURATION = 0.7f;    // C1 持続時間（仮値）
public const float C2_DURATION = 0.6f;    // C2 打ち上げ
public const float C3_DURATION = 0.5f;    // C3 ラッシュ初段
public const float C3_RUSH_DURATION = 0.2f; // C3 ラッシュ追加ヒット間隔
public const int C3_RUSH_MAX_HITS = 8;    // C3 ラッシュ最大追加ヒット数
public const float C4_DURATION = 0.8f;    // C4 吹き飛ばし
public const float C5_DURATION = 0.7f;    // C5 チャージシュート
public const float C6_DURATION = 1.0f;    // C6 最大技
```

---

## 2. ComboSystem.cs にチャージ攻撃を追加

### 入力
- 右クリック（Input.GetMouseButtonDown(1)）→ PlayerInput.ChargePressed = true

### TryStartCharge() ★サーバー側で実行
- Attack ステート中のみ発動可能
- 現在のコンボ段数（_comboStep）に応じた C 技に派生:
  - _comboStep == 0（Idle/Move時に△）→ C1
  - _comboStep == 1（N1中に△）→ C2
  - _comboStep == 2（N2中に△）→ C3
  - _comboStep == 3（N3中に△）→ C4
  - _comboStep == 4（N4中に△）→ C5
  - N5, N6 はまだ解放されていないので C6 は今は不可
- ステートを Charge に遷移（TryChangeState）
- _chargeType を記録（C1〜C6 のどれか）
- Console ログ: `[Combo] C{x} 開始`

### Idle/Move から C1
- コンボ中でなくても △ 単体押しで C1 が出る（ガード崩し技）
- _comboStep == 0 の状態で ChargePressed → C1 開始
- ステートを Charge に遷移

### UpdateCharge() ★サーバー側で実行（FixedUpdate から呼ぶ）
- Charge ステート中のみ処理
- _chargeTimer を減算
- タイマー終了 → Idle に遷移、コンボリセット
- Console ログ: `[Combo] C{x} 終了`

### C3 ラッシュの特殊処理
- C3 発動後、△（右クリック）を連打すると追加ヒット
- _isRushing フラグで管理
- 追加ヒットごとに _rushHitCount++ 、C3_RUSH_DURATION をタイマーにセット
- △ が押されなかった or _rushHitCount >= C3_RUSH_MAX_HITS → ラッシュ終了
- Console ログ: `[Combo] C3 ラッシュ {hitCount}hit`

### チャージ攻撃後
- チャージ攻撃終了後は必ず Idle に戻る（N コンボに戻らない）
- _comboStep = 0 にリセット

---

## 3. PlayerMovement.cs の修正

### 入力取得
- Update() で右クリック（Input.GetMouseButtonDown(1)）を検出
- PlayerInput.ChargePressed = true にする（押した瞬間のみ）
- C3 ラッシュ中は連打検出のため、Charge ステート中も ChargePressed を送信

### サーバー側処理
- PlayerInput.ChargePressed == true のとき → ComboSystem.TryStartCharge() を呼ぶ

---

## 4. CharacterStateMachine.cs の確認

以下が正しく動くか確認：
- Attack → Charge の遷移が許可されている
- Idle/Move → Charge の遷移が許可されている（C1用）
- Charge → Idle の遷移が許可されている
- Charge 中に CanAcceptInput(ChargeAttack) → true（C3ラッシュ用）

---

## 5. チャージ攻撃中の制限
- 移動不可
- ジャンプ不可
- ガード不可
- 向き調整: チャージ開始時のスティック方向で向きを設定（その後は固定）

---

## 6. テスト内容
1. **右クリック単体（Idle時）** → `[Combo] C1 開始` → `C1 終了`
2. **左クリック → 右クリック（N1中）** → `N1 開始` → `C2 開始` → `C2 終了`
3. **左×2 → 右クリック（N2中）** → N1 → N2 → `C3 開始`
4. **C3 中に右クリック連打** → `C3 ラッシュ 1hit` `2hit` ... と続く
5. **左×3 → 右クリック（N3中）** → N1 → N2 → N3 → `C4 開始`
6. **左×4 → 右クリック（N4中）** → N1 → N2 → N3 → N4 → `C5 開始`
7. **チャージ後にNコンボに戻らない** → C技終了後は Idle
8. **既存動作維持**: 通常コンボ・ジャンプ・ガードが壊れていない

---

## 7. 完了条件
- [ ] C1 が Idle/Move から発動できる
- [ ] N1〜N4 中にそれぞれ C2〜C5 に派生できる
- [ ] C3 ラッシュが右クリック連打で継続する
- [ ] チャージ攻撃後は Idle に戻る
- [ ] サーバー権威でチャージタイプを管理
- [ ] 既存の通常コンボ・移動・ジャンプ・ガードが壊れていない
- [ ] git commit & push: "M2-4a: チャージ攻撃 C1-C5"
