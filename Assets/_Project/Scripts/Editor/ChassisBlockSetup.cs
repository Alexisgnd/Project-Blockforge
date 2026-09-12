using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// AJOUT DES 17 BLOCS CHASSIS A L'INVENTAIRE
// Menu : Blockforge > Setup Chassis Blocks
// - Resynchronise les assets BlockDefinition sur la liste
//   canonique (les 8 anciens chassis placeholder deviennent
//   17 blocs a vrai modele ; Block_05_Tige est supprime)
// - Assigne a chaque bloc son FBX (previewPrefab) et son
//   icone rendue depuis Blender (Art/Textures/Icons)
// - Met a jour la liste "blocks" du GarageInventoryController
//   de la scene Garage
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class ChassisBlockSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    private const string AutoRunMarker = "Library/BlockforgeChassisBlocks.done";

    static ChassisBlockSetup()
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

    [MenuItem("Blockforge/Setup Chassis Blocks")]
    public static void SetupMenu()
    {
        Run();
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
            Debug.Log($"[ChassisBlockSetup] {blocks.Length} blocs relies a l'inventaire de la scene " +
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
            Debug.LogError("[ChassisBlockSetup] GarageInventoryController introuvable dans la scene " +
                           "Garage. Lance d'abord : Blockforge > Setup Garage Inventory.");
            return true; // les assets sont a jour, inutile de retenter en boucle
        }

        controller.blocks = blocks;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ChassisBlockSetup] {blocks.Length} blocs relies a l'inventaire ; scene Garage sauvegardee.");
        return true;
    }
}
