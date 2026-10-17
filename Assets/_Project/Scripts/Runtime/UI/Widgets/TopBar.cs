using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// BARRE DE NAVIGATION DU HUB (TopBar du design « screens »)
// Bandeau surface-raised de 80 px, filet border-subtle en bas :
// le lockup (36 px de haut) a gauche, les onglets Home · Play ·
// Build · Tech Tree · Workshop · Store centres (Tabs nav : souligne
// 2 px accent sous l'actif), a droite le solde de credits (readout
// mono en grade-masterwork) et la puce joueur (avatar rond 28 px
// sur surface-inset, pseudo).
// Un seul composant pour tout le jeu :
// - Build() la construit au lancement (UIPreview, scenes sans menu) ;
// - Adopt() s'accroche au header serialise de MainMenu (meme
//   hierarchie : TopNavigation / NavButtons / Nav_<Section> /
//   Right / Credits / Player), pose par Setup MainMenu Header.
// Q / E et les gachettes hautes (LB / RB) font defiler les onglets.
// =========================================================

public enum HubSection { Home, Play, Build, TechTree, Workshop, Store }

public sealed class TopBar : MonoBehaviour
{
    public static readonly (HubSection section, string key, string label, string icon)[] Sections =
    {
        (HubSection.Home, "Home", "Home", "home"),
        (HubSection.Play, "Play", "Play", "play"),
        (HubSection.Build, "Build", "Build", "create"),
        (HubSection.TechTree, "TechTree", "Tech Tree", "electronics"),
        (HubSection.Workshop, "Workshop", "Workshop", "cube"),
        (HubSection.Store, "Store", "Store", "store"),
    };

    public static string KeyOf(HubSection s) => Sections[(int)s].key;
    public static string LabelOf(HubSection s) => Sections[(int)s].label;
    public static bool TryParse(string key, out HubSection section)
    {
        foreach (var s in Sections)
            if (s.key == key) { section = s.section; return true; }
        section = HubSection.Home;
        return false;
    }

    [Tooltip("Onglet souligne")]
    [SerializeField] private HubSection active = HubSection.Home;
    public event Action<HubSection> Selected;

    public TMP_Text CreditsText { get; private set; }
    public TMP_Text PlayerNameText { get; private set; }
    public RectTransform NavRoot { get; private set; }
    private readonly Dictionary<HubSection, DSElement> tabs = new();
    private readonly Dictionary<HubSection, Button> buttons = new();
    private bool inputEnabled = true;

    public HubSection Active
    {
        get => active;
        set
        {
            active = value;
            foreach (var pair in tabs)
                pair.Value.Selected = pair.Key == value;
        }
    }

    // Coupe Q / E pendant qu'un champ de texte a le focus ou qu'un dialogue est ouvert
    public bool InputEnabled { get => inputEnabled; set => inputEnabled = value; }

    public void SetCredits(long credits)
    {
        if (CreditsText != null)
            CreditsText.text = BlockforgeTheme.Num(credits);
    }

    public void SetPlayer(string pseudo)
    {
        if (PlayerNameText != null)
            PlayerNameText.text = pseudo;
    }

    public Button ButtonOf(HubSection section) => buttons.TryGetValue(section, out var b) ? b : null;

    private void Update()
    {
        if (!inputEnabled)
            return;
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        bool typing = UnityEngine.EventSystems.EventSystem.current != null &&
                      UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
                      UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
        if (typing)
            return;
        int delta = 0;
        if (kb != null && kb.qKey.wasPressedThisFrame) delta = -1;
        if (kb != null && kb.eKey.wasPressedThisFrame) delta = 1;
        if (pad != null && pad.leftShoulder.wasPressedThisFrame) delta = -1;
        if (pad != null && pad.rightShoulder.wasPressedThisFrame) delta = 1;
        if (delta != 0)
            Cycle(delta);
    }

    public void Cycle(int delta)
    {
        int count = Sections.Length;
        var next = (HubSection)(((int)active + delta + count) % count);
        Select(next);
    }

    public void Select(HubSection section)
    {
        Active = section;
        Selected?.Invoke(section);
    }

    // =========================================================
    // ADOPTION DU HEADER SERIALISE (scene MainMenu)
    // =========================================================

    public static TopBar Adopt(Transform topNavigation)
    {
        if (topNavigation == null)
            return null;
        var bar = topNavigation.GetComponent<TopBar>() ?? topNavigation.gameObject.AddComponent<TopBar>();
        bar.tabs.Clear();
        bar.buttons.Clear();
        bar.NavRoot = topNavigation.Find("NavButtons") as RectTransform;
        if (bar.NavRoot != null)
        {
            foreach (var s in Sections)
            {
                var tab = bar.NavRoot.Find("Nav_" + s.key);
                if (tab == null)
                    continue;
                if (tab.TryGetComponent<DSElement>(out var element))
                    bar.tabs[s.section] = element;
                if (tab.TryGetComponent<Button>(out var button))
                    bar.buttons[s.section] = button;
            }
        }
        bar.CreditsText = topNavigation.Find("Right/Credits/Value")?.GetComponent<TMP_Text>();
        bar.PlayerNameText = topNavigation.Find("Right/Player/Name")?.GetComponent<TMP_Text>();
        bar.Active = bar.active;
        return bar;
    }

