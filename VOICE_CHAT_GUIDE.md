# Add Voice Chat (Photon Voice for Fusion)

This is an add-on to **[MULTIPLAYER_GUIDE.md](MULTIPLAYER_GUIDE.md)**. Finish that guide first: you need working capsule players before adding voice.

When you're done:

- **Hold V** to talk (push-to-talk), or switch to an always-on mic.
- Voice is **3D**: players close to you are loud, far away ones are quiet, and past 25 m you can't hear them.
- A **green dot** floats over a capsule while that player is talking.

---

## Part 1: How voice fits in

Voice runs **next to** Fusion, on its own Photon Voice connection. Fusion tells it which session you're in, so you only hear people in your game.

```
 NetworkRunner (Fusion)
   └─ FusionVoiceClient          ← added by VoiceRunnerSetup. Connects to Photon Voice
                                   and follows the Fusion session.

 Player prefab
   ├─ PlayerCapsule              ← calls VoiceRunnerSetup.Ensure (safety net)
   ├─ SpeakingIndicator          ← green sphere, shown while talking
   └─ Voice                      (child GameObject)
        ├─ Voice Network Object  ← links this player's mic/speaker to the network
        ├─ Recorder              ← records YOUR mic (only on your own capsule)
        ├─ Speaker + AudioSource ← plays OTHER players' voices, in 3D
        └─ Player Voice          ← your script: push-to-talk + green dot
```

| Script | Goes on | Job |
|---|---|---|
| `VoiceRunnerSetup.cs` | Nothing (static helper) | Adds `FusionVoiceClient` to the runner |
| `PlayerVoice.cs` | `Player` prefab → `Voice` child | Push-to-talk or open mic, shows the green dot |

> ⚠️ **The one rule that matters:** `FusionVoiceClient` must be on the runner **before any player spawns**.
> `Voice Network Object` looks for it the moment a capsule appears. If it isn't there yet, that player has no voice.
> That's why we call `VoiceRunnerSetup.Ensure` in two places (Part 4).

---

## Part 2: Install Photon Voice and get a Voice App ID

1. Install **Git for Windows** (git-scm.com) if you don't have it. Unity downloads this package with Git. **Restart Unity and Unity Hub** afterwards.
2. In Unity, open **Tools → Fusion → Fusion Hub → Addons → Voice for Fusion** and install it.
   - It adds the package `com.photonengine.voice-fusion`.
3. In the **Photon dashboard**, click **Create a new app** and choose type **Voice** (not Fusion). Copy its **App ID**.
4. Paste it in **Fusion Hub → Configuration → App Id Voice**.
   - **Why a second App ID:** Photon bills and runs voice separately from game networking. Without it, voice can't connect, but the game still works.

---

## Part 3: Write the voice scripts

Create both in `Assets/_Project/Scripts/`.

### Script 1: `VoiceRunnerSetup.cs`, puts the voice client on the runner

**What it does:** Finds the `NetworkRunner`'s GameObject and adds a `FusionVoiceClient` component, unless it's already there.

**Why from code, not by hand:** The NetworkRunner doesn't exist in your scene while editing. The menu (or `NetworkBootstrap`) creates it when the game starts, so there's nothing to drag the component onto.

```csharp
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
```

- `UseFusionAppSettings = true` makes it read **App Id Voice** from `PhotonAppSettings` (Part 2) and use the same region as Fusion.
- `static class` means you never attach it to anything. You just call `VoiceRunnerSetup.Ensure(runner)`.

---

### Script 2: `PlayerVoice.cs`, push-to-talk and the talking dot

**Goes on:** the `Voice` child of the Player prefab (Part 5).

**What it does:**
- On **your own** capsule: sends your mic only while **V** is held (or always, in Open Mic mode).
- On **every** capsule: shows the green dot while that player is talking.

**Why a `NetworkBehaviour`:** It needs `Spawned()` (to set up the Recorder once the capsule is on the network) and `Object.IsValid`.

**How it connects:** It reads the `Voice Network Object` next to it (`IsLocal`, `IsRecording`, `IsSpeaking`, `RecorderInUse`) and turns the Recorder's **Transmit** on and off.

