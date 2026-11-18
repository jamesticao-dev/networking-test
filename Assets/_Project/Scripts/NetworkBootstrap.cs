using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Gameplay-scene network glue. Spawns a player per joined client on the host and
/// collects local input.
///
/// Two ways in:
///  - From the Fusion Menu (normal flow): the menu already created and started a
///    NetworkRunner and loaded this scene, so we attach to that runner.
///  - Pressing Play directly in this scene (quick testing): no runner exists, so we
///    start our own with AutoHostOrClient and the fixed session name.
/// </summary>
public class NetworkBootstrap : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject playerPrefab;
    [Tooltip("Session used only when this scene is played directly, without the menu.")]
    [SerializeField] private string sessionName = "proto";
    [SerializeField] private float spawnRadius = 4f;

    private NetworkRunner _runner;
    private bool _ownsRunner;

    private InputAction _moveAction;
    private InputAction _jumpAction;

    private void Start()
    {
        if (InputSystem.actions != null)
        {
            _moveAction = InputSystem.actions.FindAction("Player/Move");
            _jumpAction = InputSystem.actions.FindAction("Player/Jump");
        }

        var existing = FindExistingRunner();
        if (existing != null)
            AttachToRunner(existing);
        else
            StartStandalone();
    }

    private void OnDestroy()
    {
        if (_runner != null && !_ownsRunner)
            _runner.RemoveCallbacks(this);
    }

    private NetworkRunner FindExistingRunner()
    {
        var forScene = NetworkRunner.GetRunnerForScene(gameObject.scene);
        if (forScene != null) return forScene;

        foreach (var r in NetworkRunner.Instances)
            if (r != null && r.IsRunning) return r;

        return null;
    }

    private void AttachToRunner(NetworkRunner runner)
    {
        _runner = runner;
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);
        VoiceRunnerSetup.Ensure(_runner); // must exist before players spawn
        Debug.Log($"[NetworkBootstrap] Attached to menu runner ({_runner.GameMode}, session '{_runner.SessionInfo?.Name}')");

        // Players (including the host itself) may have joined before this scene loaded,
        // so their OnPlayerJoined already fired. Spawn anyone still missing a capsule.
        SpawnMissingPlayers(_runner);
    }

    private async void StartStandalone()
    {
        _ownsRunner = true;
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);
        VoiceRunnerSetup.Ensure(_runner); // added before StartGame, so Fusion registers its callbacks itself

        var sceneInfo = new NetworkSceneInfo();
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        if (scene.IsValid)
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = sessionName,
            Scene = sceneInfo,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
        });

        if (result.Ok)
            Debug.Log($"[NetworkBootstrap] Started as {_runner.GameMode} in session '{sessionName}'");
        else
            Debug.LogError($"[NetworkBootstrap] StartGame failed: {result.ShutdownReason}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) => TrySpawnPlayer(runner, player);

    public void OnSceneLoadDone(NetworkRunner runner) => SpawnMissingPlayers(runner);

    private void SpawnMissingPlayers(NetworkRunner runner)
    {
        if (!runner.IsServer || !runner.IsRunning) return;
        foreach (var player in runner.ActivePlayers)
            TrySpawnPlayer(runner, player);
    }

    private void TrySpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;
        if (runner.TryGetPlayerObject(player, out var existing) && existing != null) return; // already has one

        if (playerPrefab == null)
        {
            Debug.LogError("[NetworkBootstrap] Player Prefab is not assigned. Run Tools/Prototype/Rewire Player Prefab.");
            return;
        }

        float angle = player.PlayerId * 137.5f * Mathf.Deg2Rad; // golden-angle spread
        var pos = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius + Vector3.up;

        var obj = runner.Spawn(playerPrefab, pos, Quaternion.identity, player);
        runner.SetPlayerObject(player, obj);
        Debug.Log($"[NetworkBootstrap] Spawned player {player.PlayerId}");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        if (runner.TryGetPlayerObject(player, out var obj) && obj != null)
        {
            runner.Despawn(obj);
            runner.SetPlayerObject(player, null);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        if (_moveAction != null)
        {
            data.Move = _moveAction.ReadValue<Vector2>();
            data.Buttons.Set(InputButton.Jump, _jumpAction != null && _jumpAction.IsPressed());
        }
        else if (Keyboard.current != null)
        {
            var kb = Keyboard.current;
            data.Move = new Vector2(
                (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            data.Buttons.Set(InputButton.Jump, kb.spaceKey.isPressed);
        }

        // Make movement camera-relative: rotate the stick/WASD vector by the camera yaw,
        // so the host receives a world-space direction (x, z).
        var world = Quaternion.Euler(0f, LocalCameraFollow.Yaw, 0f) * new Vector3(data.Move.x, 0f, data.Move.y);
        data.Move = new Vector2(world.x, world.z);

        input.Set(data);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) =>
        Debug.Log($"[NetworkBootstrap] Shutdown: {shutdownReason}");

    // Unused callbacks
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
#pragma warning disable CS0618 // still part of INetworkRunnerCallbacks in 2.1.3
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
#pragma warning restore CS0618
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
}
