using Fusion;
using UnityEngine;

/// <summary>
/// Networked capsule player. Movement is predicted/simulated in FixedUpdateNetwork
/// via NetworkCharacterController; color is derived from the owning PlayerRef.
/// </summary>
[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerCapsule : NetworkBehaviour
{
    [Networked] private NetworkButtons PrevButtons { get; set; }

    private NetworkCharacterController _cc;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        // Safety net: on a client, a player can be replicated before NetworkBootstrap.Start runs.
        // PlayerCapsule sits on the prefab root, so its Spawned runs before the Voice child's
        // VoiceNetworkObject.Spawned, which needs the voice client on the runner.
        VoiceRunnerSetup.Ensure(Runner);

        // Deterministic per-player color, same on every peer.
        float hue = (Object.InputAuthority.PlayerId * 0.618034f) % 1f;
        var color = Color.HSVToRGB(hue, 0.7f, 0.95f);

        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, color);
        // Only the capsule body (root renderer); children like the speaking indicator keep their own look.
        if (TryGetComponent<Renderer>(out var body))
            body.SetPropertyBlock(block);

        if (HasInputAuthority)
            LocalCameraFollow.Target = transform;

        name = $"Player_{Object.InputAuthority.PlayerId}{(HasInputAuthority ? " (local)" : "")}";
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (LocalCameraFollow.Target == transform)
            LocalCameraFollow.Target = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
            return;

        var move = new Vector3(input.Move.x, 0f, input.Move.y);
        _cc.Move(Vector3.ClampMagnitude(move, 1f));

        if (input.Buttons.WasPressed(PrevButtons, InputButton.Jump))
            _cc.Jump();

        PrevButtons = input.Buttons;
    }
}
