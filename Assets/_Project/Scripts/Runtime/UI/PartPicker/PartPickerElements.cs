using UnityEngine;
using UnityEngine.UIElements;

// =========================================================
// PART PICKER : FORMES DESSINEES (UI Toolkit)
// USS n'a ni box-shadow ni pointilles : ces elements les
// dessinent. Couleurs et mesures viennent de PickerTheme.uss
// (proprietes custom posees sur l'element), jamais du code.
// - PickerShadow : ombre portee douce d'un panneau (--shadow-color,
//   --shadow-offset-y, --shadow-blur) -> fenetre du picker ;
// - DashedOutline : cadre en pointilles (--dash-color,
//   --dash-width, --dash-length, --dash-gap) -> recherche vide.
// Le degrade des vignettes reutilise ForgeGradient (Forge Loader).
// =========================================================

[UxmlElement]
public partial class PickerShadow : VisualElement
{
    private static readonly CustomStyleProperty<Color> ColorProp = new("--shadow-color");
    private static readonly CustomStyleProperty<float> OffsetYProp = new("--shadow-offset-y");
    private static readonly CustomStyleProperty<float> BlurProp = new("--shadow-blur");

    private Color color = Color.clear;
    private float offsetY, blur;

    public PickerShadow()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
    }

    private void OnStyle(CustomStyleResolvedEvent evt)
    {
        var s = evt.customStyle;
        color = s.TryGetValue(ColorProp, out var c) ? c : Color.clear;
        offsetY = s.TryGetValue(OffsetYProp, out var y) ? y : 0f;
        blur = s.TryGetValue(BlurProp, out var b) ? b : 0f;
        MarkDirtyRepaint();
    }

    // Approximation d'un flou gaussien : coeur plein + anneau dont l'alpha
    // descend lineairement a zero sur la largeur du flou (coins en degrade).
    private void Draw(MeshGenerationContext mgc)
    {
        float w = layout.width, h = layout.height;
        if (color.a <= 0f || w <= 0f || h <= 0f)
            return;

        float half = blur * 0.5f;
        var outer = new Rect(-half, offsetY - half, w + blur, h + blur);
        var inner = new Rect(half, offsetY + half, Mathf.Max(0f, w - blur), Mathf.Max(0f, h - blur));
        Color clear = new Color(color.r, color.g, color.b, 0f);

        var mesh = mgc.Allocate(9 * 4, 9 * 6);
        int q = 0;
        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int i = q * 4;
            mesh.SetNextVertex(new Vertex { position = new Vector3(a.x, a.y, Vertex.nearZ), tint = ca });
            mesh.SetNextVertex(new Vertex { position = new Vector3(b.x, b.y, Vertex.nearZ), tint = cb });
            mesh.SetNextVertex(new Vertex { position = new Vector3(c.x, c.y, Vertex.nearZ), tint = cc });
            mesh.SetNextVertex(new Vertex { position = new Vector3(d.x, d.y, Vertex.nearZ), tint = cd });
            mesh.SetNextIndex((ushort)(i + 0));
            mesh.SetNextIndex((ushort)(i + 1));
            mesh.SetNextIndex((ushort)(i + 2));
            mesh.SetNextIndex((ushort)(i + 0));
            mesh.SetNextIndex((ushort)(i + 2));
            mesh.SetNextIndex((ushort)(i + 3));
            q++;
        }

        Vector2 oTL = new(outer.xMin, outer.yMin), oTR = new(outer.xMax, outer.yMin);
        Vector2 oBR = new(outer.xMax, outer.yMax), oBL = new(outer.xMin, outer.yMax);
        Vector2 iTL = new(inner.xMin, inner.yMin), iTR = new(inner.xMax, inner.yMin);
        Vector2 iBR = new(inner.xMax, inner.yMax), iBL = new(inner.xMin, inner.yMax);

        // Coeur
        Quad(iTL, iTR, iBR, iBL, color, color, color, color);
        // Bords
        Quad(new(iTL.x, oTL.y), new(iTR.x, oTR.y), iTR, iTL, clear, clear, color, color);
        Quad(iTR, new(oTR.x, iTR.y), new(oBR.x, iBR.y), iBR, color, clear, clear, color);
        Quad(iBL, iBR, new(iBR.x, oBR.y), new(iBL.x, oBL.y), color, color, clear, clear);
        Quad(new(oTL.x, iTL.y), iTL, iBL, new(oBL.x, iBL.y), clear, color, color, clear);
        // Coins
        Quad(oTL, new(iTL.x, oTL.y), iTL, new(oTL.x, iTL.y), clear, clear, color, clear);
        Quad(new(iTR.x, oTR.y), oTR, new(oTR.x, iTR.y), iTR, clear, clear, clear, color);
        Quad(iBR, new(oBR.x, iBR.y), oBR, new(iBR.x, oBR.y), color, clear, clear, clear);
        Quad(new(oBL.x, iBL.y), iBL, new(iBL.x, oBL.y), oBL, clear, color, clear, clear);
    }
}

[UxmlElement]
public partial class DashedOutline : VisualElement
{
    private static readonly CustomStyleProperty<Color> ColorProp = new("--dash-color");
    private static readonly CustomStyleProperty<float> WidthProp = new("--dash-width");
    private static readonly CustomStyleProperty<float> LengthProp = new("--dash-length");
    private static readonly CustomStyleProperty<float> GapProp = new("--dash-gap");

    private Color color = Color.clear;
    private float width = 1f, length = 6f, gap = 4f;

    public DashedOutline()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
        RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
    }

    private void OnStyle(CustomStyleResolvedEvent evt)
    {
        var s = evt.customStyle;
        color = s.TryGetValue(ColorProp, out var c) ? c : Color.clear;
        width = s.TryGetValue(WidthProp, out var w) ? w : 0f;
        length = s.TryGetValue(LengthProp, out var l) ? l : 0f;
        gap = s.TryGetValue(GapProp, out var g) ? g : 0f;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext mgc)
    {
        float w = layout.width, h = layout.height;
        if (color.a <= 0f || width <= 0f || length <= 0f || w <= 0f || h <= 0f)
            return;

        int across = Count(w), down = Count(h);
        int quads = 2 * across + 2 * down;
        if (quads <= 0 || quads * 4 > ushort.MaxValue)
            return;
        var mesh = mgc.Allocate(quads * 4, quads * 6);
        int q = 0;
        for (int i = 0; i < across; i++)
        {
            float x = Start(w, across) + i * (length + gap);
            float len = Mathf.Min(length, w - x);
            ForgeDraw.GradientQuad(mesh, q++ * 4, new Rect(x, 0f, len, width), color, color, true);
            ForgeDraw.GradientQuad(mesh, q++ * 4, new Rect(x, h - width, len, width), color, color, true);
        }
        for (int j = 0; j < down; j++)
        {
            float y = Start(h, down) + j * (length + gap);
            float len = Mathf.Min(length, h - y);
            ForgeDraw.GradientQuad(mesh, q++ * 4, new Rect(0f, y, width, len), color, color, true);
            ForgeDraw.GradientQuad(mesh, q++ * 4, new Rect(w - width, y, width, len), color, color, true);
        }
    }

    // Tirets centres sur le cote (coins symetriques)
    private int Count(float side) => Mathf.Max(1, Mathf.FloorToInt((side + gap) / (length + gap)));
    private float Start(float side, int count) => Mathf.Max(0f, (side - (count * length + (count - 1) * gap)) * 0.5f);
}
