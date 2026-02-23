# SSMO プロジェクト ナレッジベース v2
# 真三國無双Online風 4v4 近接アクション対戦ゲーム
# 最終更新: 2026-02-23（M2 完了時点）

---

## 1. プロジェクト概要

### コンセプト
真三國無双Online (2006年 KOEI) の戦闘体験を、現代のネットコード技術で再構築する4v4チーム対戦アクションゲーム。

### 開発者プロフィール
- **あまつき** (GitHub: amatsuki5032)
- C言語経験あり（C#は初だがC系構文に慣れている）
- JavaScript / Firebase / Web開発に精通
- 三國志覇道など和ゲーの詳細データベース構築経験あり
- 個人開発。1年計画で段階的に開発
- **開発支援**: Claude (claude.ai) で指示書作成 → Claude Code (CC) にコピペで投入

### ゲームの特徴
- **ジャンル**: 近接アクション PvP（無双系）
- **フォーマット**: 4v4 チーム対戦 + NPC兵士/武将
- **主戦闘距離**: 0〜5m（近接メイン）、弓は最大100m（サブ）
- **ターゲット体験**: SSMOの対人戦闘の緊張感とコンボの爽快感

---

## 2. 技術スタック & 環境

| 項目 | 技術 | バージョン |
|------|------|-----------|
| エンジン | Unity | 6.3 LTS (6000.3.9f1) |
| 言語 | C# | - |
| ネットワーク | Netcode for GameObjects (NGO) | 2.9.2 |
| トランスポート | Unity Transport | (NGO依存) |
| マルチテスト | ParrelSync | 1.5.2 |
| ネットワーク統計 | Multiplayer Tools | 2.2.8 |
| バージョン管理 | Git + Git LFS | 2.53.0 |
| リポジトリ | GitHub (Private) | amatsuki5032/SSMO |
| 認証/DB (将来) | Firebase Auth / Firestore | - |

### プロジェクトパス
- ローカル: `C:\dev\SSMO`
- GitHub: `https://github.com/amatsuki5032/SSMO`

### 設定済みパラメータ
- Fixed Timestep: 0.01667 (60Hz)
- NetworkManager Tick Rate: 60
- Unity Transport: localhost:7777

---

## 3. アーキテクチャ

### 最重要原則
1. **サーバー権威型**: ゲーム状態の正解はサーバーが持つ。クライアント値は一切信用しない
2. **ネットワーク前提設計**: 全機能はオンライン前提で設計。後付けしない
3. **固定ティック**: ゲームロジックは FixedUpdate 60Hz固定。描画FPSは可変
4. **動作最優先**: 見た目より動作。箱人間でいいから動かす

### ティックレート設計
| 項目 | 値 | 備考 |
|------|------|------|
| ゲームロジック (FixedUpdate) | 60Hz 固定 | 0.01667秒。全クライアント&サーバー共通 |
| サーバーティック | 60Hz 固定 | ロジックと同期 |
| クライアント→サーバー送信 | 30Hz | 入力2ティック分をまとめて送信 |
| サーバー→クライアント配信 | 30Hz | 状態同期。補間で滑らか化 |
| 描画FPS | 可変 | 60/120/144/無制限。プレイヤー設定可 |

### ラグ対策の3本柱
1. **クライアント予測**: 入力を即座にローカル実行→サーバー結果とズレたら巻き戻し再計算
2. **補間 (Interpolation)**: 他プレイヤーの位置を100ms遅延で滑らかに表示
3. **ラグコンペンセーション**: 攻撃時にサーバーが攻撃者の時刻まで巻き戻してヒット判定（最大150ms）

### 同期データ（毎ティック / 18 bytes/プレイヤー）
| データ | サイズ |
|--------|--------|
| プレイヤー位置 (Vector3) | 12 bytes |
| プレイヤー回転 (Y軸) | 2 bytes (圧縮) |
| 現在ステート (enum) | 1 byte |
| コンボ段数 | 1 byte |
| HP | 2 bytes |

---

## 4. 開発ロードマップ & 完了状況

