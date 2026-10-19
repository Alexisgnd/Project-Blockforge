using System;
using System.IO;
using System.Linq;
using Blockforge.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// =========================================================
// SETUP DES ECRANS DE FIN DE MATCH ET DU HUB
// - Blockforge > Setup Hub Screens : MainMenu passe a l'echelle
//   sur la hauteur (CanvasScaler match 1), polices mono 600 / 700
//   generees, donnees mock creees. Les ecrans Workshop / Store /
//   Tech Tree sont construits au lancement par MainMenuController
//   (rien a regenerer dans la scene).
// - Blockforge > Meta > Create Mock Data : Resources/Meta/
//   MockMetaData.asset (donnees des maquettes, editables).
// - Blockforge > Setup UIPreview Scene : scene vide avec le banc
//   d'essai UIPreview et les tailles de Game view 1920 x 1080,
//   2560 x 1080, 1280 x 800. La scene n'entre pas dans le build.
// =========================================================

public static class HubSetup
{
    public const string PreviewScene = "Assets/_Project/Scenes/UIPreview.unity";
    public const string MockDataPath = "Assets/_Project/Resources/Meta/MockMetaData.asset";

    [MenuItem("Blockforge/Setup Hub Screens")]
    public static void SetupHubScreens()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        DesignSystemApply.EnsureAssets();   // genere JetBrainsMono SemiBold / Bold SDF si absents
        CreateMockData();

        var scene = EditorSceneManager.OpenScene(BlockforgeScenes.MainMenu, OpenSceneMode.Single);
        int fixedScalers = 0;
        foreach (var scaler in UnityEngine.Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || Mathf.Approximately(scaler.matchWidthOrHeight, 1f))
                continue;
            Undo.RecordObject(scaler, "Hub Setup");
            scaler.referenceResolution = new Vector2(BlockforgeTheme.ReferenceWidth, BlockforgeTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;
            EditorUtility.SetDirty(scaler);
            fixedScalers++;
        }

        var nav = GameObject.Find("TopNavigation");
        string[] missing = nav == null ? new[] { "TopNavigation" }
            : TopBar.Sections.Where(s => nav.transform.Find("NavButtons/Nav_" + s.key) == null).Select(s => "Nav_" + s.key).ToArray();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (missing.Length > 0)
            Debug.LogWarning("[HubSetup] Header incomplet (" + string.Join(", ", missing) + ") : lance Blockforge > Setup MainMenu Header.");
        Debug.Log($"[HubSetup] MainMenu : {fixedScalers} CanvasScaler passe(s) a l'echelle sur la hauteur. " +
                  "Workshop, Store et Tech Tree sont construits au lancement sous le header.");
    }

    [MenuItem("Blockforge/Meta/Create Mock Data")]
    public static void CreateMockData()
    {
        if (AssetDatabase.LoadAssetAtPath<MockMetaData>(MockDataPath) != null)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(MockDataPath));
        AssetDatabase.Refresh();
        var data = ScriptableObject.CreateInstance<MockMetaData>();
        data.FillDefaults();
        AssetDatabase.CreateAsset(data, MockDataPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[HubSetup] Donnees mock creees : {MockDataPath}");
    }

    [MenuItem("Blockforge/Setup UIPreview Scene")]
    public static void SetupPreviewScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        DesignSystemApply.EnsureAssets();
        CreateMockData();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BlockforgeTheme.Hud.SurfaceBase;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        new GameObject("UIPreview", typeof(UIPreview));

        EditorSceneManager.SaveScene(scene, PreviewScene);
        AddGameViewSizes();
        Debug.Log($"[HubSetup] Scene {PreviewScene} creee. Play, puis 1-6 / 0 pour changer d'ecran, M pour ReduceMotion.");
    }

    // Tailles de Game view pour les verifications de cadrage (API interne : best effort)
    internal static void AddGameViewSizes() // aussi pour la scene LoaderPreview
    {
        try
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
            var texts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var kindType = asm.GetType("UnityEditor.GameViewSizeType");
            var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
            foreach (var (w, h, label) in new[] { (1920, 1080, "BF 16:9"), (2560, 1080, "BF 21:9"), (1280, 800, "BF 16:10") })
            {
                if (texts.Any(t => t.StartsWith(label, StringComparison.Ordinal)))
                    continue;
                var size = ctor.Invoke(new object[] { Enum.ToObject(kindType, 1), w, h, label });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            }
            sizesType.GetMethod("SaveToHDD")?.Invoke(instance, null);
        }
        catch (Exception e)
        {
            Debug.Log($"[HubSetup] Tailles de Game view non ajoutees ({e.GetType().Name}) : ajoute 1920x1080, 2560x1080 et 1280x800 a la main.");
        }
    }
}
