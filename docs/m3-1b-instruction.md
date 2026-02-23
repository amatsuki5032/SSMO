読むファイル: TeamManager.cs, GameConfig.cs, PlayerMovement.cs
変更ファイル: GameConfig.cs, 新規 Scripts/Server/SpawnManager.cs

チーム別スポーン地点を管理する。

1. 新規 Scripts/Server/SpawnManager.cs を作成：
   - NetworkBehaviour, サーバー権威
   - チームごとのスポーン地点リスト（Transform[]）
   - 初回スポーン: チームに応じた位置にプレイヤーを配置
   - リスポーン: 即復活 + 交互拠点制限（前回と同じ拠点は使えない）
   - public Vector3 GetSpawnPosition(ulong clientId, Team team)
   - リスポーン時にHP全回復 + 無双ゲージMAX（combat-spec準拠）

2. GameConfig.cs に追加：
   - RESPAWN_DELAY = 0f（即復活）
   - TEAM_RED_SPAWN_POS_1, TEAM_RED_SPAWN_POS_2
   - TEAM_BLUE_SPAWN_POS_1, TEAM_BLUE_SPAWN_POS_2

git commit -m "M3-1b: スポーン地点管理（SpawnManager）"
