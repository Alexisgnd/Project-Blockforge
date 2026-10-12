using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// HUD DE MATCH DE LA SCENE DE TEST (design system, theme hud)
// Construit au lancement par RobotTestSpawner, au format du
// composant MatchHud du DS :
// - Reticule au centre (Reticle : quatre arcs de 2 px + point)
// - Sante en bas a gauche (HealthBar : pastille, pourcentage en
//   readout-lg, "Robot health" en label, dix segments inclines ;
//   success > 50 %, warning <= 50 %, danger-ink <= 25 %)
// - Etat du robot en bas a droite : silhouette vue de dessus,
//   vitesse, masse, appuis au sol
// - Minimap en haut a droite (MiniMap)
// Regles du HUD : plaques translucides surface-hud sans bord ni
// ombre, coin exterieur coupe (hud-chamfer), texte avec
// shadow-hud, rien d'autre que le reticule au centre.
// Pas encore de degats : la sante reste a 100 % (SetHealth).
// =========================================================

public class RobotTestHUD : MonoBehaviour
{
    private const float Edge = 16f;   // marge de l'ecran (bf-hud)

    // Texte avec son ombre portee (shadow-hud : 0 1px 2px noir 85 %)
    private class HudLabel
    {
        public TextMeshProUGUI main, shadow;

        public void Set(string value)
        {
            if (main.text == value)
                return;
            main.text = value;
            // L'ombre reste noire : sans les balises de couleur
            shadow.text = System.Text.RegularExpressions.Regex.Replace(value, "</?color[^>]*>", "");
        }

        public void SetColor(Color c) => main.color = c;
    }

    private RobotLocomotion loco;
    private int maxHealth = 1, health = 1;
    private RectTransform root;

    // Sante
    private Image badge, badgeIcon;
    private HudLabel pct;
    private readonly List<Image> segments = new();
    // Etat
    private HudLabel speedValue, massValue, gripValue;
    private float speedShown;

    public void Bind(RobotLocomotion locomotion, string robotName, int maxHp, int weightKg)
    {
        loco = locomotion;
        maxHealth = Mathf.Max(1, maxHp);
        health = maxHealth;
        BuildCanvas();
        BuildReticle();
        BuildHealth();
        BuildStatus(robotName, weightKg);
        var map = gameObject.AddComponent<MiniMap>();
        map.Build(root, locomotion != null ? locomotion.transform : null);
        SetHealth(health);
    }

    public void SetHealth(int value)
    {
        health = Mathf.Clamp(value, 0, maxHealth);
        float ratio = (float)health / maxHealth;
        int percent = Mathf.RoundToInt(ratio * 100f);
        var tone = percent <= 25 ? BlockforgeTheme.Hud.DangerInk : percent <= 50 ? BlockforgeTheme.Hud.Warning : BlockforgeTheme.Hud.Success;
        pct.Set(percent + "%");
        pct.SetColor(percent <= 25 ? BlockforgeTheme.Hud.DangerInk : BlockforgeTheme.Hud.InkPrimary);
        badgeIcon.color = tone;
        int lit = Mathf.RoundToInt(ratio * segments.Count);
        for (int i = 0; i < segments.Count; i++)
            segments[i].color = i < lit ? tone : new Color(0f, 0f, 0f, 0.45f);
    }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void BuildCanvas()
    {
        var go = new GameObject("MatchHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)go.transform;
    }

    private void BuildReticle()
    {
        var shadow = NewImage(root, "ReticleShadow", HudShapes.ReticleShadow(), Color.white);
        Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(64f, 64f));
        var ring = NewImage(root, "Reticle", HudShapes.Reticle(), BlockforgeTheme.Hud.InkPrimary);
        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
    }

