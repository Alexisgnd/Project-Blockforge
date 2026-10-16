using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// HUD DE MATCH DE LA SCENE DE TEST (design system, theme hud)
// Construit au lancement par RobotTestSpawner, au format du
// composant MatchHud du DS :
// - Reticule au centre (Reticle : quatre arcs de 2 px + point),
//   entoure de la jauge d'energie des armes (RobotEnergy) : anneau
//   coupe a la verticale, chaque moitie se remplit du bas vers le
//   haut ; warning sous 25 %, clignote en danger sur un tir refuse
// - Sante en bas a gauche (HealthBar : pastille, pourcentage en
//   readout-lg, "Robot health" en label, dix segments inclines ;
//   success > 50 %, warning <= 50 %, danger-ink <= 25 %)
// - Etat du robot en bas a droite : silhouette vue de dessus,
//   vitesse, masse, appuis au sol
// - Minimap en haut a droite (MiniMap)
// - Armes et modules en bas a droite, a gauche de l'etat du
//   robot (ActionSlot du DS) : un slot par type monte
//   (RobotLoadoutState, 3 au plus), touche 1 / 2 / 3 dans un
//   cabochon sous la tuile ; le slot actif passe en accent, son
//   nom et sa quantite s'affichent au-dessus de la rangee.
//   Rien n'est affiche si le robot n'a ni arme ni module.
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
    // Energie (anneau autour du reticule)
    private Image energyLeft, energyRight;
    private RobotEnergy energy;
    private float energyShown = 1f, refusedFlash;
    // Armes et modules
    private RobotLoadoutState loadout;
    private readonly List<(Image tile, Image icon, bool tinted, HudLabel count, HudLabel key)> slotViews = new();
    private HudLabel slotName;

    public void Bind(RobotLocomotion locomotion, string robotName, int maxHp, int weightKg)
    {
        loco = locomotion;
        maxHealth = Mathf.Max(1, maxHp);
        health = maxHealth;
        BuildCanvas();
        BuildReticle();
        BuildHealth();
        BuildStatus(robotName, weightKg);
        BuildSlots(locomotion != null ? locomotion.GetComponent<RobotLoadoutState>() : null);
        energy = locomotion != null ? locomotion.GetComponent<RobotEnergy>() : null;
        if (energy != null)
            energy.Refused += () => refusedFlash = 1f;
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
        BuildEnergy();
    }

    // Jauge d'energie des armes autour du reticule : un anneau coupe a la
    // verticale, chaque moitie se remplit du bas vers le haut (gauche dans le sens
    // horaire, droite dans l'autre) ; fond sombre, accent par-dessus
    private void BuildEnergy()
    {
        const int radius = 27, thickness = 3, gap = 16;
        var sprite = HudShapes.Ring(radius, thickness, gap);
        float size = (radius + thickness + 2) * 2f;
        var back = NewImage(root, "EnergyBack", sprite, new Color(0f, 0f, 0f, 0.55f));
        Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(size, size));
        var dim = NewImage(root, "EnergyDim", sprite, new Color(1f, 1f, 1f, 0.22f));
        Place(dim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        energyLeft = NewImage(root, "EnergyLeft", sprite, BlockforgeTheme.Hud.Accent);
        Place(energyLeft.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        energyRight = NewImage(root, "EnergyRight", sprite, BlockforgeTheme.Hud.Accent);
        Place(energyRight.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        foreach (var half in new[] { energyLeft, energyRight })
        {
            half.type = Image.Type.Filled;
            half.fillMethod = Image.FillMethod.Radial360;
            half.fillOrigin = (int)Image.Origin360.Bottom;
            half.fillAmount = 0.5f;
        }
        energyLeft.fillClockwise = true;    // depuis le bas, le sens horaire monte par la gauche
        energyRight.fillClockwise = false;
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

    // ---------- Armes et modules (ActionSlot) ----------

    private void BuildSlots(RobotLoadoutState state)
    {
        loadout = state;
        if (state == null || state.slots.Count == 0)
            return;

        const float tile = BlockforgeTheme.HudSlot, gap = 8f, keyH = 16f, keyGap = 3f;
        float right = Edge + 252f + BlockforgeTheme.Space4;   // a gauche de la plaque d'etat
        int n = state.slots.Count;
        float rowW = n * tile + (n - 1) * gap;

        for (int i = 0; i < n; i++)
        {
            var slot = state.slots[i];
            float x = -right - rowW + i * (tile + gap);   // depuis le bord droit, slot 1 a gauche

            // Tuile : surface-hud, coin bas exterieur coupe ; accent quand active
            var tileImg = NewImage(root, "Slot_" + (i + 1), HudShapes.Plate(HudCorner.BottomLeft), BlockforgeTheme.Hud.SurfaceHud);
            tileImg.type = Image.Type.Sliced;
            Place(tileImg.rectTransform, new Vector2(1f, 0f), new Vector2(x, Edge + keyH + keyGap), new Vector2(tile, tile), new Vector2(0f, 0f));

            bool hasIcon = slot.definition.icon != null;
            var icon = NewImage(tileImg.rectTransform, "Icon",
                                hasIcon ? slot.definition.icon : DSAssets.Icon(RobotLoadout.KindIcon(slot.kind)),
                                hasIcon ? Color.white : BlockforgeTheme.Hud.InkPrimary);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), hasIcon ? new Vector2(44f, 44f) : new Vector2(24f, 24f));

            var count = Label(tileImg.rectTransform, "Count", DSTextStyle.ReadoutSM, BlockforgeTheme.Hud.InkPrimary,
                              "×" + slot.Count, TextAlignmentOptions.BottomRight);
            Place(count.main.rectTransform, new Vector2(1f, 0f), new Vector2(-4f, 2f), new Vector2(tile - 8f, 14f), new Vector2(1f, 0f));
            AlignShadow(count);

            // Cabochon de la touche sous la tuile
            var keyPlate = NewImage(root, "Key_" + (i + 1), null, BlockforgeTheme.Hud.SurfaceHud);
            Place(keyPlate.rectTransform, new Vector2(1f, 0f), new Vector2(x + tile * 0.5f, Edge), new Vector2(20f, keyH), new Vector2(0.5f, 0f));
            var key = Label(keyPlate.rectTransform, "Label", DSTextStyle.Key, BlockforgeTheme.Hud.InkPrimary,
                            (i + 1).ToString(), TextAlignmentOptions.Center);
            Place(key.main.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, keyH));
            AlignShadow(key);

            slotViews.Add((tileImg, icon, !hasIcon, count, key));
        }

        // Nom du slot actif au-dessus de la rangee
        slotName = Label(root, "SlotName", DSTextStyle.Label, BlockforgeTheme.Hud.InkPrimary, "", TextAlignmentOptions.BottomRight);
        Place(slotName.main.rectTransform, new Vector2(1f, 0f), new Vector2(-right, Edge + keyH + keyGap + tile + 6f),
              new Vector2(Mathf.Max(rowW, 260f), 18f), new Vector2(1f, 0f));
        AlignShadow(slotName);

        state.SelectedChanged += _ => RefreshSlots();
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (loadout == null)
            return;
        for (int i = 0; i < slotViews.Count; i++)
        {
            bool active = i == loadout.SelectedIndex;
            var view = slotViews[i];
            view.tile.color = active ? BlockforgeTheme.Hud.Accent : BlockforgeTheme.Hud.SurfaceHud;
            if (view.tinted)
                view.icon.color = active ? BlockforgeTheme.Hud.InkOnAccent : BlockforgeTheme.Hud.InkPrimary;
            view.count.SetColor(active ? BlockforgeTheme.Hud.InkOnAccent : BlockforgeTheme.Hud.InkPrimary);
            view.count.shadow.enabled = !active;   // pas d'ombre sur l'accent (DS)
            view.key.SetColor(active ? BlockforgeTheme.Hud.Accent : BlockforgeTheme.Hud.InkPrimary);
        }
        var slot = loadout.Selected;
        if (slot != null && slotName != null)
        {
            string muted = ColorUtility.ToHtmlStringRGB(BlockforgeTheme.Hud.InkMuted);
            slotName.Set($"{slot.definition.blockName} <color=#{muted}>· {RobotLoadout.KindLabel(slot.kind)} ×{slot.Count}</color>");
        }
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
        UpdateEnergy();
    }

    private void UpdateEnergy()
    {
        if (energy == null || energyLeft == null)
            return;
        energyShown = Mathf.Lerp(energyShown, energy.Fraction, 1f - Mathf.Exp(-14f * Time.deltaTime));
        refusedFlash = Mathf.MoveTowards(refusedFlash, 0f, Time.deltaTime * 4f);
        float fill = energyShown * 0.5f;
        energyLeft.fillAmount = fill;
        energyRight.fillAmount = fill;
        Color tone = energy.Fraction <= 0.25f ? BlockforgeTheme.Hud.Warning : BlockforgeTheme.Hud.Accent;
        if (refusedFlash > 0f)
            tone = Color.Lerp(tone, BlockforgeTheme.Hud.DangerInk, Mathf.PingPong(refusedFlash * 2f, 1f));
        energyLeft.color = tone;
        energyRight.color = tone;
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
