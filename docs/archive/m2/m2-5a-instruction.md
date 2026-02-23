# M2-5a: Hitbox/Hurtbox コンポーネント

## 概要
攻撃の当たり判定を行うための Hitbox（攻撃判定）と Hurtbox（被弾判定）のコンポーネントを作成する。
サーバー側でのみ最終判定を行う。

## 事前確認
- ComboSystem.cs のコンボ段数・チャージタイプを確認
- CharacterStateMachine.cs の Attack/Charge ステートを確認
- GameConfig.cs の既存定数を確認
- docs/combat-spec.md のヒット判定関連を参照

---

## 1. GameConfig.cs に定数追加

```csharp
// === ヒット判定 ===
public const float DEFAULT_HITBOX_RADIUS = 0.5f;     // デフォルト Hitbox 半径
public const float DEFAULT_HITBOX_LENGTH = 1.5f;      // デフォルト Hitbox 長さ（カプセル）
public const float DEFAULT_HURTBOX_RADIUS = 0.4f;     // デフォルト Hurtbox 半径
public const float DEFAULT_HURTBOX_HEIGHT = 1.8f;     // デフォルト Hurtbox 高さ
public const int MAX_HIT_TARGETS_PER_FRAME = 30;      // 1フレームあたり最大ヒット数
```

---

## 2. HitboxData.cs 新規作成（Assets/Scripts/Combat/）

攻撃ごとの Hitbox パラメータを定義するデータクラス（ScriptableObject ではなく構造体で簡易実装）。

```csharp
public struct HitboxData
{
    public float Radius;       // 判定半径
    public float Length;       // 判定長さ（前方方向）
    public Vector3 Offset;     // キャラ中心からのオフセット
    public int ActiveStartFrame;  // アクティブ開始フレーム
    public int ActiveEndFrame;    // アクティブ終了フレーム
    public bool MultiHit;     // 多段ヒットか
    public int MaxHitCount;    // 多段の場合の最大ヒット数
}
```

### 攻撃種別ごとの HitboxData テーブル
- GetHitboxData(int comboStep, bool isCharge, int chargeType) メソッドで取得
- N1〜N4、C1〜C5 のそれぞれに仮の HitboxData を設定
- 将来は武器種ごとに異なるが、今は共通の仮値

---

## 3. HitboxSystem.cs 新規作成（Assets/Scripts/Combat/）

### クラス設計
- NetworkBehaviour を継承
- ComboSystem への参照を持つ
- [RequireComponent(typeof(ComboSystem))]

### サーバー側メソッド

#### `CheckHitbox()` ★サーバー側で実行（FixedUpdate）
- Attack/Charge/DashAttack ステート中のみ処理
- 現在のフレームが HitboxData の ActiveStartFrame〜ActiveEndFrame 内か判定
- アクティブフレーム内なら:
  - キャラ位置 + Offset + 前方方向 × Length でカプセル領域を計算
  - Physics.OverlapCapsule でその領域内の Collider を取得
  - Hurtbox コンポーネントを持つ対象をフィルタ
  - 自分自身を除外
  - 味方を除外（将来のチーム判定用、今は全員敵として扱う）
  - ヒット済みリスト（_hitTargetsThisAttack）に含まれていなければヒット
  - ヒット対象を _hitTargetsThisAttack に追加（1攻撃1ヒット）
  - Console ログ: `[Hit] {attacker} → {target} ヒット`

#### `ResetHitTargets()`
- 新しい攻撃が始まったときに _hitTargetsThisAttack をクリア

### 1攻撃1ヒットのルール
- 1つの攻撃モーション中、同じ対象には1回だけヒット
- 多段技（C5等）は HitboxData.MultiHit = true で複数回ヒット可能
- _hitTargetsThisAttack: HashSet<ulong>（NetworkObjectId で管理）

---

## 4. HurtboxComponent.cs 新規作成（Assets/Scripts/Combat/）

### シンプルなコンポーネント
- NetworkBehaviour を継承
- カプセル型の判定領域（CharacterController と同サイズ）
- OwnerClientId / NetworkObjectId の参照を提供
- IsInvincible() → CharacterStateMachine の無敵判定を返す

---

## 5. NetworkPlayer Prefab への追加
- HitboxSystem コンポーネントを追加
- HurtboxComponent コンポーネントを追加
- ★ Collider は既存の CharacterController を使う（追加不要）

---

## 6. テスト内容
1. **左クリックで攻撃** → N1 のアクティブフレーム中に近くのプレイヤーがいれば `[Hit]` ログ
2. **距離が遠い** → ヒットしない
3. **同じ攻撃で2回ヒットしない** → 1攻撃1ヒット
4. **新しい攻撃（N2）** → 再度ヒット可能
5. **ParrelSync**: 2人接続で攻撃→被弾ログが出る

---

## 7. 完了条件
- [ ] HitboxData 構造体が定義されている
- [ ] HitboxSystem がサーバー側でヒット判定を行う
- [ ] HurtboxComponent が被弾判定を提供する
- [ ] 1攻撃1ヒットが守られている
- [ ] ヒット時にログが出る
- [ ] 既存の攻撃・移動・ガードが壊れていない
- [ ] git commit & push: "M2-5a: Hitbox/Hurtbox コンポーネント"
