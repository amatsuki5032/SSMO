# 調査ログ

バグ調査・技術検証の記録。原因特定と再発防止に使う。

---

## 2026-02-25: 落下バグ調査

### 症状
プレイヤーが地面をすり抜けて落下する（可能性の調査）

### 調査項目と結果

#### 1. 地面生成タイミング
- `MapGenerator` は **MonoBehaviour**（NetworkBehaviour ではない）
- `Awake()` で生成 → プレイヤーの `OnNetworkSpawn` より前に実行される
- **問題なし**

#### 2. スポーン位置Y座標
- 全スポーン地点: Y=1.0（地面 Y=0 の 1m 上）
- CharacterController が重力で着地する想定
- **問題なし**

#### 3. 重力処理（PlayerMovement.cs L987-996）
```csharp
// 接地時: 下向きスティック力で斜面浮き防止
if (_controller.isGrounded && !_isJumping)
    _verticalVelocity = -2f;  // GROUND_STICK_FORCE
// 空中: 毎tick -0.333f ずつ加速
else if (!_controller.isGrounded)
    _verticalVelocity += -20f * 0.01667f;  // JUMP_GRAVITY * FIXED_DELTA_TIME
```
- Y=1 から落下 → 数tick で着地。**問題なし**

#### 4. 地面コライダー設定
| 項目 | 値 | 判定 |
|------|-----|------|
| 種別 | BoxCollider（MeshCollider から変更済み） | OK |
| isTrigger | false（デフォルト） | OK |
| レイヤー | Default | OK（CC も Default） |
| ワールドサイズ | 100m × 0.1m × 100m | OK |
| 表面Y座標 | Y=0.0 ぴったり | OK |

#### 5. 対応履歴
- `069cb237`: MeshCollider → BoxCollider に変更（すり抜け防止）

### 結論
コード上は問題なし。実際に発生した場合の確認ポイント:
1. ParrelSync クローン側で MapGenerator がシーンに存在するか
2. CharacterController の Skin Width / Step Offset（Prefab Inspector）
3. Console ログで実際のスポーンY座標を確認
