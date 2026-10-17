using System.Collections.Generic;
using Blockforge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// TECH TREE — recherche (artboard TechTree)
// Ligne de titre : puce Tech points (tuile electronics 32 px) et
// « How research works ». En-tete Tier I … V aligne sur les
// colonnes. Quatre voies de 150 px (Structure, Movement, Weapons,
// Electronics) : tete 170 px (pictogramme + nom en couleur de
// categorie, « n of 5 researched »), cinq noeuds 176 x 108 relies
// par des connecteurs de 58 px. Etats : Researched (accent +
// accent-soft + check), Available (unlock + cout), Locked (0,55 +
// lock, non recherchable). Connecteur accent / border-strong /
// border-subtle selon ses deux bouts. Legende sous les voies.
// Detail : nom + « Weapons · Tier III · Tempered », apercu avec
// barre de grade, description, quatre StatLines, Requires /
// Unlocks next / Store price after research, CapacityMeter Tech
// points, Research for N TP (desactive avec raison). Rechercher
// passe le noeud en Researched, recolore ses connecteurs, ouvre
// le suivant, debite le portefeuille et met la piece au Store.
// =========================================================

public sealed class TechTreeScreen : HubScreen
{
    public override HubSection Section => HubSection.TechTree;
    protected override string ScreenTitle => "Tech Tree";
    protected override string ScreenSubtitle => "Research a part to put it in the Store. Each lane unlocks left to right; tech points come from matches.";

    private ITechTreeService service;
    private IWallet wallet;

    private TextMeshProUGUI techPointsValue;
    private RectTransform lanesHost;
    private readonly Dictionary<string, NodeView> nodes = new();
    private readonly Dictionary<string, Image> connectors = new();  // cle : id du noeud de droite
    private readonly Dictionary<Category, TextMeshProUGUI> laneProgress = new();
    private IReadOnlyList<TechNode> all = new List<TechNode>();
    private TechNode selected;
    private DSElement selectedRing;

    // Detail
    private RectTransform preview, statsHost, requiresHost, unlocksHost, priceHost;
    private Image previewGrade;
    private TextMeshProUGUI description;
    private DSWidgets.CapacityMeterWidget meter;
    private ButtonWidget research;

    private sealed class NodeView
    {
        public RectTransform Rect;
        public Button Button;
        public DSElement Element;
        public CanvasGroup Group;
        public Image Edge, LaneIcon, StateIcon;
        public TextMeshProUGUI Name, Grade, Foot;
        public TechNode Data;
    }

    private static readonly (Category lane, string name)[] Lanes =
    {
        (Category.Structure, "Structure"), (Category.Mobility, "Movement"), (Category.Weapon, "Weapons"), (Category.Electronics, "Electronics"),
    };

