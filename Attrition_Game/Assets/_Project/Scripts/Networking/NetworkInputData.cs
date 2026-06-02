using Fusion;
using UnityEngine;

public enum MyButtons
{
    Jump = 0,       // Space nhấn (one-shot)
    Attack = 1,     // J nhấn (one-shot)
    Dash = 2,       // Shift nhấn (one-shot)
    Crouch = 3,     // S giữ (continuous)
    AttackHold = 4, // J giữ (continuous) - cho charge attack
    JumpHeld = 5,   // Space giữ (continuous) - cho variable jump
    HealthPotion = 6, // Q nhấn (one-shot) - uống bình máu
    ManaPotion = 7,   // E nhấn (one-shot) - uống bình mana
    Rest = 8          // R nhấn (one-shot) - rest tại checkpoint
}

public struct NetworkInputData : INetworkInput
{
    public float horizontalInput;
    public NetworkButtons buttons;
}