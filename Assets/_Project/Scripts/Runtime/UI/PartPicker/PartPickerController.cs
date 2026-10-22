using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

// =========================================================
// PART PICKER DU BUILDER (UI Toolkit)
// La fenetre d'inventaire que le joueur ouvre au garage pour
// choisir la piece a poser. Seule a l'ecran pendant qu'elle est
// ouverte : le viewport continue de rendre sous un voile, le HUD
// du builder est masque par l'hote (VisibilityChanged).
// - Onglets A / B / M / W / E / C (lettre de la disposition
//   courante ; avec Alt quand la recherche a le clavier), bumpers
//   de la manette pour les faire defiler.
// - Grille 7 colonnes : image + nom (2 lignes max, ellipse), seule
//   la bordure change (survol, selection).
// - Recherche par nom a la frappe (120 ms), « / » donne le
//   clavier au champ, Echap vide la recherche puis ferme.
// - Selection = outil de pose courant (Selected) ; double clic,
//   Entree, Place ou glisser la tuile sur le viewport posent la
//   piece et ferment (Maj : reste ouvert). Rien n'est pose au-dela
//   du budget CPU de la reserve de la piece.
// - Selection et defilement memorises par onglet (session).
// - Reduce motion : ni fondu ni rotation de l'apercu.
// Donnees : Bind(IInventory, IRobotBudget). Couleurs et mesures :
// PickerTheme.uss ; textes et durees : PickerTheme.
// =========================================================

[DisallowMultipleComponent]
public class PartPickerController : MonoBehaviour
{
    [Tooltip("Theme du picker ; vide = Resources/PartPicker/PickerTheme")]
    public PickerTheme theme;
    [Tooltip("Lettre d'ouverture sur la disposition courante (Q) ; l'hote peut couper l'ecoute et appeler Open()")]
    public string openKey = "Q";
    public bool listenForOpenKey = true;

    // Outil de pose courant (clic, clavier, restauration d'un onglet)
    public event Action<PartDefinition> Selected;
    // Piece a poser ; true = Maj tenue, la fenetre reste ouverte
    public event Action<PartDefinition, bool> PlaceRequested;
    public event Action<PartDefinition> PinRequested;
    // null = ouvrir le Store ; sinon recherche a y lancer
    public event Action<string> StoreRequested;
    // Avancement du fondu (0 ferme -> 1 ouvert, deja adouci) : le HUD de l'hote suit
    public event Action<float> VisibilityChanged;
    public event Action Opened;
    public event Action Closed;

    public bool IsOpen { get; private set; }
    public bool IsVisible => fade > 0f;
    public PartDefinition Current => selected;

    private const int AllTab = 0;
    private const string Ellipsis = "…";

    private sealed class TabMemory
    {
        public string selectedId;
        public float scroll;
        public bool hasScroll;
        public int family;
        public int sort;
        public string searchSelectedId;
        public float searchScroll;
    }

    private sealed class Tile
    {
        public PartDefinition part;
        public VisualElement root, frame, image;
        public Label name;
        public float fittedWidth = -1f;
    }

    private IInventory inventory;
    private IRobotBudget budget;
    private UIDocument document;

    // Arbre
    private VisualElement root, dim, frame, window, tabsRow, popup, segments, sortSelect, countRow;
    private VisualElement gridArea, empty, footerLink, detailContent, detailEmpty, preview, statsBox, notice, noticeIcon;
    private VisualElement place, pin, close, dragGhost, dragGhostImage, emptyClear, emptyStore, searchBox;
    private Label title, searchPlaceholder, sortValue, emptyTitle, emptyBody, footerLabel, badge, detailName, detailDesc;
    private Label noticeTitle, noticeBody, detailEmptyText, placeLabel, emptyClearLabel, emptyStoreLabel, escKey;
    private VisualElement emptySuggest, emptySuggestLink;
    private Label emptySuggestBefore, emptySuggestWord, emptySuggestAfter;
    private string suggestion;
    private TextField searchField;
    private ScrollView grid;
    private Image previewImage;
    private readonly List<VisualElement> tabElements = new();

    // Etat
    private readonly Dictionary<string, Tile> tiles = new();
    private readonly List<Tile> visibleTiles = new();
    private readonly List<string> recent = new();
    private readonly Dictionary<string, int> ownedOrder = new();
    private TabMemory[] memory;
    private int activeTab;
    private string query = "";
    private PartDefinition selected;
    private bool popupOpen;
    private bool searchFocused;
    private IVisualElementScheduledItem debounce;
    private bool restoringScroll;
    private float pendingScroll = -1f;
    private int gridColumns = 7;
    private float gridGapX, gridGapY;
    private int nameLines = 2;
    private float dragThreshold = 6f;
    private float laidOutWidth = -1f;
    private bool layoutDirty = true;
    private Color previewBackdrop = Color.black;

    // Fondu
    private float fade;          // 0..1 lineaire
    private float detailFade = 1f;

    // Glisser
    private Tile pressedTile;
    private Vector2 pressPosition;
    private int pressPointer = -1;
    private bool dragging;

    // Entrees
    private Keyboard boundKeyboard;
    private string boundLayout;
    private readonly Dictionary<string, KeyControl> keyCache = new();
    private bool slashFocusRequested;
    private bool slashGuard;

    private PartPreviewStage stage;

    // =========================================================
    // CYCLE DE VIE
    // =========================================================

    private void Awake()
    {
        if (theme == null)
            theme = PickerTheme.Load();
        document = GetComponent<UIDocument>();
        if (document == null)
            document = gameObject.AddComponent<UIDocument>();
        if (document.panelSettings == null)
            document.panelSettings = theme.panelSettings != null ? theme.panelSettings : RuntimePanelSettings(theme);
        memory = new TabMemory[theme.tabs.Length];
        for (int i = 0; i < memory.Length; i++)
            memory[i] = new TabMemory();
        stage = new PartPreviewStage();
    }

    private void OnDisable()
    {
        UnbindKeyboard();
        stage?.SetActive(false);
    }

    private void OnDestroy()
    {
        Unbind();
        stage?.Dispose();
    }

    // Sans asset de setup : memes reglages que PickerPanelSettings, crees a la volee
    private static PanelSettings RuntimePanelSettings(PickerTheme theme)
    {
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.name = "PickerPanelSettings (runtime)";
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = theme.referenceResolution;
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 1f; // echelle sur la hauteur
        ps.sortingOrder = theme.sortingOrder;
        return ps;
    }

    // =========================================================
    // DONNEES
    // =========================================================

    public void Bind(IInventory inv, IRobotBudget robotBudget)
    {
        Unbind();
        inventory = inv;
        budget = robotBudget;
        if (inventory != null)
            inventory.Changed += OnInventoryChanged;
        if (budget != null)
            budget.Changed += OnBudgetChanged;
        if (EnsureBuilt())
            OnInventoryChanged();
    }

    private void Unbind()
    {
        if (inventory != null)
            inventory.Changed -= OnInventoryChanged;
        if (budget != null)
            budget.Changed -= OnBudgetChanged;
        inventory = null;
        budget = null;
    }

