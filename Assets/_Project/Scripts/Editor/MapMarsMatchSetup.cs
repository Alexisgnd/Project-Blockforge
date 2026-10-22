using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// SETUP DU MATCH CONTRE LES BOTS DANS MAP_MARS
// Menu : Blockforge > Setup Map MARS Match
// - Objet "TdmMatch" (TdmMatch : Team Deathmatch 5 v 5 contre les
//   bots) avec le catalogue des blocs (Data/Blocks) et le mode
//   par defaut (Data/GameModes/Mode_TeamDeathmatch) pour un Play
//   lance directement dans la scene
// - Camera principale : RobotFollowCamera (camera de match),
//   AudioListener ; reliee a TdmMatch
// Les points d'apparition sont les marqueurs Marker_Spawn_A_01..06
// et Marker_Spawn_B_01..06 de la carte (Map_Mars_v2.glb), lus au
// lancement : rien a poser a la main. Le NavMesh des bots est
// construit au lancement (MatchNavMesh).
// Idempotent. S'execute aussi une fois tout seul apres
// compilation (marqueur dans Library/) : Map_MARS est ouverte en
// additif si elle n'est pas la scene active, la scene courante
// n'est pas touchee.
// =========================================================

[InitializeOnLoad]
public static class MapMarsMatchSetup
{
    private const string ScenePath = BlockforgeScenes.MapMars;
    private const string BlockDataDir = "Assets/_Project/Data/Blocks";
    private const string ModePath = "Assets/_Project/Data/GameModes/Mode_TeamDeathmatch.asset";
    private const string AutoRunMarker = "Library/BlockforgeMapMarsMatch-v1.done";

    static MapMarsMatchSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        // Import ou compilation en cours : on retente au prochain tour de l'editeur
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoRunOnce;
            return;
        }
        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Setup Map MARS Match")]
    public static void SetupFromMenu()
    {
        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");
    }

    private static bool Setup()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"[MapMarsMatchSetup] Scene introuvable : {ScenePath}");
            return false;
        }

        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        // ---------- Camera ----------
        Camera camera = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            camera = root.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.CompareTag("MainCamera"))
                     ?? root.GetComponentInChildren<Camera>(true);
            if (camera != null)
                break;
        }
        if (camera == null)
        {
            var go = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            camera = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            Undo.RegisterCreatedObjectUndo(go, "Camera de match");
        }
        // Camera.main : la visee du joueur, les plaques de nom et le reticule allie la cherchent par ce tag
        camera.gameObject.tag = "MainCamera";
        if (!camera.TryGetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>(out _))
            camera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        if (!camera.TryGetComponent<AudioListener>(out _))
            camera.gameObject.AddComponent<AudioListener>();
        if (!camera.TryGetComponent<RobotFollowCamera>(out var follow))
            follow = camera.gameObject.AddComponent<RobotFollowCamera>();

        // ---------- Match ----------
        TdmMatch match = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            match = root.GetComponentInChildren<TdmMatch>(true);
            if (match != null)
                break;
        }
        if (match == null)
        {
            var go = new GameObject("TdmMatch");
            SceneManager.MoveGameObjectToScene(go, scene);
            match = go.AddComponent<TdmMatch>();
            Undo.RegisterCreatedObjectUndo(go, "TdmMatch");
        }
        match.blocks = AssetDatabase.FindAssets("t:BlockDefinition", new[] { BlockDataDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null)
            .OrderBy(d => d.name)
            .ToArray();
        match.defaultMode = AssetDatabase.LoadAssetAtPath<GameModeDefinition>(ModePath);
        match.followCamera = follow;
        match.cellSize = 1f;
        EditorUtility.SetDirty(match);
        EditorUtility.SetDirty(camera.gameObject);

        // Le mode par defaut garde ses regles a jour (champs ajoutes avec le match)
        if (match.defaultMode != null)
        {
            EditorUtility.SetDirty(match.defaultMode);
            AssetDatabase.SaveAssetIfDirty(match.defaultMode);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MapMarsMatchSetup] Map_MARS prete pour le match contre les bots : TdmMatch ({match.blocks.Length} blocs, " +
                  $"mode {(match.defaultMode != null ? match.defaultMode.name : "aucun")}), camera '{camera.name}' (RobotFollowCamera).");

        if (openedHere)
            EditorSceneManager.CloseScene(scene, true);
        return true;
    }
}
