読むファイル: HealthSystem.cs, MusouGauge.cs, GameConfig.cs
変更ファイル: 新規 Scripts/UI/BattleHUD.cs

戦闘HUDの基盤を作成する（自分のステータス表示）。

1. 新規 Scripts/UI/BattleHUD.cs：
   - MonoBehaviour（クライアント専用UI）
   - 自キャラのHP表示（画面下部、横バー）
     - HP帯による色変化（青50-100% / 黄20-50% / 赤0-20%）
   - 自キャラの無双ゲージ表示（HPバーの下、横バー）
     - MAX時に色変化（金色）
   - ターゲットHP表示（画面上部）
     - 最後に攻撃した敵 or 最後に攻撃された敵のHPを表示
   - OnGUI ベースで実装（箱人間フェーズなので見た目は最低限）
   - DebugTestHelper の GUI と被らない位置に配置

git commit -m "M3-6a: 戦闘HUD基盤（HP・無双ゲージ）"
