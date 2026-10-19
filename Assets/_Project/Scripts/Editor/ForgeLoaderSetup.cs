using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// =========================================================
// SETUP DU FORGE LOADER (one-shot + menus)
// Blockforge > Loader > Setup Forge Loader :
// - UI/Loader/LoaderPanelSettings.asset : 1920 x 1080, mise a
//   l'echelle sur la hauteur (match 1), ordre 1100 (au-dessus des
//   menus et des notices), theme runtime du projet ;
// - UI/Loader/Resources/Loader/LoaderTheme.asset (textes, courbe,
//   mock, cinematique) relie a ces PanelSettings ;
// - scene Boot : objet « BootSplash » (splash du lancement) ;
// - scene LoaderPreview (hors build) : banc d'essai.
// Les scenes sont ouvertes en additif, modifiees, sauvegardees puis
// refermees : la scene ouverte n'est pas touchee. Idempotent.
// Lance une fois apres compilation (marqueur Library/BlockforgeForgeLoader.done).
// Blockforge > Loader > Setup Loader Preview Scene : recree la scene d'essai.
// =========================================================

[InitializeOnLoad]
public static class ForgeLoaderSetup
{
    private const string Folder = "Assets/_Project/UI/Loader";
    private const string ResourcesFolder = Folder + "/Resources/Loader";
    private const string PanelSettingsPath = Folder + "/LoaderPanelSettings.asset";
    private const string ThemePath = ResourcesFolder + "/LoaderTheme.asset";
    private const string PreviewControlsPath = Folder + "/Preview/LoaderPreviewControls.uxml";
    private const string PreviewBackdropPath = Folder + "/Preview/LoaderPreviewBackdrop.uxml";
    private const string Marker = "Library/BlockforgeForgeLoader.done";
    private const int SortingOrder = 1100;

    static ForgeLoaderSetup()
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

    [MenuItem("Blockforge/Loader/Setup Forge Loader")]
    public static void SetupMenu() => Setup();

    public static bool Setup()
    {
        var panel = EnsurePanelSettings();
        var theme = EnsureTheme(panel);
        if (panel == null || theme == null)
            return false;

        bool boot = EnsureBootSplash();
        // La scene d'essai n'est creee qu'une fois ici ; le menu dedie la recree
        bool preview = File.Exists(BlockforgeScenes.LoaderPreview) || SetupPreviewScene();
        AssetDatabase.SaveAssets();
        Debug.Log($"[ForgeLoaderSetup] Panel settings + theme ok ; Boot : {(boot ? "BootSplash en place" : "scene introuvable")} ; " +
                  $"LoaderPreview : {(preview ? BlockforgeScenes.LoaderPreview : "non creee")}.");
        return true;
    }

    // ---------- Assets ----------

    private static PanelSettings EnsurePanelSettings()
    {
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panel == null)
        {
            Directory.CreateDirectory(Folder);
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
        }

        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1920, 1080);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match = 1f; // echelle sur la hauteur
        panel.sortingOrder = SortingOrder;
        if (panel.themeStyleSheet == null)
            panel.themeStyleSheet = FindRuntimeTheme();
        EditorUtility.SetDirty(panel);
        return panel;
    }

    private static ThemeStyleSheet FindRuntimeTheme()
    {
        string guid = AssetDatabase.FindAssets("t:ThemeStyleSheet").FirstOrDefault();
        return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static LoaderTheme EnsureTheme(PanelSettings panel)
    {
        var theme = AssetDatabase.LoadAssetAtPath<LoaderTheme>(ThemePath);
        if (theme == null)
        {
            Directory.CreateDirectory(ResourcesFolder);
            AssetDatabase.Refresh();
            theme = ScriptableObject.CreateInstance<LoaderTheme>();
            theme.ease = ForgeEase.CreateCurve();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }
        if (theme.panelSettings == null)
            theme.panelSettings = panel;
        EditorUtility.SetDirty(theme);
        return theme;
    }

    // ---------- Boot : splash du lancement ----------

    private static bool EnsureBootSplash()
    {
        if (!File.Exists(BlockforgeScenes.Boot))
            return false;

        var scene = SceneManager.GetSceneByPath(BlockforgeScenes.Boot);
        bool opened = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(BlockforgeScenes.Boot, OpenSceneMode.Additive);
            opened = true;
        }

        bool present = scene.GetRootGameObjects().Any(go => go.GetComponentInChildren<BootSplash>(true) != null);
        if (!present)
        {
            var go = new GameObject("BootSplash");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BootSplash>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (opened)
            EditorSceneManager.CloseScene(scene, true);
        return true;
    }

    // ---------- Scene d'essai ----------

    [MenuItem("Blockforge/Loader/Setup Loader Preview Scene")]
    public static void SetupPreviewSceneMenu()
    {
        EnsureTheme(EnsurePanelSettings());
        if (SetupPreviewScene())
            Debug.Log($"[ForgeLoaderSetup] Scene {BlockforgeScenes.LoaderPreview} prete : Play, puis le panneau en haut a droite.");
    }

    private static bool SetupPreviewScene()
    {
        var existing = SceneManager.GetSceneByPath(BlockforgeScenes.LoaderPreview);
        if (existing.isLoaded)
        {
            Debug.LogWarning("[ForgeLoaderSetup] LoaderPreview est ouverte : ferme-la pour la recreer.");
            return false;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(camGo, scene);
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BlockforgeTheme.SurfaceBase;

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(es, scene);

        var previewGo = new GameObject("LoaderPreview");
        SceneManager.MoveGameObjectToScene(previewGo, scene);
        var preview = previewGo.AddComponent<LoaderPreview>();
        preview.controlsTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PreviewControlsPath);
        preview.backdropTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PreviewBackdropPath);

        Directory.CreateDirectory(Path.GetDirectoryName(BlockforgeScenes.LoaderPreview));
        EditorSceneManager.SaveScene(scene, BlockforgeScenes.LoaderPreview);
        EditorSceneManager.CloseScene(scene, true);
        HubSetup.AddGameViewSizes();
        return true;
    }
}
