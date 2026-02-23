# M2-6b: 被弾リアクション（のけぞり/ダウン/打ち上げ）

## 概要
ヒット時に被弾者にリアクション（のけぞり、ダウン、打ち上げ等）を適用する。
サーバー権威でリアクション種別を決定し、ステートを遷移させる。

## 事前確認
- CharacterStateMachine.cs のヒットスタン系ステートを確認（Hitstun/Launch/Down 等）
- HealthSystem.cs の TakeDamage を確認
- GameConfig.cs の既存定数を確認
- docs/combat-spec.md セクション12（被弾リアクション）を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === 被弾リアクション ===
public const float HITSTUN_LIGHT_DURATION = 0.3f;    // のけぞり（軽）持続時間
public const float HITSTUN_HEAVY_DURATION = 0.5f;    // のけぞり（重）持続時間
public const float LAUNCH_HEIGHT = 4f;                // 打ち上げ高さ
public const float LAUNCH_DURATION = 1.0f;            // 打ち上げ受け身不能時間
public const float DOWN_DURATION = 1.5f;              // ダウン持続時間
public const float GETUP_INVINCIBLE_FRAMES = 30;      // 起き上がり無敵フレーム数（既存なら確認のみ）
public const float STUN_DURATION = 3.0f;              // 気絶持続時間
public const float KNOCKBACK_FORCE = 5f;              // 吹き飛ばし力
```

---

## 2. ReactionType の定義（CharacterState.cs に追加）

```csharp
public enum ReactionType : byte
{
    None = 0,
    Flinch,       // のけぞり（軽）
    Stagger,      // のけぞり（重）
    Launch,       // 打ち上げ
    Slam,         // 叩きつけ
    Knockback,    // 吹き飛ばし
    Down,         // ダウン
    Stun,         // 気絶
}
```

---

## 3. ReactionSystem.cs 新規作成（Assets/Scripts/Combat/）

### クラス設計
- NetworkBehaviour を継承
- CharacterStateMachine への参照を持つ

### ApplyReaction(ReactionType type) ★サーバー側で実行
- type に応じてステート遷移:
  - Flinch → Hitstun（軽持続時間）
  - Stagger → Hitstun（重持続時間）
  - Launch → Launch ステート + 垂直速度設定
  - Knockback → Hitstun + 水平方向に力を加える
  - Down → Down ステート
  - Stun → Stun ステート
- Console ログ: `[Reaction] {name} → {reactionType}`

### GetReactionType(int comboStep, bool isCharge, int chargeType) ★サーバー側
- 攻撃種別に応じたリアクションを決定:
  - N1〜N4: Flinch（のけぞり軽）
  - C1: Stagger（のけぞり重）
  - C2: Launch（打ち上げ）
  - C3: Flinch（連続）
  - C4: Knockback（吹き飛ばし）
  - C5: Launch（まとめて打ち上げ）
  - DashAttack: Flinch

---

## 4. HitboxSystem.cs の修正

### ヒット確定後のフロー
1. DamageCalculator でダメージ計算
2. HealthSystem.TakeDamage() でHP減少
3. ReactionSystem.ApplyReaction() でリアクション適用
4. ClientRpc でクライアントに通知

---

## 5. 打ち上げの物理処理
- Launch ステートに入ったら垂直速度を LAUNCH_HEIGHT に応じて設定
- FixedUpdate で重力適用（JUMP_GRAVITY を使用）
- 着地 → Down ステートに遷移

---

## 6. のけぞり中の無双脱出
- Hitstun ステート中に無双入力（MusouPressed）→ 無双発動で脱出（M2-8で実装）
- ここではフラグだけ準備: `_canMusouEscape = true`（Hitstun中のみ）

---

## 7. NetworkPlayer Prefab への追加
- ReactionSystem コンポーネントを追加

---

## 8. テスト内容
1. **N攻撃ヒット** → 被弾者が Hitstun ステートに遷移 + ログ
2. **C2 ヒット** → 被弾者が Launch ステートに遷移 → 空中 → Down
3. **C4 ヒット** → 被弾者が後方に吹き飛ぶ
4. **ダウン後** → 起き上がりで Idle に戻る
5. **既存動作維持**

---

## 9. 完了条件
- [ ] のけぞり（Flinch/Stagger）が動作する
- [ ] 打ち上げ（Launch）で空中に浮く
- [ ] 吹き飛ばし（Knockback）で後方移動
- [ ] ダウンから起き上がりで復帰
- [ ] リアクション中は行動不能
- [ ] サーバー権威でリアクション決定
- [ ] 既存動作が壊れていない
- [ ] git commit & push: "M2-6b: 被弾リアクション"
