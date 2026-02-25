using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 仙箪アイテム（サーバー権威型）
///
/// 設計意図:
/// - NPC兵士死亡時にドロップされるアイテム
/// - プレイヤーが一定距離内に入ると自動取得（OnTriggerEnter）
/// - 取得するとプレイヤーの仙箪カウント+1（ComboSystem.AddSentan）
/// - 一定時間（SENTAN_LIFETIME）経過で自動消滅
/// - 金色の小さい球体で視覚表現（箱人間フェーズ用）
/// - Prefab 必要: NetworkObject + SentanItem（Collider/Rigidbody は自動追加）
/// </summary>
public class SentanItem : NetworkBehaviour
{
    // ============================================================
    // サーバー側ローカル変数
    // ============================================================

    private float _lifetime;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        // トリガー用 SphereCollider をプログラムで追加（Prefab設定の手間を省く）
        var col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
        }
        col.isTrigger = true;
        col.radius = GameConfig.SENTAN_PICKUP_RADIUS;

        // OnTriggerEnter を動作させるために Kinematic Rigidbody を追加
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _lifetime = GameConfig.SENTAN_LIFETIME;
        }

        CreateVisual();
    }

    // ============================================================
    // 寿命管理（サーバー専用）
    // ============================================================

    private void FixedUpdate()
    {
        if (!IsServer) return;

        _lifetime -= GameConfig.FIXED_DELTA_TIME;
        if (_lifetime <= 0f)
        {
            Debug.Log($"[Sentan] {gameObject.name} 時間切れで消滅");
            DespawnSelf();
        }
    }

    // ============================================================
    // 拾い判定（サーバー専用・OnTriggerEnter）
    // ============================================================

    /// <summary>
    /// プレイヤーの CharacterController がトリガーに入ったら自動取得
    /// サーバー側でのみ処理する
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // プレイヤーの ComboSystem を持つオブジェクトのみ対象
        var comboSystem = other.GetComponent<ComboSystem>();
        if (comboSystem == null) return;

        // 仙箪カウント+1
        comboSystem.AddSentan(1);

        Debug.Log($"[Sentan] {other.gameObject.name} が仙箪を取得");

        DespawnSelf();
    }

    // ============================================================
    // 視覚表現
    // ============================================================

    /// <summary>
    /// 金色の小さな球体で仙箪アイテムを表現
    /// サーバー・クライアント両方で生成
    /// </summary>
    private void CreateVisual()
    {
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "SentanVisual";
        visual.transform.SetParent(transform);
        visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        visual.transform.localScale = Vector3.one * 0.4f;

        // プリミティブの SphereCollider は不要（Awake で追加したトリガーを使う）
        var sphereCol = visual.GetComponent<SphereCollider>();
        if (sphereCol != null) Destroy(sphereCol);

        var renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(1f, 0.84f, 0f); // 金色
        }
    }

    // ============================================================
    // デスポーン
    // ============================================================

    /// <summary>自身をデスポーンする（寿命切れ or プレイヤーが取得時）</summary>
    private void DespawnSelf()
    {
        if (IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}
