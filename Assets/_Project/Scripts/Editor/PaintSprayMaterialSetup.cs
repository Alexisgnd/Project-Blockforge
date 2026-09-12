using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// =========================================================
// SETUP DU SPRAY PAINT DU GARAGE
// Menu : Blockforge > Setup Paint Spray
// - Cree les materiaux HDRP/Lit du spray (corps + verre/
//   liquide/bulles transparents + les 12 teintes de la roue)
//   dans Art/Materials/Garage et les remappe sur le FBX
// - Renomme le clip "Scene" en "PaintSpray_Bubbles", active
//   Loop Time, cree l'AnimatorController et l'assigne
// - Integre le spray dans la scene Garage : wrapper
//   "PaintSpray_Garage" a cote de la pince, memes effets de
//   mouvement (PlierGarageController copie), molette de
//   couleur (PaintSprayColorWheel) et switch 1/2
//   (GarageToolSwitcher, pince par defaut)
// S'execute automatiquement apres compilation tant que le
// cablage scene n'a pas reussi (marqueur dans Library/),
// relancable par le menu.
// =========================================================

[InitializeOnLoad]
public static class PaintSprayMaterialSetup
{
    private const string FbxPath = "Assets/_Project/Art/Models/PaintSpray_Garage.fbx";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Garage";
    private const string AnimParentDir = "Assets/_Project/Art";
    private const string AnimDir = "Assets/_Project/Art/Animations";
    private const string ControllerPath = AnimDir + "/PaintSpray_Garage.controller";
    private const string FilteredClipPath = AnimDir + "/PaintSpray_Bubbles.anim";
    private const string ClipName = "PaintSpray_Bubbles";
    private const string WrapperName = "PaintSpray_Garage";
    private const string ModelName = "PaintSpray_Model";
    private const string AutoRunMarker = "Library/BlockforgePaintSpray.done";

    // Intensite emissive, en nits (exposition fixe EV 9.5 dans les scenes).
    private const float GlowNits = 800f;

