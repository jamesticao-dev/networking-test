# Make Your First Online Multiplayer Game (Unity + Photon Fusion 2)

This guide is for people who are **new to Unity**. By the end you'll have colored capsule players that walk, jump and look around **together online**.

For every step it tells you:

- **what GameObject to make**,
- **which components to add, and why**,
- **which script to write, and how it connects to the others**.

Voice chat is a separate add-on in **[VOICE_CHAT_GUIDE.md](VOICE_CHAT_GUIDE.md)**. Finish this guide first.

**Tools:** Unity 6 (6000.0.42f1, URP template), Photon Fusion 2 (2.1.3), Unity Input System (already in the URP template).

---

## Part 0: A few Unity words you'll see

| Word | Meaning |
|---|---|
| **GameObject** | Anything in your scene: a player, the floor, the camera. On its own it's an empty box. |
| **Component** | A piece of behavior you attach to a GameObject, like *Mesh Renderer* (draws it) or *Character Controller* (lets it walk). Your C# scripts are components too. |
| **Inspector** | The panel on the right that shows a selected GameObject's components. Click **Add Component** at the bottom to add one. |
| **Hierarchy** | The panel on the left that lists the GameObjects in the open scene. |
| **Project window** | The panel at the bottom that shows your files (`Assets/...`). |
| **Prefab** | A saved GameObject template. You drag a GameObject from the Hierarchy into the Project window to make one. The network creates a copy of the player prefab for each player. |
| **Scene** | A level (`.unity` file). This project has two: a menu and the gameplay scene. |

And a few networking words:

| Word | Meaning |
|---|---|
| **Host** | The first player in. Their computer runs the "real" game and decides where everyone is. |
| **Client** | Everyone else. They send their key presses to the host. |
| **NetworkRunner** | Fusion's engine. It connects to Photon's servers and keeps every computer in sync. |
| **Tick** | One network update step. Fusion runs many per second. |
| **Input authority** | "This capsule belongs to me." Only one player has it for each capsule. |

---

## Part 1: How the whole thing fits together

```
 You press Play
      │
      ▼
 NetworkBootstrap (a script on an empty GameObject)
 starts a NetworkRunner ──────────────►  Photon Cloud, session "proto"
      │
      ├─ first one in?  → you are the HOST
      └─ otherwise      → you are a CLIENT
      │
      ▼
 The host spawns one Player prefab per player
      │
      ▼
 Every tick:
   each computer reads its keyboard   (NetworkBootstrap.OnInput)
   → sends it to the host             (NetworkInputData)
   → the host moves the capsules      (PlayerCapsule.FixedUpdateNetwork)
   → everyone sees the same result
```

**The scripts and who talks to whom:**

```
 NetworkInputData   ← the "message" (move direction + jump button)
        ▲  filled in by                     read by  ▼
 NetworkBootstrap.OnInput()          PlayerCapsule.FixedUpdateNetwork()
        ▲
        │ asks "which way is forward?"
 LocalCameraFollow.Yaw  ◄── PlayerCapsule tells the camera which capsule is "me"
```

