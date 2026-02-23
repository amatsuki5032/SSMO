読むファイル: CharacterState.cs, GameConfig.cs, PlayerMovement.cs
変更ファイル: CharacterState.cs, GameConfig.cs, 新規 Scripts/Server/TeamManager.cs

チーム管理の基盤を作成する。

1. CharacterState.cs に既にある `Team` enum（Red, Blue）を使用

2. GameConfig.cs に以下の定数を追加：
   - TEAM_SIZE = 4（既にあれば確認のみ）
   - MAX_PLAYERS = 8（既にあれば確認のみ）
   - SPAWN_POINTS_PER_TEAM = 2

3. 新規 Scripts/Server/TeamManager.cs を作成：
   - NetworkBehaviour, サーバー権威
   - プレイヤー接続時にチームを自動振り分け（人数均等化）
   - NetworkVariable<byte> で各プレイヤーのチーム所属を同期
   - public Team GetPlayerTeam(ulong clientId)
   - public List<ulong> GetTeamMembers(Team team)
   - OnClientConnected / OnClientDisconnected でチーム管理

git commit -m "M3-1a: チーム管理基盤（TeamManager）"
