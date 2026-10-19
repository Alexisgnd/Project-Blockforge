using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// SETUP DE L'ECRAN PLAY (design « Play to Versus »)
// Menu : Blockforge > Setup MainMenu Play Screens
// Une seule vue sous le header : a gauche le titre, la liste des
// modes (une ligne par mode) et la baie « Your robot » ; a droite
// la fiche du mode selectionne (apercu de la carte avec ses deux
// puces, titre, description, trois chiffres) et la rangee
// d'actions (Play with bots, Find a match, raccourci Entree,
// raison d'une action indisponible).
// Le setup pose la geometrie (px du canvas de reference 1920 x
// 1080) et le cablage ; les composants du DS, les styles de texte
// et les pictogrammes sont poses par ApplyStyle, appele par
// Blockforge > Apply Design System (relancable).
// A lancer APRES « Setup MainMenu UI » (MainCanvas, MenuManager).
// =========================================================

public static class GameModesUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string ModeDataDir = "Assets/_Project/Data/GameModes";

    // Anciens modes (4 v 4, sans fiche) remplaces par ceux du design
    private static readonly string[] LegacyModes = { "Mode_TeamClash", "Mode_Control", "Mode_FreeForAll", "Mode_Custom" };

    // ---------- Gabarit (px) ----------
    private const float PadTop = 40f, PadSide = 48f, PadBottom = 32f;
    private const float LeftW = 520f, ColumnGap = 32f;
    private const float CardH = 74f, CardGap = 8f;
    private const float SlotW = 176f, SlotH = 132f;
    private const float BottomBlockH = 240f;
    private const float PanelPadX = 32f;
    private const float StatW = 170f, StatGap = 24f;
    private const float PrimaryActionMinW = 280f; // « Find a match » : plus large que « Play with bots »

    [MenuItem("Blockforge/Setup MainMenu Play Screens")]
    public static void Setup()
    {
        var definitions = EnsureGameModes();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = GameObject.Find("MainCanvas");
        var managerGo = GameObject.Find("MenuManager");
        if (canvasGo == null || managerGo == null)
        {
            Debug.LogError("[GameModesUISetup] MainCanvas ou MenuManager introuvable. " +
                           "Lance d'abord : Blockforge > Setup MainMenu UI.");
            return;
        }

        var mainController = managerGo.GetComponent<MainMenuController>();
        var slotManager = managerGo.GetComponent<RobotSlotManager>();
        var homeScreen = canvasGo.transform.Find("HomeScreen");

        // Supprime une installation precedente (Find ne voit pas les objets inactifs)
        var oldScreen = canvasGo.transform.Find("PlayScreen");
        if (oldScreen != null)
            Object.DestroyImmediate(oldScreen.gameObject);

        // ---------- Ecran racine, sous le header ----------
        var screen = CreatePanel(canvasGo.transform, "PlayScreen", BlockforgeTheme.SurfaceBase);
        StretchFull(screen);
        screen.offsetMax = new Vector2(0, -MainMenuUISetup.HeaderHeight);

        var playMenu = screen.gameObject.AddComponent<PlayMenuController>();
        playMenu.definitions = definitions;
        playMenu.slotManager = slotManager;

        // =====================================================
        // COLONNE DE GAUCHE : titre, modes, robot
        // =====================================================
        var left = CreateUI(screen, "Left");
        SetAnchors(left, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1));
        left.sizeDelta = new Vector2(LeftW, 0);
        left.anchoredPosition = new Vector2(PadSide, 0);
        left.offsetMin = new Vector2(PadSide, PadBottom);
        left.offsetMax = new Vector2(PadSide + LeftW, -PadTop);

        var title = CreateText(left, "Title", "Play", 48, BlockforgeTheme.InkPrimary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Top(title, 0, 0, LeftW, 50);
        var subtitle = CreateText(left, "Subtitle", "Pick a mode. Your current robot goes in as it is.", 15,
                                  BlockforgeTheme.InkSecondary, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        Top(subtitle, 0, 62, LeftW, 22);

        var modes = CreateUI(left, "Modes");
        Top(modes, 0, 108, LeftW, definitions.Length * CardH + Mathf.Max(0, definitions.Length - 1) * CardGap);
        var cards = new GameModeCard[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
            cards[i] = BuildModeCard(modes, $"Mode_{i}", i * (CardH + CardGap), definitions[i]);
        playMenu.cards = cards;

        // Your robot : etiquette, baie (RobotSlot du DS) et bouton Change
        var yourRobot = CreateUI(left, "YourRobot");
        SetAnchors(yourRobot, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        yourRobot.sizeDelta = new Vector2(0, 14 + 8 + SlotH);
        yourRobot.anchoredPosition = Vector2.zero;
        var robotLabel = CreateText(yourRobot, "Label", "Your robot", 11, BlockforgeTheme.InkMuted, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Top(robotLabel, 0, 0, LeftW, 14);

        var slot = CreatePanel(yourRobot, "Slot", BlockforgeTheme.SurfacePanel);
        Top(slot, 0, 22, SlotW, SlotH);
        var slotElement = slot.gameObject.AddComponent<DSElement>();
        slotElement.role = DSRole.RobotSlot;
        float pad = BlockforgeTheme.Space3;
        var bay = CreateText(slot, "Bay", "Bay 01", 11, BlockforgeTheme.InkMuted, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetAnchors(bay, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        bay.offsetMin = new Vector2(pad, -pad - 14);
        bay.offsetMax = new Vector2(-pad, -pad);
        var robotName = CreateText(slot, "Name", "Robot", 24, BlockforgeTheme.InkPrimary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetAnchors(robotName, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        robotName.offsetMin = new Vector2(pad, -pad - 14 - 4 - 60);
        robotName.offsetMax = new Vector2(-pad, -pad - 14 - 4);
        var robotStats = CreateText(slot, "Stats", "", 12, BlockforgeTheme.InkSecondary, TextAlignmentOptions.BottomLeft, FontStyles.Normal);
        SetAnchors(robotStats, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        robotStats.offsetMin = new Vector2(pad, pad);
        robotStats.offsetMax = new Vector2(-pad, pad + 16);

        var (changeBtn, _) = CreateButton(yourRobot, "ChangeButton", "Change", 13, Color.clear, BlockforgeTheme.InkSecondary);
        var changeRect = (RectTransform)changeBtn.transform;
        SetAnchors(changeRect, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
        changeRect.sizeDelta = new Vector2(120, BlockforgeTheme.ControlMd);
        changeRect.anchoredPosition = new Vector2(SlotW + BlockforgeTheme.Space4, (SlotH - BlockforgeTheme.ControlMd) / 2f);
        var changeIcon = CreatePanel(changeBtn.transform, "Icon", BlockforgeTheme.InkSecondary);
        SetAnchors(changeIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        changeIcon.sizeDelta = new Vector2(16, 16);
        changeIcon.anchoredPosition = new Vector2(BlockforgeTheme.Space3, 0);
        changeIcon.GetComponent<Image>().raycastTarget = false;

        playMenu.robotSlot = slotElement;
        playMenu.robotBayText = bay.GetComponent<TMP_Text>();
        playMenu.robotNameText = robotName.GetComponent<TMP_Text>();
        playMenu.robotStatsText = robotStats.GetComponent<TMP_Text>();
        playMenu.changeRobotButton = changeBtn;

        // =====================================================
        // FICHE DU MODE (panneau de droite)
        // =====================================================
        var detail = CreatePanel(screen, "Detail", BlockforgeTheme.SurfacePanel);
        StretchFull(detail);
        detail.offsetMin = new Vector2(PadSide + LeftW + ColumnGap, PadBottom);
        detail.offsetMax = new Vector2(-PadSide, -PadTop);
        detail.gameObject.AddComponent<RectMask2D>();

        // Apercu de la carte (tout le haut du panneau)
        var preview = CreatePanel(detail, "Preview", BlockforgeTheme.SurfaceRaised);
        StretchFull(preview);
        preview.offsetMin = new Vector2(0, BottomBlockH);
        preview.GetComponent<Image>().raycastTarget = false;
        var mapImage = CreatePanel(preview, "MapImage", Color.white);
        StretchFull(mapImage);
        mapImage.GetComponent<Image>().raycastTarget = false;
        mapImage.GetComponent<Image>().preserveAspect = false;
        mapImage.GetComponent<Image>().enabled = false;
        var placeholder = CreateText(preview, "Placeholder", "[Map preview — Hex valley]", 14, BlockforgeTheme.InkMuted, TextAlignmentOptions.Center, FontStyles.Normal);
        StretchFull(placeholder);

        var mapChip = BuildChip(preview, "MapChip", false);
        var teamsChip = BuildChip(preview, "TeamsChip", true);

        // Separation puis bloc du bas
        var divider = CreatePanel(detail, "Divider", BlockforgeTheme.BorderSubtle);
        SetAnchors(divider, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        divider.sizeDelta = new Vector2(0, 1);
        divider.anchoredPosition = new Vector2(0, BottomBlockH);
        divider.GetComponent<Image>().raycastTarget = false;

        var block = CreateUI(detail, "Block");
        SetAnchors(block, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        block.sizeDelta = new Vector2(0, BottomBlockH);
        block.anchoredPosition = Vector2.zero;

        var dTitle = CreateText(block, "Title", "Team Deathmatch", 36, BlockforgeTheme.InkPrimary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Top(dTitle, PanelPadX, 28, 620, 36);
        var dDesc = CreateText(block, "Description", "Description", 15, BlockforgeTheme.InkSecondary, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        Top(dDesc, PanelPadX, 72, 620, 44);

        var stats = CreateUI(block, "Stats");
        SetAnchors(stats, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        stats.sizeDelta = new Vector2(3 * StatW + 2 * StatGap, 44);
        stats.anchoredPosition = new Vector2(-PanelPadX, -28);
        var statTexts = new TMP_Text[3];
        string[] statNames = { "Players", "Target", "Time" };
        for (int i = 0; i < 3; i++)
        {
            float x = i * (StatW + StatGap);
            var label = CreateText(stats, $"Stat_{i}_Label", statNames[i], 11, BlockforgeTheme.InkMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            Top(label, x, 0, StatW, 14);
            var value = CreateText(stats, $"Stat_{i}_Value", "—", 22, BlockforgeTheme.InkPrimary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Top(value, x, 18, StatW, 26);
            statTexts[i] = value.GetComponent<TMP_Text>();
        }
        playMenu.detailPlayers = statTexts[0];
        playMenu.detailTarget = statTexts[1];
        playMenu.detailTime = statTexts[2];

        var divider2 = CreatePanel(block, "Divider", BlockforgeTheme.BorderSubtle);
        SetAnchors(divider2, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        divider2.offsetMin = new Vector2(PanelPadX, -137);
        divider2.offsetMax = new Vector2(-PanelPadX, -136);
        divider2.GetComponent<Image>().raycastTarget = false;

        // Rangee d'actions
        var actions = CreateUI(block, "Actions");
        SetAnchors(actions, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        actions.offsetMin = new Vector2(PanelPadX, -157 - 48);
        actions.offsetMax = new Vector2(-PanelPadX, -157);

        var (playBtn, _) = CreateButton(actions, "PlayButton", "Play with bots", 14, BlockforgeTheme.Accent, BlockforgeTheme.InkOnAccent);
        var playRect = (RectTransform)playBtn.transform;
        SetAnchors(playRect, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        playRect.sizeDelta = new Vector2(220, 48);
        playRect.anchoredPosition = Vector2.zero;
        var playIcon = CreatePanel(playBtn.transform, "Icon", BlockforgeTheme.InkOnAccent);
        SetAnchors(playIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        playIcon.sizeDelta = new Vector2(18, 18);
        playIcon.anchoredPosition = new Vector2(BlockforgeTheme.Space6, 0);
        playIcon.GetComponent<Image>().raycastTarget = false;

        var (findBtn, _) = CreateButton(actions, "FindMatchButton", "Find a match", 14, BlockforgeTheme.AccentSoft, BlockforgeTheme.InkPrimary);
        var findRect = (RectTransform)findBtn.transform;
        SetAnchors(findRect, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        findRect.sizeDelta = new Vector2(220, 48);
        findRect.anchoredPosition = new Vector2(236, 0);
        var findIcon = CreatePanel(findBtn.transform, "Icon", BlockforgeTheme.InkPrimary);
        SetAnchors(findIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        findIcon.sizeDelta = new Vector2(18, 18);
        findIcon.anchoredPosition = new Vector2(BlockforgeTheme.Space6, 0);
        findIcon.GetComponent<Image>().raycastTarget = false;
        var lockIcon = CreatePanel(findBtn.transform, "LockIcon", BlockforgeTheme.InkSecondary);
        SetAnchors(lockIcon, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        lockIcon.sizeDelta = new Vector2(14, 14);
        lockIcon.anchoredPosition = new Vector2(-BlockforgeTheme.Space3, 0);
        lockIcon.GetComponent<Image>().raycastTarget = false;

        // KeyHint : touche Entree + libelle de l'action
        var keyHint = CreateUI(actions, "KeyHint");
        SetAnchors(keyHint, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        keyHint.sizeDelta = new Vector2(200, 24);
        keyHint.anchoredPosition = new Vector2(472, 0);
        var keyCap = CreatePanel(keyHint, "Key", BlockforgeTheme.SurfaceInset);
        SetAnchors(keyCap, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        keyCap.sizeDelta = new Vector2(44, 22);
        var keyText = CreateText(keyCap, "Label", "Enter", 11, BlockforgeTheme.InkPrimary, TextAlignmentOptions.Center, FontStyles.Normal);
        StretchFull(keyText);
        var keyCaption = CreateText(keyHint, "Caption", "Play with bots", 12, BlockforgeTheme.InkSecondary, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        SetAnchors(keyCaption, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        keyCaption.offsetMin = new Vector2(44 + BlockforgeTheme.Space2, 0);
        keyCaption.offsetMax = Vector2.zero;

        // Raison d'une action indisponible (a droite, vide au repos)
        var reason = CreateText(actions, "Reason", "", 13, BlockforgeTheme.InkMuted, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
        SetAnchors(reason, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f));
        reason.sizeDelta = new Vector2(420, 0);
        reason.anchoredPosition = Vector2.zero;

        // ---------- Cablage ----------
        playMenu.detailTitle = dTitle.GetComponent<TMP_Text>();
        playMenu.detailDescription = dDesc.GetComponent<TMP_Text>();
        playMenu.mapChipText = mapChip.Find("Text").GetComponent<TMP_Text>();
        playMenu.teamsChipText = teamsChip.Find("Text").GetComponent<TMP_Text>();
        playMenu.mapPlaceholderText = placeholder.GetComponent<TMP_Text>();
        playMenu.mapImage = mapImage.GetComponent<Image>();
        playMenu.playButton = playBtn;
        playMenu.findMatchButton = findBtn;
        playMenu.reasonText = reason.GetComponent<TMP_Text>();

        screen.gameObject.SetActive(false);

        if (mainController != null)
        {
            mainController.playMenu = playMenu;
            if (homeScreen != null)
                mainController.homeScreen = homeScreen.gameObject;
            EditorUtility.SetDirty(mainController);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        // Design system Blockforge : composants, styles, pictogrammes (ApplyStyle ci-dessous)
        DesignSystemApply.ApplyToOpenScene();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GameModesUISetup] Ecran Play genere et cable (" + definitions.Length + " mode(s)).");
    }

    // =========================================================
    // LIGNE D'UN MODE
    // =========================================================

    private static GameModeCard BuildModeCard(RectTransform parent, string name, float y, GameModeDefinition def)
    {
        var card = CreatePanel(parent, name, BlockforgeTheme.SurfacePanel);
        Top(card, 0, y, LeftW, CardH);

        var widget = card.gameObject.AddComponent<GameModeCard>();
        widget.button = card.gameObject.AddComponent<Button>();
        widget.button.targetGraphic = card.GetComponent<Image>();
        widget.element = card.gameObject.AddComponent<DSElement>();
        widget.element.role = DSRole.Card;

        float padX = 20f, padY = (CardH - 44f) / 2f;
        var well = CreatePanel(card, "IconWell", BlockforgeTheme.SurfaceInset);
        SetAnchors(well, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        well.sizeDelta = new Vector2(44, 44);
        well.anchoredPosition = new Vector2(padX, 0);
        well.GetComponent<Image>().raycastTarget = false;
        widget.iconWell = well.GetComponent<Image>();

        var icon = CreatePanel(well, "Icon", BlockforgeTheme.InkSecondary);
        SetAnchors(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        icon.sizeDelta = new Vector2(24, 24);
        icon.GetComponent<Image>().raycastTarget = false;
        widget.iconImage = icon.GetComponent<Image>();

        float textLeft = padX + 44f + 16f, textRight = padX + 20f + 16f;
        var title = CreateText(card, "Title", def != null ? def.title : "Mode", 18, BlockforgeTheme.InkPrimary, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        SetAnchors(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        title.offsetMin = new Vector2(textLeft, -padY - 22);
        title.offsetMax = new Vector2(-textRight, -padY);
        widget.titleText = title.GetComponent<TMP_Text>();

        var summary = CreateText(card, "Summary", def != null ? def.summary : "", 13, BlockforgeTheme.InkMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        SetAnchors(summary, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        summary.offsetMin = new Vector2(textLeft, -padY - 22 - 4 - 16);
        summary.offsetMax = new Vector2(-textRight, -padY - 22 - 4);
        widget.summaryText = summary.GetComponent<TMP_Text>();

        var chevron = CreatePanel(card, "Chevron", BlockforgeTheme.InkSecondary);
        SetAnchors(chevron, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        chevron.sizeDelta = new Vector2(20, 20);
        chevron.anchoredPosition = new Vector2(-padX, 0);
        chevron.GetComponent<Image>().raycastTarget = false;
        widget.chevron = chevron.GetComponent<Image>();
        widget.chevron.enabled = false;

        return widget;
    }

    // Puce du HUD sur l'apercu : plaque surface-hud au coin exterieur coupe, pictogramme, libelle
    private static RectTransform BuildChip(RectTransform preview, string name, bool right)
    {
        var chip = CreateUI(preview, name);
        chip.gameObject.AddComponent<CanvasRenderer>();
        var plate = chip.gameObject.AddComponent<HudPolygon>();
        plate.raycastTarget = false;
        plate.SetSolid(BlockforgeTheme.SurfaceHud);
        if (right) plate.cutTopRight = BlockforgeTheme.HudChamfer; else plate.cutBottomLeft = BlockforgeTheme.HudChamfer;
        SetAnchors(chip, new Vector2(right ? 1 : 0, 1), new Vector2(right ? 1 : 0, 1), new Vector2(right ? 1 : 0, 1));
        chip.sizeDelta = new Vector2(140, 28);
        chip.anchoredPosition = new Vector2(right ? -24 : 24, -20);

        var icon = CreatePanel(chip, "Icon", BlockforgeTheme.InkPrimary);
        SetAnchors(icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        icon.sizeDelta = new Vector2(14, 14);
        icon.anchoredPosition = new Vector2(12, 0);
        icon.GetComponent<Image>().raycastTarget = false;
        var text = CreateText(chip, "Text", right ? "5 vs 5" : "Hex valley", 12, BlockforgeTheme.InkPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetAnchors(text, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        text.offsetMin = new Vector2(12 + 14 + 8, 0);
        text.offsetMax = new Vector2(-12, 0);
        return chip;
    }

    // =========================================================
    // STYLE DU DS (appele par DesignSystemApply.ApplyToOpenScene)
    // Relancable : pose les roles, les styles de texte, les
    // pictogrammes et les largeurs calculees sur le texte.
    // =========================================================

    public static void ApplyStyle()
    {
        var screen = DesignSystemApply.F("PlayScreen");
        if (screen == null)
            return;
        DesignSystemApply.SetImage(screen, BlockforgeTheme.SurfaceBase);
        var playMenu = screen.GetComponent<PlayMenuController>();

        // ----- Colonne de gauche -----
        DesignSystemApply.Txt(DesignSystemApply.T("PlayScreen/Left/Title"), DSTextStyle.DisplayXL, BlockforgeTheme.InkPrimary, "Play", TextAlignmentOptions.TopLeft);
        DesignSystemApply.Txt(DesignSystemApply.T("PlayScreen/Left/Subtitle"), DSTextStyle.Body, BlockforgeTheme.InkSecondary, null, TextAlignmentOptions.TopLeft);

        var cards = playMenu != null && playMenu.cards != null ? playMenu.cards : new GameModeCard[0];
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;
            var card = cards[i].transform;
            var def = playMenu.definitions != null && i < playMenu.definitions.Length ? playMenu.definitions[i] : null;
            DesignSystemApply.El(card, DSRole.Card, new[] { cards[i].titleText });
            DesignSystemApply.El(card.Find("IconWell"), DSRole.Inset);
            DesignSystemApply.SetIcon(cards[i].iconImage, def != null ? def.iconName : "objective", BlockforgeTheme.InkSecondary, 24f);
            DesignSystemApply.Txt(cards[i].titleText, DSTextStyle.TitleMD, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.TopLeft);
            if (cards[i].titleText != null)
                cards[i].titleText.fontSize = 18f;
            DesignSystemApply.Txt(cards[i].summaryText, DSTextStyle.BodySM, BlockforgeTheme.InkMuted, null, TextAlignmentOptions.TopLeft);
            DesignSystemApply.SetIcon(cards[i].chevron, "chevron-right", BlockforgeTheme.InkSecondary, 20f);
        }

        const string yr = "PlayScreen/Left/YourRobot/";
        DesignSystemApply.Txt(DesignSystemApply.T(yr + "Label"), DSTextStyle.Label, BlockforgeTheme.InkMuted, "Your robot", TextAlignmentOptions.TopLeft);
        var slot = DesignSystemApply.F(yr + "Slot");
        if (slot != null)
        {
            DesignSystemApply.El(slot, DSRole.RobotSlot, new[] { slot.Find("Name")?.GetComponent<TMP_Text>() });
            DesignSystemApply.Txt(DesignSystemApply.T(yr + "Slot/Bay"), DSTextStyle.Label, BlockforgeTheme.InkMuted, null, TextAlignmentOptions.TopLeft);
            var name = DesignSystemApply.T(yr + "Slot/Name");
            DesignSystemApply.Txt(name, DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.TopLeft);
            if (name != null)
            {
                name.textWrappingMode = TextWrappingModes.Normal;
                name.overflowMode = TextOverflowModes.Ellipsis;
                name.maxVisibleLines = 2;
            }
            DesignSystemApply.Txt(DesignSystemApply.T(yr + "Slot/Stats"), DSTextStyle.ReadoutSM, BlockforgeTheme.InkSecondary, null, TextAlignmentOptions.BottomLeft);
        }
        var change = DesignSystemApply.F(yr + "ChangeButton");
        if (change != null)
        {
            var text = change.Find("Text")?.GetComponent<TMP_Text>();
            DesignSystemApply.Txt(text, DSTextStyle.ButtonMD, BlockforgeTheme.InkSecondary, "Change", TextAlignmentOptions.Center);
            var icon = change.Find("Icon")?.GetComponent<Image>();
            DesignSystemApply.SetIcon(icon, "edit", BlockforgeTheme.InkSecondary, 16f);
            var el = DesignSystemApply.El(change, DSRole.ButtonGhost, new[] { text });
            if (el != null && icon != null)
                el.icons = new Graphic[] { icon };
            float w = BlockforgeTheme.Space3 + 16f + BlockforgeTheme.Space2 + DesignSystemApply.PrefWidth(text) + BlockforgeTheme.Space4;
            DesignSystemApply.Resize(change, w, BlockforgeTheme.ControlMd);
            if (text != null)
            {
                text.rectTransform.offsetMin = new Vector2(BlockforgeTheme.Space3 + 16f + BlockforgeTheme.Space2, 0);
                text.rectTransform.offsetMax = new Vector2(-BlockforgeTheme.Space4, 0);
            }
        }

        // ----- Fiche -----
        const string d = "PlayScreen/Detail/";
        DesignSystemApply.El(DesignSystemApply.F("PlayScreen/Detail"), DSRole.Panel);
        DesignSystemApply.SetImage(DesignSystemApply.F(d + "Preview"), BlockforgeTheme.SurfaceRaised);
        DesignSystemApply.Txt(DesignSystemApply.T(d + "Preview/Placeholder"), DSTextStyle.ReadoutMD, BlockforgeTheme.InkMuted, null, TextAlignmentOptions.Center);
        ApplyChip(d + "Preview/MapChip", "objective");
        ApplyChip(d + "Preview/TeamsChip", "group");
        DesignSystemApply.SetImage(DesignSystemApply.F(d + "Divider"), BlockforgeTheme.BorderSubtle);

        const string b = "PlayScreen/Detail/Block/";
        var title = DesignSystemApply.T(b + "Title");
        DesignSystemApply.Txt(title, DSTextStyle.DisplayLG, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.TopLeft);
        if (title != null)
            title.fontSize = 36f;
        var desc = DesignSystemApply.T(b + "Description");
        DesignSystemApply.Txt(desc, DSTextStyle.Body, BlockforgeTheme.InkSecondary, null, TextAlignmentOptions.TopLeft);
        if (desc != null)
            desc.textWrappingMode = TextWrappingModes.Normal;
        string[] statNames = { "Players", "Target", "Time" };
        for (int i = 0; i < 3; i++)
        {
            DesignSystemApply.Txt(DesignSystemApply.T($"{b}Stats/Stat_{i}_Label"), DSTextStyle.Label, BlockforgeTheme.InkMuted, statNames[i], TextAlignmentOptions.TopLeft);
            var value = DesignSystemApply.T($"{b}Stats/Stat_{i}_Value");
            DesignSystemApply.Txt(value, DSTextStyle.ReadoutLG, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.TopLeft);
            if (value != null)
                value.fontSize = 22f;
        }
        DesignSystemApply.SetImage(DesignSystemApply.F(b + "Divider"), BlockforgeTheme.BorderSubtle);

        // ----- Actions -----
        // En ligne d'abord : « Find a match » est l'action principale (primary,
        // control-lg, plus large) ; « Play with bots » passe en secondaire
        // (control-md, libelle button-md). Une seule action accent par region.
        const string a = "PlayScreen/Detail/Block/Actions/";
        float x = 0f;
        var find = DesignSystemApply.F(a + "FindMatchButton");
        if (find != null)
        {
            var text = find.Find("Text")?.GetComponent<TMP_Text>();
            DesignSystemApply.Txt(text, DSTextStyle.ButtonLG, BlockforgeTheme.InkOnAccent, "Find a match", TextAlignmentOptions.Center);
            var icon = find.Find("Icon")?.GetComponent<Image>();
            DesignSystemApply.SetIcon(icon, "group", BlockforgeTheme.InkOnAccent, 18f);
            var lockIcon = find.Find("LockIcon");
            if (lockIcon != null)
                lockIcon.gameObject.SetActive(false); // disponible : plus de cadenas
            var el = DesignSystemApply.El(find, DSRole.ButtonPrimary, new[] { text });
            if (el != null && icon != null)
                el.icons = new Graphic[] { icon };
            float w = Mathf.Max(PrimaryActionMinW, IconButtonWidth(text, 18f, 0f));
            DesignSystemApply.Place(find, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(w, BlockforgeTheme.ControlLg), new Vector2(0, 0.5f));
            IconButtonText(text, 18f, 0f);
            x += w + BlockforgeTheme.Space4;
        }
        var play = DesignSystemApply.F(a + "PlayButton");
        if (play != null)
        {
            var text = play.Find("Text")?.GetComponent<TMP_Text>();
            DesignSystemApply.Txt(text, DSTextStyle.ButtonMD, BlockforgeTheme.InkPrimary, "Play with bots", TextAlignmentOptions.Center);
            var icon = play.Find("Icon")?.GetComponent<Image>();
            DesignSystemApply.SetIcon(icon, "play", BlockforgeTheme.InkPrimary, 16f);
            if (icon != null)
                icon.rectTransform.anchoredPosition = new Vector2(BlockforgeTheme.Space4, 0);
            var el = DesignSystemApply.El(play, DSRole.ButtonSecondary, new[] { text });
            if (el != null && icon != null)
                el.icons = new Graphic[] { icon };
            float w = BlockforgeTheme.Space4 + 16f + BlockforgeTheme.Space2 + DesignSystemApply.PrefWidth(text) + BlockforgeTheme.Space4;
            DesignSystemApply.Place(play, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(w, BlockforgeTheme.ControlMd), new Vector2(0, 0.5f));
            if (text != null)
            {
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(BlockforgeTheme.Space4 + 16f + BlockforgeTheme.Space2, 0);
                text.rectTransform.offsetMax = new Vector2(-BlockforgeTheme.Space4, 0);
            }
            x += w + BlockforgeTheme.Space4;
        }
        var hint = DesignSystemApply.F(a + "KeyHint");
        if (hint != null)
        {
            var key = hint.Find("Key");
            var keyText = key != null ? key.Find("Label")?.GetComponent<TMP_Text>() : null;
            DesignSystemApply.Txt(keyText, DSTextStyle.Key, BlockforgeTheme.InkPrimary, "Enter", TextAlignmentOptions.Center);
            DesignSystemApply.El(key, DSRole.KeyCap, new[] { keyText });
            var caption = hint.Find("Caption")?.GetComponent<TMP_Text>();
            DesignSystemApply.Txt(caption, DSTextStyle.Caption, BlockforgeTheme.InkSecondary, "Find a match", TextAlignmentOptions.MidlineLeft);
            DesignSystemApply.Place(hint, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(240, 24), new Vector2(0, 0.5f));
        }
        DesignSystemApply.Txt(DesignSystemApply.T(a + "Reason"), DSTextStyle.Caption, BlockforgeTheme.InkMuted, null, TextAlignmentOptions.MidlineRight);
    }

    private static void ApplyChip(string path, string icon)
    {
        var chip = DesignSystemApply.F(path);
        if (chip == null)
            return;
        var text = chip.Find("Text")?.GetComponent<TMP_Text>();
        DesignSystemApply.Txt(text, DSTextStyle.Label, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.MidlineLeft);
        if (text != null)
            text.fontSize = 12f;
        DesignSystemApply.SetIcon(chip.Find("Icon")?.GetComponent<Image>(), icon, BlockforgeTheme.InkPrimary, 14f);
        DesignSystemApply.Resize(chip, 12 + 14 + 8 + DesignSystemApply.PrefWidth(text) + 12, 28f);
    }

    // Bouton avec pictogramme a gauche (et cadenas a droite en option) : padding space-6
    private static float IconButtonWidth(TMP_Text text, float iconSize, float trailing)
    {
        return BlockforgeTheme.Space6 + iconSize + BlockforgeTheme.Space2 + DesignSystemApply.PrefWidth(text) + trailing + BlockforgeTheme.Space6;
    }

    private static void IconButtonText(TMP_Text text, float iconSize, float trailing)
    {
        if (text == null)
            return;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(BlockforgeTheme.Space6 + iconSize + BlockforgeTheme.Space2, 0);
        text.rectTransform.offsetMax = new Vector2(-BlockforgeTheme.Space6 - trailing, 0);
    }

    // =========================================================
    // DONNEES : les deux modes du design (5 v 5)
    // =========================================================

    private static GameModeDefinition[] EnsureGameModes()
    {
        EnsureFolder(ModeDataDir);

        foreach (var legacy in LegacyModes)
        {
            string path = $"{ModeDataDir}/{legacy}.asset";
            if (AssetDatabase.LoadAssetAtPath<GameModeDefinition>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        var defs = new List<GameModeDefinition>
        {
            EnsureMode("Mode_TeamDeathmatch", m =>
            {
                m.title = "Team Deathmatch";
                m.summary = "5 vs 5 · first to 25 · about 10 min";
                m.description = "Two teams of five. Destroy enemy robots to score; the first team to 25 wins, " +
                                "or the one leading when the clock runs out. Bots fill any seat left open.";
                m.iconName = "skull";
                m.playersLabel = "5 vs 5"; m.targetLabel = "25 kills"; m.timeLabel = "10:00";
                m.teamSize = 5; m.mapName = "Hex valley"; m.versusSummary = "first to 25 · 10:00";
                m.sceneName = "Map_MARS";
            }),
            EnsureMode("Mode_BattleArena", m =>
            {
                m.title = "Battle Arena";
                m.summary = "5 vs 5 · hold three objectives · about 15 min";
                m.description = "Two teams of five fight for three objectives spread across the arena. Holding " +
                                "more objectives than the enemy drains their tickets; the first team to drain the " +
                                "other's tickets wins. Bots fill any seat left open.";
                m.iconName = "objective";
                m.playersLabel = "5 vs 5"; m.targetLabel = "3 objectives"; m.timeLabel = "15:00";
                m.teamSize = 5; m.mapName = "Hex valley"; m.versusSummary = "hold three objectives · 15:00";
                m.sceneName = "Map_MARS";
            }),
        };

        AssetDatabase.SaveAssets();
        return defs.ToArray();
    }

    private static GameModeDefinition EnsureMode(string fileName, System.Action<GameModeDefinition> fill)
    {
        string path = $"{ModeDataDir}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<GameModeDefinition>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<GameModeDefinition>();
            fill(asset);
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    // =========================================================
    // HELPERS UI (memes conventions que MainMenuUISetup)
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
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
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

    // Ancre en haut a gauche du parent : position du coin haut-gauche, taille
    private static void Top(RectTransform rect, float x, float y, float width, float height)
    {
        SetAnchors(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
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
