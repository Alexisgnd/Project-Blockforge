using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

// =========================================================
// SETUP AUTOMATIQUE DE LA SCENE BOOT
// Menu : Blockforge > Setup Boot Auth Scene
// Cree le ThemeStyleSheet + PanelSettings si besoin, puis
// ajoute le GameObject BootAuthUI (UIDocument + controller)
// dans la scene Boot et enregistre les scenes du build.
// =========================================================

public static class BootAuthSceneSetup
{
    private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
    private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string UxmlPath = "Assets/_Project/UI/Boot/BootAuth.uxml";
    private const string ThemePath = "Assets/_Project/UI/UnityDefaultRuntimeTheme.tss";
    private const string PanelSettingsPath = "Assets/_Project/UI/BootPanelSettings.asset";

    [MenuItem("Blockforge/Setup Boot Auth Scene")]
    public static void Setup()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (uxml == null)
        {
            Debug.LogError($"[BootAuthSceneSetup] UXML introuvable : {UxmlPath}");
            return;
        }

        var panelSettings = GetOrCreatePanelSettings();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

        // Supprime une eventuelle installation precedente
        var existing = GameObject.Find("BootAuthUI");
        if (existing != null)
            Object.DestroyImmediate(existing);

        // Camera : la scene Boot n'affiche que l'UI, mais sans camera
        // Unity affiche "No cameras rendering" au milieu de l'ecran.
        if (Object.FindFirstObjectByType<Camera>() == null)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();
        }

        var go = new GameObject("BootAuthUI");
        var document = go.AddComponent<UIDocument>();
        document.visualTreeAsset = uxml;
        go.AddComponent<BootAuthController>();

        // Assignation via SerializedObject : la propriete panelSettings
        // ne persiste pas toujours quand elle est settee directement en edit mode.
        var serializedDocument = new SerializedObject(document);
        serializedDocument.FindProperty("m_PanelSettings").objectReferenceValue = panelSettings;
        serializedDocument.ApplyModifiedPropertiesWithoutUndo();

        // Supprime un eventuel composant "Panel Renderer" ajoute par megarde
        // via l'avertissement de migration de l'Inspector (inutile ici).
        foreach (var component in go.GetComponents<Component>())
        {
            if (component != null && component.GetType().Name == "PanelRenderer")
                Object.DestroyImmediate(component);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (document.panelSettings == null)
            Debug.LogWarning("[BootAuthSceneSetup] Le PanelSettings n'a pas pu etre assigne automatiquement. " +
                             "Assigne Assets/_Project/UI/BootPanelSettings.asset a la main sur le UIDocument.");

        AddScenesToBuildSettings();

        Debug.Log("[BootAuthSceneSetup] Scene Boot configuree. " +
                  "Renseigne l'URL PocketBase sur le composant BootAuthController.");
        Selection.activeGameObject = go;
    }

    private static PanelSettings GetOrCreatePanelSettings()
    {
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panelSettings != null)
            return panelSettings;

        var theme = GetOrCreateTheme();

        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.themeStyleSheet = theme;
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        panelSettings.match = 0.5f;

        AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
        AssetDatabase.SaveAssets();
        return panelSettings;
    }

    private static ThemeStyleSheet GetOrCreateTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
        if (theme != null)
            return theme;

        // Cherche un theme runtime deja present dans le projet
        string guid = AssetDatabase.FindAssets("t:ThemeStyleSheet").FirstOrDefault();
        if (!string.IsNullOrEmpty(guid))
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(guid));

        File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
        AssetDatabase.ImportAsset(ThemePath);
        return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
    }

    private static void AddScenesToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Boot doit etre la premiere scene du build
        scenes.RemoveAll(s => s.path == BootScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));

        if (scenes.All(s => s.path != MainMenuScenePath))
            scenes.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