    private struct SprayMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs Blender), alpha inclus
        public float Metallic;
        public float Smoothness;
        public Color EmissiveColor;  // lineaire, noir = pas d'emission
        public bool Transparent;
        public int SortPriority;     // ordre de rendu des transparents
        public bool DoubleSided;
    }

    // Nom du materiau dans le FBX -> definition du materiau partage
    // (valeurs du .blend PaintSpray v2 : smoothness = 1 - roughness Blender)
    private static readonly Dictionary<string, SprayMat> FbxMaterials = BuildMaterialTable();

    // Materiaux de l'ancien spray (v1), supprimes s'ils existent encore
    private static readonly string[] ObsoleteMaterialAssets =
    {
        "Spray_WhiteArmor", "Spray_Steel", "Spray_DarkMetal", "Spray_GreyBody",
        "Spray_CyanGlow", "Spray_Glass", "Spray_Liquid", "Spray_Bubble",
    };

    // Noms des noeuds du FBX utilises par la molette de couleur
    private const string WheelCenterNode = "Colour_Wheel";          // axe des 2 palettes
    private const string NeedleNode = "Colour_Marker_Pivot";        // pivot des 2 marqueurs
    private const string NeedleArrowNode = "Selected_Colour_Marker"; // marqueur : direction courante
    private const string WedgeNodeFormat = "Colour_Swatch_1_{0:00}"; // 12 swatches d'une palette
    private const string LiquidNode = "Paint_Liquid";               // liquide (shape key houle)
    private const string BubbleNodeFormat = "Bubble_{0:00}";        // 14 bulles animees

    private static Dictionary<string, SprayMat> BuildMaterialTable()
    {
        var table = new Dictionary<string, SprayMat>
        {
            { "Ceramic_White",     new SprayMat { AssetName = "Spray_CeramicWhite",     BaseColor = new Color(0.720f, 0.760f, 0.800f), Metallic = 0.30f, Smoothness = 0.65f, EmissiveColor = Color.black } },
            { "Brushed_Aluminium", new SprayMat { AssetName = "Spray_BrushedAluminium", BaseColor = new Color(0.360f, 0.410f, 0.460f), Metallic = 0.80f, Smoothness = 0.75f, EmissiveColor = Color.black } },
            { "Graphite",          new SprayMat { AssetName = "Spray_Graphite",         BaseColor = new Color(0.035f, 0.045f, 0.055f), Metallic = 0.50f, Smoothness = 0.65f, EmissiveColor = Color.black } },
            { "Accent_Red",        new SprayMat { AssetName = "Spray_AccentRed",        BaseColor = new Color(0.650f, 0.035f, 0.020f), Metallic = 0.25f, Smoothness = 0.65f, EmissiveColor = Color.black } },
            { "Reservoir_Clear",   new SprayMat { AssetName = "Spray_ReservoirClear",   BaseColor = new Color(0.650f, 0.850f, 0.950f, 0.14f), Metallic = 0f, Smoothness = 0.88f, EmissiveColor = Color.black, Transparent = true, SortPriority = 3, DoubleSided = true } },
            { "Liquid_Red",        new SprayMat { AssetName = "Spray_LiquidRed",        BaseColor = new Color(0.800f, 0.035f, 0.025f, 0.62f), Metallic = 0.05f, Smoothness = 0.80f, EmissiveColor = Color.black, Transparent = true, SortPriority = 1 } },
            { "Bubbles_Pearl",     new SprayMat { AssetName = "Spray_BubblesPearl",     BaseColor = new Color(1.000f, 0.480f, 0.320f), Metallic = 0.15f, Smoothness = 0.85f, EmissiveColor = Color.black } },
        };

        // Les 12 teintes des palettes (memes valeurs que le .blend)
        var hues = new[]
        {
            new Color(0.850f, 0.127f, 0.127f), new Color(0.850f, 0.489f, 0.127f),
            new Color(0.850f, 0.850f, 0.127f), new Color(0.489f, 0.850f, 0.127f),
            new Color(0.127f, 0.850f, 0.127f), new Color(0.127f, 0.850f, 0.489f),
            new Color(0.127f, 0.850f, 0.850f), new Color(0.127f, 0.489f, 0.850f),
            new Color(0.127f, 0.127f, 0.850f), new Color(0.489f, 0.127f, 0.850f),
            new Color(0.850f, 0.127f, 0.850f), new Color(0.850f, 0.127f, 0.489f),
        };
        for (int i = 0; i < hues.Length; i++)
        {
            table[$"Palette_{i:00}"] = new SprayMat
            {
                AssetName = $"Spray_Hue_{i:00}",
                BaseColor = hues[i],
                Metallic = 0.10f,
                Smoothness = 0.65f,
                EmissiveColor = Color.black,
            };
        }
        return table;
    }

    static PaintSprayMaterialSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker))
            return;

        // Le marqueur n'est ecrit que quand le cablage scene a reussi :
        // tant que la scene Garage n'a pas ete ouverte, le setup
        // retentera a la prochaine compilation.
        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Setup Paint Spray")]
    public static void SetupFromMenu()
    {
        Setup();
    }

    private static bool Setup()
    {
        var materials = CreateMaterials();
        ConfigureImporter(materials);
        var clip = BuildFilteredClip();
        var controller = EnsureAnimatorController(clip);
        bool sceneOk = WireScene(controller);

        Debug.Log("[PaintSprayMaterialSetup] Materiaux HDRP crees et remappes sur PaintSpray_Garage.fbx" +
                  (sceneOk ? ", spray integre dans la scene Garage." : " (cablage scene en attente de la scene Garage)."));
        return sceneOk;
    }

    // =====================================================
    // MATERIAUX
    // =====================================================

    private static Dictionary<string, Material> CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialDir))
            AssetDatabase.CreateFolder(MaterialParentDir, "Garage");

        var shader = Shader.Find("HDRP/Lit");
        var result = new Dictionary<string, Material>();

        foreach (var (fbxName, def) in FbxMaterials)
        {
            var matPath = $"{MaterialDir}/{def.AssetName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.SetColor("_BaseColor", def.BaseColor);
            mat.SetFloat("_Metallic", def.Metallic);
            mat.SetFloat("_Smoothness", def.Smoothness);
            mat.SetColor("_EmissiveColor", def.EmissiveColor * GlowNits);

            mat.SetFloat("_SurfaceType", def.Transparent ? 1f : 0f);
            if (def.Transparent)
            {
                mat.SetFloat("_BlendMode", 0f); // Alpha
                mat.SetFloat("_TransparentSortPriority", def.SortPriority);
            }
            mat.SetFloat("_DoubleSidedEnable", def.DoubleSided ? 1f : 0f);

            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            result[fbxName] = mat;
        }

        foreach (var obsolete in ObsoleteMaterialAssets)
        {
            var path = $"{MaterialDir}/{obsolete}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null && AssetDatabase.DeleteAsset(path))
                Debug.Log($"[PaintSprayMaterialSetup] Materiau de l'ancien spray supprime : {path}");
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    // =====================================================
    // IMPORTEUR FBX : REMAP + CLIP EN BOUCLE
    // =====================================================

    private static void ConfigureImporter(Dictionary<string, Material> materials)
    {
        if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        }
        if (importer == null)
        {
            Debug.LogError($"[PaintSprayMaterialSetup] Importeur du FBX introuvable : {FbxPath}");
            return;
        }

        // Remaps de l'ancien spray (noms de materiaux disparus du FBX) : purges
        foreach (var (identifier, _) in importer.GetExternalObjectMap())
        {
            if (identifier.type == typeof(Material) && !materials.ContainsKey(identifier.name))
                importer.RemoveRemap(identifier);
        }
        foreach (var (matName, mat) in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

        // Rig en Generic (Animator) : en Legacy, le clip jouerait une seule
        // fois via un composant Animation en ecrasant toute la hierarchie.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;

        // Clip "Scene" (nom de la scene Blender) -> "PaintSpray_Bubbles", en boucle.
        // Toujours reconstruit depuis les clips par defaut : la plage de frames
        // suit celle du FBX (v1 : 96 frames @ 24 fps, v2 : 121 frames @ 30 fps).
        var clips = importer.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].name = i == 0 ? ClipName : $"{ClipName}_{i}";
            clips[i].loopTime = true;
            // Pas de frame de bouclage ajoutee : la boucle est deja sans
            // couture (frame 96 -> 1), une frame dupliquee ferait un a-coup.
            clips[i].loop = false;
        }
        importer.clipAnimations = clips;

        importer.SaveAndReimport();
    }

    // Le FBX bake des courbes statiques pour TOUS les noeuds (90 objets).
    // Jouees telles quelles, elles ecraseraient chaque frame le pivot des
    // marqueurs de couleur et la racine (inertie). On ne garde que ce qui
    // bouge vraiment : les bulles et le blendshape de la houle du liquide.
    private static AnimationClip BuildFilteredClip()
    {
        var source = AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == ClipName);
        if (source == null)
        {
            Debug.LogWarning("[PaintSprayMaterialSetup] Clip " + ClipName + " introuvable dans le FBX.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(AnimDir))
            AssetDatabase.CreateFolder(AnimParentDir, "Animations");

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FilteredClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, FilteredClipPath);
        }

        clip.ClearCurves();
        clip.frameRate = source.frameRate;

        int kept = 0;
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            bool isBubble = binding.path.IndexOf("bubble_", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isBlendShape = binding.propertyName.StartsWith("blendShape.");
            if (!isBubble && !isBlendShape)
                continue;

            AnimationUtility.SetEditorCurve(clip, binding,
                AnimationUtility.GetEditorCurve(source, binding));
            kept++;
        }

        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        if (kept == 0)
            Debug.LogWarning("[PaintSprayMaterialSetup] Aucune courbe bulle/blendshape trouvee dans le clip.");
        return clip;
    }

    private static AnimatorController EnsureAnimatorController(AnimationClip clip)
    {
        if (clip == null)
            return null;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);

        var stateMachine = controller.layers[0].stateMachine;
        if (stateMachine.defaultState == null)
        {
            var state = stateMachine.AddState(ClipName);
            state.motion = clip;
            stateMachine.defaultState = state;
        }
        else
        {
            stateMachine.defaultState.motion = clip;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // =====================================================
    // INTEGRATION SCENE GARAGE
    // =====================================================

    private static bool WireScene(AnimatorController controller)
    {
        var plierRoot = GameObject.Find("Plier_Garage");
        if (plierRoot == null)
        {
            Debug.LogWarning("[PaintSprayMaterialSetup] Plier_Garage introuvable : ouvre la scene " +
                             "Garage puis relance Blockforge > Setup Paint Spray.");
            return false;
        }

        var inventory = Object.FindFirstObjectByType<GarageInventoryController>();
        var parent = plierRoot.transform.parent;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null)
        {
            Debug.LogError($"[PaintSprayMaterialSetup] FBX introuvable : {FbxPath}");
            return false;
        }

        // ---------- Etat des lieux ----------
        // Wrapper propre attendu : GameObject nu "PaintSpray_Garage" avec
        // l'instance FBX en enfant "PaintSpray_Model". Tout le reste (FBX
        // pose a la main, wrapper a moitie cable, doublons) est remplace,
        // en conservant la pose reglee par l'utilisateur.
        GameObject wrapper = null;
        bool poseCaptured = false;
        Transform poseParent = parent;
        Vector3 posePosition = plierRoot.transform.localPosition;
        Quaternion poseRotation = plierRoot.transform.localRotation;
        Vector3 poseScale = Vector3.one;

        var toDestroy = new List<GameObject>();
        foreach (var tr in SceneTransforms())
        {
            var go = tr.gameObject;
            bool fbxRoot = IsFbxInstanceRoot(go, prefab);

            if (go.name == WrapperName)
            {
                bool clean = !fbxRoot &&
                             PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.NotAPrefab &&
                             tr.Find(ModelName) != null;
                if (clean && wrapper == null)
                {
                    wrapper = go;
                    continue;
                }

                // Instance FBX nommee comme le wrapper = integration manuelle :
                // on recupere sa pose avant de la remplacer.
                if (fbxRoot && !poseCaptured)
                {
                    poseParent = tr.parent;
                    posePosition = tr.localPosition;
                    poseRotation = tr.localRotation;
                    poseScale = tr.localScale;
                    poseCaptured = true;
                }
                toDestroy.Add(go);
            }
        }

        foreach (var go in toDestroy)
        {
            if (go == null)
                continue;
            Debug.Log($"[PaintSprayMaterialSetup] Ancien '{go.name}' remplace par le wrapper propre" +
                      (poseCaptured ? " (pose conservee)." : "."));
            Object.DestroyImmediate(go);
        }

        bool created = wrapper == null;
        if (created)
        {
            wrapper = new GameObject(WrapperName);
            wrapper.transform.SetParent(poseCaptured ? poseParent : parent, false);
            wrapper.transform.localPosition = posePosition;
            wrapper.transform.localRotation = poseRotation;
            wrapper.transform.localScale = poseScale;
        }
        else
        {
            wrapper.SetActive(true); // reactive le temps du cablage
        }

        var modelTr = wrapper.transform.Find(ModelName);
        if (modelTr == null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
            model.name = ModelName;
            modelTr = model.transform;
            modelTr.localPosition = Vector3.zero;
            modelTr.localRotation = Quaternion.identity;
        }

        // Purge les instances parasites du FBX hors du wrapper (doublons)
        var strays = new List<GameObject>();
        foreach (var tr in SceneTransforms())
        {
            if (IsFbxInstanceRoot(tr.gameObject, prefab) && !tr.IsChildOf(wrapper.transform))
                strays.Add(tr.gameObject);
        }
        foreach (var stray in strays)
        {
            if (stray == null)
                continue;
            Debug.LogWarning($"[PaintSprayMaterialSetup] Instance parasite du spray supprimee : {stray.name}");
            Object.DestroyImmediate(stray);
        }

        // Un composant Animation legacy residuel bloquerait l'Animator
        if (modelTr.TryGetComponent<Animation>(out var legacyAnim))
            Object.DestroyImmediate(legacyAnim);

        // Calage sur la pince uniquement pour une creation "neuve"
        if (created && !poseCaptured)
            FitToPlier(wrapper, plierRoot);

        // ---------- Memes effets de mouvement que la pince ----------
        var sprayMove = GetOrAdd<PlierGarageController>(wrapper);
        if (plierRoot.TryGetComponent<PlierGarageController>(out var plierMove))
            EditorUtility.CopySerialized(plierMove, sprayMove);

        // ---------- Animator (bulles + houle du liquide) ----------
        var animator = GetOrAdd<Animator>(modelTr.gameObject);
        if (controller != null)
            animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // ---------- Molette de couleur ----------
        // Les deux palettes (gauche/droite) partagent l'axe Colour_Wheel ; les
        // deux marqueurs sont sous le meme pivot, donc une seule palette
        // suffit pour l'ordre angulaire et la couleur.
        var wheel = GetOrAdd<PaintSprayColorWheel>(wrapper);
        wheel.wheelCenter = FindDeep(modelTr, WheelCenterNode);
        wheel.needle = FindDeep(modelTr, NeedleNode);
        wheel.needleArrow = FindDeep(modelTr, NeedleArrowNode);

        var wedges = new List<Transform>();
        for (int i = 0; i < 12; i++)
        {
            var wedge = FindDeep(modelTr, string.Format(WedgeNodeFormat, i));
            if (wedge != null)
                wedges.Add(wedge);
        }
        wheel.wedges = wedges.ToArray();

        wheel.liquidRenderer = FindDeep(modelTr, LiquidNode)?.GetComponent<Renderer>();
        wheel.surfaceRenderer = null; // v2 : la houle est un blendshape du liquide lui-meme
        var bubbles = new List<Renderer>();
        for (int i = 0; i < 16; i++)
        {
            var bubble = FindDeep(modelTr, string.Format(BubbleNodeFormat, i));
            if (bubble == null)
                break;
            var r = bubble.GetComponent<Renderer>();
            if (r != null)
                bubbles.Add(r);
        }
        wheel.bubbleRenderers = bubbles.ToArray();
        wheel.inventory = inventory;

        if (wheel.wheelCenter == null || wheel.needle == null || wheel.needleArrow == null ||
            wedges.Count != 12 || wheel.liquidRenderer == null || bubbles.Count == 0)
        {
            Debug.LogWarning("[PaintSprayMaterialSetup] Noeuds du spray introuvables dans le FBX " +
                             $"(wheel={wheel.wheelCenter != null}, pivot={wheel.needle != null}, " +
                             $"marker={wheel.needleArrow != null}, swatches={wedges.Count}, " +
                             $"liquid={wheel.liquidRenderer != null}, bubbles={bubbles.Count}).");
        }

        // ---------- Switch pince/spray (1 = pince, 2 = spray) ----------
        // L'hote du switch doit rester actif quel que soit l'outil :
        // le parent commun, ou un GameObject dedie en secours.
        GameObject switcherHost;
        if (parent != null)
        {
            switcherHost = parent.gameObject;
        }
        else
        {
            switcherHost = GameObject.Find("GarageTools") ?? new GameObject("GarageTools");
        }
        var switcher = GetOrAdd<GarageToolSwitcher>(switcherHost);
        switcher.plierRoot = plierRoot;
        switcher.sprayRoot = wrapper;
        switcher.inventory = inventory;
        switcher.buildController = Object.FindFirstObjectByType<GarageBuildController>();
        if (switcher.buildController == null)
        {
            Debug.LogWarning("[PaintSprayMaterialSetup] GarageBuildController introuvable : la pose de " +
                             "blocs ne sera pas coupee en mode spray. Lance Setup Garage Inventory " +
                             "puis relance ce setup.");
        }

        // ---------- Fige le spray pendant l'inventaire et les popups ----------
        if (inventory != null)
        {
            // Ecarte les references cassees (composants du spray remplace)
            var toDisable = (inventory.disableWhileOpen ?? new MonoBehaviour[0])
                .Where(mb => mb != null)
                .ToList();
            foreach (var mb in new MonoBehaviour[] { sprayMove, wheel, switcher })
            {
                if (!toDisable.Contains(mb))
                    toDisable.Add(mb);
            }
            inventory.disableWhileOpen = toDisable.Distinct().ToArray();
            EditorUtility.SetDirty(inventory);

            // GarageSaveController partage le meme tableau : le remplacer
            // ici aussi, sinon les popups gardent l'ancienne liste.
            var saveController = Object.FindFirstObjectByType<GarageSaveController>();
            if (saveController != null)
            {
                saveController.disableWhileOpen = inventory.disableWhileOpen;
                EditorUtility.SetDirty(saveController);
            }
        }
        else
        {
            Debug.LogWarning("[PaintSprayMaterialSetup] GarageInventoryController introuvable : " +
                             "lance Setup Garage Inventory puis relance ce setup.");
        }

        // ---------- Etat par defaut : la pince ----------
        plierRoot.SetActive(true);
        wrapper.SetActive(false);

        EditorUtility.SetDirty(wrapper);
        EditorUtility.SetDirty(switcherHost);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return true;
    }

    // Aligne grossierement le spray sur l'encombrement de la pince
    // (taille max identique, centres confondus). Uniquement a la
    // creation : les ajustements manuels ulterieurs sont conserves.
    private static void FitToPlier(GameObject wrapper, GameObject plierRoot)
    {
        if (!TryGetBounds(plierRoot, out var plierBounds) ||
            !TryGetBounds(wrapper, out var sprayBounds))
            return;

        float plierSize = MaxDimension(plierBounds);
        float spraySize = MaxDimension(sprayBounds);
        if (spraySize > 1e-4f && plierSize > 1e-4f)
        {
            float scale = Mathf.Clamp(plierSize / spraySize, 0.05f, 20f);
            wrapper.transform.localScale *= scale;
        }

        if (TryGetBounds(wrapper, out sprayBounds))
            wrapper.transform.position += plierBounds.center - sprayBounds.center;
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return false;

        bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);
        return true;
    }

    private static float MaxDimension(Bounds b)
    {
        return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
    }

    // Tous les transforms de la scene active, objets inactifs compris
    private static IEnumerable<Transform> SceneTransforms()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr != null)
                    yield return tr;
            }
        }
    }

    // Racine d'une instance de prefab dont la source est notre FBX
    private static bool IsFbxInstanceRoot(GameObject go, GameObject fbxPrefab)
    {
        if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go)
            return false;

        var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
        return source != null && AssetDatabase.GetAssetPath(source) == AssetDatabase.GetAssetPath(fbxPrefab);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }
}
