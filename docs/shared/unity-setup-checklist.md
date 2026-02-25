# Prefab & シーン手動設定チェックリスト

> **用途:** Unity エディタでの手動 Add Component / 設定漏れ防止用。
> CC がスクリプトを追加した後、このリストで Prefab・シーンの設定を確認する。

---

## 1. NetworkPlayer Prefab

プレイヤーキャラクター（箱人間）。NetworkManager の Player Prefab に設定する。

### コンポーネント一覧（追加順）

| # | コンポーネント | 種別 | [RequireComponent] で自動追加 | 備考 |
|---|--------------|------|------|------|
| 1 | **NetworkObject** | NGO 必須 | — | NGO の基盤。最初に追加 |
| 2 | **CharacterController** | Unity 標準 | — | 物理移動。PlayerMovement が `[RequireComponent]` で要求 |
| 3 | **PlayerMovement** | Character/ | `CharacterController`, `CharacterStateMachine` を自動追加 | 入力収集・サーバー権威移動・クライアント予測 |
| 4 | **CharacterStateMachine** | Character/ | — | サーバー権威ステートマシン |
| 5 | **ComboSystem** | Combat/ | `CharacterStateMachine` を自動追加 | コンボ管理（N1-N6・C1-C6・E6-E9・ブレイクチャージ） |
| 6 | **HitboxSystem** | Combat/ | `ComboSystem` を自動追加 | サーバー権威ヒット判定 |
| 7 | **HurtboxComponent** | Combat/ | `CharacterStateMachine` を自動追加 | 被弾判定（無敵・ガード方向） |
| 8 | **HealthSystem** | Combat/ | `CharacterStateMachine` を自動追加 | HP 管理 |
| 9 | **EGSystem** | Combat/ | `CharacterStateMachine`, `MusouGauge` を自動追加 | エレメンタルガード |
| 10 | **MusouGauge** | Combat/ | `CharacterStateMachine`, `HealthSystem` を自動追加 | 無双ゲージ管理 |
| 11 | **ArmorSystem** | Combat/ | — | 5段階アーマー |
| 12 | **ReactionSystem** | Combat/ | `CharacterStateMachine`, `CharacterController` を自動追加 | 被弾リアクション（のけぞり・打ち上げ等） |
| 13 | **ElementSystem** | Combat/ | — | 属性システム（装備属性管理） |
| 14 | **StatusEffectManager** | Combat/ | — | 状態異常管理（燃焼・鈍足・感電） |
| 15 | **EnhancementRing** | Combat/ | `ComboSystem` を自動追加 | 仙箪強化リング |
| 16 | **UltimateEnhancement** | Combat/ | `ArmorSystem` を自動追加 | 究極強化（30秒バフ） |
| 17 | **DebugTestHelper** | Debug/ | — | デバッグ用（`#if UNITY_EDITOR` 限定） |

### RequireComponent 依存ツリー

```
PlayerMovement
  ├── [RequireComponent] CharacterController
  └── [RequireComponent] CharacterStateMachine

ComboSystem
  └── [RequireComponent] CharacterStateMachine

HitboxSystem
  └── [RequireComponent] ComboSystem
        └── [RequireComponent] CharacterStateMachine

HurtboxComponent
  └── [RequireComponent] CharacterStateMachine

HealthSystem
  └── [RequireComponent] CharacterStateMachine

EGSystem
  ├── [RequireComponent] CharacterStateMachine
  └── [RequireComponent] MusouGauge
        ├── [RequireComponent] CharacterStateMachine
        └── [RequireComponent] HealthSystem

ReactionSystem
  ├── [RequireComponent] CharacterStateMachine
  └── [RequireComponent] CharacterController

EnhancementRing
  └── [RequireComponent] ComboSystem

UltimateEnhancement
  └── [RequireComponent] ArmorSystem
```

### GetComponent 依存（[RequireComponent] では保証されない手動確認項目）

