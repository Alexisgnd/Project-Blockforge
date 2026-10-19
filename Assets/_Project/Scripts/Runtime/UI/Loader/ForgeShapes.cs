using UnityEngine;
using UnityEngine.UIElements;

// =========================================================
// FORMES DESSINEES DU FORGE LOADER (UI Toolkit)
// UI Toolkit n'a ni coins coupes ni degrades : ces elements les
// dessinent eux-memes. Couleurs et mesures viennent toujours de
// proprietes USS (LoaderTheme.uss), jamais du code :
// - ChamferPanel : fond plein a coins coupes (--chamfer-fill,
//   --chamfer-top-left / -top-right / -bottom-right / -bottom-left)
//   -> plaque du chargement d'ecran, sieges du matchmaking ;
// - ForgeBar : barre de progression 4 px (--bar-track, --bar-fill,
//   --bar-chamfer : bout droit du remplissage coupe) ;
// - ForgeGradient : degrade 2 ou 3 arrets, vertical ou horizontal
//   (--gradient-start, --gradient-mid, --gradient-end) -> fond du
//   splash, scrim et liseré du bandeau de matchmaking ;
// - ForgeGrid : quadrillage (--grid-size, --grid-color, --grid-line).
// =========================================================

[UxmlElement]
public partial class ChamferPanel : VisualElement
{
    private static readonly CustomStyleProperty<Color> FillProp = new("--chamfer-fill");
    private static readonly CustomStyleProperty<float> TopLeftProp = new("--chamfer-top-left");
    private static readonly CustomStyleProperty<float> TopRightProp = new("--chamfer-top-right");
    private static readonly CustomStyleProperty<float> BottomRightProp = new("--chamfer-bottom-right");
    private static readonly CustomStyleProperty<float> BottomLeftProp = new("--chamfer-bottom-left");

    private Color fill = Color.clear;
    private float tl, tr, br, bl;

    public ChamferPanel()
    {
        generateVisualContent += Draw;
        RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
    }

    private void OnStyle(CustomStyleResolvedEvent evt)
    {
        var s = evt.customStyle;
        fill = s.TryGetValue(FillProp, out var c) ? c : Color.clear;
        tl = s.TryGetValue(TopLeftProp, out var a) ? a : 0f;
        tr = s.TryGetValue(TopRightProp, out var b) ? b : 0f;
        br = s.TryGetValue(BottomRightProp, out var d) ? d : 0f;
        bl = s.TryGetValue(BottomLeftProp, out var e) ? e : 0f;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext mgc)
    {
        var r = new Rect(0f, 0f, layout.width, layout.height);
        if (fill.a <= 0f || r.width <= 0f || r.height <= 0f)
            return;
        ForgeDraw.ChamferRect(mgc.painter2D, r, fill, tl, tr, br, bl);
    }
}

[UxmlElement]
public partial class ForgeBar : VisualElement
{
    private static readonly CustomStyleProperty<Color> TrackProp = new("--bar-track");
    private static readonly CustomStyleProperty<Color> FillProp = new("--bar-fill");
    private static readonly CustomStyleProperty<float> ChamferProp = new("--bar-chamfer");

    private Color track = Color.clear, fill = Color.white;
    private float chamfer;
    private float value;

    [UxmlAttribute("value")]
    public float Value
    {
        get => value;
        set
        {
            float v = Mathf.Clamp01(value);
            if (Mathf.Approximately(v, this.value))
                return;
            this.value = v;
            MarkDirtyRepaint();
        }
    }

    public ForgeBar()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
    }

    private void OnStyle(CustomStyleResolvedEvent evt)
    {
        var s = evt.customStyle;
        if (s.TryGetValue(TrackProp, out var t)) track = t;
        if (s.TryGetValue(FillProp, out var f)) fill = f;
        chamfer = s.TryGetValue(ChamferProp, out var c) ? c : 0f;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext mgc)
    {
        float w = layout.width, h = layout.height;
        if (w <= 0f || h <= 0f)
            return;
        var p = mgc.painter2D;
        ForgeDraw.ChamferRect(p, new Rect(0f, 0f, w, h), track, 0f, 0f, 0f, 0f);

        float fw = w * value;
        if (fw <= 0.5f)
            return;
        // Bout droit coupe : le haut va jusqu'a fw, le bas s'arrete chamfer plus tot
        float cut = Mathf.Min(chamfer, fw);
        p.fillColor = fill;
        p.BeginPath();
        p.MoveTo(new Vector2(0f, 0f));
        p.LineTo(new Vector2(fw, 0f));
        p.LineTo(new Vector2(fw - cut, h));
        p.LineTo(new Vector2(0f, h));
        p.ClosePath();
        p.Fill();
    }
}

public enum GradientDirection { Vertical, Horizontal }

[UxmlElement]
public partial class ForgeGradient : VisualElement
{
    private static readonly CustomStyleProperty<Color> StartProp = new("--gradient-start");
    private static readonly CustomStyleProperty<Color> MidProp = new("--gradient-mid");
    private static readonly CustomStyleProperty<Color> EndProp = new("--gradient-end");

    private Color start = Color.clear, end = Color.clear, mid;
    private bool hasMid;

    [UxmlAttribute("direction")]
    public GradientDirection Direction { get; set; } = GradientDirection.Vertical;

