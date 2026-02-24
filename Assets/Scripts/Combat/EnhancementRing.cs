using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 仙箪強化リングシステム（サーバー権威型）
///
/// 設計意図:
/// - 仙箪を7個集めるとリングが回転開始
/// - R1（Eキー）でスロット発動、回転中のスロット効果を適用
/// - 7スロット構成:
///   1-3: 武器依存（攻撃UP/防御UP/移動UP等）
///   4:   副将強化（固定、M4ではダミー効果）
///   5-6: 武器依存
///   7:   連撃強化（固定）
/// - 死亡時に全強化リセット
/// - 仙箪カウントは ComboSystem._sentanCount を参照
/// </summary>
[RequireComponent(typeof(ComboSystem))]
public class EnhancementRing : NetworkBehaviour
{
    // ============================================================
    // スロット効果の種類
    // ============================================================

    public enum SlotEffect
    {
        AtkUp,          // 攻撃力+10%
        DefUp,          // 防御力+10%
        MoveUp,         // 移動速度+10%（将来用、現在はATKと同等に仮実装）
        SubGeneral,     // 副将強化（M4ではダミー）
        ComboEnhance,   // 連撃強化
    }

    // ============================================================
    // 同期変数
    // ============================================================

    /// <summary>リング回転中か（仙箪7個溜まるとtrue）</summary>
    private readonly NetworkVariable<bool> _isRingActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>現在のリング位置（0〜6、回転で変動）</summary>
    private readonly NetworkVariable<int> _ringPosition = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>発動済み回数（強化段階の追跡用）</summary>
    private readonly NetworkVariable<int> _enhanceCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>攻撃バフ回数（DamageCalculator参照用）</summary>
    private readonly NetworkVariable<int> _atkBuffCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>防御バフ回数（DamageCalculator参照用）</summary>
    private readonly NetworkVariable<int> _defBuffCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>リング回転中か</summary>
    public bool IsRingActive => _isRingActive.Value;

    /// <summary>現在のリング位置（0〜6）</summary>
    public int RingPosition => _ringPosition.Value;

    /// <summary>発動済み回数</summary>
    public int EnhanceCount => _enhanceCount.Value;

    /// <summary>攻撃バフ回数</summary>
    public int AtkBuffCount => _atkBuffCount.Value;

    /// <summary>防御バフ回数</summary>
    public int DefBuffCount => _defBuffCount.Value;

    /// <summary>ATK倍率（1.0 + バフ回数 × 0.1）</summary>
    public float AtkMultiplier => 1f + _atkBuffCount.Value * GameConfig.ATK_BUFF_PER_ENHANCE;

    /// <summary>DEF倍率（1.0 + バフ回数 × 0.1）</summary>
    public float DefMultiplier => 1f + _defBuffCount.Value * GameConfig.DEF_BUFF_PER_ENHANCE;

    // ============================================================
    // スロット配列（武器依存 + 固定スロット）
    // ============================================================

    /// <summary>
    /// 7スロットの効果配列。武器種ごとに1-3, 5-6が変わる
    /// 現時点では全武器共通のデフォルト配列を使用
    /// </summary>
    private SlotEffect[] _slots;

    // ============================================================
    // サーバーローカル変数
    // ============================================================

