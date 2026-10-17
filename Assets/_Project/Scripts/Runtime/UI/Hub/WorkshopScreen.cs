using System.Collections.Generic;
using Blockforge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using H = BlockforgeTheme.Hub;
using W = BlockforgeTheme.Workshop;

// =========================================================
// WORKSHOP — robots de la communaute (artboard Workshop)
// Ligne de titre : champ de recherche 360 px et tri. Onglets
// segment Featured · New · Top rated · Friends · Mine (n) ; a droite
// le filtre CPU en puces. Grille 4 x 2 de cartes de robots (apercu
// 150 px avec la categorie, nom, auteur, CPU / telechargements /
// prix), pagination « Showing 8 of 1 240 robots » et ‹ 1 / 155 ›.
// Panneau de detail : apercu 3D 260 px avec le grade, description,
// quatre StatLines, puces de categories, Notice des pieces
// possedees, Download for N (desactive avec raison si credits
// insuffisants), Test drive, noter, partager.
// =========================================================

public sealed class WorkshopScreen : HubScreen
{
    public override HubSection Section => HubSection.Workshop;
    protected override string ScreenTitle => "Workshop";
    protected override string ScreenSubtitle => "Robots built by other players. Download one into a bay and make it yours.";

    private IWorkshopService service;
    private IWallet wallet;

    private DSWidgets.SearchFieldWidget search;
    private DSWidgets.SelectWidget sort;
    private DSWidgets.ChoiceGroupWidget shelves, cpuChips;
    private RectTransform grid;
    private DSWidgets.PagerWidget pager;
    private readonly List<Card> cards = new();
    private IReadOnlyList<RobotListing> page = new List<RobotListing>();
    private RobotListing selected;
    private int pageIndex;
    private string query = "";

    // Detail
    private RectTransform preview, previewBadgeHost, statsHost, badgesHost, actionsHost;
    private TextMeshProUGUI description;
    private DSWidgets.NoticeWidget ownedNotice;
    private ButtonWidget download, testDrive, rate, share;
    private DSWidgets.StatLineWidget cpuStat, massStat, speedStat, healthStat;

    private sealed class Card
    {
        public RectTransform Rect;
        public Button Button;
        public DSElement Element;
        public TextMeshProUGUI Name, By, Cpu, Downloads, Price;
        public RectTransform BadgeHost;
        public RobotListing Robot;
    }

    protected override void Build()
    {
        service = MetaServices.Workshop;
        wallet = MetaServices.Wallet;

        // ---------- Ligne de titre ----------
        search = kit.SearchField(TitleControls, "Search", "Search robots or builders", H.SearchWidth);
        search.Changed += q => { query = q ?? ""; pageIndex = 0; Refresh(); };
        sort = kit.Select(TitleControls, "Sort", new List<ChoiceOption>
        {
            new("downloads", "Most downloaded"), new("rating", "Best rated"), new("new", "Newest"), new("price", "Cheapest"),
        }, "downloads", H.SortWidth, "Sort");
        sort.Changed += _ => { pageIndex = 0; Refresh(); };

        // ---------- Rayons et filtre CPU ----------
        var filters = kit.Row(Content, "Filters", H.TitleRowGap, TextAnchor.MiddleLeft);
        UIKit.Fixed(filters, -1f, BlockforgeTheme.ControlMd);
        shelves = kit.SegmentTabs(filters, "Shelves", new List<ChoiceOption>
        {
            new("featured", "Featured"), new("new", "New"), new("top", "Top rated"), new("friends", "Friends"), new("mine", "Mine", null, 0),
        }, "featured");
        shelves.Changed += _ => { pageIndex = 0; Refresh(); };
        kit.Spacer(filters);
        kit.Text(filters, "CpuLabel", DSTextStyle.Label, kit.P.InkMuted, "CPU");
        cpuChips = kit.ChipGroup(filters, "Cpu", new List<ChoiceOption>
        {
            new("any", "Any"), new("u1000", "Under " + BlockforgeTheme.Num(1000)),
            new("1000", BlockforgeTheme.Num(1000) + " – " + BlockforgeTheme.Num(1500)),
            new("1500", BlockforgeTheme.Num(1500) + " – " + BlockforgeTheme.Num(2000)), new("fits", "Fits my bay"),
        }, "any");
        cpuChips.Changed += _ => { pageIndex = 0; Refresh(); };

        // ---------- Grille 4 x 2 ----------
        var gridHost = kit.Rect(Content, "GridHost");
        UIKit.Flexible(gridHost, 1f, 1f);
        grid = kit.Rect(gridHost, "Grid");
        UIKit.Stretch(grid);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = W.Columns;
        layout.spacing = new Vector2(W.CardGap, W.CardGap);
        layout.childAlignment = TextAnchor.UpperLeft;
        var fitter = grid.gameObject.AddComponent<GridCellSizer>();
        fitter.columns = W.Columns;
        fitter.rows = W.Rows;
        fitter.gap = W.CardGap;
        for (int i = 0; i < W.PageSize; i++)
            cards.Add(BuildCard(grid, i));

        // ---------- Pagination ----------
        pager = kit.Pager(Content, "Pager");
        pager.Changed += p => { pageIndex = p; Refresh(); };

        // ---------- Detail ----------
        BuildDetail();
    }

