using System;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// PLAQUE CHANFREINEE DU HUD (design system, theme hud)
// Une plaque surface-hud dont un ou plusieurs coins sont coupes
// (hud-chamfer par defaut), dessinee en maillage (HudPolygon),
// jamais avec un carre tourne :
// - Fill : la plaque (surface-hud, ou un fond opaque + surface-hud
//   quand la carte doit etre opaque) ;
// - Tint : degrade d'equipe (plate-ally / plate-enemy / plate-self
//   -> transparent), horizontal ou vertical ;
// - Frame : liseré de 2 px qui SUIT les chanfreins : une seconde
//   forme plus grande derriere la plaque opaque (jamais un
//   contour rectangulaire, jamais visible a travers la plaque).
// Content : RectTransform etire ou poser les enfants.
// =========================================================

[Flags]
public enum PlateCorners
{
    None = 0,
    TopLeft = 1,
    TopRight = 2,
    BottomLeft = 4,
    BottomRight = 8,
    Top = TopLeft | TopRight,
    Bottom = BottomLeft | BottomRight,
    All = Top | Bottom,
}

public sealed class ChamferedPlate : MonoBehaviour
{
    public RectTransform Rect => (RectTransform)transform;
    public RectTransform Content { get; private set; }
    public HudPolygon Fill { get; private set; }
    public HudPolygon Opaque { get; private set; }
    public HudPolygon Tint { get; private set; }
    public HudPolygon Frame { get; private set; }
    public PlateCorners Corners { get; private set; }
    public float Chamfer { get; private set; }

    public static ChamferedPlate Build(UIKit kit, Transform parent, string name, PlateCorners corners, float chamfer = BlockforgeTheme.HudChamfer,
                                       Color? fill = null, Color? tint = null, bool tintVertical = false, bool opaque = false,
                                       Color? frame = null, float frameWidth = BlockforgeTheme.ControlFocusWidth)
    {
        var rt = kit.Rect(parent, name);
        var plate = rt.gameObject.AddComponent<ChamferedPlate>();
        plate.Corners = corners;
        plate.Chamfer = chamfer;

        if (frame.HasValue)
        {
            // Forme parallele plus grande de frameWidth : un chanfrein a 45 degres
            // decale de w s'allonge de w (racine 2 - 1) sur chaque axe
            float outer = chamfer + frameWidth * (Mathf.Sqrt(2f) - 1f);
            plate.Frame = kit.Polygon(rt, "Frame");
            UIKit.Stretch(plate.Frame.rectTransform, -frameWidth, -frameWidth, -frameWidth, -frameWidth);
            Apply(plate.Frame, corners, outer);
            plate.Frame.SetSolid(frame.Value);
            UIKit.IgnoreLayout(plate.Frame);
            opaque = true; // la couleur du lisere ne doit jamais transparaitre
        }

        if (opaque)
        {
            plate.Opaque = kit.Polygon(rt, "Opaque");
            UIKit.Stretch(plate.Opaque.rectTransform);
            Apply(plate.Opaque, corners, chamfer);
            plate.Opaque.SetSolid(kit.P.SurfaceBase);
            UIKit.IgnoreLayout(plate.Opaque);
        }

        plate.Fill = kit.Polygon(rt, "Fill");
        UIKit.Stretch(plate.Fill.rectTransform);
        Apply(plate.Fill, corners, chamfer);
        plate.Fill.SetSolid(fill ?? kit.P.SurfaceHud);
        UIKit.IgnoreLayout(plate.Fill);

        if (tint.HasValue)
        {
            plate.Tint = kit.Polygon(rt, "Tint");
            UIKit.Stretch(plate.Tint.rectTransform);
            Apply(plate.Tint, corners, chamfer);
            plate.Tint.vertical = tintVertical;
            plate.Tint.SetColors(tint.Value, BlockforgeTheme.WithAlpha(tint.Value, 0f));
            UIKit.IgnoreLayout(plate.Tint);
        }

        plate.Content = kit.Rect(rt, "Content");
        UIKit.Stretch(plate.Content);
        UIKit.IgnoreLayout(plate.Content);
        return plate;
    }

    public void SetTint(Color color)
    {
        if (Tint != null)
            Tint.SetColors(color, BlockforgeTheme.WithAlpha(color, 0f));
    }

    public void SetFill(Color color)
    {
        if (Fill != null)
            Fill.SetSolid(color);
    }

    public void SetFrame(Color color)
    {
        if (Frame != null)
            Frame.SetSolid(color);
    }

    private static void Apply(HudPolygon poly, PlateCorners corners, float cut)
    {
        poly.cutTopLeft = (corners & PlateCorners.TopLeft) != 0 ? cut : 0f;
        poly.cutTopRight = (corners & PlateCorners.TopRight) != 0 ? cut : 0f;
        poly.cutBottomLeft = (corners & PlateCorners.BottomLeft) != 0 ? cut : 0f;
        poly.cutBottomRight = (corners & PlateCorners.BottomRight) != 0 ? cut : 0f;
        poly.SetVerticesDirty();
    }

    // Trapeze de jauge : bord droit incline (barre d'XP)
    public static HudPolygon Trapezoid(UIKit kit, Transform parent, string name, Color color, float slant)
    {
        var poly = kit.Polygon(parent, name);
        poly.cutBottomRight = 0f;
        poly.slantRight = slant;
        poly.SetSolid(color);
        return poly;
    }
}
