# M2-3a: 通常攻撃コンボ基盤（N1〜N4 ステート遷移）

## 概要
□（左クリック）で通常攻撃 N1〜N4 の連鎖を実装する。
アニメーションなし・Hitboxなし。ステート遷移とタイマーのみ。
サーバー権威でコンボ段数を管理。

## 事前確認
- PlayerMovement.cs の PlayerInput 構造体（AttackPressed フィールド）を確認
- CharacterStateMachine.cs の Attack ステートを確認
- GameConfig.cs の既存定数を確認
- docs/combat-spec.md セクション3（通常攻撃コンボ）を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === 通常攻撃 ===
public const int MAX_COMBO_STEP_BASE = 4;       // 無強化での最大コンボ段数
public const float COMBO_WINDOW_RATIO = 0.3f;   // コンボ受付ウィンドウ（モーション最後の30%）（既存なら確認のみ）
public const float INPUT_BUFFER_TIME = 0.15f;    // 先行入力バッファ 150ms（既存なら確認のみ）

// 各コンボ段の持続時間（秒）※仮値。将来アニメーションに合わせて調整
public const float N1_DURATION = 0.5f;
public const float N2_DURATION = 0.5f;
public const float N3_DURATION = 0.55f;
public const float N4_DURATION = 0.65f;
```

---

## 2. ComboSystem.cs を新規作成（Assets/Scripts/Combat/）

### クラス設計
- NetworkBehaviour を継承
- CharacterStateMachine への参照を持つ
- [RequireComponent(typeof(CharacterStateMachine))]

### サーバー側で管理するデータ
- `_comboStep`（int）: 現在のコンボ段数（0 = 非攻撃、1 = N1, 2 = N2 ...）
- `_attackTimer`（float）: 現在の攻撃モーションの残り時間
- `_comboWindowOpen`（bool）: コンボ受付ウィンドウが開いているか
- `_maxComboStep`（int）: 最大コンボ段数（初期値 = MAX_COMBO_STEP_BASE = 4）

### NetworkVariable
- `NetworkVariable<byte> _networkComboStep`: 全クライアントに同期（UIや他プレイヤー表示用）

### メソッド

#### `TryStartAttack()` ★サーバー側で実行
- ステートが Idle/Move のとき → N1 開始
- ステートが Attack かつ _comboWindowOpen == true のとき → 次の段に進む
- _comboStep >= _maxComboStep のとき → 受け付けない
- 攻撃開始時にステートを Attack に遷移（TryChangeState）
- _attackTimer に該当段の持続時間を設定
- Console ログ: `[Combo] N{step} 開始`

#### `UpdateCombo()` ★サーバー側で実行（FixedUpdate から呼ぶ）
- Attack ステート中のみ処理
- _attackTimer を減算
- _attackTimer が持続時間 × COMBO_WINDOW_RATIO 以下になったら _comboWindowOpen = true
- _attackTimer <= 0 になったら:
  - コンボ受付ウィンドウ中に次の入力がなかった → コンボ終了
  - _comboStep = 0、ステートを Idle に遷移
  - Console ログ: `[Combo] コンボ終了`

#### `GetCurrentAttackDuration(int step)` 
- step に応じた持続時間を返す（N1_DURATION 〜 N4_DURATION）

### コンボ中の移動
- Attack ステート中は移動不可（既に CharacterStateMachine で制御済みのはず）
- **コンボ中に方向転換は可能**: 攻撃開始時にスティック方向でキャラの向きを更新
  - PlayerMovement 側で、Attack ステート中は位置移動せず向きだけ更新

---

## 3. PlayerMovement.cs の修正

### 入力取得
- Update() で左クリック（Input.GetMouseButtonDown(0)）を検出
- PlayerInput.AttackPressed = true にする（押した瞬間のみ）

### サーバー側処理
- PlayerInput.AttackPressed == true のとき → ComboSystem.TryStartAttack() を呼ぶ

### 攻撃中の向き調整
- Attack ステート中に移動入力がある場合、位置は動かさないが向き（Y回転）だけ変更
- これにより「コンボ中に方向転換可能」を実現

---

## 4. CharacterStateMachine.cs の確認

以下が正しく動くか確認：
- Idle/Move → Attack の遷移が許可されている
- Attack → Attack の遷移が許可されている（コンボ継続用）
- Attack → Idle の遷移が許可されている（コンボ終了用）
- Attack 中に CanAcceptInput(NormalAttack) → true を返す

---

## 5. テスト内容
1. **左クリック** → `[Combo] N1 開始` ログ → 0.5秒後 `[Combo] コンボ終了` ログ
2. **素早く連打** → N1 → N2 → N3 → N4 と進む（ログで確認）
3. **N4の後に連打** → N4で止まる（N5には進まない）
4. **連打が遅い** → コンボ受付ウィンドウを逃すとコンボ終了
5. **攻撃中にWASD** → 位置は動かないが向きが変わる
6. **攻撃中にスペース** → ジャンプしない
7. **既存動作維持**: 移動・ジャンプ・ガードが壊れていない

---

## 6. 完了条件
- [ ] 左クリックで N1 攻撃が出る
- [ ] 連打で N1 → N2 → N3 → N4 と連鎖する
- [ ] N4 で最大段数に達する
- [ ] コンボ受付ウィンドウ外の入力はコンボを繋げない
- [ ] 攻撃中は移動不可、向きだけ変更可能
- [ ] サーバー権威でコンボ段数を管理
- [ ] 既存の移動・ジャンプ・ガードが壊れていない
- [ ] git commit & push: "M2-3a: 通常攻撃コンボ基盤 N1-N4"