    private Card BuildCard(Transform parent, int index)
    {
        var c = new Card();
        c.Rect = kit.Rect(parent, "Robot_" + index);
        c.Rect.gameObject.AddComponent<Image>().raycastTarget = true;
        c.Button = UIKit.Clickable(c.Rect);
        c.Rect.gameObject.AddComponent<RectMask2D>();
        var column = kit.Column(c.Rect, "Layout", 0f);
        UIKit.Stretch(column);

        var preview = kit.Polygon(column, "Preview");
        preview.vertical = true;
        preview.SetColors(kit.P.SurfaceHover, kit.P.SurfaceInset);
        UIKit.Fixed(preview, -1f, W.PreviewHeight);
        var cube = kit.Icon(preview.transform, "Cube", "cube", BlockforgeTheme.IconSize.Tile, kit.P.InkMuted);
        UIKit.Place(cube.rectTransform, UIKit.Center, Vector2.zero, new Vector2(BlockforgeTheme.IconSize.Tile, BlockforgeTheme.IconSize.Tile));
        c.BadgeHost = kit.Rect(preview.transform, "BadgeHost");
        UIKit.Place(c.BadgeHost, UIKit.TopLeft, new Vector2(W.TagInset, -W.TagInset), Vector2.zero);

        var body = kit.Column(column, "Body", W.BodyGap, TextAnchor.UpperLeft, UIKit.Pad(W.BodyPadX, W.BodyPadX, W.BodyPadTop, W.BodyPadBottom));
        c.Name = kit.Text(body, "Name", DSTextStyle.TitleMD, kit.P.InkPrimary, "");
        UIKit.Fixed(c.Name, -1f, BlockforgeTheme.Type.TitleMd.LineHeight);
        var by = kit.Row(body, "By", BlockforgeTheme.Gap.Small, TextAnchor.MiddleLeft);
        UIKit.Fixed(by, -1f, BlockforgeTheme.Type.Caption.LineHeight);
        kit.Icon(by, "Icon", "user", BlockforgeTheme.IconSize.Xs, kit.P.InkMuted);
        c.By = kit.Text(by, "Author", DSTextStyle.Caption, kit.P.InkMuted, "");
        var row = kit.Row(body, "Row", BlockforgeTheme.Space2, TextAnchor.MiddleLeft, UIKit.Pad(0f, 0f, BlockforgeTheme.Space1, 0f));
        UIKit.Fixed(row, -1f, BlockforgeTheme.Type.Caption.LineHeight + BlockforgeTheme.Space1);
        c.Cpu = Stat(row, "cpu", kit.P.InkSecondary, W.StatSize, 500);
        kit.Spacer(row);
        c.Downloads = Stat(row, "download", kit.P.InkSecondary, W.StatSize, 500);
        kit.Spacer(row);
        c.Price = Stat(row, "credits", kit.P.GradeMasterwork, W.PriceSize, 600);

        c.Element = kit.Element(c.Rect, DSRole.RowCard, new TMP_Text[] { c.Name });
        kit.Group(c.Rect);
        int i = index;
        c.Button.onClick.AddListener(() => Select(cards[i].Robot, true));
        return c;
    }