    private void BuildHealth()
    {
        // Plaque : padding 8 / 16 / 8 / 8, coin superieur exterieur coupe (bf-health)
        const float w = 258f, h = 60f;
        var plate = Plate(root, "Health", HudCorner.TopRight);
        Place(plate, new Vector2(0f, 0f), new Vector2(Edge, Edge), new Vector2(w, h), new Vector2(0f, 0f));

        badge = NewImage(plate, "Badge", HudShapes.Disc(), BlockforgeTheme.Hud.SurfaceBase);
        Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(44f, 44f), new Vector2(0f, 0.5f));
        badgeIcon = NewImage(badge.rectTransform, "Icon", DSAssets.Icon("health"), BlockforgeTheme.Hud.Success);
        Place(badgeIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

        const float x = 8f + 44f + 12f;
        pct = Label(plate, "Percent", DSTextStyle.ReadoutLG, BlockforgeTheme.Hud.InkPrimary, "100%", TextAlignmentOptions.BottomLeft);
        Place(pct.main.rectTransform, new Vector2(0f, 1f), new Vector2(x, -6f), new Vector2(72f, 28f), new Vector2(0f, 1f));
        AlignShadow(pct);
        var label = Label(plate, "Label", DSTextStyle.Label, BlockforgeTheme.Hud.InkSecondary, "Robot health", TextAlignmentOptions.BottomLeft);
        Place(label.main.rectTransform, new Vector2(0f, 1f), new Vector2(x + 72f + 8f, -10f), new Vector2(110f, 20f), new Vector2(0f, 1f));
        AlignShadow(label);

        for (int i = 0; i < 10; i++)
        {
            var seg = NewImage(plate, "Segment_" + i, HudShapes.Segment(), BlockforgeTheme.Hud.Success);
            Place(seg.rectTransform, new Vector2(0f, 1f), new Vector2(x + i * (16f + 2f), -(6f + 28f + 4f)), new Vector2(16f, 7f), new Vector2(0f, 1f));
            segments.Add(seg);
        }
    }

    private void BuildStatus(string robotName, int weightKg)
    {
        const float w = 252f, h = 150f, pad = 12f, sil = 96f;
        var plate = Plate(root, "RobotStatus", HudCorner.BottomRight);
        Place(plate, new Vector2(1f, 0f), new Vector2(-Edge, Edge), new Vector2(w, h), new Vector2(1f, 0f));

        var name = Label(plate, "Name", DSTextStyle.Label, BlockforgeTheme.Hud.InkSecondary,
                         string.IsNullOrEmpty(robotName) ? "Robot" : robotName, TextAlignmentOptions.TopLeft);
        Place(name.main.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -pad + 2f), new Vector2(w - 2 * pad, 16f), new Vector2(0f, 1f));
        AlignShadow(name);

        // Silhouette vue de dessus (avant du robot en haut)
        var well = NewImage(plate, "Silhouette", null, new Color(0f, 0f, 0f, 0.35f));
        Place(well.rectTransform, new Vector2(0f, 0f), new Vector2(pad, pad), new Vector2(sil, sil), new Vector2(0f, 0f));
        var silhouette = BuildSilhouette(out var aspect);
        if (silhouette != null)
        {
            var raw = new GameObject("Blocks", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(well.transform, false);
            raw.texture = silhouette;
            raw.raycastTarget = false;
            float inner = sil - 12f;
            var size = aspect >= 1f ? new Vector2(inner, inner / aspect) : new Vector2(inner * aspect, inner);
            Place(raw.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }

        // Chiffres : vitesse (readout-lg), masse et appuis (readout-md)
        float x = pad + sil + pad, colW = w - x - pad;
        var speedCaption = Label(plate, "SpeedCaption", DSTextStyle.Caption, BlockforgeTheme.Hud.InkMuted, "Speed", TextAlignmentOptions.TopLeft);
        Place(speedCaption.main.rectTransform, new Vector2(0f, 1f), new Vector2(x, -34f), new Vector2(colW, 16f), new Vector2(0f, 1f));
        AlignShadow(speedCaption);
        speedValue = Label(plate, "Speed", DSTextStyle.ReadoutLG, BlockforgeTheme.Hud.InkPrimary, "0", TextAlignmentOptions.TopLeft);
        Place(speedValue.main.rectTransform, new Vector2(0f, 1f), new Vector2(x, -50f), new Vector2(colW, 28f), new Vector2(0f, 1f));
        AlignShadow(speedValue);

        massValue = StatRow(plate, "Mass", x, -88f, colW);
        massValue.Set(BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(weightKg / 1000f), "t"));
        gripValue = StatRow(plate, "Grip", x, -110f, colW);
    }

