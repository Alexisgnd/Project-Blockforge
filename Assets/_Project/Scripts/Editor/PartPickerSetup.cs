using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// =========================================================
// SETUP DU PART PICKER (one-shot + menus)
// Blockforge > Part Picker > Setup Part Picker :
// - UI/PartPicker/PickerPanelSettings.asset : 1920 x 1080, mise a
//   l'echelle sur la hauteur (match 1), ordre 900 (au-dessus du HUD
//   du garage, sous les notices et le Forge Loader), theme runtime ;
// - UI/PartPicker/Resources/PartPicker/PickerTheme.asset (textes,
//   onglets, familles, tris, durees) relie a ces PanelSettings ;
// - UI/PartPicker/Resources/PartPicker/PickerMockCatalog.asset :
//   donnees simulees, icones et prefabs repris des BlockDefinition ;
// - scene PartPickerPreview (hors build) : scene vide + bootstrap
//   des mocks, pour juger la fenetre seule.
// Lance une fois apres compilation (marqueur
// Library/BlockforgePartPicker.done). La scene du garage n'est
// touchee que par le menu suivant.
// Blockforge > Part Picker > Install in Garage : objet « PartPicker »
// (GaragePartPicker) dans la scene garage, CanvasGroup sur
// GarageCanvas ; la scene est ouverte en additif puis sauvegardee.
// =========================================================

[InitializeOnLoad]
public static class PartPickerSetup
{
    private const string Folder = "Assets/_Project/UI/PartPicker";
    private const string ResourcesFolder = Folder + "/Resources/PartPicker";
    private const string PanelSettingsPath = Folder + "/PickerPanelSettings.asset";
    private const string ThemePath = ResourcesFolder + "/PickerTheme.asset";
    private const string MockPath = ResourcesFolder + "/PickerMockCatalog.asset";
    private const string BlockDataDir = "Assets/_Project/Data/Blocks";
    public const string PreviewScenePath = "Assets/_Project/Scenes/PartPickerPreview.unity";
    private const string Marker = "Library/BlockforgePartPicker.done";

