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
// - Etat du robot en bas a droite : vitesse, masse, appuis au
//   sol, et la heatmap des blocs vue de dessus (RobotHealth :
//   vert / jaune / rouge, rouge sombre + croix rouge vif si
//   detruit), qui ne
//   s'affiche que robot abime, et 3 s apres le retour a 100 %
// - Robot ennemi (CombatArena) en haut au centre : nom, vie en
//   segments danger et sa propre heatmap, tant qu'il est en vie
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
// La sante vient de RobotHealth (HealthChanged -> SetHealth) ; les impacts
// (Hit) allument une vignette rouge en forme d'oeil sur les bords de l'ecran
// et un arc autour du reticule qui pointe vers la source (et suit la camera).
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
    private int maxHealth = 1, health_hp = 1;
    private RectTransform root;

    // Sante
    private Image badge, badgeIcon;
    private HudLabel pct;
    private readonly List<Image> segments = new();
    // Etat
    private HudLabel speedValue, massValue, gripValue;
    private float speedShown;
    private RectTransform statusPlate, statsColumn, silhouetteWell;
    private float statusFullWidth, statusCompactWidth, statsFullX, statsCompactX;
    // Heatmap des blocs (vue de dessus) : visible quand le robot est abime
    private RobotHealth health;
    private readonly RobotHeatmap heatmap = new();
    private bool heatVisible = true;
    // Robot ennemi (zone de combat) : panneau en haut au centre
    private RectTransform enemyPlate;
    private HudLabel enemyName, enemyPct;
    private readonly List<Image> enemySegments = new();
    private RawImage enemyMap;
    private readonly RobotHeatmap enemyHeatmap = new();
    private RobotHealth enemyHealth;
    private GameObject enemyShown;
    private CombatArena arena;
    private float nextArenaLookup;
    // Degats subis : vignette "oeil rouge" et arcs vers la source
    private Image vignette;
    private float vignetteLevel;
    private readonly List<(Image arc, Vector3 point, float born, float life)> hitArcs = new();
    private readonly Stack<Image> arcPool = new();
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
        health = locomotion != null ? locomotion.GetComponent<RobotHealth>() : null;
        maxHealth = Mathf.Max(1, maxHp);
        health_hp = maxHealth;
        BuildCanvas();
        BuildVignette();
        BuildReticle();
        BuildHealth();
        BuildStatus(robotName, weightKg);
        BuildSlots(locomotion != null ? locomotion.GetComponent<RobotLoadoutState>() : null);
        energy = locomotion != null ? locomotion.GetComponent<RobotEnergy>() : null;
        if (energy != null)
            energy.Refused += () => refusedFlash = 1f;
        if (health != null)
        {
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(health.MaxHp));
            this.health_hp = Mathf.RoundToInt(health.Hp);
            health.HealthChanged += () => SetHealth(Mathf.RoundToInt(health.Hp));
            health.BlockChanged += _ => heatmap.Dirty = true;
            health.Hit += OnHit;
        }
        SetHeatVisible(false, true);
        var map = gameObject.AddComponent<MiniMap>();
        map.Build(root, locomotion != null ? locomotion.transform : null);
        SetHealth(health_hp);
    }

    public void SetHealth(int value)
    {
        health_hp = Mathf.Clamp(value, 0, maxHealth);
        float ratio = (float)health_hp / maxHealth;
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

    // ---------- Degats subis ----------

    // Vignette rouge en forme d'oeil sur tout l'ecran (sous le reste du HUD)
    private void BuildVignette()
    {
        vignette = NewImage(root, "DamageVignette", HudShapes.Vignette(), BlockforgeTheme.Hud.DangerInk);
        var rt = vignette.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        vignette.color = new Color(vignette.color.r, vignette.color.g, vignette.color.b, 0f);
        vignette.raycastTarget = false;
        vignette.transform.SetAsFirstSibling();
    }

    // Impact subi : la vignette monte (selon les degats) et un arc autour du
    // reticule pointe vers la source ; un impact proche d'un arc en cours le
    // ravive au lieu d'en ajouter un
    private void OnHit(Vector3 point, float amount)
    {
        if (health == null || health.IsDead)
            return;
        float strength = Mathf.Clamp01(0.3f + amount / Mathf.Max(1f, health.MaxHp * 0.08f));
        vignetteLevel = Mathf.Max(vignetteLevel, strength);
        if (loco == null)
            return;
        float angle = HitAngle(point);
        for (int i = 0; i < hitArcs.Count; i++)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angle, HitAngle(hitArcs[i].point))) < 25f)
            {
                hitArcs[i] = (hitArcs[i].arc, point, Time.time, 1.1f);
                return;
            }
        }
        var arc = arcPool.Count > 0 ? arcPool.Pop() : NewHitArc();
        arc.gameObject.SetActive(true);
        hitArcs.Add((arc, point, Time.time, 1.1f));
    }

    private Image NewHitArc()
    {
        var arc = NewImage(root, "HitArc", HudShapes.Arc(46, 6, 70), BlockforgeTheme.Hud.DangerInk);
        Place(arc.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(108f, 108f));
        arc.raycastTarget = false;
        return arc;
    }

    // Angle ecran (degres, 0 = devant, positif = a droite) entre la direction de
    // la camera et la source : l'arc tourne quand on bouge tant qu'il est affiche
    private float HitAngle(Vector3 point)
    {
        var cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : loco.transform.forward;
        forward.y = 0f;
        Vector3 to = point - loco.transform.position;
        to.y = 0f;
        if (forward.sqrMagnitude < 1e-4f || to.sqrMagnitude < 1e-4f)
            return 0f;
        return Vector3.SignedAngle(forward.normalized, to.normalized, Vector3.up);
    }

    private void UpdateDamageFX()
    {
        if (vignette != null)
        {
            vignetteLevel = Mathf.MoveTowards(vignetteLevel, 0f, Time.deltaTime * 1.4f);
            var c = vignette.color;
            float shown = BlockforgeTheme.ReduceMotion ? vignetteLevel * 0.6f : vignetteLevel;
            vignette.color = new Color(c.r, c.g, c.b, shown * 0.6f);
            vignette.enabled = shown > 0.005f;
        }
        for (int i = hitArcs.Count - 1; i >= 0; i--)
        {
            var (arc, point, born, life) = hitArcs[i];
            float t = (Time.time - born) / life;
            if (t >= 1f)
            {
                arc.gameObject.SetActive(false);
                arcPool.Push(arc);
                hitArcs.RemoveAt(i);
                continue;
            }
            arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -HitAngle(point));
            float a = t < 0.15f ? t / 0.15f : 1f - Mathf.InverseLerp(0.45f, 1f, t);
            var c = arc.color;
            arc.color = new Color(c.r, c.g, c.b, a);
            arc.rectTransform.localScale = Vector3.one * (1f + 0.12f * (1f - Mathf.Clamp01(t / 0.2f)));
        }
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

        // Heatmap des blocs vue de dessus (avant du robot en haut) : un puits qui
        // n'apparait que quand le robot est abime (SetHeatVisible)
        var well = NewImage(plate, "Silhouette", null, new Color(0f, 0f, 0f, 0.35f));
        Place(well.rectTransform, new Vector2(0f, 0f), new Vector2(pad, pad), new Vector2(sil, sil), new Vector2(0f, 0f));
        silhouetteWell = well.rectTransform;
        if (heatmap.Build(loco, health))
            HeatImage(well.transform, heatmap, sil - 12f);

        // Chiffres : vitesse (readout-lg), masse et appuis (readout-md), dans une
        // colonne qui glisse a gauche quand la heatmap est cachee
        float x = pad + sil + pad, colW = w - x - pad;
        statusPlate = plate;
        statusFullWidth = w;
        statusCompactWidth = pad + colW + pad;
        statsFullX = x;
        statsCompactX = pad;
        statsColumn = new GameObject("Stats", typeof(RectTransform)).GetComponent<RectTransform>();
        statsColumn.SetParent(plate, false);
        Place(statsColumn, new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(colW, h), new Vector2(0f, 1f));
        var speedCaption = Label(statsColumn, "SpeedCaption", DSTextStyle.Caption, BlockforgeTheme.Hud.InkMuted, "Speed", TextAlignmentOptions.TopLeft);
        Place(speedCaption.main.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -34f), new Vector2(colW, 16f), new Vector2(0f, 1f));
        AlignShadow(speedCaption);
        speedValue = Label(statsColumn, "Speed", DSTextStyle.ReadoutLG, BlockforgeTheme.Hud.InkPrimary, "0", TextAlignmentOptions.TopLeft);
        Place(speedValue.main.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -50f), new Vector2(colW, 28f), new Vector2(0f, 1f));
        AlignShadow(speedValue);

        massValue = StatRow(statsColumn, "Mass", 0f, -88f, colW);
        massValue.Set(BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(weightKg / 1000f), "t"));
        gripValue = StatRow(statsColumn, "Grip", 0f, -110f, colW);
    }

    // Heatmap visible : robot abime, ou guerison terminee depuis moins de 3 s
    private void SetHeatVisible(bool visible, bool force = false)
    {
        if (!force && visible == heatVisible)
            return;
        heatVisible = visible;
        if (silhouetteWell != null)
            silhouetteWell.gameObject.SetActive(visible);
        if (statusPlate != null)
            statusPlate.sizeDelta = new Vector2(visible ? statusFullWidth : statusCompactWidth, statusPlate.sizeDelta.y);
        if (statsColumn != null)
            statsColumn.anchoredPosition = new Vector2(visible ? statsFullX : statsCompactX, 0f);
    }

    private void UpdateHeat()
    {
        if (health == null || heatmap.Texture == null)
            return;
        bool show = health.Damaged || Time.time - health.LastFullTime < 3f;
        SetHeatVisible(show);
        if (show && heatmap.Dirty)
            heatmap.Redraw();
    }

    // RawImage d'une heatmap, ajustee dans un carre de 'inner' px
    private static RawImage HeatImage(Transform parent, RobotHeatmap map, float inner)
    {
        var raw = new GameObject("Blocks", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        raw.transform.SetParent(parent, false);
        raw.texture = map.Texture;
        raw.raycastTarget = false;
        var size = map.Aspect >= 1f ? new Vector2(inner, inner / map.Aspect) : new Vector2(inner * map.Aspect, inner);
        Place(raw.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return raw;
    }

    // ---------- Robot ennemi (zone de combat) ----------

    // Plaque en haut au centre : nom, pourcentage, dix segments, heatmap des blocs ;
    // construite quand un robot ennemi existe, visible seulement quand le joueur
    // est dans la zone de combat et l'ennemi vivant
    private void BuildEnemyPlate()
    {
        const float w = 300f, h = 76f, pad = 10f, map = 56f;
        enemyPlate = Plate(root, "EnemyStatus", HudCorner.BottomLeft);
        Place(enemyPlate, new Vector2(0.5f, 1f), new Vector2(0f, -Edge), new Vector2(w, h), new Vector2(0.5f, 1f));

        var well = NewImage(enemyPlate, "Silhouette", null, new Color(0f, 0f, 0f, 0.35f));
        Place(well.rectTransform, new Vector2(0f, 0.5f), new Vector2(pad, 0f), new Vector2(map, map), new Vector2(0f, 0.5f));
        enemyMap = HeatImage(well.transform, enemyHeatmap, map - 8f);

        float x = pad + map + pad, colW = w - x - pad;
        enemyName = Label(enemyPlate, "Name", DSTextStyle.Label, BlockforgeTheme.Hud.DangerInk, "Enemy", TextAlignmentOptions.TopLeft);
        Place(enemyName.main.rectTransform, new Vector2(0f, 1f), new Vector2(x, -pad + 2f), new Vector2(colW - 60f, 16f), new Vector2(0f, 1f));
        AlignShadow(enemyName);
        enemyPct = Label(enemyPlate, "Percent", DSTextStyle.ReadoutMD, BlockforgeTheme.Hud.InkPrimary, "100%", TextAlignmentOptions.TopRight);
        Place(enemyPct.main.rectTransform, new Vector2(1f, 1f), new Vector2(-pad, -pad + 2f), new Vector2(60f, 18f), new Vector2(1f, 1f));
        AlignShadow(enemyPct);
        enemySegments.Clear();
        for (int i = 0; i < 10; i++)
        {
            var seg = NewImage(enemyPlate, "Segment_" + i, HudShapes.Segment(), BlockforgeTheme.Hud.DangerInk);
            Place(seg.rectTransform, new Vector2(0f, 1f), new Vector2(x + i * (16f + 2f), -(pad + 24f)), new Vector2(16f, 7f), new Vector2(0f, 1f));
            enemySegments.Add(seg);
        }
        var caption = Label(enemyPlate, "Caption", DSTextStyle.Caption, BlockforgeTheme.Hud.InkMuted, "Enemy robot", TextAlignmentOptions.TopLeft);
        Place(caption.main.rectTransform, new Vector2(0f, 1f), new Vector2(x, -(pad + 36f)), new Vector2(colW, 16f), new Vector2(0f, 1f));
        AlignShadow(caption);
    }

    private void UpdateEnemy()
    {
        if (Time.time >= nextArenaLookup)
        {
            nextArenaLookup = Time.time + 1f;
            if (arena == null)
                arena = FindAnyObjectByType<CombatArena>();
        }
        var enemy = arena != null ? arena.Enemy : null;
        if (enemy != enemyShown)
        {
            enemyShown = enemy;
            enemyHealth = enemy != null ? enemy.GetComponent<RobotHealth>() : null;
            if (enemyHealth != null)
            {
                if (enemyPlate == null)
                    BuildEnemyPlate();
                enemyHealth.BlockChanged += _ => enemyHeatmap.Dirty = true;
                enemyHeatmap.Build(enemy.GetComponent<RobotLocomotion>(), enemyHealth);
                if (enemyMap != null)
                {
                    enemyMap.texture = enemyHeatmap.Texture;
                    float inner = 56f - 8f;
                    enemyMap.rectTransform.sizeDelta = enemyHeatmap.Aspect >= 1f
                        ? new Vector2(inner, inner / enemyHeatmap.Aspect)
                        : new Vector2(inner * enemyHeatmap.Aspect, inner);
                }
                enemyName.Set(enemy.name);
            }
        }
        if (enemyPlate == null)
            return;
        // Visible seulement quand le joueur est dans la zone rouge (et l'ennemi vivant)
        bool inZone = arena != null && arena.Player != null && arena.Contains(arena.Player.transform.position);
        bool show = enemyHealth != null && !enemyHealth.IsDead && inZone;
        if (enemyPlate.gameObject.activeSelf != show)
            enemyPlate.gameObject.SetActive(show);
        if (!show)
            return;
        if (enemyHeatmap.Dirty)
            enemyHeatmap.Redraw();
        float ratio = enemyHealth.Fraction;
        int percent = Mathf.RoundToInt(ratio * 100f);
        enemyPct.Set(percent + "%");
        int lit = Mathf.RoundToInt(ratio * enemySegments.Count);
        for (int i = 0; i < enemySegments.Count; i++)
            enemySegments[i].color = i < lit ? BlockforgeTheme.Hud.DangerInk : new Color(0f, 0f, 0f, 0.45f);
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
        UpdateHeat();
        UpdateEnemy();
        UpdateDamageFX();
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
