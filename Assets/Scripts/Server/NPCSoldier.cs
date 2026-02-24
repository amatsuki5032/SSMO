using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NPC兵士（雑兵）コンポーネント（サーバー権威型）
///
/// 設計意図:
/// - プレイヤーとは独立した簡易ユニット。ステートマシン不要
/// - サーバーが位置・HP を管理し、NetworkTransform + NetworkVariable で同期
/// - HitboxSystem がこのコンポーネントを検出してダメージ適用
/// - 拠点からスポーンし、敵拠点方向へ自動移動する
/// - 箱人間より小さい（0.6倍スケール）でチーム色分け
/// - 死亡時に仙箪アイテムドロップ（M4向けフラグのみ、実装スキップ）
/// </summary>
public class NPCSoldier : NetworkBehaviour
{
    // ============================================================
    // 同期変数（サーバー書き込み、全員読み取り）
    // ============================================================

    /// <summary>所属チーム</summary>
    private readonly NetworkVariable<byte> _team = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    /// <summary>現在HP</summary>
    private readonly NetworkVariable<int> _currentHp = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    public Team SoldierTeam => (Team)_team.Value;
    public int CurrentHp => _currentHp.Value;
    public int SpawnBaseIndex { get; private set; }
    public bool IsDead { get; private set; }

    // ============================================================
    // ローカル変数
    // ============================================================

    // 移動先（敵拠点座標）
    private Vector3 _targetPosition;

    // 視覚表現
    private Renderer _visualRenderer;

    // ============================================================
    // ライフサイクル
    // ============================================================

    public override void OnNetworkSpawn()
    {
        CreateVisual();
        UpdateColor();

        // チーム変更時に色を更新
        _team.OnValueChanged += OnTeamChanged;
    }

    public override void OnNetworkDespawn()
    {
        _team.OnValueChanged -= OnTeamChanged;
    }

    // ============================================================
    // 初期化（サーバー専用・スポーン後に呼ぶ）
    // ============================================================

    /// <summary>
    /// NPCSpawner から呼ばれる初期設定
    /// NetworkVariable はスポーン後に設定する必要がある
    /// </summary>
    /// <param name="team">所属チーム</param>
    /// <param name="baseIndex">スポーン元拠点番号</param>
    /// <param name="targetPos">移動先座標（敵拠点方向）</param>
    public void Initialize(Team team, int baseIndex, Vector3 targetPos)
    {
        if (!IsServer) return;

        _team.Value = (byte)team;
        _currentHp.Value = GameConfig.NPC_HP;
        SpawnBaseIndex = baseIndex;
        _targetPosition = targetPos;
    }

    // ============================================================
    // 視覚表現
    // ============================================================

    /// <summary>
    /// 小さいキューブで雑兵を表現（プレイヤーの箱人間と区別）
    /// OnNetworkSpawn でサーバー・クライアント両方で生成
    /// </summary>
    private void CreateVisual()
    {
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "NPCVisual";
        visual.transform.SetParent(transform);
        visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        visual.transform.localScale = Vector3.one;

        // プリミティブの BoxCollider は不要（Prefab の CapsuleCollider を使う）
        var boxCol = visual.GetComponent<BoxCollider>();
        if (boxCol != null) Destroy(boxCol);

        _visualRenderer = visual.GetComponent<Renderer>();
    }

    /// <summary>
    /// チーム色で色分け（赤軍=薄赤、青軍=薄青）
    /// プレイヤーの色（濃い赤/青）と区別するため薄めの色にする
    /// </summary>
    private void UpdateColor()
    {
        if (_visualRenderer == null) return;

        _visualRenderer.material = new Material(Shader.Find("Standard"));
        _visualRenderer.material.color = SoldierTeam == Team.Red
            ? new Color(1f, 0.4f, 0.4f)   // 薄赤
            : new Color(0.4f, 0.4f, 1f);  // 薄青
    }

    private void OnTeamChanged(byte prev, byte curr)
    {
        UpdateColor();
    }

    // ============================================================
    // 移動（サーバー専用・FixedUpdate）
    // ============================================================

    /// <summary>
    /// 敵拠点方向へ直線移動する
    /// NetworkTransform で位置がクライアントに同期される
    /// 到着したら停止（将来: 拠点攻撃）
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (IsDead) return;

        Vector3 diff = _targetPosition - transform.position;
        diff.y = 0f;

        // 目的地に十分近ければ停止
        if (diff.sqrMagnitude < 1f) return;

        Vector3 dir = diff.normalized;
        transform.position += dir * GameConfig.NPC_MOVE_SPEED * Time.fixedDeltaTime;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    // ============================================================
    // ダメージ処理（サーバー専用）
    // ============================================================

    /// <summary>
    /// プレイヤーの攻撃でダメージを受ける
    /// HitboxSystem から呼ばれる（サーバー側）
    /// ガード・リアクション・アーマーなし（雑兵はシンプル）
    /// </summary>
    /// <param name="damage">ダメージ量</param>
    public void TakeDamage(int damage)
    {
        if (!IsServer) return;
        if (IsDead) return;
        if (damage <= 0) return;

        _currentHp.Value = Mathf.Max(0, _currentHp.Value - damage);

        Debug.Log($"[NPC] {gameObject.name} が {damage} ダメージ → 残HP: {_currentHp.Value}");

        if (_currentHp.Value <= 0)
        {
            OnDeath();
        }
    }

    /// <summary>
    /// 死亡処理: 一定時間後にデスポーン
    /// M4向け: ここで仙箪アイテムドロップフラグを立てる（未実装）
    /// </summary>
    private void OnDeath()
    {
        IsDead = true;

        // TODO: M4 仙箪アイテムドロップ
        // DropItem();

        Debug.Log($"[NPC] {gameObject.name} 死亡");

        // 短いディレイ後にデスポーン（死亡演出用の猶予）
        Invoke(nameof(DespawnSelf), GameConfig.NPC_DESPAWN_DELAY);
    }

    private void DespawnSelf()
    {
        if (IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}
