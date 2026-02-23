読むファイル: TeamManager.cs, BasePoint.cs, GameConfig.cs
変更ファイル: 新規 Scripts/UI/MinimapHUD.cs, GameConfig.cs

ミニマップを作成する。

1. 新規 Scripts/UI/MinimapHUD.cs：
   - MonoBehaviour（クライアント専用UI）
   - 画面右下に小さい正方形マップ表示（OnGUI）
   - 表示要素:
     - 自分の位置（白い点）
     - 味方プレイヤーの位置（青い点）
     - 敵プレイヤーの位置（赤い点）※ 視界内のみ or 常時表示（仮で常時）
     - 拠点の位置と色（所属チーム色の四角）
   - マップ座標 → ミニマップ座標の変換
   - R2（Mキー）で全体マップ⇔ミニマップ切替

2. GameConfig.cs に追加：
   - MINIMAP_SIZE = 200f（ピクセル）
   - MINIMAP_RANGE = 50f（表示範囲メートル）

git commit -m "M3-6b: ミニマップ"
