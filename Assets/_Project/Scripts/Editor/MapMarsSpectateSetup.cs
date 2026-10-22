using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// SCENE DE TEST SPECTATEUR : MAP_MARS_SPECTATE
// Blockforge > Setup Map MARS Spectate : copie Map_MARS.unity en
// Map_MARS_Spectate.unity (toujours refaite a partir de Map_MARS :
// a relancer apres un remplacement de la carte), coche
// TdmMatch.spectatorOnly et enregistre la scene dans le build.
// Blockforge > Debug > Bots > Play spectator test : ouvre la scene
// (la cree au besoin) et lance le Play mode.
// =========================================================

public static class MapMarsSpectateSetup
{
    [MenuItem("Blockforge/Setup Map MARS Spectate")]
    public static bool Setup()
    {
        if (!File.Exists(BlockforgeScenes.MapMars))
        {
            Debug.LogError($"[MapMarsSpectate] Scene introuvable : {BlockforgeScenes.MapMars}");
            return false;
        }
        var active = SceneManager.GetActiveScene();
        if (active.path == BlockforgeScenes.MapMarsSpectate)
        {
            Debug.LogError("[MapMarsSpectate] Fermez Map_MARS_Spectate avant de la regenerer.");
            return false;
        }
        if (File.Exists(BlockforgeScenes.MapMarsSpectate))
            AssetDatabase.DeleteAsset(BlockforgeScenes.MapMarsSpectate);
        if (!AssetDatabase.CopyAsset(BlockforgeScenes.MapMars, BlockforgeScenes.MapMarsSpectate))
        {
            Debug.LogError("[MapMarsSpectate] Copie de Map_MARS impossible.");
            return false;
        }

        var scene = EditorSceneManager.OpenScene(BlockforgeScenes.MapMarsSpectate, OpenSceneMode.Additive);
        TdmMatch match = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            match = root.GetComponentInChildren<TdmMatch>(true);
            if (match != null)
                break;
        }
        if (match == null)
        {
            Debug.LogError("[MapMarsSpectate] Pas de TdmMatch dans Map_MARS : lancez d'abord Blockforge > Setup Map MARS Match.");
            EditorSceneManager.CloseScene(scene, true);
            return false;
        }
        match.spectatorOnly = true;
        EditorUtility.SetDirty(match);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        BlockforgeScenes.RegisterAllInBuildSettings();
        Debug.Log("[MapMarsSpectate] Map_MARS_Spectate prete : copie de Map_MARS, TdmMatch.spectatorOnly, scene enregistree dans le build.");
        return true;
    }

    [MenuItem("Blockforge/Debug/Bots/Play spectator test", false, 40)]
    public static void Play()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!File.Exists(BlockforgeScenes.MapMarsSpectate) && !Setup())
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(BlockforgeScenes.MapMarsSpectate, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
}