### M0: リポジトリ & 環境構築 ✅ 完了
- Unity 6.3 LTS インストール
- GitHub リポジトリ作成 (Private)
- Git LFS 設定
- NGO / Multiplayer Tools / ParrelSync 導入
- NetworkManager + Unity Transport 設定 (Tick Rate: 60)
- HelloNetwork.cs で Host 接続確認
- GameConfig.cs / CharacterState.cs / DamageCalculator.cs 作成
- CLAUDE.md / .claudeignore 配置

### M1: ネットワーク同期基盤 ✅ 完了
1. NetworkPlayer Prefab（NetworkObject + CharacterController。NetworkTransformは不使用→自前同期）
2. サーバー権威型の移動同期
3. クライアント予測 + リコンシリエーション
4. 他プレイヤーの補間表示
5. ラグコンペンセーション基盤
6. ネットワーク統計HUD (Ping / PacketLoss)

### M2: 戦闘アクション ✅ 完了

| # | タスク | 内容 | 状態 |
|---|--------|------|------|
| M2-1 | ステートマシン | サーバー権威型ステートマシン + 入力受付判定 + 自動遷移タイマー | ✅ |
| M2-2a | 入力統合 | PlayerInput構造体 + 入力キャプチャ統合 | ✅ |
| M2-2b | ジャンプ | サーバー権威ジャンプ（重力・着地判定） | ✅ |
| M2-2c | ダッシュ+ガード | ダッシュ判定（1.5秒移動）+ ガード移動 | ✅ |
| M2-3a | コンボ | ComboSystem N1-N4（拡張可能設計） | ✅ |
| M2-3b | 先行入力 | 先行入力バッファ 150ms | ✅ |
| M2-4a | チャージ攻撃 | C1-C5 + C3ラッシュ（△連打追加ヒット） | ✅ |
| M2-4b | ダッシュ攻撃 | ダッシュ攻撃 + ダッシュラッシュ（□連打） | ✅ |
| M2-5a | Hitbox/Hurtbox | サーバー権威ヒット判定 + 攻撃データテーブル + 1攻撃1ヒット | ✅ |
| M2-5b | ラグコンペ連携 | RTT取得→Rewindスコープ→巻き戻し判定→NotifyHitClientRpc | ✅ |
| M2-6a | ダメージ計算 | DamageCalculator拡張 + HealthSystem + HP同期 + 死亡判定 | ✅ |
| M2-6b | 被弾リアクション | のけぞり/打ち上げ/吹き飛ばし/ダウン/気絶（ReactionSystem） | ✅ |
| M2-7a | ガード判定 | 正面180度ガード + めくり判定 + ガードノックバック | ✅ |
| M2-7b | エレメンタルガード | EG準備→EGReady→カウンター発動→攻撃者吹き飛ばし | ✅ |
| M2-8 | 無双乱舞 | MusouGauge + 無双発動 + 真・無双（HP20%以下）+ 無双チャージ | ✅ |
| M2-9 | アーマー | 5段階アーマー × 攻撃レベル比較によるのけぞり判定 | ✅ |

**スキップした項目**:
- 覚醒システム（SSMOには存在しない要素のためスキップ）
- 覚醒無双（上記に伴いスキップ）

### M3: 4v4 対戦モード ← 次のマイルストーン
- マッチメイキング / ロビー
- マップ設計 + 拠点制圧
- NPC兵士 / 武将AI
- スコアリング / 勝利条件

### M4: キャラクター & コンテンツ
- 6武器種のモーション差別化
- キャラクターカスタマイズ
- 装備 / 育成システム（鍛錬・仙箪・刻印）
- 属性相性

### M5: インフラ & セキュリティ
- 専用サーバー構築 (AWS/GCP)
- チート対策（サーバー検証強化）
- ログ / 監視

### M6: ポリッシュ & α版
- エフェクト / UI 磨き込み
- バランス調整
- α版リリース

---

## 5. プロジェクトファイル構造

