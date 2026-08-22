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
    private static readonly Dictionary<string, SprayMat> FbxMaterials = BuildMaterialTable();

    private static Dictionary<string, SprayMat> BuildMaterialTable()
    {
        var table = new Dictionary<string, SprayMat>
        {
            { "white_armor",  new SprayMat { AssetName = "Spray_WhiteArmor", BaseColor = new Color(0.807f, 0.823f, 0.855f), Metallic = 0.15f, Smoothness = 0.55f, EmissiveColor = Color.black } },
            { "steel",        new SprayMat { AssetName = "Spray_Steel",      BaseColor = new Color(0.323f, 0.366f, 0.418f), Metallic = 0.40f, Smoothness = 0.65f, EmissiveColor = Color.black } },
            { "dark_metal",   new SprayMat { AssetName = "Spray_DarkMetal",  BaseColor = new Color(0.024f, 0.030f, 0.040f), Metallic = 0.30f, Smoothness = 0.40f, EmissiveColor = Color.black } },
            { "grey_body",    new SprayMat { AssetName = "Spray_GreyBody",   BaseColor = new Color(0.423f, 0.456f, 0.503f), Metallic = 0.30f, Smoothness = 0.60f, EmissiveColor = Color.black } },
            { "cyan_glow",    new SprayMat { AssetName = "Spray_CyanGlow",   BaseColor = new Color(0.050f, 0.645f, 1.000f), Metallic = 0.10f, Smoothness = 0.70f, EmissiveColor = new Color(0.003f, 0.044f, 0.084f) } },
            { "glass",        new SprayMat { AssetName = "Spray_Glass",      BaseColor = new Color(0.738f, 0.815f, 0.863f, 0.28f), Metallic = 0f, Smoothness = 0.92f, EmissiveColor = Color.black, Transparent = true, SortPriority = 3, DoubleSided = true } },
            { "paint_liquid", new SprayMat { AssetName = "Spray_Liquid",     BaseColor = new Color(0.745f, 0.238f, 0.027f, 0.75f), Metallic = 0f, Smoothness = 0.75f, EmissiveColor = new Color(0.042f, 0.012f, 0f), Transparent = true, SortPriority = 1 } },
            { "bubble",       new SprayMat { AssetName = "Spray_Bubble",     BaseColor = new Color(1.000f, 0.694f, 0.352f, 0.85f), Metallic = 0f, Smoothness = 0.80f, EmissiveColor = new Color(0.069f, 0.023f, 0f), Transparent = true, SortPriority = 0 } },
        };

        // Les 12 teintes de la roue (memes valeurs que le .blend)
        var hues = new[]
        {
            new Color(0.925f, 0.075f, 0.075f), new Color(0.925f, 0.500f, 0.075f),
            new Color(0.925f, 0.925f, 0.075f), new Color(0.500f, 0.925f, 0.075f),
            new Color(0.075f, 0.925f, 0.075f), new Color(0.075f, 0.925f, 0.500f),
            new Color(0.075f, 0.925f, 0.925f), new Color(0.075f, 0.500f, 0.925f),
            new Color(0.075f, 0.075f, 0.925f), new Color(0.500f, 0.075f, 0.925f),
            new Color(0.925f, 0.075f, 0.925f), new Color(0.925f, 0.075f, 0.500f),
        };
        for (int i = 0; i < hues.Length; i++)
        {
            table[$"hue_{i}"] = new SprayMat
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

        foreach (var (matName, mat) in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

        // Rig en Generic (Animator) : en Legacy, le clip jouerait une seule
        // fois via un composant Animation en ecrasant toute la hierarchie.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;

        // Clip "Scene" (nom de la scene Blender) -> "PaintSpray_Bubbles", en boucle
        var clips = importer.clipAnimations is { Length: > 0 }
            ? importer.clipAnimations
            : importer.defaultClipAnimations;
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

    // Le FBX bake des courbes statiques pour TOUS les noeuds (58 objets).
    // Jouees telles quelles, elles ecraseraient chaque frame l'aiguille de
    // la molette et la racine (inertie). On ne garde que ce qui bouge
    // vraiment : les bulles et les blendshapes de la houle du liquide.
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
            bool isBubble = binding.path.Contains("bubble_");
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
        var wheel = GetOrAdd<PaintSprayColorWheel>(wrapper);
        wheel.wheelCenter = FindDeep(modelTr, "color_wheel");
        wheel.needle = FindDeep(modelTr, "needle");
        wheel.needleArrow = FindDeep(modelTr, "needle_arrow");

        var wedges = new List<Transform>();
        for (int i = 0; i < 12; i++)
        {
            var wedge = FindDeep(modelTr, $"wedge_{i}");
            if (wedge != null)
                wedges.Add(wedge);
        }
        wheel.wedges = wedges.ToArray();

        wheel.liquidRenderer = FindDeep(modelTr, "can_liquid")?.GetComponent<Renderer>();
        wheel.surfaceRenderer = FindDeep(modelTr, "liquid_surface")?.GetComponent<Renderer>();
        var bubbles = new List<Renderer>();
        for (int i = 0; i < 16; i++)
        {
            var bubble = FindDeep(modelTr, $"bubble_{i}");
            if (bubble == null)
                break;
            var r = bubble.GetComponent<Renderer>();
            if (r != null)
                bubbles.Add(r);
        }
        wheel.bubbleRenderers = bubbles.ToArray();
        wheel.inventory = inventory;

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
