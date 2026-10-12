using System.Collections.Generic;
using UnityEngine;

// =========================================================
// FORMES DU HUD DE MATCH (design system, theme hud)
// Sprites generes une fois, en memoire :
// - Plaque a coin coupe (hud-chamfer = 10 px) : 9-slice, le coin
//   coupe garde sa taille quelle que soit la plaque
// - Segment incline de la HealthBar (16 x 7 px, pente de 2 px)
// - Reticule : quatre arcs de 2 px ouverts aux diagonales et un
//   point central (rayon 17 sur 64 px), avec son ombre portee
// - Disque (pastille de la HealthBar)
// Rendus a 4x puis affiches a la taille du DS : bords nets.
// =========================================================

public enum HudCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public static class HudShapes
{
    public const float Chamfer = 10f;          // hud-chamfer (px de reference)
    private const int Scale = 4;               // sur-echantillonnage des textures

    private static readonly Dictionary<HudCorner, Sprite> plates = new();
    private static Sprite segment, reticle, reticleShadow, disc;

    // ---------- Plaque a coin coupe ----------

    public static Sprite Plate(HudCorner corner)
    {
        if (plates.TryGetValue(corner, out var cached) && cached != null)
            return cached;

        int border = Mathf.CeilToInt(Chamfer + 2) * Scale; // 48 px
        int size = border * 2 + 4;
        float cut = Chamfer * Scale;
        var tex = NewTexture(size, size, "HudPlate_" + corner);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance au coin concerne (coordonnees texture : y vers le haut)
                float cx = corner == HudCorner.TopLeft || corner == HudCorner.BottomLeft ? x + 0.5f : size - x - 0.5f;
                float cy = corner == HudCorner.BottomLeft || corner == HudCorner.BottomRight ? y + 0.5f : size - y - 0.5f;
                // Dans le triangle coupe si cx + cy < cut ; transition sur 1 texel (anticrenelage)
                float d = (cx + cy - cut) / 1.41421356f;
                byte a = (byte)(Mathf.Clamp01(d + 0.5f) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * Scale, 0,
                                   SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = tex.name;
        plates[corner] = sprite;
        return sprite;
    }

    // ---------- Segment incline (HealthBar) ----------

    public static Sprite Segment()
    {
        if (segment != null)
            return segment;
        int w = 16 * Scale, h = 7 * Scale;
        float slant = 2f * Scale;
        var tex = NewTexture(w, h, "HudSegment");
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            // Parallelogramme : (2,0)-(16,0)-(14,7)-(0,7) en px CSS (y vers le bas)
            float t = 1f - (y + 0.5f) / h;      // 0 en bas (CSS 100%), 1 en haut (CSS 0)
            float left = slant * t;
            float right = w - slant * (1f - t);
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f;
                float a = Mathf.Clamp01(fx - left + 0.5f) * Mathf.Clamp01(right - fx + 0.5f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        segment = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f * Scale);
        segment.name = tex.name;
        return segment;
    }

    // ---------- Reticule (DS Reticle, etat idle) ----------

    public static Sprite Reticle() => reticle != null ? reticle : (reticle = BuildReticle(false));

    public static Sprite ReticleShadow() => reticleShadow != null ? reticleShadow : (reticleShadow = BuildReticle(true));

    private static Sprite BuildReticle(bool shadow)
    {
        int size = 64 * Scale;
        var tex = NewTexture(size, size, shadow ? "HudReticleShadow" : "HudReticle");
        var px = new Color32[size * size];
        float c = size * 0.5f;
        float ringR = 17f * Scale, half = 1f * Scale + (shadow ? 0.5f * Scale : 0f);
        float dotR = 1.6f * Scale + (shadow ? 0.5f * Scale : 0f);
        float[][] segs = { new[] { -80f, -10f }, new[] { 10f, 80f }, new[] { 100f, 170f }, new[] { 190f, 260f } };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = -(y + 0.5f - c);  // angles a la SVG (y vers le bas)
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float a = 0f;
                float ring = Mathf.Clamp01(half - Mathf.Abs(r - ringR) + 0.5f);
                if (ring > 0f)
                {
                    foreach (var s in segs)
                    {
                        float a0 = s[0], a1 = s[1];
                        float aa = ang;
                        while (aa < a0) aa += 360f;
                        while (aa > a0 + 360f) aa -= 360f;
                        // bout droit (strokeLinecap butt) : distance angulaire convertie en px
                        float inside = Mathf.Min(aa - a0, a1 - aa) * Mathf.Deg2Rad * r;
                        a = Mathf.Max(a, ring * Mathf.Clamp01(inside + 0.5f));
                    }
                }
                a = Mathf.Max(a, Mathf.Clamp01(dotR - r + 0.5f));
                px[y * size + x] = shadow
                    ? new Color32(0, 0, 0, (byte)(a * 0.85f * 255f))
                    : new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * Scale);
        sprite.name = tex.name;
        return sprite;
    }

    // ---------- Disque ----------

    public static Sprite Disc()
    {
        if (disc != null)
            return disc;
        int size = 44 * Scale;
        var tex = NewTexture(size, size, "HudDisc");
        var px = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float a = Mathf.Clamp01(c - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f * Scale);
        disc.name = tex.name;
        return disc;
    }

    private static Texture2D NewTexture(int w, int h, string name)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, true)
        {
            name = name,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
    }
}