```csharp
using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Per-player voice behaviour, on the player prefab's "Voice" child next to
/// VoiceNetworkObject + Recorder + Speaker.
///
/// - Local player: push-to-talk (hold V by default) or open mic.
/// - Everyone: shows a small indicator above the capsule while that player is talking.
/// Voice is 3D: the Speaker's AudioSource is spatial, so closer players are louder.
/// </summary>
[RequireComponent(typeof(VoiceNetworkObject))]
public class PlayerVoice : NetworkBehaviour
{
    public enum TalkMode { PushToTalk, OpenMic }

    [SerializeField] private TalkMode mode = TalkMode.PushToTalk;
    [SerializeField] private Key pushToTalkKey = Key.V;
    [Tooltip("Shown while this player is talking.")]
    [SerializeField] private GameObject speakingIndicator;

    private VoiceNetworkObject _voice;

    /// <summary>Local player's current talk mode; flip at runtime from UI if needed.</summary>
    public TalkMode Mode { get => mode; set => mode = value; }

    private void Awake()
    {
        _voice = GetComponent<VoiceNetworkObject>();
        if (speakingIndicator != null)
        {
            speakingIndicator.SetActive(false);
            if (speakingIndicator.TryGetComponent<Renderer>(out var r))
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.3f, 1f, 0.4f)); // bright green "talking" dot
                r.SetPropertyBlock(block);
            }
        }
    }

    public override void Spawned()
    {
        // Open mic uses voice detection so silence isn't transmitted; push-to-talk sends while held.
        var recorder = GetComponent<Recorder>();
        if (recorder != null && _voice.IsLocal)
        {
            recorder.VoiceDetection = mode == TalkMode.OpenMic;
            recorder.TransmitEnabled = mode == TalkMode.OpenMic;
        }
    }

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;

        if (_voice.IsLocal && _voice.RecorderInUse != null)
        {
            bool transmit = mode == TalkMode.OpenMic
                || (Keyboard.current != null && Keyboard.current[pushToTalkKey].isPressed);

            if (_voice.RecorderInUse.TransmitEnabled != transmit)
                _voice.RecorderInUse.TransmitEnabled = transmit;
        }

        if (speakingIndicator != null)
        {
            // Local: we're sending audio. Remote: their audio is playing on our Speaker.
            bool talking = _voice.IsLocal ? _voice.IsRecording : _voice.IsSpeaking;
            if (speakingIndicator.activeSelf != talking)
                speakingIndicator.SetActive(talking);
        }
    }
}
```

- It uses `Update()`, not `FixedUpdateNetwork()`, because the mic is local hardware, not synced gameplay. Voice audio travels on its own connection.
- **Voice Detection** (open mic only) skips silence, so you don't stream background noise.

---

## Part 4: Connect voice to your existing scripts

Add **one line** in three places. These make sure the voice client is on the runner before any capsule spawns.

### 4a. `NetworkBootstrap.cs`, in `AttachToRunner` (menu flow)

```csharp
_runner.AddCallbacks(this);
VoiceRunnerSetup.Ensure(_runner); // must exist before players spawn   ← ADD
Debug.Log($"[NetworkBootstrap] Attached to menu runner ...");
```

### 4b. `NetworkBootstrap.cs`, in `StartStandalone` (pressing Play in Prototype)

```csharp
_runner.AddCallbacks(this);
VoiceRunnerSetup.Ensure(_runner); // added before StartGame   ← ADD

var sceneInfo = new NetworkSceneInfo();
```

**Why before `StartGame`:** Fusion collects callback components on the runner's GameObject when the game starts. Adding it first means Fusion hooks it up by itself.

### 4c. `PlayerCapsule.cs`, first line of `Spawned()` (safety net)

```csharp
public override void Spawned()
{
    VoiceRunnerSetup.Ensure(Runner);   // ← ADD
    // ... color code continues ...
```

**Why also here:** On a client, a capsule can arrive from the network **before** `NetworkBootstrap.Start` runs. `PlayerCapsule` is on the prefab's root, so its `Spawned()` runs before the `Voice` child's `Spawned()`. That's just in time.

Save and wait for Unity to compile. The Console should have no red errors.

---

## Part 5: Add voice to the Player prefab

Double-click `Assets/_Project/Prefabs/Player.prefab` to open it in **Prefab Mode** (the scene view shows only the prefab, with a blue header). Changes here apply to every player.

### 5.1 The `Voice` child

1. In the Hierarchy, right-click `Player` → **Create Empty**. Rename it `Voice`.
2. Set its **Position** to `0, 0.6, 0` (about head height).
   - **Why a child and not on `Player` itself:** it keeps voice parts in one place that you can remove or replace without touching movement. The position matters too, because 3D sound comes **from** this point.
3. Add these components to `Voice`:

