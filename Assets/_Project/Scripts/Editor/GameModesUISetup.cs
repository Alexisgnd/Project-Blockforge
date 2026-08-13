using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// SETUP DES ECRANS "MODES DE JEU" + "DETAILS D'UN MODE"
// Menu : Blockforge > Setup MainMenu Play Screens
// A lancer APRES "Setup MainMenu UI" (il a besoin du
// MainCanvas et du MenuManager existants).
// =========================================================

public static class GameModesUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string ModeDataDir = "Assets/_Project/Data/GameModes";

    private static readonly Color ScreenBg = new(0.016f, 0.035f, 0.065f, 0.985f);
    private static readonly Color CardBg = new(0.06f, 0.10f, 0.16f, 0.95f);
    private static readonly Color PanelBg = new(0.05f, 0.08f, 0.13f, 0.9f);
    private static readonly Color CoverPlaceholder = new(0.10f, 0.16f, 0.24f, 1f);
    private static readonly Color Accent = new(0.23f, 0.51f, 0.96f);
    private static readonly Color BlueBoxBg = new(0.09f, 0.16f, 0.30f, 0.95f);
    private static readonly Color RedBoxBg = new(0.30f, 0.09f, 0.10f, 0.95f);
    private static readonly Color TextMain = new(0.86f, 0.91f, 0.96f);
    private static readonly Color TextDim = new(0.48f, 0.56f, 0.66f);
    private static readonly Color TextBody = new(0.70f, 0.77f, 0.85f);

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
        // VUE 1 : LISTE DES MODES
        // =====================================================
        var modesView = CreateUI(screen, "ModesView");
        StretchFull(modesView);

        var title = CreateText(modesView, "Title", "MODES DE JEU", 42, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        SetAnchors(title, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        title.sizeDelta = new Vector2(800, 60);
        title.anchoredPosition = new Vector2(0, -50);

        var subtitle = CreateText(modesView, "Subtitle", "Choisissez votre mode de jeu", 16,
                                  new Color(0.45f, 0.62f, 0.85f), TextAlignmentOptions.Center, FontStyles.Normal);
        SetAnchors(subtitle, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
        subtitle.sizeDelta = new Vector2(800, 30);
        subtitle.anchoredPosition = new Vector2(0, -108);

        var cards = new GameModeCard[definitions.Length];
        float cardW = 300f, spacing = 40f;
        float totalW = definitions.Length * cardW + (definitions.Length - 1) * spacing;
        for (int i = 0; i < definitions.Length; i++)
        {
            float x = -totalW / 2f + cardW / 2f + i * (cardW + spacing);
            cards[i] = BuildModeCard(modesView, $"Card_{definitions[i].title}", x);
        }
        playMenu.cards = cards;

        // =====================================================
        // VUE 2 : FICHE DETAIL
        // =====================================================
        var details = CreateUI(screen, "DetailsView");
        StretchFull(details);

        // Header gauche
        var icon = CreatePanel(details, "Icon", Accent);
        SetAnchors(icon, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        icon.sizeDelta = new Vector2(56, 56);
        icon.anchoredPosition = new Vector2(80, -50);
        playMenu.detailIcon = icon.GetComponent<Image>();

        var dTitle = CreateText(details, "Title", "CONTROL", 40, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(dTitle, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        dTitle.sizeDelta = new Vector2(560, 50);
        dTitle.anchoredPosition = new Vector2(160, -46);
        playMenu.detailTitle = dTitle.GetComponent<TMP_Text>();

        var dTagline = CreateText(details, "Tagline", "Tagline", 15, TextDim, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        SetAnchors(dTagline, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        dTagline.sizeDelta = new Vector2(540, 44);
        dTagline.anchoredPosition = new Vector2(162, -100);
        playMenu.detailTagline = dTagline.GetComponent<TMP_Text>();

        var dDesc = CreateText(details, "Description", "Description", 15, TextBody, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        SetAnchors(dDesc, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        dDesc.sizeDelta = new Vector2(640, 150);
        dDesc.anchoredPosition = new Vector2(80, -165);
        playMenu.detailDescription = dDesc.GetComponent<TMP_Text>();

        // Grande image (droite)
        var cover = CreatePanel(details, "Cover", CoverPlaceholder);
        SetAnchors(cover, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        cover.sizeDelta = new Vector2(640, 300);
        cover.anchoredPosition = new Vector2(-80, -40);
        playMenu.detailCover = cover.GetComponent<Image>();

        // ---------- Panneau DETAILS ----------
        var detailsPanel = CreatePanel(details, "DetailsPanel", PanelBg);
        SetAnchors(detailsPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        detailsPanel.sizeDelta = new Vector2(430, 390);
        detailsPanel.anchoredPosition = new Vector2(80, -370);

        CreateSectionTitle(detailsPanel, "DÉTAILS");

        var rows = new (string label, string field)[]
        {
            ("ÉQUIPES", nameof(PlayMenuController.detailTeams)),
            ("DURÉE", nameof(PlayMenuController.detailDuration)),
            ("NOMBRE DE TICKETS", nameof(PlayMenuController.detailTickets)),
            ("RÉAPPARITION", nameof(PlayMenuController.detailRespawn)),
            ("TAILLE DE L'ÉQUIPE", nameof(PlayMenuController.detailTeamSize)),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float y = -64f - i * 60f;
            var label = CreateText(detailsPanel, $"Row_{i}_Label", rows[i].label, 14, TextDim, TextAlignmentOptions.Left, FontStyles.Normal);
            SetAnchors(label, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            label.offsetMin = new Vector2(24, y - 44);
            label.offsetMax = new Vector2(-150, y);

            var value = CreateText(detailsPanel, $"Row_{i}_Value", "—", 15, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
            SetAnchors(value, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            value.offsetMin = new Vector2(24, y - 44);
            value.offsetMax = new Vector2(-24, y);

            typeof(PlayMenuController).GetField(rows[i].field)
                                      .SetValue(playMenu, value.GetComponent<TMP_Text>());
        }

        // ---------- Panneau OBJECTIFS ----------
        var objectivesPanel = CreatePanel(details, "ObjectivesPanel", PanelBg);
        SetAnchors(objectivesPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        objectivesPanel.sizeDelta = new Vector2(510, 390);
        objectivesPanel.anchoredPosition = new Vector2(560, -370);

        CreateSectionTitle(objectivesPanel, "OBJECTIFS");

        var blueBox = CreatePanel(objectivesPanel, "ObjectivePrimary", BlueBoxBg);
        SetAnchors(blueBox, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        blueBox.offsetMin = new Vector2(24, -184);
        blueBox.offsetMax = new Vector2(-24, -64);
        var blueText = CreateText(blueBox, "Text", "Objectif principal", 14.5f, TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        StretchFull(blueText);
        blueText.offsetMin = new Vector2(18, 12);
        blueText.offsetMax = new Vector2(-18, -12);
        playMenu.detailObjectivePrimary = blueText.GetComponent<TMP_Text>();

        var redBox = CreatePanel(objectivesPanel, "ObjectiveSecondary", RedBoxBg);
        SetAnchors(redBox, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        redBox.offsetMin = new Vector2(24, -324);
        redBox.offsetMax = new Vector2(-24, -204);
        var redText = CreateText(redBox, "Text", "Objectif secondaire", 14.5f, TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        StretchFull(redText);
        redText.offsetMin = new Vector2(18, 12);
        redText.offsetMax = new Vector2(-18, -12);
        playMenu.detailObjectiveSecondary = redText.GetComponent<TMP_Text>();

        // ---------- Panneau CARTE EXEMPLE ----------
        var mapPanel = CreatePanel(details, "MapPanel", PanelBg);
        SetAnchors(mapPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        mapPanel.sizeDelta = new Vector2(430, 390);
        mapPanel.anchoredPosition = new Vector2(1120, -370);

        CreateSectionTitle(mapPanel, "CARTE EXEMPLE");

        var mapImage = CreatePanel(mapPanel, "MapImage", CoverPlaceholder);
        SetAnchors(mapImage, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        mapImage.offsetMin = new Vector2(24, -344);
        mapImage.offsetMax = new Vector2(-24, -64);
        playMenu.detailMapImage = mapImage.GetComponent<Image>();

        var mapName = CreateText(mapImage, "MapName", "Outpost Valley", 16, TextMain, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        StretchFull(mapName);
        mapName.offsetMin = new Vector2(16, 12);
        mapName.offsetMax = new Vector2(-16, -12);
        playMenu.detailMapName = mapName.GetComponent<TMP_Text>();

        // ---------- Boutons bas ----------
        var (backBtn, _) = CreateButton(details, "BackButton", "RETOUR", 15, CardBg, TextMain);
        var backRect = backBtn.GetComponent<RectTransform>();
        SetAnchors(backRect, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
        backRect.sizeDelta = new Vector2(160, 48);
        backRect.anchoredPosition = new Vector2(80, 36);
        playMenu.backButton = backBtn;

        var (playBtn, _) = CreateButton(details, "PlayButton", "JOUER", 18, Accent, Color.white);
        var playRect = playBtn.GetComponent<RectTransform>();
        SetAnchors(playRect, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
        playRect.sizeDelta = new Vector2(340, 54);
        playRect.anchoredPosition = new Vector2(-80, 34);
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
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GameModesUISetup] Ecrans Modes de jeu + Details generes et cables.");
    }

    // =========================================================
    // CARTE DE MODE
    // =========================================================

    private static GameModeCard BuildModeCard(RectTransform parent, string name, float x)
    {
        var card = CreatePanel(parent, name, CardBg);
        SetAnchors(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        card.sizeDelta = new Vector2(300, 480);
        card.anchoredPosition = new Vector2(x, -30);

        var widget = card.gameObject.AddComponent<GameModeCard>();
        widget.button = card.gameObject.AddComponent<Button>();
        widget.button.targetGraphic = card.GetComponent<Image>();

        var cover = CreatePanel(card, "Cover", CoverPlaceholder);
        SetAnchors(cover, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        cover.offsetMin = new Vector2(12, -222);
        cover.offsetMax = new Vector2(-12, -12);
        widget.coverImage = cover.GetComponent<Image>();

        var icon = CreatePanel(cover, "Icon", new Color(0.09f, 0.16f, 0.28f, 0.95f));
        SetAnchors(icon, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        icon.sizeDelta = new Vector2(46, 46);
        icon.anchoredPosition = new Vector2(14, -14);
        widget.iconImage = icon.GetComponent<Image>();

        var title = CreateText(card, "Title", "MODE", 21, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        SetAnchors(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        title.offsetMin = new Vector2(12, -272);
        title.offsetMax = new Vector2(-12, -234);
        widget.titleText = title.GetComponent<TMP_Text>();

        var desc = CreateText(card, "Description", "Description", 13, TextDim, TextAlignmentOptions.Top, FontStyles.Normal);
        SetAnchors(desc, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        desc.offsetMin = new Vector2(20, -386);
        desc.offsetMax = new Vector2(-20, -282);
        widget.descriptionText = desc.GetComponent<TMP_Text>();

        var footer = CreateText(card, "Footer", "4v4   |   10-15 min", 14, TextDim, TextAlignmentOptions.Center, FontStyles.Normal);
        SetAnchors(footer, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        footer.offsetMin = new Vector2(12, 18);
        footer.offsetMax = new Vector2(-12, 52);
        widget.footerText = footer.GetComponent<TMP_Text>();

        var (configureBtn, _) = CreateButton(card, "ConfigureButton", "CONFIGURER", 13,
                                             new Color(0.10f, 0.16f, 0.25f, 1f), TextMain);
        var cfgRect = configureBtn.GetComponent<RectTransform>();
        SetAnchors(cfgRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        cfgRect.sizeDelta = new Vector2(190, 42);
        cfgRect.anchoredPosition = new Vector2(0, 18);
        widget.configureButton = configureBtn;
        configureBtn.gameObject.SetActive(false);

        return widget;
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
                m.title = "TEAM CLASH";
                m.tagline = "Deux équipes s'affrontent pour détruire tous les robots ennemis.";
                m.description = "Deux équipes de quatre robots s'affrontent en combat direct. " +
                                "Éliminez tous les robots adverses pour remporter la manche. " +
                                "La première équipe à remporter trois manches gagne la partie.";
                m.teamsLabel = "4v4"; m.durationLabel = "10-15 min"; m.ticketsLabel = "—";
                m.respawnLabel = "Non"; m.teamSizeLabel = "4"; m.mapName = "Dust Arena";
                m.objectivePrimary = "Détruisez tous les robots de l'équipe adverse.";
                m.objectiveSecondary = "Protégez vos alliés : aucune réapparition en cours de manche.";
            }),
            EnsureMode("Mode_Control", m =>
            {
                m.title = "CONTROL";
                m.tagline = "Contrôlez et conservez les points stratégiques pour gagner.";
                m.description = "Deux équipes s'affrontent pour prendre le contrôle de points stratégiques " +
                                "répartis sur la carte. Plus votre équipe contrôle de points, plus l'ennemi " +
                                "perd des tickets. L'équipe qui réduit les tickets adverses à zéro remporte la partie.";
                m.teamsLabel = "4v4"; m.durationLabel = "10-15 min"; m.ticketsLabel = "1 000";
                m.respawnLabel = "Oui"; m.teamSizeLabel = "4"; m.mapName = "Outpost Valley";
                m.objectivePrimary = "Contrôlez plus de points que l'ennemi pour le priver de tickets.";
                m.objectiveSecondary = "Détruisez les robots ennemis pour réduire leur force.";
            }),
            EnsureMode("Mode_FreeForAll", m =>
            {
                m.title = "FREE FOR ALL";
                m.tagline = "Chacun pour soi. Soyez le dernier robot en vie !";
                m.description = "Huit robots, une seule règle : survivre. Pas d'alliés, pas de pitié. " +
                                "Éliminez tout ce qui bouge et soyez le dernier robot opérationnel " +
                                "pour remporter la partie.";
                m.teamsLabel = "8"; m.durationLabel = "10-15 min"; m.ticketsLabel = "—";
                m.respawnLabel = "Non"; m.teamSizeLabel = "1"; m.mapName = "Scrapyard";
                m.objectivePrimary = "Soyez le dernier robot en vie sur le champ de bataille.";
                m.objectiveSecondary = "Chaque élimination vous rapporte des ressources bonus.";
            }),
            EnsureMode("Mode_Custom", m =>
            {
                m.title = "CUSTOM MATCH";
                m.tagline = "Créez ou rejoignez une partie personnalisée.";
                m.description = "Configurez votre propre partie : choisissez la carte, le mode, " +
                                "le nombre de joueurs et les règles. Invitez vos amis ou ouvrez " +
                                "la partie à tous.";
                m.isCustom = true;
                m.teamsLabel = "Libre"; m.durationLabel = "Libre"; m.ticketsLabel = "Configurable";
                m.respawnLabel = "Configurable"; m.teamSizeLabel = "Libre"; m.mapName = "Au choix";
                m.objectivePrimary = "Définissez vos propres règles de partie.";
                m.objectiveSecondary = "Invitez vos amis avec un code de partie privée.";
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
