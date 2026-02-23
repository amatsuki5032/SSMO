読むファイル: GameConfig.cs, TeamManager.cs, SpawnManager.cs
変更ファイル: GameConfig.cs, 新規シーン or スクリプトでマップ生成

基本バトルマップをコードで生成する（Unityエディタでの手動配置は最小限にする）。

1. マップ構成（仮。フラットな戦場）：
   - 地面: 100m × 100m の平面
   - 外壁: マップ端に見えない壁（プレイヤー落下防止）
   - 拠点位置: 5箇所（中央1 + 赤側2 + 青側2）
   - 拠点は立方体（3m×3m×3m）で仮表現。色分け
   - 障害物: 適当な箱を数個配置（カメラ壁貫通テスト用）

2. 新規 Scripts/Server/MapGenerator.cs：
   - MonoBehaviour（シーン初期化時にマップオブジェクト生成）
   - 拠点の位置を定数で定義
   - 各拠点にCollider設定（拠点エリア判定用）

3. GameConfig.cs に追加：
   - MAP_SIZE = 100f
   - BASE_POSITIONS（拠点座標5つ）

4. SpawnManager.cs のスポーン座標をマップに合わせて更新

git commit -m "M3-3: バトルマップ生成"
