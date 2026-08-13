using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// SETUP DE L'INVENTAIRE DU GARAGE (fenetre du bas)
// Menu : Blockforge > Setup Garage Inventory
// A lancer APRES "Setup Garage UI" (il a besoin du
// GarageCanvas et de la Toolbar).
// =========================================================

public static class GarageInventorySetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    private const string BlockDataDir = "Assets/_Project/Data/Blocks";

    private static readonly Color SheetBg = new(0.016f, 0.035f, 0.065f, 0.97f);
    private static readonly Color HeaderBg = new(0.03f, 0.05f, 0.09f, 1f);
    private static readonly Color CardBg = new(0.06f, 0.10f, 0.16f, 0.95f);
    private static readonly Color PanelBg = new(0.05f, 0.08f, 0.13f, 1f);
    private static readonly Color IconBg = new(0.10f, 0.16f, 0.24f, 1f);
    private static readonly Color BadgeBg = new(0.09f, 0.16f, 0.30f, 1f);
    private static readonly Color FieldBg = new(0.08f, 0.12f, 0.18f, 1f);
    private static readonly Color Accent = new(0.23f, 0.51f, 0.96f);
    private static readonly Color TextMain = new(0.86f, 0.91f, 0.96f);
    private static readonly Color TextDim = new(0.48f, 0.56f, 0.66f);

    [MenuItem("Blockforge/Setup Garage Inventory")]
    public static void Setup()
    {
        var blocks = EnsureBlocks();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = GameObject.Find("GarageCanvas");
        if (canvasGo == null)
        {
            Debug.LogError("[GarageInventorySetup] GarageCanvas introuvable. " +
                           "Lance d'abord : Blockforge > Setup Garage UI.");
            return;
        }

        foreach (var name in new[] { "InventorySheet", "InventoryClickAway" })
        {
            var old = canvasGo.transform.Find(name);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
        }

        // ---------- Zone "cliquer ailleurs pour fermer" (au-dessus du panneau) ----------
        var clickAway = CreatePanel(canvasGo.transform, "InventoryClickAway", new Color(0f, 0f, 0f, 0.25f));
        StretchFull(clickAway);
        var clickAwayButton = clickAway.gameObject.AddComponent<Button>();
        clickAwayButton.targetGraphic = clickAway.GetComponent<Image>();
        clickAwayButton.transition = Selectable.Transition.None;
        clickAway.gameObject.SetActive(false);

        // ---------- Fenetre (fermee par defaut, sous l'ecran) ----------
        var sheet = CreatePanel(canvasGo.transform, "InventorySheet", SheetBg);
        SetAnchors(sheet, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        sheet.sizeDelta = new Vector2(0, 470);
        sheet.anchoredPosition = new Vector2(0, -470);

        var inventory = sheet.gameObject.AddComponent<GarageInventoryController>();
        inventory.sheet = sheet;
        inventory.blocks = blocks;
        inventory.clickAway = clickAway.gameObject;

        // ---------- Entete ----------
        var title = CreateText(sheet, "Title", "INVENTAIRE", 20, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        title.sizeDelta = new Vector2(200, 34);
        title.anchoredPosition = new Vector2(24, -12);

        var tabsDefs = new (string label, int category, float width)[]
        {
            ("TOUT", -1, 84), ("CHÂSSIS", 0, 110), ("MOUVEMENT", 1, 130),
            ("ARMES", 2, 92), ("DÉFENSE", 3, 108), ("SPÉCIAL", 4, 104),
        };

        var tabsContainer = CreateUI(sheet, "Tabs");
        SetAnchors(tabsContainer, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        tabsContainer.sizeDelta = new Vector2(tabsDefs.Sum(t => t.width) + 6 * (tabsDefs.Length - 1), 40);
        tabsContainer.anchoredPosition = new Vector2(230, -8);
        var tabsLayout = tabsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 6;
        tabsLayout.childForceExpandWidth = false;
        tabsLayout.childForceExpandHeight = true;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;

        var tabButtons = new Button[tabsDefs.Length];
        var tabBgs = new Image[tabsDefs.Length];
        var tabLabels = new TMP_Text[tabsDefs.Length];
        var tabCategories = new int[tabsDefs.Length];
        for (int i = 0; i < tabsDefs.Length; i++)
        {
            var (btn, label) = CreateButton(tabsContainer, $"Tab_{tabsDefs[i].label}", tabsDefs[i].label, 13,
                                            Color.clear, TextDim);
            btn.gameObject.AddComponent<LayoutElement>().preferredWidth = tabsDefs[i].width;
            tabButtons[i] = btn;
            tabBgs[i] = btn.GetComponent<Image>();
            tabLabels[i] = label;
            tabCategories[i] = tabsDefs[i].category;
        }
        inventory.tabButtons = tabButtons;
        inventory.tabBackgrounds = tabBgs;
        inventory.tabLabels = tabLabels;
        inventory.tabCategories = tabCategories;

        inventory.searchField = BuildSearchField(sheet);

        // ---------- Grille de blocs ----------
        var grid = CreateUI(sheet, "Grid");
        SetAnchors(grid, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        grid.sizeDelta = new Vector2(1490, 330);
        grid.anchoredPosition = new Vector2(24, -62);
        var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(141, 118);
        gridLayout.spacing = new Vector2(8, 8);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 10;
        inventory.gridParent = grid;

        inventory.cardTemplate = BuildCardTemplate(grid);

        // ---------- Fiche detail (droite) ----------
        var details = CreatePanel(sheet, "Details", PanelBg);
        SetAnchors(details, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        details.sizeDelta = new Vector2(350, 340);
        details.anchoredPosition = new Vector2(-24, -62);

        var dName = CreateText(details, "Name", "CUBE", 18, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(dName, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        dName.offsetMin = new Vector2(18, -40);
        dName.offsetMax = new Vector2(-18, -10);
        inventory.detailName = dName.GetComponent<TMP_Text>();

        var dDesc = CreateText(details, "Description", "—", 12.5f, TextDim, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        SetAnchors(dDesc, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        dDesc.offsetMin = new Vector2(18, -88);
        dDesc.offsetMax = new Vector2(-18, -44);
        inventory.detailDescription = dDesc.GetComponent<TMP_Text>();

        var rows = new (string label, string field)[]
        {
            ("TYPE", nameof(GarageInventoryController.detailType)),
            ("TAILLE", nameof(GarageInventoryController.detailSize)),
            ("POIDS", nameof(GarageInventoryController.detailWeight)),
            ("RÉSISTANCE", nameof(GarageInventoryController.detailResistance)),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float y = -100f - i * 32f;
            var label = CreateText(details, $"Row_{i}_Label", rows[i].label, 13, TextDim, TextAlignmentOptions.Left, FontStyles.Normal);
            SetAnchors(label, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            label.offsetMin = new Vector2(18, y - 26);
            label.offsetMax = new Vector2(-160, y);

            var value = CreateText(details, $"Row_{i}_Value", "—", 13, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
            SetAnchors(value, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            value.offsetMin = new Vector2(18, y - 26);
            value.offsetMax = new Vector2(-18, y);

            typeof(GarageInventoryController).GetField(rows[i].field)
                                             .SetValue(inventory, value.GetComponent<TMP_Text>());
        }

        var costBar = CreatePanel(details, "CostBar", BadgeBg);
        SetAnchors(costBar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        costBar.offsetMin = new Vector2(14, 14);
        costBar.offsetMax = new Vector2(-14, 62);
        var costLabel = CreateText(costBar, "Label", "COÛT", 15, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        StretchFull(costLabel);
        costLabel.offsetMin = new Vector2(16, 0);
        var costValue = CreateText(costBar, "Value", "1 CPU", 15, Accent, TextAlignmentOptions.Right, FontStyles.Bold);
        StretchFull(costValue);
        costValue.offsetMax = new Vector2(-16, 0);
        inventory.detailCost = costValue.GetComponent<TMP_Text>();

        // ---------- Bandeau capacite (bas) ----------
        var footer = CreatePanel(sheet, "Footer", HeaderBg);
        SetAnchors(footer, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        footer.sizeDelta = new Vector2(0, 36);
        footer.anchoredPosition = Vector2.zero;
        var capacity = CreateText(footer, "Capacity", "CAPACITÉ UTILISÉE : 680 / 1 000 CPU", 14, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(capacity);
        capacity.GetComponent<TMP_Text>().richText = true;
        inventory.capacityText = capacity.GetComponent<TMP_Text>();

        // ---------- Cablage scene ----------
        var toolbar = canvasGo.transform.Find("Toolbar");
        if (toolbar != null)
            inventory.liftWhileOpen = (RectTransform)toolbar;

        // Ordre d'affichage : ... < zone de fermeture < barre d'outils < panneau
        clickAway.SetAsLastSibling();
        if (toolbar != null)
            toolbar.SetAsLastSibling();
        sheet.SetAsLastSibling();

        var toDisable = new List<MonoBehaviour>();
        var player = Object.FindFirstObjectByType<PlayerGarageController>();
        if (player != null)
            toDisable.Add(player);
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb != null && mb.GetType().Name.Contains("Plier") && mb is not PlierBlockPreview)
                toDisable.Add(mb);
        }
        inventory.disableWhileOpen = toDisable.Distinct().ToArray();

        // ---------- Apercu du bloc selectionne sur la pince ----------
        var supportPlate = GameObject.Find("support_plate");
        if (supportPlate != null)
        {
            if (!supportPlate.TryGetComponent<PlierBlockPreview>(out var preview))
                preview = supportPlate.AddComponent<PlierBlockPreview>();
            preview.inventory = inventory;
            preview.supportPlate = supportPlate.transform;
            EditorUtility.SetDirty(supportPlate);
        }
        else
        {
            Debug.LogWarning("[GarageInventorySetup] 'support_plate' introuvable : l'apercu du bloc " +
                             "sur la pince n'a pas pu etre cable.");
        }

        // ---------- Rotation de la tete de pince a la molette ----------
        var plierRoot = GameObject.Find("Plier_Garage");
        var wristHub = GameObject.Find("wrist_hub");
        var plierRig = GameObject.Find("CW_Plier_Rig");
        if (plierRoot != null && wristHub != null && plierRig != null)
        {
            if (!plierRoot.TryGetComponent<PlierRotator>(out var rotator))
                rotator = plierRoot.AddComponent<PlierRotator>();

            rotator.wristHub = wristHub.transform;
            rotator.inventory = inventory;

            var parts = new List<Transform> { plierRig.transform, wristHub.transform };
            foreach (var partName in new[] { "wrist_ring", "hub_light" })
            {
                var part = GameObject.Find(partName);
                if (part != null)
                    parts.Add(part.transform);
            }
            rotator.rotatingParts = parts.ToArray();

            var jaws = new List<Transform>();
            foreach (var jawName in new[] { "jaw_1", "jaw_2", "jaw_3" })
            {
                var jaw = GameObject.Find(jawName);
                if (jaw != null)
                    jaws.Add(jaw.transform);
            }
            rotator.jaws = jaws.ToArray();

            EditorUtility.SetDirty(plierRoot);
        }
        else
        {
            Debug.LogWarning("[GarageInventorySetup] Plier_Garage / wrist_hub / CW_Plier_Rig introuvable : " +
                             "la rotation a la molette n'a pas pu etre cablee.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GarageInventorySetup] Inventaire genere ({blocks.Length} blocs). TAB pour ouvrir/fermer.");
    }

    // =========================================================
    // TEMPLATE DE CARTE
    // =========================================================

    private static InventoryItemCard BuildCardTemplate(RectTransform gridParent)
    {
        var card = CreatePanel(gridParent, "CardTemplate", CardBg);
        var widget = card.gameObject.AddComponent<InventoryItemCard>();

        widget.button = card.gameObject.AddComponent<Button>();
        widget.button.targetGraphic = card.GetComponent<Image>();
        widget.background = card.GetComponent<Image>();

        var outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = Accent;
        outline.effectDistance = new Vector2(2, 2);
        outline.enabled = false;
        widget.outline = outline;

        var icon = CreatePanel(card, "Icon", IconBg);
        SetAnchors(icon, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        icon.offsetMin = new Vector2(10, -72);
        icon.offsetMax = new Vector2(-10, -10);
        widget.iconImage = icon.GetComponent<Image>();

        var name = CreateText(card, "Name", "BLOC", 12, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        SetAnchors(name, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        name.offsetMin = new Vector2(4, 24);
        name.offsetMax = new Vector2(-4, 46);
        widget.nameText = name.GetComponent<TMP_Text>();

        var cost = CreateText(card, "Cost", "1 CPU", 11, TextDim, TextAlignmentOptions.Center, FontStyles.Normal);
        SetAnchors(cost, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        cost.offsetMin = new Vector2(4, 4);
        cost.offsetMax = new Vector2(-4, 24);
        widget.costText = cost.GetComponent<TMP_Text>();

        card.gameObject.SetActive(false);
        return widget;
    }

    // =========================================================
    // CHAMP DE RECHERCHE
    // =========================================================

    private static TMP_InputField BuildSearchField(RectTransform sheet)
    {
        var field = CreatePanel(sheet, "SearchField", FieldBg);
        SetAnchors(field, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        field.sizeDelta = new Vector2(300, 36);
        field.anchoredPosition = new Vector2(-24, -10);

        var input = field.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = field.GetComponent<Image>();

        var textArea = CreateUI(field, "Text Area");
        StretchFull(textArea);
        textArea.offsetMin = new Vector2(12, 4);
        textArea.offsetMax = new Vector2(-12, -4);
        textArea.gameObject.AddComponent<RectMask2D>();

        var placeholder = CreateText(textArea, "Placeholder", "Rechercher un bloc...", 13, TextDim, TextAlignmentOptions.Left, FontStyles.Italic);
        StretchFull(placeholder);

        var text = CreateText(textArea, "Text", "", 13, TextMain, TextAlignmentOptions.Left, FontStyles.Normal);
        StretchFull(text);

        input.textViewport = textArea;
        input.textComponent = text.GetComponent<TMP_Text>();
        input.placeholder = placeholder.GetComponent<TMP_Text>();

        return input;
    }

    // =========================================================
    // DONNEES : LES 20 BLOCS
    // =========================================================

    private static BlockDefinition[] EnsureBlocks()
    {
        EnsureFolder(BlockDataDir);

        // Liste canonique : le setup resynchronise les assets sur cette liste
        // (noms, categories, stats) et supprime les blocs qui n'y sont plus.
        var defs = new (string file, string name, BlockCategory cat, int cpu, string size, int kg, int hp, string desc)[]
        {
            // ----- Chassis -----
            ("Block_01_Cube", "CUBE", BlockCategory.Chassis, 1, "1x1x1", 80, 1000, "Bloc de base. Solide et équilibré."),
            ("Block_02_Pente", "PENTE", BlockCategory.Chassis, 1, "1x1x1", 70, 850, "Surface inclinée pour dévier les tirs."),
            ("Block_03_Coin", "COIN", BlockCategory.Chassis, 1, "1x1x1", 55, 700, "Angle de finition pour carrosserie."),
            ("Block_04_Interieur", "INTÉRIEUR", BlockCategory.Chassis, 1, "1x1x1", 45, 600, "Bloc creux léger pour les structures internes."),
            ("Block_05_Tige", "TIGE", BlockCategory.Chassis, 1, "1x1x4", 30, 400, "Tige fine pour cadres et extensions légères."),
            // ----- Mouvement -----
            ("Block_06_Roues", "ROUES", BlockCategory.Mouvement, 6, "1x1x1", 120, 600, "Roue motrice standard. Rapide sur terrain plat."),
            ("Block_07_Chenilles", "CHENILLES", BlockCategory.Mouvement, 10, "3x1x1", 400, 1500, "Traction lourde. Franchit tous les terrains."),
            ("Block_08_PattesInsecte", "PATTES D'INSECTE", BlockCategory.Mouvement, 12, "2x2x1", 220, 800, "Marche articulée. Escalade les pentes raides."),
            ("Block_09_LamesSurvol", "LAMES DE SURVOL", BlockCategory.Mouvement, 14, "2x1x2", 180, 500, "Sustentation basse altitude. Glisse rapide."),
            ("Block_10_Helices", "HÉLICES", BlockCategory.Mouvement, 12, "2x1x2", 150, 450, "Portance verticale pour le vol stationnaire."),
            ("Block_11_Ailes", "AILES", BlockCategory.Mouvement, 10, "3x1x2", 130, 400, "Portance horizontale à grande vitesse."),
            ("Block_12_Propulseurs", "PROPULSEURS", BlockCategory.Mouvement, 16, "1x1x2", 260, 700, "Poussée directionnelle puissante. Consomme beaucoup."),
            // ----- Armes offensives -----
            ("Block_13_Laser", "LASER", BlockCategory.Armes, 14, "2x1x1", 180, 500, "Tir précis à dégâts continus."),
            ("Block_14_LanceurPlasma", "LANCEUR DE PLASMA", BlockCategory.Armes, 18, "2x1x1", 260, 550, "Projectiles lents à zone d'impact."),
            ("Block_15_CanonElectrique", "CANON ÉLECTRIQUE", BlockCategory.Armes, 22, "3x1x1", 340, 650, "Arc électrique qui se propage entre cibles."),
            ("Block_16_LameTesla", "LAME DE TESLA", BlockCategory.Armes, 20, "2x1x1", 240, 900, "Lame de mêlée électrifiée. Dégâts au contact."),
            // ----- Materiaux defensifs -----
            ("Block_17_DistributeurNano", "DISTRIBUTEUR NANO", BlockCategory.Defense, 18, "2x2x1", 300, 800, "Répare progressivement les blocs proches."),
            ("Block_18_Blindage", "BLINDAGE ÉLECTRODÉPOSÉ", BlockCategory.Defense, 8, "2x2x0,5", 350, 3000, "Plaque de blindage à haute résistance."),
            // ----- Equipements speciaux -----
            ("Block_19_Radar", "RADAR", BlockCategory.Special, 18, "1x1x2", 140, 400, "Révèle les ennemis proches sur la minicarte."),
            ("Block_20_DisqueBouclier", "DISQUE DE BOUCLIER", BlockCategory.Special, 26, "2x2x1", 280, 2500, "Projette une barrière énergétique directionnelle."),
        };

        // Supprime les assets qui ne sont plus dans la liste
        var keep = new HashSet<string>(defs.Select(d => d.file + ".asset"));
        foreach (var guid in AssetDatabase.FindAssets("t:BlockDefinition", new[] { BlockDataDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!keep.Contains(System.IO.Path.GetFileName(path)))
                AssetDatabase.DeleteAsset(path);
        }

        // Cree ou resynchronise chaque bloc
        var result = new List<BlockDefinition>();
        foreach (var d in defs)
        {
            string path = $"{BlockDataDir}/{d.file}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BlockDefinition>(path);
            bool isNew = asset == null;
            if (isNew)
                asset = ScriptableObject.CreateInstance<BlockDefinition>();

            asset.blockName = d.name;
            asset.category = d.cat;
            asset.costCpu = d.cpu;
            asset.sizeLabel = d.size;
            asset.weightKg = d.kg;
            asset.resistanceHp = d.hp;
            asset.description = d.desc;

            if (isNew)
                AssetDatabase.CreateAsset(asset, path);
            else
                EditorUtility.SetDirty(asset);
            result.Add(asset);
        }

        AssetDatabase.SaveAssets();
        return result.ToArray();
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

    private static RectTransform CreateUI(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return (RectTransform)go.transform;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        var rect = CreateUI(parent, name);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private static RectTransform CreatePanel(Component parent, string name, Color color) => CreatePanel(parent.transform, name, color);

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
