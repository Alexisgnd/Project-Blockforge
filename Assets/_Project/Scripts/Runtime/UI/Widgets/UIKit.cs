using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =========================================================
// BOITE A OUTILS DE CONSTRUCTION D'UI (uGUI, design system)
// Les ecrans de fin de match et du hub se construisent en code
// au lancement, comme le HUD et MatchIntro : pas de prefab a
// maintenir, et chaque ecran tourne dans une scene vide.
// UIKit porte le theme (dark / hud) : toute couleur vient de
// BlockforgeTheme.Palette, toute taille des jetons ou de
// BlockforgeTheme.Layout. Aucune valeur en dur dans les ecrans.
// =========================================================

public sealed class UIKit
{
    public readonly DSTheme Theme;
    public readonly BlockforgeTheme.Palette P;

    public UIKit(DSTheme theme)
    {
        Theme = theme;
        P = BlockforgeTheme.PaletteFor(theme);
    }

    public static readonly UIKit Dark = new(DSTheme.Dark);
    public static readonly UIKit Hud = new(DSTheme.Hud);

    private static int uiLayer = -1;
    public static int UILayer => uiLayer >= 0 ? uiLayer : (uiLayer = Mathf.Max(0, LayerMask.NameToLayer("UI")));

    // =========================================================
    // RECTANGLES ET PLACEMENT
    // =========================================================

