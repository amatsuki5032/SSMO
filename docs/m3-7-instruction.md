読むファイル: TeamManager.cs, BasePoint.cs, GameConfig.cs
変更ファイル: GameConfig.cs, 新規 Scripts/Server/GameModeManager.cs, 新規 Scripts/UI/ScoreboardHUD.cs

ゲームモード管理と勝利条件を作成する。

1. 新規 Scripts/Server/GameModeManager.cs：
   - NetworkBehaviour, サーバー権威
   - ゲームタイマー（NetworkVariable<float> で同期、カウントダウン）
   - スコア管理（各チームの撃破数を NetworkVariable で同期）
   - 勝利条件判定（時間切れ時にスコアが多いチームが勝利）
   - ゲーム状態管理: WaitingForPlayers → InProgress → GameOver
   - ゲーム開始: 両チームに1人以上いたら開始
   - ゲーム終了時に結果を全クライアントに通知（ClientRpc）

2. 新規 Scripts/UI/ScoreboardHUD.cs：
   - MonoBehaviour（クライアント専用UI）
   - 画面上部中央にタイマー表示
   - 赤チーム vs 青チームのスコア表示
   - ゲーム終了時に勝利/敗北表示

3. GameConfig.cs に追加：
   - MATCH_TIME_SECONDS = 300（5分。既にあれば確認のみ）
   - MIN_PLAYERS_TO_START = 2

git commit -m "M3-7: ゲームモード管理・勝利条件・スコアボード"
