using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// ECRAN DU HUB (base de Workshop, Store, Tech Tree)
// Theme dark, sous la TopBar. Cadre commun : padding 32 / 48,
// grille « minmax(0, 1fr) 440px » avec 32 px d'ecart, un titre
// display-xl avec son sous-titre body ink-secondary et les
// controles de la ligne de titre a droite, un Panel de detail de
// 440 px qui reflete la selection. Le premier element est
// selectionne a l'ouverture ; Echap / B efface la selection.
// Les ecrans restent vivants entre deux onglets (etat, selection) :
// HubScreenHost les fond en 220 ms.
// =========================================================

public abstract class HubScreen : MonoBehaviour
{
    public abstract HubSection Section { get; }
    public CanvasGroup Group { get; private set; }
    public RectTransform Root => (RectTransform)transform;
    public bool IsOpen { get; private set; }

    protected UIKit kit = UIKit.Dark;
    protected RectTransform Body, Left, TitleRow, TitleBlock, TitleControls, Content;
    protected DSWidgets.PanelWidget Detail;
    protected TextMeshProUGUI Title, Subtitle;
    private bool built;

    // ---------- Cadre ----------

    public void EnsureBuilt()
    {
        if (built)
            return;
        built = true;
        Group = kit.Group(Root);
        Root.gameObject.AddComponent<TabFocusCycler>();
        var H = BlockforgeTheme.Hub;

        Body = kit.Row(Root, "Body", H.ColumnGap, TextAnchor.UpperLeft, UIKit.Pad(H.PadSide, H.PadSide, H.PadTop, H.PadBottom), true, true, true);
        UIKit.Stretch(Body);

        Left = kit.Column(Body, "Main", H.SectionGap, TextAnchor.UpperLeft, null, true, true, true);
        UIKit.Flexible(Left, 1f, 1f, 0f);

        TitleRow = kit.Row(Left, "TitleRow", H.TitleRowGap, TextAnchor.LowerLeft);
        TitleBlock = kit.Column(TitleRow, "TitleBlock", H.TitleSubGap, TextAnchor.UpperLeft, null, true, true, false);
        UIKit.Flexible(TitleBlock);
        Title = kit.Text(TitleBlock, "Title", DSTextStyle.DisplayXL, kit.P.InkPrimary, ScreenTitle);
        Subtitle = kit.Text(TitleBlock, "Subtitle", DSTextStyle.Body, kit.P.InkSecondary, ScreenSubtitle);
        TitleControls = kit.Row(TitleRow, "Controls", H.TitleRowGap, TextAnchor.LowerLeft);

        Content = kit.Column(Left, "Content", H.SectionGap, TextAnchor.UpperLeft, null, true, true, true);
        UIKit.Flexible(Content, 1f, 1f);

        Detail = kit.Panel(Body, "Detail", "", "");
        UIKit.Fixed(Detail.Rect, H.DetailWidth, -1f);
        Detail.Rect.GetComponent<LayoutElement>().flexibleHeight = 1f;

        Build();
    }

    protected abstract string ScreenTitle { get; }
    protected abstract string ScreenSubtitle { get; }
    protected abstract void Build();
    protected abstract void OnOpened(bool first);
    protected virtual bool OnCancel() => false;   // true = quelque chose a ete ferme / efface

    // ---------- Ouverture / fermeture ----------

    private bool openedOnce;

    public void Open()
    {
        EnsureBuilt();
        IsOpen = true;
        gameObject.SetActive(true);
        Group.interactable = true;
        Group.blocksRaycasts = true;
        OnOpened(!openedOnce);
        openedOnce = true;
    }

    public void Close()
    {
        IsOpen = false;
        if (Group != null)
        {
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }
    }

    protected virtual void Update()
    {
        if (!IsOpen)
            return;
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        bool typing = UnityEngine.EventSystems.EventSystem.current != null &&
                      UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
                      UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
        if (typing)
            return;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            OnCancel();
    }

    // ---------- Petits outils partages ----------

    // Zone d'apercu 3D (rendu a venir) : puits degrade avec un libelle mono
    protected RectTransform Preview(Transform parent, string name, float height, string placeholder, Color? gradeBar = null)
    {
        var rt = kit.Rect(parent, name);
        UIKit.Fixed(rt, -1f, height);
        var poly = kit.Polygon(rt, "Ground");
        UIKit.Stretch(poly.rectTransform);
        poly.vertical = true;
        poly.SetColors(kit.P.SurfaceHover, kit.P.SurfaceInset);
        var label = kit.Mono(rt, "Placeholder", BlockforgeTheme.Type.BodySm.Size, 500, kit.P.InkMuted, placeholder, TextAlignmentOptions.Center);
        label.characterSpacing = 6f;
        UIKit.Stretch(label.rectTransform);
        if (gradeBar.HasValue)
        {
            var bar = kit.Image(rt, "GradeBar", gradeBar.Value);
            UIKit.Place(bar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(BlockforgeTheme.Store.GradeBar, 0f));
        }
        return rt;
    }

