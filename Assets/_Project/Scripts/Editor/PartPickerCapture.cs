using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

// =========================================================
// CAPTURES DU PART PICKER (verification de mise en page)
// Blockforge > Part Picker > Debug > Capture Screens (aussi via
// BlockforgeBatch) : lance le Play mode sur la scene
// PartPickerPreview sans fermer la scene ouverte
// (EditorSceneManager.playModeStartScene, remis a zero a la sortie).
// Au demarrage, l'outil rend le
// panneau du picker dans une texture aux trois tailles de
// l'acceptation (1920 x 1080, 2560 x 1080, 1280 x 800) et dans
// plusieurs etats (toutes les pieces, armes avec reserve pleine,
// recherche « turret », recherche vide « plasmo », focus clavier),
// ecrit les PNG dans Library/PartPickerCapture/ puis quitte le
// Play mode. Menu : Blockforge > Part Picker > Debug > Capture.
// Aucun effet sur le jeu ; le PanelSettings du projet n'est pas
// modifie (copie le temps de la capture).
// =========================================================

[InitializeOnLoad]
public static class PartPickerCapture
{
    private const string RequestPath = "Library/PartPickerCapture.request";
    private const string RestorePath = "Library/PartPickerCapture.restore";
    private const string BackgroundFlag = "Library/BlockforgeRun.background";
    private const string OutputDir = "Library/PartPickerCapture";

    private static readonly Queue<Action> steps = new();
    private static int waitFrames;
    private static UIDocument document;
    private static PanelSettings original, clone;
    private static RenderTexture target;
    private static PartPickerController picker;
    private static readonly List<string> report = new();

