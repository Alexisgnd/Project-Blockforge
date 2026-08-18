using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// SETUP DE LA SCENE MAP_MARS
// Menu : Blockforge > Setup Map MARS Scene
// Instancie le modele Map_Mars.fbx (terrain + markers de
// spawn / points de capture) dans la scene Map_MARS, puis
// sauvegarde la scene.
// S'execute aussi une seule fois tout seul apres compilation
// si la scene Map_MARS est ouverte (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MapMarsSceneSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Map_MARS.unity";
    private const string FbxPath = "Assets/_Project/Art/Models/Map_Mars.fbx";
    // v2 : relance apres la correction orientation + materiaux du FBX
    private const string AutoRunMarker = "Library/BlockforgeMapMarsSetup-v2.done";

    static MapMarsSceneSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    // Lancement automatique one-shot, uniquement si Map_MARS est
    // deja la scene active (on ne change pas de scene dans le dos
    // de l'utilisateur).
    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker))
            return;

        if (SceneManager.GetActiveScene().path != ScenePath)
            return;

        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Setup Map MARS Scene")]
    public static void SetupFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Setup();
    }

    private static bool Setup()
    {
        var scene = SceneManager.GetActiveScene();

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null)
        {
            Debug.LogError($"[MapMarsSceneSetup] FBX introuvable : {FbxPath}");
            return false;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(root) == fbx)
            {
                Debug.Log("[MapMarsSceneSetup] Map_Mars est deja dans la scene, rien a faire.");
                return true;
            }
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, scene);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Undo.RegisterCreatedObjectUndo(instance, "Ajout Map_Mars");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MapMarsSceneSetup] Map_Mars.fbx instancie dans Map_MARS.unity, scene sauvegardee.");
        return true;
    }
}
