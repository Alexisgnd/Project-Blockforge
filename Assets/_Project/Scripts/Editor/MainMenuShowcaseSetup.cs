using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// SETUP DE LA VITRINE DU ROBOT (menu d'accueil)
// Menu : Blockforge > Setup MainMenu Robot Showcase
// Cree (ou met a jour) l'objet "RobotShowcase" au centre de
// la plateforme du hangar, branche la selection des slots
// et le catalogue des blocs, et ajoute le zoom molette
// (HangarCameraZoom) sur la camera du menu. Les reglages deja
// faits sur les composants (vitesse, tailles...) sont conserves.
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

        // ---------- Zoom molette sur la camera du menu ----------
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            if (!cam.TryGetComponent<HangarCameraZoom>(out var zoom))
                zoom = cam.gameObject.AddComponent<HangarCameraZoom>();
            zoom.showcase = showcase;
            zoom.stepPerNotch = 0.15f;
            zoom.minDistanceFactor = 1.9f;
            zoom.minDistance = 6f;
            zoom.aimTopFactor = 0.85f;
            var menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            zoom.homeScreen = menu != null ? menu.homeScreen : null;
            if (zoom.homeScreen == null)
                Debug.LogWarning("[MainMenuShowcaseSetup] Ecran Accueil introuvable : le zoom restera actif sur tous les ecrans.");
            EditorUtility.SetDirty(zoom);
        }
        else
        {
            Debug.LogWarning("[MainMenuShowcaseSetup] Aucune camera dans MainMenu : zoom molette non installe.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MainMenuShowcaseSetup] Vitrine {(created ? "creee" : "mise a jour")} en {go.transform.position} : " +
                  $"{showcase.blocks.Length} bloc(s) au catalogue, zoom molette {(cam != null ? "installe" : "absent")}. Scene sauvegardee.");
    }
}
