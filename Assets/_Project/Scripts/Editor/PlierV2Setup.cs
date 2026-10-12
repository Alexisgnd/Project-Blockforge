using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// =========================================================
// SETUP DE LA PINCE V2 DU GARAGE
// Menu : Blockforge > Setup Plier V2
// - Modele : Art/Models/Plier_Garage_V2.fbx, genere par
//   Tools/Blender/plier_v2_pipeline.py (meme famille que le
//   pistolet v3). Les materiaux communs reprennent ceux du
//   pistolet (Spray_*) ; cellule de verre, lentille et
//   hologramme ont les leurs (Plier_*, Hologram)
// - Scene Garage : l'ancienne pince devient "Plier_Garage_Legacy"
//   (desactivee, conservee) ; un wrapper neuf "Plier_Garage"
//   prend sa place (meme parent, meme pose, memes effets de
//   mouvement : PlierGarageController copie), avec le modele
//   v2 en enfant, oriente tete droit devant comme le pistolet
// - Cable PlierHologram (bloc tenu en hologramme) et
//   PlierToolFX (tete a la molette, griffes, lumieres, recul),
//   le switch d'outil (touche 1), le systeme de pose et la
//   liste des scripts figes pendant l'inventaire
// Idempotent : relancable, le wrapper existant est reutilise.
// =========================================================

public static class PlierV2Setup
{
    private const string FbxPath = "Assets/_Project/Art/Models/Plier_Garage_V2.fbx";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Garage";
    private const string HologramMatPath = "Assets/_Project/Art/Materials/Garage/Hologram.mat";
    private const string WrapperName = "Plier_Garage";
    private const string LegacyName = "Plier_Garage_Legacy";
    private const string ModelName = "Plier_V2_Model";
    private const float GlowNits = 220f;   // exposition du garage (EV 11,5) : 800 nits eblouissent

    // Pose : l'outil tient dans la taille de l'ancienne pince ; l'hologramme (tete)
    // est place a un point fixe de l'ecran, l'outil legerement tourne vers le centre
    // (on voit son flanc droit et sa tete, pas seulement son dos)
    private const float PoseSize = 0.50f;                                 // x taille de l'ancienne pince
    private static readonly Vector2 HoloViewport = new(0.64f, 0.33f);     // position de l'hologramme a l'ecran
    private const float HoloDepth = 1.15f;                                // distance camera -> hologramme, x taille de l'ancienne pince
    private const float PoseYaw = -10f;                                   // degres, vers la gauche (le centre de l'ecran)
    private const float PosePitch = 4f;                                   // degres, tete legerement relevee

    // Materiau du FBX -> materiau du projet (les Spray_* sont crees par Setup Paint Spray)
    private static readonly Dictionary<string, string> SharedMaterials = new()
    {
        { "Ceramic_White", "Spray_CeramicWhite" },
        { "Graphite", "Spray_Graphite" },
        { "Rubber_Grip", "Spray_RubberGrip" },
        { "Brushed_Aluminium", "Spray_BrushedAluminium" },
        { "Accent_Red", "Spray_AccentRed" },
        { "Glow_Accent", "Spray_GlowAccent" },
    };