    private TextMeshProUGUI Stat(Transform parent, string icon, Color color, float size, int weight)
    {
        var row = kit.Row(parent, "Stat_" + icon, BlockforgeTheme.Gap.IconText, TextAnchor.MiddleLeft);
        kit.Icon(row, "Icon", icon, BlockforgeTheme.IconSize.Sm, color);
        return kit.Mono(row, "Value", size, weight, color, "");
    }

    private void BuildDetail()
    {
        var content = Detail.Content;
        preview = Preview(content, "Preview", W.DetailPreview, "[3D PREVIEW — ROTATES]");
        previewBadgeHost = kit.Rect(preview, "GradeHost");
        UIKit.Place(previewBadgeHost, UIKit.BottomRight, new Vector2(-W.GradeInset, W.GradeInset), Vector2.zero);
        description = Paragraph(content, "Description", "");
        statsHost = kit.Column(content, "Stats", H.StatGap);
        cpuStat = kit.StatLine(statsHost, "Cpu", "CPU", "0");
        massStat = kit.StatLine(statsHost, "Mass", "Mass", "0", "kg");
        speedStat = kit.StatLine(statsHost, "Speed", "Top speed", "0", "mph");
        healthStat = kit.StatLine(statsHost, "Health", "Health", "0");
        badgesHost = kit.Row(content, "Badges", BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        UIKit.Fixed(badgesHost, -1f, BlockforgeTheme.ControlSm);
        kit.Spacer(content);
        ownedNotice = kit.Notice(content, "Owned", DSTone.Info, "");
        actionsHost = kit.Column(content, "Actions", W.ActionsGap);
        download = kit.Button(actionsHost, "Download", ButtonVariant.Primary, "Download", Download, "download", BlockforgeTheme.ControlLg, true);
        var second = kit.Row(actionsHost, "Secondary", W.ActionsGap, TextAnchor.MiddleLeft);
        testDrive = kit.Button(second, "TestDrive", ButtonVariant.Secondary, "Test drive", () => Toast("Test drive is coming with the arena.", true), "test", BlockforgeTheme.ControlMd, true);
        rate = kit.IconButton(second, "Rate", "trophy", "Rate this robot", () => Toast("Thanks for rating.", true));
        share = kit.IconButton(second, "Share", "external", "Share", () => Toast("Link copied.", true));
    }

    // ---------- Donnees ----------

    protected override void OnOpened(bool first)
    {
        wallet.CreditsChanged -= OnCredits;
        wallet.CreditsChanged += OnCredits;
        Refresh();
    }

    private void OnDisable()
    {
        if (wallet != null)
            wallet.CreditsChanged -= OnCredits;
    }

    private void OnCredits(int _) => UpdateDownload();

    private WorkshopShelf Shelf => shelves.Selected switch
    {
        "new" => WorkshopShelf.New, "top" => WorkshopShelf.TopRated, "friends" => WorkshopShelf.Friends, "mine" => WorkshopShelf.Mine, _ => WorkshopShelf.Featured,
    };

    private CpuFilter Cpu => cpuChips.Selected switch
    {
        "u1000" => CpuFilter.Under1000, "1000" => CpuFilter.From1000To1500, "1500" => CpuFilter.From1500To2000, "fits" => CpuFilter.FitsMyBay, _ => CpuFilter.Any,
    };

    private WorkshopSort Sort => sort.Selected switch
    {
        "rating" => WorkshopSort.BestRated, "new" => WorkshopSort.Newest, "price" => WorkshopSort.Cheapest, _ => WorkshopSort.MostDownloaded,
    };

    private async void Refresh()
    {
        try
        {
            var list = await service.Browse(Shelf, Cpu, Sort, pageIndex);
            if (this == null) return;
            if (!string.IsNullOrEmpty(query))
            {
                var filtered = new List<RobotListing>();
                foreach (var r in list)
                    if (r.Name.ToLowerInvariant().Contains(query.ToLowerInvariant()) || r.Author.ToLowerInvariant().Contains(query.ToLowerInvariant()))
                        filtered.Add(r);
                list = filtered;
            }
            page = list;
            int total = service.TotalCount(Shelf, Cpu);
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)service.PageSize));
            pager.Set(pageIndex, pages, $"Showing {BlockforgeTheme.Num(page.Count)} of {BlockforgeTheme.Num(total)} robots");
            if (service is MockWorkshop mock)
                shelves.SetCount("mine", mock.MineCount);
            for (int i = 0; i < cards.Count; i++)
                BindCard(cards[i], i < page.Count ? page[i] : null);
            // Le premier element est selectionne a l'ouverture (ou la selection courante si elle est encore la)
            RobotListing keep = null;
            foreach (var r in page)
                if (selected != null && r.Id == selected.Id) keep = r;
            Select(keep ?? (page.Count > 0 ? page[0] : null), false);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void BindCard(Card c, RobotListing r)
    {
        c.Robot = r;
        c.Rect.gameObject.SetActive(r != null);
        if (r == null)
            return;
        c.Name.text = r.Name;
        c.By.text = r.Author;
        c.Cpu.text = BlockforgeTheme.Num(r.Cpu);
        c.Downloads.text = BlockforgeTheme.Num(r.Downloads);
        c.Price.text = BlockforgeTheme.Num(r.PriceCredits);
        Clear(c.BadgeHost);
        var badge = kit.Badge(c.BadgeHost, "Category", BadgeScale.Category, r.MainCategory.ToString().ToLowerInvariant(), CategoryTag(r.MainCategory));
        badge.anchorMin = badge.anchorMax = badge.pivot = UIKit.TopLeft;
        badge.anchoredPosition = Vector2.zero;
        c.Element.Selected = selected != null && selected.Id == r.Id;
    }

