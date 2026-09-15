using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// RESYNCHRONISATION DES BLOCS SUR LA LISTE CANONIQUE
// Menu : Blockforge > Resync Block Definitions
// Rejoue GarageInventorySetup.EnsureBlocks() (empreintes,
// ancrages, stats, FBX, icones) et relie la liste "blocks"
// au GarageInventoryController de la scene Garage, sans
// regenerer l'UI de l'inventaire.
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/) : migration des empreintes de la
// tache 05 (echelle et pivot).
// =========================================================

[InitializeOnLoad]
public static class BlockFootprintSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    private const string AutoRunMarker = "Library/BlockforgeBlockFootprints.done";

    static BlockFootprintSetup()
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

    [MenuItem("Blockforge/Resync Block Definitions")]
    public static void ResyncMenu()
    {
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    // Point d'entree batch (Unity -batchmode -executeMethod BlockFootprintSetup.BatchRunAll)
    public static void BatchRunAll()
    {
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    private static bool Run()
    {
        var blocks = GarageInventorySetup.EnsureBlocks();

        int multi = 0;
        foreach (var b in blocks)
        {
            if (b.footprint != Vector3Int.one)
                multi++;
        }

        // Scene Garage deja ouverte : mise a jour en place, sans sauvegarde
        // forcee (l'utilisateur peut avoir des modifications en cours).
        var controllerInv = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controllerInv != null)
        {
            controllerInv.blocks = blocks;
            EditorUtility.SetDirty(controllerInv);
            EditorSceneManager.MarkSceneDirty(controllerInv.gameObject.scene);
            Debug.Log($"[BlockFootprintSetup] {blocks.Length} blocs resynchronises ({multi} multi-cases) et relies " +
                      "a l'inventaire de la scene ouverte. Pense a sauvegarder la scene (Ctrl+S).");
            return true;
        }

        // Sinon : ouverture de la scene Garage, cablage puis sauvegarde.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        controllerInv = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controllerInv == null)
        {
            Debug.LogError("[BlockFootprintSetup] GarageInventoryController introuvable dans la scene " +
                           "Garage. Lance d'abord : Blockforge > Setup Garage Inventory.");
            return true; // les assets sont a jour, inutile de retenter en boucle
        }

        controllerInv.blocks = blocks;
        EditorUtility.SetDirty(controllerInv);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BlockFootprintSetup] {blocks.Length} blocs resynchronises ({multi} multi-cases) ; " +
                  "scene Garage sauvegardee.");
        return true;
    }
}