    private ComboSystem _comboSystem;
    private float _rotationTimer; // リング回転用タイマー

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        _comboSystem = GetComponent<ComboSystem>();
        InitializeSlots();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _isRingActive.Value = false;
            _ringPosition.Value = 0;
            _enhanceCount.Value = 0;
            _atkBuffCount.Value = 0;
            _defBuffCount.Value = 0;
            _rotationTimer = 0f;
        }
    }

    /// <summary>
    /// デフォルトのスロット構成を初期化する
    /// スロット1-3: 武器依存（ATK/DEF/MoveUp）
    /// スロット4:   副将強化（固定）
    /// スロット5-6: 武器依存（ATK/DEF）
    /// スロット7:   連撃強化（固定）
    /// </summary>
    private void InitializeSlots()
    {
        _slots = new SlotEffect[GameConfig.SENTAN_SLOTS];
        _slots[0] = SlotEffect.AtkUp;
        _slots[1] = SlotEffect.DefUp;
        _slots[2] = SlotEffect.MoveUp;
        _slots[3] = SlotEffect.SubGeneral;
        _slots[4] = SlotEffect.AtkUp;
        _slots[5] = SlotEffect.DefUp;
        _slots[6] = SlotEffect.ComboEnhance;
    }

    // ============================================================
    // 更新（サーバー側 FixedUpdate）
    // ============================================================

    private void FixedUpdate()
    {
        if (!IsServer) return;

        // 仙箪7個以上でリング起動
        int sentanCount = _comboSystem != null ? _comboSystem.SentanCount : 0;
        int requiredSentan = GameConfig.SENTAN_REQUIRED_FOR_ENHANCE * (_enhanceCount.Value + 1);

        if (!_isRingActive.Value && sentanCount >= requiredSentan)
        {
            _isRingActive.Value = true;
            _rotationTimer = 0f;
            Debug.Log($"[Ring] {gameObject.name}: リング起動（仙箪 {sentanCount}/{requiredSentan}）");
        }

        // リング回転更新
        if (_isRingActive.Value)
        {
            _rotationTimer += GameConfig.FIXED_DELTA_TIME;
            float interval = 1f / GameConfig.RING_ROTATION_SPEED;
            if (_rotationTimer >= interval)
            {
                _rotationTimer -= interval;
                _ringPosition.Value = (_ringPosition.Value + 1) % GameConfig.SENTAN_SLOTS;
            }
        }
    }

    // ============================================================
    // 発動処理（R1 入力。サーバー権威）
    // ============================================================

    /// <summary>
    /// R1 入力によるスロット発動（PlayerMovement から呼ばれる）
    /// リング回転中のみ有効。現在のスロット効果を適用してリング停止
    /// </summary>
    public void TryActivateSlot()
    {
        if (!IsServer) return;
        if (!_isRingActive.Value) return;

        int slotIdx = _ringPosition.Value;
        SlotEffect effect = _slots[slotIdx];

        ApplySlotEffect(effect);

        _isRingActive.Value = false;
        _enhanceCount.Value++;

        Debug.Log($"[Ring] {gameObject.name}: スロット{slotIdx + 1} 発動 → {effect}（強化{_enhanceCount.Value}回目）");
    }

    /// <summary>
    /// スロット効果を適用する
    /// </summary>
    private void ApplySlotEffect(SlotEffect effect)
    {
        switch (effect)
        {
            case SlotEffect.AtkUp:
                _atkBuffCount.Value++;
                Debug.Log($"[Ring] {gameObject.name}: ATK UP（累計 +{_atkBuffCount.Value * 10}%）");
                break;

            case SlotEffect.DefUp:
                _defBuffCount.Value++;
                Debug.Log($"[Ring] {gameObject.name}: DEF UP（累計 +{_defBuffCount.Value * 10}%）");
                break;

            case SlotEffect.MoveUp:
                // 移動速度バフは将来実装。現在はATKバフと同等に仮実装
                _atkBuffCount.Value++;
                Debug.Log($"[Ring] {gameObject.name}: MOVE UP（仮: ATK UP 代替）");
                break;

            case SlotEffect.SubGeneral:
                // 副将強化はM4後半で実装。現在はダミー
                Debug.Log($"[Ring] {gameObject.name}: 副将強化（ダミー効果）");
                break;

            case SlotEffect.ComboEnhance:
                if (_comboSystem != null)
                {
                    _comboSystem.EnhanceCombo();
                }
                Debug.Log($"[Ring] {gameObject.name}: 連撃強化 → Lv{_comboSystem?.ComboEnhanceLevel}");
                break;
        }
    }

    // ============================================================
    // リセット（死亡時）
    // ============================================================

    /// <summary>
    /// 全強化をリセットする（死亡時に呼ばれる。サーバー専用）
    /// 仙箪カウントはリセットしない（ComboSystem側で管理）
    /// </summary>
    public void ResetAllEnhancements()
    {
        if (!IsServer) return;

        _isRingActive.Value = false;
        _ringPosition.Value = 0;
        _enhanceCount.Value = 0;
        _atkBuffCount.Value = 0;
        _defBuffCount.Value = 0;
        _rotationTimer = 0f;

        // ComboSystem の連撃強化もリセット
        if (_comboSystem != null)
        {
            _comboSystem.ResetEnhancements();
        }

        Debug.Log($"[Ring] {gameObject.name}: 全強化リセット");
    }

    // ============================================================
    // スロット情報取得（UI用）
    // ============================================================

    /// <summary>
    /// 指定スロットの効果名を返す（UI表示用）
    /// </summary>
    public string GetSlotName(int index)
    {
        if (index < 0 || index >= _slots.Length) return "?";
        return _slots[index] switch
        {
            SlotEffect.AtkUp => "ATK",
            SlotEffect.DefUp => "DEF",
            SlotEffect.MoveUp => "MOV",
            SlotEffect.SubGeneral => "SUB",
            SlotEffect.ComboEnhance => "CMB",
            _ => "?"
        };
    }
}
