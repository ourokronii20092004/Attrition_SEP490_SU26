using Fusion;
using UnityEngine;

public enum MyButtons
{
    Jump = 0,
    Attack = 1
}

public struct NetworkInputData : INetworkInput
{
    public float horizontalInput;
    public NetworkButtons buttons;
}