    protected TextMeshProUGUI Paragraph(Transform parent, string name, string text)
    {
        var t = kit.Text(parent, name, BlockforgeTheme.Type.BodySm.With(BlockforgeTheme.Hub.DetailParagraphSize), kit.P.InkSecondary, text, TextAlignmentOptions.TopLeft, false, true);
        return t;
    }

    // Ligne « libelle ... valeur » d'une fiche (Requires, Unlocks next...)
    protected RectTransform DetailRow(Transform parent, string name, string label, out RectTransform valueHost)
    {
        var row = kit.Row(parent, name, BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        UIKit.Fixed(row, -1f, BlockforgeTheme.Type.BodySm.LineHeight);
        var l = kit.Text(row, "Label", DSTextStyle.BodySM, kit.P.InkSecondary, label);
        UIKit.Flexible(l);
        valueHost = kit.Row(row, "Value", BlockforgeTheme.Space1 + 2f, TextAnchor.MiddleRight);
        return row;
    }

    protected static void Clear(Transform parent)
    {
        // Desactives tout de suite : le layout de cette image ne les compte plus
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    protected void Toast(string message, bool success)
    {
        if (success) global::Toast.Success(message);
        else global::Toast.Error(message);
    }
}

// =========================================================
// HOTE DES ECRANS DU HUB
// Un conteneur sous la TopBar ; Show(section) fond l'ecran courant
// vers le suivant (220 ms, fondu seul en animations reduites) et
// garde les autres en vie. Les sections Home / Play / Build sont
// les ecrans historiques de MainMenu : le hote se masque alors.
// =========================================================

public sealed class HubScreenHost : MonoBehaviour
{
    public RectTransform Container { get; private set; }
    public TopBar Bar { get; private set; }
    private readonly Dictionary<HubSection, HubScreen> screens = new();
    private HubScreen current;
    private Coroutine transition;

    public static HubScreenHost Create(Transform canvas, TopBar bar, float topInset)
    {
        var kit = UIKit.Dark;
        var rt = kit.Rect(canvas, "HubScreens");
        UIKit.Stretch(rt, 0f, topInset, 0f, 0f);
        var host = rt.gameObject.AddComponent<HubScreenHost>();
        host.Container = rt;
        host.Bar = bar;
        host.Register(host.Make<WorkshopScreen>("Workshop"));
        host.Register(host.Make<StoreScreen>("Store"));
        host.Register(host.Make<TechTreeScreen>("TechTree"));
        return host;
    }

    private T Make<T>(string name) where T : HubScreen
    {
        var rt = UIKit.Dark.Rect(Container, name);
        UIKit.Stretch(rt);
        var screen = rt.gameObject.AddComponent<T>();
        rt.gameObject.SetActive(false);
        return screen;
    }

    public void Register(HubScreen screen)
    {
        screens[screen.Section] = screen;
    }

    public bool Handles(HubSection section) => screens.ContainsKey(section);

    public HubScreen Get(HubSection section) => screens.TryGetValue(section, out var s) ? s : null;

    // Ecran du hub : fondu croise ; section historique : le hote s'efface
    public void Show(HubSection section)
    {
        screens.TryGetValue(section, out var next);
        if (transition != null)
            StopCoroutine(transition);
        transition = StartCoroutine(Crossfade(next));
    }

    private IEnumerator Crossfade(HubScreen next)
    {
        float duration = BlockforgeTheme.Fade(BlockforgeTheme.Hub.CrossfadeTime);
        var previous = current;
        current = next;
        if (previous != null && previous != next)
        {
            previous.Close();
            yield return UIAnim.Fade(previous.Group, 0f, duration * 0.5f);
            previous.gameObject.SetActive(false);
        }
        if (next == null)
        {
            transition = null;
            yield break;
        }
        next.EnsureBuilt();
        next.Group.alpha = 0f;
        next.Open();
        yield return UIAnim.Fade(next.Group, 1f, duration);
        transition = null;
    }

    public void HideAll()
    {
        Show((HubSection)(-1));
    }
}
