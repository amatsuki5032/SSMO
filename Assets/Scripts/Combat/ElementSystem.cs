using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 属性システム（★サーバー権威★）
///
/// プレイヤーの装備属性（種別 + レベル）を管理する。
/// 属性はチャージ攻撃にのみ乗る（通常攻撃・ダッシュ攻撃には乗らない）。
///
/// 属性同士の相性なし。倍率は属性種別×レベルで決まる。
/// 実際のダメージ倍率計算は DamageCalculator.GetElementDamageMultiplier() に委譲。
///
/// 将来的に鍛錬・刻印システムで属性の変更・強化が可能になる（M4-5, M4-6）
/// </summary>
public class ElementSystem : NetworkBehaviour
{
    // ============================================================
    // NetworkVariable（サーバー権威）
    // ============================================================

    /// <summary>装備属性の種別（None=無属性）</summary>
    private readonly NetworkVariable<ElementType> _elementType = new(
        ElementType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>属性レベル（1〜4。0=属性なし）</summary>
    private readonly NetworkVariable<int> _elementLevel = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>現在の装備属性</summary>
    public ElementType CurrentElement => _elementType.Value;

    /// <summary>現在の属性レベル（0=なし、1〜4）</summary>
    public int ElementLevel => _elementLevel.Value;

    // ============================================================
    // 属性設定（サーバー専用）
    // ============================================================

    /// <summary>
    /// 装備属性を設定する（サーバー専用）
    /// 鍛錬・刻印・デバッグ等から呼ばれる
    /// </summary>
    /// <param name="element">属性種別</param>
    /// <param name="level">属性レベル（1〜4）</param>
    public void SetElement(ElementType element, int level)
    {
        if (!IsServer) return;

        // 無属性の場合はレベルも0に
        if (element == ElementType.None)
        {
            _elementType.Value = ElementType.None;
            _elementLevel.Value = 0;
            return;
        }

        _elementType.Value = element;
        _elementLevel.Value = Mathf.Clamp(level, 1, 4);
    }

    // ============================================================
    // 属性判定（HitboxSystem から呼ばれる）
    // ============================================================

    /// <summary>
    /// チャージ攻撃時に属性を付与するか判定し、属性情報を返す
    /// チャージ攻撃（chargeType > 0）の場合のみ属性が乗る
    /// </summary>
    /// <param name="chargeType">チャージ技番号（0=非チャージ）</param>
    /// <param name="outElement">出力: 付与される属性（チャージでなければNone）</param>
    /// <param name="outLevel">出力: 属性レベル（チャージでなければ0）</param>
    public void GetAttackElement(int chargeType, out ElementType outElement, out int outLevel)
    {
        // チャージ攻撃以外には属性が乗らない
        if (chargeType <= 0 || _elementType.Value == ElementType.None)
        {
            outElement = ElementType.None;
            outLevel = 0;
            return;
        }

        outElement = _elementType.Value;
        outLevel = _elementLevel.Value;
    }
}
