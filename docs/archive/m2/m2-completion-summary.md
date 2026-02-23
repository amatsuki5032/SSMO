# SSMO M2 完了時点サマリ（2026-02-23）

## M2 完了ステータス
M2（戦闘アクション）の全サブタスクが完了し、コードはGitにコミット済み。

### 完了タスク一覧
| タスク | 内容 | 状態 |
|--------|------|------|
| M2-1 | サーバー権威型ステートマシン | ✅ |
| M2-2a | PlayerInput構造体 + 入力統合 | ✅ |
| M2-2b | ジャンプ（サーバー権威） | ✅ |
| M2-2c | ダッシュ判定 + ガード移動 | ✅ |
| M2-3a | コンボシステム N1-N4 | ✅ |
| M2-3b | 先行入力バッファ 150ms | ✅ |
| M2-4a | チャージ攻撃 C1-C5 + C3ラッシュ | ✅ |
| M2-4b | ダッシュ攻撃 + ダッシュラッシュ | ✅ |
| M2-5a | Hitbox/Hurtbox コンポーネント | ✅ |
| M2-5b | ラグコンペンセーション連携 | ✅ |
| M2-6a | ダメージ計算 + HP同期 | ✅ |
| M2-6b | 被弾リアクション（のけぞり/打ち上げ/吹き飛ばし/ダウン） | ✅ |
| M2-7a | ガード判定（正面180度 + めくり） | ✅ |
| M2-7b | エレメンタルガード（EG） | ✅ |
| M2-8 | 無双乱舞 + ゲージシステム | ✅（覚醒はスキップ/SSMOに不要） |
| M2-9 | アーマーシステム（5段階） | ✅ |

## NetworkPlayer Prefab コンポーネント一覧
以下が全て追加済み（順序通り）:
1. Player Movement
2. Character State Machine
3. Combo System
4. Hitbox System
5. Hurtbox Component
6. Health System
7. EG System
8. Musou Gauge
9. Armor System
10. ★ ReactionSystem（途中で追加忘れ → 追加済み）

## 実装済みスクリプト一覧
### Assets/Scripts/Combat/
- ComboSystem.cs（コンボ・チャージ・ダッシュ攻撃）
- HitboxData.cs（攻撃ごとのHitboxパラメータ）
- HitboxSystem.cs（サーバー権威ヒット判定 + ラグコンペ連携）
- HurtboxComponent.cs（被弾判定）
- HealthSystem.cs（HP管理・NetworkVariable同期・死亡判定）
- ReactionSystem.cs（のけぞり/打ち上げ/吹き飛ばし/ダウン）
- EGSystem.cs（エレメンタルガード）
- MusouGauge.cs（無双ゲージ管理・無双乱舞発動）
- ArmorSystem.cs（5段階アーマー）

### Assets/Scripts/Character/
- PlayerMovement.cs（移動・入力処理・ジャンプ・ダッシュ）
- CharacterStateMachine.cs（サーバー権威ステートマシン）

### Assets/Scripts/Shared/
- GameConfig.cs（全定数）
- CharacterState.cs（enum定義: ステート/武器種/属性/リアクション/攻撃レベル等）
- DamageCalculator.cs（ダメージ計算式）
- PlayerInput.cs（入力構造体）

## 既知の問題・メモ
1. **クライアント側の位置ジッター**: クローン側でキャラが左右にブレる。M1のネットコード調整範囲。軽微。
2. **C番号表示**: NNNCで「C3」と表示される件 → 入力タイミングの問題の可能性大（N3確定前に△を押すとC3になるのは仕様通り）
3. **覚醒システム**: SSMOには不要と判断しスキップ

## 次にやること
1. **デバッグヘルパー作成**（F1-F10でテスト用状態強制）
2. **M2統合テスト**（デバッグヘルパーを使って各機能確認）
3. **M3: 4v4対戦モード** へ進行

## デバッグヘルパー仕様（未実装）
```
F1: 相手を Hitstun にトグル
F2: 相手を Launch 状態にトグル
F3: 自分の無双ゲージを MAX
F4: 自分をEG準備完了状態に
F5: 相手を自分の正面2mに瞬間移動
F6: 相手にガード状態を強制トグル
F9: 全員のHP全回復 + Dead復活
F10: 相手のアーマー段階を1上げる（ループ）
F12: コマンド表の表示/非表示トグル
```
- Host側のみ動作、OnGUIでコマンド一覧+状態表示
- #if UNITY_EDITOR で囲む

## CC運用で学んだこと
- **エラー → 即 exit → claude で再起動**が鉄則
- 同じセッションでリトライすると壊れたコンテキストが残る
- Unity Playモード中はCC作業を避ける
- 小さいタスクは2-3分が正常。5分超えたら怪しい
- 指示はシンプルに1つずつ

## 指示書の場所
全て `C:\dev\SSMO\docs\` に配置済み:
- m2-3a-instruction.md 〜 m2-9-instruction.md
- combat-spec.md（戦闘仕様書 v4）