```
C:\dev\SSMO/
├── Assets/
│   ├── Scripts/
│   │   ├── Netcode/           # ネットワーク同期・予測・補間・ラグ補正
│   │   ├── Combat/            # ヒット判定・ダメージ・コンボシステム
│   │   ├── Character/         # 移動・ステートマシン・アニメーション
│   │   ├── UI/                # HUD・メニュー・ロビー
│   │   ├── Shared/            # 定数・計算式・データ定義
│   │   ├── Server/            # サーバー専用ロジック・AI
│   │   └── Debug/             # デバッグツール（未作成）
│   ├── Prefabs/
│   │   └── NetworkPlayer.prefab
│   ├── Scenes/
│   │   └── SampleScene.unity
│   ├── Models/
│   ├── Materials/
│   └── Effects/
├── Packages/
├── ProjectSettings/
├── docs/
│   ├── combat-spec.md         # 戦闘仕様書 v4（本家SSMO準拠）
│   ├── m2-*-instruction.md    # M2各タスクのCC向け指示書
│   └── progress.html          # 進捗トラッカー
├── CLAUDE.md                  # Claude Code 用プロジェクトガイド
├── .claudeignore
├── .gitignore
├── .gitattributes
└── README.md
```

---

## 6. スクリプト一覧（全 .cs ファイル）

### Assets/Scripts/Character/

| ファイル | クラス | 役割 |
|---------|--------|------|
| PlayerMovement.cs | PlayerMovement | 移動・入力キャプチャ・ジャンプ・ダッシュ判定。クライアント予測+サーバー権威移動。WASD移動、Space ジャンプ、Shift ガード、マウス入力を PlayerInput 構造体に変換し ServerRpc で送信 |
| CharacterStateMachine.cs | CharacterStateMachine | サーバー権威型ステートマシン。NetworkVariable でステート同期。入力受付判定（CanAcceptInput）、自動遷移タイマー、無敵管理。全ステート遷移の最終決定権を持つ |

### Assets/Scripts/Combat/

| ファイル | クラス | 役割 |
|---------|--------|------|
| ComboSystem.cs | ComboSystem | コンボ管理。N1-N4 通常攻撃連鎖、C1-C5 チャージ攻撃派生、C3ラッシュ（△連打）、ダッシュ攻撃+ダッシュラッシュ（□連打）。先行入力バッファ 150ms。コンボ受付ウィンドウ（モーション最後30%） |
| HitboxData.cs | HitboxData | 攻撃ごとの Hitbox パラメータ定義。Radius、Length、Offset、ActiveStartFrame/EndFrame、MultiHit、MaxHitCount。GetHitboxData() で攻撃種別に応じた仮値を返す |
| HitboxSystem.cs | HitboxSystem | サーバー権威ヒット判定。FixedUpdate で Attack/Charge/DashAttack 中に Physics.OverlapCapsule 実行。ラグコンペンセーション連携（RTT取得→Rewindスコープ→巻き戻し判定）。ガード判定分岐、DamageCalculator呼び出し、ReactionSystem連携 |
| HurtboxComponent.cs | HurtboxComponent | 被弾判定コンポーネント。カプセル型判定領域。IsGuarding() / IsGuardingAgainst(attackerPos) で正面180度ガード判定。IsInvincible() で無敵判定。EGPrepare/EGReady 状態判定 |
| HealthSystem.cs | HealthSystem | HP管理。NetworkVariable\<int\> で全クライアント同期。TakeDamage() はサーバー側のみ実行。HP0 → Dead ステート遷移。GetHpRatio() で根性補正判定用 |
| ReactionSystem.cs | ReactionSystem | 被弾リアクション処理。Flinch/Stagger/Launch/Knockback/Slam/Down/Stun。打ち上げは垂直速度設定→重力適用→着地で Down 遷移。GetReactionType() で攻撃種別→リアクション決定 |
| EGSystem.cs | EGSystem | エレメンタルガード。Guard中に△1秒押し込み→EGPrepare→EGReady。EG中に攻撃受けるとカウンター発動（攻撃者吹き飛ばし）。無双ゲージ消費で維持。解除条件: ガード離し/△離し/ゲージ0 |
| MusouGauge.cs | MusouGauge | 無双ゲージ管理。NetworkVariable\<float\> で同期。ゲージ増加: 攻撃ヒット+3/被弾+5/○長押しチャージ。ゲージMAX→Musou発動（無敵4秒）。HP20%以下→真・無双乱舞（5秒）。のけぞり中に無双→脱出可能 |
| ArmorSystem.cs | ArmorSystem | 5段階アーマーシステム。NetworkVariable\<byte\> で同期。ShouldFlinch(attackLevel): 攻撃レベル > アーマー段階→のけぞる。ダメージは常に通る（アーマーはのけぞり無効化のみ） |

