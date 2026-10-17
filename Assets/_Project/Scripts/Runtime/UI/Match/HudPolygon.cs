using UnityEngine;
using UnityEngine.UI;

// =========================================================
// POLYGONE DU HUD (design system, theme hud)
// Un Graphic uGUI qui dessine un quadrilatere plein aux coins
// coupes (hud-chamfer) et/ou aux bords inclines, avec un
// degrade a deux couleurs. Remplace les clip-path et les
// linear-gradient du design :
// - plaque a coin exterieur coupe : cutTopLeft = 14 ;
// - volet de transition a bord a 12 degres : slantRight = 230
//   (le bord droit est a x = largeur en haut, largeur - 230 en bas) ;
// - couture inclinee de l'ecran Versus : slantLeft = slantRight ;
// - barre de chargement en trapeze : cutBottomLeft = cutBottomRight.
// Les couleurs : colorA -> colorB de gauche a droite (ou de haut
// en bas avec vertical), multipliees par la couleur du Graphic
// (qui sert donc de fondu via CanvasGroup / color.a).
// =========================================================

[AddComponentMenu("Blockforge/HUD Polygon")]
[RequireComponent(typeof(CanvasRenderer))]
public class HudPolygon : MaskableGraphic
{
    [Header("Coins coupes (px)")]
    public float cutTopLeft;
    public float cutTopRight;
    public float cutBottomRight;
    public float cutBottomLeft;

    [Header("Bords inclines (px de decalage entre le haut et le bas)")]
    [Tooltip("Bord gauche : x = slantLeft en haut, 0 en bas")]
    public float slantLeft;
    [Tooltip("Bord droit : x = largeur en haut, largeur - slantRight en bas")]
    public float slantRight;

    [Header("Degrade")]
    public Color colorA = Color.white;
    public Color colorB = Color.white;
    [Tooltip("false : A -> B de gauche a droite ; true : A en haut, B en bas")]
    public bool vertical;

    public void SetColors(Color a, Color b)
    {
        colorA = a;
        colorB = b;
        SetVerticesDirty();
    }

    public void SetSolid(Color c) => SetColors(c, c);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        float x0 = r.xMin, x1 = r.xMax, y0 = r.yMin, y1 = r.yMax;
        float w = Mathf.Max(0.001f, x1 - x0), h = Mathf.Max(0.001f, y1 - y0);

        float LeftX(float y) => x0 + slantLeft * (y - y0) / h;
        float RightX(float y) => x1 - slantRight * (1f - (y - y0) / h);

        // Sommets dans le sens horaire a partir du coin haut-gauche
        var pts = new System.Collections.Generic.List<Vector2>(8);
        if (cutTopLeft > 0f)
        {
            pts.Add(new Vector2(LeftX(y1 - cutTopLeft), y1 - cutTopLeft));
            pts.Add(new Vector2(LeftX(y1) + cutTopLeft, y1));
        }
        else pts.Add(new Vector2(LeftX(y1), y1));

        if (cutTopRight > 0f)
        {
            pts.Add(new Vector2(RightX(y1) - cutTopRight, y1));
            pts.Add(new Vector2(RightX(y1 - cutTopRight), y1 - cutTopRight));
        }
        else pts.Add(new Vector2(RightX(y1), y1));

        if (cutBottomRight > 0f)
        {
            pts.Add(new Vector2(RightX(y0 + cutBottomRight), y0 + cutBottomRight));
            pts.Add(new Vector2(RightX(y0) - cutBottomRight, y0));
        }
        else pts.Add(new Vector2(RightX(y0), y0));

        if (cutBottomLeft > 0f)
        {
            pts.Add(new Vector2(LeftX(y0) + cutBottomLeft, y0));
            pts.Add(new Vector2(LeftX(y0 + cutBottomLeft), y0 + cutBottomLeft));
        }
        else pts.Add(new Vector2(LeftX(y0), y0));

        var tint = color;
        for (int i = 0; i < pts.Count; i++)
        {
            float k = vertical ? 1f - (pts[i].y - y0) / h : (pts[i].x - x0) / w;
            var c = Color.Lerp(colorA, colorB, Mathf.Clamp01(k)) * tint;
            vh.AddVert(pts[i], c, new Vector2((pts[i].x - x0) / w, (pts[i].y - y0) / h));
        }
        // Eventail depuis le premier sommet (polygone convexe)
        for (int i = 1; i + 1 < pts.Count; i++)
            vh.AddTriangle(0, i, i + 1);
    }
}
