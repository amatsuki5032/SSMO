読むファイル: PlayerMovement.cs, CharacterStateMachine.cs, GameConfig.cs
変更ファイル: 新規 Scripts/Character/CameraController.cs, GameConfig.cs

3人称カメラシステムを作成する。

1. 新規 Scripts/Character/CameraController.cs：
   - MonoBehaviour（NetworkBehaviourではない。ローカルカメラはクライアント専用）
   - プレイヤーの後方上方から追従（オフセット: 後方3m, 上方2m）
   - マウス移動でカメラ回転（水平360度、垂直-10〜60度）
   - カメラと壁の間にオブジェクトがある場合、カメラを手前に寄せる（SphereCast）
   - IsOwner のプレイヤーにのみアタッチ（他プレイヤーのカメラは不要）
   - Update で処理（カメラは描画FPSに合わせる）

2. GameConfig.cs に追加：
   - CAMERA_DISTANCE = 3.0f
   - CAMERA_HEIGHT = 2.0f
   - CAMERA_SENSITIVITY = 2.0f
   - CAMERA_MIN_PITCH = -10f
   - CAMERA_MAX_PITCH = 60f

3. PlayerMovement.cs でカメラの向きに合わせた移動方向を計算するよう修正
   - 現在のWASD入力をカメラのforward/right基準に変換

git commit -m "M3-2: 3人称カメラシステム"