    private static string CategoryTag(Category c) => c switch
    {
        Category.Structure => "Tank", Category.Mobility => "Hover", Category.Weapon => "Gunship", Category.Electronics => "Support", _ => "Paint",
    };

    private void Select(RobotListing r, bool focus)
    {
        selected = r;
        foreach (var c in cards)
            c.Element.Selected = r != null && c.Robot != null && c.Robot.Id == r.Id;
        if (focus)
        {
            foreach (var c in cards)
                if (c.Robot != null && r != null && c.Robot.Id == r.Id) UIKit.Focus(c.Button);
        }
        ShowDetail(r);
    }

    private void ShowDetail(RobotListing r)
    {
        bool has = r != null;
        Detail.SetTitle(has ? r.Name : "Select a robot", has ? $"by {r.Author} · {r.UpdatedLabel}" : "Pick one in the grid to see its stats.");
        description.text = has ? r.Description : "";
        Clear(previewBadgeHost);
        Clear(badgesHost);
        statsHost.gameObject.SetActive(has);
        actionsHost.gameObject.SetActive(has);
        ownedNotice.Rect.gameObject.SetActive(has);
        if (!has)
            return;
        var grade = kit.Badge(previewBadgeHost, "Grade", BadgeScale.Grade, r.Grade.ToString().ToLowerInvariant(), MetaLabels.Of(r.Grade), true);
        grade.anchorMin = grade.anchorMax = grade.pivot = UIKit.BottomRight;
        grade.anchoredPosition = Vector2.zero;
        cpuStat.Set(BlockforgeTheme.Num(r.Cpu), null, Mathf.Clamp01(r.Cpu / MetaScales.RobotCpu));
        massStat.Set(BlockforgeTheme.Num(r.Mass), "kg", Mathf.Clamp01(r.Mass / MetaScales.RobotMassKg));
        speedStat.Set(BlockforgeTheme.Num(r.TopSpeedMph), "mph", Mathf.Clamp01(r.TopSpeedMph / MetaScales.RobotSpeedMph));
        healthStat.Set(BlockforgeTheme.Num(r.Health), null, Mathf.Clamp01(r.Health / MetaScales.RobotHealth));
        foreach (var pair in r.PartCounts)
        {
            if (pair.Value <= 0) continue;
            string label = pair.Key switch
            {
                Category.Structure => BlockforgeTheme.Num(pair.Value) + " blocks",
                Category.Mobility => BlockforgeTheme.Num(pair.Value) + (r.MainCategory == Category.Mobility ? " hovers" : " movers"),
                Category.Weapon => BlockforgeTheme.Num(pair.Value) + " weapons",
                Category.Electronics => BlockforgeTheme.Num(pair.Value) + " modules",
                _ => BlockforgeTheme.Num(pair.Value) + " cosmetics",
            };
            kit.Badge(badgesHost, "Badge_" + pair.Key, BadgeScale.Category, pair.Key.ToString().ToLowerInvariant(), label);
        }
        int missing = r.TotalPartCount - r.OwnedPartCount;
        ownedNotice.Set(DSTone.Info, null, missing > 0
            ? $"You own {BlockforgeTheme.Num(r.OwnedPartCount)} of {BlockforgeTheme.Num(r.TotalPartCount)} parts. The missing ones are bought with the robot."
            : "You already own every part of this robot.");
        UpdateDownload();
    }