    // =========================================================
    // CONSTRUCTION AU LANCEMENT
    // =========================================================

    public static TopBar Build(UIKit kit, Transform parent, HubSection active, string playerName, long credits, Action<HubSection> onSelect = null)
    {
        var T = BlockforgeTheme.TopBar;
        var nav = kit.Rect(parent, "TopNavigation");
        UIKit.TopStrip(nav, 0f, T.Height);
        kit.Element(nav, DSRole.Strip).hairlineEdge = DSEdge.Bottom;
        var bar = nav.gameObject.AddComponent<TopBar>();
        bar.active = active;

        // Lockup horizontal, 36 px de haut
        var logo = kit.Image(nav, "Logo", Color.white, DSAssets.Logo());
        logo.type = Image.Type.Simple;
        logo.preserveAspect = true;
        UIKit.Place(logo.rectTransform, UIKit.MiddleLeft, new Vector2(T.SidePadding, 0f), new Vector2(T.LogoWidth, T.LogoHeight));

        // Bloc de droite : credits puis puce joueur
        var right = kit.Row(nav, "Right", BlockforgeTheme.Space6, TextAnchor.MiddleRight);
        UIKit.Place(right, new Vector2(1f, 0f), new Vector2(1f, 1f), UIKit.MiddleRight, new Vector2(-T.SidePadding, 0f), new Vector2(T.ChipWidth + BlockforgeTheme.Space6 + T.CreditsWidth + BlockforgeTheme.IconSize.Lg + BlockforgeTheme.Space2, 0f));
        var credits = kit.Row(right, "Credits", BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        kit.Icon(credits, "Icon", "credits", BlockforgeTheme.IconSize.Lg, kit.P.GradeMasterwork);
        bar.CreditsText = kit.Mono(credits, "Value", T.CreditsValueSize, 600, kit.P.GradeMasterwork, BlockforgeTheme.Num(credits));
        UIKit.Fixed(bar.CreditsText, T.CreditsWidth, -1f);
        var player = kit.Row(right, "Player", BlockforgeTheme.Space3 - 2f, TextAnchor.MiddleLeft, UIKit.Pad(T.ChipPadding, BlockforgeTheme.Space3, 0f, 0f));
        UIKit.Fixed(player, T.ChipWidth, T.ChipHeight);
        var avatar = kit.Shape(player, "Avatar", DSRadius.Pill, kit.P.SurfaceInset);
        UIKit.Fixed(avatar, T.AvatarSize, T.AvatarSize);
        var avatarIcon = kit.Icon(avatar.transform, "Icon", "user", BlockforgeTheme.IconSize.Md, kit.P.Accent);
        UIKit.Place(avatarIcon.rectTransform, UIKit.Center, Vector2.zero, new Vector2(BlockforgeTheme.IconSize.Md, BlockforgeTheme.IconSize.Md));
        bar.PlayerNameText = kit.Text(player, "Name", DSTextStyle.TitleSM, kit.P.InkPrimary, playerName);
        UIKit.Flexible(bar.PlayerNameText);
        kit.Element(player, DSRole.Panel, new TMP_Text[] { bar.PlayerNameText });

        // Onglets centres entre le logo et le bloc de droite
        float rightW = right.sizeDelta.x;
        bar.NavRoot = kit.Row(nav, "NavButtons", BlockforgeTheme.Space1, TextAnchor.MiddleCenter, null, true, true, true);
        UIKit.Stretch(bar.NavRoot, T.SidePadding + T.LogoWidth + BlockforgeTheme.Space8, 0f, T.SidePadding + rightW + BlockforgeTheme.Space8, 0f);
        foreach (var s in Sections)
        {
            var tab = kit.Rect(bar.NavRoot, "Nav_" + s.key);
            tab.gameObject.AddComponent<Image>().raycastTarget = true;
            var button = UIKit.Clickable(tab);
            var label = kit.Text(tab, "Text", DSTextStyle.Label, kit.P.InkSecondary, s.label, TextAlignmentOptions.Center);
            label.fontSize = T.TabLabelSize;
            UIKit.Stretch(label.rectTransform);
            var element = kit.Element(tab, DSRole.TabNav, new TMP_Text[] { label });
            kit.Group(tab);
            UIKit.Fixed(tab, Mathf.Max(T.TabMinWidth, UIKit.PreferredWidth(label) + 2f * BlockforgeTheme.Space5), T.Height);
            bar.tabs[s.section] = element;
            bar.buttons[s.section] = button;
            var section = s.section;
            button.onClick.AddListener(() => bar.Select(section));
        }
        bar.Active = active;
        if (onSelect != null)
            bar.Selected += onSelect;
        return bar;
    }
}