    private void OnInventoryChanged()
    {
        if (root == null)
            return;
        RebuildTiles();
        if (selected != null && (inventory == null || !inventory.IsOwned(selected.id)))
            SetSelection(null, false);
        Refresh();
    }

    private void OnBudgetChanged()
    {
        if (root != null)
            RefreshNotice();
    }

    // Outil deja en main a l'ouverture (la selection de l'onglet courant s'il n'en a pas)
    public void SetCurrent(string partId)
    {
        if (string.IsNullOrEmpty(partId))
            return;
        foreach (var m in memory)
        {
            if (m.selectedId == null)
                m.selectedId = partId;
        }
        if (root != null && IsOpen)
            Refresh();
    }

    // Selectionne une piece visible dans la liste courante (hote, outils) ; false sinon
    public bool SelectPart(string partId)
    {
        if (root == null || string.IsNullOrEmpty(partId))
            return false;
        var tile = visibleTiles.FirstOrDefault(t => t.part.id == partId);
        if (tile == null)
            return false;
        SetSelection(tile.part, true);
        return true;
    }

    public int ActiveTab => activeTab;
    public string Query => query;

    // =========================================================
    // OUVERTURE / FERMETURE
    // =========================================================

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen || !EnsureBuilt())
            return;
        IsOpen = true;
        root.style.display = DisplayStyle.Flex;
        dim.pickingMode = PickingMode.Position;
        CancelDrag();
        Refresh();
        if (BlockforgeTheme.ReduceMotion)
            SetFade(1f);
        else
            SetFade(fade);
        // Le clavier et la manette arrivent sur la tuile selectionnee (ou la premiere)
        root.schedule.Execute(FocusGridEntry).ExecuteLater(0);
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        ClosePopup();
        CancelDrag();
        SaveScroll();
        root?.focusController?.focusedElement?.Blur();
        if (dim != null)
            dim.pickingMode = PickingMode.Ignore;
        if (BlockforgeTheme.ReduceMotion)
            SetFade(0f);
        Closed?.Invoke();
    }

    private void SetFade(float value)
    {
        fade = Mathf.Clamp01(value);
        float eased = BlockforgeTheme.EaseOut(fade);
        if (root != null)
        {
            root.style.opacity = eased;
            if (fade <= 0f && !IsOpen)
            {
                root.style.display = DisplayStyle.None;
                stage?.SetActive(false);
            }
        }
        VisibilityChanged?.Invoke(eased);
    }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private bool EnsureBuilt()
    {
        if (root != null && root.panel != null)
            return true;
        if (document == null || document.rootVisualElement == null)
            return false;

        var asset = Resources.Load<VisualTreeAsset>(PickerTheme.TemplatePath);
        if (asset == null)
        {
            Debug.LogError($"[PartPicker] Template introuvable : Resources/{PickerTheme.TemplatePath}.uxml");
            return false;
        }

        var host = document.rootVisualElement;
        host.Clear();
        debounce = null;
        host.pickingMode = PickingMode.Ignore;
        var container = asset.Instantiate();
        container.pickingMode = PickingMode.Ignore;
        container.style.position = Position.Absolute;
        container.style.left = container.style.top = container.style.right = container.style.bottom = 0;
        host.Add(container);

        root = container.Q("picker");
        dim = root.Q("dim");
        frame = root.Q("frame");
        window = root.Q("window");
        tabsRow = root.Q("tabs");
        popup = root.Q("popup");
        title = root.Q<Label>("title");
        searchBox = root.Q("search");
        searchField = root.Q<TextField>("search-field");
        searchPlaceholder = root.Q<Label>("search-placeholder");
        segments = root.Q("segments");
        sortSelect = root.Q("sort");
        sortValue = root.Q<Label>("sort-value");
        countRow = root.Q("count");
        gridArea = root.Q("grid-area");
        grid = root.Q<ScrollView>("grid");
        empty = root.Q("empty");
        emptyTitle = root.Q<Label>("empty-title");
        emptyBody = root.Q<Label>("empty-body");
        emptySuggest = root.Q("empty-suggest");
        emptySuggestLink = root.Q("empty-suggest-link");
        emptySuggestBefore = root.Q<Label>("empty-suggest-before");
        emptySuggestWord = root.Q<Label>("empty-suggest-word");
        emptySuggestAfter = root.Q<Label>("empty-suggest-after");
        emptyClear = root.Q("empty-clear");
        emptyStore = root.Q("empty-store");
        emptyClearLabel = root.Q<Label>("empty-clear-label");
        emptyStoreLabel = root.Q<Label>("empty-store-label");
        footerLink = root.Q("footer-link");
        footerLabel = root.Q<Label>("footer-label");
        detailContent = root.Q("detail-content");
        detailEmpty = root.Q("detail-empty");
        detailEmptyText = root.Q<Label>("detail-empty-text");
        preview = root.Q("preview");
        previewImage = root.Q<Image>("preview-image");
        badge = root.Q<Label>("badge");
        detailName = root.Q<Label>("detail-name");
        detailDesc = root.Q<Label>("detail-desc");
        statsBox = root.Q("stats");
        notice = root.Q("notice");
        noticeIcon = root.Q("notice-icon");
        noticeTitle = root.Q<Label>("notice-title");
        noticeBody = root.Q<Label>("notice-body");
        place = root.Q("place");
        placeLabel = root.Q<Label>("place-label");
        pin = root.Q("pin");
        close = root.Q("close");
        escKey = root.Q<Label>("esc-key");
        dragGhost = root.Q("drag-ghost");
        dragGhostImage = root.Q("drag-ghost-image");

        // Textes du theme
        searchPlaceholder.text = theme.searchPlaceholder;
        placeLabel.text = Caps(theme.placeLabel);
        emptyClearLabel.text = Caps(theme.clearSearchButton);
        emptyStoreLabel.text = Caps(theme.searchStoreButton);
        detailEmptyText.text = theme.detailEmpty;
        escKey.text = theme.escKey;
        pin.tooltip = theme.pinLabel;
        close.tooltip = theme.closeLabel;

        BuildTabs();

        // Boutons
        MakeClickable(close, Close);
        MakeClickable(place, () => RequestPlace(selected));
        MakeClickable(pin, () => { if (selected != null) PinRequested?.Invoke(selected); });
        MakeClickable(emptyClear, ClearSearch);
        MakeClickable(emptyStore, () => StoreRequested?.Invoke(query));
        MakeClickable(footerLink, () =>
        {
            if (query.Length > 0) ClearSearch();
            else StoreRequested?.Invoke(null);
        });
        MakeClickable(sortSelect, TogglePopup);

        // « Did you mean plasma? » : le mot est un lien qui relance la recherche
        MakeClickable(emptySuggestLink, () =>
        {
            if (suggestion != null)
                RunSearch(suggestion);
        });

        // Recherche
        searchField.RegisterValueChangedCallback(OnSearchChanged);
        searchField.RegisterCallback<FocusInEvent>(_ => { searchFocused = true; searchBox.AddToClassList("pp-search--focus"); });
        searchField.RegisterCallback<FocusOutEvent>(_ => { searchFocused = false; searchBox.RemoveFromClassList("pp-search--focus"); });

        // Grille
        grid.mode = ScrollViewMode.Vertical;
        grid.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        grid.verticalScrollerVisibility = ScrollerVisibility.Auto;
        grid.RegisterCallback<CustomStyleResolvedEvent>(OnGridStyle);
        grid.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => LayoutTiles());
        grid.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => LayoutTiles());
        grid.verticalScroller.valueChanged += v =>
        {
            if (!restoringScroll)
                CurrentMemoryScroll(v);
        };

        preview.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            if (selected != null && selected.previewPrefab != null)
                ShowPreview(selected);
        });
        preview.RegisterCallback<CustomStyleResolvedEvent>(e =>
        {
            if (e.customStyle.TryGetValue(new CustomStyleProperty<Color>("--pp-preview-backdrop"), out var c))
                previewBackdrop = c;
        });
        root.RegisterCallback<CustomStyleResolvedEvent>(e =>
        {
            if (e.customStyle.TryGetValue(new CustomStyleProperty<float>("--pp-drag-threshold"), out var t))
                dragThreshold = t;
        });

        // Clavier / manette : anneau de focus seulement apres une navigation
        root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationMoveEvent>(_ => SetKeyboardMode(true), TrickleDown.TrickleDown);
        root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Tab)
                SetKeyboardMode(true);
        }, TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationMoveEvent>(OnNavigate);
        // Echap est gere dans Update (vider la recherche, puis fermer)
        root.RegisterCallback<NavigationCancelEvent>(e => e.StopPropagation());

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;
        fade = 0f;
        layoutDirty = true;

        RebuildTiles();
        Refresh();
        return true;
    }

    private void BuildTabs()
    {
        tabsRow.Clear();
        tabElements.Clear();
        for (int i = 0; i < theme.tabs.Length; i++)
        {
            var spec = theme.tabs[i];
            int index = i;
            var tab = new VisualElement();
            tab.AddToClassList("pp-tab");
            tab.AddToClassList("pp-focusable");
            tab.tooltip = spec.title;

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("pp-icon");
            icon.AddToClassList("pp-tab__icon");
            if (!string.IsNullOrEmpty(spec.iconClass))
                icon.AddToClassList(spec.iconClass);
            tab.Add(icon);

            var key = new Label(spec.key) { pickingMode = PickingMode.Ignore };
            key.AddToClassList("pp-tab__key");
            key.AddToClassList("pp-font-mono");
            tab.Add(key);

            var underline = new VisualElement { pickingMode = PickingMode.Ignore };
            underline.AddToClassList("pp-tab__underline");
            tab.Add(underline);
            tab.Add(Ring());

            MakeClickable(tab, () => SelectTab(index));
            tabsRow.Add(tab);
            tabElements.Add(tab);
        }
    }

    private static VisualElement Ring()
    {
        var ring = new VisualElement { pickingMode = PickingMode.Ignore };
        ring.AddToClassList("pp-ring");
        return ring;
    }

    private static void MakeClickable(VisualElement element, Action action)
    {
        element.focusable = true;
        element.tabIndex = 0;
        element.AddManipulator(new Clickable(() =>
        {
            if (element.enabledInHierarchy)
                action();
        }));
        element.RegisterCallback<NavigationSubmitEvent>(e =>
        {
            if (element.enabledInHierarchy)
                action();
            e.StopPropagation();
        });
    }

    private static string Caps(string text) => (text ?? "").ToUpperInvariant();

    // Nombres du design system (milliers groupes) : la balise TMP <space> de
    // BlockforgeTheme.Num n'existe pas en UI Toolkit, une espace fine la remplace
    private static string Num(long value) => BlockforgeTheme.Num(value).Replace(BlockforgeTheme.ThinSpace, "\u2009");
    private static string Num(float value, string format) => BlockforgeTheme.Num(value, format).Replace(BlockforgeTheme.ThinSpace, "\u2009");

    // Le moteur de texte rogne les espaces en bord de libelle : espaces insecables a la place
    private static string KeepEdgeSpaces(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        int start = 0, end = text.Length;
        while (start < end && text[start] == ' ') start++;
        while (end > start && text[end - 1] == ' ') end--;
        return new string('\u00A0', start) + text.Substring(start, end - start) + new string('\u00A0', text.Length - end);
    }

    // =========================================================
    // TUILES
    // =========================================================

    private void RebuildTiles()
    {
        if (grid == null)
            return;
        grid.contentContainer.Clear();
        tiles.Clear();
        visibleTiles.Clear();
        ownedOrder.Clear();
        if (inventory?.Owned == null)
            return;

        int order = 0;
        foreach (var part in inventory.Owned)
        {
            if (part == null || string.IsNullOrEmpty(part.id) || tiles.ContainsKey(part.id))
                continue;
            ownedOrder[part.id] = order++;
            tiles[part.id] = CreateTile(part);
        }
        layoutDirty = true;
    }

    private Tile CreateTile(PartDefinition part)
    {
        var t = new Tile { part = part };
        t.root = new VisualElement { focusable = true, tabIndex = 0 };
        t.root.AddToClassList("pp-tile");
        t.root.tooltip = part.name;

        t.frame = new VisualElement { pickingMode = PickingMode.Ignore };
        t.frame.AddToClassList("pp-tile__frame");
        var clip = new VisualElement { pickingMode = PickingMode.Ignore };
        clip.AddToClassList("pp-tile__clip");
        var gradient = new ForgeGradient();
        gradient.AddToClassList("pp-thumb-gradient");
        gradient.AddToClassList("pp-fill");
        clip.Add(gradient);
        t.image = new VisualElement { pickingMode = PickingMode.Ignore };
        t.image.AddToClassList("pp-tile__image");
        if (part.thumbnail != null)
            t.image.style.backgroundImage = new StyleBackground(part.thumbnail);
        clip.Add(t.image);
        t.frame.Add(clip);
        t.frame.Add(Ring());
        t.root.Add(t.frame);

        t.name = new Label(Caps(part.name)) { pickingMode = PickingMode.Ignore };
        t.name.AddToClassList("pp-tile__name");
        t.name.AddToClassList("pp-font-semibold");
        t.root.Add(t.name);

        t.root.RegisterCallback<PointerDownEvent>(e => OnTilePointerDown(t, e));
        t.root.RegisterCallback<PointerMoveEvent>(e => OnTilePointerMove(t, e));
        t.root.RegisterCallback<PointerUpEvent>(e => OnTilePointerUp(t, e));
        t.root.RegisterCallback<PointerCaptureOutEvent>(_ => { if (pressedTile == t) CancelDrag(); });
        t.root.RegisterCallback<NavigationSubmitEvent>(e =>
        {
            if (selected == t.part) RequestPlace(t.part);
            else SetSelection(t.part, true);
            e.StopPropagation();
        });
        t.root.RegisterCallback<FocusInEvent>(_ => grid.ScrollTo(t.root));
        return t;
    }

    private void OnGridStyle(CustomStyleResolvedEvent e)
    {
        var s = e.customStyle;
        if (s.TryGetValue(new CustomStyleProperty<float>("--pp-grid-columns"), out var c)) gridColumns = Mathf.Max(1, Mathf.RoundToInt(c));
        if (s.TryGetValue(new CustomStyleProperty<float>("--pp-grid-gap-x"), out var gx)) gridGapX = gx;
        if (s.TryGetValue(new CustomStyleProperty<float>("--pp-grid-gap-y"), out var gy)) gridGapY = gy;
        if (s.TryGetValue(new CustomStyleProperty<float>("--pp-tile-name-lines"), out var l)) nameLines = Mathf.Max(1, Mathf.RoundToInt(l));
        layoutDirty = true;
        LayoutTiles();
    }

    // 7 colonnes egales : largeur calculee sur la zone de contenu, ecarts du USS
    private void LayoutTiles()
    {
        if (grid == null)
            return;
        float width = grid.contentContainer.contentRect.width;
        if (float.IsNaN(width) || width <= 0f)
            return;

        float tileWidth = Mathf.Floor((width - (gridColumns - 1) * gridGapX) / gridColumns);
        if (tileWidth <= 0f)
            return;

        if (layoutDirty || !Mathf.Approximately(tileWidth, laidOutWidth))
        {
            laidOutWidth = tileWidth;
            layoutDirty = false;
            for (int i = 0; i < visibleTiles.Count; i++)
            {
                var t = visibleTiles[i];
                bool lastColumn = i % gridColumns == gridColumns - 1;
                t.root.style.width = tileWidth;
                t.root.style.marginRight = lastColumn ? 0f : gridGapX;
                t.root.style.marginBottom = gridGapY;
                t.frame.style.height = tileWidth;
                t.name.style.width = tileWidth;
            }
        }

        // Noms : deux lignes au plus, ellipse (mesure apres resolution du style)
        foreach (var t in visibleTiles)
        {
            if (!Mathf.Approximately(t.fittedWidth, tileWidth))
                FitName(t, tileWidth);
        }

        if (pendingScroll >= 0f)
            ApplyPendingScroll();
    }

    private void FitName(Tile t, float width)
    {
        string full = Caps(t.part.name);
        var label = t.name;
        float lineHeight = label.MeasureTextSize("Ag", width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;
        if (float.IsNaN(lineHeight) || lineHeight <= 0f)
            return;
        float maxHeight = lineHeight * nameLines + 0.5f;
        label.style.height = lineHeight * nameLines;
        t.fittedWidth = width;

        if (Measure(label, full, width) <= maxHeight)
        {
            label.text = full;
            return;
        }

        int lo = 0, hi = full.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Measure(label, full.Substring(0, mid).TrimEnd() + Ellipsis, width) <= maxHeight)
                lo = mid;
            else
                hi = mid - 1;
        }
        label.text = full.Substring(0, lo).TrimEnd() + Ellipsis;
    }

    private static float Measure(TextElement label, string text, float width) =>
        label.MeasureTextSize(text, width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;

    // =========================================================
    // ONGLETS ET FILTRES
    // =========================================================

    public void SelectTab(int index)
    {
        if (index < 0 || index >= theme.tabs.Length || memory == null)
            return;
        if (index == activeTab && root != null)
            return;
        SaveScroll();
        activeTab = index;
        ClosePopup();
        Refresh();
    }

    private TabMemory Memory => memory[Mathf.Clamp(activeTab, 0, memory.Length - 1)];

    private void CurrentMemoryScroll(float value)
    {
        if (query.Length > 0) Memory.searchScroll = value;
        else Memory.scroll = value;
        Memory.hasScroll = true;
    }

    private void SaveScroll()
    {
        if (grid != null && memory != null)
            CurrentMemoryScroll(grid.scrollOffset.y);
    }

    private PickerSortOption[] SortOptions()
    {
        if (activeTab == AllTab)
            return theme.allPartsSorts;
        bool damage = theme.tabs[activeTab].sortByDamage;
        return theme.categorySorts.Where(o => o != null && (damage || o.sort != PickerSort.ByDamage)).ToArray();
    }

    private PickerSort CurrentSort()
    {
        var options = SortOptions();
        if (options.Length == 0)
            return PickerSort.Alphabetical;
        return options[Mathf.Clamp(Memory.sort, 0, options.Length - 1)].sort;
    }

    // =========================================================
    // RAFRAICHISSEMENT
    // =========================================================

    private void Refresh()
    {
        if (root == null)
            return;

        var owned = inventory?.Owned ?? Array.Empty<PartDefinition>();
        var ownedList = owned.Where(p => p != null && tiles.ContainsKey(p.id)).ToList();
        bool searching = query.Length > 0;
        var spec = theme.tabs[activeTab];

        for (int i = 0; i < tabElements.Count; i++)
            tabElements[i].EnableInClassList("pp-tab--active", i == activeTab);

        title.text = Caps(searching ? string.Format(theme.searchTitleFormat, query) : spec.title);

        // Liste visible
        List<PartDefinition> list;
        List<PartDefinition> matches = null;
        if (searching)
        {
            matches = PartPickerLogic.Search(ownedList, query);
            list = activeTab == AllTab ? matches : matches.Where(p => p.category == spec.category).ToList();
        }
        else if (activeTab == AllTab)
        {
            list = ownedList.ToList();
            PartPickerLogic.Sort(list, CurrentSort(), recent, ownedOrder);
        }
        else
        {
            var families = spec.families ?? Array.Empty<PickerFamily>();
            int family = Mathf.Clamp(Memory.family, 0, families.Length);
            string familyLabel = family == 0 ? null : families[family - 1]?.label;
            list = ownedList.Where(p => p.category == spec.category &&
                                        (familyLabel == null || string.Equals(p.family, familyLabel, StringComparison.OrdinalIgnoreCase)))
                            .ToList();
            PartPickerLogic.Sort(list, CurrentSort(), recent, ownedOrder);
        }

        BuildFilterRow(searching, matches);
        BuildCount(searching, ownedList, matches, list);

        // Grille
        bool emptySearch = searching && matches.Count == 0;
        empty.style.display = emptySearch ? DisplayStyle.Flex : DisplayStyle.None;
        grid.style.display = emptySearch ? DisplayStyle.None : DisplayStyle.Flex;
        if (emptySearch)
            FillEmptySearch();

        var content = grid.contentContainer;
        content.Clear();
        visibleTiles.Clear();
        foreach (var part in list)
        {
            if (tiles.TryGetValue(part.id, out var t))
            {
                content.Add(t.root);
                visibleTiles.Add(t);
            }
        }
        layoutDirty = true;

        // Pied
        if (searching)
        {
            footerLabel.text = theme.clearSearchLink;
            footerLink.style.display = DisplayStyle.Flex;
        }
        else
        {
            int more = inventory?.NotOwnedCount ?? 0;
            footerLabel.text = string.Format(theme.storeLinkFormat, Num(more));
            footerLink.style.display = more > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Selection memorisee de l'onglet (ou outil courant s'il est visible)
        string want = searching ? Memory.searchSelectedId : Memory.selectedId;
        if (want == null && selected != null && !emptySearch)
            want = selected.id;
        var target = want != null ? list.FirstOrDefault(p => p.id == want) : null;
        if (target != null)
            SetSelection(target, target != selected);
        else
            SetSelection(null, false);

        // Defilement memorise
        pendingScroll = Memory.hasScroll ? (searching ? Memory.searchScroll : Memory.scroll) : 0f;
        LayoutTiles();
        root.schedule.Execute(ApplyPendingScroll).ExecuteLater(1);
    }

    private void ApplyPendingScroll()
    {
        if (pendingScroll < 0f || grid == null)
            return;
        restoringScroll = true;
        grid.scrollOffset = new Vector2(0f, pendingScroll);
        restoringScroll = false;
        // Garde la valeur jusqu'a ce que la grille ait la hauteur voulue
        if (grid.verticalScroller.highValue >= pendingScroll - 0.5f || pendingScroll <= 0f)
            pendingScroll = -1f;
    }

    private void BuildFilterRow(bool searching, List<PartDefinition> matches)
    {
        segments.Clear();
        sortSelect.style.display = !searching && activeTab != AllTab ? DisplayStyle.Flex : DisplayStyle.None;

        if (searching)
        {
            // Portee : All categories N · Weapons N · …
            AddSegment(theme.scopeAllLabel, matches.Count, activeTab == AllTab, () => SelectTab(AllTab));
            for (int i = 1; i < theme.tabs.Length; i++)
            {
                var tab = theme.tabs[i];
                int count = matches.Count(p => p.category == tab.category);
                if (count == 0 && i != activeTab)
                    continue;
                int index = i;
                AddSegment(tab.title, count, i == activeTab, () => SelectTab(index));
            }
            return;
        }

        if (activeTab == AllTab)
        {
            var sorts = theme.allPartsSorts;
            for (int i = 0; i < sorts.Length; i++)
            {
                int index = i;
                AddSegment(sorts[i].label, -1, Memory.sort == i, () => { Memory.sort = index; Refresh(); });
            }
            return;
        }

        var families = theme.tabs[activeTab].families ?? Array.Empty<PickerFamily>();
        AddSegment(theme.familyAllLabel, -1, Memory.family == 0, () => { Memory.family = 0; Refresh(); });
        for (int i = 0; i < families.Length; i++)
        {
            int index = i + 1;
            AddSegment(families[i].label, -1, Memory.family == index, () => { Memory.family = index; Refresh(); });
        }

        var options = SortOptions();
        int sort = Mathf.Clamp(Memory.sort, 0, Mathf.Max(0, options.Length - 1));
        sortValue.text = options.Length > 0 ? options[sort].label : "";
    }

    private void AddSegment(string label, int count, bool active, Action onClick)
    {
        var seg = new VisualElement();
        seg.AddToClassList("pp-segment");
        seg.AddToClassList("pp-focusable");
        if (active)
            seg.AddToClassList("pp-segment--active");
        var text = new Label(Caps(label)) { pickingMode = PickingMode.Ignore };
        text.AddToClassList("pp-segment__label");
        text.AddToClassList("pp-font-semibold");
        seg.Add(text);
        if (count >= 0)
        {
            var num = new Label(Num(count)) { pickingMode = PickingMode.Ignore };
            num.AddToClassList("pp-segment__count");
            num.AddToClassList("pp-font-mono-medium");
            seg.Add(num);
        }
        seg.Add(Ring());
        MakeClickable(seg, () =>
        {
            bool hadFocus = seg.focusController?.focusedElement == seg;
            onClick();
            if (hadFocus)
                FocusSegment(label);
        });
        segments.Add(seg);
    }

    // Garde le focus clavier sur le segment choisi apres la reconstruction de la rangee
    private void FocusSegment(string label)
    {
        string caps = Caps(label);
        foreach (var child in segments.Children())
        {
            var l = child.Q<Label>(className: "pp-segment__label");
            if (l != null && l.text == caps)
            {
                child.Focus();
                return;
            }
        }
    }

    private void BuildCount(bool searching, List<PartDefinition> owned, List<PartDefinition> matches, List<PartDefinition> list)
    {
        countRow.Clear();
        if (searching)
        {
            AddCountParts(theme.matchCountFormat, list.Count, owned.Count);
            return;
        }
        var spec = theme.tabs[activeTab];
        int n = activeTab == AllTab ? owned.Count : owned.Count(p => p.category == spec.category);
        AddCountParts(theme.ownedCountFormat, n, n == 1 ? spec.nounSingular : spec.nounPlural);
    }

    // Decoupe un format « {0} of {1} parts match » : nombres en mono, le reste en Barlow
    private void AddCountParts(string format, params object[] args)
    {
        int i = 0;
        while (i < format.Length)
        {
            int open = format.IndexOf('{', i);
            if (open < 0)
            {
                AddCountText(format.Substring(i), false);
                break;
            }
            if (open > i)
                AddCountText(format.Substring(i, open - i), false);
            int end = format.IndexOf('}', open);
            if (end < 0)
            {
                AddCountText(format.Substring(open), false);
                break;
            }
            if (int.TryParse(format.Substring(open + 1, end - open - 1), out int arg) && arg >= 0 && arg < args.Length)
            {
                object value = args[arg];
                bool number = value is int;
                AddCountText(number ? Num((int)value) : value?.ToString() ?? "", number);
            }
            i = end + 1;
        }
    }

    private void AddCountText(string text, bool number)
    {
        if (string.IsNullOrEmpty(text))
            return;
        var label = new Label(KeepEdgeSpaces(text)) { pickingMode = PickingMode.Ignore };
        label.AddToClassList(number ? "pp-count__num" : "pp-count__text");
        label.AddToClassList(number ? "pp-font-mono-medium" : "pp-font-sans");
        countRow.Add(label);
    }

    // =========================================================
    // TRI (liste deroulante)
    // =========================================================

    private void TogglePopup()
    {
        if (popupOpen) ClosePopup();
        else OpenPopup();
    }

    private void OpenPopup()
    {
        var options = SortOptions();
        if (options.Length == 0)
            return;
        popup.Clear();
        int current = Mathf.Clamp(Memory.sort, 0, options.Length - 1);
        VisualElement focusTarget = null;
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            var option = new VisualElement();
            option.AddToClassList("pp-option");
            option.AddToClassList("pp-focusable");
            if (i == current)
                option.AddToClassList("pp-option--active");
            var label = new Label(options[i].label) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("pp-option__label");
            label.AddToClassList("pp-font-sans");
            option.Add(label);
            option.Add(Ring());
            MakeClickable(option, () =>
            {
                Memory.sort = index;
                ClosePopup();
                Refresh();
                sortSelect.Focus();
            });
            popup.Add(option);
            if (i == current)
                focusTarget = option;
        }

        // Sous la liste deroulante, dans le repere de la fenetre
        var s = sortSelect.worldBound;
        var f = frame.worldBound;
        popup.style.left = s.xMin - f.xMin;
        popup.style.top = s.yMax - f.yMin;
        popup.style.width = s.width;
        popup.style.display = DisplayStyle.Flex;
        sortSelect.AddToClassList("pp-select--open");
        popupOpen = true;
        focusTarget?.Focus();
    }

    private void ClosePopup()
    {
        if (!popupOpen || popup == null)
            return;
        popupOpen = false;
        popup.style.display = DisplayStyle.None;
        popup.Clear();
        sortSelect.RemoveFromClassList("pp-select--open");
    }

    // =========================================================
    // RECHERCHE
    // =========================================================

    private void OnSearchChanged(ChangeEvent<string> evt)
    {
        // « / » qui a donne le clavier au champ : il ne s'y ecrit pas
        if (slashGuard)
        {
            slashGuard = false;
            if (evt.newValue == (evt.previousValue ?? "") + "/")
            {
                searchField.SetValueWithoutNotify(evt.previousValue ?? "");
                return;
            }
        }
        searchPlaceholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
        long delay = Mathf.RoundToInt(Mathf.Max(0f, theme.searchDebounceSeconds) * 1000f);
        if (debounce == null)
            debounce = root.schedule.Execute(ApplySearch);
        debounce.ExecuteLater(delay);
    }

    private void ApplySearch()
    {
        string next = (searchField.value ?? "").Trim();
        if (next == query)
            return;
        SaveScroll();
        bool started = query.Length == 0 && next.Length > 0;
        query = next;
        if (started)
        {
            // Nouvelle recherche : rien de memorise d'une recherche precedente
            foreach (var m in memory)
            {
                m.searchSelectedId = null;
                m.searchScroll = 0f;
            }
        }
        Refresh();
    }

    public void ClearSearch()
    {
        debounce?.Pause();
        if (searchField != null)
            searchField.SetValueWithoutNotify("");
        if (searchPlaceholder != null)
            searchPlaceholder.style.display = DisplayStyle.Flex;
        if (query.Length == 0)
            return;
        SaveScroll();
        query = "";
        Refresh();
    }

    public void RunSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || searchField == null)
            return;
        debounce?.Pause();
        searchField.SetValueWithoutNotify(text);
        searchPlaceholder.style.display = DisplayStyle.None;
        SaveScroll();
        query = text.Trim();
        foreach (var m in memory)
            m.searchSelectedId = null;
        Refresh();
    }

    private void FillEmptySearch()
    {
        emptyTitle.text = string.Format(theme.emptyTitleFormat, query);
        emptyBody.text = theme.emptyExplanation;
        suggestion = PartPickerLogic.Suggest(inventory?.Owned ?? Array.Empty<PartDefinition>(), query, theme.suggestionTolerance);
        emptySuggest.style.display = suggestion == null ? DisplayStyle.None : DisplayStyle.Flex;
        if (suggestion == null)
            return;
        // « Did you mean {0}? » : texte avant et apres le lien
        string format = theme.emptySuggestionFormat ?? "{0}";
        int at = format.IndexOf("{0}", StringComparison.Ordinal);
        emptySuggestBefore.text = KeepEdgeSpaces(at < 0 ? format : format.Substring(0, at));
        emptySuggestAfter.text = KeepEdgeSpaces(at < 0 ? "" : format.Substring(at + 3));
        emptySuggestWord.text = $"<u>{suggestion}</u>";
    }

    // =========================================================
    // SELECTION ET DETAIL
    // =========================================================

    private void SetSelection(PartDefinition part, bool notify)
    {
        bool changed = part != selected;
        selected = part;
        foreach (var t in tiles.Values)
            t.root.EnableInClassList("pp-tile--selected", t.part == part);

        if (memory != null && part != null)
        {
            if (query.Length > 0) Memory.searchSelectedId = part.id;
            else Memory.selectedId = part.id;
        }

        if (changed || notify)
            ShowDetail(part);
        if (notify && part != null)
            Selected?.Invoke(part);
    }

    private void ShowDetail(PartDefinition part)
    {
        if (root == null)
            return;
        bool has = part != null;
        detailContent.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
        detailEmpty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
        if (!has)
        {
            stage?.SetActive(false);
            return;
        }

        badge.text = Caps(theme.BadgeFor(part));
        detailName.text = part.name;
        detailDesc.text = part.description ?? "";
        detailDesc.style.display = string.IsNullOrEmpty(part.description) ? DisplayStyle.None : DisplayStyle.Flex;
        BuildStats(part);
        RefreshNotice();
        ShowPreview(part);

        // Mise a jour en moins de 140 ms : la fiche apparait en fondu
        detailFade = BlockforgeTheme.ReduceMotion || theme.detailSeconds <= 0f ? 1f : 0f;
        detailContent.style.opacity = BlockforgeTheme.EaseOut(detailFade);
    }

    private void BuildStats(PartDefinition part)
    {
        statsBox.Clear();
        if (part.stats == null)
            return;
        for (int i = 0; i < part.stats.Count; i++)
        {
            var stat = part.stats[i];
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("pp-stat");
            if (i > 0)
                line.AddToClassList("pp-stat--spaced");

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("pp-stat__row");
            var label = new Label(stat.label) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("pp-stat__label");
            label.AddToClassList("pp-font-sans");
            row.Add(label);
            var value = new Label(Num(stat.value, string.IsNullOrEmpty(stat.format) ? "0.#" : stat.format))
                { pickingMode = PickingMode.Ignore };
            value.AddToClassList("pp-stat__value");
            value.AddToClassList("pp-font-mono-medium");
            row.Add(value);
            if (!string.IsNullOrEmpty(stat.unit))
            {
                var unit = new Label(stat.unit) { pickingMode = PickingMode.Ignore };
                unit.AddToClassList("pp-stat__unit");
                unit.AddToClassList("pp-font-sans");
                row.Add(unit);
            }
            line.Add(row);

            if (stat.HasBar)
            {
                var track = new VisualElement { pickingMode = PickingMode.Ignore };
                track.AddToClassList("pp-stat__track");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("pp-stat__fill");
                fill.style.width = Length.Percent(Mathf.Clamp01(stat.ratio) * 100f);
                track.Add(fill);
                line.Add(track);
            }
            statsBox.Add(line);
        }
    }

    private void RefreshNotice()
    {
        if (selected == null || notice == null)
            return;
        var part = selected;
        var pool = theme.Pool(part.cpuPool);
        bool fits = PartPickerLogic.Fits(part, budget, out int used, out int max);

        notice.EnableInClassList("pp-notice--info", fits);
        notice.EnableInClassList("pp-notice--warning", !fits);
        noticeIcon.EnableInClassList("pp-icon--info", fits);
        noticeIcon.EnableInClassList("pp-icon--warning", !fits);
        if (fits)
        {
            noticeTitle.style.display = DisplayStyle.None;
            noticeBody.text = string.Format(theme.fitsFormat, Num(used), Num(used + part.cpuCost),
                                            Num(max), pool.unit);
        }
        else
        {
            noticeTitle.style.display = DisplayStyle.Flex;
            noticeTitle.text = string.Format(theme.fullTitleFormat, pool.title);
            noticeBody.text = string.Format(theme.fullBodyFormat, Num(part.cpuCost), pool.unit,
                                            Num(Mathf.Max(0, max - used)), Num(max), pool.removeNoun);
        }
        place.SetEnabled(fits);
    }

    private void ShowPreview(PartDefinition part)
    {
        bool model = part.previewPrefab != null;
        previewImage.EnableInClassList("pp-preview__image--sprite", !model);
        if (model)
        {
            // Texture a la taille affichee en pixels ecran ; avant la premiere mise en page,
            // l'apercu se relance sur GeometryChangedEvent
            float w = preview.resolvedStyle.width, h = preview.resolvedStyle.height;
            float panelHeight = root.panel?.visualTree?.layout.height ?? float.NaN;
            if (float.IsNaN(w) || w <= 0f || float.IsNaN(h) || h <= 0f || float.IsNaN(panelHeight) || panelHeight <= 0f)
            {
                previewImage.image = null;
                previewImage.sprite = null;
                return;
            }
            float scale = Screen.height / panelHeight;
            stage.Show(part.previewPrefab, Mathf.RoundToInt(w * scale), Mathf.RoundToInt(h * scale), previewBackdrop);
            stage.SetActive(true);
            previewImage.sprite = null;
            previewImage.image = stage.Texture;
            previewImage.scaleMode = ScaleMode.ScaleAndCrop;
        }
        else
        {
            stage.SetActive(false);
            previewImage.image = null;
            previewImage.sprite = part.thumbnail;
            previewImage.scaleMode = ScaleMode.ScaleToFit;
        }
    }

    // =========================================================
    // POSE
    // =========================================================

    private bool CanPlace(PartDefinition part) => part != null && PartPickerLogic.Fits(part, budget, out _, out _);

    private void RequestPlace(PartDefinition part)
    {
        if (!IsOpen || !CanPlace(part))
            return;
        bool keepOpen = ShiftHeld();
        recent.Remove(part.id);
        recent.Insert(0, part.id);
        if (selected != part)
            SetSelection(part, true);
        PlaceRequested?.Invoke(part, keepOpen);
        if (!keepOpen)
            Close();
        else
            RefreshNotice();
    }

    private static bool ShiftHeld()
    {
        var kb = Keyboard.current;
        return kb != null && kb.shiftKey.isPressed;
    }

    // ---------- Pointeur sur une tuile : clic, double clic, glisser ----------

    private void OnTilePointerDown(Tile t, PointerDownEvent e)
    {
        if (e.button != 0)
            return;
        SetSelection(t.part, true);
        if (e.clickCount >= 2)
        {
            RequestPlace(t.part);
            e.StopPropagation();
            return;
        }
        pressedTile = t;
        pressPointer = e.pointerId;
        pressPosition = e.position;
        dragging = false;
        t.root.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    private void OnTilePointerMove(Tile t, PointerMoveEvent e)
    {
        if (pressedTile != t || e.pointerId != pressPointer)
            return;
        Vector2 p = e.position;
        if (!dragging && (p - pressPosition).magnitude >= dragThreshold)
        {
            dragging = true;
            dragGhostImage.style.backgroundImage = t.part.thumbnail != null
                ? new StyleBackground(t.part.thumbnail)
                : new StyleBackground(StyleKeyword.None);
            dragGhost.style.display = DisplayStyle.Flex;
        }
        if (dragging)
        {
            var local = root.WorldToLocal(p);
            float half = dragGhost.resolvedStyle.width * 0.5f;
            if (float.IsNaN(half)) half = 0f;
            dragGhost.style.left = local.x - half;
            dragGhost.style.top = local.y - half;
        }
        e.StopPropagation();
    }

    private void OnTilePointerUp(Tile t, PointerUpEvent e)
    {
        if (pressedTile != t || e.pointerId != pressPointer)
            return;
        bool dropped = dragging && IsOverViewport(e.position);
        CancelDrag();
        if (dropped)
            RequestPlace(t.part);
        e.StopPropagation();
    }

    // Lache sur le viewport = hors de la fenetre, sur le voile
    private bool IsOverViewport(Vector2 panelPosition)
    {
        var picked = root.panel?.Pick(panelPosition);
        return picked == null || picked == dim;
    }

    private void CancelDrag()
    {
        if (pressedTile != null && pressPointer >= 0 && pressedTile.root.HasPointerCapture(pressPointer))
            pressedTile.root.ReleasePointer(pressPointer);
        pressedTile = null;
        pressPointer = -1;
        dragging = false;
        if (dragGhost != null)
            dragGhost.style.display = DisplayStyle.None;
    }

    private void OnRootPointerDown(PointerDownEvent e)
    {
        SetKeyboardMode(false);
        if (popupOpen && e.target is VisualElement target && !popup.Contains(target) && target != sortSelect && !sortSelect.Contains(target))
            ClosePopup();
    }

    // =========================================================
    // CLAVIER ET MANETTE
    // =========================================================

    private void SetKeyboardMode(bool keyboard)
    {
        root?.EnableInClassList("pp-kbd", keyboard);
    }

    private void FocusGridEntry()
    {
        if (!IsOpen || root == null)
            return;
        Tile target = null;
        if (selected != null)
            tiles.TryGetValue(selected.id, out target);
        if (target == null || !visibleTiles.Contains(target))
            target = visibleTiles.Count > 0 ? visibleTiles[0] : null;
        if (target != null)
            target.root.Focus();
        else
            (tabElements.Count > activeTab ? tabElements[activeTab] : null)?.Focus();
    }

    private bool IsInSearch(VisualElement element) =>
        element != null && searchField != null && (element == searchField || searchField.Contains(element));

    // Fleches, D-pad, stick : deplacement dans la grille, puis navigation spatiale
    private void OnNavigate(NavigationMoveEvent e)
    {
        if (e.direction != NavigationMoveEvent.Direction.Left && e.direction != NavigationMoveEvent.Direction.Right &&
            e.direction != NavigationMoveEvent.Direction.Up && e.direction != NavigationMoveEvent.Direction.Down)
            return; // Tab / Maj+Tab : ordre par defaut

        var focused = root.focusController?.focusedElement as VisualElement;
        if (IsInSearch(focused) && (e.direction == NavigationMoveEvent.Direction.Left || e.direction == NavigationMoveEvent.Direction.Right))
            return; // le curseur de saisie bouge

        Vector2 dir = e.direction switch
        {
            NavigationMoveEvent.Direction.Left => Vector2.left,
            NavigationMoveEvent.Direction.Right => Vector2.right,
            NavigationMoveEvent.Direction.Up => Vector2.down, // repere du panneau : y vers le bas
            _ => Vector2.up,
        };

        // Dans la grille : case voisine (meme colonne en haut / en bas)
        int index = focused == null ? -1 : visibleTiles.FindIndex(t => t.root == focused);
        if (index >= 0)
        {
            int next = index;
            if (dir == Vector2.left && index % gridColumns > 0) next = index - 1;
            else if (dir == Vector2.right && index % gridColumns < gridColumns - 1 && index + 1 < visibleTiles.Count) next = index + 1;
            else if (dir == Vector2.down && index - gridColumns >= 0) next = index - gridColumns;
            else if (dir == Vector2.up && index + gridColumns < visibleTiles.Count) next = index + gridColumns;
            if (next != index)
            {
                visibleTiles[next].root.Focus();
                Consume(e);
                return;
            }
        }

        var best = FindSpatial(focused, dir);
        if (best != null)
        {
            best.Focus();
            Consume(e);
        }
    }

    private void Consume(EventBase e)
    {
        e.StopPropagation();
        root.focusController?.IgnoreEvent(e);
    }

    private VisualElement FindSpatial(VisualElement from, Vector2 dir)
    {
        Rect origin = from != null ? from.worldBound : window.worldBound;
        Vector2 o = origin.center;
        var viewport = grid.contentViewport.worldBound;
        VisualElement best = null;
        float bestScore = float.MaxValue;

        foreach (var candidate in root.Query<VisualElement>().Where(v => v.focusable && v.canGrabFocus).ToList())
        {
            if (candidate == from || (from != null && from.Contains(candidate)) || !IsReachable(candidate))
                continue;
            if (IsInSearch(candidate) && candidate != searchField)
                continue;
            var r = candidate.worldBound;
            if (r.width <= 0f || r.height <= 0f)
                continue;
            if (grid.contentContainer.Contains(candidate) && !viewport.Overlaps(r))
                continue;
            Vector2 d = r.center - o;
            float along = Vector2.Dot(d, dir);
            if (along <= 1f)
                continue;
            float across = Mathf.Abs(dir.x != 0f ? d.y : d.x);
            float score = along + across * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private bool IsReachable(VisualElement v)
    {
        if (!v.enabledInHierarchy || !v.visible)
            return false;
        for (var e = v; e != null && e != root.parent; e = e.parent)
        {
            if (e.resolvedStyle.display == DisplayStyle.None)
                return false;
        }
        return true;
    }

    private KeyControl KeyFor(Keyboard kb, string letter)
    {
        if (kb == null || string.IsNullOrEmpty(letter))
            return null;
        if (kb != boundKeyboard || kb.keyboardLayout != boundLayout)
        {
            keyCache.Clear();
            boundLayout = kb.keyboardLayout;
            BindKeyboard(kb);
        }
        if (!keyCache.TryGetValue(letter, out var key))
        {
            key = kb.FindKeyOnCurrentKeyboardLayout(letter.ToLowerInvariant());
            keyCache[letter] = key;
        }
        return key;
    }

    private void BindKeyboard(Keyboard kb)
    {
        UnbindKeyboard();
        boundKeyboard = kb;
        if (boundKeyboard != null)
            boundKeyboard.onTextInput += OnTextInput;
    }

    private void UnbindKeyboard()
    {
        if (boundKeyboard != null)
            boundKeyboard.onTextInput -= OnTextInput;
        boundKeyboard = null;
        boundLayout = null;
        keyCache.Clear();
    }

    private void OnTextInput(char c)
    {
        if (c == '/' && IsOpen && !searchFocused)
            slashFocusRequested = true;
    }

    private void Update()
    {
        // PanelSettings change (UIDocument reconstruit son arbre) : on reconstruit aussi
        if (root != null && root.panel == null && EnsureBuilt() && IsOpen)
        {
            root.style.display = DisplayStyle.Flex;
            SetFade(1f);
        }

        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if (kb != null && kb != boundKeyboard)
            KeyFor(kb, openKey); // abonne onTextInput au clavier courant

        // Fondu (temps reel : le picker peut s'ouvrir pendant une pause)
        float target = IsOpen ? 1f : 0f;
        if (!Mathf.Approximately(fade, target))
        {
            float duration = BlockforgeTheme.ReduceMotion ? 0f : theme.fadeSeconds;
            SetFade(duration <= 0f ? target : Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / duration));
        }

        if (!IsOpen)
        {
            if (listenForOpenKey)
            {
                var key = KeyFor(kb, openKey);
                if (key != null && key.wasPressedThisFrame && !AnyModifier(kb))
                    Open();
            }
            return;
        }

        // Fiche detail en fondu, apercu qui tourne
        if (detailFade < 1f)
        {
            detailFade = Mathf.MoveTowards(detailFade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, theme.detailSeconds));
            detailContent.style.opacity = BlockforgeTheme.EaseOut(detailFade);
        }
        if (stage != null && stage.HasModel && !BlockforgeTheme.ReduceMotion)
            stage.Spin(theme.previewDegreesPerSecond * Time.unscaledDeltaTime);

        if (slashFocusRequested)
        {
            slashFocusRequested = false;
            slashGuard = true;
            ClosePopup();
            searchField.Focus();
            root.schedule.Execute(() => slashGuard = false).ExecuteLater(50);
        }

        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Back();
                return;
            }
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !popupOpen)
            {
                // Une tuile ou un bouton qui a le focus traite deja Entree (Submit)
                var focused = root.focusController?.focusedElement as VisualElement;
                if (focused == null || IsInSearch(focused))
                    RequestPlace(selected);
            }

            // Lettres des onglets ; pendant la saisie, avec Alt (les lettres s'ecrivent sinon)
            bool alt = kb.altKey.isPressed;
            bool ctrl = kb.ctrlKey.isPressed;
            if (!ctrl && (!searchFocused || alt))
            {
                for (int i = 0; i < theme.tabs.Length; i++)
                {
                    var key = KeyFor(kb, theme.tabs[i].key);
                    if (key != null && key.wasPressedThisFrame)
                    {
                        SelectTab(i);
                        break;
                    }
                }
            }
        }

        if (pad != null)
        {
            if (pad.buttonEast.wasPressedThisFrame)
            {
                SetKeyboardMode(true);
                Back();
                return;
            }
            if (pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame)
            {
                SetKeyboardMode(true);
                int step = pad.rightShoulder.wasPressedThisFrame ? 1 : -1;
                int count = theme.tabs.Length;
                SelectTab(((activeTab + step) % count + count) % count);
                root.schedule.Execute(FocusGridEntry).ExecuteLater(0);
            }
            if (pad.dpad.ReadValue() != Vector2.zero || pad.buttonSouth.wasPressedThisFrame)
                SetKeyboardMode(true);
        }
    }

    // Echap / B : liste deroulante, puis recherche, puis fermeture
    private void Back()
    {
        if (popupOpen)
        {
            ClosePopup();
            sortSelect.Focus();
        }
        else if (query.Length > 0 || !string.IsNullOrEmpty(searchField?.value))
            ClearSearch();
        else
            Close();
    }

    private static bool AnyModifier(Keyboard kb) =>
        kb != null && (kb.ctrlKey.isPressed || kb.altKey.isPressed || kb.shiftKey.isPressed);
}