    public RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UILayer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    public static void Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // Ancre et pivot identiques (coin ou centre), position relative a cette ancre
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    // Bande horizontale pleine largeur ancree en haut (y = distance au bord haut)
    public static void TopStrip(RectTransform rt, float y, float height, float left = 0f, float right = 0f)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -y - height);
        rt.offsetMax = new Vector2(-right, -y);
    }

    public static void BottomStrip(RectTransform rt, float y, float height, float left = 0f, float right = 0f)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(left, y);
        rt.offsetMax = new Vector2(-right, y + height);
    }

    public static Vector2 TopLeft => new(0f, 1f);
    public static Vector2 TopRight => new(1f, 1f);
    public static Vector2 TopCenter => new(0.5f, 1f);
    public static Vector2 BottomLeft => new(0f, 0f);
    public static Vector2 BottomRight => new(1f, 0f);
    public static Vector2 BottomCenter => new(0.5f, 0f);
    public static Vector2 Center => new(0.5f, 0.5f);
    public static Vector2 MiddleLeft => new(0f, 0.5f);
    public static Vector2 MiddleRight => new(1f, 0.5f);

    // =========================================================
    // LAYOUTS
    // =========================================================

    public RectTransform Row(Transform parent, string name, float spacing, TextAnchor align = TextAnchor.MiddleLeft,
                             RectOffset padding = null, bool controlWidth = true, bool controlHeight = true, bool expandHeight = false)
    {
        var rt = Rect(parent, name);
        var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = align;
        if (padding != null) layout.padding = padding;
        layout.childControlWidth = controlWidth;
        layout.childControlHeight = controlHeight;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = expandHeight;
        return rt;
    }

    public RectTransform Column(Transform parent, string name, float spacing, TextAnchor align = TextAnchor.UpperLeft,
                                RectOffset padding = null, bool controlWidth = true, bool controlHeight = true, bool expandWidth = true)
    {
        var rt = Rect(parent, name);
        var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = align;
        if (padding != null) layout.padding = padding;
        layout.childControlWidth = controlWidth;
        layout.childControlHeight = controlHeight;
        layout.childForceExpandWidth = expandWidth;
        layout.childForceExpandHeight = false;
        return rt;
    }

    public RectTransform Grid(Transform parent, string name, int columns, Vector2 cell, Vector2 spacing, TextAnchor align = TextAnchor.UpperLeft)
    {
        var rt = Rect(parent, name);
        var grid = rt.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = cell;
        grid.spacing = spacing;
        grid.childAlignment = align;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        return rt;
    }

    public static RectOffset Pad(float all) => new((int)all, (int)all, (int)all, (int)all);
    public static RectOffset Pad(float x, float y) => new((int)x, (int)x, (int)y, (int)y);
    public static RectOffset Pad(float left, float right, float top, float bottom) => new((int)left, (int)right, (int)top, (int)bottom);

    public static LayoutElement Fixed(Component c, float width = -1f, float height = -1f)
    {
        var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
        if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
        return le;
    }

    public static LayoutElement Flexible(Component c, float flexWidth = 1f, float flexHeight = 0f, float minWidth = 0f)
    {
        var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = flexWidth;
        le.flexibleHeight = flexHeight;
        le.minWidth = minWidth;
        return le;
    }

    public static void IgnoreLayout(Component c)
    {
        var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
    }

    public RectTransform Spacer(Transform parent, float flex = 1f)
    {
        var rt = Rect(parent, "Spacer");
        Flexible(rt, flex, flex);
        return rt;
    }

    public RectTransform Gap(Transform parent, float size)
    {
        var rt = Rect(parent, "Gap");
        Fixed(rt, size, size);
        return rt;
    }

    public static ContentSizeFitter Fit(Component c, bool horizontal, bool vertical)
    {
        var fitter = c.gameObject.GetComponent<ContentSizeFitter>() ?? c.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        return fitter;
    }

    // =========================================================
    // IMAGES, FORMES ET PICTOGRAMMES
    // =========================================================

    public Image Image(Transform parent, string name, Color color, Sprite sprite = null)
    {
        var rt = Rect(parent, name);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        }
        return image;
    }

    // Rectangle arrondi plein (9-slice du DS)
    public Image Shape(Transform parent, string name, DSRadius radius, Color color) =>
        Image(parent, name, color, DSAssets.Shape(radius, "fill"));

    // Contour 1 px arrondi
    public Image Stroke(Transform parent, string name, DSRadius radius, Color color) =>
        Image(parent, name, color, DSAssets.Shape(radius, "stroke"));

    public Image Icon(Transform parent, string name, string icon, float size, Color color)
    {
        var image = Image(parent, name, color, DSAssets.Icon(icon));
        image.type = UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        Fixed(image, size, size);
        return image;
    }

    public HudPolygon Polygon(Transform parent, string name)
    {
        var rt = Rect(parent, name);
        rt.gameObject.AddComponent<CanvasRenderer>();
        var poly = rt.gameObject.AddComponent<HudPolygon>();
        poly.raycastTarget = false;
        return poly;
    }

    // Filet 1 px horizontal
    public Image Hairline(Transform parent, string name, Color color)
    {
        var image = Image(parent, name, color);
        Fixed(image, -1f, BlockforgeTheme.ControlBorder);
        return image;
    }

    public CanvasGroup Group(Component c)
    {
        var group = c.gameObject.GetComponent<CanvasGroup>();
        return group != null ? group : c.gameObject.AddComponent<CanvasGroup>();
    }

    // =========================================================
    // TEXTES
    // =========================================================

    public TextMeshProUGUI Text(Transform parent, string name, DSTextStyle style, Color color, string value,
                                TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false)
    {
        var rt = Rect(parent, name);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        DSText.Apply(text, style, color);
        text.text = value;
        text.alignment = align;
        text.raycastTarget = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        return text;
    }

    public TextMeshProUGUI Text(Transform parent, string name, TypeSpec spec, Color color, string value,
                                TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool caps = false, bool wrap = false)
    {
        var rt = Rect(parent, name);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        DSText.Apply(text, spec, color, caps);
        text.text = value;
        text.alignment = align;
        text.raycastTarget = false;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        if (wrap)
            text.lineSpacing = 0f;
        return text;
    }

    // Chiffre en mono (tout nombre que le joueur compare)
    public TextMeshProUGUI Mono(Transform parent, string name, float size, int weight, Color color, string value,
                                TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft) =>
        Text(parent, name, new TypeSpec(FontFamily.Mono, size, size, weight, 0f), color, value, align);

    // Over-line en capitales
    public TextMeshProUGUI Caps(Transform parent, string name, float size, int weight, float trackingEm, Color color, string value,
                                TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft) =>
        Text(parent, name, new TypeSpec(FontFamily.Sans, size, size, weight, trackingEm), color, value, align, true);

    // Titre condense en capitales
    public TextMeshProUGUI Condensed(Transform parent, string name, float size, int weight, float trackingEm, Color color, string value,
                                     TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft) =>
        Text(parent, name, new TypeSpec(FontFamily.Condensed, size, size, weight, trackingEm), color, value, align, true);

    // shadow-hud (0 1px 2px noir 85 %) : underlay du shader TMP sur une instance du materiau ;
    // soft = ombre douce supplementaire (verdict de fin de match)
    public static void HudShadow(TMP_Text text, bool soft = false)
    {
        if (text == null)
            return;
        var spec = BlockforgeTheme.ShadowHud;
        var mat = text.fontMaterial;
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, spec.Color);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, soft ? 0.25f : 0.1f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, soft ? 0.6f : 0.35f);
    }

    public static void Ellipsis(TMP_Text text)
    {
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    // Largeur preferee d'un texte (apres mise en forme)
    public static float PreferredWidth(TMP_Text text) => text == null ? 0f : text.GetPreferredValues(text.text).x;

    // =========================================================
    // ELEMENTS DU DS
    // =========================================================

    public DSElement Element(Component target, DSRole role, TMP_Text[] labels = null, Graphic[] icons = null, DSTone tone = DSTone.Neutral)
    {
        var go = target.gameObject;
        if (go.GetComponent<Image>() == null)
        {
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
        }
        var element = go.GetComponent<DSElement>() ?? go.AddComponent<DSElement>();
        element.role = role;
        element.theme = Theme;
        element.tone = tone;
        element.labels = labels ?? new TMP_Text[0];
        element.icons = icons ?? new Graphic[0];
        element.EnsureParts();
        element.ApplyShape();
        element.RefreshImmediate();
        return element;
    }

    // Bouton uGUI sans teinte (les etats sont portes par DSElement)
    public static Button Clickable(Component target)
    {
        var button = target.gameObject.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.colors = BlockforgeTheme.ButtonStates();
        var image = target.gameObject.GetComponent<Image>();
        if (image != null)
        {
            button.targetGraphic = image;
            image.raycastTarget = true;
        }
        return button;
    }

    // Focus clavier / manette : selectionne sans anneau si le pointeur a ete utilise
    public static void Focus(Selectable selectable)
    {
        if (selectable == null || EventSystem.current == null)
            return;
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    // =========================================================
    // CANVAS D'ECRAN
    // =========================================================

    // Canvas plein ecran a l'echelle du DS (1920 x 1080, echelle sur la hauteur)
    public static Canvas ScreenCanvas(GameObject host, int sortingOrder)
    {
        var canvas = host.GetComponent<Canvas>() ?? host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = host.GetComponent<CanvasScaler>() ?? host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(BlockforgeTheme.ReferenceWidth, BlockforgeTheme.ReferenceHeight);
        scaler.matchWidthOrHeight = 1f; // echelle sur la hauteur : rien ne coupe en 21:9
        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // EventSystem (Input System) si la scene n'en a pas
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        Object.DontDestroyOnLoad(go);
    }
}

// =========================================================
// ANIMATIONS (ease-out, temps non scale) : coroutines reutilisables
// =========================================================

public static class UIAnim
{
    public static float Ease(float t) => BlockforgeTheme.EaseOut(Mathf.Clamp01(t));

    public static IEnumerator Fade(CanvasGroup group, float to, float duration)
    {
        if (group == null)
            yield break;
        float from = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (group == null) yield break;
            group.alpha = Mathf.Lerp(from, to, Ease(t / duration));
            yield return null;
        }
        if (group != null) group.alpha = to;
    }

    // Fondu + glissement vertical (dy px, de la position d'arrivee) : rien ne bouge en animations reduites
    public static IEnumerator FadeSlide(CanvasGroup group, RectTransform rect, float fromDy, float fadeDuration)
    {
        if (group == null || rect == null)
            yield break;
        float slide = BlockforgeTheme.Duration(fadeDuration);
        float fade = BlockforgeTheme.Fade(fadeDuration);
        var target = rect.anchoredPosition;
        if (slide <= 0f)
            fromDy = 0f;
        group.alpha = 0f;
        rect.anchoredPosition = target + new Vector2(0f, fromDy);
        float t = 0f;
        float duration = Mathf.Max(fade, slide);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (group == null || rect == null) yield break;
            group.alpha = fade <= 0f ? 1f : Mathf.Clamp01(Ease(t / fade));
            rect.anchoredPosition = target + new Vector2(0f, fromDy * (1f - Ease(slide <= 0f ? 1f : t / slide)));
            yield return null;
        }
        group.alpha = 1f;
        rect.anchoredPosition = target;
    }

    public static IEnumerator Scale(RectTransform rect, float from, float to, float duration)
    {
        if (rect == null)
            yield break;
        duration = BlockforgeTheme.Duration(duration);
        if (duration <= 0f)
        {
            rect.localScale = Vector3.one * to;
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (rect == null) yield break;
            rect.localScale = Vector3.one * Mathf.Lerp(from, to, Ease(t / duration));
            yield return null;
        }
        rect.localScale = Vector3.one * to;
    }

    // Compteur : appelle setter(valeur) de 0 a target ; immediat en animations reduites
    public static IEnumerator CountUp(long target, float duration, System.Action<long> setter)
    {
        duration = BlockforgeTheme.Duration(duration);
        if (duration <= 0f)
        {
            setter(target);
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            setter((long)Mathf.Round(target * Ease(t / duration)));
            yield return null;
        }
        setter(target);
    }

    public static IEnumerator Lerp(float duration, System.Action<float> setter)
    {
        duration = BlockforgeTheme.Duration(duration);
        if (duration <= 0f)
        {
            setter(1f);
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            setter(Ease(t / duration));
            yield return null;
        }
        setter(1f);
    }

    public static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
