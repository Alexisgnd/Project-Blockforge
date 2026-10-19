using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// =========================================================
// SETUP DE LA CINEMATIQUE DE MATCHMAKING (scene Matchmaking_Space)
// Menu : Blockforge > Setup Matchmaking Cinematic
// A lancer apres Setup Matchmaking Space Scene (Super Destroyer,
// camera, soleil, ciel). Ajoute sans toucher au reste :
// - « GarageShip » : pivot + ShipGarage_V3.glb (meme modele que
//   le garage), nez vers +Z (Front lintel / Engine nozzle),
//   centre sur le pivot ; place a l'execution dans la baie ;
// - « EngineFX_N » sur chaque « Engine nozzle » du vaisseau
//   garage : ShipThrusterFX (materiaux des propulseurs du garage,
//   cyan), reglages bien plus puissants que dans le garage (jets
//   de 55 m, etincelles, lumiere ponctuelle par moteur) ;
// - « EngineFX_<nom> » sur chaque tuyere du Super Destroyer
//   (engine_main_port/stbd, engine_center, engine_aux_port/stbd) :
//   ShipThrusterFX dimensionne sur la tuyere (jet = 9 fois son
//   rayon, jusqu'a 180 m), lumiere ponctuelle ; les cones de plume
//   statiques du GLB (engine_*_plume, halos) sont masques ;
// - « BowFlame » au centre de la face avant : ReentryFlameFX
//   (calotte de plasma aux dimensions de la face, rubans de
//   flamme sur le pourtour qui filent vers l'arriere, particules),
//   plus « BowFlameLight » ; allumee au plan de poursuite ;
// - materiaux Art/Space/Reentry{Cap,Ribbon,Streak,Ember},
//   SpeedStreak (HDRP/Unlit additif, textures Art/Textures/
//   Reentry{Cap,Ribbon}.png — Ribbon periodique en V — et
//   SoftDot.png) ;
// - Bloom dans Art/Space/MatchmakingSpaceProfile (la flamme et
//   les moteurs rayonnent) ;
// - MatchmakingCinematic sur « MatchmakingSpace », cable.
// Relancable. S'execute une seule fois tout seul apres
// compilation si la scene existe (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MatchmakingCinematicSetup
{
    private const string ScenePath = BlockforgeScenes.MatchmakingSpace;
    private const string TextureDir = "Assets/_Project/Art/Textures";
    private const string SpaceDir = "Assets/_Project/Art/Space";
    private const string GarageMatDir = "Assets/_Project/Art/Garage";
    private const string AutoRunMarker = "Library/BlockforgeMatchmakingCinematic-v10.done";

    private const string CarrierName = "SuperDestroyer";
    private const string CameraName = "MatchmakingCamera";
    private const string ControllerName = "MatchmakingSpace";
    private const string GarageShipName = "GarageShip";
    private const string GarageModelName = "GarageShip_Model";
    private const string BowFlameName = "BowFlame";

    private const string ProfilePath = SpaceDir + "/MatchmakingSpaceProfile.asset";
    private const float BloomIntensity = 0.25f;
    // Flamme d'entree : enveloppe l'avant du vaisseau garage (60 x 45 m) et file vers l'arriere
    private const float BowLightLumen = 600000f, BowLightRange = 260f;
    private static readonly Color BowLightColor = new(1f, 0.55f, 0.2f);

    static MatchmakingCinematicSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker) || !File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        string previous = SceneManager.GetActiveScene().path;
        if (Setup() && !string.IsNullOrEmpty(previous) && File.Exists(previous) && previous != ScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    [MenuItem("Blockforge/Setup Matchmaking Cinematic")]
    public static void SetupFromMenu()
    {
        Setup();
    }

    private static bool Setup()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("[MatchmakingCinematic] Scene Matchmaking_Space absente : lance d'abord Blockforge > Setup Matchmaking Space Scene.");
            return false;
        }

        var garageModel = BlockforgeScenes.LoadModelAsset(GarageSceneSetup.ModelPath);
        if (garageModel == null)
        {
            Debug.LogError($"[MatchmakingCinematic] Vaisseau garage introuvable : {GarageSceneSetup.ModelPath}.");
            return false;
        }

        // Materiaux
        if (!AssetDatabase.IsValidFolder(SpaceDir))
            AssetDatabase.CreateFolder("Assets/_Project/Art", "Space");
        var capTex = ImportTexture("ReentryCap", repeatV: false);
        var ribbonTex = ImportTexture("ReentryRibbon", repeatV: true);
        var dotTex = ImportTexture("SoftDot", repeatV: false);
        if (capTex == null || ribbonTex == null || dotTex == null)
            return false; // textures pas encore importees : relancer apres l'import
        // L'eclat est module par ReentryFlameFX (bloc de proprietes)
        var capMat = EnsureMaterial($"{SpaceDir}/ReentryCap.mat", capTex, Color.white, 2f);
        var ribbonMat = EnsureMaterial($"{SpaceDir}/ReentryRibbon.mat", ribbonTex, Color.white, 2f);
        var filamentMat = EnsureMaterial($"{SpaceDir}/ReentryStreak.mat", dotTex, new Color(1f, 0.62f, 0.22f), 3f);
        var emberMat = EnsureMaterial($"{SpaceDir}/ReentryEmber.mat", dotTex, new Color(1f, 0.45f, 0.12f), 6f);
        var streakMat = EnsureMaterial($"{SpaceDir}/SpeedStreak.mat", dotTex, new Color(0.75f, 0.88f, 1f), 1.6f);
        EnsureBloom();
        var plume = AssetDatabase.LoadAssetAtPath<Material>($"{GarageMatDir}/ThrusterPlume.mat");
        var core = AssetDatabase.LoadAssetAtPath<Material>($"{GarageMatDir}/ThrusterCore.mat");
        var spark = AssetDatabase.LoadAssetAtPath<Material>($"{GarageMatDir}/ThrusterSpark.mat");
        if (plume == null || core == null)
            Debug.LogWarning("[MatchmakingCinematic] Materiaux des propulseurs du garage absents (Art/Garage/Thruster*.mat) : " +
                             "lance Blockforge > Setup Garage Thrusters, puis relance ce setup.");

        if (!BlockforgeScenes.OpenForSetup(ScenePath, out var scene))
            return false;

        var carrier = FindRoot(scene, CarrierName);
        var cameraGo = FindRoot(scene, CameraName);
        var controllerGo = FindRoot(scene, ControllerName);
        if (carrier == null || cameraGo == null || controllerGo == null)
        {
            Debug.LogError("[MatchmakingCinematic] SuperDestroyer, MatchmakingCamera ou MatchmakingSpace introuvable : " +
                           "relance Blockforge > Setup Matchmaking Space Scene.");
            return false;
        }

        // ---------- Vaisseau garage ----------
        var ship = FindRoot(scene, GarageShipName);
        if (ship == null)
        {
            ship = new GameObject(GarageShipName);
            SceneManager.MoveGameObjectToScene(ship, scene);
        }
        for (int i = ship.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(ship.transform.GetChild(i).gameObject);
        // Pose de travail a cote du Super Destroyer (la cinematique le met dans la baie)
        ship.transform.SetPositionAndRotation(new Vector3(400f, 0f, 0f), Quaternion.identity);
        ship.transform.localScale = Vector3.one;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(garageModel, ship.transform);
        model.name = GarageModelName;
        var m = model.transform;
        m.localPosition = Vector3.zero;
        m.localRotation = Quaternion.identity;
        m.localScale = Vector3.one;
        if (TryNodeBounds(m, "Front lintel", out var front) && TryNodeBounds(m, "Engine nozzle", out var rear))
        {
            Vector3 nose = front.center - rear.center;
            nose.y = 0f;
            if (nose.sqrMagnitude > 0.01f)
                m.localRotation = Quaternion.AngleAxis(Vector3.SignedAngle(nose, Vector3.forward, Vector3.up), Vector3.up);
        }
        else
        {
            Debug.LogWarning("[MatchmakingCinematic] Front lintel / Engine nozzle introuvables : orientation du garage conservee.");
        }
        var shipBounds = MeasureBounds(m);
        m.position -= shipBounds.center - ship.transform.position;
        shipBounds = MeasureBounds(m);

        // Propulseurs sur les tuyeres (jet vers l'arriere)
        var engines = new System.Collections.Generic.List<ShipThrusterFX>();
        int n = 0;
        foreach (var r in m.GetComponentsInChildren<Renderer>(true))
        {
            string name = r.gameObject.name;
            if (name != "Engine nozzle" && !name.StartsWith("Engine nozzle_"))
                continue;
            var anchor = new GameObject($"EngineFX_{n++}").transform;
            anchor.SetParent(ship.transform, false);
            anchor.SetPositionAndRotation(r.bounds.center - ship.transform.forward * 0.2f, Quaternion.LookRotation(-ship.transform.forward, Vector3.up));
            // Tuyere de ~2,7 m : jet de 55 m, bien plus puissant que dans le garage
            var fx = ConfigureThruster(anchor, plume, core, spark, radius: 1.35f, lengthFactor: 40f, lumen: 150000f);
            engines.Add(fx);
        }

        // Propulseurs animes du Super Destroyer, a la place des cones statiques du GLB
        int carrierEngines = SetupCarrierEngines(carrier.transform, plume, core, spark);

        // Gaine de plasma au centre de la face avant, +Z vers l'arriere : la calotte couvre toute la face
        var bow = new GameObject(BowFlameName).transform;
        bow.SetParent(ship.transform, false);
        bow.SetPositionAndRotation(
            ship.transform.position + ship.transform.forward * (shipBounds.extents.z + 0.5f) + Vector3.up * (shipBounds.center.y - ship.transform.position.y),
            Quaternion.LookRotation(-ship.transform.forward, Vector3.up));
        var bowFx = bow.gameObject.AddComponent<ReentryFlameFX>();
        bowFx.faceWidth = shipBounds.size.x;
        bowFx.faceHeight = shipBounds.size.y;
        bowFx.cornerRadius = 12f;
        bowFx.margin = 6f;
        bowFx.capRoundness = 2f;
        bowFx.capRipple = 0.22f;
        bowFx.capMaterial = capMat;
        bowFx.capGlow = 1.7f;
        bowFx.capFront = 1.5f;  // plaquee sur la face
        bowFx.capWrap = 10f;    // rabattue sur les flancs
        bowFx.ribbonMaterial = ribbonMat;
        bowFx.ribbonGlow = 1.4f;
        bowFx.ribbonCount = 56;
        bowFx.ribbonLength = new Vector2(25f, 110f);
        bowFx.ribbonOverlap = 1.8f;
        bowFx.ribbonTaper = 0.6f;
        bowFx.ribbonWave = 3.5f;
        bowFx.ribbonLift = 5f;
        bowFx.streakMaterial = filamentMat;
        bowFx.emberMaterial = emberMat;
        bowFx.intensity = 0f;
        bow.gameObject.SetActive(false); // allumee par la cinematique, au plan de poursuite

        var lightGo = new GameObject("BowFlameLight");
        lightGo.transform.SetParent(ship.transform, false);
        lightGo.transform.position = bow.position + ship.transform.forward * 10f;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = BowLightColor;
        light.range = BowLightRange;
        light.shadows = LightShadows.None;
        lightGo.AddComponent<HDAdditionalLightData>();
#pragma warning disable 618 // API de remplacement pas encore stable selon les versions HDRP
        lightGo.GetComponent<HDAdditionalLightData>().SetIntensity(BowLightLumen, UnityEngine.Rendering.LightUnit.Lumen);
#pragma warning restore 618
        lightGo.SetActive(false);

        // ---------- Controleur ----------
        if (!controllerGo.TryGetComponent<MatchmakingCinematic>(out var cinematic))
            cinematic = controllerGo.AddComponent<MatchmakingCinematic>();
        cinematic.carrier = carrier.transform;
        cinematic.cam = cameraGo.GetComponent<Camera>();
        cinematic.garageShip = ship.transform;
        cinematic.engines = engines.ToArray();
        cinematic.bowFlame = bowFx;
        cinematic.bowLight = light;
        cinematic.streakMaterial = streakMat;
        EditorUtility.SetDirty(cinematic);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(AutoRunMarker, "done");
        Debug.Log($"[MatchmakingCinematic] Cinematique cablee : GarageShip ({shipBounds.size.x:0} x {shipBounds.size.y:0} x {shipBounds.size.z:0} m), " +
                  $"{engines.Count} propulseur(s), {carrierEngines} propulseur(s) sur le Super Destroyer, gaine de plasma, trainees de vitesse. " +
                  "Scene sauvegardee. Lance la scene pour la voir.");
        return true;
    }

    // =========================================================
    // PROPULSEURS
    // =========================================================

    // ShipThrusterFX dimensionne sur le rayon de la tuyere : jet = radius x lengthFactor,
    // coeur plus court, etincelles rapides, lumiere ponctuelle cyan juste derriere
    private static ShipThrusterFX ConfigureThruster(Transform anchor, Material plume, Material core, Material spark,
                                                    float radius, float lengthFactor, float lumen)
    {
        var fx = anchor.gameObject.AddComponent<ShipThrusterFX>();
        fx.plumeMaterial = plume;
        fx.coreMaterial = core;
        fx.sparkMaterial = spark;
        fx.jetBaseHalfWidth = radius * 1.6f;
        fx.jetTipHalfWidth = radius * 0.25f;
        fx.jetLength = radius * lengthFactor;
        fx.coreBaseHalfWidth = radius * 0.9f;
        fx.coreTipHalfWidth = radius * 0.1f;
        fx.coreLength = radius * lengthFactor * 0.38f;
        fx.jetGlow = 7f;
        fx.coreGlow = 10f;
        fx.flicker = 0.14f;
        fx.flickerSpeed = 14f;
        fx.spinSpeed = 220f;
        fx.sparks = true;
        fx.sparkRate = 70f;
        fx.sparkSpeed = new Vector2(radius * lengthFactor * 0.6f, radius * lengthFactor * 1.3f);

        var lightGo = new GameObject("FlameLight");
        lightGo.transform.SetParent(anchor, false);
        lightGo.transform.localPosition = new Vector3(0f, 0f, radius * 2f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.35f, 0.85f, 1f);
        light.range = radius * lengthFactor * 1.2f;
        light.shadows = LightShadows.None;
        lightGo.AddComponent<HDAdditionalLightData>();
#pragma warning disable 618 // API de remplacement pas encore stable selon les versions HDRP
        lightGo.GetComponent<HDAdditionalLightData>().SetIntensity(lumen, UnityEngine.Rendering.LightUnit.Lumen);
#pragma warning restore 618
        fx.flameLight = light;
        return fx;
    }

    private static readonly string[] CarrierEngineNames = { "main_port", "main_stbd", "center", "aux_port", "aux_stbd" };

    // Une tuyere « engine_<nom> » du GLB : le jet part de sa face arriere, large comme elle ;
    // le cone statique « engine_<nom>_plume » (et son halo) est masque.
    private static int SetupCarrierEngines(Transform carrier, Material plume, Material core, Material spark)
    {
        for (int i = carrier.childCount - 1; i >= 0; i--)
        {
            if (carrier.GetChild(i).name.StartsWith("EngineFX_"))
                Object.DestroyImmediate(carrier.GetChild(i).gameObject);
        }

        Vector3 fwd = carrier.forward;
        var fwdAbs = new Vector3(Mathf.Abs(fwd.x), Mathf.Abs(fwd.y), Mathf.Abs(fwd.z));
        int count = 0;
        foreach (var name in CarrierEngineNames)
        {
            Renderer nozzle = null;
            foreach (var t in carrier.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "engine_" + name + "_plume")
                    t.gameObject.SetActive(false);
                else if (t.name == "engine_" + name && nozzle == null)
                    nozzle = t.GetComponent<Renderer>();
            }
            if (nozzle == null)
            {
                Debug.LogWarning($"[MatchmakingCinematic] Tuyere engine_{name} introuvable sur le Super Destroyer.");
                continue;
            }

            var b = nozzle.bounds;
            float depth = Vector3.Dot(b.extents, fwdAbs);
            float radius = 0.5f * Mathf.Max(b.size.x * (1f - fwdAbs.x), b.size.y * (1f - fwdAbs.y), b.size.z * (1f - fwdAbs.z));
            Vector3 exit = b.center - fwd * depth;

            var anchor = new GameObject("EngineFX_" + name).transform;
            anchor.SetParent(carrier, false);
            anchor.SetPositionAndRotation(exit - fwd * 0.5f, Quaternion.LookRotation(-fwd, carrier.up));
            float lumen = 1500000f * (radius / 20f) * (radius / 20f);
            ConfigureThruster(anchor, plume, core, spark, radius, lengthFactor: 9f, lumen: Mathf.Max(lumen, 200000f));
            count++;
        }
        return count;
    }

    // =========================================================
    // OUTILS
    // =========================================================

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }
        return null;
    }

    // Noeud exact ou doublon suffixe par glTFast (« Engine nozzle_1 »)
    private static bool TryNodeBounds(Transform root, string name, out Bounds bounds)
    {
        bounds = default;
        bool has = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.gameObject.name;
            if (n != name && !n.StartsWith(name + "_"))
                continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
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

    // Bloom dans le profil de la scene : la flamme (emissif HDR) et les moteurs rayonnent
    private static void EnsureBloom()
    {
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[MatchmakingCinematic] Profil {ProfilePath} absent : relance Blockforge > Setup Matchmaking Space Scene pour le bloom.");
            return;
        }
        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(false);
            bloom.name = nameof(Bloom);
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }
        bloom.intensity.Override(BloomIntensity);
        bloom.scatter.Override(0.6f);
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    private static Texture2D ImportTexture(string name, bool repeatV)
    {
        string path = $"{TextureDir}/{name}.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[MatchmakingCinematic] Texture introuvable : {path}");
            return null;
        }
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapModeU = TextureWrapMode.Clamp;
        importer.wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; // rubans : defilement le long
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // HDRP/Unlit additif (comme les propulseurs du garage) : tout l'eclat passe par l'emissif, hors exposition
    private static Material EnsureMaterial(string path, Texture2D texture, Color tint, float intensity)
    {
        if (texture == null)
            return null;
        var shader = Shader.Find("HDRP/Unlit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }
        material.SetFloat("_SurfaceType", 1f);       // Transparent
        material.SetFloat("_BlendMode", 1f);         // Additive
        material.SetFloat("_DoubleSidedEnable", 1f);
        material.SetFloat("_EnableFogOnTransparent", 0f);
        material.SetColor("_UnlitColor", new Color(0f, 0f, 0f, 1f));
        material.SetTexture("_UnlitColorMap", texture);
        material.SetTexture("_EmissiveColorMap", texture);
        material.SetColor("_EmissiveColor", tint * intensity);
        material.SetFloat("_EmissiveExposureWeight", 0f);
        HDMaterial.ValidateMaterial(material);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}
