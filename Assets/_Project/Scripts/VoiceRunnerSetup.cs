using Fusion;
using Photon.Voice.Fusion;
using UnityEngine;

/// <summary>
/// Makes sure the NetworkRunner has a FusionVoiceClient. Photon Voice expects the client
/// on the runner's GameObject, and VoiceNetworkObject looks it up in Spawned(), so this must
/// run before any player spawns.
///
/// The runner is created at runtime (by the Fusion Menu, or by NetworkBootstrap when playing
/// the gameplay scene directly), so the client is added from code instead of a runner prefab.
/// </summary>
public static class VoiceRunnerSetup
{
    public static FusionVoiceClient Ensure(NetworkRunner runner)
    {
        if (runner == null) return null;

        var client = runner.GetComponent<FusionVoiceClient>();
        if (client != null) return client;

        client = runner.gameObject.AddComponent<FusionVoiceClient>();
        client.UseFusionAppSettings = true; // Voice App ID comes from PhotonAppSettings (AppIdVoice)
        client.UseFusionAuthValues = true;

        // Callbacks on the runner's GameObject are collected when StartGame is called.
        // If the runner has already started, the client must be registered by hand,
        // otherwise it won't hear about the local player leaving / disconnecting.
        if (runner.State != NetworkRunner.States.Shutdown)
            runner.AddCallbacks(client);

        Debug.Log("[Voice] FusionVoiceClient added to NetworkRunner");
        return client;
    }
}
