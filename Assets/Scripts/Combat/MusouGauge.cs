using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 無双ゲージ管理 + 無双乱舞発動（サーバー権威）
///
/// ゲージ増加: 攻撃ヒット / 被弾 / ○長押しチャージ
/// ゲージMAXで ○ → 無双乱舞（無敵 + 連続攻撃）
/// HP20%以下なら真・無双乱舞（持続時間延長）
///
/// ステート遷移:
///   Idle/Move + ○(MAX) → Musou or TrueMusou
///   Hitstun + ○(MAX) → Musou（無双脱出）
///   Idle/Move + ○長押し(非MAX) → MusouCharge（ゲージ溜め）
/// </summary>
[RequireComponent(typeof(CharacterStateMachine))]
[RequireComponent(typeof(HealthSystem))]
public class MusouGauge : NetworkBehaviour
{
    // ============================================================
    // 同期変数
    // ============================================================

    private readonly NetworkVariable<float> _currentGauge = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> _isMusouActive = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    public float CurrentGauge => _currentGauge.Value;
    public float MaxGauge => GameConfig.MUSOU_GAUGE_MAX;
    public bool IsMusouActive => _isMusouActive.Value;
    public bool IsGaugeFull => _currentGauge.Value >= GameConfig.MUSOU_GAUGE_MAX;

    // ============================================================
    // 参照
    // ============================================================

    private CharacterStateMachine _stateMachine;
    private HealthSystem _healthSystem;

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
        _healthSystem = GetComponent<HealthSystem>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 戦闘開始時はゲージ 0
            _currentGauge.Value = 0f;
            _isMusouActive.Value = false;
        }
    }

    // ============================================================
    // ゲージ増減（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// ゲージを加算する（攻撃ヒット・被弾等）
    /// </summary>
    public void AddGauge(float amount)
    {
        if (!IsServer) return;
        if (amount <= 0f) return;

        // 無双発動中はゲージ増加しない
        if (_isMusouActive.Value) return;

        _currentGauge.Value = Mathf.Min(_currentGauge.Value + amount, GameConfig.MUSOU_GAUGE_MAX);
    }

    /// <summary>
    /// ゲージを消費する（EGカウンター・EG維持等）
    /// </summary>
    public void ConsumeGauge(float amount)
    {
        if (!IsServer) return;
        if (amount <= 0f) return;

        _currentGauge.Value = Mathf.Max(0f, _currentGauge.Value - amount);
    }

    // ============================================================
    // 無双乱舞発動（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 無双乱舞の発動を試みる
    /// ゲージMAX + 発動可能ステート → 無双 or 真・無双
    /// </summary>
    /// <returns>発動成功か</returns>
    public bool TryActivateMusou()
    {
        if (!IsServer) return false;

        // ゲージMAX必須
        if (_currentGauge.Value < GameConfig.MUSOU_GAUGE_MAX) return false;

        // 発動可能ステートチェック
        var state = _stateMachine.CurrentState;
        bool canActivate = state == CharacterState.Idle
                        || state == CharacterState.Move
                        || state == CharacterState.Hitstun; // のけぞり脱出

        if (!canActivate) return false;

        // ゲージ全消費
        _currentGauge.Value = 0f;
        _isMusouActive.Value = true;

        // HP20%以下なら真・無双乱舞
        bool isTrueMusou = _healthSystem.GetHpRatio() <= GameConfig.TRUE_MUSOU_HP_THRESHOLD;

        if (isTrueMusou)
        {
            _stateMachine.TryChangeState(CharacterState.TrueMusou);
            Debug.Log($"[Musou] {gameObject.name} 真・無双乱舞発動！");
        }
        else
        {
            _stateMachine.TryChangeState(CharacterState.Musou);
            Debug.Log($"[Musou] {gameObject.name} 無双乱舞発動！");
        }

        return true;
    }

    // ============================================================
    // 無双チャージ（○長押し）（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 無双チャージ処理（○長押し中に毎ティック呼ばれる）
    /// ゲージがMAX未満の時のみチャージ可能
    /// </summary>
    /// <param name="musouHeld">○ボタンが押されているか</param>
    public void ProcessMusouCharge(bool musouHeld)
    {
        if (!IsServer) return;

        var state = _stateMachine.CurrentState;

        // MusouCharge 中: ゲージ加算 or 解除
        if (state == CharacterState.MusouCharge)
        {
            if (!musouHeld)
            {
                // ○離し → Idle に戻る
                _stateMachine.TryChangeState(CharacterState.Idle);
                return;
            }

            // ゲージ加算
            _currentGauge.Value = Mathf.Min(
                _currentGauge.Value + GameConfig.MUSOU_CHARGE_RATE * GameConfig.FIXED_DELTA_TIME,
                GameConfig.MUSOU_GAUGE_MAX
            );

            // ゲージMAX → Idle に戻る（発動は別途 MusouPressed で行う）
            if (_currentGauge.Value >= GameConfig.MUSOU_GAUGE_MAX)
            {
                Debug.Log($"[Musou] {gameObject.name} ゲージMAX");
                _stateMachine.TryChangeState(CharacterState.Idle);
            }
            return;
        }

        // Idle/Move で○長押し開始（ゲージMAX未満時）→ MusouCharge
        if (musouHeld && !IsGaugeFull
            && (state == CharacterState.Idle || state == CharacterState.Move))
        {
            _stateMachine.TryChangeState(CharacterState.MusouCharge);
        }
    }

    // ============================================================
    // 無双終了監視（★サーバー側 FixedUpdate★）
    // ============================================================

    private void FixedUpdate()
    {
        if (!IsServer) return;

        // 無双終了検知: ステートが Musou/TrueMusou でなくなったら _isMusouActive を解除
        // タイマー管理は CharacterStateMachine が行う（InitializeStateTimer で設定済み）
        if (_isMusouActive.Value)
        {
            var state = _stateMachine.CurrentState;
            if (state != CharacterState.Musou && state != CharacterState.TrueMusou)
            {
                _isMusouActive.Value = false;
                Debug.Log($"[Musou] {gameObject.name} 無双終了");
            }
        }
    }
}