    protected override void Build()
    {
        service = MetaServices.TechTree;
        wallet = MetaServices.Wallet;
        var T = BlockforgeTheme.Tech;
        var H = BlockforgeTheme.Hub;

        // ---------- Ligne de titre ----------
        var chip = kit.Row(TitleControls, "TechPoints", T.ChipGap, TextAnchor.MiddleLeft, UIKit.Pad(T.ChipPadX, T.ChipPadY));
        var tile = kit.Shape(chip, "Tile", DSRadius.Sm, kit.P.SurfaceInset);
        UIKit.Fixed(tile, T.ChipTile, T.ChipTile);
        var tileIcon = kit.Icon(tile.transform, "Icon", "electronics", BlockforgeTheme.IconSize.Lg, kit.P.Accent);
        UIKit.Place(tileIcon.rectTransform, UIKit.Center, Vector2.zero, new Vector2(BlockforgeTheme.IconSize.Lg, BlockforgeTheme.IconSize.Lg));
        var chipText = kit.Column(chip, "Text", 2f, TextAnchor.MiddleLeft, null, true, true, false);
        kit.Text(chipText, "Label", DSTextStyle.Label, kit.P.InkMuted, "Tech points");
        techPointsValue = kit.Mono(chipText, "Value", T.ChipValueSize, 600, kit.P.InkPrimary, "0");
        kit.Element(chip, DSRole.Panel);
        kit.Button(TitleControls, "How", ButtonVariant.Secondary, "How research works", () => Toast("Research a part with tech points; it then shows up in the Store.", true), "question");

        // ---------- Tiers + voies ----------
        var tiers = kit.Row(Content, "Tiers", 0f, TextAnchor.MiddleLeft, UIKit.Pad(T.LaneHeadWidth, 0f, 0f, 0f));
        UIKit.Fixed(tiers, -1f, BlockforgeTheme.Type.Label.LineHeight);
        for (int i = 1; i <= T.Tiers; i++)
        {
            var label = kit.Caps(tiers, "Tier" + i, T.TierLabelSize, 600, T.TierLabelTracking, kit.P.InkMuted, "Tier " + MetaLabels.Roman(i));
            UIKit.Fixed(label, T.TierColumn, -1f);
        }
        lanesHost = kit.Column(Content, "Lanes", 0f, TextAnchor.UpperLeft, null, true, true, true);
        UIKit.Flexible(lanesHost, 1f, 1f);

        // ---------- Legende ----------
        var legend = kit.Row(Content, "Legend", T.LegendGap, TextAnchor.MiddleLeft);
        UIKit.Fixed(legend, -1f, BlockforgeTheme.Type.BodySm.LineHeight);
        LegendItem(legend, "check", kit.P.Accent, "Researched", 1f);
        LegendItem(legend, "unlock", kit.P.InkSecondary, "Available — the cost is in tech points", 1f);
        LegendItem(legend, "lock", kit.P.InkSecondary, "Locked — research the part before it", 0.6f);
        kit.Spacer(legend);
        kit.Text(legend, "Edge", BlockforgeTheme.Type.BodySm.With(T.LegendSize), kit.P.InkSecondary, "Edge bar colour = the part's grade");

        BuildDetail();
    }

