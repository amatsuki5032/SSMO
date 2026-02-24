using Unity.Netcode;
using UnityEngine;

/// <summary>
/// プレイヤー入力データ（1ティック分）
/// 全入力を1つの構造体にまとめて ServerRpc で送信する（帯域効率）
/// M2-3以降で使うフィールドは今は false 固定で送信。将来の入力追加時にプロトコル変更が不要
/// </summary>
public struct PlayerInput : INetworkSerializable
{
    public Vector2 MoveInput;       // 移動方向 (H, V)
    public bool JumpPressed;        // ×ボタン（押した瞬間）
    public bool GuardHeld;          // L1（押しっぱなし）
    public bool AttackPressed;      // □（M2-3で使用）
    public bool ChargePressed;      // △（M2-4で使用）
    public bool ChargeHeld;         // △ 長押し（EG準備用）
    public bool MusouPressed;       // ○ 押した瞬間（無双発動）
    public bool MusouHeld;          // ○ 長押し（無双チャージ）
    public bool EnhancePressed;     // R1（Eキー）仙箪強化リング発動
    public bool BreakPressed;       // L2（Rキー）ブレイクチャージ（武器2攻撃）
    public uint Tick;               // ティック番号

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MoveInput);
        serializer.SerializeValue(ref JumpPressed);
        serializer.SerializeValue(ref GuardHeld);
        serializer.SerializeValue(ref AttackPressed);
        serializer.SerializeValue(ref ChargePressed);
        serializer.SerializeValue(ref ChargeHeld);
        serializer.SerializeValue(ref MusouPressed);
        serializer.SerializeValue(ref MusouHeld);
        serializer.SerializeValue(ref EnhancePressed);
        serializer.SerializeValue(ref BreakPressed);
        serializer.SerializeValue(ref Tick);
    }
}
