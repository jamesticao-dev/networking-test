# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Unity **6000.0.42f1** (Unity 6) project using **URP 17** and the **new Input System**, set up as a testbed for multiplayer networking with **Photon Fusion 2 (2.1.3 Stable, build 2390)**. It started from the Unity URP template.

## Keeping docs current (required)

Every change that **adds, renames, moves or deletes a file**, adds a package, or changes how something works must update, in the same task:

1. **`progress.md`**:
   - Add a dated line under **Done** saying what changed.
   - Keep the **Project files** table accurate (one row per project-made file).
   - Record any design choice under **Decisions / notes** and anything unverified under **Open issues**.
2. **`CLAUDE.md`** (this file): update **Layout that matters** and the **Project files** list below when files, menus, packages or architecture change.
3. **`MULTIPLAYER_GUIDE.md`**: update it when a change affects how a person builds, sets up or plays the game.

Don't finish a task with these out of date.

## Layout that matters

- **Scenes** (`Assets/Scenes/`): `FusionSampleMenu.unity` (Fusion Menu, build index 0) → `Prototype.unity` (capsule gameplay, index 1).
- `Assets/_Project/`: all project code and content.
  - `Scripts/`:
    - `NetworkBootstrap`: lives in `Prototype.unity`. If the menu already started a `NetworkRunner`, it attaches to it (`AddCallbacks`, `ProvideInput = true`). Otherwise, when Play is pressed directly in `Prototype`, it starts its own runner with `AutoHostOrClient` in session `proto`. It spawns players on the host, tracked with `SetPlayerObject`, and implements `OnInput`. Players who joined before the scene loaded, including the host, are spawned from `Start` or `OnSceneLoadDone`.
    - `PlayerCapsule`: a `NetworkBehaviour` that moves through `NetworkCharacterController`.
    - `NetworkInputData` and `LocalCameraFollow`.
    - **Voice** (Photon Voice for Fusion, a UPM package in `Packages/manifest.json`, assembly `PhotonVoice.Fusion`, which is already in `AssembliesToWeave`):
      - `VoiceRunnerSetup.Ensure(runner)` adds `FusionVoiceClient` to the runner's GameObject. `VoiceNetworkObject.Spawned` looks it up there, so it must exist before players spawn. It's called from `NetworkBootstrap` and from `PlayerCapsule.Spawned`, which runs before the `Voice` child's `Spawned`.
      - `PlayerVoice` sits on the prefab's `Voice` child and handles push-to-talk (V) or open mic, plus the speaking indicator.
      - The Voice App ID is `AppIdVoice` in `PhotonAppSettings` and is separate from the Fusion App ID.
  - `Resources/PrototypeSceneInfo.asset`: a `FusionMenuSceneInfo` that lists `Prototype` in the menu. The menu picks Shared mode only if the scene name contains "Shared"; otherwise it uses Host, Client or AutoHostOrClient.
  - `Editor/PrototypeSceneBuilder.cs` adds menu items under **Tools → Prototype**:
    - **Build Multiplayer Scene** regenerates `Prefabs/Player.prefab` and `Assets/Scenes/Prototype.unity`, overwriting both. Fusion post-processes newly saved prefabs, so you can't get a valid reference in the same call. The prefab reference is therefore set with `delayCall`.
    - **Rewire Player Prefab** sets that reference by hand.
    - **Setup Fusion Menu** writes the scene-info asset and the Build Settings order.
    - **Add Voice To Player Prefab** adds the `Voice` child and the `SpeakingIndicator` to the existing prefab. It doesn't add them twice, and Build Multiplayer Scene also adds them.