    static PartPickerSetup()
    {
        if (File.Exists(Marker))
            return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Marker))
                return;
            if (Setup())
                File.WriteAllText(Marker, System.DateTime.Now.ToString("o"));
        };
    }

    [MenuItem("Blockforge/Part Picker/Setup Part Picker")]
    public static void SetupMenu()
    {
        if (Setup())
            Debug.Log($"[PartPickerSetup] Assets prets, scene d'essai {PreviewScenePath}.");
    }

    public static bool Setup()
    {
        var panel = EnsurePanelSettings();
        var theme = EnsureTheme(panel);
        var mock = EnsureMockCatalog();
        if (panel == null || theme == null || mock == null)
            return false;
        SetupPreviewScene();
        AssetDatabase.SaveAssets();
        return true;
    }

    // ---------- Assets ----------

    private static PanelSettings EnsurePanelSettings()
    {
        Directory.CreateDirectory(Folder);
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panel == null)
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
        }
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1920, 1080);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match = 1f; // echelle sur la hauteur
        panel.sortingOrder = 900;
        if (panel.themeStyleSheet == null)
        {
            string guid = AssetDatabase.FindAssets("t:ThemeStyleSheet").FirstOrDefault();
            if (!string.IsNullOrEmpty(guid))
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
        }
        EditorUtility.SetDirty(panel);
        return panel;
    }

    private static PickerTheme EnsureTheme(PanelSettings panel)
    {
        Directory.CreateDirectory(ResourcesFolder);
        var theme = AssetDatabase.LoadAssetAtPath<PickerTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<PickerTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }
        theme.panelSettings = panel;
        EditorUtility.SetDirty(theme);
        return theme;
    }

    // Donnees simulees : regenerees a chaque setup (icones et prefabs des blocs du projet)
    private static PickerMockCatalog EnsureMockCatalog()
    {
        Directory.CreateDirectory(ResourcesFolder);
        var mock = AssetDatabase.LoadAssetAtPath<PickerMockCatalog>(MockPath);
        if (mock == null && File.Exists(MockPath))
            AssetDatabase.DeleteAsset(MockPath); // asset sans script valide : recree
        if (mock == null)
        {
            mock = ScriptableObject.CreateInstance<PickerMockCatalog>();
            AssetDatabase.CreateAsset(mock, MockPath);
        }
        mock.owned = PickerMockCatalog.DefaultParts();
        int linked = 0;
        foreach (var entry in mock.owned)
        {
            if (string.IsNullOrEmpty(entry.sourceBlockId))
                continue;
            var def = AssetDatabase.LoadAssetAtPath<BlockDefinition>($"{BlockDataDir}/{entry.sourceBlockId}.asset");
            if (def == null)
                continue;
            entry.part.thumbnail = def.icon;
            entry.part.previewPrefab = def.previewPrefab;
            linked++;
        }
        EditorUtility.SetDirty(mock);
        Debug.Log($"[PartPickerSetup] Catalogue simule : {mock.owned.Count} pieces, {linked} avec icone et modele du projet.");
        return mock;
    }

    // ---------- Scene d'essai (hors build) ----------

    [MenuItem("Blockforge/Part Picker/Setup Preview Scene")]
    public static void SetupPreviewSceneMenu()
    {
        EnsureTheme(EnsurePanelSettings());
        EnsureMockCatalog();
        if (SetupPreviewScene())
            Debug.Log($"[PartPickerSetup] Scene {PreviewScenePath} prete : Play (Q ouvre le picker).");
    }

    private static bool SetupPreviewScene()
    {
        var existing = SceneManager.GetSceneByPath(PreviewScenePath);
        if (existing.isLoaded)
        {
            Debug.LogWarning("[PartPickerSetup] PartPickerPreview est ouverte : ferme-la pour la recreer.");
            return false;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var go = new GameObject("PartPicker");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<PartPickerMockBootstrap>();

        Directory.CreateDirectory(Path.GetDirectoryName(PreviewScenePath));
        EditorSceneManager.SaveScene(scene, PreviewScenePath);
        EditorSceneManager.CloseScene(scene, true);
        HubSetup.AddGameViewSizes();
        return true;
    }

    // ---------- Garage ----------

    [MenuItem("Blockforge/Part Picker/Install in Garage")]
    public static void InstallInGarageMenu()
    {
        EnsureTheme(EnsurePanelSettings());
        if (InstallInGarage())
            Debug.Log("[PartPickerSetup] Part picker installe dans le garage.");
    }

    public static bool InstallInGarage()
    {
        string path = BlockforgeScenes.ResolveGarageScenePath();
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            opened = true;
        }

        var roots = scene.GetRootGameObjects();
        T Find<T>() where T : Component =>
            roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

        var inventory = Find<GarageInventoryController>();
        if (inventory == null)
        {
            Debug.LogError($"[PartPickerSetup] Pas de GarageInventoryController dans {path} : lance d'abord Setup Garage Inventory.");
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            return false;
        }

        var bridge = Find<GaragePartPicker>();
        if (bridge == null)
        {
            var go = new GameObject("PartPicker");
            SceneManager.MoveGameObjectToScene(go, scene);
            bridge = go.AddComponent<GaragePartPicker>();
        }
        bridge.inventory = inventory;
        bridge.build = Find<GarageBuildController>();
        bridge.statsHud = Find<GarageStatsHUD>();

        var canvas = roots.FirstOrDefault(r => r.name == "GarageCanvas");
        if (canvas != null)
        {
            if (!canvas.TryGetComponent<CanvasGroup>(out var group))
                group = canvas.AddComponent<CanvasGroup>();
            bridge.builderHud = group;
        }

        EditorUtility.SetDirty(bridge);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened)
            EditorSceneManager.CloseScene(scene, true);
        return true;
    }
}
