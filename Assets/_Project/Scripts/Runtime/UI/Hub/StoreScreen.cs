using System.Collections.Generic;
using Blockforge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using H = BlockforgeTheme.Hub;
using S = BlockforgeTheme.Store;

// =========================================================
// STORE — pieces et packs (artboard Store)
// Ligne de titre : recherche 320 px, « Get credits ». Onglets
// segment Featured · Blocks · Movement · Weapons · Electronics ·
// Cosmetics · Bundles avec les pictogrammes de categorie. Banniere
// du pack mis en avant (coin bas-droit coupe 24 px) : visuel, ends
// in, −20 %, nom, description, contenu, ancien prix barre, prix,
// Buy bundle. Rayon : titre, Hide owned, tri, grille de 6 PartCards
// avec prix et Owned N / Research first (piece verrouillee =
// opacity-disabled + cadenas, non achetable).
// Detail : nom + « Weapon · Hardened », apercu avec barre de grade,
// description, cinq StatLines, rangee des grades, Notice Owned: N
// avec l'impact CPU sur le robot courant (danger si depassement),
// stepper de quantite, Buy for N. L'achat debite le portefeuille
// et met a jour Owned sans quitter l'ecran ; une Notice confirme.
// =========================================================

public sealed class StoreScreen : HubScreen
{
    public override HubSection Section => HubSection.Store;
    protected override string ScreenTitle => "Store";
    protected override string ScreenSubtitle => "Parts you have researched are here to buy. Everything else is in the Tech Tree first.";

    private IStoreService service;
    private IWallet wallet;

    private DSWidgets.SearchFieldWidget search;
    private DSWidgets.ChoiceGroupWidget shelves;
    private DSWidgets.ToggleWidget hideOwned;
    private DSWidgets.SelectWidget sort;
    private TextMeshProUGUI shelfTitle;
    private RectTransform banner, gridHost, grid;
    private readonly List<Item> items = new();
    private IReadOnlyList<StoreItem> shelfItems = new List<StoreItem>();
    private StoreItem selected;
    private string query = "";

    // Banniere
    private TextMeshProUGUI bundleEnds, bundleName, bundleText, bundleWas, bundlePrice;
    private RectTransform bundleBadges;
    private ButtonWidget buyBundle;
    private Bundle bundle;

    // Detail
    private RectTransform preview, statsHost, gradesHost, buyRow;
    private Image previewGrade;
    private TextMeshProUGUI description;
    private DSWidgets.NoticeWidget ownedNotice, successNotice;
    private DSWidgets.StepperWidget qty;
    private ButtonWidget buy;
    private readonly List<DSWidgets.StatLineWidget> stats = new();

    private sealed class Item
    {
        public RectTransform Rect;
        public DSWidgets.PartCardWidget Card;
        public TextMeshProUGUI Price, Owned;
        public StoreItem Data;
    }

