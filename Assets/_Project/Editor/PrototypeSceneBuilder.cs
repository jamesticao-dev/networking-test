using System.Linq;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates the capsule player prefab and the Prototype scene. Safe to re-run;
/// it overwrites the assets it owns.
/// </summary>
public static class PrototypeSceneBuilder
{
    private const string Root = "Assets/_Project";
    private const string PrefabPath = Root + "/Prefabs/Player.prefab";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";

    [MenuItem("Tools/Prototype/Build Multiplayer Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(Root, "Prefabs");
        EnsureFolder("Assets", "Scenes");

        BuildPlayerPrefab();
        BuildScene();
        AddSceneToBuildSettings();

        // Fusion post-processes the saved prefab, so it can't be loaded until the next editor update.
        EditorApplication.delayCall += WirePlayerPrefab;

        Debug.Log($"[PrototypeSceneBuilder] Built {PrefabPath} and {ScenePath}");
    }

    /// <summary>Assigns Player.prefab to the scene's NetworkBootstrap and saves the scene.</summary>
    [MenuItem("Tools/Prototype/Rewire Player Prefab")]
    public static void WirePlayerPrefab()
    {
        var prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var prefab = prefabGo != null ? prefabGo.GetComponent<NetworkObject>() : null;
        var bootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();

        if (prefab == null || bootstrap == null)
        {
            Debug.LogError($"[PrototypeSceneBuilder] Could not wire player prefab (prefab={prefab}, bootstrap={bootstrap}). Open {ScenePath} and run Tools/Prototype/Rewire Player Prefab.");
            return;
        }

        var so = new SerializedObject(bootstrap);
        so.FindProperty("playerPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
        Debug.Log("[PrototypeSceneBuilder] Wired Player.prefab into NetworkBootstrap");
    }

    private static void BuildPlayerPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Player";
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>()); // CharacterController handles collision

        go.AddComponent<NetworkObject>();
        var ncc = go.AddComponent<NetworkCharacterController>(); // adds CharacterController
        ncc.maxSpeed = 6f;
        ncc.acceleration = 40f;
        ncc.braking = 40f;
        ncc.jumpImpulse = 7f;
        go.AddComponent<PlayerCapsule>();

        var cc = go.GetComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;

        AddVoiceTo(go);

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        NetworkProjectConfigUtilities.RebuildPrefabTable();
    }

    // ---------------------------------------------------------------- Voice

    /// <summary>Adds voice chat to the existing Player.prefab without touching anything else.</summary>
    [MenuItem("Tools/Prototype/Add Voice To Player Prefab")]
    public static void AddVoiceToPlayerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (AddVoiceTo(root))
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[PrototypeSceneBuilder] Voice added to Player.prefab");
            }
            else
            {
                Debug.Log("[PrototypeSceneBuilder] Player.prefab already has voice; nothing changed.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        NetworkProjectConfigUtilities.RebuildPrefabTable();
    }

    /// <summary>
    /// Adds a "Voice" child (VoiceNetworkObject + Recorder + Speaker with 3D AudioSource + PlayerVoice)
    /// and a "SpeakingIndicator" sphere above the head. Returns false if voice is already there.
    /// </summary>
    private static bool AddVoiceTo(GameObject root)
    {
        if (root.GetComponentInChildren<Photon.Voice.Fusion.VoiceNetworkObject>(true) != null)
            return false;

        var voice = new GameObject("Voice");
        voice.transform.SetParent(root.transform, false);
        voice.transform.localPosition = new Vector3(0f, 0.6f, 0f); // roughly head height

        voice.AddComponent<Photon.Voice.Fusion.VoiceNetworkObject>();
        voice.AddComponent<Photon.Voice.Unity.Recorder>();
        voice.AddComponent<Photon.Voice.Unity.Speaker>(); // adds AudioSource

        // Proximity voice: full 3D, audible up to ~25 m, no doppler pitch shifting.
        var source = voice.GetComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 25f;
        source.dopplerLevel = 0f;
        source.playOnAwake = false;

        var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicator.name = "SpeakingIndicator";
        Object.DestroyImmediate(indicator.GetComponent<SphereCollider>());
        indicator.transform.SetParent(root.transform, false);
        indicator.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        indicator.transform.localScale = Vector3.one * 0.3f;
        indicator.SetActive(false);

        var playerVoice = voice.AddComponent<PlayerVoice>();
        var so = new SerializedObject(playerVoice);
        so.FindProperty("speakingIndicator").objectReferenceValue = indicator;
        so.ApplyModifiedPropertiesWithoutUndo();

        return true;
    }

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var cam = Camera.main;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 12f, -14f), Quaternion.Euler(40f, 0f, 0f));
        cam.gameObject.AddComponent<LocalCameraFollow>();

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(4f, 1f, 4f); // 40x40

        var obstacles = new GameObject("Obstacles");
        var positions = new[]
        {
            new Vector3(6f, 0.5f, 6f), new Vector3(-7f, 0.5f, 3f),
            new Vector3(3f, 0.5f, -8f), new Vector3(-5f, 1f, -6f),
        };
        for (int i = 0; i < positions.Length; i++)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = $"Box_{i}";
            box.transform.SetParent(obstacles.transform);
            box.transform.position = positions[i];
            box.transform.localScale = new Vector3(2f, positions[i].y * 2f, 2f);
        }

        new GameObject("NetworkBootstrap").AddComponent<NetworkBootstrap>();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void AddSceneToBuildSettings()
    {
        // Order: menu (if present) → Prototype → everything else.
        var first = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null
            ? new[] { MenuScenePath, ScenePath }
            : new[] { ScenePath };

        // Also drop entries whose scene file no longer exists (e.g. after moving scenes).
        var others = EditorBuildSettings.scenes.Where(s =>
            !first.Contains(s.path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) != null);
        EditorBuildSettings.scenes = first.Select(p => new EditorBuildSettingsScene(p, true))
            .Concat(others)
            .ToArray();
    }

    // ---------------------------------------------------------------- Fusion Menu

    private const string MenuScenePath = "Assets/Scenes/FusionSampleMenu.unity";
    private const string SceneInfoPath = Root + "/Resources/PrototypeSceneInfo.asset";

    /// <summary>
    /// Registers Prototype.unity as a playable scene in the Fusion Menu (via a
    /// FusionMenuSceneInfo asset in Resources) and puts the menu first in Build Settings.
    /// The scene name must NOT contain "Shared", or the menu would start Shared mode
    /// instead of Host/Client.
    /// </summary>
    [MenuItem("Tools/Prototype/Setup Fusion Menu")]
    public static void SetupMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null)
        {
            Debug.LogError($"[PrototypeSceneBuilder] Menu scene not found at {MenuScenePath}. Install it from Fusion Hub → Welcome → Install Fusion Menu.");
            return;
        }

        EnsureFolder(Root, "Resources");

        var info = AssetDatabase.LoadAssetAtPath<FusionMenuSceneInfo>(SceneInfoPath);
        if (info == null)
        {
            info = ScriptableObject.CreateInstance<FusionMenuSceneInfo>();
            AssetDatabase.CreateAsset(info, SceneInfoPath);
        }
        info.Name = "Capsule Prototype";
        info.ScenePath = ScenePath;
        info.IsHidden = false;
        EditorUtility.SetDirty(info);
        AssetDatabase.SaveAssets();

        AddSceneToBuildSettings();
        Debug.Log($"[PrototypeSceneBuilder] Fusion Menu set up: {SceneInfoPath} → {ScenePath}; menu is build scene 0.");
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            AssetDatabase.CreateFolder(parent, name);
    }
}
