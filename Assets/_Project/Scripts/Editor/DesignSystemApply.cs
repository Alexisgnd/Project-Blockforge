using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// =========================================================
// APPLIQUER LE DESIGN SYSTEM BLOCKFORGE AUX SCENES
// Menu : Blockforge > Apply Design System
// Source : Branding/blockforge-design-system.zip (README,
// tokens.json, composants). Pour MainMenu et Garage :
// - pose un DSElement (role du composant DS : Button primary /
//   secondary / ghost / danger, Tabs nav / segment, Panel,
//   Dialog, RobotSlot, PartCard, Field, KeyHint, ToolRail...)
//   qui porte formes arrondies, bordures, anneau de focus et
//   etats (survol, focus, presse, selectionne, desactive) ;
// - applique les styles de texte (Barlow / Barlow Condensed /
//   mono pour les chiffres) et les encres du DS ;
// - passe les libelles en anglais, sentence case (le style
//   met en capitales ce qui doit l'etre) ;
// - recale tailles et espacements sur les jetons (control-md,
//   control-lg, space-*, tile-md) ;
// - ajoute logo et pictogrammes du DS.
// Relancable sans effet de bord. Les setups d'UI l'appellent
// a la fin (ApplyToOpenScene) : une UI regeneree reste au DS.
// =========================================================

public static class DesignSystemApply
{
    private static Transform root;
    private static readonly List<string> missing = new();
    private static int elementCount, textCount;

    [MenuItem("Blockforge/Apply Design System")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureAssets();
        var summary = new List<string>();
        foreach (var path in new[] { BlockforgeScenes.MainMenu, BlockforgeScenes.Garage })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ApplyToOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            summary.Add($"{scene.name} : {elementCount} composant(s), {textCount} texte(s)");
        }