| # | Component (search for) | Why you need it |
|---|---|---|
| 1 | **Voice Network Object** | Connects this capsule's voice to the Fusion player who owns it, so the right voice comes out of the right capsule. It's also a NetworkBehaviour, so it uses the Player's **Network Object**. |
| 2 | **Recorder** | Captures the microphone. It only records on **your own** capsule; on everyone else's it does nothing. Uses your default Windows mic. |
| 3 | **Speaker** | Receives another player's voice stream and plays it. It also adds an **Audio Source** automatically. |
| 4 | **Player Voice** (your script) | Push-to-talk and the green dot. |

### 5.2 Make the voice 3D (Audio Source settings)

Select `Voice`, find the **Audio Source** (added by Speaker):

| Field | Value | Why |
|---|---|---|
| Play On Awake | **off** | The Speaker starts playback when someone talks |
| Spatial Blend | `1` (slide fully to **3D**) | Sound comes from the capsule's position instead of straight into both ears |
| 3D Sound Settings → Doppler Level | `0` | Stops the voice from changing pitch when players move |
| 3D Sound Settings → Volume Rolloff | **Linear Rolloff** | Volume drops evenly with distance, easy to predict |
| 3D Sound Settings → Min Distance | `2` | Full volume within 2 m |
| 3D Sound Settings → Max Distance | `25` | Silent beyond 25 m |

### 5.3 The `SpeakingIndicator` (green dot)

1. Right-click `Player` (not `Voice`) → **3D Object → Sphere**. Rename it `SpeakingIndicator`.
2. **Position** `0, 1.45, 0` (just above the head), **Scale** `0.3, 0.3, 0.3`.
3. **Remove** its **Sphere Collider** (⋮ → Remove Component).
   - **Why:** it's only a visual. A collider would bump into the Character Controller and block the camera.
4. Untick the checkbox next to its name at the top of the Inspector so it starts **hidden**.
   - `PlayerVoice` turns it on while the player talks, and colors it green.

### 5.4 Connect it

Select `Voice`. On **Player Voice**:

| Field | Value |
|---|---|
| Mode | **Push To Talk** (or **Open Mic** for always on) |
| Push To Talk Key | `V` |
| Speaking Indicator | drag `SpeakingIndicator` here |

Click **Save** at the top of the Scene view (or Ctrl+S), then click the **<** arrow in the Hierarchy to leave Prefab Mode.

**Finished prefab:**

```
Player
├─ Network Object, Character Controller, Network Character Controller, Player Capsule
├─ SpeakingIndicator    (Sphere, no collider, hidden, scale 0.3, y 1.45)
└─ Voice                (y 0.6)
   ├─ Voice Network Object
   ├─ Recorder
   ├─ Speaker
   ├─ Audio Source      (3D, linear 2 m → 25 m, doppler 0)
   └─ Player Voice      (Speaking Indicator = SpeakingIndicator)
```

> **Shortcut:** **Tools → Prototype → Add Voice To Player Prefab** does all of Part 5 for you. It won't add voice twice.
> It doesn't do Part 4; those lines are already in this project's scripts.

---

## Part 6: Test it

1. Make a new build (**File → Build And Run**). Old builds don't have voice.
2. Press **Play** in the Editor so both are in the same game.
3. **Use headphones**, or you'll get echo with both on one PC.
4. Check:
   - ✅ The Console shows `[Voice] FusionVoiceClient added to NetworkRunner`.
   - ✅ Holding **V** in one window lets the other window hear you.
   - ✅ The green dot shows over the talking capsule in both windows.
   - ✅ Walking away makes the voice quieter, and past 25 m it's silent.

---

## Common problems

| Problem | Cause / fix |
|---|---|
| "No 'git' executable was found" when installing | Install Git for Windows, then restart Unity **and** Unity Hub. |
| `The type or namespace 'Photon.Voice' could not be found` | The Voice package isn't installed yet (Part 2). |
| Console mentions AppId / authentication; no voice | Set **App Id Voice** to a **Voice**-type app in **Fusion Hub → Configuration**. |
| Voice connects but nobody hears you | Hold **V** while talking. Check your mic in Windows **Sound settings**; the Recorder uses the default input device. |
| One player has no voice at all | The voice client was added after that capsule spawned. Check the three `VoiceRunnerSetup.Ensure` lines (Part 4). |
| You hear everyone at the same volume | Audio Source **Spatial Blend** isn't `1` (Part 5.2). |
| Green dot never appears | **Speaking Indicator** isn't assigned on Player Voice (Part 5.4). |
| Echo or feedback | Use headphones, or test on two computers. |

---

## Removing voice

To turn voice off again: delete the `Voice` and `SpeakingIndicator` children from the prefab, remove the three `VoiceRunnerSetup.Ensure` lines, then delete `VoiceRunnerSetup.cs` and `PlayerVoice.cs`. Remove the package last; otherwise the scripts won't compile.
