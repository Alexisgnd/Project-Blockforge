using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// AJOUT DES BLOCS AILERONS (Hawk -> Bat, 7 niveaux) A
// L'INVENTAIRE DU GARAGE
// Menu : Blockforge > Setup Rudder Blocks
// - Resynchronise les assets BlockDefinition sur la liste
//   canonique de GarageInventorySetup (famille "Ailerons")
// - Assigne a chaque aileron son FBX (previewPrefab) et son
//   icone rendue depuis Blender (Art/Textures/Icons)
// - Met a jour la liste "blocks" du GarageInventoryController
//   de la scene Garage
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class RudderBlockSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    private const string AutoRunMarker = "Library/BlockforgeRudderBlocks.done";

    static RudderBlockSetup()
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

    [MenuItem("Blockforge/Setup Rudder Blocks")]
    public static void SetupMenu()
    {
        Run();
    }

    // Point d'entree batch (Unity -batchmode -executeMethod RudderBlockSetup.BatchRunAll) :
    // materiaux + blocs en une passe, puis marqueurs poses pour ne pas rejouer a l'ouverture.
    public static void BatchRunAll()
    {
        RudderMaterialSetup.Setup();
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
        File.WriteAllText("Library/BlockforgeRudderMaterials.done", "done");
    }

    private static bool Run()
    {
        var blocks = GarageInventorySetup.EnsureBlocks();

        // Scene Garage deja ouverte : mise a jour en place, sans sauvegarde
        // forcee (l'utilisateur peut avoir des modifications en cours).
        var controller = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            controller.blocks = blocks;
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log($"[RudderBlockSetup] {blocks.Length} blocs relies a l'inventaire de la scene " +
                      "ouverte. Pense a sauvegarder la scene (Ctrl+S).");
            return true;
        }

        // Sinon : ouverture de la scene Garage, cablage puis sauvegarde.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        controller = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("[RudderBlockSetup] GarageInventoryController introuvable dans la scene " +
                           "Garage. Lance d'abord : Blockforge > Setup Garage Inventory.");
            return true; // les assets sont a jour, inutile de retenter en boucle
        }

        controller.blocks = blocks;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[RudderBlockSetup] {blocks.Length} blocs relies a l'inventaire ; scene Garage sauvegardee.");
        return true;
    }
}
