using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// SETUP DE L'ECRAN "CHOISIR UN SLOT DE CONSTRUCTION"
// Menu : Blockforge > Setup MainMenu Build Screen
// A lancer APRES "Setup MainMenu UI".
// Menu : Blockforge > MainMenu Build Screen - Add Delete
// Ajoute a l'ecran EXISTANT (sans le regenerer) le bouton
// SUPPRIMER de chaque carte et la popup de confirmation.
// =========================================================

public static class BuildMenuUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";

    private static readonly Color ScreenBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Background, 0.985f);
    private static readonly Color CardBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Surface, 0.95f);
    private static readonly Color CoverPlaceholder = BlockforgeTheme.SurfaceRaised;
    private static readonly Color Accent = BlockforgeTheme.Accent;
    private static readonly Color BadgeBg = BlockforgeTheme.AccentMuted;
    private static readonly Color TextMain = BlockforgeTheme.TextPrimary;
    private static readonly Color TextDim = BlockforgeTheme.TextSecondary;
    private static readonly Color DangerBg = BlockforgeTheme.DangerMuted;
    private static readonly Color DangerText = BlockforgeTheme.DangerText;
    private static readonly Color DangerStrong = BlockforgeTheme.Danger;
    private static readonly Color OverlayBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Scrim, 0.78f);
    private static readonly Color PopupBg = BlockforgeTheme.Surface;

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

        // Les slots sont charges au runtime depuis PocketBase (collection "robots")
        var buildMenu = screen.gameObject.AddComponent<BuildMenuController>();

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
            cards[i] = BuildSlotCardUI(screen, $"Slot0{i + 1}", -380f + i * 380f);
        buildMenu.cards = cards;
        foreach (var card in cards)
            EnsureDeleteButton(card);

        // ---------- Popup de suppression ----------
        EnsureDeletePopup(screen, buildMenu);

        // ---------- Etat initial + cablage ----------
        screen.gameObject.SetActive(false);

        if (mainController != null)
        {
            mainController.buildMenu = buildMenu;
            EditorUtility.SetDirty(mainController);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        // Design system Blockforge : formes, etats, typo, libelles anglais
        DesignSystemApply.ApplyToOpenScene();
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
        var numberText = CreateText(numberBadge, "Text", "1", 17, BlockforgeTheme.OnAccent, TextAlignmentOptions.Center, FontStyles.Bold);
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

        var (modifyBtn, _) = CreateButton(filled, "ModifyButton", "MODIFIER", 16, Accent, BlockforgeTheme.OnAccent);
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

        var (createBtn, _) = CreateButton(empty, "CreateButton", "+  CRÉER", 16, Accent, BlockforgeTheme.OnAccent);
        var createRect = createBtn.GetComponent<RectTransform>();
        SetAnchors(createRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        createRect.sizeDelta = new Vector2(300, 48);
        createRect.anchoredPosition = new Vector2(0, 18);
        widget.createButton = createBtn;

        empty.gameObject.SetActive(false);
        return widget;
    }

    // =========================================================
    // SUPPRESSION : AJOUT A L'ECRAN EXISTANT
    // =========================================================

    [MenuItem("Blockforge/MainMenu Build Screen - Add Delete")]
    public static void AddDeleteToExistingScreen()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var buildMenu = Object.FindAnyObjectByType<BuildMenuController>(FindObjectsInactive.Include);
        if (buildMenu == null)
        {
            Debug.LogError("[BuildMenuUISetup] Aucun BuildMenuController dans MainMenu : " +
                           "lance d'abord Blockforge > Setup MainMenu Build Screen.");
            return;
        }

        int added = 0;
        foreach (var card in buildMenu.cards)
        {
            if (card != null && EnsureDeleteButton(card))
                added++;
        }
        bool popupCreated = EnsureDeletePopup((RectTransform)buildMenu.transform, buildMenu);

        EditorUtility.SetDirty(buildMenu);
        EditorSceneManager.MarkSceneDirty(scene);
        // Design system Blockforge : formes, etats, typo, libelles anglais
        DesignSystemApply.ApplyToOpenScene();
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BuildMenuUISetup] Suppression ajoutee : {added} bouton(s) SUPPRIMER, " +
                  $"popup {(popupCreated ? "creee" : "deja presente")}. Scene sauvegardee.");
    }

    // Bouton SUPPRIMER a droite de MODIFIER (MODIFIER est raccourci).
    // Retourne true si le bouton a ete cree.
    private static bool EnsureDeleteButton(BuildSlotCard widget)
    {
        if (widget.deleteButton != null)
            return false;

        if (widget.modifyButton != null)
        {
            var modifyRect = widget.modifyButton.GetComponent<RectTransform>();
            modifyRect.sizeDelta = new Vector2(196, 48);
            modifyRect.anchoredPosition = new Vector2(-52, 18);
            EditorUtility.SetDirty(modifyRect);
        }

        var parent = widget.filledGroup != null ? widget.filledGroup.transform : widget.transform;
        var (deleteBtn, _) = CreateButton(parent, "DeleteButton", "SUPPRIMER", 13, DangerBg, DangerText);
        var deleteRect = deleteBtn.GetComponent<RectTransform>();
        SetAnchors(deleteRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        deleteRect.sizeDelta = new Vector2(96, 48);
        deleteRect.anchoredPosition = new Vector2(102, 18);

        widget.deleteButton = deleteBtn;
        EditorUtility.SetDirty(widget);
        return true;
    }

    // Popup modale plein ecran (le fond bloque les clics sur les cartes).
    // Retourne true si la popup a ete creee.
    private static bool EnsureDeletePopup(RectTransform screen, BuildMenuController buildMenu)
    {
        if (buildMenu.deletePopup != null)
            return false;

        var overlay = CreatePanel(screen, "DeletePopup", OverlayBg);
        StretchFull(overlay);
        overlay.SetAsLastSibling();

        var panel = CreatePanel(overlay, "Panel", PopupBg);
        SetAnchors(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.sizeDelta = new Vector2(560, 260);
        panel.anchoredPosition = new Vector2(0, 20);

        var accent = CreatePanel(panel, "Accent", DangerStrong);
        SetAnchors(accent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        accent.offsetMin = new Vector2(0, -4);
        accent.offsetMax = Vector2.zero;

        var title = CreateText(panel, "Title", "SUPPRIMER CE ROBOT ?", 22, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        title.offsetMin = new Vector2(32, -64);
        title.offsetMax = new Vector2(-32, -24);

        var message = CreateText(panel, "Message", "« ROBOT » (slot 1) sera définitivement supprimé.\nCette action est irréversible.",
                                 15, TextDim, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        SetAnchors(message, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        message.offsetMin = new Vector2(32, -160);
        message.offsetMax = new Vector2(-32, -76);

        var (cancelBtn, _) = CreateButton(panel, "CancelButton", "ANNULER", 15, CardBg, TextMain);
        var cancelRect = cancelBtn.GetComponent<RectTransform>();
        SetAnchors(cancelRect, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        cancelRect.sizeDelta = new Vector2(170, 46);
        cancelRect.anchoredPosition = new Vector2(-218, 28);

        var (confirmBtn, _) = CreateButton(panel, "ConfirmButton", "SUPPRIMER", 15, DangerStrong, Color.white);
        var confirmRect = confirmBtn.GetComponent<RectTransform>();
        SetAnchors(confirmRect, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        confirmRect.sizeDelta = new Vector2(170, 46);
        confirmRect.anchoredPosition = new Vector2(-32, 28);

        overlay.gameObject.SetActive(false);

        buildMenu.deletePopup = overlay.gameObject;
        buildMenu.deleteMessageText = message.GetComponent<TMP_Text>();
        buildMenu.deleteConfirmButton = confirmBtn;
        buildMenu.deleteCancelButton = cancelBtn;
        EditorUtility.SetDirty(buildMenu);
        return true;
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