| スクリプト | GetComponent で取得する対象 | 用途 |
|-----------|--------------------------|------|
| PlayerMovement | `ComboSystem` | 攻撃入力の委譲 |
| PlayerMovement | `EGSystem` | EG 入力処理の委譲 |
| PlayerMovement | `MusouGauge` | 無双入力処理の委譲 |
| PlayerMovement | `EnhancementRing` | 仙箪強化リング発動の委譲 |
| HitboxSystem | `CharacterStateMachine` | 無双ステート判定 |
| HitboxSystem | `CharacterController` | 攻撃前進移動 |
| HitboxSystem | `PlayerMovement` | 武器種取得 |
| HitboxSystem | `ElementSystem` | 攻撃時の属性情報取得 |
| ReactionSystem | `ArmorSystem` | のけぞり判定（アーマー比較） |
| EGSystem | `MusouGauge` | ゲージ消費 |
| StatusEffectManager | `CharacterStateMachine` | 状態異常フラグ付与/解除 |
| StatusEffectManager | `HealthSystem` | 燃焼ダメージ適用 |
| EnhancementRing | `ComboSystem` | 仙箪カウント参照・連撃強化 |
| EnhancementRing | `UltimateEnhancement` | 究極強化の発動 |
| UltimateEnhancement | `ArmorSystem` | アーマーレベル変更 |

### 注意: 手動追加不要のコンポーネント

| コンポーネント | 理由 |
|--------------|------|
| CameraController | PlayerMovement.OnNetworkSpawn でオーナー専用に動的生成される |

---

## 2. NPCSoldier Prefab

NPC 兵士（雑兵）。NPCSpawner の `_npcSoldierPrefab` に設定する。

### コンポーネント一覧

| # | コンポーネント | 種別 | 備考 |
|---|--------------|------|------|
| 1 | **NetworkObject** | NGO 必須 | NPCSpawner が Spawn() する前提 |
| 2 | **NetworkTransform** | NGO | サーバー権威の位置同期（NPCSoldier はサーバーで移動） |
| 3 | **NPCSoldier** | Server/ | AI 行動・HP 管理・チーム色分け |
| 4 | **CapsuleCollider** | Unity 標準 | 物理判定（HitboxSystem の OverlapSphere で検出される） |

### 手動追加不要のコンポーネント

| コンポーネント | 理由 |
|--------------|------|
| ビジュアル（Cube） | NPCSoldier.CreateVisual() で動的生成 |
| BoxCollider（ビジュアル子オブジェクト） | CreateVisual() で Destroy される |

### NPCSpawner での設定

| フィールド | Inspector で設定する内容 |
|-----------|----------------------|
| `_npcSoldierPrefab` | NPCSoldier Prefab をドラッグ＆ドロップ |

---

## 3. SentanItem Prefab

仙箪アイテム（NPC 死亡時ドロップ）。NPCSpawner の `_sentanItemPrefab` に設定する。

### コンポーネント一覧

| # | コンポーネント | 種別 | 備考 |
|---|--------------|------|------|
| 1 | **NetworkObject** | NGO 必須 | NPCSpawner が Spawn() する前提 |
| 2 | **SentanItem** | Combat/ | 自動拾い・寿命管理 |

### 手動追加不要のコンポーネント

| コンポーネント | 理由 |
|--------------|------|
| SphereCollider | SentanItem.Awake() で自動追加（isTrigger=true） |
| Rigidbody | SentanItem.Awake() で自動追加（isKinematic=true） |
| ビジュアル（Sphere） | SentanItem.OnNetworkSpawn() の CreateVisual() で動的生成 |

### NPCSpawner での設定

| フィールド | Inspector で設定する内容 |
|-----------|----------------------|
| `_sentanItemPrefab` | SentanItem Prefab をドラッグ＆ドロップ |

---

## 4. GameManager（シーンオブジェクト）

シーン上の空の GameObject に以下のコンポーネントをアタッチする。

### コンポーネント一覧

| # | コンポーネント | 種別 | 備考 |
|---|--------------|------|------|
| 1 | **NetworkObject** | NGO 必須 | NetworkBehaviour を持つコンポーネントが複数あるため |
| 2 | **TeamManager** | Server/ | チーム管理（シングルトン） |
| 3 | **SpawnManager** | Server/ | スポーン地点管理（シングルトン） |
| 4 | **MapGenerator** | Server/ | バトルマップ生成（MonoBehaviour。Awake で自動実行） |
| 5 | **NPCSpawner** | Server/ | NPC 兵士スポーン管理（シングルトン）。Inspector で Prefab 設定が必要 |
| 6 | **GameModeManager** | Server/ | ゲームモード管理（シングルトン） |
| 7 | **HelloNetwork** | Netcode/ | 接続 UI（Host/Client/Server ボタン表示。MonoBehaviour） |

### NPCSpawner の Inspector 設定

| フィールド | 設定内容 |
|-----------|---------|
| `Npc Soldier Prefab` | NPCSoldier Prefab |
| `Sentan Item Prefab` | SentanItem Prefab |