### Assets/Scripts/Shared/

| ファイル | クラス | 役割 |
|---------|--------|------|
| GameConfig.cs | GameConfig | 全定数定義。ティックレート、移動速度、ジャンプパラメータ、ダッシュ閾値、コンボタイミング、各攻撃の持続時間、HP/ATK/DEFデフォルト値、ラグコンペ上限、Hitbox/Hurtboxサイズ、EGパラメータ、無双パラメータ、アーマー定数 |
| CharacterState.cs | 各種 enum | ステートマシン用 enum（CharacterState: Idle/Move/Attack/Charge/Guard 等14種）、武器種（WeaponType）、属性（ElementType）、リアクション種別（HitReaction）、攻撃レベル（AttackLevel） |
| DamageCalculator.cs | DamageCalculator | ダメージ計算式。combat-spec セクション16準拠: 基礎ダメージ→防御計算→空中補正→根性補正→ガード補正→クリティカル→最低保証。GetMotionMultiplier() でN1-N6/C1-C6/D/Rush の倍率テーブル |
| PlayerInput.cs | PlayerInput (struct) | 入力構造体。INetworkSerializable 実装。Move(Vector2)、AttackPressed、ChargePressed、ChargeHeld、GuardHeld、JumpPressed、MusouPressed、MusouHeld、Timestamp |

### Assets/Scripts/Netcode/

| ファイル | クラス | 役割 |
|---------|--------|------|
| HelloNetwork.cs | HelloNetwork | M0 接続テスト用（レガシー） |
| *(M1で作成された同期・予測・補間関連)* | - | クライアント予測、リコンシリエーション、補間表示、ラグコンペンセーション基盤 |

### Assets/Scripts/UI/

| ファイル | クラス | 役割 |
|---------|--------|------|
| *(M1で作成されたネットワーク統計HUD)* | - | Ping / PacketLoss 表示 |

---

## 7. NetworkPlayer Prefab コンポーネント一覧

Prefab に以下のコンポーネントが全て追加済み（順序通り）:

| # | コンポーネント | スクリプト |
|---|-------------|-----------|
| 1 | Network Object | NGO 組み込み |
| 2 | Character Controller | Unity 組み込み（Radius:0.4, Height:1.8） |
| 3 | Player Movement | PlayerMovement.cs |
| 4 | Character State Machine | CharacterStateMachine.cs |
| 5 | Combo System | ComboSystem.cs |
| 6 | Hitbox System | HitboxSystem.cs |
| 7 | Hurtbox Component | HurtboxComponent.cs |
| 8 | Health System | HealthSystem.cs |
| 9 | EG System | EGSystem.cs |
| 10 | Musou Gauge | MusouGauge.cs |
| 11 | Armor System | ArmorSystem.cs |
| 12 | Reaction System | ReactionSystem.cs |

> ⚠ **Prefab 手動追加を忘れがち**: 新しいスクリプト追加時は必ず NetworkPlayer Prefab への Add Component + Ctrl+S を確認すること

---

## 8. M2 で確定した戦闘仕様（本家SSMO準拠の変更点）

### 元のナレッジベース（v1）から変更された仕様

