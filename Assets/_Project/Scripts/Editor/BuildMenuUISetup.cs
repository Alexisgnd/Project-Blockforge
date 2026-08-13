using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// SETUP DE L'ECRAN "CHOISIR UN SLOT DE CONSTRUCTION"
// Menu : Blockforge > Setup MainMenu Build Screen
// A lancer APRES "Setup MainMenu UI".
// =========================================================

public static class BuildMenuUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string RobotDataDir = "Assets/_Project/Data/Robots";

    private static readonly Color ScreenBg = new(0.016f, 0.035f, 0.065f, 0.985f);
    private static readonly Color CardBg = new(0.06f, 0.10f, 0.16f, 0.95f);
    private static readonly Color PanelBg = new(0.05f, 0.08f, 0.13f, 0.9f);
    private static readonly Color CoverPlaceholder = new(0.10f, 0.16f, 0.24f, 1f);
    private static readonly Color Accent = new(0.23f, 0.51f, 0.96f);
    private static readonly Color BadgeBg = new(0.09f, 0.16f, 0.30f, 1f);
    private static readonly Color TextMain = new(0.86f, 0.91f, 0.96f);
    private static readonly Color TextDim = new(0.48f, 0.56f, 0.66f);

    [MenuItem("Blockforge/Setup MainMenu Build Screen")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = GameObject.Find("MainCanvas");
        var managerGo = GameObject.Find("MenuManager");
        if (canvasGo == null || managerGo == null)
        {
            Debug.LogError("[BuildMenuUISetup] MainCanvas ou MenuManager introuvable. " +
                           "Lance d'abord : Blockforge > Setup MainMenu UI.");
            return;
        }

        var mainController = managerGo.GetComponent<MainMenuController>();

        var oldScreen = canvasGo.transform.Find("BuildScreen");
        if (oldScreen != null)
            Object.DestroyImmediate(oldScreen.gameObject);

        // ---------- Ecran racine ----------
        var screen = CreatePanel(canvasGo.transform, "BuildScreen", ScreenBg);
        StretchFull(screen);
        screen.offsetMax = new Vector2(0, -80); // sous la top nav

        var buildMenu = screen.gameObject.AddComponent<BuildMenuController>();
        buildMenu.slots = new[]
        {
            AssetDatabase.LoadAssetAtPath<RobotPreset>($"{RobotDataDir}/Robot_Scout.asset"),
            AssetDatabase.LoadAssetAtPath<RobotPreset>($"{RobotDataDir}/Robot_Falcon.asset"),
            null, // slot libre
        };

        // ---------- Entete ----------
        var titleBar = CreatePanel(screen, "TitleAccent", Accent);
        SetAnchors(titleBar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        titleBar.sizeDelta = new Vector2(6, 44);
        titleBar.anchoredPosition = new Vector2(80, -36);

        var title = CreateText(screen, "Title", "CHOISIR UN SLOT DE CONSTRUCTION", 34, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        title.sizeDelta = new Vector2(900, 46);
        title.anchoredPosition = new Vector2(102, -34);

        var subtitle = CreateText(screen, "Subtitle",
            "Sélectionnez un emplacement pour modifier un robot existant ou créer un nouveau robot.",
            15, TextDim, TextAlignmentOptions.Left, FontStyles.Normal);
        SetAnchors(subtitle, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        subtitle.sizeDelta = new Vector2(900, 26);
        subtitle.anchoredPosition = new Vector2(102, -86);

        var usedBadge = CreatePanel(screen, "UsedBadge", BadgeBg);
        SetAnchors(usedBadge, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        usedBadge.sizeDelta = new Vector2(250, 42);
        usedBadge.anchoredPosition = new Vector2(-80, -34);
        var usedText = CreateText(usedBadge, "Text", "0 / 3 SLOTS UTILISÉS", 15, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(usedText);
        buildMenu.usedCountText = usedText.GetComponent<TMP_Text>();

        var maxText = CreateText(screen, "MaxSlots", "3 slots max", 13, TextDim, TextAlignmentOptions.Right, FontStyles.Normal);
        SetAnchors(maxText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        maxText.sizeDelta = new Vector2(250, 22);
        maxText.anchoredPosition = new Vector2(-80, -84);

        // ---------- Cartes de slots ----------
        var cards = new BuildSlotCard[3];
        for (int i = 0; i < 3; i++)
            cards[i] = BuildSlotCardUI(screen, $"Slot0{i + 1}", -500f + i * 380f);
        buildMenu.cards = cards;

        // ---------- Panneau presets (droite) ----------
        var presetsPanel = CreatePanel(screen, "PresetsPanel", PanelBg);
        SetAnchors(presetsPanel, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        presetsPanel.sizeDelta = new Vector2(330, 270);
        presetsPanel.anchoredPosition = new Vector2(-70, -40);

        var presetsTitle = CreateText(presetsPanel, "Title", "ROBOTS PRESETS DISPONIBLES", 14, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(presetsTitle, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        presetsTitle.offsetMin = new Vector2(20, -50);
        presetsTitle.offsetMax = new Vector2(-20, -14);

        var presetsBody = CreateText(presetsPanel, "Body",
            "Besoin d'un point de départ ?\nChoisissez un robot preset pour le personnaliser.",
            14, TextDim, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        SetAnchors(presetsBody, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        presetsBody.offsetMin = new Vector2(20, -150);
        presetsBody.offsetMax = new Vector2(-20, -58);

        var (presetsBtn, _) = CreateButton(presetsPanel, "PresetsButton", "VOIR LES PRESETS  >", 14,
                                           new Color(0.10f, 0.18f, 0.32f, 1f), TextMain);
        var presetsBtnRect = presetsBtn.GetComponent<RectTransform>();
        SetAnchors(presetsBtnRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        presetsBtnRect.sizeDelta = new Vector2(280, 46);
        presetsBtnRect.anchoredPosition = new Vector2(0, 20);
        buildMenu.presetsButton = presetsBtn;

        // ---------- Etat initial + cablage ----------
        screen.gameObject.SetActive(false);

        if (mainController != null)
        {
            mainController.buildMenu = buildMenu;
            EditorUtility.SetDirty(mainController);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BuildMenuUISetup] Ecran de construction genere et cable.");
    }

    // =========================================================
    // CARTE D'UN SLOT
    // =========================================================

    private static BuildSlotCard BuildSlotCardUI(RectTransform parent, string name, float x)
    {
        var card = CreatePanel(parent, name, CardBg);
        SetAnchors(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        card.sizeDelta = new Vector2(340, 560);
        card.anchoredPosition = new Vector2(x, -40);

        var widget = card.gameObject.AddComponent<BuildSlotCard>();

        // Numero du slot
        var numberBadge = CreatePanel(card, "Number", Accent);
        SetAnchors(numberBadge, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        numberBadge.sizeDelta = new Vector2(36, 36);
        numberBadge.anchoredPosition = new Vector2(14, -14);
        var numberText = CreateText(numberBadge, "Text", "1", 17, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(numberText);
        widget.numberText = numberText.GetComponent<TMP_Text>();

        // Titre (nom du robot ou "SLOT DISPONIBLE")
        var title = CreateText(card, "Title", "SLOT", 19, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        SetAnchors(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        title.offsetMin = new Vector2(56, -50);
        title.offsetMax = new Vector2(-56, -14);
        widget.titleText = title.GetComponent<TMP_Text>();

        // ----- Groupe "robot existant" -----
        var filled = CreateUI(card, "FilledGroup");
        StretchFull(filled);
        widget.filledGroup = filled.gameObject;

        var tier = CreateText(filled, "Tier", "TIER", 13, Accent, TextAlignmentOptions.Center, FontStyles.Bold);
        SetAnchors(tier, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        tier.offsetMin = new Vector2(20, -80);
        tier.offsetMax = new Vector2(-20, -52);
        widget.tierText = tier.GetComponent<TMP_Text>();

        var cover = CreatePanel(filled, "Cover", CoverPlaceholder);
        SetAnchors(cover, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        cover.offsetMin = new Vector2(14, -300);
        cover.offsetMax = new Vector2(-14, -88);
        widget.coverImage = cover.GetComponent<Image>();

        var statDefs = new (string label, string field)[]
        {
            ("PV", nameof(BuildSlotCard.healthValue)),
            ("VITESSE", nameof(BuildSlotCard.speedValue)),
            ("ÉNERGIE", nameof(BuildSlotCard.energyValue)),
            ("MASSE", nameof(BuildSlotCard.massValue)),
        };
        for (int i = 0; i < statDefs.Length; i++)
        {
            float y = -312f - i * 42f;
            var label = CreateText(filled, $"Stat_{i}_Label", statDefs[i].label, 13, TextDim, TextAlignmentOptions.Left, FontStyles.Normal);
            SetAnchors(label, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            label.offsetMin = new Vector2(22, y - 34);
            label.offsetMax = new Vector2(-140, y);

            var value = CreateText(filled, $"Stat_{i}_Value", "—", 14, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
            SetAnchors(value, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            value.offsetMin = new Vector2(22, y - 34);
            value.offsetMax = new Vector2(-22, y);

            typeof(BuildSlotCard).GetField(statDefs[i].field)
                                 .SetValue(widget, value.GetComponent<TMP_Text>());
        }

        var (modifyBtn, _) = CreateButton(filled, "ModifyButton", "MODIFIER", 16, Accent, Color.white);
        var modifyRect = modifyBtn.GetComponent<RectTransform>();
        SetAnchors(modifyRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        modifyRect.sizeDelta = new Vector2(300, 48);
        modifyRect.anchoredPosition = new Vector2(0, 18);
        widget.modifyButton = modifyBtn;

        // ----- Groupe "slot vide" -----
        var empty = CreateUI(card, "EmptyGroup");
        StretchFull(empty);
        widget.emptyGroup = empty.gameObject;

        var plus = CreateText(empty, "Plus", "+", 96, Accent, TextAlignmentOptions.Center, FontStyles.Bold);
        SetAnchors(plus, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        plus.offsetMin = new Vector2(20, -300);
        plus.offsetMax = new Vector2(-20, -120);

        var emptyText = CreateText(empty, "Text", "Créez un nouveau robot\net donnez vie à vos idées.",
                                   14, TextDim, TextAlignmentOptions.Top, FontStyles.Normal);
        SetAnchors(emptyText, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        emptyText.offsetMin = new Vector2(30, -400);
        emptyText.offsetMax = new Vector2(-30, -320);

        var (createBtn, _) = CreateButton(empty, "CreateButton", "+  CRÉER", 16, Accent, Color.white);
        var createRect = createBtn.GetComponent<RectTransform>();
        SetAnchors(createRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        createRect.sizeDelta = new Vector2(300, 48);
        createRect.anchoredPosition = new Vector2(0, 18);
        widget.createButton = createBtn;

        empty.gameObject.SetActive(false);
        return widget;
    }

    // =========================================================
    // HELPERS UI
    // =========================================================

    private static RectTransform CreateUI(Component parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        go.layer = LayerMask.NameToLayer("UI");
        return (RectTransform)go.transform;
    }

    private static RectTransform CreatePanel(Component parent, string name, Color color)
    {
        var rect = CreateUI(parent, name);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private static RectTransform CreateText(Component parent, string name, string text, float size,
                                            Color color, TextAlignmentOptions align, FontStyles style)
    {
        var rect = CreateUI(parent, name);
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
        var rect = CreatePanel(parent, name, bgColor);
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
}
