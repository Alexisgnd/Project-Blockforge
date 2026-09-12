using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// AJOUT DES 6 ROUES A MODELE 3D (Scout -> Monster) A
// L'INVENTAIRE
// Menu : Blockforge > Setup Wheel Blocks
// - Resynchronise les assets BlockDefinition sur la liste
//   canonique (les roues N1-N6 recoivent leur FBX + icone)
// - Met a jour la liste "blocks" du GarageInventoryController
//   de la scene Garage
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class WheelBlockSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    // v2 : ajout du Scout (N1) apres le premier passage
    private const string AutoRunMarker = "Library/BlockforgeWheelBlocks-v2.done";

    static WheelBlockSetup()
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

    [MenuItem("Blockforge/Setup Wheel Blocks")]
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
            Debug.Log($"[WheelBlockSetup] {blocks.Length} blocs relies a l'inventaire de la scene " +
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
            Debug.LogError("[WheelBlockSetup] GarageInventoryController introuvable dans la scene " +
                           "Garage. Lance d'abord : Blockforge > Setup Garage Inventory.");
            return true; // les assets sont a jour, inutile de retenter en boucle
        }

        controller.blocks = blocks;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[WheelBlockSetup] {blocks.Length} blocs relies a l'inventaire ; scene Garage sauvegardee.");
        return true;
    }
}