    public ForgeGradient()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
    }

    private void OnStyle(CustomStyleResolvedEvent evt)
    {
        var s = evt.customStyle;
        if (s.TryGetValue(StartProp, out var a)) start = a;
        if (s.TryGetValue(EndProp, out var b)) end = b;
        hasMid = s.TryGetValue(MidProp, out mid);
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext mgc)
    {
        var r = new Rect(0f, 0f, layout.width, layout.height);
        if (r.width <= 0f || r.height <= 0f)
            return;
        bool vertical = Direction == GradientDirection.Vertical;
        if (!hasMid)
        {
            var mesh = mgc.Allocate(4, 6);
            ForgeDraw.GradientQuad(mesh, 0, r, start, end, vertical);
            return;
        }

        // Arret central transparent (#6BA2FF -> transparent -> #FF6F5E) : chaque moitie
        // s'efface dans sa propre teinte, sans virer au gris
        Color midA = mid.a <= 0f ? new Color(start.r, start.g, start.b, 0f) : mid;
        Color midB = mid.a <= 0f ? new Color(end.r, end.g, end.b, 0f) : mid;
        Rect first = vertical ? new Rect(r.x, r.y, r.width, r.height * 0.5f) : new Rect(r.x, r.y, r.width * 0.5f, r.height);
        Rect second = vertical ? new Rect(r.x, r.y + r.height * 0.5f, r.width, r.height * 0.5f) : new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, r.height);
        var m = mgc.Allocate(8, 12);
        ForgeDraw.GradientQuad(m, 0, first, start, midA, vertical);
        ForgeDraw.GradientQuad(m, 4, second, midB, end, vertical);
    }
}

[UxmlElement]
public partial class ForgeGrid : VisualElement
{
    private static readonly CustomStyleProperty<float> SizeProp = new("--grid-size");
    private static readonly CustomStyleProperty<Color> ColorProp = new("--grid-color");
    private static readonly CustomStyleProperty<float> LineProp = new("--grid-line");

    private float cell, line = 1f;
    private Color color = Color.clear;

    public ForgeGrid()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
        RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
        RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
    }

    private void OnStyle(CustomStyleResolvedEvent evt)
    {
        var s = evt.customStyle;
        cell = s.TryGetValue(SizeProp, out var c) ? c : 0f;
        line = s.TryGetValue(LineProp, out var l) ? l : 1f;
        color = s.TryGetValue(ColorProp, out var col) ? col : Color.clear;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext mgc)
    {
        float w = layout.width, h = layout.height;
        if (cell < 4f || color.a <= 0f || w <= 0f || h <= 0f)
            return;

        // Grille centree : une ligne passe par le centre de l'ecran, quel que soit le format
        float ox = Mathf.Repeat(w * 0.5f, cell), oy = Mathf.Repeat(h * 0.5f, cell);
        int cols = Mathf.FloorToInt((w - ox) / cell) + 1;
        int rows = Mathf.FloorToInt((h - oy) / cell) + 1;
        int quads = cols + rows;
        var mesh = mgc.Allocate(quads * 4, quads * 6);
        int q = 0;
        for (int i = 0; i < cols; i++, q++)
            ForgeDraw.GradientQuad(mesh, q * 4, new Rect(ox + i * cell - line * 0.5f, 0f, line, h), color, color, true);
        for (int j = 0; j < rows; j++, q++)
            ForgeDraw.GradientQuad(mesh, q * 4, new Rect(0f, oy + j * cell - line * 0.5f, w, line), color, color, true);
    }
}

// Primitives communes
public static class ForgeDraw
{
    public static void ChamferRect(Painter2D p, Rect r, Color fill, float tl, float tr, float br, float bl)
    {
        if (fill.a <= 0f)
            return;
        float max = Mathf.Min(r.width, r.height) * 0.5f;
        tl = Mathf.Clamp(tl, 0f, max);
        tr = Mathf.Clamp(tr, 0f, max);
        br = Mathf.Clamp(br, 0f, max);
        bl = Mathf.Clamp(bl, 0f, max);

        p.fillColor = fill;
        p.BeginPath();
        p.MoveTo(new Vector2(r.xMin + tl, r.yMin));
        p.LineTo(new Vector2(r.xMax - tr, r.yMin));
        if (tr > 0f) p.LineTo(new Vector2(r.xMax, r.yMin + tr));
        p.LineTo(new Vector2(r.xMax, r.yMax - br));
        if (br > 0f) p.LineTo(new Vector2(r.xMax - br, r.yMax));
        p.LineTo(new Vector2(r.xMin + bl, r.yMax));
        if (bl > 0f) p.LineTo(new Vector2(r.xMin, r.yMax - bl));
        p.LineTo(new Vector2(r.xMin, r.yMin + tl));
        p.ClosePath();
        p.Fill();
    }

    // Quad a couleurs par sommet (degrade lineaire), indices dans le sens horaire
    public static void GradientQuad(MeshWriteData mesh, int baseIndex, Rect r, Color from, Color to, bool vertical)
    {
        Color tlc = from, trc = vertical ? from : to, brc = to, blc = vertical ? to : from;
        mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMin, Vertex.nearZ), tint = tlc });
        mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMin, Vertex.nearZ), tint = trc });
        mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMax, Vertex.nearZ), tint = brc });
        mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMax, Vertex.nearZ), tint = blc });
        mesh.SetNextIndex((ushort)(baseIndex + 0));
        mesh.SetNextIndex((ushort)(baseIndex + 1));
        mesh.SetNextIndex((ushort)(baseIndex + 2));
        mesh.SetNextIndex((ushort)(baseIndex + 0));
        mesh.SetNextIndex((ushort)(baseIndex + 2));
        mesh.SetNextIndex((ushort)(baseIndex + 3));
    }
}
