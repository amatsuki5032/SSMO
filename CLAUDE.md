# SSMO - Claude Code プロジェクトガイド

## プロジェクト概要
真三國無双Online風の **4v4 近接アクション対戦ゲーム**。
Unity 6.3 LTS (C#) + Netcode for GameObjects (NGO) で開発中。

## 仕様の信頼性ルール
- **戦闘仕様の唯一の正 (Single Source of Truth) は `docs/shared/combat-spec.md`**
- CLAUDE.md や指示書と combat-spec.md で矛盾がある場合、**combat-spec.md を優先**する
- CLAUDE.md はあくまでCCへの作業ガイド。仕様の詳細判断は必ず combat-spec.md を参照すること

## 最重要原則
1. **サーバー権威型**: ゲーム状態の正解はサーバーが持つ。クライアントの値は一切信用しない
2. **ネットワークは後付けできない**: 全ての機能はオンライン前提で設計・実装する
3. **固定ティック**: ゲームロジックは FixedUpdate 60Hz 固定。描画FPSは可変
4. **動くものを最速で作る**: 見た目より動作優先。箱人間でいいから動かす

## 技術スタック
- **エンジン**: Unity 6.3 LTS (6000.3.9f1)
- **言語**: C#
- **ネットワーク**: Netcode for GameObjects (NGO) 2.9.2
- **トランスポート**: Unity Transport
- **マルチテスト**: ParrelSync 1.5.2
- **ネットワーク統計**: Multiplayer Tools 2.2.8
- **認証/DB**: Firebase Auth / Firestore (将来)

## ファイル構成（実際のコードに準拠）

### Assets/Scripts/ 全ファイル一覧

| パス | クラス名 | 役割 |
|------|---------|------|
| `Character/PlayerMovement.cs` | PlayerMovement | 入力収集・サーバー権威移動・クライアント予測・リコンシリエーション |
| `Character/CameraController.cs` | CameraController | 3人称カメラ（MonoBehaviour・オーナー専用・壁衝突回避） |
| `Character/CharacterStateMachine.cs` | CharacterStateMachine | サーバー権威ステートマシン（NetworkVariable同期・遷移バリデーション・無敵管理） |
| `Combat/ComboSystem.cs` | ComboSystem | コンボ管理（N1-N4・C1-C5派生・先行入力バッファ・ダッシュ攻撃） |
| `Combat/HitboxSystem.cs` | HitboxSystem | サーバー権威ヒット判定（OverlapCapsule・ラグコンペンセーション・ガード判定） |
| `Combat/HitboxData.cs` | HitboxData | 全攻撃種別のヒットボックスパラメータ（アクティブフレーム・範囲・オフセット） |
| `Combat/HurtboxComponent.cs` | HurtboxComponent | 被弾判定コンポーネント（無敵チェック・ガード方向判定・めくり検出） |
| `Combat/HealthSystem.cs` | HealthSystem | HP管理（NetworkVariable同期・TakeDamage・FullHeal・Dead遷移） |
| `Combat/ReactionSystem.cs` | ReactionSystem | 被弾リアクション（のけぞり・打ち上げ・吹き飛ばし・ダウン4種） |
| `Combat/ArmorSystem.cs` | ArmorSystem | 5段階アーマー（攻撃レベル比較でのけぞり判定） |
| `Combat/EGSystem.cs` | EGSystem | エレメンタルガード（準備・完成・カウンター・無双ゲージ連携） |
| `Combat/MusouGauge.cs` | MusouGauge | 無双ゲージ管理（チャージ・消費・乱舞発動・真無双判定） |
| `Shared/GameConfig.cs` | GameConfig | ゲーム全体の定数・設定値（サーバー/クライアント共有） |
| `Shared/DamageCalculator.cs` | DamageCalculator | ダメージ計算（サーバー専用。モーション倍率・属性・根性補正・空中補正） |
| `Shared/CharacterState.cs` | CharacterState等 | enum定義（CharacterState・HitReaction・AttackLevel・ArmorLevel・ElementType・GamePhase等） |
| `Shared/PlayerInput.cs` | PlayerInput | 入力構造体（INetworkSerializable。移動・攻撃・ガード・チャージ等） |
| `Netcode/LagCompensationManager.cs` | LagCompensationManager | ラグコンペンセーション（ワールドスナップショット・Rewindスコープ） |
| `Netcode/HelloNetwork.cs` | HelloNetwork | 接続確認用（M0で作成） |
| `UI/NetworkStatsHUD.cs` | NetworkStatsHUD | ネットワーク統計表示（RTT・PacketLoss） |
| `UI/BattleHUD.cs` | BattleHUD | 戦闘HUD（自キャラHP・無双ゲージ・ターゲットHP） |
| `UI/MinimapHUD.cs` | MinimapHUD | ミニマップ（プレイヤー位置・拠点表示・全体マップ切替） |
| `UI/ScoreboardHUD.cs` | ScoreboardHUD | スコアボード（タイマー・チーム撃破数・勝敗表示） |
| `Server/TeamManager.cs` | TeamManager | チーム管理（サーバー権威・自動振り分け・NetworkList同期） |
| `Server/SpawnManager.cs` | SpawnManager | スポーン地点管理（チーム別配置・リスポーン・交互拠点制限） |
| `Server/MapGenerator.cs` | MapGenerator | バトルマップ生成（地面・外壁・拠点5箇所・障害物） |
| `Server/BasePoint.cs` | BasePoint | 拠点システム（サーバー権威・制圧ゲージ・HP自動回復） |
| `Server/NPCSoldier.cs` | NPCSoldier | NPC兵士（サーバー権威・簡易HP・チーム色分け・自動移動） |
| `Server/NPCSpawner.cs` | NPCSpawner | NPC兵士スポーン管理（拠点ごと定期スポーン・上限管理） |
| `Server/GameModeManager.cs` | GameModeManager | ゲームモード管理（フェーズ・タイマー・スコア・勝敗） |
| `Debug/DebugTestHelper.cs` | DebugTestHelper | デバッグ用テストヘルパー（Editor限定・F1-F10キー操作） |

### NetworkPlayer Prefab コンポーネント一覧（追加順）

1. NetworkObject（NGO必須）
2. CharacterController（物理移動）
3. PlayerMovement（入力・移動同期）
4. CharacterStateMachine（ステート管理）
5. ComboSystem（コンボ管理）
6. HitboxSystem（ヒット判定）
7. HurtboxComponent（被弾判定）
8. HealthSystem（HP管理）
9. EGSystem（エレメンタルガード）
10. MusouGauge（無双ゲージ）
11. ArmorSystem（アーマー）
12. ReactionSystem（被弾リアクション）
13. DebugTestHelper（デバッグ用、Editor限定）

### ドキュメント構成

```
docs/
├── shared/                     # claude.ai プロジェクトナレッジと同期するファイル
│   ├── combat-spec.md          # ★ 戦闘仕様の正（Single Source of Truth）
│   ├── code-reference.md       # コードAPI参照
│   └── ssmo-system-prompt.md   # 仕様収集用プロンプト
├── design/                     # 設計メモ（参考用）
│   ├── combat-design.md
│   └── netcode-design.md
├── archive/m2/                 # M2指示書アーカイブ（参照用）
└── progress.html               # 進捗トラッカー（PROGRESS_DATA）
```

## ネットワークアーキテクチャ

### ティックレート設計
| 項目 | 値 |
|------|------|
| ゲームロジック (FixedUpdate) | 60Hz 固定 (0.01667秒) |
| サーバーティック | 60Hz 固定 |
| クライアント→サーバー送信 | 30Hz |
| サーバー→クライアント配信 | 30Hz |
| 描画FPS | 可変 (60/120/144/無制限) |

### ラグ対策の3本柱
1. **クライアント予測**: 入力を即座にローカル実行。サーバー結果とズレたら巻き戻し再計算
2. **補間 (Interpolation)**: 他プレイヤーの位置を100ms遅延で滑らかに表示
3. **ラグコンペンセーション**: 攻撃時にサーバーが攻撃者の時刻まで巻き戻してヒット判定（最大150ms）

### 同期データ (毎ティック, 18 bytes/プレイヤー)
- 位置 (Vector3): 12 bytes
- 回転 (Y軸): 2 bytes
- ステート (enum): 1 byte
- コンボ段数: 1 byte
- HP: 2 bytes

## 戦闘システム（M2で確定した仕様）

※ 詳細仕様は `docs/shared/combat-spec.md` を参照（正の情報源）

### コンボ構造
- 通常攻撃 (□): 無強化 N1→N4 / 連撃強化1回 N5 / 2回 N6 / 3回+無双MAX E6→E9
- チャージ攻撃 (△): N[x]中に△ で C[x+1] に派生（C1〜C6、武器種固有）。最終段からは派生不可（無強化→C4まで）
- ダッシュ攻撃: 一定時間移動後に □ で発動（Nコンボとは別系統）
- 先行入力バッファ: 150ms
- コンボ受付ウィンドウ: 各攻撃モーションの最後30%フレーム

### ダメージ計算式 (★サーバー側のみ★)
```
1. 攻撃倍率 = モーション倍率 × 属性倍率（チャージ攻撃のみ属性が乗る）
2. 基礎ダメージ = ATK × 攻撃倍率
3. 防御計算 = 基礎ダメージ × (100 / (100 + DEF))   ※斬属性は DEF=0
4. 空中補正 = 空中被弾時 ÷2
5. 根性補正 = HP青(50-100%):÷1 / HP黄(20-50%):÷1.5 / HP赤(0-20%):÷2
6. 斬保証 = 斬属性の場合 max(最終ダメージ, 斬固定値)
7. 最低保証 = max(最終ダメージ, 1)
8. クリティカル = 5%確率で ×1.5
※ ガード成功時はダメージ0（完全カット）。計算自体をスキップする
※ ガード不可技は存在しない。崩し手段はめくり（背面攻撃）のみ
```

### ガード（SSMO本家準拠・M2で確定）
- L1 押しっぱなしで正面180度を防御（**ダメージ完全カット = 0ダメージ**）
- 通常ガード時は**ノックバックあり**（多少押される）
- **ガード不可技は存在しない**（無双乱舞・C1含め全攻撃ガード可能）
- **崩し手段はめくり（側面・背面攻撃）のみ**
- ガード移動可能（正面向いたまま）
- エレメンタルガード (EG): ガード中に△約1秒押し込みで発動、カウンター攻撃
- **EG中はノックバックなし**（その場で完全に受け止める）
- ジャストガードシステムは**なし**（EGに置換）

### 属性システム（5種 + 無属性）
- **属性同士の相性なし**
- **チャージ攻撃にのみ属性が乗る**（通常攻撃には乗らない）
- 炎: 燃焼（持続ダメージ、HP0にはしない）
- 氷: 凍結（確率発動、約2秒行動不能）
- 雷: 感電（受け身不可）+ 気絶（地上のみ約3秒行動不可）
- 風: 鈍足（移動低下+ジャンプ不可）
- 斬: 防御無視 + HP&無双両方にダメージ（攻撃側無双も減少）

### アーマーシステム（5段階）
| 段階 | 耐性 |
|------|------|
| 1 通常 | なし（全てのけぞる） |
| 2 矢耐性 | 雑魚の矢でのけぞらない |
| 3 N耐性 | 通常攻撃でのけぞらない |
| 4 SA | チャージでものけぞらない |
| 5 HA | 無双でものけぞらない |

※ アーマーはのけぞり無効化のみ。ダメージは常に通る

### M2で確定したその他の仕様
- **ヒットストップなし**（常時戦闘が流れるスピード感を重視）
- **覚醒システムなし**（SSMOに存在しない。究極強化は仙箪システムでM4以降に検討）

### ステートマシン
**基本**: Idle / Move / Jump / JumpAttack
**攻撃**: Attack(N) / Charge(C) / DashAttack(D) / DashRush / BreakCharge
**防御**: Guard / GuardMove / EG準備 / EG完成 / EGCounter
**無双**: MusouCharge / Musou / TrueMusou
**被弾**: Hitstun / Launch / AirHitstun / AirRecover / Slam
**ダウン(4種)**: FaceDownDown(前のめり) / CrumbleDown(崩れ落ち) / SprawlDown(仰向け) / Stun(気絶)
**復帰**: Getup(起き上がり・無敵)
**状態異常**: Freeze(氷) / Electrified(雷) / Burn(炎) / Slow(風)
**死亡**: Dead

※ ステート遷移の最終決定権はサーバー

## 開発ロードマップ
- [x] **M0** (Week 1-2): リポジトリ & 環境構築
- [x] **M1** (Week 3-8): ネットワーク同期基盤（クライアント予測・補間・ラグ補正）
- [x] **M2** (Week 9-22): 戦闘アクション（コンボ・ヒット判定・ガード・ジャンプ・無双）
- [ ] **M3** (Week 23-28): 4v4 対戦モード（マッチメイキング・マップ・AI）
- [ ] **M4** (Week 29-38): キャラクター & コンテンツ（武器種・育成・仙箪強化）
- [ ] **M5** (Week 39-44): インフラ & チート対策
- [ ] **M6** (Week 45-52): ポリッシュ & α版リリース

## 現在の状態 (M3 進行中)

### M0: 環境構築 (完了)
- リポジトリ作成・Git LFS設定
- Unity 6.3 LTS + NGO 2.9.2 + ParrelSync 1.5.2

### M1: ネットワーク同期基盤 (完了)
- M1-1: NetworkPlayer Prefab（箱人間 + NetworkObject + CharacterController）
- M1-2: サーバー権威型 移動同期（NetworkVariable + ServerRpc）
- M1-3: クライアント予測 + リコンシリエーション（巻き戻し再計算）
- M1-4: 他プレイヤー補間表示（100ms遅延 + スナップ閾値）
- M1-5: ラグコンペンセーション基盤（ワールドスナップショット + 巻き戻しAPI）
- M1-6: ネットワーク統計HUD（RTT / PacketLoss）

### M2: 戦闘アクション (完了)
- M2-1: キャラクターステートマシン（サーバー権威遷移・無敵管理・自動タイマー）
- M2-2: 入力・ジャンプ・ダッシュ・ガード移動（PlayerInput構造体）
- M2-3: 通常攻撃コンボ N1-N4 + 先行入力バッファ 150ms
- M2-4: チャージ攻撃 C1-C5 + C3ラッシュ + ダッシュ攻撃 + ダッシュラッシュ
- M2-5: Hitbox/Hurtbox サーバー権威ヒット判定 + ラグコンペンセーション連携
- M2-6: 被弾リアクション（のけぞり・打ち上げ・吹き飛ばし・ダウン4種）
- M2-7: ダメージシステム（HP管理・ガード判定・EGカウンター）
- M2-8: 無双乱舞 + 真無双 + 無双ゲージ管理
- M2-9: アーマーシステム（5段階 × 攻撃レベル比較）
- M2-10: ガードシステム本家準拠（完全カット・めくりのみ・EGノックバックなし）
- デバッグテストヘルパー（F1-F10操作・OnGUI表示）

### M3: 4v4 対戦モード (進行中)
- M3-1a: チーム管理基盤（TeamManager・自動振り分け・NetworkList同期）
- M3-1b: スポーン地点管理（SpawnManager・チーム別配置・リスポーン）
- M3-2: 3人称カメラシステム（CameraController・壁衝突回避）
- M3-3: バトルマップ生成（MapGenerator・100m×100m・拠点5箇所）
- M3-4a: 拠点システム基盤（BasePoint・制圧ゲージ・HP自動回復）
- M3-5a: NPC兵士スポーンシステム（NPCSpawner・拠点ごと定期スポーン）
- M3-5b: NPC兵士AI行動（NPCSoldier・敵検出・攻撃・ガード判定）
- M3-6a: 戦闘HUD基盤（BattleHUD・HP/無双ゲージ/ターゲット表示）
- M3-6b: ミニマップ（MinimapHUD・プレイヤー/拠点位置表示・全体マップ切替）
- M3-7: ゲームモード管理（GameModeManager・タイマー・スコア・勝敗判定）

### 次のステップ
1. M3残タスク: テスト（真無双・アーマー・めくり）
2. M4: キャラクター＆コンテンツ（WeaponData基盤 → 武器種パラメータ → 属性システム → 連撃強化 → 仙箪・鍛錬・刻印）

## 開発ワークフロー & CC運用ノウハウ

### 開発フロー
1. **Claude (claude.ai)** で指示書を作成 → `docs/` に配置
2. **Claude Code (CC)** に指示書の内容をコピペで投入
3. CC完了後、**Prefab へのコンポーネント追加**を忘れずに手動実行
4. **ParrelSync** で2人接続テスト
5. 問題なければ CC が `git add -A && git commit && git push`

### CC運用の鉄則
| ルール | 理由 |
|--------|------|
| **エラー → 即 exit → CC再起動** | 壊れたコンテキストが残ると詰まる |
| **5分超 thinking = 詰まり** | 小さいタスクは2-3分が正常 |
| **Unity Playモード中はCC避ける** | Play中の変更はPlay停止時に巻き戻される |
| **指示は1つずつシンプルに** | 複数指示は混乱の元 |
| **既存コードを先に確認させる** | 指示に「★実装前にAssets/Scripts/以下を確認」を含める |

### よくあるミス & 対策
| ミス | 対策 |
|------|------|
| Prefab への Add Component 忘れ | 新スクリプト追加時は必ず確認。CCの出力に手動操作があれば即実行 |
| Unity Play停止し忘れてCC指示 | CCに投げる前に必ずPlay停止を確認する習慣をつける |
| 旧仕様が指示書に紛れ込む | 指示書に「combat-spec.md を正とする」を含める |
| CCが既存メソッドを無視して直接書き換え | 「既存のpublicメソッド/NetworkVariableを経由すること」を指示に含める |

### テスト時の注意
- 新コンポーネント追加後は **必ず Prefab に手動 Add Component + Ctrl+S**
- ParrelSync クローンが起動しない → `Library` フォルダ削除 → 再起動
- Console ログで判定確認（アニメーションなしの箱人間なので視覚的確認は限定的）

## コーディング規約

### 命名規則
- クラス名: PascalCase (例: `DamageCalculator`)
- メソッド: PascalCase (例: `CalculateDamage`)
- フィールド (private): camelCase with _ prefix (例: `_currentHealth`)
- フィールド (public): PascalCase (例: `MaxHealth`)
- 定数: UPPER_SNAKE_CASE (例: `MAX_PLAYERS`)
- enum値: PascalCase (例: `CharacterState.Idle`)

### コメント
- コードコメントは日本語で書く
- 設計意図（なぜこのアプローチか）を必ずコメントに含める

### ネットワーク関連の規約
- サーバー専用処理は `[ServerRpc]` または `if (IsServer)` で明示
- クライアント専用処理は `[ClientRpc]` または `if (IsClient)` で明示
- 共有ロジック (ダメージ計算等) は `Scripts/Shared/` に配置
- NetworkVariable には `[SerializeField]` ではなく NetworkVariable<T> を使用
- RPC メソッド名は `〜ServerRpc` / `〜ClientRpc` サフィックスを付ける

### ファイル配置ルール
- ネットワーク同期コード → `Scripts/Netcode/`
- 戦闘ロジック → `Scripts/Combat/`
- キャラ制御 → `Scripts/Character/`
- UI → `Scripts/UI/`
- サーバー/クライアント共有の定数・計算式 → `Scripts/Shared/`
- サーバー専用ロジック (AI等) → `Scripts/Server/`
- デバッグ専用（Editor限定） → `Scripts/Debug/`

### 重要な注意事項
- **ダメージ計算は必ずサーバー側で実行**。クライアント値を絶対に信用しない
- **無敵状態 (無双乱舞・ジャンプ離陸・起き上がり) はサーバーのみが管理**
- **ヒット判定はサーバー権威**。クライアントは演出（エフェクト）のみ
- **ステート遷移の最終決定権はサーバー**。クライアントは予測遷移するが否認されたら巻き戻る
- **投射物 (弓矢等) の衝突判定もサーバーで実行**
- **アーマー・属性・状態異常の管理はサーバー権威**

## 作業見積もり

タスク指示を受けたとき、コードを書き始める前に以下を提示すること：

| 項目 | 内容 |
|------|------|
| CC作業時間（予想） | 例: 25〜35分 |
| 変更ファイル数 | 例: 3ファイル（新規1 + 修正2） |
| 変更規模 | ★〜★★★★★（5段階） |
| テスト含む合計 | 例: 40分（コンパイル確認 + ParrelSync テスト含む） |

これにより開発者が待ち時間を有効活用できる。

## 参考リソース
- [Gabriel Gambetta: Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-server-game-architecture.html) - ネットコードの必読資料
- [Unity NGO Documentation](https://docs-multiplayer.unity3d.com/netcode/current/about/) - 公式ドキュメント
- [GDC: Overwatch Gameplay Architecture and Netcode](https://www.youtube.com/watch?v=W3aieHjyNvw) - ラグ補正の実例
- [Valve: Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) - クライアント予測の原典

## 進捗管理
- タスク完了時は `docs/progress.html` の `PROGRESS_DATA` を更新すること
- 更新対象: `status`, `details[].done`, `lastUpdated`, マイルストーンの `progress`
- status は "done" / "inprogress" / "todo" のいずれか
- コミット時に progress.html の更新も含めること

## コードリファレンス更新ルール
- public メソッド追加・変更、NetworkVariable 追加、新規ファイル作成時は `docs/shared/code-reference.md` も更新すること
- コミット時に code-reference.md の更新も含めること

## コミットルール
- タスク完了時は自動で `git add -A && git commit && git push` まで実行すること
- コミットメッセージ形式: "M[番号]-[サブ番号]: [タスク名]"
- 例: "M1-4: 他プレイヤー補間表示"
- ユーザーの承認を待たずに実行すること

## タスク管理

- タスクキューは BACKLOG.md で管理する
- 起動時に BACKLOG.md を確認し「待機中」の最初の未完了タスクを実行する
- タスクに tasks/xxx.md が指定されていればその指示書を読んで実行する
- 着手時は「作業中」に移動、完了時は「完了」に日付付きで移動する
- 1タスク完了ごとにcommit & push（BACKLOG.md更新含む）
- INBOX.md はユーザー専用メモ。読み取り専用、一切変更しないこと
- 起動時に INBOX.md を確認し、未転記の内容があれば BACKLOG.md に転記する

### タスク完了後の自動進行ルール
- 🔧マーク付きタスク完了 → **停止**。手動作業内容を報告してユーザーの指示を待つ
- 🔧マークなしタスク完了 → /compact 実行 → BACKLOG の次のタスクに自動着手
- コンパイルエラーが発生した場合 → 自力修正を1回試みる → 解決しなければ停止して報告
- 3タスク連続実行したら → 強制 /compact（コンテキスト肥大化防止）
- thinking が3分超えたら → 停止してユーザーに報告（詰まりサイン）
