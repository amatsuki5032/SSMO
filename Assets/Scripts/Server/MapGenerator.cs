using UnityEngine;

/// <summary>
/// バトルマップをコードで生成する（MonoBehaviour）
///
/// 設計意図:
/// - シーン初期化時（Awake）にマップオブジェクトをプロシージャルに生成
/// - Unityエディタでの手動配置を最小限にする（GameManager に Add Component するだけ）
/// - 地面・外壁・拠点・障害物をすべてコードで配置
/// - 拠点には BoxCollider（isTrigger）を設定し、エリア判定に使えるようにする
/// </summary>
public class MapGenerator : MonoBehaviour
{
    // 生成したオブジェクトの親（ヒエラルキー整理用）
    private Transform _mapRoot;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        GenerateMap();
    }

    // ============================================================
    // マップ生成
    // ============================================================

    /// <summary>
    /// マップ全体を生成する
    /// </summary>
    private void GenerateMap()
    {
        // ヒエラルキー整理用の親オブジェクト
        var rootObj = new GameObject("MapRoot");
        _mapRoot = rootObj.transform;

        GenerateGround();
        GenerateWalls();
        GenerateBases();
        GenerateObstacles();

        Debug.Log($"[MapGenerator] マップ生成完了 ({GameConfig.MAP_SIZE}m × {GameConfig.MAP_SIZE}m)");
    }

    // ============================================================
    // 地面
    // ============================================================

    /// <summary>
    /// 100m × 100m の平面を生成
    /// Plane のデフォルトスケール1 = 10m なので、スケール10で100mになる
    /// </summary>
    private void GenerateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(_mapRoot);
        ground.transform.position = Vector3.zero;
        // Plane のデフォルトサイズ = 10m × 10m。スケール10で100m × 100m
        float planeScale = GameConfig.MAP_SIZE / 10f;
        ground.transform.localScale = new Vector3(planeScale, 1f, planeScale);

        // 地面の色を設定（暗めグレー = キャラクターの視認性を優先）
        var renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.25f, 0.25f, 0.28f); // 暗めグレー
        }

        // レイヤーをデフォルトに（地面判定はCharacterControllerが自動処理）
        ground.layer = LayerMask.NameToLayer("Default");
    }

    // ============================================================
    // 外壁（見えない壁。プレイヤー落下防止）
    // ============================================================

    /// <summary>
    /// マップ4辺に見えない壁を配置
    /// Renderer を無効化して透明にする
    /// </summary>
    private void GenerateWalls()
    {
        float half = GameConfig.MAP_HALF;
        float height = GameConfig.WALL_HEIGHT;
        float thickness = 1f; // 壁の厚さ

        // 北壁 (Z+)
        CreateWall("Wall_North", new Vector3(0f, height / 2f, half), new Vector3(GameConfig.MAP_SIZE, height, thickness));
        // 南壁 (Z-)
        CreateWall("Wall_South", new Vector3(0f, height / 2f, -half), new Vector3(GameConfig.MAP_SIZE, height, thickness));
        // 東壁 (X+)
        CreateWall("Wall_East", new Vector3(half, height / 2f, 0f), new Vector3(thickness, height, GameConfig.MAP_SIZE));
        // 西壁 (X-)
        CreateWall("Wall_West", new Vector3(-half, height / 2f, 0f), new Vector3(thickness, height, GameConfig.MAP_SIZE));
    }

    /// <summary>
    /// 見えない壁を1枚生成
    /// BoxCollider のみ有効、Renderer は無効化
    /// </summary>
    private void CreateWall(string name, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(_mapRoot);
        wall.transform.position = position;
        wall.transform.localScale = scale;

        // 見えない壁: Renderer を無効化
        var renderer = wall.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
    }

    // ============================================================
    // 拠点
    // ============================================================

    /// <summary>
    /// 5箇所の拠点を生成
    /// 立方体（BASE_SIZE × BASE_SIZE × BASE_SIZE）で仮表現
    /// BoxCollider (isTrigger) を追加してエリア判定に使用可能にする
    /// </summary>
    private void GenerateBases()
    {
        // 拠点名と色の定義
        string[] baseNames = { "Base_Center", "Base_Red_1", "Base_Red_2", "Base_Blue_1", "Base_Blue_2" };
        Color[] baseColors =
        {
            new Color(0.8f, 0.8f, 0.2f),  // 中央: 黄色
            new Color(0.8f, 0.2f, 0.2f),  // 赤1
            new Color(0.8f, 0.2f, 0.2f),  // 赤2
            new Color(0.2f, 0.2f, 0.8f),  // 青1
            new Color(0.2f, 0.2f, 0.8f),  // 青2
        };
        // 拠点の初期所属: 中央=Neutral、赤側=Red、青側=Blue
        BaseStatus[] initialStatuses =
        {
            BaseStatus.Neutral,
            BaseStatus.Red,
            BaseStatus.Red,
            BaseStatus.Blue,
            BaseStatus.Blue,
        };

        for (int i = 0; i < GameConfig.BASE_POSITIONS.Length; i++)
        {
            CreateBase(baseNames[i], GameConfig.BASE_POSITIONS[i], baseColors[i], i, initialStatuses[i]);
        }
    }

    /// <summary>
    /// 拠点オブジェクトを1つ生成
    /// 地面に接地するよう Y座標を補正（拠点底面 = Y:0）
    /// </summary>
    private void CreateBase(string name, Vector3 position, Color color, int index, BaseStatus initialStatus)
    {
        var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseObj.name = name;
        baseObj.transform.SetParent(_mapRoot);

        float size = GameConfig.BASE_SIZE;
        // Y座標を補正: 拠点の底面が地面(Y=0)に接するようにする
        baseObj.transform.position = new Vector3(position.x, size / 2f, position.z);
        baseObj.transform.localScale = new Vector3(size, size, size);

        // 色を設定
        var renderer = baseObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = color;
        }

        // エリア判定用の Trigger Collider を追加
        // デフォルトの BoxCollider は物理用（プレイヤーが上に乗れる）
        // 追加の BoxCollider (isTrigger) はエリア判定用（少し大きめ）
        var triggerCollider = baseObj.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(2f, 2f, 2f); // 拠点の2倍の範囲をエリアとする

        // BasePoint コンポーネントを追加して初期化
        // SphereCollider は [RequireComponent] で自動追加される
        var basePoint = baseObj.AddComponent<BasePoint>();
        basePoint.SetBaseIndex(index);
        basePoint.SetInitialStatus(initialStatus);

        // タグ設定（将来の拠点制圧判定用）
        baseObj.tag = "Untagged"; // カスタムタグは手動追加が必要なので仮
    }

    // ============================================================
    // 障害物（カメラ壁貫通テスト用）
    // ============================================================

    /// <summary>
    /// 適当な箱を数個配置（カメラの壁衝突回避テスト用）
    /// </summary>
    private void GenerateObstacles()
    {
        // マップ中央寄りに障害物を配置
        CreateObstacle("Obstacle_1", new Vector3(-10f, 0f,  8f), new Vector3(3f, 4f, 3f));
        CreateObstacle("Obstacle_2", new Vector3( 10f, 0f, -8f), new Vector3(4f, 3f, 2f));
        CreateObstacle("Obstacle_3", new Vector3(  0f, 0f, 20f), new Vector3(2f, 5f, 6f));
        CreateObstacle("Obstacle_4", new Vector3( 15f, 0f, 15f), new Vector3(3f, 3f, 3f));
        CreateObstacle("Obstacle_5", new Vector3(-15f, 0f,-15f), new Vector3(5f, 2f, 4f));
        CreateObstacle("Obstacle_6", new Vector3(-25f, 0f,  0f), new Vector3(2f, 6f, 2f)); // 赤側の高い柱
        CreateObstacle("Obstacle_7", new Vector3( 25f, 0f,  0f), new Vector3(2f, 6f, 2f)); // 青側の高い柱
    }

    /// <summary>
    /// 障害物を1つ生成
    /// 底面がY=0に接地するようY座標を補正
    /// </summary>
    private void CreateObstacle(string name, Vector3 position, Vector3 scale)
    {
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = name;
        obstacle.transform.SetParent(_mapRoot);

        // Y座標を補正: 底面がY=0に接地
        obstacle.transform.position = new Vector3(position.x, scale.y / 2f, position.z);
        obstacle.transform.localScale = scale;

        // 障害物の色（灰色）
        var renderer = obstacle.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.5f, 0.5f, 0.5f); // グレー
        }
    }
}