    [MenuItem("Blockforge/Setup Plier V2")]
    public static void Setup()
    {
        if (!BlockforgeScenes.OpenForSetup(BlockforgeScenes.Garage, out var scene))
            return;

        MakeBlockModelsReadable();
        var materials = CreateMaterials();
        if (!ConfigureImporter(materials))
            return;
        if (!WireScene())
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PlierV2Setup] Pince v2 installee dans la scene Garage (ancienne pince : Plier_Garage_Legacy, desactivee).");
    }

    // Les aretes de l'hologramme sont calculees sur les maillages des blocs :
    // leurs modeles doivent etre lisibles par script (Read/Write)
    private static void MakeBlockModelsReadable()
    {
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:BlockDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def == null || def.previewPrefab == null)
                continue;
            string path = AssetDatabase.GetAssetPath(def.previewPrefab);
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                changed++;
            }
        }
        if (changed > 0)
            Debug.Log($"[PlierV2Setup] {changed} modele(s) de blocs passes en Read/Write (aretes de l'hologramme).");
    }

    // =====================================================
    // MATERIAUX
    // =====================================================

    private static Dictionary<string, Material> CreateMaterials()
    {
        var result = new Dictionary<string, Material>();
        foreach (var (fbxName, asset) in SharedMaterials)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{asset}.mat");
            if (mat != null)
                result[fbxName] = mat;
            else
                Debug.LogWarning($"[PlierV2Setup] Materiau {asset} introuvable : lance d'abord Blockforge > Setup Paint Spray.");
        }

        var lit = Shader.Find("HDRP/Lit");
        var glass = GetOrCreate($"{MaterialDir}/Plier_CellGlass.mat", lit);
        glass.SetColor("_BaseColor", new Color(0.65f, 0.85f, 0.95f, 0.16f));
        glass.SetFloat("_Metallic", 0f);
        glass.SetFloat("_Smoothness", 0.9f);
        glass.SetColor("_EmissiveColor", Color.black);
        glass.SetFloat("_SurfaceType", 1f);
        glass.SetFloat("_BlendMode", 0f);
        glass.SetFloat("_TransparentSortPriority", 2f);
        glass.SetFloat("_DoubleSidedEnable", 1f);
        HDMaterial.ValidateMaterial(glass);
        EditorUtility.SetDirty(glass);
        result["Cell_Glass"] = glass;

        var lens = GetOrCreate($"{MaterialDir}/Plier_HoloLens.mat", lit);
        lens.SetColor("_BaseColor", new Color(0.45f, 0.95f, 1f));
        lens.SetFloat("_Metallic", 0f);
        lens.SetFloat("_Smoothness", 0.9f);
        lens.SetColor("_EmissiveColor", new Color(0.45f, 0.95f, 1f) * GlowNits * 2.5f);
        lens.SetFloat("_SurfaceType", 0f);
        HDMaterial.ValidateMaterial(lens);
        EditorUtility.SetDirty(lens);
        result["Holo_Lens"] = lens;

        var holoShader = Shader.Find("Blockforge/Hologram");
        if (holoShader == null)
            Debug.LogError("[PlierV2Setup] Shader Blockforge/Hologram introuvable (Art/Shaders/Hologram.shader).");
        else
        {
            var holo = GetOrCreate(HologramMatPath, holoShader);
            holo.SetColor("_Color", new Color(0.2f, 0.85f, 1f, 1f));
            EditorUtility.SetDirty(holo);
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static Material GetOrCreate(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    private static bool ConfigureImporter(Dictionary<string, Material> materials)
    {
        if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        }
        if (importer == null)
        {
            Debug.LogError($"[PlierV2Setup] FBX introuvable : {FbxPath}");
            return false;
        }

        foreach (var (identifier, _) in importer.GetExternalObjectMap())
        {
            if (identifier.type == typeof(Material) && !materials.ContainsKey(identifier.name))
                importer.RemoveRemap(identifier);
        }
        foreach (var (name, mat) in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);

        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.SaveAndReimport();
        return true;
    }

    // =====================================================
    // SCENE GARAGE
    // =====================================================

    private static bool WireScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null)
        {
            Debug.LogError($"[PlierV2Setup] FBX introuvable : {FbxPath}");
            return false;
        }

        // ---------- Ancienne pince -> Legacy (desactivee) ----------
        var legacy = FindInScene(LegacyName);
        var existing = FindInScene(WrapperName);
        if (existing != null && existing.GetComponent<PlierToolFX>() == null)
        {
            // Premiere installation : la pince v1 garde tout (scripts, rig) mais s'eteint
            legacy = existing;
            legacy.name = LegacyName;
            existing = null;
        }
        if (legacy == null)
        {
            Debug.LogError("[PlierV2Setup] Ancienne pince (Plier_Garage) introuvable : ouvre la scene Garage.");
            return false;
        }

        bool legacyWasActive = legacy.activeSelf;
        legacy.SetActive(true); // le temps de mesurer son encombrement
        var legacyRenderer = legacy.GetComponentInChildren<Renderer>(true);
        int toolLayer = legacyRenderer != null ? legacyRenderer.gameObject.layer : legacy.layer;
        BlockPreviewFactory.TryGetBounds(legacy, out var legacyBounds);
        float legacySize = Mathf.Max(legacyBounds.size.x, legacyBounds.size.y, legacyBounds.size.z);

        // ---------- Wrapper ----------
        var wrapper = existing;
        if (wrapper == null)
        {
            wrapper = new GameObject(WrapperName);
            Undo.RegisterCreatedObjectUndo(wrapper, "Plier V2");
            wrapper.transform.SetParent(legacy.transform.parent, false);
            wrapper.transform.SetLocalPositionAndRotation(legacy.transform.localPosition, legacy.transform.localRotation);
            wrapper.transform.localScale = Vector3.one;
            wrapper.transform.SetSiblingIndex(legacy.transform.GetSiblingIndex());
            wrapper.layer = toolLayer;
        }
        wrapper.SetActive(true);

        var modelTr = wrapper.transform.Find(ModelName);
        if (modelTr == null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
            model.name = ModelName;
            modelTr = model.transform;
        }
        SetLayerRecursive(modelTr, toolLayer);
        foreach (var r in modelTr.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // outil a l'ecran : pas d'ombre sur le hangar

        var head = FindDeep(modelTr, "Plier_Head");
        var anchor = FindDeep(modelTr, "Holo_Anchor");
        var top = FindDeep(modelTr, "Tool_Top");
        if (head == null || anchor == null || top == null)
        {
            Debug.LogError("[PlierV2Setup] Noeuds Plier_Head / Holo_Anchor / Tool_Top absents du FBX : relance l'export Blender.");
            return false;
        }

        // ---------- Pose : tete droit devant, haut de l'outil vers le haut ----------
        PoseModel(modelTr, head, anchor, top, legacyBounds, legacySize);

        // L'ancienne pince s'eteint (switch d'outil et inventaire pointent sur le wrapper)
        legacy.SetActive(false);
        _ = legacyWasActive;

        // ---------- Effets de mouvement (copies de la v1) ----------
        var move = GetOrAdd<PlierGarageController>(wrapper);
        if (legacy.TryGetComponent<PlierGarageController>(out var legacyMove))
            EditorUtility.CopySerialized(legacyMove, move);

        // ---------- Hologramme + effets ----------
        var inventory = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        var build = Object.FindAnyObjectByType<GarageBuildController>(FindObjectsInactive.Include);

        var hologram = GetOrAdd<PlierHologram>(wrapper);
        hologram.inventory = inventory;
        hologram.anchor = anchor;
        hologram.sizeMarker = FindDeep(modelTr, "Holo_Size");
        hologram.lens = FindDeep(modelTr, "Holo_Lens");
        hologram.topMarker = top;
        hologram.hologramMaterial = AssetDatabase.LoadAssetAtPath<Material>(HologramMatPath);

        var fx = GetOrAdd<PlierToolFX>(wrapper);
        fx.build = build;
        fx.hologram = hologram;
        fx.model = modelTr;
        fx.head = head;
        fx.holoAnchor = anchor;
        fx.jaws = Enumerable.Range(0, 3).Select(i => FindDeep(modelTr, $"Jaw_{i}")).Where(t => t != null).ToArray();
        fx.glows = modelTr.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name == "Head_Glow" || r.name.StartsWith("Status_Strip") || r.name.EndsWith("_Glow") || r.name.StartsWith("Rear_Led"))
            .ToArray();
        fx.cellCores = modelTr.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Cell_Core")).ToArray();
        fx.lens = FindDeep(modelTr, "Holo_Lens")?.GetComponent<Renderer>();
        fx.glowNits = GlowNits;
        fx.lensNits = GlowNits * 2.5f;

        // ---------- Systeme de pose ----------
        if (build != null)
        {
            Undo.RecordObject(build, "Plier V2");
            build.plierHologram = hologram;
            build.plierPreview = null; // apercu v1 (sur la pince eteinte)
            EditorUtility.SetDirty(build);
        }
        else
        {
            Debug.LogWarning("[PlierV2Setup] GarageBuildController introuvable : lance Setup Garage Inventory puis relance ce setup.");
        }

        // ---------- Switch d'outil (touche 1 = pince) ----------
        foreach (var switcher in Object.FindObjectsByType<GarageToolSwitcher>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(switcher, "Plier V2");
            switcher.plierRoot = wrapper;
            EditorUtility.SetDirty(switcher);
        }

        // ---------- Figes pendant l'inventaire et les popups ----------
        if (inventory != null)
        {
            var list = (inventory.disableWhileOpen ?? new MonoBehaviour[0])
                .Where(mb => mb != null && !mb.transform.IsChildOf(legacy.transform))
                .ToList();
            foreach (var mb in new MonoBehaviour[] { move, fx })
            {
                if (!list.Contains(mb))
                    list.Add(mb);
            }
            Undo.RecordObject(inventory, "Plier V2");
            inventory.disableWhileOpen = list.Distinct().ToArray();
            EditorUtility.SetDirty(inventory);

            var save = Object.FindAnyObjectByType<GarageSaveController>(FindObjectsInactive.Include);
            if (save != null)
            {
                Undo.RecordObject(save, "Plier V2");
                save.disableWhileOpen = inventory.disableWhileOpen;
                EditorUtility.SetDirty(save);
            }
        }

        EditorUtility.SetDirty(wrapper);
        return true;
    }

    // Tete vers l'avant (tournee de PoseYaw / PosePitch), Tool_Top vers le haut de la
    // camera ; taille relative a l'ancienne pince ; hologramme a HoloViewport
    private static void PoseModel(Transform model, Transform head, Transform anchor, Transform top,
                                  Bounds legacyBounds, float legacySize)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam == null)
            return;

        model.localScale = Vector3.one;
        model.localRotation = Quaternion.identity;
        model.localPosition = Vector3.zero;

        Vector3 forward = (anchor.position - head.position).normalized;
        Vector3 up = Vector3.ProjectOnPlane(top.position - head.position, forward).normalized;
        var current = Quaternion.LookRotation(forward, up);
        var view = cam.transform.rotation * Quaternion.Euler(-PosePitch, PoseYaw, 0f);
        model.rotation = view * Quaternion.Inverse(current) * model.rotation;

        if (legacySize > 1e-4f && BlockPreviewFactory.TryGetBounds(model.gameObject, out var b))
        {
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            model.localScale = Vector3.one * (legacySize * PoseSize / Mathf.Max(size, 1e-4f));
            Vector3 target = cam.ViewportToWorldPoint(new Vector3(HoloViewport.x, HoloViewport.y, legacySize * HoloDepth));
            model.position += target - anchor.position;
        }

        // Diagnostic de pose (distance de la partie la plus proche vs plan proche)
        if (BlockPreviewFactory.TryGetBounds(model.gameObject, out var posed))
        {
            float nearest = float.MaxValue;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var c = r.bounds.center;
                nearest = Mathf.Min(nearest, Vector3.Dot(c - cam.transform.position, cam.transform.forward) - r.bounds.extents.magnitude);
            }
            Debug.Log($"[PlierV2Setup] Pose : ancienne pince {legacySize:0.###} m (centre a {Vector3.Dot(legacyBounds.center - cam.transform.position, cam.transform.forward):0.###} m), " +
                      $"pince v2 {Mathf.Max(posed.size.x, posed.size.y, posed.size.z):0.###} m, piece la plus proche a {nearest:0.###} m, plan proche {cam.nearClipPlane:0.###} m.");
        }
    }

    // =====================================================
    // OUTILS
    // =====================================================

    private static GameObject FindInScene(string name)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == name)
                    return tr.gameObject;
            }
        }
        return null;
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

    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursive(child, layer);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent<T>(out var c) ? c : Undo.AddComponent<T>(go);
    }
}
