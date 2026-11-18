# Making a Simple Multiplayer Game with Photon Fusion 2

This guide walks through how this project's multiplayer prototype was built: colored capsule players that move, jump and look around together online. Follow the steps in order.

**Tools used:** Unity 6 (6000.0.42f1, URP), Photon Fusion 2 (2.1.3), Unity Input System.

---

## How it works (the big picture)

```
 Player presses Play
        │
        ▼
 NetworkBootstrap starts a NetworkRunner  ──►  Photon Cloud (session "proto")
        │
        ├─ First player in?  → becomes the HOST (runs the real game)
        └─ Otherwise         → joins as a CLIENT
        │
        ▼
 Host spawns a capsule for every player who joins
        │
        ▼
 Every tick: each client sends its input (move + jump) → host moves the capsules
             → everyone sees the same result
```

- The **host** is the boss. It decides where every player really is.
- **Clients** only send their input (keys pressed). They also *predict* their own movement so it feels instant.
- The **NetworkRunner** is Fusion's engine. It connects, syncs and ticks the game.

---

## Step 1: Install Fusion and get an App ID

1. Import the **Photon Fusion 2** SDK into the project (from the Asset Store or Photon's website).
2. Create a free account at the Photon dashboard and make a new **Fusion** app. Copy its **App ID**.
3. In Unity, open **Tools → Fusion → Fusion Hub**, go to **Configuration**, and paste the App ID.
   - It's saved in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`. Keep it private.

---

## Step 2: Make a folder for your own work

Keep your files separate from Photon's:

```
Assets/_Project/
├── Scripts/     ← game code
├── Editor/      ← editor-only tools
├── Prefabs/
└── Resources/   ← menu scene-info asset
Assets/Scenes/   ← FusionSampleMenu.unity, Prototype.unity
```

> ⚠️ **Do not** put your game scripts inside an Assembly Definition (`.asmdef`) unless you also add its name to
> **NetworkProjectConfig → Assemblies To Weave**. Fusion rewrites networked code after compiling. If it skips your
> assembly, networked variables silently stop working.

---

## Step 3: Define what input gets sent over the network

**File:** `Scripts/NetworkInputData.cs`

This is the "message" each client sends to the host every tick.

```csharp
public enum InputButton { Jump = 0 }

public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;            // direction to walk (world space x, z)
    public NetworkButtons Buttons;  // which buttons are held (Jump)
}
```

Keep it small: only send what the host needs to simulate the player.

---

## Step 4: Start the network and spawn players

**File:** `Scripts/NetworkBootstrap.cs` (put it on an empty GameObject in the scene)

It does four jobs:

| Job | Where in the code | What happens |
|---|---|---|
| **Connect** | `Start()` | Adds a `NetworkRunner` and calls `StartGame` with `GameMode.AutoHostOrClient` and session name `"proto"` |
| **Spawn** | `OnPlayerJoined()` | *Host only.* Calls `runner.Spawn(playerPrefab, position, rotation, player)` |
| **Despawn** | `OnPlayerLeft()` | *Host only.* Calls `runner.Despawn(...)` on that player's capsule |
| **Read input** | `OnInput()` | Reads WASD, Space and the camera direction, then calls `input.Set(data)` |

Key parts:

```csharp
_runner = gameObject.AddComponent<NetworkRunner>();
_runner.ProvideInput = true;          // this machine has a player that sends input
_runner.AddCallbacks(this);           // so OnPlayerJoined / OnInput get called

await _runner.StartGame(new StartGameArgs {
    GameMode     = GameMode.AutoHostOrClient,
    SessionName  = "proto",
    Scene        = sceneInfo,
    SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
});
```

> 💡 Always spawn networked objects with `runner.Spawn(...)`, **never** with `Instantiate(...)`.

---

## Step 5: Make the player move

**File:** `Scripts/PlayerCapsule.cs` (a `NetworkBehaviour` on the player prefab)

```csharp
[Networked] private NetworkButtons PrevButtons { get; set; }

public override void FixedUpdateNetwork()
{
    if (!GetInput(out NetworkInputData input)) return;

    _cc.Move(new Vector3(input.Move.x, 0, input.Move.y));     // walk

    if (input.Buttons.WasPressed(PrevButtons, InputButton.Jump))
        _cc.Jump();                                          // jump once per press

    PrevButtons = input.Buttons;
}
```

- `FixedUpdateNetwork()` is Fusion's version of `FixedUpdate`. **All gameplay that must stay in sync goes here.**
- `_cc` is Fusion's built-in **`NetworkCharacterController`**, which handles gravity, jumping and prediction for you.
- `[Networked]` variables are synced automatically. They **must** be auto-properties: `{ get; set; }`.
- `Spawned()` gives each player a different color and tells the camera which capsule is "me" (`HasInputAuthority`).

---

## Step 6: Add a camera that looks around

**File:** `Scripts/LocalCameraFollow.cs` (on the Main Camera)

- Orbits around **your own** capsule with the mouse or right stick.
- Click the game world to lock the mouse; press **Esc** to unlock it. Clicking UI buttons (like the menu's gear) doesn't lock the mouse.
- Pulls in closer if a wall is in the way.
- Shares its facing direction (`LocalCameraFollow.Yaw`) so `OnInput` can make **W = forward on screen**:

```csharp
var world = Quaternion.Euler(0, LocalCameraFollow.Yaw, 0) * new Vector3(move.x, 0, move.y);
data.Move = new Vector2(world.x, world.z);
```

The camera is **not** networked. Each player has their own camera, and only the resulting movement direction is sent.

---

## Step 7: Build the player prefab and the scene

### Option A: Automatic (recommended)

Run **Tools → Prototype → Build Multiplayer Scene**. It creates:

- `Prefabs/Player.prefab`: Capsule + `NetworkObject` + `NetworkCharacterController` + `PlayerCapsule`
- `Assets/Scenes/Prototype.unity`: ground, light, a few boxes, the camera and `NetworkBootstrap`
- Adds the scene to **Build Settings**, after the menu scene if it's installed

If the **Player Prefab** field on `NetworkBootstrap` is empty afterwards, run **Tools → Prototype → Rewire Player Prefab**.

### Option B: By hand

1. **Player prefab**
   1. *GameObject → 3D Object → Capsule*, name it `Player`.
   2. Remove the **Capsule Collider** (the Character Controller replaces it).
   3. Add components: **Network Object**, **Network Character Controller**, **Player Capsule**.
   4. Drag it into `Assets/_Project/Prefabs/` to make it a prefab, then delete it from the scene.
2. **Scene**
   1. Add a **Plane** (scale 4, 1, 4) for the ground.
   2. Add **Local Camera Follow** to the Main Camera.
   3. Create an empty GameObject `NetworkBootstrap`, add the **Network Bootstrap** script, and drag `Player.prefab` into **Player Prefab**.
   4. Save the scene and add it to **File → Build Profiles / Build Settings**.

---

## Step 8: Add the Fusion Menu (rooms and party codes)

The Fusion Menu is a ready-made main menu with player name, **Quick Play**, **Party** (create or join with a code) and settings.

1. Open **Tools → Fusion → Fusion Hub → Welcome**, then click **Install Fusion Menu** (Step 4).
   The menu scene is `Assets/Scenes/FusionSampleMenu.unity`.
2. Run **Tools → Prototype → Setup Fusion Menu**. It:
   - creates `Assets/_Project/Resources/PrototypeSceneInfo.asset`, which tells the menu "Capsule Prototype" is a playable scene;
   - sets Build Settings to **FusionSampleMenu (0) → Prototype (1)**.
3. Open `FusionSampleMenu` and press **Play** → **Quick Play**.

How it fits together:

```
FusionSampleMenu  ──(creates NetworkRunner + StartGame)──►  loads Prototype.unity
                                                               │
                                       NetworkBootstrap.Start: │ runner already exists?
                                         yes → attach to it (callbacks + input) and spawn players
                                         no  → start its own runner (direct Play in Prototype)
```

| Menu button | Game mode used |
|---|---|
| Quick Play | `AutoHostOrClient` |
| Party → Create | `Host` |
| Party → Join (code) | `Client` |

**In game**, the **gear** in the top right opens the in-game panel. It shows the party code (with Copy), the players, ping and FPS, plus **Disconnect** to return to the menu. It doesn't pause the game. The dot inside the gear shows your ping: green is good. Press **Esc** to free the mouse before clicking it.

> ⚠️ The menu switches to **Shared** mode if the *scene name* contains `Shared`. Keep "Shared" out of your gameplay scene's name.

You can still press Play directly in `Prototype.unity` for quick tests. It skips the menu and auto-joins session `proto`.

---

## Step 9: Add voice chat (Photon Voice)

Players can talk to each other. Voice is **3D**, so nearby players sound louder.

### 9a. Install and configure

1. Install **Git for Windows** if you don't have it. Unity needs it to download Git packages. Restart Unity and Unity Hub afterwards.
2. **Fusion Hub → Addons → Voice for Fusion** installs `com.photonengine.voice-fusion`.
3. In the Photon dashboard, create a new app of type **Voice** and copy its App ID.
4. Paste it into **Fusion Hub → Configuration → App Id Voice** (`PhotonAppSettings.AppIdVoice`).
   > Voice uses its **own** App ID, separate from the Fusion one. Without it, voice can't connect, but the game still works.

### 9b. How it's wired

| Piece | Where | Job |
|---|---|---|
| `FusionVoiceClient` | Added to the **NetworkRunner** at runtime by `VoiceRunnerSetup.Ensure` | Connects to Photon Voice and follows the Fusion session |
| `VoiceNetworkObject` | `Player.prefab → Voice` | Links this player's mic and speaker to the network |
| `Recorder` | `Player.prefab → Voice` | Captures **your** microphone (local player only) |
| `Speaker` + `AudioSource` | `Player.prefab → Voice` | Plays **other** players' voices, in 3D (2 m to 25 m) |
| `PlayerVoice` | `Player.prefab → Voice` | Push-to-talk / open mic and the speaking dot |
| `SpeakingIndicator` | `Player.prefab` | Green sphere above the head while that player talks |

> ⚠️ The voice client **must be on the runner before players spawn**, because `VoiceNetworkObject` looks for it in `Spawned()`.
> That's why `NetworkBootstrap` calls `VoiceRunnerSetup.Ensure`, and `PlayerCapsule.Spawned` calls it again as a backup.

To add voice to an existing player prefab: **Tools → Prototype → Add Voice To Player Prefab**.

### 9c. Controls

- **Hold V** to talk (push-to-talk). A green dot appears over your capsule.
- For always-on mic: select `Player.prefab → Voice`, then **Player Voice → Mode → Open Mic**. It only sends when it detects your voice.

---

## Step 10: Test with two players

You need two copies of the game running:

1. **File → Build And Run** to make a Windows build and start it. In the menu, click **Quick Play** (or **Party → Create** and note the code). It becomes the **host**.
2. Press **Play** in the Unity Editor with `FusionSampleMenu` open, then **Quick Play** (or **Party → Join** with the code). It joins as a **client**.
3. Check that:
   - ✅ Two capsules appear, in different colors.
   - ✅ Each window controls only its own capsule (WASD, mouse, Space).
   - ✅ Movement shows up in both windows.
   - ✅ Closing one window makes its capsule disappear in the other.
   - ✅ Holding **V** in one window lets the other hear you, and the green dot shows over the talking capsule. Use headphones to avoid echo when both run on one PC.

The console should print `[NetworkBootstrap] Started as Host` (or `Client`) and `Spawned player ...`.

---

## Common problems

| Problem | Cause / fix |
|---|---|
| `ArgumentNullException: prefab` on Play | `NetworkBootstrap` has no Player Prefab. Run **Tools → Prototype → Rewire Player Prefab**. |
| Networked variable never changes | It's not an auto-property, or the script is in an assembly that isn't woven (see Step 2). |
| Both windows move the same capsule | Movement code isn't using `GetInput(...)` inside `FixedUpdateNetwork()`. |
| Mouse doesn't turn the camera | Click inside the Game view first to lock the cursor. |
| Can't connect at all | Check the App ID in Fusion Hub → Configuration and your internet connection. |
| Players don't see each other | Both must use the **same session name** and the same Photon region. With the menu, both must Quick Play or use the same party code, with the same App Version and region in Settings. |
| No voice; console mentions AppId / authentication | Set **App Id Voice** (a separate Voice-type app) in Fusion Hub → Configuration. |
| Voice connects but nobody hears you | Hold **V** while talking, or check your mic in Windows Sound settings. The Recorder uses the default input device. |
| "No 'git' executable was found" when adding Voice | Install Git for Windows, then restart Unity and Unity Hub. |
| Menu Play button greyed out ("No valid scene") | Run **Tools → Prototype → Setup Fusion Menu** so `PrototypeSceneInfo.asset` exists in a `Resources` folder. |

---

## Golden rules to remember

1. **Host decides, clients ask.** Clients send input; the host moves things.
2. Synced gameplay goes in **`FixedUpdateNetwork()`**, not `Update()`.
3. Spawn with **`runner.Spawn`**, never `Instantiate`.
4. Synced data uses **`[Networked]` auto-properties**.
5. Only send **input** over the network, not cameras, UI or visual effects.

---

## What's next

- Add shooting or interaction using RPCs or networked input buttons.
- Add a name tag above each capsule.
