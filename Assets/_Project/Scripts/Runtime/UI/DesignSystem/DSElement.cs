using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =========================================================
// ELEMENT DU DESIGN SYSTEM (uGUI)
// Porte le rendu d'un composant du DS sur l'Image de son
// GameObject : forme (rayon), fond, bordure 1 px, anneau de
// focus clavier (2 px, decale de 4 px), soulignement d'onglet,
// ombre, et TOUS les etats : repos, survol, focus clavier,
// presse, selectionne, desactive (+ vide pour une baie).
// Transitions de couleur en 80 ms ease-out (BlockforgeTheme).
//
// Pose par Blockforge > Apply Design System ; les scripts
// d'UI ne touchent plus aux couleurs : ils reglent Selected,
// Empty ou Tone, le composant fait le reste.
// =========================================================

public enum DSRole
{
    None,
    // ---------- Surfaces ----------
    Panel,          // surface-panel + border-subtle, radius-md
    PanelFloating,  // idem + shadow-panel : panneau pose sur la vue 3D
    PanelEdge,      // panneau contre un bord d'ecran : radius-none
    Strip,          // bandeau surface-raised (nav du haut, barre du bas) + filet border-subtle
    Inset,          // puits surface-inset (vignettes, apercus), radius-xs
    Track,          // piste de jauge surface-inset, radius-xs
    Fill,           // remplissage de jauge (accent / warning / danger selon Tone), radius-xs
    Scrim,          // voile derriere un dialogue
    Dialog,         // surface-panel, border-strong, radius-lg, shadow-overlay
    // ---------- Controles ----------
    ButtonPrimary,  // accent + ink-on-accent : une seule action principale par region
    ButtonSecondary,// accent-soft dans un bord border-strong
    ButtonGhost,    // texte seul : fermer, annuler, revenir
    ButtonDanger,   // danger : destructif et irreversible uniquement
    IconButton,     // pictogramme seul (outil du garage, supprimer...)
    ToolRail,       // barre d'outils du garage
    TabNav,         // onglet de navigation d'ecran : souligne en accent
    TabSegment,     // filtre dans un panneau : surface-selected + border-accent
    Field,          // champ de saisie : surface-inset + border-strong, accent au focus
    RobotSlot,      // baie de robot : radius-lg, vide = pointilles sur surface-inset
    PartCard,       // tuile de piece : radius-sm
    Card,           // carte cliquable (mode de jeu) : radius-md
    KeyCap,         // touche d'un KeyHint : surface-inset + border-strong, radius-xs
    Badge,          // puce : radius-pill
    Notice,         // message d'etat : fond *-soft + bord de la tonalite, radius-md
    // ---------- Hub (fin de match, Workshop, Store, Tech Tree) ----------
    ChipFilter,     // filtre pilule 32 px : border-strong, selectionne = border-accent + surface-selected
    TechNode,       // noeud de l'arbre : border-strong, radius-sm ; Done = accent + accent-soft
    Stepper,        // cadre d'un stepper de quantite : border-strong, radius-sm
    RowCard,        // ligne / carte cliquable a fond panel, radius-sm (cartes de robots du Workshop)
}

public enum DSTone { Neutral, Info, Success, Warning, Danger }

