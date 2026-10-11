using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// SETUP DE LA VITRINE DU ROBOT (menu d'accueil)
// Menu : Blockforge > Setup MainMenu Robot Showcase
// Cree (ou met a jour) l'objet "RobotShowcase" au centre de
// la plateforme du hangar, branche la selection des slots
// et le catalogue des blocs. Les reglages deja faits sur le
// composant (vitesse, tailles...) sont conserves.
// =========================================================

public static class MainMenuShowcaseSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string BlockDataDir = "Assets/_Project/Data/Blocks";
    private const string ShowcaseName = "RobotShowcase";

    // Dessus de la plateforme centrale du hangar (Platform_Top du FBX : y 0.15 +- 0.05)
    private static readonly Vector3 PlatformTop = new(0f, 0.2f, 0f);

    [MenuItem("Blockforge/Setup MainMenu Robot Showcase")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var slotManager = Object.FindAnyObjectByType<RobotSlotManager>(FindObjectsInactive.Include);
        if (slotManager == null)
        {
            Debug.LogError("[MainMenuShowcaseSetup] Aucun RobotSlotManager dans MainMenu : " +
                           "lance d'abord Blockforge > Setup MainMenu UI.");
            return;
        }

        var go = GameObject.Find(ShowcaseName);
        bool created = go == null;
        if (created)
        {
            go = new GameObject(ShowcaseName);
            go.transform.SetPositionAndRotation(PlatformTop, Quaternion.identity);
        }

        if (!go.TryGetComponent<HangarRobotShowcase>(out var showcase))
            showcase = go.AddComponent<HangarRobotShowcase>();

        showcase.slotManager = slotManager;
        showcase.blocks = AssetDatabase.FindAssets("t:BlockDefinition", new[] { BlockDataDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(block => block != null)
            .OrderBy(block => block.name)
            .ToArray();

        EditorUtility.SetDirty(showcase);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MainMenuShowcaseSetup] Vitrine {(created ? "creee" : "mise a jour")} en {go.transform.position} : " +
                  $"{showcase.blocks.Length} bloc(s) au catalogue. Scene sauvegardee.");
    }
}