    private void LegendItem(Transform parent, string icon, Color iconColor, string text, float alpha)
    {
        var row = kit.Row(parent, "Legend_" + icon, BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        kit.Icon(row, "Icon", icon, BlockforgeTheme.IconSize.Sm, iconColor);
        kit.Text(row, "Text", BlockforgeTheme.Type.BodySm.With(BlockforgeTheme.Tech.LegendSize), kit.P.InkSecondary, text);
        kit.Group(row).alpha = alpha;
    }

    private void BuildLanes()
    {
        var T = BlockforgeTheme.Tech;
        Clear(lanesHost);
        nodes.Clear();
        connectors.Clear();
        laneProgress.Clear();
        foreach (var (lane, name) in Lanes)
        {
            var row = kit.Row(lanesHost, "Lane_" + lane, 0f, TextAnchor.MiddleLeft);
            UIKit.Fixed(row, -1f, T.LaneHeight);
            var hair = kit.Image(row, "Hairline", kit.P.BorderSubtle);
            UIKit.BottomStrip(hair.rectTransform, 0f, BlockforgeTheme.ControlBorder);
            UIKit.IgnoreLayout(hair);

            var head = kit.Column(row, "Head", 6f, TextAnchor.MiddleLeft, UIKit.Pad(0f, T.LaneHeadPadRight, 0f, 0f), true, true, false);
            UIKit.Fixed(head, T.LaneHeadWidth, -1f);
            var title = kit.Row(head, "Title", T.LaneTitleGap, TextAnchor.MiddleLeft);
            var color = kit.CategoryColor(lane);
            kit.Icon(title, "Icon", MetaLabels.Icon(lane), BlockforgeTheme.IconSize.Xl, color);
            kit.Condensed(title, "Name", T.LaneTitleSize, 700, 0.03f, color, name);
            laneProgress[lane] = kit.Text(head, "Progress", DSTextStyle.Caption, kit.P.InkMuted, "");

            var laneNodes = new List<TechNode>();
            foreach (var n in all)
                if (n.Lane == lane) laneNodes.Add(n);
            laneNodes.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            for (int i = 0; i < laneNodes.Count; i++)
            {
                if (i > 0)
                {
                    var link = kit.Image(row, "Link_" + laneNodes[i].Id, kit.P.BorderSubtle);
                    UIKit.Fixed(link, T.NodeGap, T.Connector);
                    connectors[laneNodes[i].Id] = link;
                }
                nodes[laneNodes[i].Id] = BuildNode(row, laneNodes[i]);
            }
        }
    }

    private NodeView BuildNode(Transform parent, TechNode n)
    {
        var T = BlockforgeTheme.Tech;
        var v = new NodeView { Data = n };
        v.Rect = kit.Rect(parent, "Node_" + n.Id);
        v.Rect.gameObject.AddComponent<Image>().raycastTarget = true;
        v.Button = UIKit.Clickable(v.Rect);
        UIKit.Fixed(v.Rect, T.NodeWidth, T.NodeHeight);
        v.Group = kit.Group(v.Rect);

        v.Edge = kit.Shape(v.Rect, "Edge", DSRadius.Xs, kit.GradeColor(n.Grade));
        UIKit.Place(v.Edge.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(BlockforgeTheme.Store.GradeBar, -2f * T.NodeEdgeInset));

        var column = kit.Column(v.Rect, "Layout", 0f, TextAnchor.UpperLeft, UIKit.Pad(T.NodePadLeft, T.NodePadRight, T.NodePadTop, T.NodePadBottom), true, true, true);
        UIKit.Stretch(column);
        var top = kit.Row(column, "Top", BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        UIKit.Fixed(top, -1f, BlockforgeTheme.IconSize.Md);
        v.LaneIcon = kit.Icon(top, "Lane", MetaLabels.Icon(n.Lane), BlockforgeTheme.IconSize.Md, kit.P.InkSecondary);
        kit.Spacer(top);
        v.StateIcon = kit.Icon(top, "State", "unlock", BlockforgeTheme.IconSize.Md, kit.P.InkSecondary);
        kit.Spacer(column);
        v.Name = kit.Text(column, "Name", BlockforgeTheme.Type.TitleSm.With(T.NodeNameSize), kit.P.InkPrimary, n.Name);
        kit.Spacer(column);
        var foot = kit.Row(column, "Foot", BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        UIKit.Fixed(foot, -1f, BlockforgeTheme.Type.Caption.LineHeight);
        v.Grade = kit.Mono(foot, "Grade", T.NodeFootSize, 500, kit.P.InkSecondary, MetaLabels.Of(n.Grade));
        kit.Spacer(foot);
        v.Foot = kit.Mono(foot, "Cost", T.NodeFootSize, 500, kit.P.InkSecondary, "", TextAlignmentOptions.MidlineRight);

        v.Element = kit.Element(v.Rect, DSRole.TechNode, new TMP_Text[] { v.Name }, new Graphic[] { v.LaneIcon, v.StateIcon });
        v.Button.onClick.AddListener(() => Select(v.Data, true));
        ApplyState(v);
        return v;
    }

    private void ApplyState(NodeView v)
    {
        var n = v.Data;
        bool done = n.State == TechState.Researched;
        bool locked = n.State == TechState.Locked;
        v.Element.Done = done;
        v.Group.alpha = locked ? BlockforgeTheme.Tech.LockedOpacity : 1f;
        v.StateIcon.sprite = DSAssets.Icon(done ? "check" : locked ? "lock" : "unlock");
        v.Foot.text = done ? "Researched" : BlockforgeTheme.Qty(n.CostTechPoints, "TP");
        var foot = done ? kit.P.Accent : kit.P.InkSecondary;
        v.Foot.color = foot;
        v.Grade.color = foot;
        v.Button.interactable = true; // un noeud verrouille se selectionne (pour lire sa fiche) mais ne se recherche pas
    }

    private void RefreshConnectors()
    {
        foreach (var pair in connectors)
        {
            var right = nodes[pair.Key].Data;
            var left = right.RequiresId != null && nodes.TryGetValue(right.RequiresId, out var l) ? l.Data : null;
            var state = left == null ? ConnectorState.Idle : MatchLogic.Connector(left.State, right.State);
            pair.Value.color = state switch { ConnectorState.Done => kit.P.Accent, ConnectorState.Reached => kit.P.BorderStrong, _ => kit.P.BorderSubtle };
        }
        foreach (var (lane, _) in Lanes)
        {
            int done = 0, total = 0;
            foreach (var n in all)
                if (n.Lane == lane) { total++; if (n.State == TechState.Researched) done++; }
            if (laneProgress.TryGetValue(lane, out var text))
                text.text = $"{BlockforgeTheme.Num(done)} of {BlockforgeTheme.Num(total)} researched";
        }
    }

    private void BuildDetail()
    {
        var T = BlockforgeTheme.Tech;
        var H = BlockforgeTheme.Hub;
        var content = Detail.Content;
        preview = Preview(content, "Preview", T.DetailPreview, "[3D PREVIEW]", kit.P.GradeRaw);
        previewGrade = preview.Find("GradeBar").GetComponent<Image>();
        description = Paragraph(content, "Description", "");
        statsHost = kit.Column(content, "Stats", H.StatGap);
        var rows = kit.Column(content, "Rows", T.DetailRowsGap);
        DetailRow(rows, "Requires", "Requires", out requiresHost);
        DetailRow(rows, "Unlocks", "Unlocks next", out unlocksHost);
        DetailRow(rows, "Price", "Store price after research", out priceHost);
        kit.Spacer(content);
        meter = kit.CapacityMeter(content, "Meter", "Tech points", 0, 1, "");
        research = kit.Button(content, "Research", ButtonVariant.Primary, "Research", Research, "unlock", BlockforgeTheme.ControlLg, true);
    }

    // ---------- Donnees ----------

    protected override void OnOpened(bool first)
    {
        wallet.TechPointsChanged -= OnTechPoints;
        wallet.TechPointsChanged += OnTechPoints;
        Refresh(true);
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.TechPointsChanged -= OnTechPoints;
    }

    private void OnTechPoints(int _)
    {
        techPointsValue.text = BlockforgeTheme.Num(wallet.TechPoints);
        ShowDetail(selected);
    }

    private async void Refresh(bool rebuild)
    {
        try
        {
            all = await service.Nodes();
            if (this == null) return;
            techPointsValue.text = BlockforgeTheme.Num(service.TechPoints);
            if (rebuild || nodes.Count == 0)
                BuildLanes();
            else
            {
                foreach (var n in all)
                    if (nodes.TryGetValue(n.Id, out var v)) { v.Data = n; ApplyState(v); }
            }
            RefreshConnectors();
            TechNode keep = null;
            if (selected != null) foreach (var n in all) if (n.Id == selected.Id) keep = n;
            if (keep == null) foreach (var n in all) if (n.State == TechState.Available) { keep = n; break; }
            Select(keep ?? (all.Count > 0 ? all[0] : null), false);
        }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    private void Select(TechNode n, bool focus)
    {
        selected = n;
        foreach (var pair in nodes)
        {
            bool sel = n != null && pair.Key == n.Id;
            pair.Value.Element.Selected = sel;
            if (sel && focus) UIKit.Focus(pair.Value.Button);
        }
        ShowDetail(n);
    }

    private void ShowDetail(TechNode n)
    {
        bool has = n != null;
        Detail.SetTitle(has ? n.Name : "Select a part", has ? $"{MetaLabels.Of(n.Lane)} · Tier {MetaLabels.Roman(n.Tier)} · {MetaLabels.Of(n.Grade)}" : "Pick a node to read about it.");
        description.text = has ? n.Description : "";
        Clear(statsHost);
        Clear(requiresHost);
        Clear(unlocksHost);
        Clear(priceHost);
        statsHost.gameObject.SetActive(has);
        requiresHost.parent.parent.gameObject.SetActive(has); // la colonne Requires / Unlocks / Price
        meter.Rect.gameObject.SetActive(has);
        research.Rect.gameObject.SetActive(has);
        if (!has) return;
        previewGrade.color = kit.GradeColor(n.Grade);
        foreach (var (label, value, ratio) in n.Stats)
        {
            string unit = label switch { "Fire rate" => "/s", "Range" => "m", "Mass" => "kg", _ => null };
            kit.StatLine(statsHost, "Stat_" + label, label, value, unit, ratio);
        }

        TechNode prev = null, next = null;
        foreach (var o in all)
        {
            if (o.Id == n.RequiresId) prev = o;
            if (o.Id == n.UnlocksId) next = o;
        }
        if (prev != null)
        {
            bool ok = prev.State == TechState.Researched;
            kit.Icon(requiresHost, "Icon", ok ? "check" : "lock", BlockforgeTheme.IconSize.Sm, ok ? kit.P.Accent : kit.P.InkMuted);
            kit.Text(requiresHost, "Value", DSTextStyle.BodySM, ok ? kit.P.Accent : kit.P.InkPrimary, prev.Name);
        }
        else
            kit.Text(requiresHost, "Value", DSTextStyle.BodySM, kit.P.InkPrimary, "Nothing");
        kit.Text(unlocksHost, "Value", DSTextStyle.BodySM, kit.P.InkPrimary, next != null ? $"{next.Name} · {BlockforgeTheme.Num(next.CostTechPoints)}" : "Top of the lane");
        kit.Icon(priceHost, "Icon", "credits", BlockforgeTheme.IconSize.Sm, kit.P.GradeMasterwork);
        kit.Mono(priceHost, "Value", BlockforgeTheme.Type.BodySm.Size, 600, kit.P.GradeMasterwork, BlockforgeTheme.Num(n.StorePriceAfter));

        int have = wallet.TechPoints;
        bool done = n.State == TechState.Researched;
        bool locked = n.State == TechState.Locked;
        if (done)
        {
            meter.Set(n.CostTechPoints, Mathf.Max(1, n.CostTechPoints), "Researched");
            research.Text = "Researched";
            research.Disable("Already researched.");
        }
        else
        {
            bool shortOf = have < n.CostTechPoints;
            // value = cout ; max = cout si on peut payer, sinon ce qu'on a (le manque passe en danger)
            meter.Set(n.CostTechPoints, shortOf ? Mathf.Max(1, have) : n.CostTechPoints,
                      $"{BlockforgeTheme.Num(n.CostTechPoints)} needed · you have {BlockforgeTheme.Num(have)}");
            research.Text = "Research for " + BlockforgeTheme.Qty(n.CostTechPoints, "TP");
            if (locked)
                research.Disable(prev != null ? $"Research {prev.Name} first." : "Locked.");
            else if (shortOf)
                research.Disable($"You need {BlockforgeTheme.Num(n.CostTechPoints - have)} more tech points.");
            else
                research.Enable();
        }
    }

    private async void Research()
    {
        if (selected == null) return;
        try
        {
            string id = selected.Id;
            await service.Research(id);
            if (this == null) return;
            Toast($"{selected.Name} researched. It is now in the Store.", true);
            Refresh(false);
        }
        catch (InsufficientFundsException) { Toast("Not enough tech points.", false); }
        catch (System.InvalidOperationException e) { Toast(e.Message, false); }
        catch (System.Exception e) { Debug.LogException(e); Toast("Couldn't research this part.", false); }
    }

    protected override bool OnCancel()
    {
        if (selected == null) return false;
        Select(null, false);
        return true;
    }
}