- `Assets/Photon/` — vendor SDK (Fusion runtime/editor/codegen, Photon Realtime, PhotonLibs, NanoSockets native plugins). **Treat as read-only**; upgrades come from re-importing the SDK.
  - `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` — Fusion project config (JSON). Edited via the Unity inspector normally; `AssembliesToWeave` lists the assemblies Fusion IL-weaves (`Assembly-CSharp`, `Assembly-CSharp-firstpass`).
  - `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` — holds the Photon App ID. Never print, commit to a public repo, or overwrite it.
  - `Assets/Photon/FusionMenu/`: the imported Fusion Menu, with its own `Fusion.Menu` asmdef. Its connection logic is in `FusionMenuConnectionBehaviourSdk.cs`: it creates the runner, loads the chosen scene additively, and disables the menu camera once connected.
  - `Assets/Photon/FusionDemos/*.unitypackage` (the Intro Shared sample) is not imported.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln` — Unity-generated; never edit by hand.

## Project files

All files created for this project (Photon SDK files excluded):

| File | Purpose |
|---|---|
| `CLAUDE.md` | Guidance for Claude Code (this file) |
| `progress.md` | Running log: done, next up, decisions, open issues, file table |
| `MULTIPLAYER_GUIDE.md` | Step-by-step human guide to building the prototype |
| `Assets/Scenes/FusionSampleMenu.unity` | Fusion Menu scene (from the Menu package, moved here); build index 0 |
| `Assets/Scenes/Prototype.unity` | Capsule gameplay scene: ground, boxes, camera, `NetworkBootstrap`; build index 1 |
| `Assets/_Project/Scripts/NetworkBootstrap.cs` | Attaches to the menu's runner or starts its own; spawns and despawns players; sends input |
| `Assets/_Project/Scripts/NetworkInputData.cs` | `INetworkInput` struct: world-space move direction and Jump button |
| `Assets/_Project/Scripts/PlayerCapsule.cs` | Player `NetworkBehaviour`: predicted move and jump, color per player, sets the camera target |
| `Assets/_Project/Scripts/LocalCameraFollow.cs` | Third-person orbit camera, cursor lock (ignores UI clicks), shares `Yaw` |
| `Assets/_Project/Scripts/VoiceRunnerSetup.cs` | Adds `FusionVoiceClient` to the runner at runtime |
| `Assets/_Project/Scripts/PlayerVoice.cs` | Push-to-talk (V) or open mic, and the speaking indicator |
| `Assets/_Project/Editor/PrototypeSceneBuilder.cs` | **Tools → Prototype** menu: build scene and prefab, rewire prefab, set up menu, add voice |
| `Assets/_Project/Prefabs/Player.prefab` | Capsule with `NetworkObject`, `NetworkCharacterController`, `PlayerCapsule`, a `Voice` child and a `SpeakingIndicator` |
| `Assets/_Project/Resources/PrototypeSceneInfo.asset` | `FusionMenuSceneInfo` that lists `Prototype` in the menu |

Packages added: `com.photonengine.voice-fusion` (Git UPM, needs Git installed). The Fusion Menu `.unitypackage` is imported into `Assets/Photon/FusionMenu/`.

## Fusion architecture notes

- Networked code lives in `NetworkBehaviour` subclasses using `[Networked]` properties, RPCs, and `FixedUpdateNetwork()`. These are rewritten by the **Fusion IL weaver** after compilation, so:
  - New gameplay scripts must compile into a weaved assembly. If you add an `.asmdef` for game code, add its name to `AssembliesToWeave` in `NetworkProjectConfig.fusion` or networked properties will silently not work.
  - `[Networked]` properties must be auto-properties (`{ get; set; }`) — no backing fields or initializers on the field.
- Networked prefabs need a `NetworkObject` component; Fusion auto-registers them in the prefab table on import.
- A `NetworkRunner` (started with a `GameMode` such as `Shared`, `Host`, `Client`, `AutoHostOrClient`) owns the session; spawn via `runner.Spawn(...)`, not `Instantiate`.
- Default `PeerMode` is Single (one runner per process). Multi-peer mode lets one editor run host + clients for testing.

## Commands

There is no CLI build/test script; work happens through the Unity Editor. For batchmode, use the Unity Hub editor binary (default install path shown — adjust if installed elsewhere):

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.0.42f1\Editor\Unity.exe"

# Compile check (also runs the Fusion weaver); inspect the log for errors
& $unity -batchmode -quit -projectPath . -logFile Logs\batch-compile.log

# Run EditMode tests (Unity Test Framework 1.4.6 is installed; no tests exist yet)
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs\editmode-results.xml -logFile Logs\tests.log

# Run a single test / fixture
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.FixtureName.TestName" -testResults Logs\one.xml
```

Batchmode fails if the project is already open in the Editor — close it first, or use the Editor's Test Runner window instead.

The `unity-mcp` server (bridged by the `com.unity.ai.assistant` package) gives control of the open Editor: `Unity_RunCommand` compiles and runs a C# `CommandScript`, and `Unity_GetConsoleLogs` reads the console. When the Editor is open, prefer it over batchmode. Notes:
- After changing scripts, run `AssetDatabase.Refresh()` and wait about 10 seconds for the domain reload. Calls made during the reload fail with "Could not load file or assembly".
- RunCommand scripts can't reference `Fusion.*` types directly. Use `GetComponent("TypeName")` or `SerializedObject` instead.
- `EditorApplication.delayCall` may not fire while the Editor is unfocused.
