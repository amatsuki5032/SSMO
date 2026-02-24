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
/// - AI行動: 移動 → 敵検出 → 追跡 → 攻撃 → 移動
/// - 攻撃はN1相当（OverlapSphere、サーバー権威判定）
/// - 敵NPC同士も戦闘する
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

    // AI: 追跡対象
    private Transform _currentEnemy;

    // AI: タイマー
    private float _attackCooldown;
    private float _detectTimer;

    // AI: OverlapSphere 用事前確保バッファ（GC回避・全NPCで共有）
    private static readonly Collider[] _detectBuffer = new Collider[32];

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
    // AI行動ループ（サーバー専用・FixedUpdate）
    // ============================================================

    /// <summary>
    /// 毎FixedUpdateでAI行動を実行
    /// 優先度: 攻撃 > 追跡 > パトロール（敵拠点方向移動）
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (IsDead) return;

        // 攻撃クールダウン減少
        if (_attackCooldown > 0f)
            _attackCooldown -= Time.fixedDeltaTime;

        // 定期的に敵をスキャン（毎フレームは重いので間隔を空ける）
        _detectTimer += Time.fixedDeltaTime;
        if (_detectTimer >= GameConfig.NPC_DETECT_INTERVAL)
        {
            _detectTimer = 0f;
            ScanForEnemy();
        }

        // 追跡中の敵が無効になったらクリア
        if (_currentEnemy != null && !IsValidEnemy(_currentEnemy))
        {
            _currentEnemy = null;
        }

        if (_currentEnemy != null)
        {
            // 敵がいる: 距離に応じて攻撃 or 追跡
            float dist = HorizontalDistance(transform.position, _currentEnemy.position);

            if (dist <= GameConfig.NPC_ATTACK_RANGE)
            {
                // 攻撃範囲内: 向きを合わせて攻撃
                FaceTarget(_currentEnemy.position);
                TryAttack();
            }
            else
            {
                // 接近中
                MoveToward(_currentEnemy.position);
            }
        }
        else
        {
            // 敵なし: 敵拠点方向へパトロール
            MoveToward(_targetPosition);
        }
    }

    // ============================================================
    // 敵検出（サーバー専用）
    // ============================================================

    /// <summary>
    /// NPC_DETECT_RANGE 内の最も近い敵を検出する
    /// 対象: 敵プレイヤー + 敵NPC
    /// パフォーマンス: NPC_DETECT_INTERVAL 間隔で実行（約6Hz）
    /// </summary>
    private void ScanForEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, GameConfig.NPC_DETECT_RANGE, _detectBuffer
        );

        float nearestDist = float.MaxValue;
        Transform nearest = null;

        for (int i = 0; i < count; i++)
        {
            var col = _detectBuffer[i];
            if (col.transform == transform) continue; // 自分除外

            // 敵NPCチェック
            var otherNpc = col.GetComponent<NPCSoldier>();
            if (otherNpc != null)
            {
                if (otherNpc.IsDead) continue;
                if (otherNpc.SoldierTeam == SoldierTeam) continue; // 味方NPC除外

                float dist = HorizontalDistance(transform.position, col.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = col.transform;
                }
                continue;
            }

            // 敵プレイヤーチェック
            var netObj = col.GetComponent<NetworkObject>();
            if (netObj == null) continue;

            // NPCSoldier でも NetworkObject でもないコライダーはスキップ
            var hurtbox = col.GetComponent<HurtboxComponent>();
            if (hurtbox == null) continue;

            // TeamManager で敵チーム判定
            if (TeamManager.Instance == null) continue;
            Team playerTeam = TeamManager.Instance.GetPlayerTeam(netObj.OwnerClientId);
            if (playerTeam == SoldierTeam) continue; // 味方プレイヤー除外

            // Dead プレイヤー除外
            var stateMachine = col.GetComponent<CharacterStateMachine>();
            if (stateMachine != null && stateMachine.CurrentState == CharacterState.Dead) continue;

            float playerDist = HorizontalDistance(transform.position, col.transform.position);
            if (playerDist < nearestDist)
            {
                nearestDist = playerDist;
                nearest = col.transform;
            }
        }

        _currentEnemy = nearest;
    }

    /// <summary>
    /// 追跡対象がまだ有効か判定（破棄済み・死亡・デスポーン等を除外）
    /// </summary>
    private bool IsValidEnemy(Transform enemy)
    {
        if (enemy == null) return false;

        // 敵NPC: 死亡チェック
        var otherNpc = enemy.GetComponent<NPCSoldier>();
        if (otherNpc != null)
            return !otherNpc.IsDead && otherNpc.IsSpawned;

        // 敵プレイヤー: Dead チェック
        var stateMachine = enemy.GetComponent<CharacterStateMachine>();
        if (stateMachine != null)
            return stateMachine.CurrentState != CharacterState.Dead;

        return false;
    }

    // ============================================================
    // 攻撃（サーバー専用）
    // ============================================================

    /// <summary>
    /// N1相当の単発攻撃を試行する
    /// NPC_ATK_INTERVAL 間隔でクールダウン管理
    /// 前方の OverlapSphere で敵を検出してダメージ適用
    /// </summary>
    private void TryAttack()
    {
        if (_attackCooldown > 0f) return;

        _attackCooldown = GameConfig.NPC_ATK_INTERVAL;

        // 前方にOverlapSphereで攻撃判定
        Vector3 attackCenter = transform.position + transform.forward * GameConfig.NPC_ATTACK_RANGE * 0.5f;
        attackCenter.y += 0.5f; // 少し上（地面を拾わないように）

        int hitCount = Physics.OverlapSphereNonAlloc(
            attackCenter, GameConfig.NPC_ATTACK_RANGE * 0.7f, _detectBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            var col = _detectBuffer[i];
            if (col.transform == transform) continue; // 自分除外

            // 敵NPCへのダメージ
            var otherNpc = col.GetComponent<NPCSoldier>();
            if (otherNpc != null)
            {
                if (otherNpc.IsDead) continue;
                if (otherNpc.SoldierTeam == SoldierTeam) continue; // 味方除外

                otherNpc.TakeDamage(GameConfig.NPC_ATK);
                Debug.Log($"[NPC-ATK] {gameObject.name} → {otherNpc.gameObject.name} ({GameConfig.NPC_ATK}ダメージ)");
                continue;
            }

            // 敵プレイヤーへのダメージ
            var hurtbox = col.GetComponent<HurtboxComponent>();
            if (hurtbox == null) continue;

            var netObj = col.GetComponent<NetworkObject>();
            if (netObj == null) continue;

            // 味方プレイヤー除外
            if (TeamManager.Instance == null) continue;
            Team playerTeam = TeamManager.Instance.GetPlayerTeam(netObj.OwnerClientId);
            if (playerTeam == SoldierTeam) continue;

            // 無敵チェック
            if (hurtbox.IsInvincible()) continue;

            // ガードチェック（NPC攻撃もガード可能）
            if (hurtbox.IsGuardingAgainst(transform.position))
            {
                Debug.Log($"[NPC-ATK] {gameObject.name} → {netObj.gameObject.name} ガード成功");
                continue;
            }

            // ダメージ適用（NPC_ATK固定、ガード・根性補正なし）
            var health = col.GetComponent<HealthSystem>();
            if (health != null)
            {
                health.TakeDamage(GameConfig.NPC_ATK);
                Debug.Log($"[NPC-ATK] {gameObject.name} → {netObj.gameObject.name} ({GameConfig.NPC_ATK}ダメージ)");
            }

            // 軽いのけぞり（N1相当のリアクション）
            var reaction = col.GetComponent<ReactionSystem>();
            if (reaction != null)
            {
                reaction.ApplyReaction(
                    HitReaction.Flinch, transform.position,
                    comboStep: 1, chargeType: 0, AttackLevel.Arrow
                );
            }
        }
    }

    // ============================================================
    // 移動（サーバー専用）
    // ============================================================

    /// <summary>
    /// 目標地点へ移動する（水平方向のみ）
    /// 目的地に十分近ければ停止
    /// </summary>
    private void MoveToward(Vector3 target)
    {
        Vector3 diff = target - transform.position;
        diff.y = 0f;

        // 目的地に十分近ければ停止
        if (diff.sqrMagnitude < 1f) return;

        Vector3 dir = diff.normalized;
        transform.position += dir * GameConfig.NPC_MOVE_SPEED * Time.fixedDeltaTime;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    /// <summary>
    /// 対象の方向を向く（Y軸回転のみ）
    /// </summary>
    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }

    /// <summary>
    /// 水平距離を計算（Y軸無視）
    /// </summary>
    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
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
