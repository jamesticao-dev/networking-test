using Fusion;
using UnityEngine;

public enum InputButton
{
    Jump = 0,
}

/// <summary>Per-tick input sent from each client to the host.</summary>
public struct NetworkInputData : INetworkInput
{
    /// <summary>World-space move direction (x, z), already rotated by the local camera yaw.</summary>
    public Vector2 Move;
    public NetworkButtons Buttons;
}
