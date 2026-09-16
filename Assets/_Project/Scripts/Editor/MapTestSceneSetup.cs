using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// =========================================================
// SETUP DE LA SCENE MAP_TEST (terrain d'essai du robot)
// Menus : Blockforge > Setup Map Test Scene              (arene d'entrainement 180 x 160 m)
//         Blockforge > Setup Map Test Scene (Red Canyon) (raffinerie dans un canyon, 1,4 km, lourde)
// - Soleil, volume global (profil Sky and Fog partage avec
//   Map_MARS), camera de poursuite (RobotFollowCamera)
// - Map : GLB importe par glTFast (Art/Models/Map_Test.glb ou
//   Map_Canyon.glb) instancie a l'identite, MeshCollider sur
//   chaque maillage solide (marquages, joints et liseres
//   decoratifs exclus) ; repli sur un sol plat + obstacles
//   procéduraux si le GLB manque
// - SpawnPoint : noeud "SpawnPoint" du modele (ou premier noeud
//   sans maillage dont le nom commence par "spawn"), sinon un
//   objet a (0, 0.2, 0)
// - TestSceneBootstrap : RobotTestSpawner (blueprint en memoire
//   ou robot par defaut) avec le catalogue des blocs
// - Enregistre toutes les scenes du build (BlockforgeScenes)
// S'execute une fois tout seul apres compilation si Map_Test
// est deja la scene active (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MapTestSceneSetup
{
    private const string ScenePath = BlockforgeScenes.MapTest;
    public const string ArenaModelPath = "Assets/_Project/Art/Models/Map_Test.glb";
    public const string CanyonModelPath = "Assets/_Project/Art/Models/Map_Canyon.glb";
    private const string AssetDir = "Assets/_Project/Art/MapTest";
    private const string TemplateProfilePath = "Assets/Settings/SkyandFogSettingsProfile.asset";
    private const string ProfilePath = AssetDir + "/MapTestVolumeProfile.asset";
    private const string BlockDataDir = "Assets/_Project/Data/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeMapTestScene-v1.done";

    // Maillages sans collision : marquages au sol, joints du sol, pointilles des
    // routes, liseres et bandes de hauteur (README de l'arene : « exclure les
    // marquages et les liserés décoratifs »)
    private static readonly string[] DecorativeNameParts =
    {
        "marking", "deck_joint", "_dash", "lane_", "height_band", "top_marker", "landing_marker",
        "spawn_cross", "spawn_ring", "lettering", "label", "decal", "stripe",
    };

    static MapTestSceneSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SceneManager.GetActiveScene().path != ScenePath)
            return;

        if (Setup(ArenaModelPath, "Map_Test"))
            File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Setup Map Test Scene")]
    public static void SetupArena()
    {
        Setup(ArenaModelPath, "Map_Test");
    }

    [MenuItem("Blockforge/Setup Map Test Scene (Red Canyon)")]
    public static void SetupCanyon()
    {
        Setup(CanyonModelPath, "Map_Canyon");
    }

    public static bool Setup(string modelPath, string mapName)
    {
        Scene scene;
        if (!File.Exists(ScenePath))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[MapTestSceneSetup] Scene creee : {ScenePath}");
        }
        else if (!BlockforgeScenes.OpenForSetup(ScenePath, out scene))
        {
            return false;
        }

        EnsureSun();
        EnsureVolume();
        var camera = EnsureCamera();
        var (spawn, mapSource) = EnsureMap(scene, modelPath, mapName);
        var spawner = EnsureBootstrap(spawn, camera);

        BlockforgeScenes.RegisterAllInBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MapTestSceneSetup] Map_Test prete : map = {mapSource}, spawn = '{spawn.name}' {spawn.position}, " +
                  $"{spawner.blocks.Length} blocs, case {spawner.cellSize} m. Play direct = robot par defaut ; P depuis le garage = robot en cours.");
        return true;
    }

    // =========================================================
    // LUMIERE / VOLUME / CAMERA
    // =========================================================

    private static void EnsureSun()
    {
        var existing = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                             .FirstOrDefault(l => l.type == LightType.Directional);
        if (existing != null)
        {
            if (!existing.TryGetComponent<HDAdditionalLightData>(out _))
                existing.gameObject.AddComponent<HDAdditionalLightData>();
            return;
        }

        var go = new GameObject("Sun");
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        var hd = go.AddComponent<HDAdditionalLightData>();
#pragma warning disable 618 // API de remplacement pas encore stable selon les versions HDRP
        hd.SetIntensity(100000f, LightUnit.Lux);
#pragma warning restore 618
    }

    private static void EnsureVolume()
    {
        var volume = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
                           .FirstOrDefault(v => v.isGlobal);
        if (volume == null)
        {
            var go = new GameObject("Global Volume");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
        }

        if (volume.sharedProfile == null)
        {
            var template = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TemplateProfilePath);
            volume.sharedProfile = template != null ? template : GetOrCreateFallbackProfile();
            EditorUtility.SetDirty(volume);
        }
    }

    // Ciel physique + exposition auto si le profil du template HDRP a disparu
    private static VolumeProfile GetOrCreateFallbackProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            EnsureFolder(AssetDir);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        if (!profile.TryGet(out VisualEnvironment env))
        {
            env = profile.Add<VisualEnvironment>(true);
            AssetDatabase.AddObjectToAsset(env, profile);
        }
        env.skyType.Override((int)SkyType.PhysicallyBased);

        if (!profile.TryGet(out PhysicallyBasedSky sky))
        {
            sky = profile.Add<PhysicallyBasedSky>(true);
            AssetDatabase.AddObjectToAsset(sky, profile);
        }

        if (!profile.TryGet(out Exposure exposure))
        {
            exposure = profile.Add<Exposure>(true);
            AssetDatabase.AddObjectToAsset(exposure, profile);
        }
        exposure.mode.Override(ExposureMode.Automatic);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static RobotFollowCamera EnsureCamera()
    {
        var cam = Camera.main;
        if (cam == null)
            cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).FirstOrDefault();

        GameObject go;
        if (cam == null)
        {
            go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.transform.position = new Vector3(0f, 4f, -10f);
        }
        else
        {
            go = cam.gameObject;
        }

        go.tag = "MainCamera";
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 2000f;
        if (!go.TryGetComponent<HDAdditionalCameraData>(out _))
            go.AddComponent<HDAdditionalCameraData>();
        if (!go.TryGetComponent<AudioListener>(out _))
            go.AddComponent<AudioListener>();
        if (!go.TryGetComponent<RobotFollowCamera>(out var follow))
            follow = go.AddComponent<RobotFollowCamera>();
        EditorUtility.SetDirty(go);
        return follow;
    }

    // =========================================================
    // MAP (GLB ou repli procedural) + SPAWN
    // =========================================================

    private static (Transform spawn, string source) EnsureMap(Scene scene, string modelPath, string mapName)
    {
        // Une seule map a la fois : on retire la precedente (GLB ou repli)
        foreach (var root in scene.GetRootGameObjects())
        {
            switch (root.name)
            {
                case "Map_Test":
                case "Map_Canyon":
                case "TestGround":
                case "TestObstacles":
                case "SpawnPoint":
                    Object.DestroyImmediate(root);
                    break;
            }
        }

        var model = BlockforgeScenes.LoadModelAsset(modelPath);
        if (model == null)
        {
            Debug.LogWarning($"[MapTestSceneSetup] Modele introuvable ou pas encore importe : {modelPath} " +
                             "(GLB importe par le package com.unity.cloud.gltfast). Sol plat procedural en attendant ; " +
                             "relance le menu une fois l'import termine.");
            return (BuildProceduralGround(), "sol procedural");
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        instance.name = mapName;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;
        int colliders = AddColliders(instance);

        var spawn = FindSpawnNode(instance.transform);
        string spawnSource = "noeud du modele";
        if (spawn == null)
        {
            spawn = new GameObject("SpawnPoint").transform;
            spawn.position = new Vector3(0f, 0.2f, 0f);
            spawnSource = "objet cree (aucun noeud spawn dans le modele)";
        }

        Debug.Log($"[MapTestSceneSetup] {Path.GetFileName(modelPath)} instancie : {colliders} MeshCollider, spawn = {spawnSource}.");
        return (spawn, Path.GetFileName(modelPath));
    }

    // MeshCollider (statique, non convexe) sur chaque maillage solide du modele
    private static int AddColliders(GameObject root)
    {
        int count = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || IsDecorative(filter.gameObject.name))
                continue;

            if (!filter.TryGetComponent<Collider>(out _))
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
            count++;
        }
        return count;
    }

    private static bool IsDecorative(string name)
    {
        string lower = name.ToLowerInvariant();
        foreach (var part in DecorativeNameParts)
        {
            if (lower.Contains(part))
                return true;
        }
        return false;
    }

    private static Transform FindSpawnNode(Transform root)
    {
        Transform fallback = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root)
                continue;
            if (string.Equals(t.name, "SpawnPoint", System.StringComparison.OrdinalIgnoreCase))
                return t;
            if (fallback == null
                && t.name.StartsWith("spawn", System.StringComparison.OrdinalIgnoreCase)
                && t.GetComponent<Renderer>() == null)
            {
                fallback = t;
            }
        }
        return fallback;
    }

    // Repli sans modele : sol de 200 m, deux murs, deux caisses, une rampe
    private static Transform BuildProceduralGround()
    {
        var groundMat = GetOrCreateMaterial("MapTest_Ground", new Color(0.32f, 0.34f, 0.37f), 0.25f);
        var obstacleMat = GetOrCreateMaterial("MapTest_Obstacle", new Color(0.95f, 0.55f, 0.15f), 0.4f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // garde son MeshCollider
        ground.name = "TestGround";
        ground.transform.localScale = new Vector3(20f, 1f, 20f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        var obstacles = new GameObject("TestObstacles").transform;
        CreateBox(obstacles, "Wall_N", new Vector3(0f, 1.5f, 30f), new Vector3(40f, 3f, 1f), Quaternion.identity, obstacleMat);
        CreateBox(obstacles, "Wall_E", new Vector3(30f, 1.5f, 0f), new Vector3(1f, 3f, 40f), Quaternion.identity, obstacleMat);
        CreateBox(obstacles, "Box_1", new Vector3(-8f, 1f, 10f), Vector3.one * 2f, Quaternion.identity, obstacleMat);
        CreateBox(obstacles, "Box_2", new Vector3(8f, 1f, 12f), new Vector3(3f, 2f, 3f), Quaternion.identity, obstacleMat);
        CreateBox(obstacles, "Ramp", new Vector3(0f, 1.6f, -20f), new Vector3(8f, 0.5f, 14f), Quaternion.Euler(-15f, 0f, 0f), obstacleMat);

        var spawn = new GameObject("SpawnPoint").transform;
        spawn.position = new Vector3(0f, 0.2f, 0f);
        return spawn;
    }

    private static void CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube); // BoxCollider conserve
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.SetPositionAndRotation(position, rotation);
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
    }

    // Le materiau par defaut de CreatePrimitive n'existe qu'en editeur avec HDRP
    private static Material GetOrCreateMaterial(string name, Color color, float smoothness)
    {
        string path = $"{AssetDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            EnsureFolder(AssetDir);
            mat = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        HDMaterial.ValidateMaterial(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // =========================================================
    // BOOTSTRAP (spawner)
    // =========================================================

    private static RobotTestSpawner EnsureBootstrap(Transform spawn, RobotFollowCamera camera)
    {
        var go = GameObject.Find("TestSceneBootstrap");
        if (go == null)
            go = new GameObject("TestSceneBootstrap");
        if (!go.TryGetComponent<RobotTestSpawner>(out var spawner))
            spawner = go.AddComponent<RobotTestSpawner>(); // cellSize conservee si le composant existait

        spawner.spawnPoint = spawn;
        spawner.followCamera = camera;
        spawner.blocks = AssetDatabase.FindAssets("t:BlockDefinition", new[] { BlockDataDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(block => block != null)
            .OrderBy(block => block.name)
            .ToArray();
        EditorUtility.SetDirty(go);
        return spawner;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
