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
// Genere UNE FOIS la hierarchie Canvas (header, slots robots,
// stats) puis te laisse la retoucher visuellement dans
// l'editeur. La logique reste en C#.
// Menu : Blockforge > Setup MainMenu Header
// Regenere seulement le header (design « Play to Versus ») dans
// la scene existante : logo, sections Home / Play / Build /
// Tech tree / Workshop / Store, credits et puce joueur, sans
// toucher aux ecrans Home, Play et Build deja generes.
// =========================================================

public static class MainMenuUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    public const float HeaderHeight = 80f;

    // Sections du header, dans l'ordre du design ; la cle est le suffixe de « Nav_<cle> »
    public static readonly (string key, string label)[] Sections =
    {
        (MainMenuController.SectionHome, "Home"),
        (MainMenuController.SectionPlay, "Play"),
        (MainMenuController.SectionBuild, "Build"),
        (MainMenuController.SectionTechTree, "Tech tree"),
        (MainMenuController.SectionWorkshop, "Workshop"),
        (MainMenuController.SectionStore, "Store"),
    };

    // Palette (les couleurs definitives sont posees par DesignSystemApply)
    private static readonly Color NavBg = BlockforgeTheme.SurfaceRaised;
    private static readonly Color PanelBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Surface, 0.85f);
    private static readonly Color CardBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Surface, 0.92f);
    private static readonly Color Accent = BlockforgeTheme.Accent;
    private static readonly Color TextMain = BlockforgeTheme.TextPrimary;
    private static readonly Color TextDim = BlockforgeTheme.TextSecondary;

    [MenuItem("Blockforge/Setup MainMenu UI")]
    public static void Setup()
    {
        if (TMP_Settings.instance == null)
        {
            Debug.LogError("[MainMenuUISetup] TextMeshPro n'est pas initialise. " +
                           "Fais d'abord : Window > TextMeshPro > Import TMP Essential Resources, puis relance ce menu.");
            return;
        }

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

        // ---------- Header ----------
        BuildHeader(canvasGo.transform, controller);

        // ---------- HomeScreen ----------
        var home = CreateUI(canvasGo.transform, "HomeScreen");
        StretchFull(home);

        // Titre robot (gauche)
        var bayLabel = CreateText(home, "BayLabel", "BAY 01", 64, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(bayLabel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        bayLabel.sizeDelta = new Vector2(560, 80);
        bayLabel.anchoredPosition = new Vector2(80, -150);

        var robotName = CreateText(home, "RobotName", "ROBOT", 30, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
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
            var badgeText = CreateText(badge, "Text", "SÉLECTIONNÉ", 12, BlockforgeTheme.OnAccent, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(badgeText);
            slotBadges[i] = badge.gameObject;
            badge.gameObject.SetActive(false);
        }

        slotManager.slotButtons = slotButtons;
        slotManager.slotTierLabels = slotTiers;
        slotManager.slotNameLabels = slotNames;
        slotManager.selectedBadges = slotBadges;

        // ---------- Cablage final du controller ----------
        controller.slotManager = slotManager;
        controller.statsUI = statsUI;
        controller.bayLabelText = bayLabel.GetComponent<TMP_Text>();
        controller.robotNameText = robotName.GetComponent<TMP_Text>();

        AddScenesToBuild();

        EditorSceneManager.MarkSceneDirty(scene);
        // Design system Blockforge : formes, etats, typo, libelles anglais
        DesignSystemApply.ApplyToOpenScene();
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasGo;
        Debug.Log("[MainMenuUISetup] UI du menu generee. Relance ensuite Setup Build Screen et Setup MainMenu Play Screens.");
    }

    // Regenere le header seul dans la scene MainMenu existante
    [MenuItem("Blockforge/Setup MainMenu Header")]
    public static void SetupHeader()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = GameObject.Find("MainCanvas");
        var managerGo = GameObject.Find("MenuManager");
        if (canvasGo == null || managerGo == null)
        {
            Debug.LogError("[MainMenuUISetup] MainCanvas ou MenuManager introuvable. Lance d'abord : Blockforge > Setup MainMenu UI.");
            return;
        }
        var controller = managerGo.GetComponent<MainMenuController>();

        var old = canvasGo.transform.Find("TopNavigation");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var nav = BuildHeader(canvasGo.transform, controller);
        nav.SetAsFirstSibling(); // meme place qu'au setup complet (les ecrans commencent sous le header)
        EditorUtility.SetDirty(controller);

        EditorSceneManager.MarkSceneDirty(scene);
        DesignSystemApply.ApplyToOpenScene();
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = nav.gameObject;
        Debug.Log("[MainMenuUISetup] Header regenere : logo, sections, credits, joueur.");
    }

    // =========================================================
    // HEADER (design « Play to Versus »)
    // Bandeau surface-raised de 80 px : lockup a gauche, onglets
    // centres, a droite le solde de credits et la puce joueur.
    // La geometrie finale, les styles et les pictogrammes sont
    // poses par DesignSystemApply.ApplyMainMenu.
    // =========================================================

    public static RectTransform BuildHeader(Transform canvas, MainMenuController controller)
    {
        var nav = CreatePanel(canvas, "TopNavigation", NavBg);
        SetAnchors(nav, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        nav.sizeDelta = new Vector2(0, HeaderHeight);
        nav.anchoredPosition = Vector2.zero;

        // Lockup horizontal (sprite pose par le DS)
        var logo = CreatePanel(nav, "Logo", Color.white);
        SetAnchors(logo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        logo.sizeDelta = new Vector2(190, 52);
        logo.anchoredPosition = new Vector2(BlockforgeTheme.Space12, 0);
        logo.GetComponent<Image>().raycastTarget = false;

        // Onglets de section, centres entre le logo et le bloc de droite
        var navContainer = CreateUI(nav, "NavButtons");
        SetAnchors(navContainer, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        navContainer.offsetMin = new Vector2(360, 0);
        navContainer.offsetMax = new Vector2(-360, 0);
        var layout = navContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = BlockforgeTheme.Space1;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        foreach (var (key, label) in Sections)
        {
            var (btn, _) = CreateButton(navContainer, "Nav_" + key, label, 15, Color.clear, TextDim);
            btn.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
            if (controller != null)
                UnityEventTools.AddStringPersistentListener(btn.onClick, controller.OnNavClicked, key);
        }

        // Bloc de droite : credits puis puce joueur
        var right = CreateUI(nav, "Right");
        SetAnchors(right, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        right.sizeDelta = new Vector2(420, 40);
        right.anchoredPosition = new Vector2(-BlockforgeTheme.Space12, 0);

        var credits = CreateUI(right, "Credits");
        SetAnchors(credits, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        credits.sizeDelta = new Vector2(120, 24);
        var creditsIcon = CreatePanel(credits, "Icon", BlockforgeTheme.GradeMasterwork);
        SetAnchors(creditsIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        creditsIcon.sizeDelta = new Vector2(18, 18);
        creditsIcon.GetComponent<Image>().raycastTarget = false;
        var creditsValue = CreateText(credits, "Value", "0", 16, BlockforgeTheme.GradeMasterwork, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetAnchors(creditsValue, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        creditsValue.offsetMin = new Vector2(18 + BlockforgeTheme.Space2, 0);
        creditsValue.offsetMax = Vector2.zero;

        var player = CreatePanel(right, "Player", BlockforgeTheme.SurfacePanel);
        SetAnchors(player, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        player.sizeDelta = new Vector2(180, 40);
        var avatar = CreatePanel(player, "Avatar", BlockforgeTheme.SurfaceInset);
        SetAnchors(avatar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        avatar.sizeDelta = new Vector2(28, 28);
        avatar.anchoredPosition = new Vector2(6, 0);
        avatar.GetComponent<Image>().raycastTarget = false;
        var avatarIcon = CreatePanel(avatar, "Icon", Accent);
        SetAnchors(avatarIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        avatarIcon.sizeDelta = new Vector2(16, 16);
        avatarIcon.GetComponent<Image>().raycastTarget = false;
        var playerName = CreateText(player, "Name", "Pilot", 14, TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetAnchors(playerName, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        playerName.offsetMin = new Vector2(6 + 28 + 10, 0);
        playerName.offsetMax = new Vector2(-12, 0);

        if (controller != null)
        {
            controller.playerNameText = playerName.GetComponent<TMP_Text>();
            controller.creditsText = creditsValue.GetComponent<TMP_Text>();
            controller.navButtonsRoot = navContainer;
        }
        return nav;
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

    // Liste canonique partagee (supprime au passage l'entree fantome Arena.unity)
    private static void AddScenesToBuild()
    {
        BlockforgeScenes.RegisterAllInBuildSettings();
    }
}
