using Unity.Netcode;
using UnityEngine;

/// <summary>
/// アーマーシステム（サーバー権威）
///
/// アーマー段階（5段階）と攻撃レベルの比較により、のけぞるかどうかを判定する。
/// アーマーはのけぞり無効化のみ。ダメージは常に通る。
///
/// 判定ルール:
///   攻撃レベル > アーマー段階 → のけぞる（リアクション発生）
///   攻撃レベル ≤ アーマー段階 → のけぞらない（ダメージは受ける）
/// </summary>
public class ArmorSystem : NetworkBehaviour
{
    // ============================================================
    // 同期変数
    // ============================================================

    // 現在のアーマー段階（デフォルト: 1 = 通常、全てのけぞる）
    private readonly NetworkVariable<byte> _armorLevel = new(
        (byte)ArmorLevel.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ============================================================
    // 公開プロパティ
    // ============================================================

    /// <summary>現在のアーマー段階</summary>
    public ArmorLevel CurrentArmorLevel => (ArmorLevel)_armorLevel.Value;

    // ============================================================
    // サーバー側メソッド
    // ============================================================

    /// <summary>
    /// アーマー段階を設定する（装備・バフ・特定モーション中に変更）
    /// ★サーバー側で実行★
    /// </summary>
    public void SetArmorLevel(ArmorLevel level)
    {
        if (!IsServer) return;
        _armorLevel.Value = (byte)level;
    }

    /// <summary>
    /// 攻撃を受けた時にのけぞるか判定する
    /// ★サーバー側で実行★
    /// </summary>
    /// <param name="attackLevel">攻撃レベル</param>
    /// <returns>true: のけぞる, false: のけぞらない（アーマーで耐える）</returns>
    public bool ShouldFlinch(AttackLevel attackLevel)
    {
        return (int)attackLevel > _armorLevel.Value;
    }
}
