# SSMO - Claude Code プロジェクトガイド

## プロジェクト概要
真三國無双Online風の **4v4 近接アクション対戦ゲーム**。
Unity 6.3 LTS (C#) + Netcode for GameObjects (NGO) で開発中。

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

## プロジェクト構造
```
C:\dev\SSMO/
├── Assets/
│   ├── Scripts/
│   │   ├── Netcode/        # ネットワーク同期・予測・補間・ラグ補正
│   │   ├── Combat/         # ヒット判定・ダメージ・コンボシステム
│   │   ├── Character/      # 移動・ステートマシン・アニメーション
│   │   ├── UI/             # HUD・メニュー・ロビー
│   │   ├── Shared/         # 定数・計算式・データ定義（サーバー/クライアント共有）
│   │   └── Server/         # サーバー専用ロジック・AI
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Models/
│   ├── Materials/
│   └── Effects/
├── docs/                   # 設計ドキュメント
├── .gitattributes          # Git LFS 設定
└── README.md
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

## 戦闘システム

### コンボ構造
- 通常攻撃 (□): N1→N2→N3→N4→N5→N6
- チャージ攻撃 (△): N[x]→△ で C[x] に派生
- 先行入力バッファ: 150ms
- コンボ受付ウィンドウ: 250ms

### ヒット判定フロー (近接)
1. クライアント: 攻撃入力 → ローカル予測実行 → 予測ヒットエフェクト
2. サーバー: 入力受信 → ラグコンペンセーション → Hitbox vs Hurtbox 判定
3. サーバー: ヒット確定 → 全クライアントに通知
4. クライアント: 予測が正しければそのまま / 外れたらエフェクト取消

### ダメージ計算式 (サーバー側のみ)
```
基礎ダメージ = ATK × モーション倍率
属性補正 = 属性相性テーブル参照 (火>風>雷>氷>火, 有利1.2倍)
防御計算 = 基礎ダメージ × 属性補正 × (100 / (100 + DEF))
ガード時 = 最終ダメージ × 0.2 (ジャストガード = 0)
クリティカル = 5%確率で 1.5倍
```

### 武器種 (初期6種)
| 武器 | 射程 | 特徴 |
|------|------|------|
| 大剣 | 3m | 広範囲・高威力・遅い |
| 双剣 | 1.5m | 手数型・連撃コンボ |
| 槍 | 4.5m | 突き特化・リーチ戦 |
| 戟 | 3.5m | 打ち上げ・回転斬り |
| 拳 | 1m | 超近距離ラッシュ・投げ |
| 弓 | 100m | 遠距離射撃・牽制（サブ） |

### ステートマシン
Idle / Move / Attack / Charge / Guard / Dash / Musou / Awakening /
Hitstun / Launch / AirHitstun / Slam / Down / Dead

※ ステート遷移の最終決定権はサーバー

## 開発ロードマップ
- [x] **M0** (Week 1-2): リポジトリ & 環境構築 ← 完了
- [ ] **M1** (Week 3-8): ネットワーク同期基盤（クライアント予測・補間・ラグ補正）
- [ ] **M2** (Week 9-22): 戦闘アクション（コンボ・ヒット判定・ガード・回避・無双）
- [ ] **M3** (Week 23-28): 4v4 対戦モード（マッチメイキング・マップ・AI）
- [ ] **M4** (Week 29-38): キャラクター & コンテンツ（武器種・育成）
- [ ] **M5** (Week 39-44): インフラ & チート対策
- [ ] **M6** (Week 45-52): ポリッシュ & α版リリース

## 現在の状態 (M0 完了)
- Unity 6.3 LTS プロジェクト作成済み
- NGO 2.9.2 / Multiplayer Tools 2.2.8 / ParrelSync 1.5.2 導入済み
- NetworkManager + Unity Transport 設定済み (Tick Rate: 60)
- HelloNetwork.cs で Host 接続確認済み
- GameConfig.cs / CharacterState.cs / DamageCalculator.cs 作成済み
- Fixed Timestep: 0.01667 (60Hz) 設定済み

## 次のタスク: M1 - ネットワーク同期基盤
1. NetworkPlayer Prefab 作成（NetworkObject + CharacterController。NetworkTransformは使わない→自前同期）
2. サーバー権威型の移動同期（NetworkVariable で位置・回転を同期）
3. クライアント予測 + リコンシリエーション
4. 他プレイヤーの補間表示
5. ラグコンペンセーション基盤
6. ネットワーク統計HUD (Ping / PacketLoss)

※ NetworkTransform を使わない理由: クライアント予測＋リコンシリエーションを自前実装する必要があり、NetworkTransform ではその制御ができないため。最初から NetworkVariable による自前同期で構築する。

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

### 重要な注意事項
- **ダメージ計算は必ずサーバー側で実行**。クライアント値を絶対に信用しない
- **無敵状態 (無双乱舞・回避・起き上がり) はサーバーのみが管理**
- **ヒット判定はサーバー権威**。クライアントは演出（エフェクト・ヒットストップ）のみ
- **ステート遷移の最終決定権はサーバー**。クライアントは予測遷移するが否認されたら巻き戻る
- **投射物 (弓矢等) の衝突判定もサーバーで実行**

## 参考リソース
- [Gabriel Gambetta: Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-server-game-architecture.html) - ネットコードの必読資料
- [Unity NGO Documentation](https://docs-multiplayer.unity3d.com/netcode/current/about/) - 公式ドキュメント
- [GDC: Overwatch Gameplay Architecture and Netcode](https://www.youtube.com/watch?v=W3aieHjyNvw) - ラグ補正の実例
- [Valve: Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) - クライアント予測の原典
