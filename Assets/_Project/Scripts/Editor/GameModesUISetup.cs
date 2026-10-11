using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// SETUP DES ECRANS "MODES DE JEU" + "DETAILS D'UN MODE"
// Menu : Blockforge > Setup MainMenu Play Screens
// Pas d'apercu : les cartes et la fiche n'ont plus d'image de
// couverture ni de carte (pictogramme du mode a la place, carte
// d'exemple en ligne de texte dans le panneau Details).
// A lancer APRES "Setup MainMenu UI" (il a besoin du
// MainCanvas et du MenuManager existants).
// =========================================================

public static class GameModesUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string ModeDataDir = "Assets/_Project/Data/GameModes";

    private static readonly Color ScreenBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Background, 0.985f);
    private static readonly Color CardBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Surface, 0.95f);
    private static readonly Color PanelBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Surface, 0.9f);
    private static readonly Color CoverPlaceholder = BlockforgeTheme.SurfaceRaised;
    private static readonly Color Accent = BlockforgeTheme.Accent;
    private static readonly Color BlueBoxBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.AccentMuted, 0.95f);
    private static readonly Color RedBoxBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.DangerMuted, 0.95f);
    private static readonly Color TextMain = BlockforgeTheme.TextPrimary;
    private static readonly Color TextDim = BlockforgeTheme.TextSecondary;
    private static readonly Color TextBody = BlockforgeTheme.TextSecondary;

    // Gabarits (px, canvas de reference) : cartes compactes, fiche en colonne
    private const float CardWidth = 320f;
    private const float CardHeight = 340f;
    private const float CardGap = 24f;
    private const float DetailWidth = 1200f;
    private const float DetailsPanelWidth = 460f;
    private const float PanelGap = 24f;
    private const float PanelHeight = 400f;

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
        var homeScreen = canvasGo.transform.Find("HomeScreen");

        // Supprime une installation precedente (Find ne voit pas les objets inactifs)
        var oldScreen = canvasGo.transform.Find("PlayScreen");
        if (oldScreen != null)
            Object.DestroyImmediate(oldScreen.gameObject);

        // ---------- Ecran racine ----------
        var screen = CreatePanel(canvasGo.transform, "PlayScreen", ScreenBg);
        StretchFull(screen);
        // Sous la top nav (80 px)
        screen.offsetMax = new Vector2(0, -80);

        var playMenu = screen.gameObject.AddComponent<PlayMenuController>();
        playMenu.definitions = definitions;

        // =====================================================
        // VUE 1 : LISTE DES MODES (cartes sans apercu)
        // =====================================================
        var modesView = CreateUI(screen, "ModesView");
        StretchFull(modesView);

        var title = CreateText(modesView, "Title", "Game modes", 42, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        SetAnchors(title, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        title.sizeDelta = new Vector2(800, 60);
        title.anchoredPosition = new Vector2(0, -50);

        var subtitle = CreateText(modesView, "Subtitle", "Choose how you want to play.", 16,
                                  TextDim, TextAlignmentOptions.Center, FontStyles.Normal);
        SetAnchors(subtitle, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        subtitle.sizeDelta = new Vector2(800, 30);
        subtitle.anchoredPosition = new Vector2(0, -108);

        var cards = new GameModeCard[definitions.Length];
        float totalW = definitions.Length * CardWidth + (definitions.Length - 1) * CardGap;
        for (int i = 0; i < definitions.Length; i++)
        {
            float x = -totalW / 2f + CardWidth / 2f + i * (CardWidth + CardGap);
            cards[i] = BuildModeCard(modesView, $"Card_{i}", x);
        }
        playMenu.cards = cards;

        // =====================================================
        // VUE 2 : FICHE DETAIL (colonne de 1200 px centree)
        // En-tete (pictogramme, titre, accroche, description)
        // puis deux panneaux : Details (dont la carte d'exemple)
        // et Objectives. Plus d'image de couverture ni de carte.
        // =====================================================
        var details = CreateUI(screen, "DetailsView");
        StretchFull(details);
        const float left = -DetailWidth / 2f;

        var iconFrame = CreatePanel(details, "IconFrame", CoverPlaceholder);
        TopCenter(iconFrame, left, -40, 72, 72);
        var icon = CreatePanel(iconFrame, "Icon", TextDim);
        SetAnchors(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        icon.sizeDelta = new Vector2(40, 40);
        icon.anchoredPosition = Vector2.zero;
        icon.GetComponent<Image>().raycastTarget = false;
        playMenu.detailIcon = icon.GetComponent<Image>();

        var dTitle = CreateText(details, "Title", "Mode", 40, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        TopCenter(dTitle, left + 96, -36, DetailWidth - 96, 50);
        playMenu.detailTitle = dTitle.GetComponent<TMP_Text>();

        var dTagline = CreateText(details, "Tagline", "Tagline", 15, TextDim, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        TopCenter(dTagline, left + 96, -86, DetailWidth - 96, 28);
        playMenu.detailTagline = dTagline.GetComponent<TMP_Text>();

        var dDesc = CreateText(details, "Description", "Description", 15, TextBody, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        TopCenter(dDesc, left, -140, 760, 90);
        playMenu.detailDescription = dDesc.GetComponent<TMP_Text>();

        // ---------- Panneau DETAILS ----------
        var detailsPanel = CreatePanel(details, "DetailsPanel", PanelBg);
        TopCenter(detailsPanel, left, -250, DetailsPanelWidth, PanelHeight);

        CreateSectionTitle(detailsPanel, "Details");

        var rows = new (string label, string field)[]
        {
            ("Teams", nameof(PlayMenuController.detailTeams)),
            ("Duration", nameof(PlayMenuController.detailDuration)),
            ("Tickets", nameof(PlayMenuController.detailTickets)),
            ("Respawn", nameof(PlayMenuController.detailRespawn)),
            ("Team size", nameof(PlayMenuController.detailTeamSize)),
            ("Map", nameof(PlayMenuController.detailMapName)),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float y = -64f - i * 52f;
            var label = CreateText(detailsPanel, $"Row_{i}_Label", rows[i].label, 14, TextDim, TextAlignmentOptions.Left, FontStyles.Normal);
            SetAnchors(label, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            label.offsetMin = new Vector2(24, y - 44);
            label.offsetMax = new Vector2(-200, y);

            var value = CreateText(detailsPanel, $"Row_{i}_Value", "—", 15, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
            SetAnchors(value, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            value.offsetMin = new Vector2(140, y - 44);
            value.offsetMax = new Vector2(-24, y);

            typeof(PlayMenuController).GetField(rows[i].field)
                                      .SetValue(playMenu, value.GetComponent<TMP_Text>());
        }

        // ---------- Panneau OBJECTIFS ----------
        var objectivesPanel = CreatePanel(details, "ObjectivesPanel", PanelBg);
        TopCenter(objectivesPanel, left + DetailsPanelWidth + PanelGap, -250,
                  DetailWidth - DetailsPanelWidth - PanelGap, PanelHeight);

        CreateSectionTitle(objectivesPanel, "Objectives");

        var blueBox = CreatePanel(objectivesPanel, "ObjectivePrimary", BlueBoxBg);
        SetAnchors(blueBox, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        blueBox.offsetMin = new Vector2(24, -210);
        blueBox.offsetMax = new Vector2(-24, -64);
        var blueText = CreateText(blueBox, "Text", "Primary objective", 14.5f, TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        StretchFull(blueText);
        blueText.offsetMin = new Vector2(18, 12);
        blueText.offsetMax = new Vector2(-18, -12);
        playMenu.detailObjectivePrimary = blueText.GetComponent<TMP_Text>();

        var redBox = CreatePanel(objectivesPanel, "ObjectiveSecondary", RedBoxBg);
        SetAnchors(redBox, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        redBox.offsetMin = new Vector2(24, -372);
        redBox.offsetMax = new Vector2(-24, -226);
        var redText = CreateText(redBox, "Text", "Secondary objective", 14.5f, TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        StretchFull(redText);
        redText.offsetMin = new Vector2(18, 12);
        redText.offsetMax = new Vector2(-18, -12);
        playMenu.detailObjectiveSecondary = redText.GetComponent<TMP_Text>();

        // ---------- Boutons bas (alignes sur la colonne) ----------
        var (backBtn, _) = CreateButton(details, "BackButton", "Back", 15, CardBg, TextMain);
        var backRect = backBtn.GetComponent<RectTransform>();
        SetAnchors(backRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0));
        backRect.sizeDelta = new Vector2(160, 48);
        backRect.anchoredPosition = new Vector2(left, 36);
        playMenu.backButton = backBtn;

        var (playBtn, _) = CreateButton(details, "PlayButton", "Play", 18, Accent, BlockforgeTheme.OnAccent);
        var playRect = playBtn.GetComponent<RectTransform>();
        SetAnchors(playRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(1, 0));
        playRect.sizeDelta = new Vector2(340, 54);
        playRect.anchoredPosition = new Vector2(-left, 34);
        playMenu.playButton = playBtn;

        // ---------- Etats initiaux + cablage ----------
        playMenu.modesView = modesView.gameObject;
        playMenu.detailsView = details.gameObject;
        details.gameObject.SetActive(false);
        screen.gameObject.SetActive(false);

        if (mainController != null)
        {
            mainController.playMenu = playMenu;
            if (homeScreen != null)
                mainController.homeScreen = homeScreen.gameObject;
            EditorUtility.SetDirty(mainController);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        // Design system Blockforge : formes, etats, typo, libelles anglais
        DesignSystemApply.ApplyToOpenScene();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GameModesUISetup] Ecrans Modes de jeu + Details generes et cables.");
    }

    // =========================================================
    // CARTE DE MODE
    // =========================================================

    // Carte sans apercu : pictogramme dans un puits, titre, accroche,
    // lignes de chiffres en bas (alignees d'une carte a l'autre)
    private static GameModeCard BuildModeCard(RectTransform parent, string name, float x)
    {
        var card = CreatePanel(parent, name, CardBg);
        SetAnchors(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        card.sizeDelta = new Vector2(CardWidth, CardHeight);
        card.anchoredPosition = new Vector2(x, -20);

        var widget = card.gameObject.AddComponent<GameModeCard>();
        widget.button = card.gameObject.AddComponent<Button>();
        widget.button.targetGraphic = card.GetComponent<Image>();

        var iconFrame = CreatePanel(card, "IconFrame", CoverPlaceholder);
        SetAnchors(iconFrame, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        iconFrame.sizeDelta = new Vector2(64, 64);
        iconFrame.anchoredPosition = new Vector2(24, -24);
        iconFrame.GetComponent<Image>().raycastTarget = false;

        var icon = CreatePanel(iconFrame, "Icon", TextDim);
        SetAnchors(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        icon.sizeDelta = new Vector2(36, 36);
        icon.anchoredPosition = Vector2.zero;
        icon.GetComponent<Image>().raycastTarget = false;
        widget.iconImage = icon.GetComponent<Image>();

        var title = CreateText(card, "Title", "Mode", 21, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        title.offsetMin = new Vector2(24, -140);
        title.offsetMax = new Vector2(-24, -108);
        widget.titleText = title.GetComponent<TMP_Text>();

        var desc = CreateText(card, "Description", "Description", 13, TextDim, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        SetAnchors(desc, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        desc.offsetMin = new Vector2(24, -216);
        desc.offsetMax = new Vector2(-24, -146);
        widget.descriptionText = desc.GetComponent<TMP_Text>();

        // Chiffres du mode : deux lignes libelle / valeur
        var stats = CreateUI(card, "Stats");
        SetAnchors(stats, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        stats.offsetMin = new Vector2(24, 20);
        stats.offsetMax = new Vector2(-24, 84);
        widget.statsGroup = stats.gameObject;
        widget.teamsText = StatRow(stats, 0, "Teams");
        widget.durationText = StatRow(stats, 1, "Duration");

        var (configureBtn, _) = CreateButton(card, "ConfigureButton", "Configure", 13,
                                             BlockforgeTheme.SurfaceRaised, TextMain);
        var cfgRect = configureBtn.GetComponent<RectTransform>();
        SetAnchors(cfgRect, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
        cfgRect.sizeDelta = new Vector2(150, 36);
        cfgRect.anchoredPosition = new Vector2(24, 24);
        widget.configureButton = configureBtn;
        configureBtn.gameObject.SetActive(false);

        return widget;
    }

    // Ligne "libelle ........ valeur" dans le pied de carte (index 0 = haut)
    private static TMP_Text StatRow(RectTransform stats, int index, string label)
    {
        float top = -index * 32f;
        var labelRect = CreateText(stats, $"Row_{index}_Label", label, 13, TextDim, TextAlignmentOptions.Left, FontStyles.Normal);
        SetAnchors(labelRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        labelRect.offsetMin = new Vector2(0, top - 32);
        labelRect.offsetMax = new Vector2(-120, top);

        var valueRect = CreateText(stats, $"Row_{index}_Value", "—", 14, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
        SetAnchors(valueRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        valueRect.offsetMin = new Vector2(100, top - 32);
        valueRect.offsetMax = new Vector2(0, top);
        return valueRect.GetComponent<TMP_Text>();
    }

    // Ancre haut-centre, position du coin haut-gauche relative au centre
    private static void TopCenter(RectTransform rect, float x, float y, float width, float height)
    {
        SetAnchors(rect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1));
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private static void CreateSectionTitle(RectTransform panel, string text)
    {
        var title = CreateText(panel, "SectionTitle", text, 16, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        title.offsetMin = new Vector2(24, -52);
        title.offsetMax = new Vector2(-24, -14);
    }

    // =========================================================
    // DONNEES
    // =========================================================

    private static GameModeDefinition[] EnsureGameModes()
    {
        EnsureFolder(ModeDataDir);

        var defs = new List<GameModeDefinition>
        {
            EnsureMode("Mode_TeamClash", m =>
            {
                m.title = "Team Clash";
                m.tagline = "Two teams fight to destroy every enemy robot.";
                m.description = "Two teams of four robots meet head-on. Destroy every enemy robot to win " +
                                "the round. The first team to win three rounds wins the match.";
                m.teamsLabel = "4v4"; m.durationLabel = "10–15 min"; m.ticketsLabel = "—";
                m.respawnLabel = "No"; m.teamSizeLabel = "4"; m.mapName = "Dust Arena";
                m.objectivePrimary = "Destroy every robot on the enemy team.";
                m.objectiveSecondary = "Protect your allies: there is no respawn during a round.";
            }),
            EnsureMode("Mode_Control", m =>
            {
                m.title = "Control";
                m.tagline = "Capture and hold strategic points to win.";
                m.description = "Two teams fight for strategic points spread across the map. The more points " +
                                "your team holds, the more tickets the enemy loses. The first team to drain " +
                                "the other's tickets to zero wins.";
                m.teamsLabel = "4v4"; m.durationLabel = "10–15 min"; m.ticketsLabel = "1 000";
                m.respawnLabel = "Yes"; m.teamSizeLabel = "4"; m.mapName = "Outpost Valley";
                m.objectivePrimary = "Hold more points than the enemy to drain their tickets.";
                m.objectiveSecondary = "Destroy enemy robots to weaken their team.";
            }),
            EnsureMode("Mode_FreeForAll", m =>
            {
                m.title = "Free for All";
                m.tagline = "Every robot for itself. Be the last one standing.";
                m.description = "Eight robots, one rule: survive. No allies, no mercy. Take out everything " +
                                "that moves and be the last working robot to win the match.";
                m.teamsLabel = "8"; m.durationLabel = "10–15 min"; m.ticketsLabel = "—";
                m.respawnLabel = "No"; m.teamSizeLabel = "1"; m.mapName = "Scrapyard";
                m.objectivePrimary = "Be the last robot standing on the battlefield.";
                m.objectiveSecondary = "Every takedown earns you bonus resources.";
            }),
            EnsureMode("Mode_Custom", m =>
            {
                m.title = "Custom Match";
                m.tagline = "Create or join a custom match.";
                m.description = "Set up your own match: pick the map, the mode, the number of players and " +
                                "the rules. Invite your friends or open the match to everyone.";
                m.isCustom = true;
                m.teamsLabel = "Any"; m.durationLabel = "Any"; m.ticketsLabel = "Custom";
                m.respawnLabel = "Custom"; m.teamSizeLabel = "Any"; m.mapName = "Your choice";
                m.objectivePrimary = "Set your own match rules.";
                m.objectiveSecondary = "Invite your friends with a private match code.";
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
