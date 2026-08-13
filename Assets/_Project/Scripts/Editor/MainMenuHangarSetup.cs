using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// =========================================================
// SETUP DE LA SCENE MAINMENU (hangar)
// Menu : Blockforge > Setup MainMenu Hangar
// - Cree les materiaux HDRP (dont neons emissifs)
// - Remappe les materiaux du FBX exporte depuis Blender
// - Place le hangar, la camera, les lumieres et le volume
//   (bloom + exposition) dans la scene MainMenu.
// =========================================================

public static class MainMenuHangarSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string FbxPath = "Assets/_Project/Art/Hangar/Hangar_MainMenu.fbx";
    private const string MatDir = "Assets/_Project/Art/Hangar/Materials";
    private const string VolumeProfilePath = "Assets/_Project/Art/Hangar/MainMenuVolumeProfile.asset";

    // Couleurs lineaires reprises du fichier Blender
    private struct MatDef
    {
        public Color baseColor;
        public float metallic;
        public float smoothness;
        public Color emissive;   // couleur * intensite (nits), noire si non emissif
    }

    private static readonly Dictionary<string, MatDef> Definitions = new()
    {
        { "Hangar_Floor",      new MatDef { baseColor = new Color(0.020f, 0.045f, 0.085f), metallic = 0.3f, smoothness = 0.88f, emissive = Color.black } },
        { "Hangar_Dark",       new MatDef { baseColor = new Color(0.035f, 0.060f, 0.100f), metallic = 0.6f, smoothness = 0.55f, emissive = Color.black } },
        { "Hangar_Panel",      new MatDef { baseColor = new Color(0.060f, 0.110f, 0.180f), metallic = 0.4f, smoothness = 0.65f, emissive = Color.black } },
        { "Hangar_Platform",   new MatDef { baseColor = new Color(0.045f, 0.080f, 0.140f), metallic = 0.5f, smoothness = 0.78f, emissive = Color.black } },
        { "Glow_Blue_Strong",  new MatDef { baseColor = Color.black, metallic = 0f, smoothness = 0.5f, emissive = new Color(0.08f, 0.45f, 1f) * 120f } },
        { "Glow_Blue_Soft",    new MatDef { baseColor = Color.black, metallic = 0f, smoothness = 0.5f, emissive = new Color(0.50f, 0.72f, 1f) * 80f } },
        { "Glow_Floor_Lines",  new MatDef { baseColor = Color.black, metallic = 0f, smoothness = 0.5f, emissive = new Color(0.06f, 0.38f, 0.95f) * 60f } },
    };

    [MenuItem("Blockforge/Setup MainMenu Hangar")]
    public static void Setup()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) == null)
        {
            Debug.LogError($"[MainMenuHangarSetup] FBX introuvable : {FbxPath}");
            return;
        }

        CreateMaterials();
        RemapFbxMaterials();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Nettoie une installation precedente
        foreach (var name in new[] { "Hangar_MainMenu", "Main Camera", "Camera", "Directional Light", "Light_Key", "Light_Fill_L", "Light_Fill_R", "Light_Gate", "Global Volume" })
        {
            var old = GameObject.Find(name);
            if (old != null)
                Object.DestroyImmediate(old);
        }

        // ---------- Hangar ----------
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var hangar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        hangar.name = "Hangar_MainMenu";
        hangar.transform.position = Vector3.zero;

        // ---------- Camera (l'ouverture du hangar est cote +Z apres l'export FBX) ----------
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<HDAdditionalCameraData>();
        camGo.transform.position = new Vector3(0f, 4f, 23.43f);
        camGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        cam.fieldOfView = 43f;

        // ---------- Lumieres ----------
        CreateLight("Light_Key", LightType.Rectangle, new Vector3(0f, 9f, 0f), Quaternion.Euler(90f, 0f, 0f),
                    new Color(0.75f, 0.85f, 1f), 60000f, new Vector2(8f, 8f), shadows: true);
        CreateLight("Light_Fill_L", LightType.Point, new Vector3(-9f, 5.5f, 11f), Quaternion.identity,
                    new Color(0.35f, 0.55f, 1f), 20000f, null, shadows: false);
        CreateLight("Light_Fill_R", LightType.Point, new Vector3(9f, 5.5f, 11f), Quaternion.identity,
                    new Color(0.35f, 0.55f, 1f), 20000f, null, shadows: false);
        CreateLight("Light_Gate", LightType.Rectangle, new Vector3(0f, 3.5f, -13.2f), Quaternion.identity,
                    new Color(0.5f, 0.7f, 1f), 15000f, new Vector2(6f, 4f), shadows: false);

        // ---------- Volume global : bloom + exposition fixe ----------
        var profile = GetOrCreateVolumeProfile();
        var volumeGo = new GameObject("Global Volume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[MainMenuHangarSetup] Scene MainMenu configuree avec le hangar.");
        Selection.activeGameObject = hangar;
    }

    // =========================================================
    // MATERIAUX
    // =========================================================

    private static void CreateMaterials()
    {
        EnsureFolder(MatDir);
        var shader = Shader.Find("HDRP/Lit");

        foreach (var (name, def) in Definitions)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", def.baseColor);
            mat.SetFloat("_Metallic", def.metallic);
            mat.SetFloat("_Smoothness", def.smoothness);
            mat.SetColor("_EmissiveColor", def.emissive);
            mat.SetFloat("_EmissiveExposureWeight", 0f);
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
    }

    private static void RemapFbxMaterials()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                                         ModelImporterMaterialSearch.Everywhere);
        importer.SaveAndReimport();
    }

    // =========================================================
    // LUMIERES / VOLUME
    // =========================================================

    private static void CreateLight(string name, LightType type, Vector3 pos, Quaternion rot,
                                    Color color, float lumens, Vector2? areaSize, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(pos, rot);

        var light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        if (areaSize.HasValue)
            light.areaSize = areaSize.Value;

        if (!go.TryGetComponent<HDAdditionalLightData>(out var hd))
            hd = go.AddComponent<HDAdditionalLightData>();
#pragma warning disable 618 // API de remplacement pas encore stable selon les versions HDRP
        hd.SetIntensity(lumens, LightUnit.Lumen);
#pragma warning restore 618
    }

    private static VolumeProfile GetOrCreateVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        // Ajoute/actualise chaque override (idempotent si le profil existe deja)
        if (!profile.TryGet(out Bloom bloom))
        {
            bloom = profile.Add<Bloom>(true);
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }
        bloom.intensity.Override(0.15f);
        bloom.scatter.Override(0.45f);

        if (!profile.TryGet(out Tonemapping tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>(true);
            AssetDatabase.AddObjectToAsset(tonemapping, profile);
        }
        tonemapping.mode.Override(TonemappingMode.ACES);

        if (!profile.TryGet(out Exposure exposure))
        {
            exposure = profile.Add<Exposure>(true);
            AssetDatabase.AddObjectToAsset(exposure, profile);
        }
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(9.5f);

        // Gradient Sky bleu nuit tres sombre : equivalent du "World" Blender.
        // Fournit une ambiance douce pour que l'interieur reste lisible,
        // sans la lumiere du jour du ciel physique par defaut.
        if (!profile.TryGet(out GradientSky sky))
        {
            sky = profile.Add<GradientSky>(true);
            AssetDatabase.AddObjectToAsset(sky, profile);
        }
        sky.top.Override(new Color(0.010f, 0.022f, 0.050f));
        sky.middle.Override(new Color(0.006f, 0.013f, 0.030f));
        sky.bottom.Override(new Color(0.003f, 0.007f, 0.016f));
        sky.multiplier.Override(1f);

        if (!profile.TryGet(out VisualEnvironment env))
        {
            env = profile.Add<VisualEnvironment>(true);
            AssetDatabase.AddObjectToAsset(env, profile);
        }
        env.skyType.Override((int)SkyType.Gradient);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
