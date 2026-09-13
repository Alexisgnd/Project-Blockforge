using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// AJOUT DES 6 LAMES DE SURVOL A MODELE 3D (Squall -> Hurricane)
// A L'INVENTAIRE
// Menu : Blockforge > Setup Hover Blade Blocks
// - Resynchronise les assets BlockDefinition sur la liste
//   canonique (les 6 Block_HoverBlade_N* recoivent leur FBX
//   + icone a la place du placeholder)
// - Met a jour la liste "blocks" du GarageInventoryController
//   de la scene Garage (l'inventaire, la pince et la
//   sauvegarde par blockId = nom d'asset suivent)
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class HoverBladeBlockSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    private const string AutoRunMarker = "Library/BlockforgeHoverBladeBlocks.done";
    private const string MaterialsMarker = "Library/BlockforgeHoverBladeMaterials.done";

    static HoverBladeBlockSetup()
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

    [MenuItem("Blockforge/Setup Hover Blade Blocks")]
    public static void SetupMenu()
    {
        Run();
    }

    // Point d'entree batch (Unity -batchmode -executeMethod HoverBladeBlockSetup.BatchRunAll) :
    // materiaux + blocs en une passe, puis marqueurs poses pour ne pas rejouer a l'ouverture.
    public static void BatchRunAll()
    {
        HoverBladeMaterialSetup.Setup();
        File.WriteAllText(MaterialsMarker, "done");
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    private static bool Run()
    {
        // L'ordre des [InitializeOnLoad] n'est pas garanti : les materiaux doivent
        // etre remappes sur les FBX avant de les relier aux blocs.
        if (!File.Exists(MaterialsMarker))
        {
            HoverBladeMaterialSetup.Setup();
            File.WriteAllText(MaterialsMarker, "done");
        }

        var blocks = GarageInventorySetup.EnsureBlocks();

        // Scene Garage deja ouverte : mise a jour en place, sans sauvegarde
        // forcee (l'utilisateur peut avoir des modifications en cours).
        var controller = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            controller.blocks = blocks;
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log($"[HoverBladeBlockSetup] {blocks.Length} blocs relies a l'inventaire de la scene " +
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
            Debug.LogError("[HoverBladeBlockSetup] GarageInventoryController introuvable dans la scene " +
                           "Garage. Lance d'abord : Blockforge > Setup Garage Inventory.");
            return true; // les assets sont a jour, inutile de retenter en boucle
        }

        controller.blocks = blocks;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[HoverBladeBlockSetup] {blocks.Length} blocs relies a l'inventaire ; scene Garage sauvegardee.");
        return true;
    }
}
