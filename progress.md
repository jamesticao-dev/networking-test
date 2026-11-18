# Progress

Running log of what's been done, what's in flight, and what's next for NetworkingTest.
**Rule:** every new, moved or deleted file, and every new package, gets a dated **Done** entry and a row in **Project files**. See `CLAUDE.md` → "Keeping docs current".

Step-by-step guide to how the prototype is built: [MULTIPLAYER_GUIDE.md](MULTIPLAYER_GUIDE.md).

## Current state (2026-11-18)

- Unity 6000.0.42f1 (URP) project; Photon Fusion 2.1.3 SDK; Fusion App ID configured.
- Capsule multiplayer prototype: code in `Assets/_Project/`, scenes in `Assets/Scenes/`.
- Build order: `FusionSampleMenu` (0) → `Prototype` (1). The user removed `SampleScene` and `TutorialInfo`.
- Packages added: Fusion Menu (imported `.unitypackage`) and Photon Voice for Fusion 2.63 (Git UPM).
- **Voice App ID not set yet**, so voice can't connect.
- Project is not under version control.

## Project files

| File | Added | Purpose |
|---|---|---|
| `CLAUDE.md` | 2026-11-18 | Guidance for Claude Code: layout, architecture, commands, doc rules |
| `progress.md` | 2026-11-18 | This log |
| `MULTIPLAYER_GUIDE.md` | 2026-11-18 | Human step-by-step guide (setup, scripts, menu, voice, testing, troubleshooting) |
| `Assets/Scenes/Prototype.unity` | 2026-11-18 | Gameplay scene: ground, boxes, light, camera, `NetworkBootstrap`. The user moved it here from `_Project/Scenes` |
| `Assets/Scenes/FusionSampleMenu.unity` | 2026-11-18 | Fusion Menu scene (from the Menu package via Fusion Hub) |
| `Assets/_Project/Scripts/NetworkInputData.cs` | 2026-11-18 | Network input struct: move direction and Jump |
| `Assets/_Project/Scripts/NetworkBootstrap.cs` | 2026-11-18 | Runner attach/start, player spawn/despawn, input collection |
| `Assets/_Project/Scripts/PlayerCapsule.cs` | 2026-11-18 | Player movement and jump, color, camera target, voice-client safety net |
| `Assets/_Project/Scripts/LocalCameraFollow.cs` | 2026-11-18 | Orbit camera with mouse look and cursor lock that ignores UI clicks |
| `Assets/_Project/Scripts/VoiceRunnerSetup.cs` | 2026-11-18 | Adds `FusionVoiceClient` to the runner |
| `Assets/_Project/Scripts/PlayerVoice.cs` | 2026-11-18 | Push-to-talk (V) or open mic, speaking indicator |
| `Assets/_Project/Editor/PrototypeSceneBuilder.cs` | 2026-11-18 | **Tools → Prototype** menu items (build, rewire, setup menu, add voice) |
| `Assets/_Project/Prefabs/Player.prefab` | 2026-11-18 | Networked capsule with a `Voice` child and a `SpeakingIndicator` |
| `Assets/_Project/Resources/PrototypeSceneInfo.asset` | 2026-11-18 | Lists "Capsule Prototype" in the Fusion Menu |

## Done

- [x] 2026-11-18: Project initialized; `CLAUDE.md` and `progress.md` created.
- [x] 2026-11-18: Topology chosen: Host/Client (`AutoHostOrClient`, session `proto`).
- [x] 2026-11-18: Core networking added: `NetworkInputData`, `NetworkBootstrap`, `PlayerCapsule`, `Player.prefab`, `Prototype.unity`, plus `PrototypeSceneBuilder` to generate them. Single-instance check: started as Host and connected.
- [x] 2026-11-18: Orbit camera with mouse look (`LocalCameraFollow`); movement made camera-relative.
- [x] 2026-11-18: `MULTIPLAYER_GUIDE.md` written.
- [x] 2026-11-18: Fusion Menu integrated. Package imported, `PrototypeSceneInfo.asset` added, and `NetworkBootstrap` attaches to the menu's runner (falls back to its own when Play is pressed directly in `Prototype`). Build Settings: menu → Prototype.
- [x] 2026-11-18: The camera no longer locks the cursor when clicking UI, such as the Fusion Menu gear and its panel.
- [x] 2026-11-18: Voice chat added. Photon Voice for Fusion is installed; `VoiceRunnerSetup` and `PlayerVoice` added. `Player.prefab` gets a `Voice` child (`VoiceNetworkObject`, `Recorder`, `Speaker` with 3D audio, `PlayerVoice`) and a `SpeakingIndicator`. Talking is push-to-talk with V. Compiles cleanly; not yet tested in Play mode.
- [x] 2026-11-18: Doc rule added. `CLAUDE.md` and `progress.md` both carry a full project-files list and must be updated with every change.

## Next up

- [ ] **Set the Voice App ID**: create a Voice app in the Photon dashboard, then Fusion Hub → Configuration → App Id Voice.
- [ ] Test the menu flow: Play from `FusionSampleMenu` → Quick Play → capsule spawns and moves; Party create/join with a code; Disconnect returns to the menu.
- [ ] Two-instance test (Windows build + Editor): both capsules visible, each moves only its own, a capsule disappears when its client leaves, and voice works (hold V, green dot, quieter with distance).
- [ ] Put the project under git with a Unity `.gitignore` (exclude `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `*.csproj`/`*.sln`). Git for Windows is needed anyway for the Voice package.

## Decisions / notes

- Host/Client over Shared: the host has authority and clients predict their own movement, which suits action-style prototypes.
- Camera: third-person orbit (mouse or right stick). Clicking the game world locks the cursor, Esc releases it, and clicks on UI don't lock it. Pitch is clamped to -30..70°, and the camera pulls in when obstacles block it.
- Movement is camera-relative. The client rotates input by the camera yaw before sending, so the host receives a world-space direction.
- Menu modes: Quick Play uses AutoHostOrClient, Create party uses Host, and Join code uses Client. A *scene* name containing "Shared" would switch the menu to Shared mode.
- In-game menu gear: it toggles the Fusion Menu's in-game panel (party code, players, ping, Disconnect). It's not a Settings screen and doesn't pause.
- Voice: the voice client must be on the runner's GameObject before players spawn. The runner is created at runtime, so `VoiceRunnerSetup.Ensure` is called from `NetworkBootstrap` and from `PlayerCapsule.Spawned`. If the runner has already started, its callbacks are registered with `AddCallbacks`.
- Voice is proximity-based: 3D `AudioSource` with linear rolloff from 2 to 25 m.
- **Build Multiplayer Scene** overwrites `Prototype.unity` and `Player.prefab`, so hand edits to them are lost. Use **Add Voice To Player Prefab** and **Setup Fusion Menu** for changes that shouldn't regenerate those files.

## Open issues

- Not yet verified in Play mode: input on the menu-created runner (`ProvideInput` is switched on after `StartGame`), the host's own capsule spawning after the scene loads, and voice joining when `FusionVoiceClient` is added to an already-running runner.
- The builder sets the prefab reference on the next Editor update (`delayCall`). If `NetworkBootstrap.playerPrefab` is empty after a rebuild, run **Tools → Prototype → Rewire Player Prefab**.
