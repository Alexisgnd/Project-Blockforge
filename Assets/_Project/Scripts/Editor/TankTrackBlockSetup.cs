using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// AJOUT DES 4 CHENILLES ANIMEES (Bison -> Mammoth) A
// L'INVENTAIRE
// Menu : Blockforge > Setup Tank Track Blocks
// - Cree pour chaque chenille un AnimatorController
//   (Art/Animations/TankTrack_N*.controller) : etat Idle par
//   defaut, Roll_Forward / Roll_Reverse pilotes par le
//   parametre float "Roll" (> 0 avant, < 0 arriere), vitesse
//   multipliee par "RollSpeed"
// - Cree un prefab variant du FBX avec l'Animator cable
//   (Art/Prefabs/Blocks/TankTrack_N*.prefab) : c'est lui que
//   GarageInventorySetup prend comme previewPrefab
// - Verifie que le clip anime bien la hierarchie importee
//   (echantillonnage d'un maillon a t = 0 et t = 1 s)
// - Resynchronise les assets BlockDefinition sur la liste
//   canonique (Block_07_Chenilles placeholder remplace par
//   Block_TankTrack_N1..N4) et met a jour la liste "blocks"
//   du GarageInventoryController de la scene Garage
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class TankTrackBlockSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    private const string AnimParentDir = "Assets/_Project/Art";
    private const string AnimDir = "Assets/_Project/Art/Animations";
    private const string PrefabParentDir = "Assets/_Project/Art/Prefabs";
    private const string PrefabDir = "Assets/_Project/Art/Prefabs/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeTankTrackBlocks.done";
    private const string MaterialsMarker = "Library/BlockforgeTankTrackMaterials.done";

    // Parametres de l'Animator (a piloter par le gameplay)
    public const string RollParam = "Roll";           // -1..1 : sens et seuil (|Roll| > 0.05)
    public const string RollSpeedParam = "RollSpeed"; // multiplicateur de vitesse des clips (1 = 1,5 a 2,4 m/s)
    private const float RollThreshold = 0.05f;
    private const float TransitionSeconds = 0.15f;

    static TankTrackBlockSetup()
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

    [MenuItem("Blockforge/Setup Tank Track Blocks")]
    public static void SetupMenu()
    {
        Run();
    }

    // Point d'entree batch (Unity -batchmode -executeMethod TankTrackBlockSetup.BatchRunAll) :
    // materiaux + blocs en une passe, puis marqueurs poses pour ne pas rejouer a l'ouverture.
    public static void BatchRunAll()
    {
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    public static string PrefabPath(string fbxName) => $"{PrefabDir}/{fbxName}.prefab";
    public static string ControllerPath(string fbxName) => $"{AnimDir}/{fbxName}.controller";

    private static bool Run()
    {
        // L'ordre des [InitializeOnLoad] n'est pas garanti : les clips doivent etre
        // configures (Loop Time) avant de construire les controllers et de verifier.
        if (!File.Exists(MaterialsMarker))
        {
            TankTrackMaterialSetup.Setup();
            File.WriteAllText(MaterialsMarker, "done");
        }

        foreach (var fbxName in TankTrackMaterialSetup.FbxNames)
        {
            var controller = EnsureAnimatorController(fbxName);
            if (controller == null)
                continue;
            var prefab = EnsurePrefabVariant(fbxName, controller);
            VerifyAnimation(fbxName, prefab);
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
            Debug.Log($"[TankTrackBlockSetup] {blocks.Length} blocs relies a l'inventaire de la scene " +
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
            Debug.LogError("[TankTrackBlockSetup] GarageInventoryController introuvable dans la scene " +
                           "Garage. Lance d'abord : Blockforge > Setup Garage Inventory.");
            return true; // les assets sont a jour, inutile de retenter en boucle
        }

        controllerInv.blocks = blocks;
        EditorUtility.SetDirty(controllerInv);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TankTrackBlockSetup] {blocks.Length} blocs relies a l'inventaire ; scene Garage sauvegardee.");
        return true;
    }

    // =====================================================
    // ANIMATOR CONTROLLER : Idle <-> Roll_Forward / Roll_Reverse
    // =====================================================

    private static AnimationClip FindClip(string fbxName, string clipName)
    {
        return AssetDatabase.LoadAllAssetRepresentationsAtPath(TankTrackMaterialSetup.FbxPath(fbxName))
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName);
    }

    private static AnimatorController EnsureAnimatorController(string fbxName)
    {
        var forward = FindClip(fbxName, TankTrackMaterialSetup.ClipForward);
        var reverse = FindClip(fbxName, TankTrackMaterialSetup.ClipReverse);
        if (forward == null || reverse == null)
        {
            Debug.LogError($"[TankTrackBlockSetup] Clips {TankTrackMaterialSetup.ClipForward}/" +
                           $"{TankTrackMaterialSetup.ClipReverse} introuvables dans {fbxName}.fbx. " +
                           "Lance d'abord : Blockforge > Setup Tank Track Materials.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(AnimDir))
            AssetDatabase.CreateFolder(AnimParentDir, "Animations");

        var path = ControllerPath(fbxName);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Reconstruit toujours la machine a etats (setup relancable, GUID conserve)
        foreach (var p in controller.parameters.ToArray())
            controller.RemoveParameter(p);
        // "parameters" renvoie une copie : modifier puis reassigner le tableau
        controller.parameters = new[]
        {
            new AnimatorControllerParameter { name = RollParam, type = AnimatorControllerParameterType.Float, defaultFloat = 0f },
            new AnimatorControllerParameter { name = RollSpeedParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f },
        };

        var sm = controller.layers[0].stateMachine;
        foreach (var s in sm.states.ToArray())
            sm.RemoveState(s.state);

        var idle = sm.AddState("Idle", new Vector3(250, 0, 0));
        var rollForward = sm.AddState(TankTrackMaterialSetup.ClipForward, new Vector3(500, -80, 0));
        var rollReverse = sm.AddState(TankTrackMaterialSetup.ClipReverse, new Vector3(500, 80, 0));
        sm.defaultState = idle;

        foreach (var (state, clip) in new[] { (rollForward, forward), (rollReverse, reverse) })
        {
            state.motion = clip;
            state.speed = 1f;
            state.speedParameterActive = true;
            state.speedParameter = RollSpeedParam;
        }

        AddTransition(idle, rollForward, AnimatorConditionMode.Greater, RollThreshold);
        AddTransition(idle, rollReverse, AnimatorConditionMode.Less, -RollThreshold);
        AddTransition(rollForward, idle, AnimatorConditionMode.Less, RollThreshold);
        AddTransition(rollForward, rollReverse, AnimatorConditionMode.Less, -RollThreshold);
        AddTransition(rollReverse, idle, AnimatorConditionMode.Greater, -RollThreshold);
        AddTransition(rollReverse, rollForward, AnimatorConditionMode.Greater, RollThreshold);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = TransitionSeconds;
        t.AddCondition(mode, threshold, RollParam);
    }

    // =====================================================
    // PREFAB VARIANT DU FBX AVEC L'ANIMATOR CABLE
    // =====================================================

    private static GameObject EnsurePrefabVariant(string fbxName, AnimatorController controller)
    {
        if (!AssetDatabase.IsValidFolder(PrefabParentDir))
            AssetDatabase.CreateFolder(AnimParentDir, "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder(PrefabParentDir, "Blocks");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(TankTrackMaterialSetup.FbxPath(fbxName));
        if (model == null)
        {
            Debug.LogError($"[TankTrackBlockSetup] FBX introuvable : {TankTrackMaterialSetup.FbxPath(fbxName)}");
            return null;
        }

        var path = PrefabPath(fbxName);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // Les blocs poses sont mis a l'echelle par BlockPreviewFactory : la
            // sortie du frustum ne doit pas figer les chenilles d'un robot visible.
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);
            if (!success)
                Debug.LogError($"[TankTrackBlockSetup] Echec de creation du prefab {path}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // =====================================================
    // VERIFICATION : LE CLIP DEPLACE BIEN UN MAILLON
    // =====================================================

    private static void VerifyAnimation(string fbxName, GameObject prefab)
    {
        var clip = FindClip(fbxName, TankTrackMaterialSetup.ClipForward);
        if (prefab == null || clip == null)
            return;

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var link = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Link_00");
            var skinned = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (link == null || skinned == null)
            {
                Debug.LogError($"[TankTrackBlockSetup] {fbxName} : hierarchie inattendue (Link_00 ou SkinnedMeshRenderer absent).");
                return;
            }

            // Un tour de chenille dure 4 s : echantillonner a 1 s, pas a un multiple de 4.
            clip.SampleAnimation(go, 0f);
            var p0 = link.position;
            clip.SampleAnimation(go, 1f);
            var p1 = link.position;
            float moved = Vector3.Distance(p0, p1);
            int bindings = AnimationUtility.GetCurveBindings(clip).Length;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);

            string msg = $"[TankTrackBlockSetup] {fbxName} : clip {clip.name} {clip.length:0.##} s @ {clip.frameRate} fps, " +
                         $"{bindings} courbes, loop={settings.loopTime}, {skinned.bones.Length} os, " +
                         $"Link_00 deplace de {moved:0.###} m entre t=0 et t=1 s.";
            if (moved < 0.05f)
                Debug.LogError(msg + " Le clip n'anime pas la hierarchie !");
            else
                Debug.Log(msg);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
