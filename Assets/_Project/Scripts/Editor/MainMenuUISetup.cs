using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// =========================================================
// SETUP DE L'UI uGUI DU MENU D'ACCUEIL
// Menu : Blockforge > Setup MainMenu UI
// Genere UNE FOIS la hierarchie Canvas (top nav, slots
// robots, stats, boutons) puis te laisse la retoucher
// visuellement dans l'editeur. La logique reste en C#.
// =========================================================

public static class MainMenuUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string RobotDataDir = "Assets/_Project/Data/Robots";

    // Palette
    private static readonly Color NavBg = new(0.03f, 0.05f, 0.09f, 0.92f);
    private static readonly Color PanelBg = new(0.05f, 0.08f, 0.13f, 0.85f);
    private static readonly Color CardBg = new(0.06f, 0.10f, 0.16f, 0.92f);
    private static readonly Color Accent = new(0.23f, 0.51f, 0.96f);
    private static readonly Color TextMain = new(0.86f, 0.91f, 0.96f);
    private static readonly Color TextDim = new(0.48f, 0.56f, 0.66f);

    [MenuItem("Blockforge/Setup MainMenu UI")]
    public static void Setup()
    {
        if (TMP_Settings.instance == null)
        {
            Debug.LogError("[MainMenuUISetup] TextMeshPro n'est pas initialise. " +
                           "Fais d'abord : Window > TextMeshPro > Import TMP Essential Resources, puis relance ce menu.");
            return;
        }

        var presets = EnsureRobotPresets();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (var name in new[] { "MainCanvas", "EventSystem", "MenuManager" })
        {
            var old = GameObject.Find(name);
            if (old != null)
                Object.DestroyImmediate(old);
        }

        // ---------- EventSystem (Input System) ----------
        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<InputSystemUIInputModule>();

        // ---------- Canvas ----------
        var canvasGo = new GameObject("MainCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // ---------- Manager ----------
        var managerGo = new GameObject("MenuManager");
        var controller = managerGo.AddComponent<MainMenuController>();
        var slotManager = managerGo.AddComponent<RobotSlotManager>();
        slotManager.presets = presets;

        // ---------- Top navigation ----------
        var nav = CreatePanel(canvasGo.transform, "TopNavigation", NavBg);
        SetAnchors(nav, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        nav.sizeDelta = new Vector2(0, 80);
        nav.anchoredPosition = Vector2.zero;

        var logo = CreateText(nav, "LogoText", "BLOCKFORGE", 30, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(logo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        logo.sizeDelta = new Vector2(280, 50);
        logo.anchoredPosition = new Vector2(40, 0);

        var navButtons = new (string label, float width)[]
        {
            ("ACCUEIL", 130), ("JOUER", 110), ("CONSTRUIRE", 160),
            ("INVENTAIRE", 160), ("ARBRE DE TECHNOLOGIE", 260), ("WORKSHOP", 150),
        };
        var navContainer = CreateUI(nav, "NavButtons");
        SetAnchors(navContainer, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        navContainer.offsetMin = new Vector2(340, 0);
        navContainer.offsetMax = new Vector2(-360, 0);
        var layout = navContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        for (int i = 0; i < navButtons.Length; i++)
        {
            bool isHome = i == 0;
            var (btn, label) = CreateButton(navContainer, $"Nav_{navButtons[i].label}", navButtons[i].label, 16,
                                            isHome ? new Color(0.09f, 0.16f, 0.28f, 1f) : Color.clear,
                                            isHome ? TextMain : TextDim);
            btn.gameObject.AddComponent<LayoutElement>().preferredWidth = navButtons[i].width;
            UnityEventTools.AddStringPersistentListener(btn.onClick, controller.OnNavClicked, navButtons[i].label);
        }

        var playerName = CreateText(nav, "PlayerName", "PILOTE", 20, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
        SetAnchors(playerName, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        playerName.sizeDelta = new Vector2(300, 30);
        playerName.anchoredPosition = new Vector2(-40, 10);

        var playerLevel = CreateText(nav, "PlayerLevel", "NIVEAU 1", 12, TextDim, TextAlignmentOptions.Right, FontStyles.Normal);
        SetAnchors(playerLevel, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        playerLevel.sizeDelta = new Vector2(300, 20);
        playerLevel.anchoredPosition = new Vector2(-40, -14);

        // ---------- HomeScreen ----------
        var home = CreateUI(canvasGo.transform, "HomeScreen");
        StretchFull(home);

        // Titre robot (gauche)
        var bayLabel = CreateText(home, "BayLabel", "BAY 07", 64, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(bayLabel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        bayLabel.sizeDelta = new Vector2(560, 80);
        bayLabel.anchoredPosition = new Vector2(80, -150);

        var robotName = CreateText(home, "RobotName", "FALCON MK.II", 30, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(robotName, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        robotName.sizeDelta = new Vector2(560, 40);
        robotName.anchoredPosition = new Vector2(82, -228);

        // Panneau statistiques (droite)
        var statsPanel = CreatePanel(home, "RobotStats", PanelBg);
        SetAnchors(statsPanel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        statsPanel.sizeDelta = new Vector2(420, 470);
        statsPanel.anchoredPosition = new Vector2(-60, -130);

        var statsTitle = CreateText(statsPanel, "Title", "STATISTIQUES", 18, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(statsTitle, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        statsTitle.offsetMin = new Vector2(24, -54);
        statsTitle.offsetMax = new Vector2(-24, -16);

        var statsUI = statsPanel.gameObject.AddComponent<RobotStatsUI>();
        var statRows = new (string label, string field)[]
        {
            ("CAPACITÉ", nameof(RobotStatsUI.capacityValue)),
            ("PUISSANCE OFFENSIVE", nameof(RobotStatsUI.offensiveValue)),
            ("SANTÉ", nameof(RobotStatsUI.healthValue)),
            ("VITESSE", nameof(RobotStatsUI.speedValue)),
            ("CONSOMMATION ÉNERGÉTIQUE", nameof(RobotStatsUI.energyValue)),
            ("POIDS", nameof(RobotStatsUI.weightValue)),
            ("NOMBRE DE BLOCS", nameof(RobotStatsUI.blocksValue)),
        };
        for (int i = 0; i < statRows.Length; i++)
        {
            float y = -70f - i * 56f;
            var rowLabel = CreateText(statsPanel, $"Stat_{i}_Label", statRows[i].label, 14, TextDim, TextAlignmentOptions.Left, FontStyles.Normal);
            SetAnchors(rowLabel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            rowLabel.offsetMin = new Vector2(24, y - 40);
            rowLabel.offsetMax = new Vector2(-140, y);

            var rowValue = CreateText(statsPanel, $"Stat_{i}_Value", "0", 17, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
            SetAnchors(rowValue, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            rowValue.offsetMin = new Vector2(24, y - 40);
            rowValue.offsetMax = new Vector2(-24, y);

            typeof(RobotStatsUI).GetField(statRows[i].field)
                                .SetValue(statsUI, rowValue.GetComponent<TMP_Text>());
        }

        // Slots robots (bas, centre)
        var slotsContainer = CreateUI(home, "RobotSlots");
        SetAnchors(slotsContainer, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        slotsContainer.sizeDelta = new Vector2(1040, 180);
        slotsContainer.anchoredPosition = new Vector2(0, 120);

        var slotButtons = new Button[3];
        var slotTiers = new TMP_Text[3];
        var slotNames = new TMP_Text[3];
        var slotBadges = new GameObject[3];

        for (int i = 0; i < 3; i++)
        {
            var card = CreatePanel(slotsContainer, $"Slot0{i + 1}", CardBg);
            SetAnchors(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            card.sizeDelta = new Vector2(320, 170);
            card.anchoredPosition = new Vector2((i - 1) * 350f, 0);
            slotButtons[i] = card.gameObject.AddComponent<Button>();
            slotButtons[i].targetGraphic = card.GetComponent<Image>();

            var tier = CreateText(card, "Tier", "TIER", 13, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
            SetAnchors(tier, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            tier.sizeDelta = new Vector2(220, 24);
            tier.anchoredPosition = new Vector2(16, -14);
            slotTiers[i] = tier.GetComponent<TMP_Text>();

            var nameText = CreateText(card, "Name", "ROBOT", 20, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
            SetAnchors(nameText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            nameText.sizeDelta = new Vector2(280, 30);
            nameText.anchoredPosition = new Vector2(16, -42);
            slotNames[i] = nameText.GetComponent<TMP_Text>();

            var badge = CreatePanel(card, "SelectedBadge", Accent);
            SetAnchors(badge, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            badge.sizeDelta = new Vector2(130, 28);
            badge.anchoredPosition = new Vector2(-12, -12);
            var badgeText = CreateText(badge, "Text", "SÉLECTIONNÉ", 12, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(badgeText);
            slotBadges[i] = badge.gameObject;
            badge.gameObject.SetActive(false);
        }

        slotManager.slotButtons = slotButtons;
        slotManager.slotTierLabels = slotTiers;
        slotManager.slotNameLabels = slotNames;
        slotManager.selectedBadges = slotBadges;

        // Barre du bas : actions principales
        var bottomBar = CreatePanel(home, "BottomBar", NavBg);
        SetAnchors(bottomBar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        bottomBar.sizeDelta = new Vector2(0, 72);
        bottomBar.anchoredPosition = Vector2.zero;

        var (playBtn, _) = CreateButton(bottomBar, "PlayButton", "JOUER", 18, Accent, Color.white);
        SetAnchors(playBtn.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        playBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 48);
        playBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(60, 0);
        UnityEventTools.AddPersistentListener(playBtn.onClick, controller.OnPlayClicked);

        var (buildBtn, _) = CreateButton(bottomBar, "BuildButton", "CONSTRUIRE", 16, CardBg, TextMain);
        SetAnchors(buildBtn.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        buildBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 48);
        buildBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(310, 0);
        UnityEventTools.AddPersistentListener(buildBtn.onClick, controller.OnBuildClicked);

        var (settingsBtn, _) = CreateButton(bottomBar, "RobotSettingsButton", "PARAMÈTRES DU ROBOT", 15, CardBg, TextDim);
        SetAnchors(settingsBtn.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        settingsBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 48);
        settingsBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(560, 0);
        UnityEventTools.AddPersistentListener(settingsBtn.onClick, controller.OnRobotSettingsClicked);

        // ---------- Cablage final du controller ----------
        controller.slotManager = slotManager;
        controller.statsUI = statsUI;
        controller.bayLabelText = bayLabel.GetComponent<TMP_Text>();
        controller.robotNameText = robotName.GetComponent<TMP_Text>();
        controller.playerNameText = playerName.GetComponent<TMP_Text>();

        AddScenesToBuild();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasGo;
        Debug.Log("[MainMenuUISetup] UI du menu generee. Elle est a toi : retouche-la dans la hierarchie.");
    }

    // =========================================================
    // DONNEES
    // =========================================================

    private static RobotPreset[] EnsureRobotPresets()
    {
        EnsureFolder(RobotDataDir);

        var defs = new (string file, string bay, string name, string tier, int[] stats)[]
        {
            ("Robot_Scout",  "BAY 01", "SCOUT",        "DÉBUTANT", new[] { 850, 1200, 2100, 240, 340, 4200, 74 }),
            ("Robot_Falcon", "BAY 07", "FALCON MK.II", "VÉTÉRAN",  new[] { 1520, 2480, 3350, 210, 580, 9420, 152 }),
            ("Robot_Titan",  "BAY 12", "TITAN",        "GUERRIER", new[] { 2100, 3600, 5200, 140, 760, 14300, 218 }),
        };

        var result = new List<RobotPreset>();
        foreach (var d in defs)
        {
            string path = $"{RobotDataDir}/{d.file}.asset";
            var preset = AssetDatabase.LoadAssetAtPath<RobotPreset>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<RobotPreset>();
                preset.bayLabel = d.bay;
                preset.robotName = d.name;
                preset.tier = d.tier;
                preset.capacity = d.stats[0];
                preset.offensivePower = d.stats[1];
                preset.health = d.stats[2];
                preset.speedKmh = d.stats[3];
                preset.energyPerSec = d.stats[4];
                preset.weightKg = d.stats[5];
                preset.blockCount = d.stats[6];
                AssetDatabase.CreateAsset(preset, path);
            }
            result.Add(preset);
        }

        AssetDatabase.SaveAssets();
        return result.ToArray();
    }

    // =========================================================
    // HELPERS UI
    // =========================================================

    private static RectTransform CreateUI(Component parent, string name) => CreateUI(parent.transform, name);

    private static RectTransform CreateUI(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return (RectTransform)go.transform;
    }

    private static RectTransform CreatePanel(Component parent, string name, Color color) => CreatePanel(parent.transform, name, color);

    private static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        var rect = CreateUI(parent, name);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    private static RectTransform CreateText(Component parent, string name, string text, float size,
                                            Color color, TextAlignmentOptions align, FontStyles style)
    {
        var rect = CreateUI(parent.transform, name);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        return rect;
    }

    private static (Button, TMP_Text) CreateButton(Component parent, string name, string label, float fontSize,
                                                   Color bgColor, Color textColor)
    {
        var rect = CreatePanel(parent.transform, name, bgColor);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();

        var textRect = CreateText(rect, "Text", label, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(textRect);
        return (button, textRect.GetComponent<TMP_Text>());
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AddScenesToBuild()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var path in new[] { "Assets/_Project/Scenes/Garage.unity", "Assets/_Project/Scenes/Arena.unity" })
        {
            if (scenes.All(s => s.path != path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
