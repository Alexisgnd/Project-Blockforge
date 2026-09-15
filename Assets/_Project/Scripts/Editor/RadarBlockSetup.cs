using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// AJOUT DU RADAR ANIME (PANNEAUX REPLIABLES) A L'INVENTAIRE
// Menu : Blockforge > Setup Radar Block
// - Cree l'AnimatorController du radar
//   (Art/Animations/Radar.controller) : etat Idle par defaut
//   (pose importee = panneaux deplies), Fold / Unfold pilotes
//   par le parametre bool "Deployed" (true = deplie), chaque
//   clip joue une fois et tient sa derniere pose
// - Cree un prefab variant du FBX avec l'Animator cable
//   (Art/Prefabs/Blocks/Radar.prefab) : c'est lui que
//   GarageInventorySetup prend comme previewPrefab
// - Verifie que le clip Fold tourne bien une charniere de
//   ~90 degres entre son debut et sa fin
// - Resynchronise les assets BlockDefinition sur la liste
//   canonique (Block_19_Radar recoit son prefab + icone) et
//   met a jour la liste "blocks" du GarageInventoryController
//   de la scene Garage
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class RadarBlockSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    private const string AnimParentDir = "Assets/_Project/Art";
    private const string AnimDir = "Assets/_Project/Art/Animations";
    private const string PrefabParentDir = "Assets/_Project/Art/Prefabs";
    private const string PrefabDir = "Assets/_Project/Art/Prefabs/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeRadarBlocks.done";
    private const string MaterialsMarker = "Library/BlockforgeRadarMaterials.done";

    private const string FbxName = "Radar";
    private const string HingeName = "Left_Panel_Hinge";

    // Parametre de l'Animator (a piloter par le gameplay) : true = panneaux deplies
    public const string DeployedParam = "Deployed";
    private const float TransitionSeconds = 0.1f;

    static RadarBlockSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        // En Play mode les modifications de scene seraient perdues :
        // on retentera au prochain rechargement de domaine.
        if (File.Exists(AutoRunMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Setup Radar Block")]
    public static void SetupMenu()
    {
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    // Point d'entree batch (Unity -batchmode -executeMethod RadarBlockSetup.BatchRunAll) :
    // materiaux + bloc en une passe, puis marqueurs poses pour ne pas rejouer a l'ouverture.
    public static void BatchRunAll()
    {
        RadarMaterialSetup.Setup();
        File.WriteAllText(MaterialsMarker, "done");
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    public static string PrefabPath => $"{PrefabDir}/{FbxName}.prefab";
    public static string ControllerPath => $"{AnimDir}/{FbxName}.controller";

    private static bool Run()
    {
        // L'ordre des [InitializeOnLoad] n'est pas garanti : les clips doivent etre
        // configures avant de construire le controller et de verifier.
        if (!File.Exists(MaterialsMarker))
        {
            RadarMaterialSetup.Setup();
            File.WriteAllText(MaterialsMarker, "done");
        }

        var controller = EnsureAnimatorController();
        if (controller != null)
        {
            var prefab = EnsurePrefabVariant(controller);
            VerifyAnimation(prefab);
        }
        AssetDatabase.SaveAssets();

        var blocks = GarageInventorySetup.EnsureBlocks();

        // Scene Garage deja ouverte : mise a jour en place, sans sauvegarde
        // forcee (l'utilisateur peut avoir des modifications en cours).
        var controllerInv = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controllerInv != null)
        {
            controllerInv.blocks = blocks;
            EditorUtility.SetDirty(controllerInv);
            EditorSceneManager.MarkSceneDirty(controllerInv.gameObject.scene);
            Debug.Log($"[RadarBlockSetup] {blocks.Length} blocs relies a l'inventaire de la scene " +
                      "ouverte. Pense a sauvegarder la scene (Ctrl+S).");
            return true;
        }

        // Sinon : ouverture de la scene Garage, cablage puis sauvegarde.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        controllerInv = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controllerInv == null)
        {
            Debug.LogError("[RadarBlockSetup] GarageInventoryController introuvable dans la scene " +
                           "Garage. Lance d'abord : Blockforge > Setup Garage Inventory.");
            return true; // les assets sont a jour, inutile de retenter en boucle
        }

        controllerInv.blocks = blocks;
        EditorUtility.SetDirty(controllerInv);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[RadarBlockSetup] {blocks.Length} blocs relies a l'inventaire ; scene Garage sauvegardee.");
        return true;
    }

    // =====================================================
    // ANIMATOR CONTROLLER : Idle (deplie) <-> Fold / Unfold
    // =====================================================

    private static AnimationClip FindClip(string clipName)
    {
        return AssetDatabase.LoadAllAssetRepresentationsAtPath(RadarMaterialSetup.FbxPath(FbxName))
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName);
    }

    private static AnimatorController EnsureAnimatorController()
    {
        var fold = FindClip(RadarMaterialSetup.ClipFold);
        var unfold = FindClip(RadarMaterialSetup.ClipUnfold);
        if (fold == null || unfold == null)
        {
            Debug.LogError($"[RadarBlockSetup] Clips {RadarMaterialSetup.ClipFold}/{RadarMaterialSetup.ClipUnfold} " +
                           $"introuvables dans {FbxName}.fbx. Lance d'abord : Blockforge > Setup Radar Materials.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(AnimDir))
            AssetDatabase.CreateFolder(AnimParentDir, "Animations");

        var path = ControllerPath;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Reconstruit toujours la machine a etats (setup relancable, GUID conserve)
        foreach (var p in controller.parameters.ToArray())
            controller.RemoveParameter(p);
        // "parameters" renvoie une copie : modifier puis reassigner le tableau
        controller.parameters = new[]
        {
            new AnimatorControllerParameter { name = DeployedParam, type = AnimatorControllerParameterType.Bool, defaultBool = true },
        };

        var sm = controller.layers[0].stateMachine;
        foreach (var s in sm.states.ToArray())
            sm.RemoveState(s.state);

        // Idle sans motion : la pose importee du FBX (panneaux deplies) reste affichee.
        var idle = sm.AddState("Idle", new Vector3(250, 0, 0));
        var foldState = sm.AddState(RadarMaterialSetup.ClipFold, new Vector3(500, -80, 0));
        var unfoldState = sm.AddState(RadarMaterialSetup.ClipUnfold, new Vector3(500, 80, 0));
        sm.defaultState = idle;
        foldState.motion = fold;
        unfoldState.motion = unfold;

        // Les clips ne bouclent pas : l'etat tient sa derniere frame tant que le
        // parametre ne change pas.
        AddTransition(idle, foldState, AnimatorConditionMode.IfNot);
        AddTransition(foldState, unfoldState, AnimatorConditionMode.If);
        AddTransition(unfoldState, foldState, AnimatorConditionMode.IfNot);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = TransitionSeconds;
        t.AddCondition(mode, 0f, DeployedParam);
    }

    // =====================================================
    // PREFAB VARIANT DU FBX AVEC L'ANIMATOR CABLE
    // =====================================================

    private static GameObject EnsurePrefabVariant(AnimatorController controller)
    {
        if (!AssetDatabase.IsValidFolder(PrefabParentDir))
            AssetDatabase.CreateFolder(AnimParentDir, "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder(PrefabParentDir, "Blocks");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(RadarMaterialSetup.FbxPath(FbxName));
        if (model == null)
        {
            Debug.LogError($"[RadarBlockSetup] FBX introuvable : {RadarMaterialSetup.FbxPath(FbxName)}");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // Les blocs poses sont mis a l'echelle par BlockPreviewFactory : la
            // sortie du frustum ne doit pas figer les panneaux d'un robot visible.
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool success);
            if (!success)
                Debug.LogError($"[RadarBlockSetup] Echec de creation du prefab {PrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // =====================================================
    // VERIFICATION : LE CLIP FOLD TOURNE BIEN UNE CHARNIERE
    // =====================================================

    private static void VerifyAnimation(GameObject prefab)
    {
        var clip = FindClip(RadarMaterialSetup.ClipFold);
        if (prefab == null || clip == null)
            return;

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var hinge = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == HingeName);
            if (hinge == null)
            {
                Debug.LogError($"[RadarBlockSetup] {FbxName} : charniere {HingeName} introuvable dans la hierarchie importee.");
                return;
            }

            clip.SampleAnimation(go, 0f);
            var r0 = hinge.localRotation;
            clip.SampleAnimation(go, clip.length);
            var r1 = hinge.localRotation;
            float angle = Quaternion.Angle(r0, r1);
            int bindings = AnimationUtility.GetCurveBindings(clip).Length;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);

            string msg = $"[RadarBlockSetup] {FbxName} : clip {clip.name} {clip.length:0.##} s @ {clip.frameRate} fps, " +
                         $"{bindings} courbes, loop={settings.loopTime}, {HingeName} tourne de {angle:0.#} deg entre le debut et la fin.";
            if (angle < 60f)
                Debug.LogError(msg + " Le clip n'anime pas la charniere !");
            else
                Debug.Log(msg);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