| 項目 | v1（旧） | v2（現在 / 本家準拠） |
|------|---------|---------------------|
| ヒットストップ | あり（通常3F / 重攻撃5F） | **なし**（本家SSMOにヒットストップは存在しない） |
| ガードブレイク | あり（連続ガードでゲージ減少→破壊） | **なし**（正面ガードは絶対崩れない。めくりでのみ崩す） |
| ガード不可技 | C1、投げ、無双乱舞 | **基本なし**（無双乱舞もガード可能。めくりでのみ通る） |
| 回避ステップ | あり（無敵6F→硬直12F） | **なし**（本家SSMOに回避ステップは存在しない） |
| 覚醒システム | あり（別ゲージMAXで発動） | **なし**（本家SSMOに覚醒は存在しない） |
| エマージェンシーガード | 旧名称 | **エレメンタルガード (EG)** が正式名称 |
| ジャストガード | あり（200ms窓、ダメージ0） | **なし**（本家SSMOにジャストガードは存在しない） |
| 属性相性 | 火>風>雷>氷>火（有利1.2倍、不利0.8倍） | **相性なし**（属性間の有利不利は存在しない） |
| コンボ段数 | N1〜N6 固定 | **成長式**: 無強化N4 → 連撃強化1でN5 → 連撃強化2でN6 → 連撃強化3+無双MAXでE6-E9 |
| C1 の性質 | ガード崩し | **武器ごとの強力な攻撃（単体）**。C1/C6は刻印で変更可能 |
| 根性補正 | なし | **あり**: HP50%以下÷1.5、HP20%以下÷2 |
| 属性倍率 | 全属性共通テーブル | **属性ごとに個別倍率**: 炎0.175/氷0.25/雷0.50/風0.50/斬:固定値 |
| 無双ゲージ初期値 | 不明 | **戦闘開始:0 / リスポーン:MAX** |
| ダメージ計算 | ATK×倍率×属性×(100/(100+DEF)) | **10段階計算式**: 攻撃倍率→基礎→防御→空中補正→根性補正→ガード補正→斬保証→最低保証→クリティカル |

### 本家SSMOにあって今回スキップした要素（M2では未実装）
- 投げ / 投げ抜け
- ジャンプ攻撃 (JA) / ジャンプチャージ (JC)
- エボリューション攻撃 (E6-E9)
- ブレイクチャージ (L2)
- 受け身（エリアルリカバリー）
- 状態異常（燃焼・凍結・感電・鈍足）
- 属性付き攻撃判定
- 仙箪強化システム
- 鍛錬・刻印システム
- NPC兵士・武将

---

## 9. 戦闘システム詳細（実装済み範囲）

### コンボ構造
- **通常攻撃 (□)**: N1→N2→N3→N4（現在は4段まで。拡張可能設計）
- **チャージ攻撃 (△)**: N[x]中に△ で C[x+1] に派生
  - C1: △単体（Idle/Move中に△。武器固有強力攻撃）
  - C2: N1中に△（打ち上げ＝空中コンボ起点）
  - C3: N2中に△（ラッシュ。△連打で追加ヒット）
  - C4: N3中に△（吹き飛ばし）
  - C5: N4中に△（まとめて打ち上げ、C2の範囲版）
- **ダッシュ攻撃 (D)**: 1.5秒以上移動→□（N1とは別モーション）
  - D→□連打でダッシュラッシュ
- **先行入力バッファ**: 150ms
- **コンボ受付ウィンドウ**: 各攻撃モーションの最後30%のフレーム
- ★ サーバーがコンボ段数を追跡し不正な連鎖を防止

### ヒット判定フロー（サーバー権威）
1. クライアント: 攻撃入力→ServerRpc送信
2. サーバー: ステート遷移→Attack/Charge/DashAttack
3. サーバー: FixedUpdate でアクティブフレーム中に Physics.OverlapCapsule
4. サーバー: リモートクライアントの攻撃→RTT取得→ラグコンペンセーション（巻き戻し判定）
5. サーバー: ヒット確定→ガード判定分岐
6. サーバー: 非ガード→DamageCalculator→HealthSystem.TakeDamage→ReactionSystem.ApplyReaction
7. サーバー: ガード成功→ダメージ×0.2 + ガードノックバック0.3m
8. サーバー: EGReady中にガード成功→カウンター発動→攻撃者吹き飛ばし
9. サーバー: NotifyHitClientRpc / NotifyDamageClientRpc で全クライアントに通知

