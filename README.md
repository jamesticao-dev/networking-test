# networking-test

A small **online multiplayer prototype** made with **Unity 6** and **Photon Fusion 2**. Colored capsule players walk, jump and look around together, and can talk with **proximity voice chat**.

It's a learning project: the code is short and commented, and step-by-step guides explain every GameObject, component and script.

| | |
|---|---|
| **Engine** | Unity 6 (`6000.0.42f1`), Universal Render Pipeline |
| **Networking** | Photon Fusion 2 (`2.1.3`), Host/Client mode |
| **Voice** | Photon Voice for Fusion (push-to-talk, 3D audio) |
| **Input** | Unity Input System (keyboard, mouse, gamepad) |

---

## Features

- **Online multiplayer** through Photon Cloud. No server of your own needed.
- **Main menu** with player name, **Quick Play**, and **Party** (create a room or join one with a code).
- **Third-person camera** that orbits with the mouse and moves in when a wall is in the way.
- **Predicted movement**: your own capsule reacts instantly, and the host keeps everyone in sync.
- **Voice chat**: hold **V** to talk. Closer players sound louder, and a green dot shows who's talking.
- **One-click scene builder** under **Tools → Prototype**.

---

## Getting started

### 1. What you need

- **Unity Hub** with Unity **6000.0.42f1**. Other Unity 6 versions may work, but they're untested.
- **Git for Windows** ([git-scm.com](https://git-scm.com)). Unity needs it to download the Photon Voice package. Restart Unity Hub after installing.
- A free **Photon account** ([photonengine.com](https://www.photonengine.com)).

### 2. Download the project

```powershell
git clone https://github.com/jamesticao-dev/networking-test.git
```

Or click **Code → Download ZIP** on GitHub and unzip it.

### 3. Open it in Unity

1. Open **Unity Hub → Add → Add project from disk** and pick the `networking-test` folder.
2. Open it. The first import takes a few minutes while Unity builds the `Library` folder and downloads packages.

### 4. Use your own Photon App IDs

Photon App IDs connect the game to Photon's servers. Use your own so your testing doesn't share someone else's account and limits.

1. In the [Photon dashboard](https://dashboard.photonengine.com), click **Create a new app** twice:
   - one of type **Fusion**,
   - one of type **Voice**.
2. In Unity, open **Tools → Fusion → Fusion Hub → Configuration**:
   - paste the Fusion ID into **App Id Fusion**,
   - paste the Voice ID into **App Id Voice**.
3. **Fixed Region** in the same settings is `asia`. Change it to the region closest to your players. Everyone must use the same region to see each other.

> Without the Voice ID, the game still works, but voice chat doesn't connect.

### 5. Play

1. Open `Assets/Scenes/FusionSampleMenu.unity`.
2. Press **Play**, then **Quick Play**.
3. You're the host. A colored capsule appears.

For a quick test without the menu, open `Assets/Scenes/Prototype.unity` and press **Play**. It joins the session `proto` straight away.

---

## Controls

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | **W A S D** | Left stick |
| Look | Mouse (click the game first) | Right stick |
| Jump | **Space** | South button |
| Talk (push-to-talk) | Hold **V** | — |
| Free the mouse | **Esc** | — |
| In-game panel (party code, players, ping, Disconnect) | Gear icon, top right | — |

---

## Testing with two players

1. **File → Build And Run**. Click **Quick Play** in the build; it becomes the host.
2. Press **Play** in the Unity Editor and click **Quick Play**. It joins as a client.
3. Both windows should show two capsules in different colors, each moving only its own.

To play with a friend, send them your build. You both need the **same App ID, region and build**. Use **Party → Create**, then share the code shown in the gear panel. Your friend joins with **Party → Join**.

Testing voice on one PC? Use headphones to avoid echo.

---

## Guides

| Guide | What it covers |
|---|---|
| [MULTIPLAYER_GUIDE.md](MULTIPLAYER_GUIDE.md) | Build the whole game from scratch, for Unity beginners: every GameObject, component and why, full scripts, the menu and its configuration |
| [VOICE_CHAT_GUIDE.md](VOICE_CHAT_GUIDE.md) | Add voice chat on top: install, scripts, prefab setup, testing |
| [progress.md](progress.md) | Development log: what's done, decisions, open issues |

---

## Project structure

```
Assets/
├── _Project/                    ← all of this project's own code and content
│   ├── Scripts/
│   │   ├── NetworkBootstrap.cs   connects, spawns and removes players, reads input
│   │   ├── NetworkInputData.cs   the input each player sends every tick
│   │   ├── PlayerCapsule.cs      networked movement, jump, player color
│   │   ├── LocalCameraFollow.cs  third-person orbit camera
│   │   ├── VoiceRunnerSetup.cs   adds the voice client to the network runner
│   │   └── PlayerVoice.cs        push-to-talk and the "talking" dot
│   ├── Editor/PrototypeSceneBuilder.cs   Tools → Prototype menu
│   ├── Prefabs/Player.prefab
│   └── Resources/PrototypeSceneInfo.asset   lists the scene in the menu
├── Scenes/
│   ├── FusionSampleMenu.unity   main menu (build index 0)
│   └── Prototype.unity          gameplay (build index 1)
└── Photon/                      Photon SDKs (third-party; avoid editing)
```

**How it works, in one picture:**

```
Menu → creates a NetworkRunner → Photon Cloud
        │
        ├─ first player in = HOST (runs the real game)
        └─ everyone else  = CLIENT
        │
Every tick: clients send input → host moves every capsule → everyone sees the same result
```

---

## Editor tools

| Menu item | What it does |
|---|---|
| **Tools → Prototype → Build Multiplayer Scene** | Regenerates `Player.prefab` and `Prototype.unity`. **Overwrites both.** |
| **Tools → Prototype → Rewire Player Prefab** | Re-links the Player prefab to `NetworkBootstrap` if the field is empty |
| **Tools → Prototype → Setup Fusion Menu** | Registers the gameplay scene with the menu and sets the build order |
| **Tools → Prototype → Add Voice To Player Prefab** | Adds the voice parts to an existing Player prefab |

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Can't connect | Check the App ID in **Fusion Hub → Configuration** and your internet connection. |
| Players can't see each other | Same App ID, same **Fixed Region**, same build. With the menu, use Quick Play on both, or the same party code. In **Settings**, set **App Version** to `1.0` on both. |
| "No 'git' executable was found" | Install Git for Windows, then restart Unity and Unity Hub. |
| No voice | Set **App Id Voice**, hold **V** while talking, and check your microphone in Windows Sound settings. |
| `Player Prefab is not assigned` | Run **Tools → Prototype → Rewire Player Prefab**. |
| Menu Play button greyed out | Run **Tools → Prototype → Setup Fusion Menu**. |

More fixes are in [MULTIPLAYER_GUIDE.md](MULTIPLAYER_GUIDE.md#common-problems).

---

## Known limitations

- **Region selection is turned off.** Everyone plays in one fixed region (`asia`). Party codes only work with the default "Best" region, so leave the menu's region list empty.
- **The Fusion Menu has a small project edit.** `Assets/Photon/FusionMenu/Runtime/FusionMenuConnectionBehaviourSdk.cs` falls back to the Fixed Region (marked `PROJECT CHANGE`). Re-importing the Fusion Menu undoes it.
- **No character model or animations yet.** Players are capsules.

---

## Credits

- [Photon Fusion 2](https://doc.photonengine.com/fusion/current/getting-started/fusion-intro) and [Photon Voice](https://doc.photonengine.com/voice/current/getting-started/voice-intro) by Exit Games. The SDKs in `Assets/Photon/` are covered by Photon's own license terms.
- Built on Unity's URP template.
