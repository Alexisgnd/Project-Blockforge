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
    private const string ChassisModelDir = "Assets/_Project/Art/Models/Blocks/Chassis";
    private const string MovementModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string WeaponModelDir = "Assets/_Project/Art/Models/Blocks/Weapons";
    private const string DefenseModelDir = "Assets/_Project/Art/Models/Blocks/Defense";
    private const string SpecialModelDir = "Assets/_Project/Art/Models/Blocks/Special";
    private const string IconDir = "Assets/_Project/Art/Textures/Icons";

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

        // ---------- Sauvegarde du robot (touche T) + popups ----------
        var statsHud = Object.FindFirstObjectByType<GarageStatsHUD>();
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

        var buildGrid = GameObject.Find("buildGrid");
        if (buildGrid != null)
        {
            var buildGo = new GameObject("BuildSystem");
            var build = buildGo.AddComponent<GarageBuildController>();
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
        }
        else
        {
            Debug.LogWarning("[GarageInventorySetup] 'buildGrid' introuvable : le systeme de pose " +
                             "de blocs n'a pas pu etre cable.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GarageInventorySetup] Inventaire genere ({blocks.Length} blocs). TAB pour ouvrir/fermer.");
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
        var nameOverlay = CreatePanel(canvas, "NamePopup", new Color(0f, 0f, 0f, 0.6f));
        StretchFull(nameOverlay);

        var namePanel = CreatePanel(nameOverlay, "Panel", new Color(0.05f, 0.08f, 0.13f, 0.98f));
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

        var (nameConfirm, _) = CreateButton(namePanel, "ConfirmButton", "SAUVEGARDER", 14, Accent, Color.white);
        var nameConfirmRect = nameConfirm.GetComponent<RectTransform>();
        SetAnchors(nameConfirmRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        nameConfirmRect.sizeDelta = new Vector2(200, 46);
        nameConfirmRect.anchoredPosition = new Vector2(108, 22);

        nameOverlay.gameObject.SetActive(false);

        // ---------- Popup "quitter sans sauvegarder" ----------
        var quitOverlay = CreatePanel(canvas, "QuitPopup", new Color(0f, 0f, 0f, 0.6f));
        StretchFull(quitOverlay);

        var quitPanel = CreatePanel(quitOverlay, "Panel", new Color(0.05f, 0.08f, 0.13f, 0.98f));
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
                                            new Color(0.62f, 0.18f, 0.18f, 1f), Color.white);
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
        // (noms, familles, categories, stats) et supprime les blocs qui n'y sont plus.
        // "art" = nom de base du FBX et de l'icone (Icon_<art>.png) quand le modele
        // existe ; laisser "" tant que l'asset 3D n'est pas produit (placeholder).
        var defs = new (string file, string name, string family, BlockCategory cat,
                        int cpu, string size, int kg, int hp, string art, string desc)[]
        {
            // ----- Chassis : 17 blocs (FBX Art/Models/Blocks/Chassis, pack GLB du 08/09/2026) -----
            // Pieces pleines (1 case) : armure blanche texturee
            ("Block_01_Cube", "CUBE", "Cube", BlockCategory.Chassis, 1, "1x1x1", 80, 1000, "Chassis_Cube", "Bloc de base. Solide et équilibré."),
            ("Block_02_Pente", "PENTE", "Pente", BlockCategory.Chassis, 1, "1x1x1", 70, 850, "Chassis_Pente", "Surface inclinée pour dévier les tirs."),
            ("Block_03_Coin", "COIN", "Coin", BlockCategory.Chassis, 1, "1x1x1", 55, 700, "Chassis_Coin", "Angle de finition pour carrosserie."),
            ("Block_04_Interieur", "COIN INTÉRIEUR", "Coin intérieur", BlockCategory.Chassis, 1, "1x1x1", 75, 900, "Chassis_CoinInterieur", "Angle rentrant pour raccorder deux pentes."),
            ("Block_Chassis_PenteArrondie", "PENTE ARRONDIE", "Pente", BlockCategory.Chassis, 1, "1x1x1", 72, 870, "Chassis_PenteArrondie", "Pente à profil courbe pour carrosseries lisses."),
            ("Block_Chassis_CoinArrondi", "COIN ARRONDI", "Coin", BlockCategory.Chassis, 1, "1x1x1", 60, 740, "Chassis_CoinArrondi", "Coin bombé pour arrondir les angles extérieurs."),
            ("Block_Chassis_CoinInterieurArrondi", "COIN INT. ARRONDI", "Coin intérieur", BlockCategory.Chassis, 1, "1x1x1", 78, 920, "Chassis_CoinInterieurArrondi", "Raccord courbe pour les angles rentrants."),
            ("Block_Chassis_PenteConcave", "PENTE CONCAVE", "Pente", BlockCategory.Chassis, 1, "1x1x1", 50, 620, "Chassis_PenteConcave", "Pente creusée pour gorges et prises d'air."),
            ("Block_Chassis_CoinConcave", "COIN CONCAVE", "Coin", BlockCategory.Chassis, 1, "1x1x1", 35, 480, "Chassis_CoinConcave", "Coin évidé, ultra léger, pour les finitions fines."),
            ("Block_Chassis_CoinInterieurConcave", "COIN INT. CONCAVE", "Coin intérieur", BlockCategory.Chassis, 1, "1x1x1", 65, 800, "Chassis_CoinInterieurConcave", "Angle rentrant creusé pour les raccords de carrosserie."),
            ("Block_Chassis_Cone", "CÔNE", "Cône", BlockCategory.Chassis, 1, "1x1x1", 45, 600, "Chassis_Cone", "Pointe conique pour nez et pare-chocs."),
            ("Block_Chassis_Pyramide", "PYRAMIDE", "Pyramide", BlockCategory.Chassis, 1, "1x1x1", 40, 560, "Chassis_Pyramide", "Pointe à quatre pans pour pics et déflecteurs."),
            // Tiges metalliques (platines aux extremites, metal brosse)
            ("Block_Rod_Court", "TIGE COURTE", "Tige", BlockCategory.Chassis, 1, "1x1x1", 18, 260, "Rod_Court", "Barre courte pour relier deux points rapprochés."),
            ("Block_Rod_Long", "TIGE LONGUE", "Tige", BlockCategory.Chassis, 1, "1x1x2", 32, 400, "Rod_Long", "Barre longue pour bras et perches déportées."),
            ("Block_Rod_Arc", "TIGE ARC", "Tige", BlockCategory.Chassis, 2, "2x1x2", 35, 420, "Rod_Arc", "Barre courbée pour arceaux et carrosseries arrondies."),
            ("Block_Rod_Diag2D", "TIGE DIAGONALE 2D", "Tige", BlockCategory.Chassis, 2, "2x1x2", 30, 380, "Rod_Diag2D", "Barre en biais reliant deux cases décalées sur un même plan."),
            ("Block_Rod_Diag3D", "TIGE DIAGONALE 3D", "Tige", BlockCategory.Chassis, 2, "2x2x2", 34, 410, "Rod_Diag3D", "Barre en biais reliant deux cases décalées dans l'espace."),

            // ----- Mouvement : roues (Scout -> Monster ; FBX Art/Models/Blocks/Movement) -----
            ("Block_Wheel_N1_Scout", "WHEEL SCOUT", "Roues", BlockCategory.Mouvement, 4, "1x1x1", 90, 450, "Wheel_Scout", "Roue légère de reconnaissance. Vitesse maximale, adhérence minimale."),
            ("Block_Wheel_N2_Discover", "WHEEL DISCOVER", "Roues", BlockCategory.Mouvement, 6, "1x1x1", 120, 600, "Wheel_Discover", "Roue polyvalente. Bon compromis vitesse / adhérence."),
            ("Block_Wheel_N3_Pathfinder", "WHEEL PATHFINDER", "Roues", BlockCategory.Mouvement, 8, "2x1x2", 170, 780, "Wheel_Pathfinder", "Roue tout-chemin à crampons moyens. Franchit les débris."),
            ("Block_Wheel_N4_Stormer", "WHEEL STORMER", "Roues", BlockCategory.Mouvement, 11, "2x1x2", 230, 950, "Wheel_Stormer", "Roue de course renforcée. Accélération brutale sur terrain dur."),
            ("Block_Wheel_N5_Geoterrain", "WHEEL GEOTERRAIN", "Roues", BlockCategory.Mouvement, 14, "2x1x2", 310, 1200, "Wheel_Geoterrain", "Roue tout-terrain à flancs épais. Absorbe les reliefs."),
            ("Block_Wheel_N6_Monster", "WHEEL MONSTER", "Roues", BlockCategory.Mouvement, 18, "3x1x3", 420, 1500, "Wheel_Monster", "Roue géante. Écrase les obstacles mais alourdit le châssis."),
            // ----- Mouvement : chenilles -----
            ("Block_07_Chenilles", "CHENILLES", "Chenilles", BlockCategory.Mouvement, 10, "3x1x1", 400, 1500, "", "Traction lourde. Franchit tous les terrains."),
            // ----- Mouvement : pattes d'insecte (FBX Art/Models/Blocks/Movement, source Tools/Blender/InsectLegs.blend) -----
            ("Block_InsectLeg_N1_Walker", "INSECT LEG WALKER", "Pattes d'insecte", BlockCategory.Mouvement, 10, "2x2x1", 190, 700, "InsectLeg_Walker", "Patte articulée légère. Escalade souple et silencieuse."),
            ("Block_InsectLeg_N2_Soldier", "INSECT LEG SOLDIER", "Pattes d'insecte", BlockCategory.Mouvement, 15, "2x2x2", 280, 1000, "InsectLeg_Soldier", "Patte de combat renforcée. Stabilise les châssis lourds en pente."),
            // ----- Mouvement : lames de survol (Squall -> Hurricane) -----
            ("Block_HoverBlade_N1_Squall", "HOVER BLADE SQUALL", "Lames de survol", BlockCategory.Mouvement, 8, "2x1x2", 130, 380, "", "Sustentation basse altitude. Glisse rapide et nerveuse."),
            ("Block_HoverBlade_N2_Thunder", "HOVER BLADE THUNDER", "Lames de survol", BlockCategory.Mouvement, 11, "2x1x2", 160, 450, "", "Lame de survol équilibrée. Portance stable à vitesse moyenne."),
            ("Block_HoverBlade_N3_Storm", "HOVER BLADE STORM", "Lames de survol", BlockCategory.Mouvement, 14, "2x1x2", 190, 520, "", "Lame renforcée. Supporte les châssis intermédiaires."),
            ("Block_HoverBlade_N4_Tempest", "HOVER BLADE TEMPEST", "Lames de survol", BlockCategory.Mouvement, 18, "2x1x2", 230, 600, "", "Double flux de sustentation. Bonne tenue sous le feu."),
            ("Block_HoverBlade_N5_Tornado", "HOVER BLADE TORNADO", "Lames de survol", BlockCategory.Mouvement, 22, "3x1x3", 280, 680, "", "Turbine de survol large. Soulève les carrosseries lourdes."),
            ("Block_HoverBlade_N6_Hurricane", "HOVER BLADE HURRICANE", "Lames de survol", BlockCategory.Mouvement, 27, "3x1x3", 340, 780, "", "Sustentation maximale. Réservée aux châssis les plus massifs."),
            // ----- Mouvement : helices (FBX Art/Models/Blocks/Movement) -----
            ("Block_RotorBlade_Recon", "ROTOR RECON", "Hélices", BlockCategory.Mouvement, 10, "2x1x2", 120, 350, "RotorBlade_Recon", "Rotor bipale léger. Vol stationnaire agile et discret."),
            ("Block_RotorBlade_Invader", "ROTOR INVADER", "Hélices", BlockCategory.Mouvement, 14, "2x1x2", 170, 500, "RotorBlade_Invader", "Rotor tripale équilibré. Portance stable pour châssis moyens."),
            ("Block_RotorBlade_Assault", "ROTOR ASSAULT", "Hélices", BlockCategory.Mouvement, 18, "2x1x2", 230, 650, "RotorBlade_Assault", "Rotor quadripale surpuissant. Soulève les châssis blindés."),
            // ----- Mouvement : ailes -----
            ("Block_11_Ailes", "AILES", "Ailes", BlockCategory.Mouvement, 10, "3x1x2", 130, 400, "", "Portance horizontale à grande vitesse."),
            // ----- Mouvement : ailerons (Hawk -> Bat ; FBX Art/Models/Blocks/Movement, source Tools/Blender/Rudders.blend, pack GLB du 11/09/2026) -----
            ("Block_Rudder_N1_Hawk", "RUDDER HAWK", "Ailerons", BlockCategory.Mouvement, 6, "1x2x1", 60, 300, "Rudder_N1_Hawk", "Aileron léger de reconnaissance. Virages vifs à faible vitesse."),
            ("Block_Rudder_N2_Falcon", "RUDDER FALCON", "Ailerons", BlockCategory.Mouvement, 8, "1x3x1", 75, 360, "Rudder_N2_Falcon", "Dérive polyvalente à rail titane. Bon compromis stabilité / réactivité."),
            ("Block_Rudder_N3_Kestrel", "RUDDER KESTREL", "Ailerons", BlockCategory.Mouvement, 10, "1x3x1", 90, 420, "Rudder_N3_Kestrel", "Gouverne à charnière renforcée. Tient le cap sous le feu."),
            ("Block_Rudder_N4_Eagle", "RUDDER EAGLE", "Ailerons", BlockCategory.Mouvement, 13, "1x3x1", 110, 500, "Rudder_N4_Eagle", "Aileron blindé à longeron relevé. Stabilise les châssis moyens."),
            ("Block_Rudder_N5_VampireBat", "RUDDER VAMPIRE BAT", "Ailerons", BlockCategory.Mouvement, 16, "1x3x1", 135, 580, "Rudder_N5_VampireBat", "Dérive composite à lame graphite. Corrige les trajectoires des châssis lourds."),
            ("Block_Rudder_N6_Albatross", "RUDDER ALBATROSS", "Ailerons", BlockCategory.Mouvement, 20, "1x3x1", 160, 660, "Rudder_N6_Albatross", "Grande dérive à pointe dorée. Contrôle précis à haute vitesse."),
            ("Block_Rudder_N7_Bat", "RUDDER BAT", "Ailerons", BlockCategory.Mouvement, 24, "1x4x1", 190, 760, "Rudder_N7_Bat", "Aileron d'élite à double lame et liseré or. Réservé aux châssis massifs."),
            // ----- Mouvement : propulseurs (Lynx -> Cheetah ; FBX Art/Models/Blocks/Movement, tuyere vers l'arriere) -----
            ("Block_Thruster_N1_Lynx", "THRUSTER LYNX", "Propulseurs", BlockCategory.Mouvement, 9, "1x1x2", 150, 420, "Thruster_N1_Lynx", "Propulseur d'appoint. Poussée courte, consommation faible."),
            ("Block_Thruster_N2_Panther", "THRUSTER PANTHER", "Propulseurs", BlockCategory.Mouvement, 12, "1x1x2", 190, 500, "Thruster_N2_Panther", "Poussée directionnelle équilibrée. Bon rapport poids / puissance."),
            ("Block_Thruster_N3_Leopard", "THRUSTER LEOPARD", "Propulseurs", BlockCategory.Mouvement, 16, "1x1x2", 240, 580, "Thruster_N3_Leopard", "Tuyère renforcée. Accélération soutenue en ligne droite."),
            ("Block_Thruster_N4_Puma", "THRUSTER PUMA", "Propulseurs", BlockCategory.Mouvement, 21, "1x1x3", 300, 670, "Thruster_N4_Puma", "Propulseur lourd à postcombustion. Consomme beaucoup."),
            ("Block_Thruster_N5_Cheetah", "THRUSTER CHEETAH", "Propulseurs", BlockCategory.Mouvement, 26, "1x1x3", 370, 760, "Thruster_N5_Cheetah", "Poussée maximale. Projette les châssis les plus lourds."),

            // ----- Armes offensives : lasers N1 a N6 (FBX Art/Models/Blocks/Weapons) -----
            ("Block_Laser_N1_Wasp", "LASER WASP", "Laser", BlockCategory.Armes, 10, "1x1x1", 120, 400, "Laser_N1_Wasp", "Tourelle laser légère à cadence élevée."),
            ("Block_Laser_N2_Hornet", "LASER HORNET", "Laser", BlockCategory.Armes, 14, "1x1x1", 160, 500, "Laser_N2_Hornet", "Laser à double condensateur. Bon équilibre dégâts/poids."),
            ("Block_Laser_N3_Blaster", "LASER BLASTER", "Laser", BlockCategory.Armes, 18, "1x1x1", 220, 600, "Laser_N3_Blaster", "Canon laser à faisceau concentré. Perce les blindages légers."),
            ("Block_Laser_N4_Vaporizer", "LASER VAPORIZER", "Laser", BlockCategory.Armes, 24, "1x1x1", 280, 700, "Laser_N4_Vaporizer", "Émetteur haute énergie. Vaporise les surfaces exposées."),
            ("Block_Laser_N5_Disintegrator", "LASER DISINTEGRATOR", "Laser", BlockCategory.Armes, 30, "1x1x1", 360, 850, "Laser_N5_Disintegrator", "Faisceau à désintégration soutenue. Dégâts continus massifs."),
            ("Block_Laser_N6_Leviathan", "LASER LEVIATHAN", "Laser", BlockCategory.Armes, 38, "1x1x1", 480, 1000, "Laser_N6_Leviathan", "Batterie laser triple. L'arme ultime des châssis lourds."),
            // ----- Armes offensives : lanceurs de plasma (Pulser -> Goliathon) -----
            ("Block_Plasma_N1_Pulser", "PLASMA PULSER", "Lanceur de plasma", BlockCategory.Armes, 12, "1x1x1", 150, 420, "", "Lanceur de plasma compact. Salves rapides à courte portée."),
            ("Block_Plasma_N2_Disruptor", "PLASMA DISRUPTOR", "Lanceur de plasma", BlockCategory.Armes, 17, "1x1x1", 200, 520, "", "Charge plasmique instable. Déséquilibre les châssis touchés."),
            ("Block_Plasma_N3_Bombarder", "PLASMA BOMBARDER", "Lanceur de plasma", BlockCategory.Armes, 22, "2x1x2", 270, 640, "", "Tir en cloche à zone d'impact large. Idéal contre les groupes."),
            ("Block_Plasma_N4_Ravager", "PLASMA RAVAGER", "Lanceur de plasma", BlockCategory.Armes, 28, "2x1x2", 350, 760, "", "Projectiles surchauffés. Fait fondre les blindages moyens."),
            ("Block_Plasma_N5_Devastator", "PLASMA DEVASTATOR", "Lanceur de plasma", BlockCategory.Armes, 35, "2x2x2", 450, 900, "", "Mortier plasma lourd. Dégâts de zone dévastateurs."),
            ("Block_Plasma_N6_Goliathon", "PLASMA GOLIATHON", "Lanceur de plasma", BlockCategory.Armes, 44, "3x2x2", 580, 1100, "", "Batterie plasma de siège. Pulvérise tout ce qui reste debout."),
            // ----- Armes offensives : canons electriques (Piercer -> Erazer) -----
            ("Block_Rail_N1_Piercer", "RAIL PIERCER", "Canon électrique", BlockCategory.Armes, 16, "1x1x3", 210, 450, "", "Canon à rail léger. Traverse les blocs fins d'un seul tir."),
            ("Block_Rail_N2_Penetrator", "RAIL PENETRATOR", "Canon électrique", BlockCategory.Armes, 22, "1x1x3", 280, 550, "", "Rail à double bobine. Perforation nette à longue portée."),
            ("Block_Rail_N3_Decimator", "RAIL DECIMATOR", "Canon électrique", BlockCategory.Armes, 30, "2x1x4", 380, 680, "", "Canon électrique lourd. Transperce plusieurs blocs alignés."),
            ("Block_Rail_N4_Erazer", "RAIL ERAZER", "Canon électrique", BlockCategory.Armes, 40, "2x1x4", 500, 820, "", "Rail de siège. Un tir, une ligne entière effacée."),
            // ----- Armes offensives : lames de Tesla (Slicer -> Nova) -----
            ("Block_Tesla_N1_Slicer", "TESLA SLICER", "Lame de Tesla", BlockCategory.Armes, 12, "1x1x2", 140, 500, "", "Lame électrifiée courte. Tranche au contact."),
            ("Block_Tesla_N2_Ripper", "TESLA RIPPER", "Lame de Tesla", BlockCategory.Armes, 19, "2x1x2", 210, 650, "", "Double lame à arc électrique. Déchire les blindages au corps à corps."),
            ("Block_Tesla_N3_Nova", "TESLA NOVA", "Lame de Tesla", BlockCategory.Armes, 28, "2x1x2", 300, 820, "", "Lame à décharge en étoile. Frappe tous les blocs adjacents."),

            // ----- Materiaux defensifs : distributeurs nano (Blinder -> Constructor) -----
            ("Block_Nano_N1_Blinder", "NANO BLINDER", "Distributeur nano / Healer", BlockCategory.Defense, 14, "1x1x1", 180, 550, "", "Nuage de nanites aveuglant. Brouille les capteurs adverses."),
            ("Block_Nano_N2_Mender", "NANO MENDER", "Distributeur nano / Healer", BlockCategory.Defense, 18, "2x2x1", 300, 800, "", "Répare progressivement les blocs proches."),
            ("Block_Nano_N3_Constructor", "NANO CONSTRUCTOR", "Distributeur nano / Healer", BlockCategory.Defense, 24, "2x2x1", 380, 950, "", "Reconstruit les blocs détruits en pleine bataille."),
            // ----- Materiaux defensifs : blindage -----
            ("Block_18_Blindage", "BLINDAGE ÉLECTRODÉPOSÉ", "Blindage électrodéposé", BlockCategory.Defense, 8, "2x2x0,5", 350, 3000, "", "Plaque de blindage à haute résistance."),

            // ----- Equipements speciaux (FBX Art/Models/Blocks/Special) -----
            ("Block_19_Radar", "RADAR", "Radar", BlockCategory.Special, 18, "1x1x2", 140, 400, "", "Révèle les ennemis proches sur la minicarte."),
            // Disque de bouclier : emetteur vers l'avant, bloc de fixation a l'arriere (source Tools/Blender/ShieldDisk.blend)
            ("Block_20_DisqueBouclier", "DISQUE DE BOUCLIER", "Disque de bouclier", BlockCategory.Special, 26, "2x2x1", 280, 2500, "ShieldDisk", "Projette une barrière énergétique directionnelle."),
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
            asset.sizeLabel = d.size;
            asset.weightKg = d.kg;
            asset.resistanceHp = d.hp;
            asset.description = d.desc;

            // Blocs a vrai modele FBX + icone rendue depuis Blender.
            // Les blocs sans "art" gardent le placeholder : on ne touche
            // ni a leur icone ni a leur previewPrefab existants.
            if (!string.IsNullOrEmpty(d.art))
            {
                string modelDir = ModelDirFor(d.cat);
                asset.previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{modelDir}/{d.art}.fbx");
                asset.icon = LoadBlockIcon(d.art);
                if (asset.previewPrefab == null)
                    Debug.LogWarning($"[GarageInventorySetup] FBX introuvable : {modelDir}/{d.art}.fbx");
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
