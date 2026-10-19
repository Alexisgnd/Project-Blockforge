using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// =========================================================
// SETUP DE LA SCENE MATCHMAKING_SPACE (attente du matchmaking)
// Menu : Blockforge > Setup Matchmaking Space Scene
// Scene amenee par un petit fondu quand « Find a match » lance
// une recherche (PlayMenuController -> SceneFade) ; le bandeau
// du Forge Loader reste par-dessus.
// - Vaisseau : SuperDestroyer.glb (export Three.js du « super
//   destroyer carrier », ~1 km de long, metres, Y-up) importe par
//   glTFast. S'il est depose a la racine du projet
//   (SuperDestroyer.glb ou super-destroyer-carrier.glb), il est
//   deplace et renomme en Art/Models/SuperDestroyer.glb.
// - Pivot « SuperDestroyer » a l'origine (objet a animer plus
//   tard) ; le modele en enfant, nez tourne vers +Z (mesure sur
//   hull_fore / hull_aft) et centre sur le pivot.
// - Camera « MatchmakingCamera » (MainCamera) en vue 3/4 avant,
//   cadree sur les bornes du vaisseau ; fond noir comme le
//   garage (le ciel du projet reste pour l'eclairage ambiant).
// - « Sun » : lumiere directionnelle rasante, ombres douces.
// - « SpaceStarDome » : meme champ d'etoiles que le garage
//   (Art/Garage/SpaceStarDome.mat), sphere de 20 km.
// - « SpaceExposureVolume » : exposition fixe (comme le garage)
//   + distance d'ombre etendue a l'echelle du vaisseau.
// - « MatchmakingSpace » : MatchmakingSpaceScene (retour au menu
//   si la recherche est annulee).
// Relancable : reapplique tout sans dupliquer. Les reglages se
// retouchent ensuite dans la scene (camera, soleil) ou dans
// Art/Space/MatchmakingSpaceProfile.
// S'execute une seule fois tout seul apres compilation si la
// scene n'existe pas encore (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MatchmakingSpaceSetup
{
    private const string ScenePath = BlockforgeScenes.MatchmakingSpace;
    public const string ModelPath = "Assets/_Project/Art/Models/SuperDestroyer.glb";
    private static readonly string[] IncomingModelPaths = { "SuperDestroyer.glb", "super-destroyer-carrier.glb" };
    private const string ArtDir = "Assets/_Project/Art/Space";
    private const string ProfilePath = ArtDir + "/MatchmakingSpaceProfile.asset";
    private const string DomeMaterialPath = "Assets/_Project/Art/Garage/SpaceStarDome.mat";
    private const string AutoRunMarker = "Library/BlockforgeMatchmakingSpaceSetup-v1.done";

    private const string ShipPivotName = "SuperDestroyer";
    private const string ShipModelName = "SuperDestroyer_Model";
    private const string CameraName = "MatchmakingCamera";
    private const string SunName = "Sun";
    private const string DomeName = "SpaceStarDome";
    private const string VolumeName = "SpaceExposureVolume";
    private const string ControllerName = "MatchmakingSpace";

    // ---- Reglages ----
    private const float FixedExposure = 11.5f;     // EV100, comme le garage
    private const float SunLux = 12000f;
    // Vient de la droite, un peu de l'arriere et d'en haut : flanc vu par la camera eclaire, nez en demi-teinte
    private static readonly Vector3 SunEuler = new(35f, -80f, 0f);
    private const float ShadowDistance = 4000f;    // m : le vaisseau fait ~1 km
    private const float DomeRadius = 20000f;
    private const float CameraFov = 32f;
    private const float CameraFar = 60000f;
    // Direction camera <- vaisseau (repere du vaisseau, nez = +Z) : 3/4 avant, un peu au-dessus
    private static readonly Vector3 CameraDirection = new(0.85f, 0.3f, 0.75f);
    private const float FrameMargin = 0.95f;       // < 1 : le vaisseau deborde un peu du cadre

    static MatchmakingSpaceSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker) || File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!File.Exists(ModelPath) && System.Array.TrueForAll(IncomingModelPaths, p => !File.Exists(p)))
            return;

        string previous = SceneManager.GetActiveScene().path;
        if (Setup() && !string.IsNullOrEmpty(previous) && File.Exists(previous) && previous != ScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    [MenuItem("Blockforge/Setup Matchmaking Space Scene")]
    public static void SetupFromMenu()
    {
        Setup();
    }

    private static bool Setup()
    {
        ImportIncomingModel();
        var model = BlockforgeScenes.LoadModelAsset(ModelPath);
        if (model == null)
        {
            Debug.LogError($"[MatchmakingSpaceSetup] GLB introuvable ou pas encore importe : {ModelPath} " +
                           "(importeur glTFast). Relance le menu une fois l'import termine.");
            return false;
        }

        if (!OpenOrCreateScene(out var scene))
            return false;

        EnsureFolder();
        var pivot = EnsureShip(scene, model, out var bounds);
        var camera = EnsureCamera(scene, bounds);
        EnsureSun(scene);
        bool dome = EnsureDome(scene);
        EnsureVolume(scene);
        EnsureController(scene);

        BlockforgeScenes.RegisterAllInBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(AutoRunMarker, "done");

        Debug.Log($"[MatchmakingSpaceSetup] Scene Matchmaking_Space prete : '{pivot.name}' a l'origine " +
                  $"({bounds.size.x:0} x {bounds.size.y:0} x {bounds.size.z:0} m, nez vers +Z), camera a {Vector3.Distance(camera.transform.position, bounds.center):0} m, " +
                  $"soleil {SunLux} lux, exposition fixe {FixedExposure} EV" + (dome ? ", ciel etoile" : "") + ". Scene sauvegardee et ajoutee au build.");
        return true;
    }

    // =========================================================
    // SCENE + MODELE
    // =========================================================

    private static bool OpenOrCreateScene(out Scene scene)
    {
        if (File.Exists(ScenePath))
            return BlockforgeScenes.OpenForSetup(ScenePath, out scene);

        scene = default;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[MatchmakingSpaceSetup] Scene creee : {ScenePath}");
        return true;
    }

    private static void ImportIncomingModel()
    {
        foreach (var incoming in IncomingModelPaths)
        {
            if (!File.Exists(incoming))
                continue;
            if (File.Exists(ModelPath))
            {
                Debug.LogWarning($"[MatchmakingSpaceSetup] {incoming} present a la racine mais {ModelPath} existe deja : " +
                                 "fichier de la racine laisse tel quel, le GLB du projet est conserve.");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ModelPath));
            File.Move(incoming, ModelPath);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[MatchmakingSpaceSetup] {incoming} deplace et renomme en {ModelPath}, import glTFast lance.");
            return;
        }
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(ArtDir))
            AssetDatabase.CreateFolder("Assets/_Project/Art", "Space");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }
        return null;
    }

    private static GameObject EnsureRoot(Scene scene, string name)
    {
        var go = FindRoot(scene, name);
        if (go == null)
        {
            go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        return go;
    }

    // Pivot a l'origine (a animer plus tard), modele en enfant : nez vers +Z, centre sur le pivot
    private static GameObject EnsureShip(Scene scene, GameObject model, out Bounds bounds)
    {
        var pivot = EnsureRoot(scene, ShipPivotName);
        pivot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        pivot.transform.localScale = Vector3.one;

        // Instance precedente remplacee (modele mis a jour entre deux setups)
        for (int i = pivot.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(pivot.transform.GetChild(i).gameObject);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot.transform);
        instance.name = ShipModelName;
        var t = instance.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        // Orientation : nez (hull_fore) vers +Z
        // (lacet seul : FromToRotation a 180 degres pourrait retourner le vaisseau)
        if (TryNodeBounds(t, "hull_fore", out var fore) && TryNodeBounds(t, "hull_aft", out var aft))
        {
            Vector3 nose = fore.center - aft.center;
            nose.y = 0f;
            if (nose.sqrMagnitude > 0.01f)
                t.localRotation = Quaternion.AngleAxis(Vector3.SignedAngle(nose, Vector3.forward, Vector3.up), Vector3.up);
        }
        else
        {
            Debug.LogWarning("[MatchmakingSpaceSetup] hull_fore / hull_aft introuvables : orientation du GLB conservee.");
        }

        // Centrage sur le pivot
        bounds = MeasureBounds(t);
        t.position -= bounds.center;
        bounds.center = Vector3.zero;

        int lights = instance.GetComponentsInChildren<Light>(true).Length;
        Debug.Log($"[MatchmakingSpaceSetup] {Path.GetFileName(ModelPath)} instancie sous '{ShipPivotName}' " +
                  $"({instance.GetComponentsInChildren<Renderer>(true).Length} renderers, {lights} lumiere(s) du GLB).");
        return pivot;
    }

    // Bornes d'un noeud du GLB (renderers du noeud et de ses enfants : glTFast peut
    // eclater un maillage multi-primitives en sous-objets)
    private static bool TryNodeBounds(Transform root, string name, out Bounds bounds)
    {
        bounds = default;
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
        {
            if (node.name != name)
                continue;
            bool has = false;
            foreach (var r in node.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (has)
                return true;
        }
        return false;
    }

    private static Bounds MeasureBounds(Transform root)
    {
        bool has = false;
        var b = new Bounds(root.position, Vector3.zero);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // =========================================================
    // CAMERA, SOLEIL, ETOILES, VOLUME
    // =========================================================

    private static Camera EnsureCamera(Scene scene, Bounds ship)
    {
        var go = EnsureRoot(scene, CameraName);
        go.tag = "MainCamera";
        if (!go.TryGetComponent<Camera>(out var camera))
            camera = go.AddComponent<Camera>();
        camera.fieldOfView = CameraFov;
        camera.nearClipPlane = 1f;
        camera.farClipPlane = CameraFar;

        if (!go.TryGetComponent<HDAdditionalCameraData>(out var hd))
            hd = go.AddComponent<HDAdditionalCameraData>();
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = Color.black;
        EditorUtility.SetDirty(hd);

        // Cadrage : la sphere englobante tient dans le champ vertical
        float radius = ship.extents.magnitude;
        float distance = radius / Mathf.Sin(CameraFov * 0.5f * Mathf.Deg2Rad) * FrameMargin;
        Vector3 position = ship.center + CameraDirection.normalized * distance;
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(ship.center - position, Vector3.up));
        EditorUtility.SetDirty(camera);
        return camera;
    }

    private static void EnsureSun(Scene scene)
    {
        var go = EnsureRoot(scene, SunName);
        go.transform.rotation = Quaternion.Euler(SunEuler);
        if (!go.TryGetComponent<Light>(out var light))
            light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.shadows = LightShadows.Soft;
        if (!go.TryGetComponent<HDAdditionalLightData>(out var hd))
            hd = go.AddComponent<HDAdditionalLightData>();
#pragma warning disable 618 // API de remplacement pas encore stable selon les versions HDRP
        hd.SetIntensity(SunLux, LightUnit.Lux);
#pragma warning restore 618
        EditorUtility.SetDirty(light);
        EditorUtility.SetDirty(hd);
    }

    private static bool EnsureDome(Scene scene)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(DomeMaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"[MatchmakingSpaceSetup] {DomeMaterialPath} introuvable : lance Blockforge > Setup Garage Space Sky " +
                             "puis relance ce setup pour le ciel etoile.");
            return false;
        }

        var dome = FindRoot(scene, DomeName);
        if (dome == null)
        {
            dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = DomeName;
            Object.DestroyImmediate(dome.GetComponent<Collider>());
            SceneManager.MoveGameObjectToScene(dome, scene);
        }
        dome.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        dome.transform.localScale = Vector3.one * (DomeRadius * 2f);

        var renderer = dome.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
        EditorUtility.SetDirty(dome);
        return true;
    }

    private static void EnsureVolume(Scene scene)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        if (!profile.TryGet<Exposure>(out var exposure))
        {
            exposure = profile.Add<Exposure>(false);
            exposure.name = nameof(Exposure);
            AssetDatabase.AddObjectToAsset(exposure, profile);
        }
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(FixedExposure);
        EditorUtility.SetDirty(exposure);

        if (!profile.TryGet<HDShadowSettings>(out var shadows))
        {
            shadows = profile.Add<HDShadowSettings>(false);
            shadows.name = nameof(HDShadowSettings);
            AssetDatabase.AddObjectToAsset(shadows, profile);
        }
        shadows.maxShadowDistance.Override(ShadowDistance);
        EditorUtility.SetDirty(shadows);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var go = EnsureRoot(scene, VolumeName);
        if (!go.TryGetComponent<Volume>(out var volume))
            volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
    }

    private static void EnsureController(Scene scene)
    {
        var go = EnsureRoot(scene, ControllerName);
        if (!go.TryGetComponent<MatchmakingSpaceScene>(out _))
            go.AddComponent<MatchmakingSpaceScene>();
        EditorUtility.SetDirty(go);
    }
}