---

## 5. NetworkManager の設定確認項目

シーン上の NetworkManager オブジェクト（NGO 自動生成 or 手動作成）。

| 設定項目 | 値 | 備考 |
|---------|-----|------|
| **Player Prefab** | NetworkPlayer Prefab | 接続時に自動スポーンされる |
| **Protocol Type** | Unity Transport | デフォルトのまま |
| **Tick Rate** | 60 | `GameConfig.SERVER_TICK_RATE` に合わせる |
| **Network Prefabs List** | 以下の3つを登録 | — |
| └ NetworkPlayer Prefab | — | Player Prefab と同じもの |
| └ NPCSoldier Prefab | — | ランタイムスポーン用 |
| └ SentanItem Prefab | — | ランタイムスポーン用 |

### 補助コンポーネント（NetworkManager と同じオブジェクト or 別オブジェクト）

| コンポーネント | 種別 | 備考 |
|--------------|------|------|
| **Unity Transport** | NGO Transport | NetworkManager と同じオブジェクトに自動追加される |
| **NetworkStatsHUD** | UI/ | ネットワーク統計表示（MonoBehaviour。任意のオブジェクトに配置可） |

---

## 6. シーン上に必要なオブジェクト一覧

| オブジェクト名 | 必須コンポーネント | 備考 |
|--------------|------------------|------|
| **NetworkManager** | NetworkManager, Unity Transport | NGO の基盤。1つのみ |
| **GameManager** | NetworkObject, TeamManager, SpawnManager, MapGenerator, NPCSpawner, GameModeManager, HelloNetwork | セクション4参照 |
| **Main Camera** | Camera, AudioListener | PlayerMovement がオーナー用に CameraController を動的にアタッチする |
| **Directional Light** | Light | シーンの照明（Unity デフォルト） |

### 注意事項

- **MapGenerator** が Awake で地面・壁・拠点・障害物を自動生成するため、手動でマップオブジェクトを配置する必要はない
- **BasePoint** は MapGenerator が拠点生成時に動的に AddComponent するため、手動追加不要
- **LagCompensationManager** はシングルトンで遅延初期化（`Instance` アクセス時に自動生成）のため、シーン配置不要

---

## クイックチェック手順

### 新規セットアップ時

1. [ ] シーンに **NetworkManager** オブジェクトを配置（Unity Transport 付き）
2. [ ] シーンに **GameManager** オブジェクトを作成（空 GameObject）
3. [ ] GameManager に NetworkObject → TeamManager → SpawnManager → MapGenerator → NPCSpawner → GameModeManager → HelloNetwork を追加
4. [ ] **NetworkPlayer Prefab** を作成（セクション1の全17コンポーネント）
5. [ ] **NPCSoldier Prefab** を作成（セクション2の全4コンポーネント）
6. [ ] **SentanItem Prefab** を作成（セクション3の全2コンポーネント）
7. [ ] NetworkManager の Player Prefab に NetworkPlayer Prefab を設定
8. [ ] NetworkManager の Network Prefabs List に 3つの Prefab を登録
9. [ ] NPCSpawner の Inspector に NPCSoldier Prefab と SentanItem Prefab を設定
10. [ ] NetworkManager の Tick Rate を **60** に設定

### CC がスクリプトを追加した後

1. [ ] 新しいスクリプトが NetworkPlayer Prefab 用 → Prefab に Add Component
2. [ ] 新しいスクリプトが NPC 用 → NPCSoldier Prefab に Add Component
3. [ ] 新しい Prefab が必要 → Prefab 作成 + NetworkManager の Network Prefabs List に登録
4. [ ] `Ctrl+S` で Prefab 変更を保存

### UI コンポーネント（配置自由・MonoBehaviour）

以下の UI スクリプトは任意のオブジェクトに配置可能（OnGUI ベース）:

| スクリプト | 推奨配置先 | 備考 |
|-----------|-----------|------|
| NetworkStatsHUD | NetworkManager or 専用 UI オブジェクト | RTT・PacketLoss 表示 |
| BattleHUD | 専用 UI オブジェクト | HP・無双ゲージ・ターゲット表示 |
| MinimapHUD | 専用 UI オブジェクト | ミニマップ表示 |
| ScoreboardHUD | 専用 UI オブジェクト | タイマー・スコア・勝敗表示 |
| EnhancementRingHUD | 専用 UI オブジェクト | 仙箪カウント・リングスロット表示 |