        Debug.Log("[DesignSystem] Design system applique et scenes sauvegardees.\n" + string.Join("\n", summary));
    }

    // Polices TMP (generees une fois) ; les sprites sont regles par DesignSystemAssetImporter
    public static void EnsureAssets()
    {
        if (!DesignSystemFonts.AllPresent())
            DesignSystemFonts.Build(false);
        DSAssets.ClearCache();
    }

    // Scene deja ouverte (appele par les setups d'UI)
    public static void ApplyToOpenScene()
    {
        EnsureAssets();
        missing.Clear();
        elementCount = textCount = 0;

        var mainCanvas = GameObject.Find("MainCanvas");
        if (mainCanvas != null)
        {
            root = mainCanvas.transform;
            ApplyMainMenu();
        }

        var garageCanvas = GameObject.Find("GarageCanvas");
        if (garageCanvas != null)
        {
            root = garageCanvas.transform;
            ApplyGarage();
        }

        // Tout texte TMP restant (hors nuancier) passe au moins en Barlow
        foreach (var canvas in new[] { mainCanvas, garageCanvas })
        {
            if (canvas == null)
                continue;
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.GetComponentInParent<PaintSprayColorWheel>(true) != null)
                    continue;
                var regular = DSAssets.Font(DSFont.Regular);
                if (text.font != null && text.font.name.StartsWith("LiberationSans") && regular != null)
                {
                    text.font = regular;
                    text.fontSharedMaterial = regular.material;
                    text.fontStyle &= ~FontStyles.Bold;
                    EditorUtility.SetDirty(text);
                }
            }
        }

        if (missing.Count > 0)
            Debug.LogWarning("[DesignSystem] Elements introuvables (ignores) :\n" + string.Join("\n", missing.Distinct()));
    }

    // =========================================================
    // ACCUEIL, JOUER, CONSTRUIRE (MainMenu)
    // =========================================================

    private static readonly (string key, string label)[] NavLabels =
    {
        ("ACCUEIL", "Home"), ("JOUER", "Play"), ("CONSTRUIRE", "Build"),
        ("INVENTAIRE", "Inventory"), ("ARBRE DE TECHNOLOGIE", "Tech tree"), ("WORKSHOP", "Workshop"),
    };

    private static void ApplyMainMenu()
    {
        // ---------- Navigation du haut (Tabs nav sur un bandeau surface-raised) ----------
        var nav = F("TopNavigation");
        El(nav, DSRole.Strip, null, e => e.hairlineEdge = DSEdge.Bottom);

        var logoText = F("TopNavigation/LogoText");
        if (logoText != null)
            logoText.gameObject.SetActive(false);
        if (nav != null)
        {
            // Lockup horizontal : jamais sous 180 px de large
            var logo = Child(nav, "Logo");
            var logoImage = GetOrAdd<Image>(logo.gameObject);
            logoImage.sprite = DSAssets.Logo();
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;
            logoImage.color = Color.white;
            Place(logo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(BlockforgeTheme.Space8, 0), new Vector2(190, 52));
        }

        var navButtons = F("TopNavigation/NavButtons");
        if (navButtons != null)
        {
            var navRect = (RectTransform)navButtons;
            navRect.offsetMin = new Vector2(BlockforgeTheme.Space8 + 190 + BlockforgeTheme.Space8, navRect.offsetMin.y);
            var layout = navButtons.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = BlockforgeTheme.Space2;
                layout.childAlignment = TextAnchor.MiddleLeft;
            }
            foreach (var (key, label) in NavLabels)
            {
                var tab = navButtons.Find("Nav_" + key);
                if (tab == null)
                    continue;
                var text = tab.GetComponentInChildren<TMP_Text>(true);
                Txt(text, DSTextStyle.Label, BlockforgeTheme.InkSecondary, label, TextAlignmentOptions.Center);
                var el = El(tab, DSRole.TabNav, new[] { text });
                if (el != null)
                    el.Selected = key == "ACCUEIL";
                var le = GetOrAdd<LayoutElement>(tab.gameObject);
                le.preferredWidth = PrefWidth(text) + 2 * BlockforgeTheme.Space3;
            }
        }

        var playerName = T("TopNavigation/PlayerName");
        Txt(playerName, DSTextStyle.TitleSM, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.Right);
        Offset(playerName, x: -BlockforgeTheme.Space8);
        var playerLevel = T("TopNavigation/PlayerLevel");
        Txt(playerLevel, DSTextStyle.Caption, BlockforgeTheme.InkMuted, "Level 1", TextAlignmentOptions.Right);
        Offset(playerLevel, x: -BlockforgeTheme.Space8);

        ApplyHome();
        ApplyPlayScreen();
        ApplyBuildScreen();

        // Onglets : couleurs portees par DSElement (le controleur regle Selected)
        var controller = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (controller != null)
            EditorUtility.SetDirty(controller);
    }

    private static readonly string[] HomeStatLabels =
        { "CPU", "Firepower", "Health", "Speed", "Energy use", "Mass", "Parts" };

    private static void ApplyHome()
    {
        float top = 80f + BlockforgeTheme.Space8; // sous la navigation

        // Titre d'ecran : la baie (display-xl, une seule fois) puis le robot (title-lg)
        var bay = T("HomeScreen/BayLabel");
        Txt(bay, DSTextStyle.DisplayXL, BlockforgeTheme.InkPrimary, "Hangar", TextAlignmentOptions.TopLeft);
        Place(bay, new Vector2(0, 1), new Vector2(0, 1), new Vector2(BlockforgeTheme.Space8, -top), new Vector2(600, 50), new Vector2(0, 1));
        var robotName = T("HomeScreen/RobotName");
        Txt(robotName, DSTextStyle.TitleLG, BlockforgeTheme.InkSecondary, "Loading…", TextAlignmentOptions.TopLeft);
        Place(robotName, new Vector2(0, 1), new Vector2(0, 1), new Vector2(BlockforgeTheme.Space8, -top - 54), new Vector2(600, 28), new Vector2(0, 1));

        // Bloc de stats compact a droite (panneau flottant sur la vue 3D)
        var stats = F("HomeScreen/RobotStats");
        if (stats != null)
        {
            const float rowStep = 20f + BlockforgeTheme.Space4;
            float contentTop = BlockforgeTheme.Space4 + 26f + BlockforgeTheme.Space5;
            float height = contentTop + HomeStatLabels.Length * rowStep - BlockforgeTheme.Space4 + BlockforgeTheme.Space4;
            El(stats, DSRole.PanelFloating);
            Place(stats, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-BlockforgeTheme.Space8, -top), new Vector2(340, height), new Vector2(1, 1));

            var title = T("HomeScreen/RobotStats/Title");
            Txt(title, DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Stats", TextAlignmentOptions.TopLeft);
            Row(title, BlockforgeTheme.Space4, 26f, BlockforgeTheme.Space4, BlockforgeTheme.Space4);

            for (int i = 0; i < HomeStatLabels.Length; i++)
            {
                float y = contentTop + i * rowStep;
                var label = T($"HomeScreen/RobotStats/Stat_{i}_Label");
                var value = T($"HomeScreen/RobotStats/Stat_{i}_Value");
                Txt(label, DSTextStyle.Caption, BlockforgeTheme.InkSecondary, HomeStatLabels[i], TextAlignmentOptions.MidlineLeft);
                Txt(value, DSTextStyle.ReadoutMD, BlockforgeTheme.InkPrimary, "–", TextAlignmentOptions.MidlineRight);
                Row(label, y, 20f, BlockforgeTheme.Space4, 150f);
                Row(value, y, 20f, BlockforgeTheme.Space4, BlockforgeTheme.Space4);
            }
        }

        // Baies : bande en bas, une par slot (RobotSlot 176 px, radius-lg)
        const float slotW = 176f, slotH = 132f, gap = BlockforgeTheme.Space3;
        var slots = F("HomeScreen/RobotSlots");
        var manager = Object.FindAnyObjectByType<RobotSlotManager>(FindObjectsInactive.Include);
        if (slots != null)
        {
            Place(slots, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 72f + BlockforgeTheme.Space8),
                  new Vector2(3 * slotW + 2 * gap, slotH), new Vector2(0.5f, 0));
            var statLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = slots.Find($"Slot0{i + 1}");
                if (slot == null)
                {
                    missing.Add($"HomeScreen/RobotSlots/Slot0{i + 1}");
                    continue;
                }
                El(slot, DSRole.RobotSlot, new[] { slot.Find("Name")?.GetComponent<TMP_Text>() });
                Place(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * (slotW + gap), 0),
                      new Vector2(slotW, slotH));

                float pad = BlockforgeTheme.Space3;
                var bayLabel = slot.Find("Tier")?.GetComponent<TMP_Text>();
                Txt(bayLabel, DSTextStyle.Label, BlockforgeTheme.InkMuted, $"Bay {i + 1:00}", TextAlignmentOptions.TopLeft);
                Place(bayLabel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -pad), new Vector2(-2 * pad, 14), new Vector2(0.5f, 1));

                var name = slot.Find("Name")?.GetComponent<TMP_Text>();
                Txt(name, DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Robot", TextAlignmentOptions.TopLeft);
                Place(name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -pad - 14 - BlockforgeTheme.Space1), new Vector2(-2 * pad, 60), new Vector2(0.5f, 1));
                if (name != null)
                {
                    name.textWrappingMode = TextWrappingModes.Normal;
                    name.overflowMode = TextOverflowModes.Ellipsis;
                    name.maxVisibleLines = 2;
                }

                var statsText = Child(slot, "Stats").gameObject;
                var stat = GetOrAdd<TextMeshProUGUI>(statsText);
                stat.raycastTarget = false;
                Txt(stat, DSTextStyle.ReadoutSM, BlockforgeTheme.InkSecondary, "", TextAlignmentOptions.BottomLeft);
                Place(stat, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, pad), new Vector2(-2 * pad, 16), new Vector2(0.5f, 0));
                statLabels[i] = stat;

                // La selection passe par surface-selected + border-accent : plus de badge
                var badge = slot.Find("SelectedBadge");
                if (badge != null)
                    Undo.DestroyObjectImmediate(badge.gameObject);
            }

            if (manager != null)
            {
                Undo.RecordObject(manager, "Design System");
                manager.slotStatLabels = statLabels;
                manager.selectedBadges = new GameObject[3];
                EditorUtility.SetDirty(manager);
            }
        }

        // Barre d'actions du bas : un seul bouton principal (Play)
        var bar = F("HomeScreen/BottomBar");
        El(bar, DSRole.Strip, null, e => e.hairlineEdge = DSEdge.Top);
        float x = BlockforgeTheme.Space8;
        x = BarButton("HomeScreen/BottomBar/PlayButton", DSRole.ButtonPrimary, DSTextStyle.ButtonLG, "Play", x, BlockforgeTheme.ControlLg);
        x = BarButton("HomeScreen/BottomBar/BuildButton", DSRole.ButtonSecondary, DSTextStyle.ButtonMD, "Build", x, BlockforgeTheme.ControlMd);
        BarButton("HomeScreen/BottomBar/RobotSettingsButton", DSRole.ButtonGhost, DSTextStyle.ButtonMD, "Robot settings", x, BlockforgeTheme.ControlMd);
    }

    private static float BarButton(string path, DSRole role, DSTextStyle style, string label, float x, float height)
    {
        var button = F(path);
        if (button == null)
            return x;
        var text = button.GetComponentInChildren<TMP_Text>(true);
        Txt(text, style, InkFor(role), label, TextAlignmentOptions.Center);
        El(button, role, new[] { text });
        float padX = height >= BlockforgeTheme.ControlLg ? BlockforgeTheme.Space6 : BlockforgeTheme.Space4;
        float width = PrefWidth(text) + 2 * padX;
        Place(button, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(width, height), new Vector2(0, 0.5f));
        return x + width + BlockforgeTheme.Space2;
    }

    private static readonly (string card, string icon)[] ModeIcons =
    {
        ("Card_TEAM CLASH", "group"), ("Card_CONTROL", "objective"), ("Card_FREE FOR ALL", "skull"), ("Card_CUSTOM MATCH", "settings"),
    };

    private static void ApplyPlayScreen()
    {
        var screen = F("PlayScreen");
        if (screen == null)
            return;
        SetImage(screen, BlockforgeTheme.SurfaceBase);

        Txt(T("PlayScreen/ModesView/Title"), DSTextStyle.DisplayXL, BlockforgeTheme.InkPrimary, "Game modes");
        Txt(T("PlayScreen/ModesView/Subtitle"), DSTextStyle.Body, BlockforgeTheme.InkSecondary, "Choose how you want to play.");

        foreach (var (cardName, icon) in ModeIcons)
        {
            var card = F($"PlayScreen/ModesView/{cardName}");
            if (card == null)
                continue;
            El(card, DSRole.Card);
            var cover = card.Find("Cover");
            El(cover, DSRole.Inset);
            var iconImage = cover != null ? cover.Find("Icon")?.GetComponent<Image>() : null;
            SetIcon(iconImage, icon, BlockforgeTheme.InkSecondary, 40f);
            Txt(card.Find("Title")?.GetComponent<TMP_Text>(), DSTextStyle.TitleMD, BlockforgeTheme.InkPrimary);
            Txt(card.Find("Description")?.GetComponent<TMP_Text>(), DSTextStyle.BodySM, BlockforgeTheme.InkSecondary);
            Txt(card.Find("Footer")?.GetComponent<TMP_Text>(), DSTextStyle.ReadoutSM, BlockforgeTheme.InkMuted);
            var configure = card.Find("ConfigureButton");
            if (configure != null)
            {
                var text = configure.GetComponentInChildren<TMP_Text>(true);
                Txt(text, DSTextStyle.ButtonSM, BlockforgeTheme.InkPrimary, "Configure", TextAlignmentOptions.Center);
                El(configure, DSRole.ButtonSecondary, new[] { text });
                Resize(configure, PrefWidth(text) + 2 * BlockforgeTheme.Space3, BlockforgeTheme.ControlSm);
            }
        }

        // Fiche d'un mode
        const string d = "PlayScreen/DetailsView/";
        SetIcon(F(d + "Icon")?.GetComponent<Image>(), "objective", BlockforgeTheme.InkSecondary, 0f);
        Txt(T(d + "Title"), DSTextStyle.DisplayXL, BlockforgeTheme.InkPrimary);
        Txt(T(d + "Tagline"), DSTextStyle.Body, BlockforgeTheme.InkSecondary);
        Txt(T(d + "Description"), DSTextStyle.Body, BlockforgeTheme.InkSecondary);
        El(F(d + "Cover"), DSRole.Inset);

        El(F(d + "DetailsPanel"), DSRole.Panel);
        Txt(T(d + "DetailsPanel/SectionTitle"), DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Details");
        string[] rows = { "Teams", "Duration", "Tickets", "Respawn", "Team size" };
        for (int i = 0; i < rows.Length; i++)
        {
            Txt(T($"{d}DetailsPanel/Row_{i}_Label"), DSTextStyle.Caption, BlockforgeTheme.InkSecondary, rows[i]);
            Txt(T($"{d}DetailsPanel/Row_{i}_Value"), DSTextStyle.ReadoutMD, BlockforgeTheme.InkPrimary);
        }

        El(F(d + "ObjectivesPanel"), DSRole.Panel);
        Txt(T(d + "ObjectivesPanel/SectionTitle"), DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Objectives");
        El(F(d + "ObjectivesPanel/ObjectivePrimary"), DSRole.Inset);
        Txt(T(d + "ObjectivesPanel/ObjectivePrimary/Text"), DSTextStyle.Body, BlockforgeTheme.InkPrimary);
        El(F(d + "ObjectivesPanel/ObjectiveSecondary"), DSRole.Inset);
        Txt(T(d + "ObjectivesPanel/ObjectiveSecondary/Text"), DSTextStyle.Body, BlockforgeTheme.InkSecondary);

        El(F(d + "MapPanel"), DSRole.Panel);
        Txt(T(d + "MapPanel/SectionTitle"), DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Sample map");
        El(F(d + "MapPanel/MapImage"), DSRole.Inset);
        Txt(T(d + "MapPanel/MapImage/MapName"), DSTextStyle.TitleMD, BlockforgeTheme.InkPrimary);

        DialogButton(F(d + "BackButton"), DSRole.ButtonGhost, DSTextStyle.ButtonMD, "Back", BlockforgeTheme.ControlMd);
        DialogButton(F(d + "PlayButton"), DSRole.ButtonPrimary, DSTextStyle.ButtonLG, "Play", BlockforgeTheme.ControlLg);
    }

    private static void ApplyBuildScreen()
    {
        var screen = F("BuildScreen");
        if (screen == null)
            return;
        SetImage(screen, BlockforgeTheme.SurfaceBase);

        // L'accent est reserve aux actions et a la selection
        var accentBar = F("BuildScreen/TitleAccent");
        if (accentBar != null)
            accentBar.gameObject.SetActive(false);
        var title = T("BuildScreen/Title");
        Txt(title, DSTextStyle.DisplayXL, BlockforgeTheme.InkPrimary, "Choose a bay", TextAlignmentOptions.TopLeft);
        Place(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(BlockforgeTheme.Space8, -BlockforgeTheme.Space8), new Vector2(900, 50), new Vector2(0, 1));
        var subtitle = T("BuildScreen/Subtitle");
        Txt(subtitle, DSTextStyle.Body, BlockforgeTheme.InkSecondary, "Edit a robot or build a new one in a free bay.", TextAlignmentOptions.TopLeft);
        Place(subtitle, new Vector2(0, 1), new Vector2(0, 1), new Vector2(BlockforgeTheme.Space8, -BlockforgeTheme.Space8 - 54), new Vector2(900, 22), new Vector2(0, 1));

        var used = F("BuildScreen/UsedBadge");
        if (used != null)
        {
            var text = used.GetComponentInChildren<TMP_Text>(true);
            Txt(text, DSTextStyle.Label, BlockforgeTheme.InkSecondary, "0 / 3 bays used", TextAlignmentOptions.Center);
            El(used, DSRole.Badge, new[] { text });
            Place(used, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-BlockforgeTheme.Space8, -BlockforgeTheme.Space8 - 10),
                  new Vector2(PrefWidth(text) + 2 * BlockforgeTheme.Space3, BlockforgeTheme.ControlSm), new Vector2(1, 1));
        }
        var max = F("BuildScreen/MaxSlots");
        if (max != null)
            max.gameObject.SetActive(false);

        for (int i = 1; i <= 3; i++)
        {
            var slot = F($"BuildScreen/Slot0{i}");
            if (slot == null)
                continue;
            El(slot, DSRole.Panel);
            float w = ((RectTransform)slot).sizeDelta.x;
            float pad = BlockforgeTheme.Space4;

            var number = slot.Find("Number");
            if (number != null)
            {
                var numberText = number.GetComponentInChildren<TMP_Text>(true);
                Txt(numberText, DSTextStyle.Label, BlockforgeTheme.InkSecondary, $"Bay {i:00}", TextAlignmentOptions.Center);
                El(number, DSRole.Badge, new[] { numberText });
                Place(number, new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -pad), new Vector2(64, 24), new Vector2(0, 1));
            }
            var name = slot.Find("Title")?.GetComponent<TMP_Text>();
            Txt(name, DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Free bay", TextAlignmentOptions.TopLeft);
            Place(name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -pad - 24 - BlockforgeTheme.Space2), new Vector2(-2 * pad, 32), new Vector2(0.5f, 1));
            if (name != null)
            {
                // Une ligne, coupee par une ellipse (TMP masque une ligne qui depasse en hauteur)
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
            }

            var filled = slot.Find("FilledGroup");
            if (filled != null)
            {
                var tier = filled.Find("Tier")?.GetComponent<TMP_Text>();
                Txt(tier, DSTextStyle.ReadoutSM, BlockforgeTheme.InkSecondary, null, TextAlignmentOptions.TopLeft);
                Row(tier, 84f, 16f, pad, pad);
                var cover = filled.Find("Cover");
                El(cover, DSRole.Inset);
                Row(cover, 108f, 192f, pad, pad);
                string[] labels = { "Health", "Speed", "Energy", "Mass" };
                for (int s = 0; s < labels.Length; s++)
                {
                    Txt(filled.Find($"Stat_{s}_Label")?.GetComponent<TMP_Text>(), DSTextStyle.Caption, BlockforgeTheme.InkSecondary, labels[s]);
                    Txt(filled.Find($"Stat_{s}_Value")?.GetComponent<TMP_Text>(), DSTextStyle.ReadoutMD, BlockforgeTheme.InkPrimary);
                }

                // Edit (secondaire : trois cartes cote a cote, aucune n'est LA action de l'ecran)
                var modify = filled.Find("ModifyButton");
                if (modify != null)
                {
                    var text = modify.GetComponentInChildren<TMP_Text>(true);
                    Txt(text, DSTextStyle.ButtonMD, BlockforgeTheme.InkPrimary, "Edit", TextAlignmentOptions.Center);
                    El(modify, DSRole.ButtonSecondary, new[] { text });
                    Place(modify, new Vector2(0, 0), new Vector2(0, 0), new Vector2(pad, pad),
                          new Vector2(w - 2 * pad - BlockforgeTheme.ControlMd - BlockforgeTheme.Space2, BlockforgeTheme.ControlMd), new Vector2(0, 0));
                }

                // Supprimer : bouton icone, ton danger (danger-ink)
                var delete = filled.Find("DeleteButton");
                if (delete != null)
                {
                    var text = delete.GetComponentInChildren<TMP_Text>(true);
                    if (text != null)
                        text.gameObject.SetActive(false);
                    var icon = IconChild(delete, "trash", 20f);
                    El(delete, DSRole.IconButton, null, e => { e.tone = DSTone.Danger; e.icons = new Graphic[] { icon }; });
                    Place(delete, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-pad, pad),
                          new Vector2(BlockforgeTheme.ControlMd, BlockforgeTheme.ControlMd), new Vector2(1, 0));
                }
            }

            var emptyGroup = slot.Find("EmptyGroup");
            if (emptyGroup != null)
            {
                var plus = emptyGroup.Find("Plus");
                if (plus != null)
                {
                    plus.gameObject.SetActive(false);
                    var addIcon = Child(emptyGroup, "AddIcon");
                    var image = GetOrAdd<Image>(addIcon.gameObject);
                    image.sprite = DSAssets.Icon("add");
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    image.color = BlockforgeTheme.InkMuted;
                    // Pictogramme 48 px au centre de l'ancienne zone "+" (120 a 300 px du haut)
                    Place(addIcon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(48, 48), new Vector2(0.5f, 0.5f));
                    EditorUtility.SetDirty(image);
                }
                Txt(emptyGroup.Find("Text")?.GetComponent<TMP_Text>(), DSTextStyle.Body, BlockforgeTheme.InkSecondary,
                    "Build a new robot in this bay and give it a name.", TextAlignmentOptions.Top);
                var create = emptyGroup.Find("CreateButton");
                if (create != null)
                {
                    var text = create.GetComponentInChildren<TMP_Text>(true);
                    Txt(text, DSTextStyle.ButtonMD, BlockforgeTheme.InkPrimary, "New robot", TextAlignmentOptions.Center);
                    El(create, DSRole.ButtonSecondary, new[] { text });
                    Place(create, new Vector2(0, 0), new Vector2(0, 0), new Vector2(pad, pad),
                          new Vector2(w - 2 * pad, BlockforgeTheme.ControlMd), new Vector2(0, 0));
                }
            }
        }

        // Dialogue de suppression (ton danger, bouton = verbe + objet)
        ApplyDialog("BuildScreen/DeletePopup", "Delete this robot?", null, 196f,
                    ("CancelButton", DSRole.ButtonGhost, "Cancel"),
                    ("ConfirmButton", DSRole.ButtonDanger, "Delete robot"));
        var accent = F("BuildScreen/DeletePopup/Panel/Accent");
        if (accent != null)
            accent.gameObject.SetActive(false);
    }

    // =========================================================
    // GARAGE
    // =========================================================

    private static readonly string[] GarageStatLabels = { "CPU", "Health", "Speed", "Power", "Mass" };

    private static readonly (string key, string action, string keys, int group)[] Controls =
    {
        ("AVANCER", "Forward", "Z", 0), ("RECULER", "Backward", "S", 0), ("GAUCHE", "Left", "Q", 0), ("DROITE", "Right", "D", 0),
        ("INVENTAIRE", "Inventory", "Tab", 1), ("AJOUTER", "Place part", "Left click", 1), ("SUPPRIMER", "Remove part", "Right click", 1),
        ("PIVOTER", "Rotate", "Wheel", 1), ("MIROIR", "Mirror", "M", 1),
        ("MONTER", "Up", "Space", 2), ("DESCENDRE", "Down", "Ctrl", 2),
        ("SAUVER", "Save", "T", 3), ("TESTER", "Test", "P", 3), ("RENOMMER", "Rename", "R", 3), ("RETOUR", "Leave garage", "Esc", 3),
    };

    private static readonly (string key, string label, string icon)[] InventoryTabs =
    {
        ("TOUT", "All", "grid"), ("CHÂSSIS", "Chassis", "structure"), ("MOUVEMENT", "Mobility", "mobility"),
        ("ARMES", "Weapons", "weapon"), ("DÉFENSE", "Defense", "health"), ("SPÉCIAL", "Special", "electronics"),
    };

    private static void ApplyGarage()
    {
        // ---------- Stats (panneau flottant, StatLine : nom a gauche, chiffre a droite,
        // barre de proportion de 4 px sous les deux) ----------
        const float statsW = 320f, lineH = 20f, rowH = 28f;
        float pad = BlockforgeTheme.Space4;
        float statsH = pad + GarageStatLabels.Length * rowH + (GarageStatLabels.Length - 1) * BlockforgeTheme.Space4 + pad;
        var stats = F("StatsPanel");
        El(stats, DSRole.PanelFloating);
        Place(stats, new Vector2(0, 1), new Vector2(0, 1), new Vector2(BlockforgeTheme.Space8, -BlockforgeTheme.Space8),
              new Vector2(statsW, statsH), new Vector2(0, 1));
        for (int i = 0; i < GarageStatLabels.Length; i++)
        {
            float y = pad + i * (rowH + BlockforgeTheme.Space4);
            var label = T($"StatsPanel/Row_{i}_Label");
            var value = T($"StatsPanel/Row_{i}_Value");
            Txt(label, DSTextStyle.Caption, BlockforgeTheme.InkSecondary, GarageStatLabels[i], TextAlignmentOptions.MidlineLeft);
            Txt(value, DSTextStyle.ReadoutMD, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.MidlineRight);
            if (value != null)
                value.textWrappingMode = TextWrappingModes.NoWrap;
            Row(label, y, lineH, pad, 160f);
            Row(value, y, lineH, 100f, pad);
            var track = F($"StatsPanel/Row_{i}_BarBg");
            El(track, DSRole.Track);
            Row(track, y + lineH + BlockforgeTheme.Space1, 4f, pad, pad);
            El(F($"StatsPanel/Row_{i}_BarBg/Fill"), DSRole.Fill);
        }

        // ---------- Baie + renommer (panneau suivant, space-6 plus bas) ----------
        El(F("SlotBar"), DSRole.PanelFloating);
        Place(F("SlotBar"), new Vector2(0, 1), new Vector2(0, 1),
              new Vector2(BlockforgeTheme.Space8, -BlockforgeTheme.Space8 - statsH - BlockforgeTheme.Space6),
              new Vector2(statsW, BlockforgeTheme.ControlLg), new Vector2(0, 1));
        Txt(T("SlotBar/Text"), DSTextStyle.TitleSM, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.MidlineLeft);
        var edit = F("SlotBar/EditButton");
        if (edit != null)
        {
            var text = edit.GetComponentInChildren<TMP_Text>(true);
            Txt(text, DSTextStyle.ButtonSM, BlockforgeTheme.InkSecondary, "Rename", TextAlignmentOptions.Center);
            El(edit, DSRole.ButtonGhost, new[] { text });
            Resize(edit, PrefWidth(text) + 2 * BlockforgeTheme.Space3, BlockforgeTheme.ControlSm);
            var slotText = F("SlotBar/Text") as RectTransform;
            if (slotText != null)
                slotText.offsetMax = new Vector2(-(PrefWidth(text) + 2 * BlockforgeTheme.Space3 + 20f), slotText.offsetMax.y);
        }

        ApplyControls();
        ApplyToolRail();

        // ---------- Inventaire ----------
        var clickAway = F("InventoryClickAway");
        if (clickAway != null)
            SetImage(clickAway, BlockforgeTheme.Scrim);
        ApplyInventory();

        // ---------- Dialogues ----------
        ApplyDialog("NamePopup", "Name your robot", null, 176f,
                    ("CancelButton", DSRole.ButtonGhost, "Cancel"),
                    ("ConfirmButton", DSRole.ButtonPrimary, "Save"));
        var input = F("NamePopup/Panel/NameInput");
        if (input != null)
        {
            ApplyField(input, "Robot name", null);
            Place(input, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -(BlockforgeTheme.Space4 + 26 + BlockforgeTheme.Space4)),
                  new Vector2(-2 * BlockforgeTheme.Space4, BlockforgeTheme.ControlMd), new Vector2(0.5f, 1));
        }
        ApplyDialog("QuitPopup", "Leave the garage?",
                    "Your latest changes are not saved. If you leave now, they are lost.", 196f,
                    ("CancelButton", DSRole.ButtonGhost, "Stay"),
                    ("ConfirmButton", DSRole.ButtonDanger, "Leave without saving"));
    }

    private static void ApplyControls()
    {
        var panel = F("ControlsPanel");
        if (panel == null)
            return;

        // Aide des commandes : panneau flottant, opacity-ghost au repos, repliable (H)
        GetOrAdd<Image>(panel.gameObject);
        El(panel, DSRole.PanelFloating);
        GetOrAdd<CanvasGroup>(panel.gameObject);
        var hint = GetOrAdd<GarageControlsHint>(panel.gameObject);

        float pad = BlockforgeTheme.Space4, rowH = 22f, step = rowH + BlockforgeTheme.Space2, groupGap = BlockforgeTheme.Space3;
        const float width = 248f;

        // En-tete : "Controls" + touche H
        var headerLabel = Child(panel, "Header_Label");
        var headerText = GetOrAdd<TextMeshProUGUI>(headerLabel.gameObject);
        headerText.raycastTarget = false;
        Txt(headerText, DSTextStyle.Label, BlockforgeTheme.InkMuted, "Controls", TextAlignmentOptions.MidlineLeft);
        Row(headerText, pad, rowH, pad, 60f);
        var headerKey = KeyCap(panel, "Header_Key", "H", pad);

        float y = pad + rowH + BlockforgeTheme.Space3;
        int lastGroup = 0;
        var rows = new List<GameObject>();
        foreach (var (key, action, keys, group) in Controls)
        {
            var label = panel.Find("Ctrl_" + key)?.GetComponent<TMP_Text>();
            if (label == null)
            {
                missing.Add("ControlsPanel/Ctrl_" + key);
                continue;
            }
            if (group != lastGroup)
            {
                y += groupGap;
                lastGroup = group;
            }
            Txt(label, DSTextStyle.BodySM, BlockforgeTheme.InkSecondary, action, TextAlignmentOptions.MidlineLeft);
            Row(label, y, rowH, pad, 110f);
            rows.Add(label.gameObject);
            var cap = KeyCap(panel, "Key_" + key, keys, y);
            if (cap != null)
                rows.Add(cap.gameObject);
            y += step;
        }
        float height = y - BlockforgeTheme.Space2 + pad;
        Place(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-BlockforgeTheme.Space8, -BlockforgeTheme.Space8),
              new Vector2(width, height), new Vector2(1, 1));

        Undo.RecordObject(hint, "Design System");
        hint.header = (RectTransform)headerLabel;
        hint.rows = rows.ToArray();
        hint.expandedHeight = height;
        hint.collapsedHeight = pad + rowH + pad;
        EditorUtility.SetDirty(hint);
        _ = headerKey;
    }

    // Touche d'un KeyHint, alignee a droite de la ligne y (mono, surface-inset, border-strong, radius-xs)
    private static RectTransform KeyCap(Transform panel, string name, string keys, float y)
    {
        var cap = Child(panel, name);
        GetOrAdd<Image>(cap.gameObject);
        var textTransform = cap.Find("Text") ?? Child(cap, "Text");
        var text = GetOrAdd<TextMeshProUGUI>(textTransform.gameObject);
        text.raycastTarget = false;
        Txt(text, DSTextStyle.Key, BlockforgeTheme.InkPrimary, keys, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        El(cap, DSRole.KeyCap, new[] { text });
        float w = Mathf.Max(22f, PrefWidth(text) + 2 * BlockforgeTheme.Space1 + 2);
        Place(cap, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-BlockforgeTheme.Space4, -y), new Vector2(w, 22f), new Vector2(1, 1));
        return (RectTransform)cap;
    }

    private static void ApplyToolRail()
    {
        var rail = F("Toolbar");
        if (rail == null)
            return;
        GetOrAdd<Image>(rail.gameObject);
        El(rail, DSRole.ToolRail);

        string[] icons = { "create", "paint" };
        float size = BlockforgeTheme.ControlLg, gap = BlockforgeTheme.Space1;
        int count = 0;
        for (int i = 0; i < 5; i++)
        {
            var tool = rail.Find($"Tool_{i + 1}");
            if (tool == null)
                break;
            count++;
            // Pictogramme seul : le nom vit dans le raccourci et l'infobulle
            var label = tool.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);
            var icon = IconChild(tool, i < icons.Length ? icons[i] : "cube", 24f);

            TMP_Text keyText = null;
            var badge = tool.Find("KeyBadge");
            if (badge != null)
            {
                keyText = badge.GetComponentInChildren<TMP_Text>(true);
                Txt(keyText, DSTextStyle.KeyCorner, BlockforgeTheme.InkMuted, (i + 1).ToString(), TextAlignmentOptions.Center);
                El(badge, DSRole.Inset);
                Place(badge, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-2, 2), new Vector2(12, 12), new Vector2(1, 0));
            }

            var captured = icon;
            var capturedKey = keyText;
            El(tool, DSRole.IconButton, null, e => { e.icons = new Graphic[] { captured }; e.keyCorner = capturedKey; });
            Place(tool, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(gap + i * (size + gap), 0), new Vector2(size, size), new Vector2(0, 0.5f));
        }

        // Rail centre en bas, space-8 au-dessus du bord
        Place(rail, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, BlockforgeTheme.Space8),
              new Vector2(count * size + (count + 1) * gap, size + 2 * gap), new Vector2(0.5f, 0));

        var toolbar = rail.GetComponent<GarageToolbar>();
        if (toolbar != null)
        {
            Undo.RecordObject(toolbar, "Design System");
            toolbar.outlines = new Outline[toolbar.buttons.Length];
            EditorUtility.SetDirty(toolbar);
        }
    }

    private static void ApplyInventory()
    {
        var sheet = F("InventorySheet");
        if (sheet == null)
            return;
        El(sheet, DSRole.PanelEdge, null, e => e.hairlineEdge = DSEdge.Top);
        Txt(T("InventorySheet/Title"), DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Inventory", TextAlignmentOptions.MidlineLeft);

        // Onglets segment avec pictogramme de categorie (non teinte : il suit le texte)
        var tabs = F("InventorySheet/Tabs");
        if (tabs != null)
        {
            var layout = tabs.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = BlockforgeTheme.Space1;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childForceExpandHeight = false;
                layout.childControlHeight = true;
            }
            foreach (var (key, label, icon) in InventoryTabs)
            {
                var tab = tabs.Find("Tab_" + key);
                if (tab == null)
                {
                    missing.Add("InventorySheet/Tabs/Tab_" + key);
                    continue;
                }
                var text = tab.Find("Text")?.GetComponent<TMP_Text>();
                Txt(text, DSTextStyle.Label, BlockforgeTheme.InkSecondary, label, TextAlignmentOptions.MidlineLeft);
                var iconImage = IconChild(tab, icon, 16f);
                var iconRect = iconImage.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 0.5f);
                iconRect.pivot = new Vector2(0, 0.5f);
                iconRect.anchoredPosition = new Vector2(BlockforgeTheme.Space3, 0);
                if (text != null)
                {
                    text.rectTransform.anchorMin = Vector2.zero;
                    text.rectTransform.anchorMax = Vector2.one;
                    text.rectTransform.offsetMin = new Vector2(BlockforgeTheme.Space3 + 16 + BlockforgeTheme.Space2, 0);
                    text.rectTransform.offsetMax = new Vector2(-BlockforgeTheme.Space3, 0);
                }
                El(tab, DSRole.TabSegment, new[] { text }, e => e.icons = new Graphic[] { iconImage });
                var le = GetOrAdd<LayoutElement>(tab.gameObject);
                le.preferredWidth = BlockforgeTheme.Space3 + 16 + BlockforgeTheme.Space2 + PrefWidth(text) + BlockforgeTheme.Space3;
                le.preferredHeight = BlockforgeTheme.ControlMd;
            }
        }

        var search = F("InventorySheet/SearchField");
        if (search != null)
        {
            ApplyField(search, "Search parts", "search");
            Resize(search, ((RectTransform)search).sizeDelta.x, BlockforgeTheme.ControlMd);
        }

        // Grille de tuiles tile-md, ecart space-2
        var grid = F("InventorySheet/GridScroll/Viewport/Grid")?.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            Undo.RecordObject(grid, "Design System");
            grid.cellSize = new Vector2(BlockforgeTheme.TileMd, 124f);
            grid.spacing = new Vector2(BlockforgeTheme.Space2, BlockforgeTheme.Space2);
            // Autant de colonnes que la largeur de la grille en contient
            var scroll = F("InventorySheet/GridScroll") as RectTransform;
            if (scroll != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                float usable = scroll.rect.width - grid.padding.left - grid.padding.right;
                grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((usable + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x)));
            }
            EditorUtility.SetDirty(grid);
        }

        var card = F("InventorySheet/GridScroll/Viewport/Grid/CardTemplate");
        if (card != null)
            ApplyPartCard(card);

        // Colonne de detail
        const string d = "InventorySheet/Details";
        El(F(d), DSRole.Panel);
        Txt(T(d + "/Name"), DSTextStyle.TitleLG, BlockforgeTheme.InkPrimary);
        Txt(T(d + "/Description"), DSTextStyle.BodySM, BlockforgeTheme.InkSecondary);
        string[] rows = { "Type", "Size", "Mass", "Health" };
        for (int i = 0; i < rows.Length; i++)
        {
            Txt(T($"{d}/Row_{i}_Label"), DSTextStyle.Caption, BlockforgeTheme.InkSecondary, rows[i]);
            Txt(T($"{d}/Row_{i}_Value"), i == 0 ? DSTextStyle.BodySM : DSTextStyle.ReadoutMD, BlockforgeTheme.InkPrimary);
        }
        El(F(d + "/CostBar"), DSRole.Inset);
        Txt(T(d + "/CostBar/Label"), DSTextStyle.Caption, BlockforgeTheme.InkSecondary, "CPU cost");
        Txt(T(d + "/CostBar/Value"), DSTextStyle.ReadoutMD, BlockforgeTheme.InkPrimary);

        El(F("InventorySheet/Footer"), DSRole.Strip, null, e => e.hairlineEdge = DSEdge.Top);
        Txt(T("InventorySheet/Footer/Capacity"), DSTextStyle.ReadoutSM, BlockforgeTheme.InkSecondary);
    }

    // Tuile de piece : barre de grade 3 px, vignette inset 52 px, nom 12/600, cout mono ink-muted
    private static void ApplyPartCard(Transform card)
    {
        var item = card.GetComponent<InventoryItemCard>();
        El(card, DSRole.PartCard);
        float pad = BlockforgeTheme.Space2;

        var grade = Child(card, "Grade");
        var gradeImage = GetOrAdd<Image>(grade.gameObject);
        gradeImage.raycastTarget = false;
        gradeImage.color = BlockforgeTheme.GradeRaw;
        GetOrAdd<LayoutElement>(grade.gameObject).ignoreLayout = true;
        Place(grade, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(3, -2), new Vector2(0, 0.5f));

        var thumb = Child(card, "Thumb");
        GetOrAdd<Image>(thumb.gameObject).raycastTarget = false;
        El(thumb, DSRole.Inset);
        Place(thumb, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1.5f, -pad), new Vector2(-2 * pad - 3, 52), new Vector2(0.5f, 1));
        var icon = card.Find("Icon");
        if (icon != null)
        {
            // La vignette passe juste sous l'apercu (sans le recouvrir a la 2e application)
            if (thumb.GetSiblingIndex() > icon.GetSiblingIndex())
                thumb.SetSiblingIndex(icon.GetSiblingIndex());
            Place(icon, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1.5f, -pad - 4), new Vector2(-2 * pad - 11, 44), new Vector2(0.5f, 1));
            var iconImage = icon.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.color = new Color(1, 1, 1, 0); // transparent tant qu'aucun apercu n'est assigne
                iconImage.preserveAspect = true;
            }
        }

        var name = card.Find("Name")?.GetComponent<TMP_Text>();
        Txt(name, DSTextStyle.Caption, BlockforgeTheme.InkPrimary, "Part", TextAlignmentOptions.TopLeft);
        if (name != null)
        {
            var semi = DSAssets.Font(DSFont.SemiBold);
            name.font = semi;
            name.fontSharedMaterial = semi.material;
            name.textWrappingMode = TextWrappingModes.Normal;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.maxVisibleLines = 2;
        }
        Place(name, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1.5f, -pad - 52 - pad), new Vector2(-2 * pad - 3, 30), new Vector2(0.5f, 1));

        var cost = card.Find("Cost")?.GetComponent<TMP_Text>();
        Txt(cost, DSTextStyle.ReadoutSM, BlockforgeTheme.InkMuted, "1 CPU", TextAlignmentOptions.BottomLeft);
        if (cost != null)
            cost.fontSize = 11f;
        Place(cost, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1.5f, pad), new Vector2(-2 * pad - 3, 14), new Vector2(0.5f, 0));

        if (item != null)
        {
            Undo.RecordObject(item, "Design System");
            item.outline = null;
            item.gradeBar = gradeImage;
            EditorUtility.SetDirty(item);
        }
    }

    // =========================================================
    // COMPOSANTS PARTAGES
    // =========================================================

    // Champ : surface-inset + border-strong (accent au focus), pictogramme optionnel a gauche
    private static void ApplyField(Transform field, string placeholder, string icon)
    {
        var input = field.GetComponent<TMP_InputField>();
        float left = BlockforgeTheme.Space3;
        Image iconImage = null;
        if (!string.IsNullOrEmpty(icon))
        {
            iconImage = IconChild(field, icon, 16f);
            var r = iconImage.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 0.5f);
            r.pivot = new Vector2(0, 0.5f);
            r.anchoredPosition = new Vector2(BlockforgeTheme.Space3, 0);
            left += 16 + BlockforgeTheme.Space2;
        }
        var area = field.Find("Text Area") as RectTransform;
        if (area != null)
        {
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(left, 0);
            area.offsetMax = new Vector2(-BlockforgeTheme.Space3, 0);
        }
        if (input != null)
        {
            Undo.RecordObject(input, "Design System");
            Txt(input.placeholder as TMP_Text, DSTextStyle.Body, BlockforgeTheme.InkMuted, placeholder, TextAlignmentOptions.MidlineLeft);
            Txt(input.textComponent, DSTextStyle.Body, BlockforgeTheme.InkPrimary, null, TextAlignmentOptions.MidlineLeft);
            if (input.placeholder is TMP_Text ph)
                ph.fontStyle = FontStyles.Normal;
            input.caretColor = BlockforgeTheme.Accent;
            input.customCaretColor = true;
            input.caretWidth = 2;
            input.selectionColor = BlockforgeTheme.WithAlpha(BlockforgeTheme.Accent, 0.35f);
            input.fontAsset = DSAssets.Font(DSFont.Regular);
            input.pointSize = DSText.Size(DSTextStyle.Body);
            EditorUtility.SetDirty(input);
        }
        El(field, DSRole.Field, null, e => e.icons = iconImage != null ? new Graphic[] { iconImage } : new Graphic[0]);
    }

    // Dialogue : scrim, panneau radius-lg + shadow-overlay, titre display-md, corps body,
    // actions alignees a droite, l'action qui confirme en dernier
    private static void ApplyDialog(string path, string title, string body, float height,
                                    params (string name, DSRole role, string label)[] buttons)
    {
        var popup = F(path);
        if (popup == null)
            return;
        El(popup, DSRole.Scrim);
        var panel = popup.Find("Panel");
        if (panel == null)
        {
            missing.Add(path + "/Panel");
            return;
        }
        El(panel, DSRole.Dialog);
        float pad = BlockforgeTheme.Space4;
        var rect = (RectTransform)panel;
        Undo.RecordObject(rect, "Design System");
        rect.sizeDelta = new Vector2(440f, height);

        var titleText = panel.Find("Title")?.GetComponent<TMP_Text>();
        Txt(titleText, DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, title, TextAlignmentOptions.TopLeft);
        Place(titleText, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -pad), new Vector2(-2 * pad, 26), new Vector2(0.5f, 1));

        foreach (var bodyName in new[] { "Body", "Message" })
        {
            var bodyText = panel.Find(bodyName)?.GetComponent<TMP_Text>();
            if (bodyText == null)
                continue;
            Txt(bodyText, DSTextStyle.Body, BlockforgeTheme.InkSecondary, body, TextAlignmentOptions.TopLeft);
            Place(bodyText, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -pad - 26 - pad),
                  new Vector2(-2 * pad, rect.sizeDelta.y - (pad + 26 + pad) - (pad + BlockforgeTheme.ControlMd + pad)), new Vector2(0.5f, 1));
        }

        // De droite a gauche : la confirmation (dernier bouton) contre le bord
        float x = -pad;
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            var (name, role, label) = buttons[i];
            var button = panel.Find(name);
            if (button == null)
            {
                missing.Add($"{path}/Panel/{name}");
                continue;
            }
            var text = button.GetComponentInChildren<TMP_Text>(true);
            Txt(text, DSTextStyle.ButtonMD, InkFor(role), label, TextAlignmentOptions.Center);
            El(button, role, new[] { text });
            float width = PrefWidth(text) + 2 * BlockforgeTheme.Space4;
            Place(button, new Vector2(1, 0), new Vector2(1, 0), new Vector2(x, pad), new Vector2(width, BlockforgeTheme.ControlMd), new Vector2(1, 0));
            x -= width + BlockforgeTheme.Space2;
        }
    }

    private static void DialogButton(Transform button, DSRole role, DSTextStyle style, string label, float height)
    {
        if (button == null)
            return;
        var text = button.GetComponentInChildren<TMP_Text>(true);
        Txt(text, style, InkFor(role), label, TextAlignmentOptions.Center);
        El(button, role, new[] { text });
        float padX = height >= BlockforgeTheme.ControlLg ? BlockforgeTheme.Space6 : BlockforgeTheme.Space4;
        Resize(button, Mathf.Max(((RectTransform)button).sizeDelta.x, PrefWidth(text) + 2 * padX), height);
    }

    private static Color InkFor(DSRole role) => role switch
    {
        DSRole.ButtonPrimary => BlockforgeTheme.InkOnAccent,
        DSRole.ButtonGhost => BlockforgeTheme.InkSecondary,
        _ => BlockforgeTheme.InkPrimary,
    };

    // =========================================================
    // OUTILS
    // =========================================================

    private static Transform F(string path)
    {
        if (root == null)
            return null;
        var found = root.Find(path);
        if (found == null)
            missing.Add(path);
        return found;
    }

    private static TMP_Text T(string path) => F(path)?.GetComponent<TMP_Text>();

    private static DSElement El(Transform target, DSRole role, TMP_Text[] labels = null, Action<DSElement> configure = null)
    {
        if (target == null)
            return null;
        var go = target.gameObject;
        GetOrAdd<Image>(go);

        // Les anciens contours (Outline / Shadow uGUI) sont remplaces par la bordure du DS
        foreach (var effect in go.GetComponents<UnityEngine.UI.Shadow>())
            Undo.DestroyObjectImmediate(effect);

        var element = go.GetComponent<DSElement>();
        if (element == null)
            element = Undo.AddComponent<DSElement>(go);
        Undo.RecordObject(element, "Design System");
        element.role = role;
        element.labels = labels?.Where(l => l != null).ToArray() ?? new TMP_Text[0];
        configure?.Invoke(element);

        var selectable = go.GetComponent<Selectable>();
        if (selectable != null)
        {
            Undo.RecordObject(selectable, "Design System");
            selectable.transition = Selectable.Transition.None;
            selectable.targetGraphic = go.GetComponent<Image>();
            EditorUtility.SetDirty(selectable);
        }

        element.EnsureParts();
        element.ApplyShape();
        element.RefreshImmediate();
        EditorUtility.SetDirty(element);
        EditorUtility.SetDirty(go.GetComponent<Image>());
        elementCount++;
        return element;
    }

    private static void Txt(TMP_Text text, DSTextStyle style, Color color, string value = null, TextAlignmentOptions? align = null)
    {
        if (text == null)
            return;
        Undo.RecordObject(text, "Design System");
        DSText.Apply(text, style, color);
        if (value != null)
            text.text = value;
        if (align.HasValue)
            text.alignment = align.Value;
        EditorUtility.SetDirty(text);
        textCount++;
    }

    private static float PrefWidth(TMP_Text text)
    {
        if (text == null)
            return 60f;
        text.ForceMeshUpdate(true, true);
        return Mathf.Ceil(text.GetPreferredValues(text.text, 9999f, 9999f).x);
    }

    private static Transform Child(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
            return existing;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Design System");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // Pictogramme du DS (blanc, teinte par l'etat) centre dans le parent
    private static Image IconChild(Transform parent, string icon, float size)
    {
        var child = Child(parent, "DS_Icon");
        var image = GetOrAdd<Image>(child.gameObject);
        image.sprite = DSAssets.Icon(icon);
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = BlockforgeTheme.InkSecondary;
        GetOrAdd<LayoutElement>(child.gameObject).ignoreLayout = true;
        var rect = (RectTransform)child;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        EditorUtility.SetDirty(image);
        return image;
    }

    private static void SetIcon(Image image, string icon, Color color, float size)
    {
        if (image == null)
            return;
        Undo.RecordObject(image, "Design System");
        image.sprite = DSAssets.Icon(icon);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = color;
        if (size > 0f)
            image.rectTransform.sizeDelta = new Vector2(size, size);
        EditorUtility.SetDirty(image);
    }

    private static void SetImage(Transform target, Color color)
    {
        var image = target.GetComponent<Image>();
        if (image == null)
            return;
        Undo.RecordObject(image, "Design System");
        image.sprite = null;
        image.color = color;
        EditorUtility.SetDirty(image);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    private static void Place(Component target, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        if (target == null)
            return;
        var rect = target.transform as RectTransform;
        if (rect == null)
            return;
        Undo.RecordObject(rect, "Design System");
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot ?? anchorMin;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        EditorUtility.SetDirty(rect);
    }

    // Ligne dans un panneau : y depuis le haut, hauteur, marges gauche/droite
    private static void Row(Component target, float y, float height, float left, float right)
    {
        if (target == null)
            return;
        var rect = (RectTransform)target.transform;
        Undo.RecordObject(rect, "Design System");
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.offsetMin = new Vector2(left, -y - height);
        rect.offsetMax = new Vector2(-right, -y);
        EditorUtility.SetDirty(rect);
    }

    private static void Resize(Component target, float width, float height)
    {
        if (target == null)
            return;
        var rect = (RectTransform)target.transform;
        Undo.RecordObject(rect, "Design System");
        rect.sizeDelta = new Vector2(width, height);
        EditorUtility.SetDirty(rect);
    }

    private static void Offset(Component target, float x)
    {
        if (target == null)
            return;
        var rect = (RectTransform)target.transform;
        Undo.RecordObject(rect, "Design System");
        rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        EditorUtility.SetDirty(rect);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
