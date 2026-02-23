using Unity.Netcode;
using UnityEngine;

/// <summary>
/// エレメンタルガード（EG）システム（サーバー権威）
///
/// ガード中に △ を約1秒押し込みで EG 準備完了。
/// EG 中に攻撃を受けるとカウンター発動（攻撃者を吹き飛ばし）。
/// 維持中は無双ゲージを消費し続ける。
///
/// ステート遷移:
///   Guard/GuardMove + ChargeHeld → EGPrepare（タイマー開始）
///   EGPrepare + タイマー完了 → EGReady（カウンター待ち）
///   EGReady + 被弾 → EGCounter（カウンター発動）
///   EGCounter → タイマー完了 → Guard
///   解除条件: ガード解除 / △離し / 無双ゲージ0
/// </summary>
[RequireComponent(typeof(CharacterStateMachine))]
[RequireComponent(typeof(MusouGauge))]
public class EGSystem : NetworkBehaviour
{
    // ============================================================
    // 参照
    // ============================================================

    private CharacterStateMachine _stateMachine;
    private MusouGauge _musouGauge;

    // ============================================================
    // サーバー側フィールド
    // ============================================================

    // EG 準備タイマー（△押し込み時間）
    private float _egChargeTimer;

    // EGカウンター持続タイマー
    private float _egCounterTimer;

#if UNITY_EDITOR
    /// <summary>デバッグ用: EG強制維持フラグ。true の間は入力・ゲージに関係なくEGReady を維持</summary>
    public bool DebugForceEG { get; set; }
#endif

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>EG 準備完了状態か（EGReady ステート）</summary>
    public bool IsEGReady => _stateMachine != null
        && _stateMachine.CurrentState == CharacterState.EGReady;

    /// <summary>EG 関連のステートか（EGPrepare / EGReady / EGCounter）</summary>
    public bool IsInEGState
    {
        get
        {
            if (_stateMachine == null) return false;
            var state = _stateMachine.CurrentState;
            return state == CharacterState.EGPrepare
                || state == CharacterState.EGReady
                || state == CharacterState.EGCounter;
        }
    }

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _stateMachine = GetComponent<CharacterStateMachine>();
        _musouGauge = GetComponent<MusouGauge>();
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        UpdateEGCounter();
    }

    // ============================================================
    // EG 入力処理（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// 毎ティック呼ばれる EG 入力処理
    /// PlayerMovement の SubmitInputServerRpc / ホスト処理から呼ばれる
    /// </summary>
    /// <param name="chargeHeld">△（右クリック）が押しっぱなしか</param>
    /// <param name="guardHeld">L1（ガード）が押しっぱなしか</param>
    public void ProcessEG(bool chargeHeld, bool guardHeld)
    {
        if (!IsServer) return;

        var state = _stateMachine.CurrentState;

        // EGPrepare 中: タイマー加算 → 完了で EGReady
        if (state == CharacterState.EGPrepare)
        {
            // 解除条件: ガード離し or △離し
            if (!guardHeld || !chargeHeld)
            {
                CancelEG();
                return;
            }

            _egChargeTimer += GameConfig.FIXED_DELTA_TIME;

            if (_egChargeTimer >= GameConfig.EG_CHARGE_SEC)
            {
                _stateMachine.TryChangeState(CharacterState.EGReady);
                Debug.Log($"[EG] {gameObject.name} EG準備完了");
            }
            return;
        }

        // EGReady 中: 維持 or 解除
        if (state == CharacterState.EGReady)
        {
#if UNITY_EDITOR
            // デバッグ強制維持: 入力・ゲージ解除を無視し、ゲージを補充し続ける
            if (DebugForceEG)
            {
                _musouGauge.AddGauge(GameConfig.EG_MUSOU_DRAIN_RATE * GameConfig.FIXED_DELTA_TIME);
                return;
            }
#endif

            // 解除条件: ガード離し or △離し
            if (!guardHeld || !chargeHeld)
            {
                CancelEG();
                return;
            }

            // 無双ゲージ消費
            _musouGauge.ConsumeGauge(GameConfig.EG_MUSOU_DRAIN_RATE * GameConfig.FIXED_DELTA_TIME);
            if (_musouGauge.CurrentGauge <= 0f)
            {
                Debug.Log($"[EG] {gameObject.name} 無双ゲージ切れ → EG解除");
                CancelEG();
                return;
            }
            return;
        }

        // Guard/GuardMove 中に △ 長押し開始 → EGPrepare へ
        if ((state == CharacterState.Guard || state == CharacterState.GuardMove)
            && chargeHeld && guardHeld)
        {
            _egChargeTimer = 0f;
            _stateMachine.TryChangeState(CharacterState.EGPrepare);
            return;
        }
    }

    // ============================================================
    // EG カウンター（★サーバー側で実行★）
    // ============================================================

    /// <summary>
    /// EGReady 中に攻撃を受けた際に呼ばれるカウンター発動
    /// 攻撃者を吹き飛ばし、無双ゲージを消費する
    /// </summary>
    /// <param name="attackerTransform">攻撃者の Transform</param>
    /// <param name="attackerReaction">攻撃者の ReactionSystem</param>
    public void OnEGCounter(Transform attackerTransform, ReactionSystem attackerReaction)
    {
        if (!IsServer) return;

        Debug.Log($"[EG] {gameObject.name} カウンター発動！ → {attackerTransform.name}");

        // 無双ゲージ消費
        _musouGauge.ConsumeGauge(GameConfig.EG_COUNTER_MUSOU_COST);

        // EGCounter ステートに遷移
        _egCounterTimer = GameConfig.EG_COUNTER_DURATION;
        _stateMachine.TryChangeState(CharacterState.EGCounter);

        // 攻撃者に吹き飛ばしリアクション適用
        if (attackerReaction != null)
        {
            // EGカウンターは最高攻撃レベル（アーマー貫通）
            attackerReaction.ApplyReaction(
                HitReaction.Knockback,
                transform.position,
                0, 0,
                AttackLevel.Musou
            );
        }
    }

    // ============================================================
    // EG カウンタータイマー更新
    // ============================================================

    /// <summary>
    /// EGCounter ステートのタイマーを管理し、完了後に Guard に戻す
    /// </summary>
    private void UpdateEGCounter()
    {
        if (_stateMachine.CurrentState != CharacterState.EGCounter) return;

        _egCounterTimer -= GameConfig.FIXED_DELTA_TIME;
        if (_egCounterTimer <= 0f)
        {
            _egCounterTimer = 0f;
            // カウンター完了 → Guard に戻す
            _stateMachine.TryChangeState(CharacterState.Guard);
        }
    }

    // ============================================================
    // EG 解除
    // ============================================================

    /// <summary>
    /// EG を解除して Guard に戻す
    /// </summary>
    private void CancelEG()
    {
        _egChargeTimer = 0f;
        Debug.Log($"[EG] {gameObject.name} EG解除");

        // Guard に戻す（ガードが続いている場合）
        // ステートが EGPrepare/EGReady の場合のみ遷移
        var state = _stateMachine.CurrentState;
        if (state == CharacterState.EGPrepare || state == CharacterState.EGReady)
        {
            _stateMachine.TryChangeState(CharacterState.Guard);
        }
    }
}
