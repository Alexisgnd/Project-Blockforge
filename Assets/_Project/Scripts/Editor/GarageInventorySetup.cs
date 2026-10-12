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
// GarageCanvas et de la Toolbar). S'applique a la scene garage
// ouverte, sinon a Garage.unity ; recable
// aussi le systeme de pose (grille du vaisseau, indicateur
// miroir, switch d'outil et roue de couleur du spray).
// =========================================================

public static class GarageInventorySetup
{
    private const string BlockDataDir = "Assets/_Project/Data/Blocks";
    private const string ChassisModelDir = "Assets/_Project/Art/Models/Blocks/Chassis";
    private const string MovementModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string WeaponModelDir = "Assets/_Project/Art/Models/Blocks/Weapons";
    private const string DefenseModelDir = "Assets/_Project/Art/Models/Blocks/Defense";
    private const string SpecialModelDir = "Assets/_Project/Art/Models/Blocks/Special";
    private const string IconDir = "Assets/_Project/Art/Textures/Icons";
    private const string BlockPrefabDir = "Assets/_Project/Art/Prefabs/Blocks";

    private static readonly Color SheetBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Background, 0.97f);
    private static readonly Color HeaderBg = BlockforgeTheme.Background;
    private static readonly Color CardBg = BlockforgeTheme.WithAlpha(BlockforgeTheme.Surface, 0.95f);
    private static readonly Color PanelBg = BlockforgeTheme.Surface;
    private static readonly Color IconBg = BlockforgeTheme.SurfaceRaised;
    private static readonly Color BadgeBg = BlockforgeTheme.AccentMuted;
    private static readonly Color FieldBg = BlockforgeTheme.SurfaceSunken;
    private static readonly Color Accent = BlockforgeTheme.Accent;
    private static readonly Color TextMain = BlockforgeTheme.TextPrimary;
    private static readonly Color TextDim = BlockforgeTheme.TextSecondary;

    [MenuItem("Blockforge/Setup Garage Inventory")]
    public static void Setup()
    {
        var blocks = EnsureBlocks();

        if (!BlockforgeScenes.OpenForSetup(BlockforgeScenes.ResolveGarageScenePath(), out var scene))
            return;

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

        // ---------- Grille de blocs (defilement vertical : la liste depasse la zone) ----------
        var gridScroll = CreateUI(sheet, "GridScroll");
        SetAnchors(gridScroll, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        gridScroll.sizeDelta = new Vector2(1490, 330);
        gridScroll.anchoredPosition = new Vector2(24, -62);
        var scrollRect = gridScroll.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        var viewport = CreateUI(gridScroll, "Viewport");
        StretchFull(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        scrollRect.viewport = viewport;

        var grid = CreateUI(viewport, "Grid");
        SetAnchors(grid, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        grid.sizeDelta = new Vector2(0, 330);
        grid.anchoredPosition = Vector2.zero;
        scrollRect.content = grid;
        var gridFitter = grid.gameObject.AddComponent<ContentSizeFitter>();
        gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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
        var player = Object.FindAnyObjectByType<PlayerGarageController>();
        if (player != null)
            toDisable.Add(player);
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
        {
            if (mb != null && mb.GetType().Name.Contains("Plier") && mb is not PlierBlockPreview && mb is not PlierHologram)
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

        // ---------- Sauvegarde du robot (touche T) + popups ----------
        var statsHud = Object.FindAnyObjectByType<GarageStatsHUD>();
        if (statsHud != null)
        {
            if (!canvasGo.TryGetComponent<GarageSaveController>(out var saveController))
                saveController = canvasGo.AddComponent<GarageSaveController>();
            saveController.statsHud = statsHud;
            saveController.inventory = inventory;
            saveController.disableWhileOpen = inventory.disableWhileOpen;
            BuildSavePopups(canvasGo.transform, saveController);
            EditorUtility.SetDirty(canvasGo);
        }
        else
        {
            Debug.LogWarning("[GarageInventorySetup] GarageStatsHUD introuvable : la sauvegarde (T) " +
                             "n'a pas pu etre cablee. Lance d'abord Setup Garage UI.");
        }

        // ---------- Systeme de pose de blocs ----------
        var oldBuild = GameObject.Find("BuildSystem");
        if (oldBuild != null)
            Object.DestroyImmediate(oldBuild);

        GarageBuildController build = null;
        var buildGrid = FindGridRoot();
        if (buildGrid != null)
        {
            var buildGo = new GameObject("BuildSystem");
            build = buildGo.AddComponent<GarageBuildController>();
            build.gridRoot = buildGrid.transform;
            build.inventory = inventory;
            build.toolbar = canvasGo.transform.Find("Toolbar")?.GetComponent<GarageToolbar>();
            build.statsHud = statsHud;

            var cameraGo = GameObject.Find("Camera");
            if (cameraGo != null)
                build.viewCamera = cameraGo.GetComponent<Camera>();

            if (supportPlate != null)
                build.plierPreview = supportPlate.GetComponent<PlierBlockPreview>();

            build.ghostValidMaterial = GetOrCreateGhostMaterial("GhostValid", new Color(0.25f, 0.85f, 1f, 0.4f));
            build.ghostInvalidMaterial = GetOrCreateGhostMaterial("GhostInvalid", new Color(1f, 0.25f, 0.25f, 0.4f));

            // Indicateur du mode miroir dans la legende (GarageUISetup)
            build.mirrorChip = canvasGo.transform.Find("ControlsPanel/Key_MIROIR")?.GetComponent<Image>();
            build.mirrorLabel = canvasGo.transform.Find("ControlsPanel/Ctrl_MIROIR")?.GetComponent<TMP_Text>();
        }
        else
        {
            Debug.LogWarning("[GarageInventorySetup] Grille du vaisseau introuvable (objet 'buildGrid' ou lignes " +
                             "BuildGrid_*) : le systeme de pose de blocs n'a pas pu etre cable.");
        }

        // BuildSystem et inventaire viennent d'etre recrees : le switch d'outil et
        // la roue de couleur du spray (cables par Setup Paint Spray) pointaient
        // encore sur les anciens objets.
        foreach (var switcher in Object.FindObjectsByType<GarageToolSwitcher>(FindObjectsInactive.Include))
        {
            switcher.buildController = build;
            switcher.inventory = inventory;
            EditorUtility.SetDirty(switcher);
        }
        foreach (var wheel in Object.FindObjectsByType<PaintSprayColorWheel>(FindObjectsInactive.Include))
        {
            wheel.inventory = inventory;
            EditorUtility.SetDirty(wheel);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        // Design system Blockforge : formes, etats, typo, libelles anglais
        DesignSystemApply.ApplyToOpenScene();
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GarageInventorySetup] Inventaire genere ({blocks.Length} blocs). TAB pour ouvrir/fermer.");
    }

    // Racine de la grille de construction : l'objet "buildGrid" de l'ancien
    // vaisseau, sinon le parent des lignes BuildGrid_* du Mothership (V2)
    private static GameObject FindGridRoot()
    {
        var named = GameObject.Find("buildGrid");
        if (named != null)
            return named;

        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (r.gameObject.name.StartsWith("BuildGrid_", System.StringComparison.OrdinalIgnoreCase) && r.transform.parent != null)
                return r.transform.parent.gameObject;
        }
        return null;
    }

    // =========================================================
    // MATERIAUX FANTOMES (HDRP Unlit transparent)
    // =========================================================

    private static Material GetOrCreateGhostMaterial(string name, Color color)
    {
        string dir = "Assets/_Project/Art/Garage";
        EnsureFolder(dir);
        string path = $"{dir}/{name}.mat";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("HDRP/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetFloat("_SurfaceType", 1f); // transparent
        mat.SetColor("_UnlitColor", color);
        try
        {
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mat);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GarageInventorySetup] Validation HDRP du materiau {name} : {e.Message}");
        }
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // =========================================================
    // POPUPS SAUVEGARDE / QUITTER
    // =========================================================

    private static void BuildSavePopups(Transform canvas, GarageSaveController saveController)
    {
        foreach (var name in new[] { "NamePopup", "QuitPopup" })
        {
            var old = canvas.Find(name);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
        }

        // ---------- Popup "nom du robot" ----------
        var nameOverlay = CreatePanel(canvas, "NamePopup", BlockforgeTheme.Scrim);
        StretchFull(nameOverlay);

        var namePanel = CreatePanel(nameOverlay, "Panel", BlockforgeTheme.WithAlpha(BlockforgeTheme.Surface, 0.98f));
        SetAnchors(namePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        namePanel.sizeDelta = new Vector2(480, 250);
        namePanel.anchoredPosition = new Vector2(0, 20);

        var nameTitle = CreateText(namePanel, "Title", "NOM DU ROBOT", 17, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(nameTitle, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        nameTitle.offsetMin = new Vector2(24, -56);
        nameTitle.offsetMax = new Vector2(-24, -16);

        var inputRect = CreatePanel(namePanel, "NameInput", FieldBg);
        SetAnchors(inputRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        inputRect.offsetMin = new Vector2(24, -130);
        inputRect.offsetMax = new Vector2(-24, -82);

        var input = inputRect.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = inputRect.GetComponent<Image>();
        var textArea = CreateUI(inputRect, "Text Area");
        StretchFull(textArea);
        textArea.offsetMin = new Vector2(14, 6);
        textArea.offsetMax = new Vector2(-14, -6);
        textArea.gameObject.AddComponent<RectMask2D>();
        var placeholder = CreateText(textArea, "Placeholder", "Nom du robot...", 15, TextDim, TextAlignmentOptions.Left, FontStyles.Italic);
        StretchFull(placeholder);
        var inputText = CreateText(textArea, "Text", "", 15, TextMain, TextAlignmentOptions.Left, FontStyles.Normal);
        StretchFull(inputText);
        input.textViewport = textArea;
        input.textComponent = inputText.GetComponent<TMP_Text>();
        input.placeholder = placeholder.GetComponent<TMP_Text>();
        input.characterLimit = 40;

        var (nameCancel, _) = CreateButton(namePanel, "CancelButton", "ANNULER", 14, CardBg, TextMain);
        var nameCancelRect = nameCancel.GetComponent<RectTransform>();
        SetAnchors(nameCancelRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        nameCancelRect.sizeDelta = new Vector2(200, 46);
        nameCancelRect.anchoredPosition = new Vector2(-108, 22);

        var (nameConfirm, _) = CreateButton(namePanel, "ConfirmButton", "SAUVEGARDER", 14, Accent, BlockforgeTheme.OnAccent);
        var nameConfirmRect = nameConfirm.GetComponent<RectTransform>();
        SetAnchors(nameConfirmRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        nameConfirmRect.sizeDelta = new Vector2(200, 46);
        nameConfirmRect.anchoredPosition = new Vector2(108, 22);

        nameOverlay.gameObject.SetActive(false);

        // ---------- Popup "quitter sans sauvegarder" ----------
        var quitOverlay = CreatePanel(canvas, "QuitPopup", BlockforgeTheme.Scrim);
        StretchFull(quitOverlay);

        var quitPanel = CreatePanel(quitOverlay, "Panel", BlockforgeTheme.WithAlpha(BlockforgeTheme.Surface, 0.98f));
        SetAnchors(quitPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        quitPanel.sizeDelta = new Vector2(560, 250);
        quitPanel.anchoredPosition = new Vector2(0, 20);

        var quitTitle = CreateText(quitPanel, "Title", "QUITTER LE GARAGE ?", 17, Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(quitTitle, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        quitTitle.offsetMin = new Vector2(24, -56);
        quitTitle.offsetMax = new Vector2(-24, -16);

        var quitBody = CreateText(quitPanel, "Body",
            "Des modifications n'ont pas été sauvegardées.\nQuitter sans sauvegarder ?",
            14.5f, TextDim, TextAlignmentOptions.Center, FontStyles.Normal);
        SetAnchors(quitBody, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        quitBody.offsetMin = new Vector2(24, -140);
        quitBody.offsetMax = new Vector2(-24, -64);

        var (quitCancel, _) = CreateButton(quitPanel, "CancelButton", "ANNULER", 14, CardBg, TextMain);
        var quitCancelRect = quitCancel.GetComponent<RectTransform>();
        SetAnchors(quitCancelRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        quitCancelRect.sizeDelta = new Vector2(230, 46);
        quitCancelRect.anchoredPosition = new Vector2(-128, 22);

        var (quitConfirm, _) = CreateButton(quitPanel, "ConfirmButton", "QUITTER SANS SAUVER", 14,
                                            BlockforgeTheme.Danger, Color.white);
        var quitConfirmRect = quitConfirm.GetComponent<RectTransform>();
        SetAnchors(quitConfirmRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        quitConfirmRect.sizeDelta = new Vector2(230, 46);
        quitConfirmRect.anchoredPosition = new Vector2(128, 22);

        quitOverlay.gameObject.SetActive(false);

        // ---------- Cablage ----------
        saveController.namePopup = nameOverlay.gameObject;
        saveController.nameInput = input;
        saveController.nameCancelButton = nameCancel;
        saveController.nameConfirmButton = nameConfirm;
        saveController.quitPopup = quitOverlay.gameObject;
        saveController.quitCancelButton = quitCancel;
        saveController.quitConfirmButton = quitConfirm;

        // Bouton EDIT du bandeau de slot (genere par GarageUISetup)
        var editButton = canvas.Find("SlotBar/EditButton");
        if (editButton != null)
            saveController.editButton = editButton.GetComponent<Button>();
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
    // DONNEES : LES BLOCS
    // =========================================================

    // Public : aussi appele par les one-shots post-compilation
    // (LaserWeaponBlockSetup, RotorBladeBlockSetup)
    public static BlockDefinition[] EnsureBlocks()
    {
        EnsureFolder(BlockDataDir);

        // Liste canonique : le setup resynchronise les assets sur cette liste
        // (noms, familles, categories, stats, empreintes) et supprime les blocs
        // qui n'y sont plus.
        // "footprint" = empreinte en cases "LxHxP" (largeur X, hauteur Y, profondeur Z)
        // validee le 15/09/2026 a partir des dimensions reelles des FBX (tache 05 :
        // le modele est ajuste dans cette boite par BlockPreviewFactory, sauf
        // echelle native). Mesuree au repos pour les blocs animes : le radar
        // deplie et les lames de Tesla en action deborderont, c'est accepte.
        // L'ancrage (face plaquee sur la boite) est deduit de la famille par
        // AnchorFor ; les tiges gardent leur echelle native.
        // "art" = nom de base du FBX et de l'icone (Icon_<art>.png) quand le modele
        // existe ; laisser "" tant que l'asset 3D n'est pas produit (placeholder).
        var defs = new (string file, string name, string family, BlockCategory cat,
                        int cpu, string footprint, int kg, int hp, string art, string desc)[]
        {
            // ----- Chassis : 17 blocs (FBX Art/Models/Blocks/Chassis, pack GLB du 08/09/2026) -----
            // Pieces pleines (1 case) : armure blanche texturee
            ("Block_01_Cube", "Cube", "Cube", BlockCategory.Chassis, 1, "1x1x1", 80, 1000, "Chassis_Cube", "The basic block. Solid and balanced."),
            ("Block_02_Pente", "Slope", "Slope", BlockCategory.Chassis, 1, "1x1x1", 70, 850, "Chassis_Pente", "An angled face that deflects incoming fire."),
            ("Block_03_Coin", "Corner", "Corner", BlockCategory.Chassis, 1, "1x1x1", 55, 700, "Chassis_Coin", "A finishing angle for bodywork."),
            ("Block_04_Interieur", "Inner Corner", "Inner corner", BlockCategory.Chassis, 1, "1x1x1", 75, 900, "Chassis_CoinInterieur", "A recessed angle that joins two slopes."),
            ("Block_Chassis_PenteArrondie", "Round Slope", "Slope", BlockCategory.Chassis, 1, "1x1x1", 72, 870, "Chassis_PenteArrondie", "A slope with a curved profile for smooth bodywork."),
            ("Block_Chassis_CoinArrondi", "Round Corner", "Corner", BlockCategory.Chassis, 1, "1x1x1", 60, 740, "Chassis_CoinArrondi", "A domed corner that rounds off outer edges."),
            ("Block_Chassis_CoinInterieurArrondi", "Round Inner Corner", "Inner corner", BlockCategory.Chassis, 1, "1x1x1", 78, 920, "Chassis_CoinInterieurArrondi", "A curved joint for recessed angles."),
            ("Block_Chassis_PenteConcave", "Concave Slope", "Slope", BlockCategory.Chassis, 1, "1x1x1", 50, 620, "Chassis_PenteConcave", "A hollowed slope for channels and air intakes."),
            ("Block_Chassis_CoinConcave", "Concave Corner", "Corner", BlockCategory.Chassis, 1, "1x1x1", 35, 480, "Chassis_CoinConcave", "A hollow, ultra-light corner for fine finishing."),
            ("Block_Chassis_CoinInterieurConcave", "Concave Inner Corner", "Inner corner", BlockCategory.Chassis, 1, "1x1x1", 65, 800, "Chassis_CoinInterieurConcave", "A hollowed recessed angle for bodywork joints."),
            ("Block_Chassis_Cone", "Cone", "Cone", BlockCategory.Chassis, 1, "1x1x1", 45, 600, "Chassis_Cone", "A conical tip for noses and bumpers."),
            ("Block_Chassis_Pyramide", "Pyramid", "Pyramid", BlockCategory.Chassis, 1, "1x1x1", 40, 560, "Chassis_Pyramide", "A four-sided tip for spikes and deflectors."),
            // Tiges metalliques (platines aux extremites, metal brosse)
            ("Block_Rod_Court", "Short Rod", "Rod", BlockCategory.Chassis, 1, "1x1x1", 18, 260, "Rod_Court", "A short bar that links two close points."),
            ("Block_Rod_Long", "Long Rod", "Rod", BlockCategory.Chassis, 1, "1x2x1", 32, 400, "Rod_Long", "A long bar for arms and outrigger booms."),
            ("Block_Rod_Arc", "Arc Rod", "Rod", BlockCategory.Chassis, 2, "1x1x1", 35, 420, "Rod_Arc", "A curved bar for roll hoops and rounded bodywork."),
            ("Block_Rod_Diag2D", "Diagonal Rod 2D", "Rod", BlockCategory.Chassis, 2, "1x1x1", 30, 380, "Rod_Diag2D", "An angled bar linking two offset cells on one plane."),
            ("Block_Rod_Diag3D", "Diagonal Rod 3D", "Rod", BlockCategory.Chassis, 2, "1x1x1", 34, 410, "Rod_Diag3D", "An angled bar linking two offset cells in space."),

            // ----- Mouvement : roues (Scout -> Monster ; FBX Art/Models/Blocks/Movement) -----
            ("Block_Wheel_N1_Scout", "Wheel Scout", "Wheels", BlockCategory.Mouvement, 4, "1x2x2", 90, 450, "Wheel_Scout", "A light scout wheel. Top speed, minimal grip."),
            ("Block_Wheel_N2_Discover", "Wheel Discover", "Wheels", BlockCategory.Mouvement, 6, "1x2x2", 120, 600, "Wheel_Discover", "A versatile wheel. A good balance of speed and grip."),
            ("Block_Wheel_N3_Pathfinder", "Wheel Pathfinder", "Wheels", BlockCategory.Mouvement, 8, "2x2x2", 170, 780, "Wheel_Pathfinder", "An all-road wheel with medium studs. Rolls over debris."),
            ("Block_Wheel_N4_Stormer", "Wheel Stormer", "Wheels", BlockCategory.Mouvement, 11, "2x3x3", 230, 950, "Wheel_Stormer", "A reinforced racing wheel. Brutal acceleration on hard ground."),
            ("Block_Wheel_N5_Geoterrain", "Wheel Geoterrain", "Wheels", BlockCategory.Mouvement, 14, "2x3x3", 310, 1200, "Wheel_Geoterrain", "An all-terrain wheel with thick sidewalls. Soaks up bumps."),
            ("Block_Wheel_N6_Monster", "Wheel Monster", "Wheels", BlockCategory.Mouvement, 18, "2x3x3", 420, 1500, "Wheel_Monster", "A giant wheel. Crushes obstacles but weighs the chassis down."),
            // ----- Mouvement : chenilles (Bison -> Mammoth ; FBX animes Art/Models/Blocks/Movement + prefab Art/Prefabs/Blocks, source Tools/Blender/TankTracks.blend) -----
            ("Block_TankTrack_N1_Bison", "Tank Track Bison", "Tank tracks", BlockCategory.Mouvement, 10, "1x1x3", 400, 1500, "TankTrack_N1_Bison", "A compact three-wheel track. Reliable traction on any terrain."),
            ("Block_TankTrack_N2_Rhino", "Tank Track Rhino", "Tank tracks", BlockCategory.Mouvement, 14, "2x2x3", 520, 1900, "TankTrack_N2_Rhino", "An armoured track with wide links. Takes head-on hits."),
            ("Block_TankTrack_N3_Elephant", "Tank Track Elephant", "Tank tracks", BlockCategory.Mouvement, 19, "2x2x3", 680, 2400, "TankTrack_N3_Elephant", "A long four-wheel track. Steadies heavy chassis."),
            ("Block_TankTrack_N4_Mammoth", "Tank Track Mammoth", "Tank tracks", BlockCategory.Mouvement, 25, "2x2x4", 900, 3000, "TankTrack_N4_Mammoth", "A five-wheel siege track. Crushes everything in its path."),
            // ----- Mouvement : pattes d'insecte (FBX Art/Models/Blocks/Movement, source Tools/Blender/InsectLegs.blend) -----
            ("Block_InsectLeg_N1_Walker", "Insect Leg Walker", "Insect legs", BlockCategory.Mouvement, 10, "1x2x1", 190, 700, "InsectLeg_Walker", "A light jointed leg. Climbs smoothly and quietly."),
            ("Block_InsectLeg_N2_Soldier", "Insect Leg Soldier", "Insect legs", BlockCategory.Mouvement, 15, "2x2x1", 280, 1000, "InsectLeg_Soldier", "A reinforced combat leg. Steadies heavy chassis on slopes."),
            // ----- Mouvement : lames de survol (Squall -> Hurricane ; FBX Art/Models/Blocks/Movement, attache vers l'arriere, source Tools/Blender/HoverBlades.blend, pack GLB du 14/09/2026) -----
            ("Block_HoverBlade_N1_Squall", "Hover Blade Squall", "Hover blades", BlockCategory.Mouvement, 8, "2x1x2", 130, 380, "HoverBlade_N1_Squall", "Low-altitude lift. Fast, twitchy gliding."),
            ("Block_HoverBlade_N2_Thunder", "Hover Blade Thunder", "Hover blades", BlockCategory.Mouvement, 11, "2x1x2", 160, 450, "HoverBlade_N2_Thunder", "A balanced hover blade. Steady lift at medium speed."),
            ("Block_HoverBlade_N3_Storm", "Hover Blade Storm", "Hover blades", BlockCategory.Mouvement, 14, "2x1x2", 190, 520, "HoverBlade_N3_Storm", "A reinforced blade. Carries mid-weight chassis."),
            ("Block_HoverBlade_N4_Tempest", "Hover Blade Tempest", "Hover blades", BlockCategory.Mouvement, 18, "3x1x3", 230, 600, "HoverBlade_N4_Tempest", "Dual lift flow. Holds steady under fire."),
            ("Block_HoverBlade_N5_Tornado", "Hover Blade Tornado", "Hover blades", BlockCategory.Mouvement, 22, "3x1x3", 280, 680, "HoverBlade_N5_Tornado", "A wide hover turbine. Lifts heavy bodywork."),
            ("Block_HoverBlade_N6_Hurricane", "Hover Blade Hurricane", "Hover blades", BlockCategory.Mouvement, 27, "3x1x3", 340, 780, "HoverBlade_N6_Hurricane", "Maximum lift. Built for the most massive chassis."),
            // ----- Mouvement : helices (FBX Art/Models/Blocks/Movement) -----
            ("Block_RotorBlade_Recon", "Rotor Recon", "Rotors", BlockCategory.Mouvement, 10, "3x1x3", 120, 350, "RotorBlade_Recon", "A light two-blade rotor. Agile, quiet hovering."),
            ("Block_RotorBlade_Invader", "Rotor Invader", "Rotors", BlockCategory.Mouvement, 14, "3x1x3", 170, 500, "RotorBlade_Invader", "A balanced three-blade rotor. Steady lift for medium chassis."),
            ("Block_RotorBlade_Assault", "Rotor Assault", "Rotors", BlockCategory.Mouvement, 18, "3x1x3", 230, 650, "RotorBlade_Assault", "An overpowered four-blade rotor. Lifts armoured chassis."),
            // ----- Mouvement : ailerons (Hawk -> Bat ; FBX Art/Models/Blocks/Movement, source Tools/Blender/Rudders.blend, pack GLB du 11/09/2026) -----
            ("Block_Rudder_N1_Hawk", "Rudder Hawk", "Rudders", BlockCategory.Mouvement, 6, "1x2x1", 60, 300, "Rudder_N1_Hawk", "A light scout rudder. Sharp turns at low speed."),
            ("Block_Rudder_N2_Falcon", "Rudder Falcon", "Rudders", BlockCategory.Mouvement, 8, "2x3x1", 75, 360, "Rudder_N2_Falcon", "A versatile fin on a titanium rail. Balances stability and response."),
            ("Block_Rudder_N3_Kestrel", "Rudder Kestrel", "Rudders", BlockCategory.Mouvement, 10, "2x3x1", 90, 420, "Rudder_N3_Kestrel", "A rudder with a reinforced hinge. Holds course under fire."),
            ("Block_Rudder_N4_Eagle", "Rudder Eagle", "Rudders", BlockCategory.Mouvement, 13, "2x3x1", 110, 500, "Rudder_N4_Eagle", "An armoured rudder with a raised spar. Steadies medium chassis."),
            ("Block_Rudder_N5_VampireBat", "Rudder Vampire Bat", "Rudders", BlockCategory.Mouvement, 16, "2x3x1", 135, 580, "Rudder_N5_VampireBat", "A composite fin with a graphite blade. Corrects the path of heavy chassis."),
            ("Block_Rudder_N6_Albatross", "Rudder Albatross", "Rudders", BlockCategory.Mouvement, 20, "2x4x1", 160, 660, "Rudder_N6_Albatross", "A large fin with a golden tip. Precise control at high speed."),
            ("Block_Rudder_N7_Bat", "Rudder Bat", "Rudders", BlockCategory.Mouvement, 24, "2x4x1", 190, 760, "Rudder_N7_Bat", "An elite twin-blade rudder with gold trim. Built for massive chassis."),
            // ----- Mouvement : propulseurs (Lynx -> Cheetah ; FBX Art/Models/Blocks/Movement, tuyere vers l'arriere) -----
            ("Block_Thruster_N1_Lynx", "Thruster Lynx", "Thrusters", BlockCategory.Mouvement, 9, "2x2x2", 150, 420, "Thruster_N1_Lynx", "A booster thruster. Short thrust, low consumption."),
            ("Block_Thruster_N2_Panther", "Thruster Panther", "Thrusters", BlockCategory.Mouvement, 12, "2x2x2", 190, 500, "Thruster_N2_Panther", "Balanced directional thrust. A good power-to-weight ratio."),
            ("Block_Thruster_N3_Leopard", "Thruster Leopard", "Thrusters", BlockCategory.Mouvement, 16, "2x2x3", 240, 580, "Thruster_N3_Leopard", "A reinforced nozzle. Sustained acceleration in a straight line."),
            ("Block_Thruster_N4_Puma", "Thruster Puma", "Thrusters", BlockCategory.Mouvement, 21, "2x2x3", 300, 670, "Thruster_N4_Puma", "A heavy afterburning thruster. Uses a lot of energy."),
            ("Block_Thruster_N5_Cheetah", "Thruster Cheetah", "Thrusters", BlockCategory.Mouvement, 26, "2x2x3", 370, 760, "Thruster_N5_Cheetah", "Maximum thrust. Launches the heaviest chassis."),

            // ----- Armes offensives : lasers N1 a N6 (FBX Art/Models/Blocks/Weapons) -----
            ("Block_Laser_N1_Wasp", "Laser Wasp", "Laser", BlockCategory.Armes, 10, "1x1x1", 120, 400, "Laser_N1_Wasp", "A light laser turret with a high fire rate."),
            ("Block_Laser_N2_Hornet", "Laser Hornet", "Laser", BlockCategory.Armes, 14, "1x1x1", 160, 500, "Laser_N2_Hornet", "A twin-capacitor laser. A good damage-to-weight balance."),
            ("Block_Laser_N3_Blaster", "Laser Blaster", "Laser", BlockCategory.Armes, 18, "1x1x2", 220, 600, "Laser_N3_Blaster", "A focused-beam laser cannon. Pierces light armour."),
            ("Block_Laser_N4_Vaporizer", "Laser Vaporizer", "Laser", BlockCategory.Armes, 24, "1x1x2", 280, 700, "Laser_N4_Vaporizer", "A high-energy emitter. Vaporizes exposed surfaces."),
            ("Block_Laser_N5_Disintegrator", "Laser Disintegrator", "Laser", BlockCategory.Armes, 30, "2x2x3", 360, 850, "Laser_N5_Disintegrator", "A sustained disintegration beam. Massive continuous damage."),
            ("Block_Laser_N6_Leviathan", "Laser Leviathan", "Laser", BlockCategory.Armes, 38, "2x2x4", 480, 1000, "Laser_N6_Leviathan", "A triple laser battery. The ultimate weapon for heavy chassis."),
            // ----- Armes offensives : lanceurs de plasma (Pulser -> Goliathon ; FBX Art/Models/Blocks/Weapons, canon vers l'avant, source Tools/Blender/PlasmaWeapons.blend, pack GLB du 14/09/2026) -----
            ("Block_Plasma_N1_Pulser", "Plasma Pulser", "Plasma launcher", BlockCategory.Armes, 12, "1x2x2", 150, 420, "Plasma_N1_Pulser", "A compact plasma launcher. Fast short-range bursts."),
            ("Block_Plasma_N2_Disruptor", "Plasma Disruptor", "Plasma launcher", BlockCategory.Armes, 17, "2x2x3", 200, 520, "Plasma_N2_Disruptor", "An unstable plasma charge. Knocks hit chassis off balance."),
            ("Block_Plasma_N3_Bombarder", "Plasma Bombarder", "Plasma launcher", BlockCategory.Armes, 22, "2x2x3", 270, 640, "Plasma_N3_Bombarder", "Lobbed shots with a wide blast area. Ideal against groups."),
            ("Block_Plasma_N4_Ravager", "Plasma Ravager", "Plasma launcher", BlockCategory.Armes, 28, "2x3x4", 350, 760, "Plasma_N4_Ravager", "Superheated projectiles. Melts medium armour."),
            ("Block_Plasma_N5_Devastator", "Plasma Devastator", "Plasma launcher", BlockCategory.Armes, 35, "2x3x4", 450, 900, "Plasma_N5_Devastator", "A heavy plasma mortar. Devastating area damage."),
            ("Block_Plasma_N6_Goliathon", "Plasma Goliathon", "Plasma launcher", BlockCategory.Armes, 44, "3x3x4", 580, 1100, "Plasma_N6_Goliathon", "A siege plasma battery. Pulverizes whatever is still standing."),
            // ----- Armes offensives : canons electriques (Piercer -> Erazer ; FBX Art/Models/Blocks/Weapons, canon vers l'avant, pied recentre, source Tools/Blender/RailCannons.blend, pack GLB du 15/09/2026) -----
            ("Block_Rail_N1_Piercer", "Rail Piercer", "Rail cannon", BlockCategory.Armes, 16, "2x2x4", 210, 450, "Rail_N1_Piercer", "A light rail cannon. Punches through thin blocks in one shot."),
            ("Block_Rail_N2_Penetrator", "Rail Penetrator", "Rail cannon", BlockCategory.Armes, 22, "2x2x5", 280, 550, "Rail_N2_Penetrator", "A twin-coil rail. Clean penetration at long range."),
            ("Block_Rail_N3_Decimator", "Rail Decimator", "Rail cannon", BlockCategory.Armes, 30, "2x3x5", 380, 680, "Rail_N3_Decimator", "A heavy rail cannon. Pierces several blocks in a row."),
            ("Block_Rail_N4_Erazer", "Rail Erazer", "Rail cannon", BlockCategory.Armes, 40, "2x3x6", 500, 820, "Rail_N4_Erazer", "A siege rail. One shot erases a whole line."),
            // ----- Armes offensives : lames de Tesla (Slicer -> Nova ; FBX Art/Models/Blocks/Weapons, semelle a plat, arc dans l'axe avant/arriere, source Tools/Blender/TeslaBlades.blend, pack GLB du 15/09/2026) -----
            ("Block_Tesla_N1_Slicer", "Tesla Slicer", "Tesla blade", BlockCategory.Armes, 12, "1x2x4", 140, 500, "Tesla_N1_Slicer", "A short electrified blade. Cuts on contact."),
            ("Block_Tesla_N2_Ripper", "Tesla Ripper", "Tesla blade", BlockCategory.Armes, 19, "1x2x4", 210, 650, "Tesla_N2_Ripper", "A twin arc blade. Tears through armour up close."),
            ("Block_Tesla_N3_Nova", "Tesla Nova", "Tesla blade", BlockCategory.Armes, 28, "1x3x5", 300, 820, "Tesla_N3_Nova", "A star-discharge blade. Hits every adjacent block."),

            // ----- Materiaux defensifs : distributeurs nano (Blinder -> Constructor ; FBX Art/Models/Blocks/Defense, emetteur vers l'avant, empties Turret/Mount conserves, source Tools/Blender/NanoDisruptors.blend, pack GLB du 15/09/2026) -----
            ("Block_Nano_N1_Blinder", "Nano Blinder", "Nano dispenser", BlockCategory.Defense, 14, "1x2x2", 180, 550, "Nano_N1_Blinder", "A blinding nanite cloud. Scrambles enemy sensors."),
            ("Block_Nano_N2_Mender", "Nano Mender", "Nano dispenser", BlockCategory.Defense, 18, "1x2x3", 300, 800, "Nano_N2_Mender", "Gradually repairs nearby blocks."),
            ("Block_Nano_N3_Constructor", "Nano Constructor", "Nano dispenser", BlockCategory.Defense, 24, "1x2x3", 380, 950, "Nano_N3_Constructor", "Rebuilds destroyed blocks mid-battle."),
            // ----- Materiaux defensifs : electroplates (T2 -> T9 ; FBX Art/Models/Blocks/Defense, face protectrice vers l'avant, pivot sur la fixation arriere, source Tools/Blender/Electroplates.blend, pack GLB du 15/09/2026) -----
            ("Block_Electroplate_T2", "Electroplate T2", "Electroplate", BlockCategory.Defense, 4, "1x2x1", 120, 900, "Electroplate_T2", "A light ogive plate. Your first electroplated armour."),
            ("Block_Electroplate_T3", "Electroplate T3", "Electroplate", BlockCategory.Defense, 6, "2x3x1", 160, 1300, "Electroplate_T3", "A shouldered plate. Covers the flank of a light chassis."),
            ("Block_Electroplate_T4", "Electroplate T4", "Electroplate", BlockCategory.Defense, 8, "1x3x1", 200, 1700, "Electroplate_T4", "A tall, narrow blade. Protects masts and turrets."),
            ("Block_Electroplate_T5", "Electroplate T5", "Electroplate", BlockCategory.Defense, 10, "2x3x1", 250, 2200, "Electroplate_T5", "A bevelled wing with a splitter. Deflects angled fire."),
            ("Block_Electroplate_T6", "Electroplate T6", "Electroplate", BlockCategory.Defense, 13, "2x3x1", 310, 2800, "Electroplate_T6", "A bent, double-faced plate. Wraps a bodywork corner."),
            ("Block_Electroplate_T7", "Electroplate T7", "Electroplate", BlockCategory.Defense, 16, "2x4x1", 380, 3500, "Electroplate_T7", "A large reinforced panel. The main armour of medium chassis."),
            ("Block_Electroplate_T8", "Electroplate T8", "Electroplate", BlockCategory.Defense, 20, "2x4x1", 460, 4300, "Electroplate_T8", "A stepped plate. Absorbs heavy impacts."),
            ("Block_Electroplate_T9", "Electroplate T9", "Electroplate", BlockCategory.Defense, 24, "3x4x1", 550, 5200, "Electroplate_T9", "A suspended delta wing. The ultimate armour for massive chassis."),

            // ----- Equipements speciaux (FBX Art/Models/Blocks/Special) -----
            // Radar : panneaux repliables (FBX anime Art/Models/Blocks/Special/Radar.fbx + prefab variant Art/Prefabs/Blocks/Radar.prefab, source Tools/Blender/Radar.blend, GLB "Radar_Module_Folding_v3" du 15/09/2026)
            ("Block_19_Radar", "Radar", "Radar", BlockCategory.Special, 18, "1x2x2", 140, 400, "Radar", "Shows nearby enemies on the minimap."),
            // Disque de bouclier : emetteur vers l'avant, bloc de fixation a l'arriere (source Tools/Blender/ShieldDisk.blend)
            ("Block_20_DisqueBouclier", "Shield Disk", "Shield disk", BlockCategory.Special, 26, "2x2x2", 280, 2500, "ShieldDisk", "Projects a directional energy barrier."),
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
            asset.family = d.family;
            asset.category = d.cat;
            asset.costCpu = d.cpu;
            asset.weightKg = d.kg;
            asset.resistanceHp = d.hp;
            asset.description = d.desc;
            asset.footprint = ParseFootprint(d.footprint);
            asset.anchor = AnchorFor(d.file);
            asset.nativeScale = d.file.StartsWith("Block_Rod_");
            // Tout sauf le chassis s'accroche a la face visee (une arme se monte
            // aussi bien sur un flanc que sur le dessus) ; les pieces de chassis
            // restent droites et ne font que du lacet.
            asset.orientToFace = d.cat != BlockCategory.Chassis;

            // Blocs a vrai modele FBX + icone rendue depuis Blender.
            // Les blocs sans "art" gardent le placeholder : on ne touche
            // ni a leur icone ni a leur previewPrefab existants.
            if (!string.IsNullOrEmpty(d.art))
            {
                // Blocs animes (chenilles) : un prefab variant du FBX avec l'Animator
                // cable existe dans Art/Prefabs/Blocks et prime sur le FBX brut.
                string modelDir = ModelDirFor(d.cat);
                var variant = AssetDatabase.LoadAssetAtPath<GameObject>($"{BlockPrefabDir}/{d.art}.prefab");
                asset.previewPrefab = variant != null
                    ? variant
                    : AssetDatabase.LoadAssetAtPath<GameObject>($"{modelDir}/{d.art}.fbx");
                asset.icon = LoadBlockIcon(d.art);
                if (asset.previewPrefab == null)
                    Debug.LogWarning($"[GarageInventorySetup] FBX introuvable : {modelDir}/{d.art}.fbx");
                asset.modelBounds = asset.previewPrefab != null
                    ? ComputeModelBounds(asset.previewPrefab)
                    : default;
            }

            if (isNew)
                AssetDatabase.CreateAsset(asset, path);
            else
                EditorUtility.SetDirty(asset);
            result.Add(asset);
        }

        AssetDatabase.SaveAssets();
        return result.ToArray();
    }

    // "LxHxP" -> empreinte en cases (largeur X, hauteur Y, profondeur Z)
    private static Vector3Int ParseFootprint(string footprint)
    {
        var parts = footprint.Split('x');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y) || !int.TryParse(parts[2], out int z)
            || x < 1 || y < 1 || z < 1)
        {
            throw new System.ArgumentException($"[GarageInventorySetup] Empreinte invalide : \"{footprint}\" (attendu LxHxP).");
        }
        return new Vector3Int(x, y, z);
    }

    // Bounds exacts d'un modele dans le repere de son prefab (pose importee) :
    // sommets des MeshFilter transformes en monde, l'instance a l'identite.
    // renderer.bounds surestime les pieces tournees (tourelle a 45 degres =
    // boite 2x plus large) et ferait sous-dimensionner le bloc a l'ajustement.
    // Les meshes skinnes (chenilles) gardent leurs bounds de renderer, exacts
    // au repos. Calcule dans une scene de preview pour ne pas toucher la scene
    // ouverte ; les sommets sont lisibles dans l'editeur meme sans Read/Write.
    private static Bounds ComputeModelBounds(GameObject prefab)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            return TryGetExactBounds(instance, out var bounds) ? bounds : default;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // Bounds exacts (monde) d'une hierarchie instanciee : sommets des MeshFilter
    // transformes, meshes skinnes par leurs bounds de renderer. False si vide.
    // Editeur seulement : les sommets ne sont pas lisibles en build sans Read/Write.
    private static readonly List<Vector3> vertexScratch = new();

    public static bool TryGetExactBounds(GameObject root, out Bounds bounds)
    {
        bool any = false;
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<Renderer>() == null)
                continue;
            var toWorld = mf.transform.localToWorldMatrix;
            mf.sharedMesh.GetVertices(vertexScratch); // sans copie de tableau, contrairement a .vertices
            foreach (var v in vertexScratch)
            {
                var w = toWorld.MultiplyPoint3x4(v);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
                any = true;
            }
        }

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh == null)
                continue;
            var b = smr.bounds;
            min = Vector3.Min(min, b.min);
            max = Vector3.Max(max, b.max);
            any = true;
        }

        bounds = new Bounds();
        if (any)
            bounds.SetMinMax(min, max);
        return any;
    }

    // Face d'ancrage par famille, dans le repere Unity (l'import FBX inverse X
    // par rapport a Blender ; verifie par Blockforge > Report Block Bounds) :
    // roues plaquees par le moyeu (-X), pattes par la plaque de hanche (+X),
    // plaques / lames de survol / disque de bouclier par leur fixation
    // arriere (-Z) ; tout le reste est pose sur sa base.
    private static BlockAnchor AnchorFor(string file)
    {
        if (file.StartsWith("Block_Wheel_")) return BlockAnchor.Left;
        if (file.StartsWith("Block_InsectLeg_")) return BlockAnchor.Right;
        if (file.StartsWith("Block_HoverBlade_") || file.StartsWith("Block_Electroplate_")
            || file == "Block_20_DisqueBouclier")
            return BlockAnchor.Back;
        return BlockAnchor.Bottom;
    }

    // Dossier FBX correspondant a une categorie de bloc
    private static string ModelDirFor(BlockCategory category) => category switch
    {
        BlockCategory.Chassis => ChassisModelDir,
        BlockCategory.Mouvement => MovementModelDir,
        BlockCategory.Armes => WeaponModelDir,
        BlockCategory.Defense => DefenseModelDir,
        _ => SpecialModelDir,
    };

    // Charge l'icone PNG d'un bloc en Sprite (force l'import en Sprite au besoin)
    private static Sprite LoadBlockIcon(string fbxName)
    {
        string path = $"{IconDir}/Icon_{fbxName}.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[GarageInventorySetup] Icone introuvable : {path}");
        return sprite;
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
