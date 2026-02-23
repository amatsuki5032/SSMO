using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 被弾リアクションシステム（サーバー権威）
///
/// ヒット確定後に被弾者に適切なリアクションを適用する:
/// - Flinch: のけぞり（軽）— 通常攻撃 N1〜N4
/// - Stagger: のけぞり（重）— C1 等
/// - Launch: 打ち上げ — C2, C5
/// - Knockback: 吹き飛ばし — C4
/// - Down: ダウン — 各種条件
/// - Stun: 気絶
///
/// 打ち上げ・吹き飛ばしの物理処理（垂直/水平速度）もここで管理する
/// CharacterController を使って FixedUpdate で移動させる
/// </summary>
[RequireComponent(typeof(CharacterStateMachine))]
[RequireComponent(typeof(CharacterController))]
public class ReactionSystem : NetworkBehaviour
{
    // ============================================================
    // 参照
    // ============================================================

    private CharacterStateMachine _stateMachine;
    private CharacterController _characterController;
    private ArmorSystem _armorSystem;

    // ============================================================
    // 物理状態（サーバーのみ）
    // ============================================================

    // 打ち上げ・吹き飛ばし時の速度（サーバーで計算・適用）
    private Vector3 _reactionVelocity;

    // リアクション物理が有効か（Launch/Knockback 中のみ）
    private bool _isReactionPhysicsActive;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
        _characterController = GetComponent<CharacterController>();
        _armorSystem = GetComponent<ArmorSystem>();
    }

    /// <summary>
    /// サーバーのみ: リアクション物理（打ち上げの重力・吹き飛ばしの減速）を更新
    /// </summary>
    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (!_isReactionPhysicsActive) return;

        UpdateReactionPhysics();
    }

    // ============================================================
    // リアクション適用（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 攻撃種別に応じたリアクションタイプを決定する
    /// HitboxSystem のヒット確定後に呼ばれる
    /// </summary>
    /// <param name="comboStep">通常攻撃のコンボ段数（0 = 非N攻撃）</param>
    /// <param name="chargeType">チャージ技番号（0 = 非チャージ）</param>
    /// <param name="isDashAttack">ダッシュ攻撃か</param>
    public static HitReaction GetReactionType(int comboStep, int chargeType, bool isDashAttack)
    {
        // ダッシュ攻撃: のけぞり（軽）
        if (isDashAttack) return HitReaction.Flinch;

        // チャージ攻撃: 技番号に応じたリアクション
        if (chargeType > 0)
        {
            return chargeType switch
            {
                1 => HitReaction.Flinch,      // C1: のけぞり（重）→ Stagger 相当だが Flinch に統一、時間で差別化
                2 => HitReaction.Launch,       // C2: 打ち上げ
                3 => HitReaction.Flinch,       // C3: 連続のけぞり
                4 => HitReaction.Knockback,    // C4: 吹き飛ばし
                5 => HitReaction.Launch,       // C5: まとめて打ち上げ
                6 => HitReaction.Knockback,    // C6: 最大技 → 吹き飛ばし
                _ => HitReaction.Flinch,
            };
        }

        // 通常攻撃: のけぞり（軽）
        if (comboStep > 0) return HitReaction.Flinch;

        return HitReaction.None;
    }

    /// <summary>
    /// 被弾者にリアクションを適用する（サーバー権威）
    /// ステート遷移 + 物理速度の設定を行う
    /// </summary>
    /// <param name="reaction">リアクション種別</param>
    /// <param name="attackerPosition">攻撃者の位置（吹き飛ばし方向の計算用）</param>
    /// <param name="comboStep">攻撃のコンボ段数（のけぞり時間の判定用）</param>
    /// <param name="chargeType">チャージ技番号（のけぞり時間の判定用）</param>
    public void ApplyReaction(HitReaction reaction, Vector3 attackerPosition, int comboStep, int chargeType, AttackLevel attackLevel = AttackLevel.Normal)
    {
        if (!IsServer) return;

        // アーマー判定: のけぞらない場合はリアクションをスキップ（ダメージは別途適用済み）
        if (_armorSystem != null && !_armorSystem.ShouldFlinch(attackLevel))
        {
            Debug.Log($"[Armor] {gameObject.name} アーマーでのけぞり無効 (Armor={_armorSystem.CurrentArmorLevel}, AtkLv={attackLevel})");
            return;
        }

        switch (reaction)
        {
            case HitReaction.Flinch:
                ApplyFlinch(chargeType);
                break;

            case HitReaction.Launch:
                ApplyLaunch();
                break;

            case HitReaction.Knockback:
                ApplyKnockback(attackerPosition);
                break;

            case HitReaction.Slam:
                ApplySlam();
                break;

            case HitReaction.FaceDown:
                _stateMachine.TryChangeState(CharacterState.FaceDownDown);
                break;

            case HitReaction.Crumble:
                _stateMachine.TryChangeState(CharacterState.CrumbleDown);
                break;

            case HitReaction.Stun:
                _stateMachine.TryChangeState(CharacterState.Stun);
                break;

            case HitReaction.None:
            default:
                return;
        }

        Debug.Log($"[Reaction] {gameObject.name} → {reaction}");
    }

    // ============================================================
    // 各リアクション実装
    // ============================================================

    /// <summary>
    /// のけぞり（Flinch）: 短時間の行動不能
    /// チャージ攻撃（C1等）は重いのけぞり、通常攻撃は軽いのけぞり
    /// </summary>
    private void ApplyFlinch(int chargeType)
    {
        // チャージ攻撃は重いのけぞり、通常攻撃は軽いのけぞり
        float duration = chargeType > 0
            ? GameConfig.HITSTUN_HEAVY_DURATION
            : GameConfig.HITSTUN_LIGHT_DURATION;

        _stateMachine.SetHitstunDuration(duration);
        _stateMachine.TryChangeState(CharacterState.Hitstun);

        // のけぞりで物理はリセット
        _isReactionPhysicsActive = false;
        _reactionVelocity = Vector3.zero;
    }

    /// <summary>
    /// 打ち上げ（Launch）: 垂直方向に吹き飛ばし → 重力で落下 → ダウン
    /// 初速 = sqrt(2 * |gravity| * height) で目標高さに到達する
    /// </summary>
    private void ApplyLaunch()
    {
        _stateMachine.TryChangeState(CharacterState.Launch);

        // 目標高さに到達するための初速を計算: v = sqrt(2 * g * h)
        float launchSpeed = Mathf.Sqrt(2f * Mathf.Abs(GameConfig.JUMP_GRAVITY) * GameConfig.LAUNCH_HEIGHT);
        _reactionVelocity = new Vector3(0f, launchSpeed, 0f);
        _isReactionPhysicsActive = true;
    }

    /// <summary>
    /// 吹き飛ばし（Knockback）: 攻撃者の反対方向に水平移動 + のけぞり
    /// </summary>
    private void ApplyKnockback(Vector3 attackerPosition)
    {
        // 攻撃者から被弾者への方向（水平のみ）
        Vector3 knockDir = transform.position - attackerPosition;
        knockDir.y = 0f;
        if (knockDir.sqrMagnitude < 0.001f)
        {
            // 攻撃者と重なっている場合: 攻撃者の正面方向にフォールバック
            knockDir = -transform.forward;
        }
        knockDir.Normalize();

        _stateMachine.SetHitstunDuration(GameConfig.HITSTUN_HEAVY_DURATION);
        _stateMachine.TryChangeState(CharacterState.Hitstun);

        _reactionVelocity = knockDir * GameConfig.KNOCKBACK_FORCE;
        _isReactionPhysicsActive = true;
    }

    /// <summary>
    /// 叩きつけ（Slam）: 空中から急速落下 → ダウン
    /// </summary>
    private void ApplySlam()
    {
        _stateMachine.TryChangeState(CharacterState.Slam);

        // 急速落下（重力の3倍で叩きつけ）
        _reactionVelocity = new Vector3(0f, GameConfig.JUMP_GRAVITY * 3f, 0f);
        _isReactionPhysicsActive = true;
    }

    // ============================================================
    // リアクション物理更新（★サーバー側 FixedUpdate★）
    // ============================================================

    /// <summary>
    /// リアクション中の物理を更新する（打ち上げの重力・吹き飛ばしの減速）
    /// </summary>
    private void UpdateReactionPhysics()
    {
        CharacterState state = _stateMachine.CurrentState;

        // リアクションステートを抜けたら物理を停止
        bool isReactionState = state == CharacterState.Launch
                            || state == CharacterState.Hitstun
                            || state == CharacterState.Slam;

        if (!isReactionState)
        {
            _isReactionPhysicsActive = false;
            _reactionVelocity = Vector3.zero;
            return;
        }

        // 重力適用（Launch / Slam）
        if (state == CharacterState.Launch || state == CharacterState.Slam)
        {
            _reactionVelocity.y += GameConfig.JUMP_GRAVITY * GameConfig.FIXED_DELTA_TIME;
        }

        // 水平減速（Knockback 時のみ: 摩擦で徐々に停止）
        if (state == CharacterState.Hitstun)
        {
            float decel = 10f * GameConfig.FIXED_DELTA_TIME;
            _reactionVelocity.x = Mathf.MoveTowards(_reactionVelocity.x, 0f, decel);
            _reactionVelocity.z = Mathf.MoveTowards(_reactionVelocity.z, 0f, decel);
        }

        // CharacterController で移動
        Vector3 motion = _reactionVelocity * GameConfig.FIXED_DELTA_TIME;
        _characterController.Move(motion);

        // Launch 中に着地したら SprawlDown に遷移
        if (state == CharacterState.Launch && _characterController.isGrounded && _reactionVelocity.y <= 0f)
        {
            _isReactionPhysicsActive = false;
            _reactionVelocity = Vector3.zero;
            _stateMachine.TryChangeState(CharacterState.SprawlDown);
            Debug.Log($"[Reaction] {gameObject.name}: Launch 着地 → SprawlDown");
        }

        // Slam 中に着地したら FaceDownDown に遷移
        if (state == CharacterState.Slam && _characterController.isGrounded)
        {
            _isReactionPhysicsActive = false;
            _reactionVelocity = Vector3.zero;
            _stateMachine.TryChangeState(CharacterState.FaceDownDown);
            Debug.Log($"[Reaction] {gameObject.name}: Slam 着地 → FaceDownDown");
        }
    }

    // ============================================================
    // 外部ヘルパー
    // ============================================================

    /// <summary>
    /// リアクション物理をリセットする（リスポーン等で使用）
    /// </summary>
    public void ResetReactionPhysics()
    {
        _isReactionPhysicsActive = false;
        _reactionVelocity = Vector3.zero;
    }
}