    static PartPickerCapture()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.EnteredPlayMode && File.Exists(RequestPath))
            {
                string mode = File.ReadAllText(RequestPath).Trim();
                File.Delete(RequestPath);
                if (mode == "garage") BeginGarage();
                else Begin();
            }
            else if (change == PlayModeStateChange.EnteredEditMode && File.Exists(RestorePath))
            {
                File.Delete(RestorePath);
                EditorSceneManager.playModeStartScene = null;
            }
        };
    }

    [MenuItem("Blockforge/Part Picker/Debug/Capture Screens")]
    public static void CaptureScreens()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PartPickerSetup.PreviewScenePath);
        if (scene == null && PartPickerSetup.Setup())
            scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PartPickerSetup.PreviewScenePath);
        if (scene == null)
        {
            Debug.LogError("[PartPickerCapture] Scene PartPickerPreview introuvable.");
            return;
        }
        StartPlay(scene, "preview");
    }

    // Garage : le pont GaragePartPicker (installe par Install in Garage) ouvre le picker,
    // choisit une arme, ferme ; le rapport note HUD, curseur, pince et composants coupes
    [MenuItem("Blockforge/Part Picker/Debug/Smoke Test Garage")]
    public static void SmokeTestGarage()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BlockforgeScenes.Garage);
        if (scene == null)
        {
            Debug.LogError("[PartPickerCapture] Scene Garage introuvable.");
            return;
        }
        StartPlay(scene, "garage");
    }

    private static void StartPlay(SceneAsset scene, string mode)
    {
        File.WriteAllText(RequestPath, mode);
        File.WriteAllText(RestorePath, "restore");
        File.WriteAllText(BackgroundFlag, "capture"); // le jeu tourne sans le focus de l'editeur
        EditorSceneManager.playModeStartScene = scene;
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Blockforge/Part Picker/Debug/Capture (Play mode)")]
    public static void Begin()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PartPickerCapture] A lancer en Play mode (scene PartPickerPreview).");
            return;
        }
        steps.Clear();
        report.Clear();
        prepareTries = 0;
        waitFrames = 30; // bootstrap, polices, premiere mise en page
        Directory.CreateDirectory(OutputDir);

        steps.Enqueue(Prepare);
        foreach (var (w, h) in new[] { (1920, 1080), (2560, 1080), (1280, 800) })
        {
            int width = w, height = h;
            steps.Enqueue(() => Resize(width, height));
            steps.Enqueue(() => { picker.SelectTab(0); picker.SelectPart("mock_cube"); });
            steps.Enqueue(() => Shot($"all_{width}x{height}"));
        }
        steps.Enqueue(() => Resize(1920, 1080));
        steps.Enqueue(() => { picker.SelectTab(3); picker.SelectPart("mock_bombarder"); });
        steps.Enqueue(() => Shot("weapons_full_1920x1080"));
        steps.Enqueue(() => { picker.SelectTab(0); picker.RunSearch("turret"); });
        steps.Enqueue(() => picker.SelectPart("mock_flak_turret"));
        steps.Enqueue(() => Shot("search_turret_1920x1080"));
        steps.Enqueue(() => picker.RunSearch("plasmo"));
        steps.Enqueue(() => Shot("search_empty_1920x1080"));
        steps.Enqueue(() => { picker.ClearSearch(); picker.SelectTab(1); FocusRing(); });
        steps.Enqueue(() => Shot("blocks_focus_1920x1080"));
        steps.Enqueue(Finish);

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // ---------- Garage ----------

    private static GaragePartPicker bridge;
    private static string blockBefore;

    private static void BeginGarage()
    {
        steps.Clear();
        report.Clear();
        prepareTries = 0;
        waitFrames = 60;
        Directory.CreateDirectory(OutputDir);
        steps.Enqueue(PrepareGarage);
        steps.Enqueue(() =>
        {
            blockBefore = bridge.inventory.CurrentBlock != null ? bridge.inventory.CurrentBlock.name : "-";
            report.Add($"avant : outil {blockBefore}, curseur {UnityEngine.Cursor.lockState}, feuille uGUI active {bridge.inventory.enabled}, HUD alpha {Alpha()}");
            bridge.Open();
        });
        steps.Enqueue(() =>
        {
            report.Add($"ouvert : picker {picker.IsOpen}, HUD alpha {Alpha()}, curseur {UnityEngine.Cursor.lockState}, " +
                       $"joueur actif {Enabled<PlayerGarageController>()}, sauvegarde active {Enabled<GarageSaveController>()}");
            Resize(1920, 1080);
        });
        steps.Enqueue(() => { picker.SelectTab(3); });
        steps.Enqueue(() => report.Add("selection Laser Wasp : " + picker.SelectPart("Block_Laser_N1_Wasp")));
        steps.Enqueue(() =>
        {
            report.Add($"outil de la pince : {(bridge.inventory.CurrentBlock != null ? bridge.inventory.CurrentBlock.name : "-")}");
            Shot("garage_weapons_1920x1080");
        });
        steps.Enqueue(() => { picker.SelectTab(0); });
        steps.Enqueue(() => Shot("garage_all_1920x1080"));
        steps.Enqueue(() => { RestorePanel(); picker.Close(); });
        steps.Enqueue(() => { waitFrames = 40; });
        steps.Enqueue(() =>
        {
            report.Add($"ferme : picker {picker.IsOpen}, HUD alpha {Alpha()}, curseur {UnityEngine.Cursor.lockState}, " +
                       $"joueur actif {Enabled<PlayerGarageController>()}, sauvegarde active {Enabled<GarageSaveController>()}, " +
                       $"feuille uGUI active {bridge.inventory.enabled}");
        });
        steps.Enqueue(Finish);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void PrepareGarage()
    {
        bridge = UnityEngine.Object.FindAnyObjectByType<GaragePartPicker>();
        picker = bridge != null ? bridge.Picker : null;
        // Play lance sur le Garage sans session : AuthGuard renvoie vers Boot. On continue
        // hors ligne (meme chemin que « Play offline ») puis on recharge le garage.
        if (picker == null && !SessionService.HasIdentity &&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == AuthGuard.BootSceneName)
        {
            SessionService.ContinueOffline();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Garage");
            report.Add("pas de session : hors ligne, rechargement du garage");
        }
        if (picker == null)
        {
            if (++prepareTries < 20)
            {
                var retry = steps.ToArray();
                steps.Clear();
                steps.Enqueue(PrepareGarage);
                foreach (var s in retry)
                    steps.Enqueue(s);
                waitFrames = 30;
                return;
            }
            steps.Clear();
            steps.Enqueue(Finish);
            throw new InvalidOperationException("GaragePartPicker absent ou inactif, scene active : " +
                                                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        }
        report.Add("scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path +
                   $", pieces {picker.GetType().Name} pretes");
        document = picker.GetComponent<UIDocument>();
        original = document.panelSettings;
        clone = UnityEngine.Object.Instantiate(original);
        clone.name = "PickerPanelSettings (capture)";
        clone.clearColor = true;
        clone.colorClearValue = new Color(0.30f, 0.34f, 0.40f, 1f);
    }

    private static void RestorePanel()
    {
        if (document != null && original != null)
            document.panelSettings = original;
    }

    private static string Alpha() => bridge != null && bridge.builderHud != null ? bridge.builderHud.alpha.ToString("0.##") : "-";

    private static bool Enabled<T>() where T : Behaviour
    {
        var b = UnityEngine.Object.FindAnyObjectByType<T>();
        return b != null && b.enabled;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Tick;
            Cleanup();
            return;
        }
        if (waitFrames > 0)
        {
            waitFrames--;
            return;
        }
        if (steps.Count == 0)
        {
            EditorApplication.update -= Tick;
            return;
        }
        waitFrames = 12; // fondus (140 ms), mise en page, mesure des noms
        try
        {
            steps.Dequeue()();
        }
        catch (Exception e)
        {
            report.Add("EXC " + e.GetType().Name + " " + e.Message);
            Debug.LogException(e);
        }
    }

    private static int prepareTries;

    private static void Prepare()
    {
        picker = UnityEngine.Object.FindAnyObjectByType<PartPickerController>();
        if (picker == null)
        {
            // Scene encore en chargement : on reessaie quelques secondes
            if (++prepareTries < 20)
            {
                var retry = steps.ToArray();
                steps.Clear();
                steps.Enqueue(Prepare);
                foreach (var s in retry)
                    steps.Enqueue(s);
                waitFrames = 30;
                return;
            }
            steps.Clear();
            steps.Enqueue(Finish);
            throw new InvalidOperationException("PartPickerController absent, scene active : " +
                                                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        }
        report.Add("scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        document = picker.GetComponent<UIDocument>();
        original = document.panelSettings;
        clone = UnityEngine.Object.Instantiate(original);
        clone.name = "PickerPanelSettings (capture)";
        clone.clearColor = true;
        // Viewport simule : un gris bleute derriere le voile
        clone.colorClearValue = new Color(0.30f, 0.34f, 0.40f, 1f);
        document.panelSettings = clone;
        if (!picker.IsOpen)
            picker.Open();
    }

    private static void Resize(int width, int height)
    {
        if (target != null)
        {
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
        target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "PickerCapture" };
        target.Create();
        clone.targetTexture = target;
        if (document.panelSettings != clone)
            document.panelSettings = clone;
    }

    private static void FocusRing()
    {
        var root = document.rootVisualElement.Q(className: "pp-root");
        root?.AddToClassList("pp-kbd");
        var tile = root?.Query(className: "pp-tile").ToList().Skip(2).FirstOrDefault();
        tile?.Focus();
    }

    private static void Shot(string name)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var tex = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        string path = Path.Combine(OutputDir, name + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        // Mesures utiles au rapport : fenetre et onglets dans le repere du panneau
        var root = document.rootVisualElement;
        var window = root.Q("window");
        var tabs = root.Q("tabs");
        var title = root.Q<Label>("title");
        var count = root.Q("count");
        string countText = count == null ? "" : string.Concat(count.Children().OfType<Label>().Select(l => l.text));
        report.Add($"{name}: window {Fmt(window?.worldBound)} tabs {Fmt(tabs?.worldBound)} title '{title?.text}' count '{countText}'");
    }

    private static string Fmt(Rect? r) => r.HasValue ? $"x{r.Value.x:0.#} y{r.Value.y:0.#} w{r.Value.width:0.#} h{r.Value.height:0.#}" : "-";

    private static void Finish()
    {
        File.WriteAllText(Path.Combine(OutputDir, "report.txt"), string.Join("\n", report));
        Debug.Log("[PartPickerCapture] Captures ecrites dans " + OutputDir + "\n" + string.Join("\n", report));
        Cleanup();
        EditorApplication.ExitPlaymode();
    }

    private static void Cleanup()
    {
        if (document != null && original != null)
            document.panelSettings = original;
        if (clone != null)
            UnityEngine.Object.DestroyImmediate(clone);
        if (target != null)
        {
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
        clone = null;
        target = null;
        original = null;
        document = null;
        picker = null;
        bridge = null;
    }
}