    protected override void Build()
    {
        service = MetaServices.Store;
        wallet = MetaServices.Wallet;

        search = kit.SearchField(TitleControls, "Search", "Search parts", H.StoreSearchWidth);
        search.Changed += q => { query = q ?? ""; RefreshShelf(); };
        kit.Button(TitleControls, "GetCredits", ButtonVariant.Secondary, "Get credits", () => Toast("Credits come from matches for now.", true), "credits");

        shelves = kit.SegmentTabs(Content, "Shelves", new List<ChoiceOption>
        {
            new("featured", "Featured"), new("structure", "Blocks", "structure"), new("mobility", "Movement", "mobility"),
            new("weapon", "Weapons", "weapon"), new("electronics", "Electronics", "electronics"), new("cosmetic", "Cosmetics", "cosmetic"), new("bundles", "Bundles"),
        }, "weapon");
        shelves.Changed += _ => RefreshShelf();

        BuildBanner();

        var shelfHead = kit.Row(Content, "ShelfHead", BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        UIKit.Fixed(shelfHead, -1f, BlockforgeTheme.ControlMd);
        shelfTitle = kit.Condensed(shelfHead, "Title", S.ShelfTitleSize, 700, BlockforgeTheme.Type.DisplayLg.LetterSpacingEm, kit.P.InkPrimary, "Weapons");
        UIKit.Flexible(shelfTitle);
        hideOwned = kit.Toggle(shelfHead, "HideOwned", "Hide owned", false);
        hideOwned.Changed += _ => RefreshShelf();
        sort = kit.Select(shelfHead, "Sort", new List<ChoiceOption> { new("grade", "By grade"), new("price", "Price, low to high"), new("new", "Newest") }, "grade", H.StoreSortWidth);
        sort.Changed += _ => RefreshShelf();

        gridHost = kit.Rect(Content, "GridHost");
        UIKit.Flexible(gridHost, 1f, 1f);
        grid = kit.Rect(gridHost, "Grid");
        UIKit.Stretch(grid);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = S.Columns;
        layout.spacing = new Vector2(S.CardGap, S.CardGap);
        layout.cellSize = new Vector2(BlockforgeTheme.TileMd, BlockforgeTheme.TileMd + S.ItemGap + BlockforgeTheme.Type.Caption.LineHeight);
        layout.childAlignment = TextAnchor.UpperLeft;
        var sizer = grid.gameObject.AddComponent<GridCellSizer>();
        sizer.columns = S.Columns;
        sizer.rows = 1;
        sizer.gap = S.CardGap;
        sizer.fixedHeight = BlockforgeTheme.TileMd + S.ItemGap + BlockforgeTheme.Type.Caption.LineHeight;

        BuildDetail();
    }

    private void BuildBanner()
    {
        var plate = ChamferedPlate.Build(kit, Content, "Banner", PlateCorners.BottomRight, S.BannerCut, kit.P.SurfacePanel);
        banner = plate.Rect;
        UIKit.Fixed(banner, -1f, S.BannerArtHeight + 2f * S.BannerPadY);
        // Lueur accent vers la droite, bord 1 px
        var glow = kit.Polygon(plate.Content, "Glow");
        UIKit.Stretch(glow.rectTransform);
        glow.SetColors(BlockforgeTheme.WithAlpha(kit.P.Accent, 0f), BlockforgeTheme.WithAlpha(kit.P.Accent, S.BannerGlowAlpha));
        glow.cutBottomRight = S.BannerCut;
        var border = kit.Polygon(plate.Content, "Border");
        UIKit.TopStrip(border.rectTransform, 0f, BlockforgeTheme.ControlBorder);
        border.SetSolid(kit.P.BorderSubtle);

        var row = kit.Row(plate.Content, "Row", S.BannerGap, TextAnchor.MiddleLeft, UIKit.Pad(S.BannerPadX, S.BannerPadX, S.BannerPadY, S.BannerPadY));
        UIKit.Stretch(row);
        var art = Preview(row, "Art", S.BannerArtHeight, "[BUNDLE ART]");
        UIKit.Fixed(art, S.BannerArtWidth, S.BannerArtHeight);

        var text = kit.Column(row, "Text", S.BannerInnerGap, TextAnchor.MiddleLeft, null, true, true, false);
        UIKit.Flexible(text);
        var overRow = kit.Row(text, "Overline", BlockforgeTheme.Space3, TextAnchor.MiddleLeft);
        UIKit.Fixed(overRow, -1f, BlockforgeTheme.ControlSm);
        bundleEnds = kit.Caps(overRow, "Ends", S.BannerOverlineSize, 600, S.BannerOverlineTracking, kit.P.Accent, "Featured");
        kit.Badge(overRow, "Discount", BadgeScale.Status, "warning", "−20%");
        bundleName = kit.Condensed(text, "Name", S.BannerNameSize, 700, BlockforgeTheme.Type.DisplayXl.LetterSpacingEm, kit.P.InkPrimary, "");
        UIKit.Fixed(bundleName, -1f, S.BannerNameSize);
        bundleText = kit.Text(text, "Description", BlockforgeTheme.Type.BodySm.With(S.BannerTextSize), kit.P.InkSecondary, "", TextAlignmentOptions.TopLeft, false, true);
        UIKit.Fixed(bundleText, S.BannerTextWidth, -1f);
        bundleBadges = kit.Row(text, "Contents", BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        UIKit.Fixed(bundleBadges, -1f, BlockforgeTheme.ControlSm);

        var price = kit.Column(row, "Price", BlockforgeTheme.Space3, TextAnchor.MiddleRight, UIKit.Pad(0f, BlockforgeTheme.Space2, 0f, 0f), true, true, false);
        var priceRow = kit.Row(price, "PriceRow", BlockforgeTheme.Gap.Medium, TextAnchor.LowerRight);
        bundleWas = kit.Mono(priceRow, "Was", S.WasSize, 500, kit.P.InkMuted, "", TextAlignmentOptions.BottomRight);
        bundleWas.fontStyle = FontStyles.Strikethrough;
        kit.Icon(priceRow, "Icon", "credits", S.BannerPriceIcon, kit.P.GradeMasterwork);
        bundlePrice = kit.Mono(priceRow, "Price", S.BannerPriceSize, 600, kit.P.GradeMasterwork, "", TextAlignmentOptions.BottomRight);
        buyBundle = kit.Button(price, "BuyBundle", ButtonVariant.Primary, "Buy bundle", BuyBundle, "store", BlockforgeTheme.ControlLg);
    }

    private void BuildDetail()
    {
        var content = Detail.Content;
        preview = Preview(content, "Preview", S.DetailPreview, "[3D PREVIEW — ROTATES]", kit.P.GradeRaw);
        previewGrade = preview.Find("GradeBar").GetComponent<Image>();
        description = Paragraph(content, "Description", "");
        statsHost = kit.Column(content, "Stats", H.StatGap);
        var gradesBlock = kit.Column(content, "Grades", BlockforgeTheme.Space2);
        kit.Text(gradesBlock, "Label", DSTextStyle.Label, kit.P.InkMuted, "Grades");
        gradesHost = kit.Row(gradesBlock, "Row", S.GradesGap, TextAnchor.MiddleLeft);
        UIKit.Fixed(gradesHost, -1f, BlockforgeTheme.ControlSm);
        kit.Spacer(content);
        successNotice = kit.Notice(content, "Success", DSTone.Success, "", "Bought");
        successNotice.Rect.gameObject.SetActive(false);
        ownedNotice = kit.Notice(content, "Owned", DSTone.Info, "", "Owned: 0");
        buyRow = kit.Row(content, "BuyRow", S.BuyRowGap, TextAnchor.MiddleLeft);
        UIKit.Fixed(buyRow, -1f, BlockforgeTheme.ControlLg);
        qty = kit.Stepper(buyRow, "Qty", 1, 1, 99);
        qty.Changed += _ => UpdateBuy();
        buy = kit.Button(buyRow, "Buy", ButtonVariant.Primary, "Buy", Buy, "credits", BlockforgeTheme.ControlLg, true);
    }

    // ---------- Donnees ----------

    protected override void OnOpened(bool first)
    {
        wallet.CreditsChanged -= OnCredits;
        wallet.CreditsChanged += OnCredits;
        service.Changed -= OnStoreChanged;
        service.Changed += OnStoreChanged;
        RefreshBundle();
        RefreshShelf();
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.CreditsChanged -= OnCredits;
        if (service != null) service.Changed -= OnStoreChanged;
    }

    private void OnCredits(int _) { UpdateBuy(); UpdateBundleButton(); }
    private void OnStoreChanged() { if (IsOpen) RefreshShelf(); }

    private StoreShelf Shelf => shelves.Selected switch
    {
        "structure" => StoreShelf.Blocks, "mobility" => StoreShelf.Movement, "weapon" => StoreShelf.Weapons, "electronics" => StoreShelf.Electronics,
        "cosmetic" => StoreShelf.Cosmetics, "bundles" => StoreShelf.Bundles, _ => StoreShelf.Featured,
    };

    private async void RefreshBundle()
    {
        try
        {
            bundle = await service.Featured();
            if (this == null || bundle == null) return;
            int hours = Mathf.FloorToInt((float)bundle.EndsIn.TotalHours);
            bundleEnds.text = $"Featured · ends in {hours / 24} d {hours % 24:00} h";
            bundleName.text = bundle.Name;
            bundleText.text = bundle.Description;
            bundleWas.text = BlockforgeTheme.Num(bundle.WasPriceCredits);
            bundlePrice.text = BlockforgeTheme.Num(bundle.PriceCredits);
            Clear(bundleBadges);
            foreach (var (category, count) in bundle.Contents)
            {
                if (count <= 0) continue;
                string label = category switch
                {
                    Category.Weapon => BlockforgeTheme.Num(count) + (count > 1 ? " weapons" : " weapon"),
                    Category.Electronics => BlockforgeTheme.Num(count) + (count > 1 ? " modules" : " module"),
                    Category.Structure => BlockforgeTheme.Num(count) + (count > 1 ? " blocks" : " block"),
                    Category.Cosmetic => BlockforgeTheme.Num(count) + (count > 1 ? " paint sets" : " paint set"),
                    _ => BlockforgeTheme.Num(count) + " parts",
                };
                kit.Badge(bundleBadges, "Badge_" + category, BadgeScale.Category, category.ToString().ToLowerInvariant(), label);
            }
            UpdateBundleButton();
        }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    private void UpdateBundleButton()
    {
        if (bundle == null || buyBundle == null) return;
        if (wallet.Credits < bundle.PriceCredits)
            buyBundle.Disable($"You need {BlockforgeTheme.Num(bundle.PriceCredits - wallet.Credits)} more credits.");
        else
            buyBundle.Enable();
    }

    private async void RefreshShelf()
    {
        try
        {
            var shelf = Shelf;
            shelfTitle.text = shelf switch
            {
                StoreShelf.Featured => "Featured", StoreShelf.Blocks => "Blocks", StoreShelf.Movement => "Movement", StoreShelf.Weapons => "Weapons",
                StoreShelf.Electronics => "Electronics", StoreShelf.Cosmetics => "Cosmetics", _ => "Bundles",
            };
            banner.gameObject.SetActive(shelf is StoreShelf.Featured or StoreShelf.Bundles or StoreShelf.Weapons);
            var list = new List<StoreItem>(await service.Shelf(shelf));
            if (this == null) return;
            if (hideOwned.Checked) list.RemoveAll(i => i.Owned > 0);
            if (!string.IsNullOrEmpty(query)) list.RemoveAll(i => !i.Name.ToLowerInvariant().Contains(query.ToLowerInvariant()));
            list.Sort((a, b) => sort.Selected switch
            {
                "price" => a.PriceCredits.CompareTo(b.PriceCredits),
                "new" => string.CompareOrdinal(b.Id, a.Id),
                _ => a.Grade.CompareTo(b.Grade),
            });
            shelfItems = list;
            while (items.Count < list.Count) items.Add(BuildItem(items.Count));
            for (int i = 0; i < items.Count; i++) BindItem(items[i], i < list.Count ? list[i] : null);
            StoreItem keep = null;
            foreach (var it in list) if (selected != null && it.Id == selected.Id) keep = it;
            if (keep == null) foreach (var it in list) if (!it.RequiresResearch) { keep = it; break; }
            Select(keep, false);
        }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    private Item BuildItem(int index)
    {
        var it = new Item();
        it.Rect = kit.Column(grid, "Item_" + index, S.ItemGap, TextAnchor.UpperLeft, null, true, true, false);
        int i = index;
        it.Card = kit.PartCard(it.Rect, "Card", "", Category.Weapon, Grade.Raw, "", () => Select(items[i].Data, true));
        var price = kit.Row(it.Rect, "Price", BlockforgeTheme.Space2, TextAnchor.MiddleLeft, UIKit.Pad(S.PricePadX, 0f));
        UIKit.Fixed(price, -1f, BlockforgeTheme.Type.Caption.LineHeight);
        var cr = kit.Row(price, "Credits", BlockforgeTheme.Gap.IconText, TextAnchor.MiddleLeft);
        kit.Icon(cr, "Icon", "credits", BlockforgeTheme.IconSize.Sm, kit.P.GradeMasterwork);
        it.Price = kit.Mono(cr, "Value", S.PriceSize, 600, kit.P.GradeMasterwork, "");
        kit.Spacer(price);
        it.Owned = kit.Caps(price, "Owned", S.OwnedSize, 500, S.OwnedTracking, kit.P.InkMuted, "", TextAlignmentOptions.MidlineRight);
        return it;
    }

    private void BindItem(Item it, StoreItem data)
    {
        it.Data = data;
        it.Rect.gameObject.SetActive(data != null);
        if (data == null) return;
        it.Card.Name.text = data.Name;
        it.Card.Stat.text = BlockforgeTheme.Qty(data.Cpu, "CPU");
        it.Card.GradeBar.color = kit.GradeColor(data.Grade);
        it.Card.CategoryIcon.sprite = DSAssets.Icon(MetaLabels.Icon(data.Category));
        it.Card.SetLocked(data.RequiresResearch);
        it.Price.text = BlockforgeTheme.Num(data.PriceCredits);
        it.Owned.text = data.RequiresResearch ? "Research first" : data.Owned > 0 ? "Owned " + BlockforgeTheme.Num(data.Owned) : "";
        it.Card.Selected = selected != null && selected.Id == data.Id;
    }

    private void Select(StoreItem item, bool focus)
    {
        if (item != null && item.RequiresResearch)
            return; // verrouillee : pas selectionnable pour l'achat
        selected = item;
        foreach (var it in items)
        {
            it.Card.Selected = item != null && it.Data != null && it.Data.Id == item.Id;
            if (focus && it.Card.Selected) UIKit.Focus(it.Card.Button);
        }
        successNotice.Rect.gameObject.SetActive(false);
        ShowDetail(item);
    }

    private void ShowDetail(StoreItem item)
    {
        bool has = item != null;
        Detail.SetTitle(has ? item.Name : "Select a part", has ? $"{MetaLabels.Singular(item.Category)} · {MetaLabels.Of(item.Grade)}" : "Pick one on the shelf to see its stats.");
        description.text = has ? item.Description : "";
        Clear(statsHost);
        Clear(gradesHost);
        stats.Clear();
        statsHost.gameObject.SetActive(has);
        gradesHost.transform.parent.gameObject.SetActive(has);
        ownedNotice.Rect.gameObject.SetActive(has);
        buyRow.gameObject.SetActive(has);
        if (!has) return;
        previewGrade.color = kit.GradeColor(item.Grade);
        foreach (var (label, value, ratio) in item.Stats)
        {
            string unit = label switch { "Fire rate" => "/s", "Range" => "m", "Mass" => "kg", _ => null };
            stats.Add(kit.StatLine(statsHost, "Stat_" + label, label, value, unit, ratio));
        }
        foreach (Grade g in System.Enum.GetValues(typeof(Grade)))
        {
            if (g == Grade.Raw && item.Grade != Grade.Raw) continue;
            bool owned = g == item.Grade;
            bool unresearched = g > item.Grade;
            kit.Badge(gradesHost, "Grade_" + g, BadgeScale.Grade, g.ToString().ToLowerInvariant(), MetaLabels.Of(g), owned, unresearched ? "lock" : null);
        }
        qty.Set(1, false);
        UpdateBuy();
    }

    private void UpdateBuy()
    {
        if (selected == null || buy == null) return;
        var (robotName, robotCpu, budget) = service.CurrentRobot;
        int count = qty.Value;
        int total = selected.PriceCredits * count;
        int after = robotCpu + selected.Cpu * count;
        bool over = after > budget;
        string ordinal = (selected.Owned + 1) switch { 1 => "A first", 2 => "A second", 3 => "A third", 4 => "A fourth", _ => "Another" };
        string impact = count > 1
            ? $"{BlockforgeTheme.Num(count)} more add {BlockforgeTheme.Num(selected.Cpu * count)} CPU to your {robotName} — {BlockforgeTheme.Num(after)} of {BlockforgeTheme.Num(budget)}."
            : $"{ordinal} {selected.Name.ToLowerInvariant()} adds {BlockforgeTheme.Num(selected.Cpu)} CPU to your {robotName} — {BlockforgeTheme.Num(after)} of {BlockforgeTheme.Num(budget)}.";
        if (over)
            impact = impact.Replace($"{BlockforgeTheme.Num(after)} of", BlockforgeTheme.Tint(BlockforgeTheme.Num(after), kit.P.DangerInk) + " of");
        ownedNotice.Set(over ? DSTone.Warning : DSTone.Info, "Owned: " + BlockforgeTheme.Num(selected.Owned), impact);
        buy.Text = "Buy for " + BlockforgeTheme.Num(total);
        if (wallet.Credits < total)
            buy.Disable($"You need {BlockforgeTheme.Num(total - wallet.Credits)} more credits.");
        else
            buy.Enable();
    }

    private async void Buy()
    {
        if (selected == null) return;
        try
        {
            int count = qty.Value;
            await service.Buy(selected.Id, count);
            if (this == null) return;
            // Le service a change : Owned mis a jour, selection conservee
            if (service is MockStore mock)
                selected = mock.Find(selected.Id) ?? selected;
            successNotice.Set(DSTone.Success, "Bought", $"{BlockforgeTheme.Num(count)} × {selected.Name} added to your inventory. You now own {BlockforgeTheme.Num(selected.Owned)}.");
            successNotice.Rect.gameObject.SetActive(true);
            foreach (var it in items)
                if (it.Data != null && it.Data.Id == selected.Id) BindItem(it, selected);
            UpdateBuy();
        }
        catch (InsufficientFundsException) { Toast("Not enough credits.", false); }
        catch (System.Exception e) { Debug.LogException(e); Toast("Couldn't buy this part.", false); }
    }

    private async void BuyBundle()
    {
        if (bundle == null) return;
        try
        {
            await service.BuyBundle(bundle.Id);
            Toast($"{bundle.Name} is yours.", true);
            buyBundle.Disable("Already bought.");
        }
        catch (InsufficientFundsException) { Toast("Not enough credits.", false); }
        catch (System.Exception e) { Debug.LogException(e); Toast("Couldn't buy this bundle.", false); }
    }

    protected override bool OnCancel()
    {
        if (sort.Popup != null) { sort.Close(); return true; }
        if (selected == null) return false;
        Select(null, false);
        return true;
    }
}