### ダメージ計算式（サーバー側のみ）
```
1. 攻撃倍率 = モーション倍率 × 属性倍率（将来実装）
2. 基礎ダメージ = ATK × 攻撃倍率
3. 防御計算 = 基礎ダメージ × (100 / (100 + DEF))
4. 空中補正 = 空中被弾時は ÷2
5. 根性補正 = HP青(50-100%):÷1 / HP黄(20-50%):÷1.5 / HP赤(20%以下):÷2
6. ガード補正 = ガード時 ×0.2 / 非ガード ×1.0
7. 最低ダメージ保証 = max(最終ダメージ, 1)
8. クリティカル = 5%確率で ×1.5
```

### 被弾リアクション
| リアクション | トリガー | 効果 |
|-------------|---------|------|
| Flinch | N攻撃 | 短い行動ロック |
| Stagger | 重攻撃 | 長い行動ロック |
| Launch | C2/C5系 | 垂直速度→空中→重力→着地でDown |
| Knockback | C4系 | 後方移動 + Down |
| Slam | 空中→地面 | ダウン状態 |
| Down | 各種ダウン | 起き上がり無敵あり |
| Stun | 気絶技 | 完全無防備 |

### アーマーシステム
- 5段階: 1通常 → 2矢耐性 → 3N耐性 → 4SA → 5HA
- 攻撃レベル: 1雑魚矢 / 2通常N / 3チャージC / 4無双
- 判定: 攻撃レベル > アーマー段階 → のけぞる（ダメージは常に通る）

---

## 10. キャラクターステートマシン

### ステート一覧（実装済み）
| ステート | 受付可能入力 | 遷移先 |
|---------|-------------|--------|
| Idle | 全アクション | Move/Attack/Guard/Dash/Musou |
| Move | 攻撃/ガード/ジャンプ | Idle/Attack/Guard/Dash/Musou |
| Attack | 次段/チャージ | Attack/Charge/Idle/Hitstun |
| Charge | 一部キャンセル | Idle/Hitstun |
| DashAttack | ラッシュ | Idle/Hitstun |
| Guard | EG/回避 | Idle/Move/EGPrepare |
| GuardMove | ガード移動中 | Guard/Idle |
| Jump | 着地で戻る | Idle |
| Dash | 攻撃入力 | DashAttack/Idle |
| EGPrepare | EG準備中 | EGReady/Guard |
| EGReady | カウンター待機 | EGCounter/Guard |
| EGCounter | カウンター発動中 | Guard |
| Musou | 無敵・タイマー制 | Idle |
| Hitstun | 無双のみ受付 | Idle/Launch/Down/Musou |
| Launch | 行動不能（空中） | Down/AirHitstun |
| AirHitstun | 追撃中 | Slam/Down |
| Slam | 叩きつけ | Down |
| Down | 起き上がり無敵 | Idle/Dead |
| Dead | リスポーン待ち | Idle |

### サーバー権威ルール
- ステート遷移の最終権限はサーバー
- クライアントは予測遷移するがサーバー否認時ロールバック
- 無敵状態（無双/起き上がり）はサーバーのみ管理
- 死亡判定は100%サーバー（HP≤0 → Dead遷移）

---

## 11. 既知の問題 & メモ

| # | 問題 | 重要度 | 対応 |
|---|------|--------|------|
| 1 | クライアント側の位置ジッター | 軽微 | クローン側でキャラが左右にブレる。M1ネットコード調整範囲 |
| 2 | C番号表示ズレ | 調査中 | NNNCで「C3」表示→入力タイミングの問題の可能性（N3確定前に△を押すとN2段階のC3になるのは仕様通り） |

---

## 12. 現在地 & 次のステップ

### 直近のタスク
1. **デバッグヘルパー作成** — F1-F10キーで相手の状態を強制変更するテストツール（Host操作で完結）
2. **M2統合テスト** — デバッグヘルパーを使い、全戦闘機能の動作確認
3. **M3 準備** — 4v4対戦モードの設計