    private void UpdateDownload()
    {
        if (selected == null || download == null)
            return;
        bool owned = service is MockWorkshop mock && mock.Downloaded.Contains(selected.Id);
        download.Text = owned ? "Downloaded" : "Download for " + BlockforgeTheme.Num(selected.PriceCredits);
        if (owned)
            download.Disable("Already in your bays.");
        else if (wallet.Credits < selected.PriceCredits)
            download.Disable($"You need {BlockforgeTheme.Num(selected.PriceCredits - wallet.Credits)} more credits.");
        else
            download.Enable();
    }

    private async void Download()
    {
        if (selected == null)
            return;
        try
        {
            await service.Download(selected.Id);
            Toast($"{selected.Name} is in your bays.", true);
            UpdateDownload();
            if (service is MockWorkshop mock)
                shelves.SetCount("mine", mock.MineCount);
        }
        catch (InsufficientFundsException)
        {
            Toast("Not enough credits.", false);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Toast("Couldn't download this robot.", false);
        }
    }

    protected override bool OnCancel()
    {
        if (sort.Popup != null) { sort.Close(); return true; }
        if (selected == null) return false;
        Select(null, false);
        return true;
    }
}

// Cellules d'une grille a colonnes et lignes fixes : la taille suit la zone disponible
public sealed class GridCellSizer : MonoBehaviour
{
    public int columns = 4, rows = 2;
    public float gap = BlockforgeTheme.Space4;
    [Tooltip("> 0 : hauteur de cellule fixe (la largeur seule suit la zone)")]
    public float fixedHeight;
    private GridLayoutGroup grid;
    private Vector2 last;

    private void OnEnable() => Apply();
    private void OnRectTransformDimensionsChange() => Apply();

    private void Apply()
    {
        if (grid == null)
            grid = GetComponent<GridLayoutGroup>();
        var rt = (RectTransform)transform;
        if (grid == null || rt.rect.width <= 0f || rt.rect.height <= 0f)
            return;
        var size = new Vector2(
            Mathf.Floor((rt.rect.width - gap * (columns - 1)) / columns),
            fixedHeight > 0f ? fixedHeight : Mathf.Floor((rt.rect.height - gap * (rows - 1)) / rows));
        if (size == last) return;
        last = size;
        grid.cellSize = size;
    }
}