public enum DSEdge { Bottom, Top }

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class DSElement : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Composant")]
    public DSRole role = DSRole.Panel;
    public DSTone tone = DSTone.Neutral;
    [Tooltip("Theme des jetons : dark (menus, hub) ou hud (overlay de match, resultats)")]
    public DSTheme theme = DSTheme.Dark;
    [Tooltip("Etat selectionne (onglet actif, baie choisie, outil courant, mode actif)")]
    [SerializeField] private bool selected;
    [Tooltip("Baie vide : pointilles sur surface-inset, texte ink-muted")]
    [SerializeField] private bool empty;
    [Tooltip("Noeud recherche (TechNode) : bord accent, fond accent-soft")]
    [SerializeField] private bool done;
    [Tooltip("Filet d'un bandeau (Strip) ou d'un panneau contre un bord d'ecran (PanelEdge)")]
    public DSEdge hairlineEdge = DSEdge.Bottom;

    [Header("Contenu teinte par l'etat")]
    public TMP_Text[] labels = new TMP_Text[0];
    public Graphic[] icons = new Graphic[0];
    [Tooltip("Raccourci dans le coin d'un IconButton (ink-muted, accent si actif)")]
    public TMP_Text keyCorner;

    [Header("Parties generees (EnsureParts)")]
    public Image border;
    public Image focusRing;
    public Image underline;
    public Image shadow;

    private Image fill;
    private Selectable selectable;
    private TMP_InputField inputField;
    private CanvasGroup canvasGroup;
    private bool hovered, pressed, keyboardFocus, fieldFocus, disabled;

    // Transition
    private Look from, to, current;
    private float t = 1f;
    private bool initialized;

    private static int lastPointerFrame = -10;

    private struct Look
    {
        public Color fill, border, ink, icon, key, underline;
    }

    // =========================================================
    // API
    // =========================================================

    public bool Selected
    {
        get => selected;
        set { if (selected != value) { selected = value; Refresh(); } }
    }

    public bool Empty
    {
        get => empty;
        set { if (empty != value) { empty = value; ApplyShape(); Refresh(); } }
    }

    public DSTone Tone
    {
        get => tone;
        set { if (tone != value) { tone = value; Refresh(); } }
    }

    public bool Done
    {
        get => done;
        set { if (done != value) { done = value; Refresh(); } }
    }

    public DSTheme Theme
    {
        get => theme;
        set { if (theme != value) { theme = value; Refresh(); } }
    }

    // Jetons du theme courant
    private BlockforgeTheme.Palette P => BlockforgeTheme.PaletteFor(theme);

    public bool IsInteractive => role is DSRole.ButtonPrimary or DSRole.ButtonSecondary or DSRole.ButtonGhost
        or DSRole.ButtonDanger or DSRole.IconButton or DSRole.TabNav or DSRole.TabSegment or DSRole.Field
        or DSRole.RobotSlot or DSRole.PartCard or DSRole.Card or DSRole.ChipFilter or DSRole.TechNode or DSRole.RowCard;

    public DSRadius Radius => role switch
    {
        DSRole.Panel or DSRole.PanelFloating or DSRole.Card or DSRole.Notice => DSRadius.Md,
        DSRole.Dialog or DSRole.RobotSlot => DSRadius.Lg,
        DSRole.Inset or DSRole.Track or DSRole.Fill or DSRole.KeyCap => DSRadius.Xs,
        DSRole.Badge or DSRole.ChipFilter => DSRadius.Pill,
        DSRole.PanelEdge or DSRole.Strip or DSRole.Scrim or DSRole.TabNav or DSRole.None => DSRadius.None,
        _ => DSRadius.Sm,
    };

    private bool HasBorder => role is not (DSRole.None or DSRole.Strip or DSRole.PanelEdge or DSRole.Inset or DSRole.Track
        or DSRole.Fill or DSRole.Scrim or DSRole.TabNav or DSRole.ButtonPrimary or DSRole.ButtonDanger);

    private bool HasShadow => role is DSRole.PanelFloating or DSRole.Dialog;

    // Recalcule la cible et lance la transition (80 ms)
    public void Refresh()
    {
        if (!initialized)
        {
            RefreshImmediate();
            return;
        }
        from = current;
        to = Compute();
        t = 0f;
        if (BlockforgeTheme.ReduceMotion || !isActiveAndEnabled)
            Snap();
    }

    // Applique l'etat sans transition (outil d'editeur, activation)
    public void RefreshImmediate()
    {
        Cache();
        to = Compute();
        Snap();
        initialized = true;
    }

    // =========================================================
    // CYCLE DE VIE
    // =========================================================

    private void Awake()
    {
        Cache();
        EnsureParts();
        ApplyShape();
    }

    private void OnEnable()
    {
        hovered = pressed = keyboardFocus = false;
        RefreshImmediate();
        SyncShadow();
    }

    private void OnDisable()
    {
        if (shadow != null)
            shadow.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Etat desactive et focus d'un champ : lus a chaque image (pas d'evenement uGUI)
        bool nowDisabled = selectable != null && !selectable.IsInteractable();
        bool nowFieldFocus = inputField != null && inputField.isFocused;
        if (nowDisabled != disabled || nowFieldFocus != fieldFocus)
        {
            disabled = nowDisabled;
            fieldFocus = nowFieldFocus;
            Refresh();
        }

        if (t < 1f)
        {
            float duration = BlockforgeTheme.Duration(BlockforgeTheme.MotionHover);
            t = duration <= 0f ? 1f : Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
            float k = BlockforgeTheme.EaseOut(t);
            current = Lerp(from, to, k);
            Paint(current);
        }
    }

    private void LateUpdate()
    {
        SyncShadow();
    }

    // =========================================================
    // EVENEMENTS
    // =========================================================

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; Refresh(); }

    public void OnPointerDown(PointerEventData eventData)
    {
        lastPointerFrame = Time.frameCount;
        keyboardFocus = false;
        pressed = true;
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData) { pressed = false; Refresh(); }

    // Anneau de focus seulement pour une selection au clavier / a la manette
    public void OnSelect(BaseEventData eventData)
    {
        keyboardFocus = Time.frameCount - lastPointerFrame > 1 && IsInteractive;
        Refresh();
    }

    public void OnDeselect(BaseEventData eventData) { keyboardFocus = false; Refresh(); }

    // =========================================================
    // CALCUL DE L'APPARENCE
    // =========================================================

    private Color ToneColor(DSTone value) => value switch
    {
        DSTone.Success => P.Success,
        DSTone.Warning => P.Warning,
        DSTone.Danger => P.Danger,
        _ => P.Accent,
    };

    private Look Compute()
    {
        var clear = new Color(0, 0, 0, 0);
        var look = new Look
        {
            fill = clear, border = clear, underline = clear,
            ink = P.InkPrimary, icon = P.InkSecondary, key = P.InkMuted,
        };
        bool hot = hovered && !disabled;
        bool down = pressed && hovered && !disabled;

        switch (role)
        {
            case DSRole.Panel:
            case DSRole.PanelFloating:
                look.fill = P.SurfacePanel;
                look.border = P.BorderSubtle;
                break;
            case DSRole.PanelEdge:
                look.fill = P.SurfacePanel;
                look.underline = P.BorderSubtle;
                break;
            case DSRole.Strip:
                look.fill = P.SurfaceRaised;
                look.underline = P.BorderSubtle;
                break;
            case DSRole.Inset:
            case DSRole.Track:
                look.fill = P.SurfaceInset;
                break;
            case DSRole.Fill:
                look.fill = ToneColor(tone);
                break;
            case DSRole.Scrim:
                look.fill = P.Scrim;
                break;
            case DSRole.Dialog:
                look.fill = P.SurfacePanel;
                look.border = P.BorderStrong;
                break;
            case DSRole.ToolRail:
                look.fill = P.SurfaceRaised;
                look.border = P.BorderSubtle;
                break;

            case DSRole.ButtonPrimary:
                look.fill = down ? P.AccentPress : hot ? P.AccentHover : P.Accent;
                look.ink = look.icon = P.InkOnAccent;
                break;
            case DSRole.ButtonSecondary:
                look.fill = down ? P.SurfaceSelected : hot ? P.SurfaceHover : P.AccentSoft;
                look.border = down ? P.AccentPress : hot ? P.Accent : P.BorderStrong;
                look.icon = P.InkPrimary;
                break;
            case DSRole.ButtonGhost:
                look.fill = down ? P.SurfaceSelected : hot ? P.SurfaceHover : clear;
                look.ink = look.icon = hot ? P.InkPrimary : P.InkSecondary;
                break;
            case DSRole.ButtonDanger:
                look.fill = down ? Shade(P.Danger, 0.88f) : hot ? Shade(P.Danger, 1.12f) : P.Danger;
                look.icon = P.InkPrimary;
                break;

            case DSRole.IconButton:
                if (selected)
                {
                    look.fill = P.SurfaceSelected;
                    look.border = P.BorderAccent;
                    look.icon = P.InkPrimary;
                    look.key = P.Accent;
                }
                else
                {
                    look.fill = down ? P.SurfaceSelected : hot ? P.SurfaceHover : clear;
                    look.icon = tone == DSTone.Danger ? P.DangerInk
                        : hot ? P.InkPrimary : P.InkSecondary;
                }
                look.ink = look.icon;
                break;

            case DSRole.TabNav:
                look.fill = !selected && hot ? P.SurfaceHover : clear;
                look.ink = look.icon = selected || hot ? P.InkPrimary : P.InkSecondary;
                look.underline = selected ? P.Accent : clear;
                break;
            case DSRole.TabSegment:
                look.fill = selected ? P.SurfaceSelected : hot ? P.SurfaceHover : clear;
                look.border = selected ? P.BorderAccent : clear;
                look.ink = look.icon = selected || hot ? P.InkPrimary : P.InkSecondary;
                break;

            case DSRole.Field:
                look.fill = P.SurfaceInset;
                look.border = fieldFocus ? P.Accent : P.BorderStrong;
                look.icon = P.InkMuted;
                break;

            case DSRole.RobotSlot:
            case DSRole.PartCard:
            case DSRole.Card:
                if (empty)
                {
                    look.fill = hot ? P.SurfaceHover : P.SurfaceInset;
                    look.border = hot ? P.BorderStrong : P.BorderSubtle;
                    look.ink = look.icon = hot ? P.InkSecondary : P.InkMuted;
                }
                else if (selected)
                {
                    look.fill = P.SurfaceSelected;
                    look.border = P.BorderAccent;
                }
                else
                {
                    look.fill = hot ? P.SurfaceHover : P.SurfacePanel;
                    look.border = hot ? P.BorderStrong : P.BorderSubtle;
                }
                break;

            case DSRole.KeyCap:
                look.fill = selected ? P.SurfaceSelected : P.SurfaceInset;
                look.border = selected ? P.BorderAccent : P.BorderStrong;
                look.ink = P.InkPrimary;
                break;
            case DSRole.Badge:
                look.fill = P.SurfaceInset;
                look.border = P.BorderSubtle;
                look.ink = look.icon = P.InkSecondary;
                break;

            case DSRole.ChipFilter:
                look.fill = selected ? P.SurfaceSelected : hot ? P.SurfaceHover : clear;
                look.border = selected ? P.BorderAccent : P.BorderStrong;
                look.ink = look.icon = selected || hot ? P.InkPrimary : P.InkSecondary;
                break;
            case DSRole.TechNode:
                if (done)
                {
                    look.fill = hot ? P.SurfaceHover : P.AccentSoft;
                    look.border = P.BorderAccent;
                    look.icon = P.Accent;
                }
                else
                {
                    look.fill = hot ? P.SurfaceHover : P.SurfacePanel;
                    look.border = P.BorderStrong;
                    look.icon = P.InkSecondary;
                }
                // Noeud selectionne : bord focus-ring (la selection suit le clavier et la souris)
                if (selected)
                    look.border = P.FocusRing;
                break;
            case DSRole.Stepper:
                look.fill = clear;
                look.border = P.BorderStrong;
                break;
            case DSRole.RowCard:
                look.fill = selected ? P.SurfaceSelected : hot ? P.SurfaceHover : P.SurfacePanel;
                look.border = selected ? P.BorderAccent : hot ? P.BorderStrong : P.BorderSubtle;
                break;

            case DSRole.Notice:
                (look.fill, look.border, look.ink) = tone switch
                {
                    DSTone.Success => (P.SuccessSoft, P.Success, P.Success),
                    DSTone.Warning => (P.WarningSoft, P.Warning, P.Warning),
                    DSTone.Danger => (P.DangerSoft, P.DangerInk, P.DangerInk),
                    _ => (P.AccentSoft, P.Accent, P.Accent),
                };
                look.icon = look.ink;
                break;
        }
        return look;
    }

    private static Color Shade(Color c, float k) => new(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

    private static Look Lerp(Look a, Look b, float k) => new()
    {
        fill = Color.Lerp(a.fill, b.fill, k),
        border = Color.Lerp(a.border, b.border, k),
        ink = Color.Lerp(a.ink, b.ink, k),
        icon = Color.Lerp(a.icon, b.icon, k),
        key = Color.Lerp(a.key, b.key, k),
        underline = Color.Lerp(a.underline, b.underline, k),
    };

    private void Snap()
    {
        current = from = to;
        t = 1f;
        Paint(current);
    }

    private void Paint(Look look)
    {
        if (fill != null)
            fill.color = look.fill;
        if (border != null)
            border.color = look.border;
        if (underline != null)
            underline.color = look.underline;
        foreach (var label in labels)
            if (label != null) label.color = look.ink;
        foreach (var icon in icons)
            if (icon != null) icon.color = look.icon;
        if (keyCorner != null)
            keyCorner.color = look.key;

        if (focusRing != null)
            focusRing.enabled = keyboardFocus && IsInteractive && !disabled;

        if (canvasGroup != null)
            canvasGroup.alpha = disabled ? BlockforgeTheme.OpacityDisabled : 1f;
    }

    // =========================================================
    // PARTIES GENEREES
    // =========================================================

    private void Cache()
    {
        if (fill == null)
            fill = GetComponent<Image>();
        if (selectable == null)
            selectable = GetComponent<Selectable>();
        if (inputField == null)
            inputField = GetComponent<TMP_InputField>();
        if (canvasGroup == null && IsInteractive)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    // Forme du fond (sprite 9-slice selon le rayon) et des parties
    public void ApplyShape()
    {
        Cache();
        var radius = Radius;
        if (fill != null)
        {
            var sprite = DSAssets.Shape(radius, "fill");
            fill.sprite = sprite;
            fill.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            fill.pixelsPerUnitMultiplier = 1f;
            fill.fillCenter = true;
            fill.raycastTarget = IsInteractive || role is DSRole.Scrim || fill.raycastTarget;
        }

        if (border != null)
        {
            bool dashed = empty && role is DSRole.RobotSlot or DSRole.PartCard or DSRole.Card;
            var sprite = dashed ? DSAssets.Shape("lg_dash") : DSAssets.Shape(radius, "stroke");
            border.sprite = sprite;
            border.type = dashed ? Image.Type.Tiled : Image.Type.Sliced;
            border.pixelsPerUnitMultiplier = 1f;
            border.enabled = sprite != null;
        }

        if (focusRing != null)
        {
            focusRing.sprite = DSAssets.Shape(radius == DSRadius.None ? DSRadius.Xs : radius, "ring");
            focusRing.type = Image.Type.Sliced;
            focusRing.color = P.FocusRing;
        }
    }

    // Cree les enfants DS_Border / DS_Focus / DS_Underline et l'ombre si le role en a besoin
    public void EnsureParts()
    {
        Cache();

        if (HasBorder && border == null)
            border = CreatePart("DS_Border", 0f, true);
        if (!HasBorder && border != null)
            DestroyPart(ref border);

        if (IsInteractive && focusRing == null)
        {
            float outset = BlockforgeTheme.FocusOffset + BlockforgeTheme.ControlFocusWidth;
            focusRing = CreatePart("DS_Focus", outset, false);
            focusRing.enabled = false;
        }

        bool needsLine = role is DSRole.TabNav or DSRole.Strip or DSRole.PanelEdge;
        if (needsLine && underline == null)
            underline = CreatePart("DS_Underline", 0f, true);
        if (needsLine)
        {
            var rt = underline.rectTransform;
            bool top = role != DSRole.TabNav && hairlineEdge == DSEdge.Top;
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, role == DSRole.TabNav ? BlockforgeTheme.TopBar.Underline : BlockforgeTheme.ControlBorder);
            rt.anchoredPosition = Vector2.zero;
        }
        if (!needsLine && underline != null)
            DestroyPart(ref underline);

        if (HasShadow && shadow == null && transform.parent != null)
            shadow = CreateShadow();
        if (!HasShadow && shadow != null)
            DestroyPart(ref shadow);

        if (IsInteractive && GetComponent<Selectable>() != null && canvasGroup == null)
            canvasGroup = GetOrAdd<CanvasGroup>(gameObject);
    }

    private Image CreatePart(string partName, float outset, bool first)
    {
        var existing = transform.Find(partName);
        var go = existing != null ? existing.gameObject : new GameObject(partName, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset);
        rt.offsetMax = new Vector2(outset, outset);
        if (first)
            rt.SetSiblingIndex(0);
        var le = GetOrAdd<LayoutElement>(go);
        le.ignoreLayout = true;
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color(1, 1, 1, 0);
        return image;
    }

    private Image CreateShadow()
    {
        string shadowName = "DS_Shadow_" + name;
        var parent = transform.parent;
        var existing = parent.Find(shadowName);
        var go = existing != null ? existing.gameObject : new GameObject(shadowName, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(transform.GetSiblingIndex());
        var le = GetOrAdd<LayoutElement>(go);
        le.ignoreLayout = true;
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        string sprite = role == DSRole.Dialog ? "shadow_overlay" : "shadow_panel";
        image.sprite = DSAssets.Shape(sprite);
        image.type = Image.Type.Sliced;
        image.color = role == DSRole.Dialog ? new Color(0, 0, 0, 0.62f) : new Color(0, 0, 0, 0.42f);
        return image;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private void DestroyPart(ref Image part)
    {
        if (part == null)
            return;
        var go = part.gameObject;
        part = null;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // L'ombre est une soeur placee juste avant : elle suit taille, position et visibilite
    private void SyncShadow()
    {
        if (shadow == null)
            return;
        var rt = (RectTransform)transform;
        var srt = shadow.rectTransform;
        if (srt.parent != rt.parent)
            return;

        bool show = isActiveAndEnabled;
        if (shadow.gameObject.activeSelf != show)
            shadow.gameObject.SetActive(show);
        if (!show)
            return;

        string sprite = role == DSRole.Dialog ? "shadow_overlay" : "shadow_panel";
        float pad = DSAssets.ShadowPadding(sprite);
        float offsetY = role == DSRole.Dialog ? 16f : 2f;
        srt.anchorMin = rt.anchorMin;
        srt.anchorMax = rt.anchorMax;
        srt.pivot = rt.pivot;
        srt.localScale = rt.localScale;
        srt.sizeDelta = rt.sizeDelta + new Vector2(2f * pad, 2f * pad);
        srt.anchoredPosition = rt.anchoredPosition
            + new Vector2(pad * (2f * rt.pivot.x - 1f), pad * (2f * rt.pivot.y - 1f) - offsetY);
        // Juste avant l'element (dessine dessous)
        int index = rt.GetSiblingIndex();
        int shadowIndex = srt.GetSiblingIndex();
        if (shadowIndex != index - 1)
            srt.SetSiblingIndex(shadowIndex < index ? index - 1 : index);
    }
}