### デバッグヘルパー仕様（未実装）
```
F1:  相手を Hitstun にトグル
F2:  相手を Launch 状態にトグル
F3:  自分の無双ゲージを MAX
F4:  自分をEG準備完了状態に
F5:  相手を自分の正面2mに瞬間移動
F6:  相手にガード状態を強制トグル
F9:  全員のHP全回復 + Dead復活
F10: 相手のアーマー段階を1上げる（ループ）
F12: コマンド表の表示/非表示トグル
```
- Host側のみ動作、OnGUIでコマンド一覧+状態表示
- `#if UNITY_EDITOR` で囲む（リリースビルドには含めない）

---

## 13. コーディング規約

### 命名規則
| 対象 | 規則 | 例 |
|------|------|-----|
| クラス名 | PascalCase | `DamageCalculator` |
| メソッド | PascalCase | `CalculateDamage` |
| private フィールド | _camelCase | `_currentHealth` |
| public フィールド | PascalCase | `MaxHealth` |
| 定数 | UPPER_SNAKE_CASE | `MAX_PLAYERS` |
| enum値 | PascalCase | `CharacterState.Idle` |
| ServerRpc | 〜ServerRpc サフィックス | `SubmitInputServerRpc` |
| ClientRpc | 〜ClientRpc サフィックス | `ApplyDamageClientRpc` |

### ネットワーク関連ルール
- サーバー専用処理: `[ServerRpc]` または `if (IsServer)` で明示
- クライアント専用処理: `[ClientRpc]` または `if (IsClient)` で明示
- 同期変数: `NetworkVariable<T>` を使用（WritePermission を必ず明示）
- ServerRpc 内では受信した入力値を必ずバリデーション
- サーバー専用コメント: `// ★ サーバー側で実行 ★`

### 重要禁止事項（NGパターン）
- クライアント側でダメージ計算
- クライアントが無敵状態を管理
- クライアントがヒット判定の最終結果を決定
- ステート遷移をクライアントだけで完結
- NetworkTransform に頼る（自前同期）
- ServerRpc の入力値をバリデーションせずに使う

### 判断に迷ったときの優先順位
> **サーバー権威 ＞ ネットワーク正確性 ＞ チート耐性 ＞ 可読性 ＞ パフォーマンス ＞ 実装速度**

---

## 14. 開発ワークフロー & CC運用ノウハウ

### 開発フロー
1. **Claude (claude.ai)** で指示書を作成 → `C:\dev\SSMO\docs\` に配置
2. **Claude Code (CC)** に指示書のパスを投げてコピペで実装
3. CC完了後、**Prefab へのコンポーネント追加**を忘れずに手動実行
4. **ParrelSync** で2人接続テスト
5. 問題なければ `git add -A && git commit && git push`

### CC運用の鉄則
| ルール | 理由 |
|--------|------|
| **エラー → 即 exit → claude で再起動** | 壊れたコンテキストが残ると詰まる |
| **5分超 thinking = 詰まり** | 小さいタスクは2-3分が正常 |
| **Unity Playモード中はCC避ける** | ファイルロックで不安定になる |
| **指示は1つずつシンプルに** | 複数指示は混乱の元 |
| **1時間ごとにトークン確認** | `claude /login` でリフレッシュ |

### テスト時の注意
- 新コンポーネント追加後は **必ず Prefab に手動 Add Component + Ctrl+S**
- ParrelSync クローンが起動しない → `Library` フォルダ削除 → 再起動
- Console ログで判定確認（アニメーションなしの箱人間なので視覚的確認は限定的）

---

## 15. 参考資料

### ゲーム参考
- **真三國無双Online (SSMO, 2006)**: プロジェクトの最大の参考元
- **無双シリーズ全般**: コンボ構造 (N→C派生)、無双乱舞
- **公式マニュアル (gamecity.ne.jp)**: EG・属性・鍛錬・刻印等の正確な仕様確認に使用

### 技術資料
- Gabriel Gambetta: Fast-Paced Multiplayer
- Overwatch GDC Talk: Gameplay Architecture and Netcode
- Valve Source Multiplayer Networking
- Unity NGO Documentation

### プロジェクト内ドキュメント
- `docs/combat-spec.md` — 戦闘仕様書 v4（全20セクション、800行超）
- `docs/m2-*-instruction.md` — M2各タスクのCC向け指示書
- `docs/progress.html` — 進捗トラッカー（ブラウザで閲覧可能）