| Script | Goes on | Job |
|---|---|---|
| `NetworkInputData.cs` | Nothing (it's just data) | Describes what each player sends every tick |
| `NetworkBootstrap.cs` | Empty GameObject `NetworkBootstrap` in the scene | Connects, spawns and removes players, reads the keyboard |
| `PlayerCapsule.cs` | The **Player** prefab | Moves and jumps the capsule, gives it a color |
| `LocalCameraFollow.cs` | The **Main Camera** | Follows your own capsule and turns with the mouse |

---

## Part 2: Install Photon Fusion and get an App ID

1. Import the **Photon Fusion 2** SDK (Asset Store or Photon's website). It lands in `Assets/Photon/`. Don't edit files in there.
2. Go to the **Photon dashboard** (photonengine.com), create a free account and click **Create a new app**. Choose type **Fusion**, then copy the **App ID**.
3. In Unity, open **Tools → Fusion → Fusion Hub → Configuration** and paste the App ID.
   - It's saved in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`. **Keep it private.** Don't post it online.
4. In the same asset, set **Fixed Region** to `asia` (or whichever region is closest to all your players).
   - **Why:** Photon has separate servers per region. Two players on different regions can never see each other.

---

## Part 3: Make your folders

In the **Project** window, right-click `Assets` → **Create → Folder**. Make:

```
Assets/
├── _Project/
│   ├── Scripts/     ← your C# scripts
│   ├── Editor/      ← editor-only tools (optional)
│   ├── Prefabs/     ← the Player prefab
│   └── Resources/   ← menu settings (Part 9)
└── Scenes/          ← your scenes
```

> ⚠️ **Don't** add an *Assembly Definition* (`.asmdef`) to your Scripts folder. Fusion rewrites networked code after it compiles,
> and it only does this for the assemblies listed in **NetworkProjectConfig → Assemblies To Weave**. If you add an
> `.asmdef`, you must add its name to that list, or your networked variables will silently stop working.

---

## Part 4: Write the scripts

Create each file in `Assets/_Project/Scripts/` (right-click → **Create → MonoBehaviour Script** or **Empty C# Script**). The **file name must match the class name** exactly.

> The code below is the **no-voice** version. This project's own `NetworkBootstrap.cs` and `PlayerCapsule.cs` also have
> three `VoiceRunnerSetup.Ensure(...)` lines; [VOICE_CHAT_GUIDE.md](VOICE_CHAT_GUIDE.md) (Part 4) adds them.
> Don't copy those lines before installing Photon Voice, or the project won't compile.

### Script 1: `NetworkInputData.cs`, the message each player sends

**What it is:** A small struct listing what a player is pressing. Every tick, each computer fills one in and sends it to the host.

**Why it's small:** It's sent many times a second. Only send what the host needs to move you.

```csharp
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
```

- `INetworkInput` tells Fusion "this is input data, send it every tick."
- `NetworkButtons` packs many buttons into one number. We only use **Jump** for now. Add more names to `InputButton` later (for example `Fire = 1`).

---

### Script 2: `LocalCameraFollow.cs`, the camera

**Goes on:** the **Main Camera**.

**What it does:**
- Orbits around **your own** capsule. The mouse or right stick turns it.
- Click the game to lock the mouse. Press **Esc** to free it. Clicking UI buttons doesn't lock it.
- Moves closer if a wall is in the way.
- Shares which way it faces (`Yaw`), so **W always means "forward on screen"**.

**Why it's not networked:** Each player has their own camera. Nobody else needs to know where yours points. Only the resulting move direction is sent.

**How it connects:**
- `PlayerCapsule` sets `LocalCameraFollow.Target` to your capsule when it spawns.
- `NetworkBootstrap` reads `LocalCameraFollow.Yaw` to turn WASD into a world direction.

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Third-person orbit camera for the local player's capsule.
/// Mouse / right stick rotates around the player; movement input is made
/// relative to <see cref="Yaw"/> by NetworkBootstrap.
/// Click the game world to lock the cursor, Esc to release it. Clicks on UI don't lock.
/// </summary>
public class LocalCameraFollow : MonoBehaviour
{
    public static Transform Target;

    /// <summary>Current camera yaw in degrees (world space), used to orient movement input.</summary>
    public static float Yaw { get; private set; }

    [Header("Orbit")]
    [SerializeField] private float distance = 6f;
    [SerializeField] private float pivotHeight = 1.5f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Sensitivity")]
    [SerializeField] private float mouseSensitivity = 0.15f;   // degrees per pixel
    [SerializeField] private float stickSensitivity = 180f;    // degrees per second
    [SerializeField] private bool invertY;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private float _yaw;
    private float _pitch = 20f;
    private InputAction _lookAction;

    private void Start()
    {
        _lookAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Look") : null;
        _yaw = transform.eulerAngles.y;
        Yaw = _yaw;
    }

    private void Update()
    {
        // Cursor lock: click the game world to capture, Esc to release.
        // Clicks on UI (e.g. the Fusion Menu gear / in-game panel) leave the cursor free.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            SetCursorLocked(true);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        ReadLook();
    }

    private void ReadLook()
    {
        Vector2 delta = Vector2.zero;
        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

        if (_lookAction != null)
        {
            var raw = _lookAction.ReadValue<Vector2>();
            bool fromGamepad = _lookAction.activeControl?.device is Gamepad;

            if (fromGamepad)
                delta = raw * (stickSensitivity * Time.deltaTime);
            else if (cursorLocked)
                delta = raw * mouseSensitivity;
        }
        else if (cursorLocked && Mouse.current != null)
        {
            delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        _yaw += delta.x;
        _pitch += invertY ? delta.y : -delta.y;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        Yaw = _yaw;
    }

    private void LateUpdate()
    {
        if (Target == null) return;

        var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        var pivot = Target.position + Vector3.up * pivotHeight;
        var back = rotation * Vector3.back;

        // Pull the camera in if something is between it and the player.
        float dist = distance;
        if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore)
            && hit.transform != Target)
        {
            dist = Mathf.Max(0.5f, hit.distance);
        }

        transform.SetPositionAndRotation(pivot + back * dist, rotation);
    }

    private static bool IsPointerOverUI()
    {
        // Only meaningful while the cursor is free; a locked cursor never "hovers" UI.
        return Cursor.lockState != CursorLockMode.Locked
            && EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject();
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDisable() => SetCursorLocked(false);
}
```

> `InputSystem.actions` is the project's input actions asset (the URP template ships one with **Player/Move**, **Player/Look** and **Player/Jump**).
> If it's missing, the script falls back to reading the mouse directly.

---

### Script 3: `PlayerCapsule.cs`, the player

**Goes on:** the **Player** prefab (Part 5).

**What it does:**
- `Spawned()` runs once when the capsule appears on a computer. It picks a color from the player's number (the same color on every computer) and, **if this capsule is mine**, tells the camera to follow it.
- `FixedUpdateNetwork()` runs every tick. It reads that player's input and moves the capsule.

**Why it's a `NetworkBehaviour` and not a `MonoBehaviour`:** A `NetworkBehaviour` gets Fusion's tick (`FixedUpdateNetwork`), `[Networked]` variables and input. A normal `MonoBehaviour` doesn't.

**How it connects:**
- `GetInput(out NetworkInputData input)` receives what `NetworkBootstrap.OnInput` sent for **this capsule's owner**. That's why each player moves only their own capsule.
- Movement goes through the `NetworkCharacterController` component, which handles gravity, jumping and smooth prediction.

```csharp
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
        // Deterministic per-player color, same on every peer.
        float hue = (Object.InputAuthority.PlayerId * 0.618034f) % 1f;
        var color = Color.HSVToRGB(hue, 0.7f, 0.95f);

        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, color);
        // Only the capsule body (root renderer); children keep their own look.
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
```

- `[Networked]` means "keep this value in sync". It **must** be written as `{ get; set; }` with nothing else.
- `PrevButtons` remembers last tick's buttons, so holding Space jumps **once**, not every tick.
- `[RequireComponent]` makes Unity add a `NetworkCharacterController` automatically if you forget.

---

### Script 4: `NetworkBootstrap.cs`, the connection manager

**Goes on:** an empty GameObject called `NetworkBootstrap` in the gameplay scene (Part 6).

**What it does (four jobs):**

| Job | Method | When it runs |
|---|---|---|
| **Connect** | `Start()` → `AttachToRunner` or `StartStandalone` | When the scene opens |
| **Spawn** a player's capsule | `OnPlayerJoined()`, `OnSceneLoadDone()` | Host only, when someone joins |
| **Remove** a capsule | `OnPlayerLeft()` | Host only, when someone leaves |
| **Read the keyboard** and send it | `OnInput()` | Every tick, on every computer |

**Why "attach or start":** Later (Part 10) a main menu creates the NetworkRunner before this scene loads. Then this script just **joins in** with it. If you press Play directly in the gameplay scene, there's no runner yet, so it **starts its own** in session `proto`. That's handy for quick tests.

**How it connects:**
- `INetworkRunnerCallbacks` is a list of "Fusion, call me when..." methods. `AddCallbacks(this)` subscribes this script to them.
- `playerPrefab` is the **Player** prefab you drag in (Part 6).
- `OnInput` fills a `NetworkInputData` and hands it to Fusion with `input.Set(data)`. Fusion delivers it to that player's `PlayerCapsule`.

```csharp
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

    // Unused callbacks (the interface requires them, so they stay empty)
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
```

Important lines:
- `runner.IsServer`: only the host spawns and removes capsules. Clients never do.
- `runner.Spawn(playerPrefab, pos, rotation, player)`: creates the capsule **on every computer** and makes `player` its owner (input authority). **Never** use `Instantiate` for networked objects.
- `runner.SetPlayerObject(player, obj)`: remembers "this capsule belongs to this player", so we can find and remove it later.

Save all four scripts and wait for Unity to finish compiling (the spinner in the bottom right). The **Console** (**Window → General → Console**) should show no red errors.

---

## Part 5: Build the Player prefab

This is the capsule every player gets.

### 5.1 Create the GameObject

1. In the **Hierarchy**, right-click → **3D Object → Capsule**. Rename it `Player` (press F2).
2. In the **Inspector**, set **Position** to `0, 0, 0`.

A new capsule already has these components:

| Component | Keep? | Why |
|---|---|---|
| **Transform** | Keep | Every GameObject has one: position, rotation, scale |
| **Mesh Filter** | Keep | The capsule shape |
| **Mesh Renderer** | Keep | Draws the shape. `PlayerCapsule` changes its color |
| **Capsule Collider** | **Remove** | The Character Controller below has its own collider. Two colliders would bump into each other. Click the **⋮** on the component → **Remove Component** |

### 5.2 Add components

Click **Add Component** and add these, in this order:

| # | Component (search for) | Why you need it |
|---|---|---|
| 1 | **Network Object** | Makes the GameObject a networked object. Without it, Fusion can't spawn it or sync it across computers. Fusion registers every prefab that has one. |
| 2 | **Network Character Controller** | Fusion's networked walking controller: gravity, jumping, and smoothing so movement feels instant and stays in sync. It also adds Unity's **Character Controller** automatically. |
| 3 | **Player Capsule** (your script) | Reads the player's input and tells the Network Character Controller where to go. Also sets the color and the camera target. |

### 5.3 Set the values

**Character Controller** (added by step 2):

| Field | Value | Why |
|---|---|---|
| Height | `2` | Matches the capsule mesh |
| Radius | `0.5` | Matches the capsule mesh |
| Center | `0, 0, 0` | The mesh is centered on the GameObject |

**Network Character Controller**:

| Field | Value | Why |
|---|---|---|
| Max Speed | `6` | Walking speed (meters per second) |
| Acceleration | `40` | How fast you reach full speed. High = snappy |
| Braking | `40` | How fast you stop |
| Jump Impulse | `7` | How high you jump |

Leave the **Network Object** settings at their defaults.

### 5.4 Turn it into a prefab

1. Drag `Player` from the **Hierarchy** into `Assets/_Project/Prefabs/` in the **Project** window. Its icon turns blue: it's a prefab now.
2. **Delete** `Player` from the Hierarchy. The network spawns players, not you.

**Finished prefab:**

```
Player                      (Capsule)
├─ Transform
├─ Mesh Filter / Mesh Renderer
├─ Network Object           ← "I'm networked"
├─ Character Controller     ← collision shape (added automatically)
├─ Network Character Controller ← networked movement
└─ Player Capsule           ← your script
```

---

## Part 6: Build the gameplay scene

1. **File → New Scene → Basic (URP)** (or "Standard"), then **File → Save As** `Assets/Scenes/Prototype.unity`.
   - It already contains a **Main Camera** and a **Directional Light** (the sun).
2. Make these GameObjects:

| GameObject | How to make it | Components to add | Why |
|---|---|---|---|
| **Ground** | Right-click Hierarchy → **3D Object → Plane**. Scale `4, 1, 4` | None (it already has a Mesh Collider) | Something to stand on. A plane is 10×10 m, so scale 4 makes it 40×40 m |
| **Obstacles** *(optional)* | **3D Object → Cube** a few times, e.g. scale `2, 1, 2` at `6, 0.5, 6` | None (cubes have a Box Collider) | Things to walk around and jump on, so you can see movement |
| **Main Camera** | Already there | **Local Camera Follow** | Follows your own capsule and turns with the mouse |
| **NetworkBootstrap** | Right-click Hierarchy → **Create Empty**, rename it | **Network Bootstrap** | Starts the network and spawns players. An *empty* GameObject because it doesn't need to be seen; it only runs code |

3. **Connect the prefab:** select `NetworkBootstrap`. In the Inspector, drag `Assets/_Project/Prefabs/Player.prefab` onto the **Player Prefab** field.
   - **Why:** this is the prefab `runner.Spawn(...)` creates for each player. If it's empty you'll get an error in the Console.
4. Leave **Session Name** as `proto` and **Spawn Radius** as `4`.
5. **Save the scene** (Ctrl+S).
6. **Add it to the build:** **File → Build Profiles** (or Build Settings) → **Add Open Scenes**.
   - **Why:** Fusion loads scenes by their build index. A scene that's not in the list can't be loaded over the network.

**Finished scene:**

```
Prototype
├─ Main Camera          + Local Camera Follow
├─ Directional Light
├─ Ground               (Plane, scale 4,1,4)
├─ Obstacles            (a few Cubes, optional)
└─ NetworkBootstrap     + Network Bootstrap  (Player Prefab = Player.prefab)
```

> **Shortcut:** this project has **Tools → Prototype → Build Multiplayer Scene**, which makes the prefab and scene for you.
> It **overwrites** both (and also adds voice), so use it only to start fresh. If **Player Prefab** is empty afterwards,
> run **Tools → Prototype → Rewire Player Prefab**.

---

## Part 7: First test (one window)

1. Open `Prototype.unity` and press **Play**.
2. The Console should show:
   - `[NetworkBootstrap] Started as Host in session 'proto'`
   - `[NetworkBootstrap] Spawned player ...`
3. A colored capsule appears. Click the Game view, then use **WASD** to move, **Space** to jump, the mouse to look. Press **Esc** to free the mouse.

If that works, the network is running. You're the host of a game with one player.

---

## Part 8: Test with two players

You need two copies of the game:

1. **File → Build And Run**. A Windows build starts. Leave it running; it becomes the **host**.
2. Back in Unity, press **Play**. The Editor joins the same session `proto` as a **client**.
3. Check:
   - ✅ Two capsules, in different colors.
   - ✅ Each window controls only its own capsule.
   - ✅ Moving in one window shows up in the other.
   - ✅ Closing one window makes its capsule disappear in the other.

> Testing with a friend on another computer? Send them your build. You both need the same **App ID**, the same **region**
> (Part 2) and a build made after any network change.

---

## Part 9: Add a main menu (rooms and party codes)

Fusion comes with a ready-made menu: player name, **Quick Play**, **Party** (create a room or join one with a code) and settings. You don't write any code for it.

1. Open **Tools → Fusion → Fusion Hub → Welcome** and click **Install Fusion Menu**. Move or keep the menu scene at `Assets/Scenes/FusionSampleMenu.unity`.
2. Run **Tools → Prototype → Setup Fusion Menu**. It:
   - creates `Assets/_Project/Resources/PrototypeSceneInfo.asset`. This asset tells the menu "there's a playable scene called *Capsule Prototype*". It must be in a `Resources` folder because the menu finds it by name at runtime;
   - sets the build order to **FusionSampleMenu (0) → Prototype (1)**, so the game opens on the menu.
3. Open `FusionSampleMenu` and press **Play → Quick Play**.

**How the menu and your scene connect:**

```
FusionSampleMenu ──(creates the NetworkRunner, connects)──► loads Prototype.unity
                                                               │
                                     NetworkBootstrap.Start(): │ is there already a runner?
                                       yes → attach to it and spawn players   ← menu flow
                                       no  → start our own (session "proto")  ← pressing Play in Prototype
```

| Menu button | What it does |
|---|---|
| Quick Play | Joins any open game, or hosts one if there isn't any |
| Party → Create | Hosts a game with a code to share |
| Party → Join | Joins a friend's game using their code |

**In game**, the **gear** (top right) opens a panel with the party code (with Copy), the player list, ping, FPS and **Disconnect**. It doesn't pause. Press **Esc** first to free the mouse.

> ⚠️ The menu switches to a different mode ("Shared") if the **scene name contains `Shared`**. Don't put that word in your gameplay scene's name.

> 🌏 **Region:** by default the menu picks each player's "best" region, so friends in different countries can end up on different servers.
> This project pins everyone to `asia` with **Fixed Region** (Part 2), plus a small fallback in
> `FusionMenuConnectionBehaviourSdk.CopyAppSettings` (marked `PROJECT CHANGE`). Re-apply it if you re-import the menu.
> To let players choose a region instead (Quick Play only), see 9.1.

### 9.1 Configure the menu (regions, scenes, players)

Most menu options don't need code. They live in two places:

| What | Where | Used for |
|---|---|---|
| **Menu Config** | `Assets/Photon/FusionMenu/FusionMenuConfig.asset` | Max players, app versions, the region list |
| **Scene Info** assets | Any `Resources` folder, e.g. `Assets/_Project/Resources/` | One asset per scene the player can pick |

> To find the Menu Config in use: open `FusionSampleMenu`, select the GameObject with the **Fusion Menu UI Controller**
> component, and click its **Config** field. Unity highlights the asset in the Project window.

> ⚠️ `FusionMenuConfig.asset` is inside `Assets/Photon/`, so **re-importing the Fusion Menu resets it**. Write down your values.

#### Menu Config fields

Select `FusionMenuConfig.asset` and edit these in the Inspector:

| Field | Default here | What it does |
|---|---|---|
| **Max Players** | `6` | Most players allowed in one game. The Settings screen can't go higher. |
| **Available App Versions** | `1.0` | Versions players can pick in **Settings**. Players only find games with the **same** App Version, so use it to keep old and new builds apart. Empty list = the dropdown is hidden. |
| **Available Regions** | *(empty)* | Regions players can pick in **Settings**. Empty list = the dropdown is hidden. |
| **Default Scene Preview** | Fusion image | Picture shown for a scene that has no preview of its own. |

#### Available regions

**Leave the list empty** (this project's setting) if everyone should play in one region. The menu then hides the region dropdown, and every player uses **Fixed Region** from Part 2 (`asia`). This is the simplest way to make sure friends find each other.

**To let players pick a region:**

1. Select `FusionMenuConfig.asset`.
2. Under **Available Regions**, click **+** and add one region code per entry, for example:

   | Entry | Shows in the dropdown as | Connects to |
   |---|---|---|
   | *(empty)* | `Best` | This project's **Fixed Region** (`asia`), thanks to the `PROJECT CHANGE` fallback |
   | `asia` | `asia` | Singapore |
   | `jp` | `jp` | Tokyo |
   | `eu` | `eu` | Amsterdam |
   | `us` | `us` | Washington D.C. |

   Region codes are lowercase. Photon's docs list them all (`asia`, `au`, `cae`, `cn`, `eu`, `hk`, `in`, `jp`, `kr`, `sa`, `tr`, `uae`, `us`, `usw`, `ussc`, `za`). Keep an **empty first entry** so there's a default.
3. Make a new build. Players then pick a region in **Settings → Region**. The menu remembers their pick on that computer.

Things to know before you turn it on:

- **Everyone in one game must pick the same region.** A game in `eu` is invisible to a player on `asia`.
- **Party (create or join with a code) only works with `Best` selected.** This menu version's party screen only knows the "best" region (`FusionMenuConnectionBehaviourSdk.RequestAvailableOnlineRegionsAsync`). With any other region picked, Party shows *"Selected region is not available."* Quick Play works with any region.
- If you remove a region from the list, players who had picked it go back to `Best` automatically.
- "Best" doesn't measure ping in this project. It always means **Fixed Region**.

#### Available scenes

The menu shows every **Scene Info** asset it finds in any `Resources` folder. Each asset is one playable scene.

**To add another playable scene:**

1. Build the new scene like Part 6: Ground, Main Camera with **Local Camera Follow**, and a **NetworkBootstrap** with **Player Prefab** set. Without a `NetworkBootstrap`, nobody spawns.
2. Add it to **File → Build Profiles → Scene List**. Fusion can only load scenes that are in the build.
3. In `Assets/_Project/Resources/`, right-click → **Create → Fusion → Menu → Menu Scene Info**. (Or select `PrototypeSceneInfo.asset` and press **Ctrl+D** to duplicate it.)
4. Fill in the new asset:

   | Field | What to put | Why |
   |---|---|---|
   | **Name** | e.g. `Big Arena` | The text players see. Empty = the scene's file name. |
   | **Scene Path** | Pick the scene | The scene to load when the game starts |
   | **Preview** | A sprite (optional) | A screenshot shown in the menu. Import a PNG and set **Texture Type** to **Sprite (2D and UI)**. |
   | **Is Hidden** | Leave off | Not used by this menu version. To hide a scene, move its asset out of `Resources` instead. |

5. Press Play in `FusionSampleMenu`. With **two or more** scene assets, the scene button on the main screen becomes clickable and opens a list (sorted by Name). The host's pick is the scene everyone plays.

**To remove a scene from the menu:** delete its Scene Info asset, or move it out of the `Resources` folder.

> ⚠️ Same rule as before: if a scene's **file name contains `Shared`**, the menu starts it in Shared mode, which this project's scripts don't support.

---

## Part 10: Add voice chat (optional)

See **[VOICE_CHAT_GUIDE.md](VOICE_CHAT_GUIDE.md)**. It adds push-to-talk, 3D voice and a "talking" dot above each player.

---

## Common problems

| Problem | Cause / fix |
|---|---|
| `Player Prefab is not assigned` / `ArgumentNullException: prefab` | Drag `Player.prefab` onto **NetworkBootstrap → Player Prefab** (or run **Tools → Prototype → Rewire Player Prefab**). |
| The script won't attach: "class cannot be found" | The file name and class name don't match, or there's a compile error. Check the Console. |
| Capsule falls through the floor | The Ground has no collider, or the Player still has its Capsule Collider fighting the Character Controller. |
| A networked variable never changes | It's not written as `{ get; set; }`, or your scripts are in an `.asmdef` that isn't in **Assemblies To Weave** (Part 3). |
| Both windows move the same capsule | Movement must use `GetInput(...)` inside `FixedUpdateNetwork()`, and the prefab must be spawned with the player as the last argument of `runner.Spawn`. |
| Mouse doesn't turn the camera | Click inside the Game view to lock the cursor. |
| Camera doesn't follow anyone | `PlayerCapsule.Spawned` sets the target only for your own capsule. Check that `LocalCameraFollow` is on the Main Camera. |
| Can't connect at all | Check the App ID in **Fusion Hub → Configuration** and your internet. |
| Players don't see each other | Same session name, same region (Part 2), same build. With the menu, both must use Quick Play or the same party code, and the same **App Version** in Settings (set both to `1.0`). |
| Menu's Play button is greyed out ("No valid scene") | Run **Tools → Prototype → Setup Fusion Menu** so `PrototypeSceneInfo.asset` exists in a `Resources` folder. |

---

## Golden rules

1. **Host decides, clients ask.** Clients send input; the host moves things.
2. Synced gameplay goes in **`FixedUpdateNetwork()`**, not `Update()`.
3. Spawn with **`runner.Spawn`**, never `Instantiate`.
4. Synced data uses **`[Networked]` auto-properties** (`{ get; set; }`).
5. Only send **input** over the network, never cameras, UI or effects.
6. Anything networked needs a **Network Object** component.

---

## What's next

- **Voice chat:** [VOICE_CHAT_GUIDE.md](VOICE_CHAT_GUIDE.md).
- Add shooting or interaction: add `Fire = 1` to `InputButton`, set it in `OnInput`, and react to it in `FixedUpdateNetwork`.
- Add a name tag above each capsule.