    private HudLabel StatRow(RectTransform plate, string caption, float x, float y, float width)
    {
        var c = Label(plate, caption + "Caption", DSTextStyle.Caption, BlockforgeTheme.Hud.InkMuted, caption, TextAlignmentOptions.MidlineLeft);
        Place(c.main.rectTransform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, 18f), new Vector2(0f, 1f));
        AlignShadow(c);
        var v = Label(plate, caption, DSTextStyle.ReadoutMD, BlockforgeTheme.Hud.InkPrimary, "–", TextAlignmentOptions.MidlineRight);
        Place(v.main.rectTransform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, 18f), new Vector2(0f, 1f));
        AlignShadow(v);
        return v;
    }

    // Cases occupees par chaque bloc, vue de dessus ; mouvement plus discret
    private Texture2D BuildSilhouette(out float aspect)
    {
        aspect = 1f;
        if (loco == null)
            return null;
        var cells = new Dictionary<Vector2Int, bool>(); // vrai = structure (prioritaire sur le mouvement)
        foreach (var view in loco.GetComponentsInChildren<PlacedBlockView>())
        {
            if (!loco.TryGetLocalBounds(view.transform, out var b))
                continue;
            bool structure = view.definition == null || view.definition.category != BlockCategory.Mouvement;
            int x0 = Mathf.FloorToInt(b.min.x + 0.05f), x1 = Mathf.CeilToInt(b.max.x - 0.05f);
            int z0 = Mathf.FloorToInt(b.min.z + 0.05f), z1 = Mathf.CeilToInt(b.max.z - 0.05f);
            for (int x = x0; x < Mathf.Max(x1, x0 + 1); x++)
            {
                for (int z = z0; z < Mathf.Max(z1, z0 + 1); z++)
                {
                    var key = new Vector2Int(x, z);
                    cells[key] = (cells.TryGetValue(key, out var s) && s) || structure;
                }
            }
        }
        if (cells.Count == 0)
            return null;

        int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
        foreach (var k in cells.Keys)
        {
            minX = Mathf.Min(minX, k.x); maxX = Mathf.Max(maxX, k.x);
            minZ = Mathf.Min(minZ, k.y); maxZ = Mathf.Max(maxZ, k.y);
        }
        int cw = maxX - minX + 1, ch = maxZ - minZ + 1;
        const int cell = 8;
        var tex = new Texture2D(cw * cell, ch * cell, TextureFormat.RGBA32, false)
        {
            name = "RobotSilhouette",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color32[tex.width * tex.height];
        Color32 solid = BlockforgeTheme.Hud.Success;
        Color32 soft = new Color(BlockforgeTheme.Hud.Success.r, BlockforgeTheme.Hud.Success.g, BlockforgeTheme.Hud.Success.b, 0.5f);
        foreach (var kv in cells)
        {
            int ox = (kv.Key.x - minX) * cell, oy = (kv.Key.y - minZ) * cell;
            for (int y = 1; y < cell; y++)
                for (int x = 1; x < cell; x++)
                    px[(oy + y) * tex.width + ox + x] = kv.Value ? solid : soft;
        }
        tex.SetPixels32(px);
        tex.Apply();
        aspect = (float)cw / ch;
        return tex;
    }

    // =========================================================
    // MISE A JOUR
    // =========================================================

    private void LateUpdate()
    {
        if (loco == null || loco.Body == null)
            return;
        float kmh = loco.Body.linearVelocity.magnitude * 3.6f;
        speedShown = Mathf.Lerp(speedShown, kmh, 1f - Mathf.Exp(-10f * Time.deltaTime));
        speedValue.Set($"{Mathf.RoundToInt(speedShown)}<size=60%><color=#{ColorUtility.ToHtmlStringRGB(BlockforgeTheme.Hud.InkMuted)}> km/h</color></size>");
        gripValue.Set(loco.TotalContacts > 0
            ? $"{loco.GroundedContacts} / {loco.TotalContacts}"
            : (loco.HasRotors ? "Air" : "–"));
    }

    // =========================================================
    // OUTILS
    // =========================================================

    private static RectTransform Plate(RectTransform parent, string name, HudCorner cut)
    {
        var img = NewImage(parent, name, HudShapes.Plate(cut), BlockforgeTheme.Hud.SurfaceHud);
        img.type = Image.Type.Sliced;
        return img.rectTransform;
    }

    public static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = false;
        return img;
    }

    private static HudLabel Label(Transform parent, string name, DSTextStyle style, Color color, string text,
                                  TextAlignmentOptions align)
    {
        var label = new HudLabel
        {
            shadow = NewText(parent, name + "_Shadow", style, new Color(0f, 0f, 0f, 0.85f), text, align),
            main = NewText(parent, name, style, color, text, align),
        };
        return label;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, DSTextStyle style, Color color, string text,
                                           TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        DSText.Apply(t, style, color);
        t.text = text;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    // L'ombre suit le texte, 1 px plus bas (et reste noire malgre les balises de couleur)
    private static void AlignShadow(HudLabel label)
    {
        var m = label.main.rectTransform;
        var s = label.shadow.rectTransform;
        s.anchorMin = m.anchorMin;
        s.anchorMax = m.anchorMax;
        s.pivot = m.pivot;
        s.sizeDelta = m.sizeDelta;
        s.anchoredPosition = m.anchoredPosition + new Vector2(0f, -1f);
        label.shadow.text = System.Text.RegularExpressions.Regex.Replace(label.main.text, "</?color[^>]*>", "");
        label.shadow.alpha = 0.85f;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
