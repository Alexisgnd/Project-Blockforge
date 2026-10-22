using System.Collections;
using System.Collections.Generic;
using Blockforge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// HUD DE COMBAT DU MATCH PAR EQUIPES (composant MatchHud du DS,
// theme hud) — complete RobotTestHUD (sante, reticule, energie,
// slots d'armes, cree avec le robot du joueur) :
// - TeamRoster en haut a gauche (equipe du joueur, plate-ally)
//   et en haut a droite (ennemis, plate-enemy, en miroir) : une
//   ligne de 26 px par joueur, badge sur le bord exterieur (disque
//   allie / losange ennemi ; user + plate-self pour le joueur ;
//   skull + opacity-ghost pour un robot detruit), nom (ellipse),
//   kills en mono. Ordre de l'ecran Versus, jamais retrie.
// - ScoreBar en haut au centre, mode deathmatch : deux barres
//   chanfreinees qui se remplissent des bords exterieurs vers le
//   centre (team-ally a gauche, team-enemy a droite, toujours),
//   compteur dans la barre, plaque centrale crane + objectif,
//   chrono en mono dessous.
// - NamePlate au-dessus de chaque robot visible : silhouette
//   d'equipe, nom (13 px, ombre hud), distance en mono, ligne de
//   vie de 2 px ; sans plaque ; echelle selon la distance (jamais
//   sous 12 px) ; cachee derriere le relief, au-dela de 220 m
//   (ennemis) ou 400 m (allies), hors ecran.
// - Reticule au-dessus d'un allie : RobotTestHUD.SetReticleAlly.
// - Killfeed sous le roster ennemi : TdmKillfeed.
// Cache pendant le compte a rebours, apparait en 140 ms au
// depart (TdmMatch.PhaseChanged). Masque en entier par
// MatchEndFlow en fin de match (hudRoot).
// =========================================================

public class TdmHud : MonoBehaviour
{
    private const float Edge = 16f;               // bf-hud : 16 px du bord
    private const float RosterWidth = 250f, RowHeight = 26f, RowGap = 3f;
    private const float BarWidth = 150f, BarHeight = 14f, MidWidth = 72f, DmHeight = 26f, DmWidth = 60f;

    private TdmMatch match;
    private readonly UIKit kit = UIKit.Hud;
    private RectTransform root;
    private CanvasGroup combatGroup;

    // ---------- Rosters ----------
    private sealed class RosterRow
    {
        public TdmParticipant p;
        public CanvasGroup group;
        public Image badgeIcon;
        public TextMeshProUGUI name, score;
        public bool down;
        public int kills = -1;
    }
    private readonly List<RosterRow> rows = new();

    // ---------- Score ----------
    private RectTransform allyFill, enemyFill;
    private HudPolygon allyFillPoly, enemyFillPoly;
    private TextMeshProUGUI allyNum, enemyNum, targetNum, clock;
    private int shownAlly = -1, shownEnemy = -1, shownSeconds = -1;

    // ---------- Plaques ----------
    private sealed class Plate
    {
        public TdmParticipant p;
        public RectTransform rt;
        public CanvasGroup group;
        public TextMeshProUGUI name, dist;
        public Image mark;
        public RectTransform fill;
        public float rowWidth;
        public bool occluded;
        public float nextOcclusion;
        public int shownMeters = -1;
        public bool ally;
    }
    private readonly List<Plate> plates = new();
    private RectTransform platesLayer;
    private Camera cam;
    private static readonly RaycastHit[] aimHits = new RaycastHit[12];

    public static TdmHud Create(TdmMatch match)
    {
        var go = new GameObject("[TdmHud]", typeof(RectTransform));
        var hud = go.AddComponent<TdmHud>();
        hud.match = match;
        hud.Build();
        return hud;
    }

    private void Build()
    {
        UIKit.ScreenCanvas(gameObject, BlockforgeTheme.Order.Hud + 1);
        GetComponent<GraphicRaycaster>().enabled = false;   // rien a cliquer : le curseur est verrouille
        root = (RectTransform)transform;

        var combat = kit.Rect(root, "Combat");
        UIKit.Stretch(combat);
        combatGroup = kit.Group(combat);
        combatGroup.alpha = 0f;
        combatGroup.interactable = false;
        combatGroup.blocksRaycasts = false;

        platesLayer = kit.Rect(combat, "NamePlates");
        UIKit.Stretch(platesLayer);

        BuildRoster(combat, true);
        BuildRoster(combat, false);
        BuildScoreBar(combat);
        BuildPlates();
        // Qui tue qui : bord droit, sous le roster ennemi (TdmKillfeed)
        TdmKillfeed.Create(kit, combat, match);

        match.PhaseChanged += OnPhase;
    }

    private void OnDestroy()
    {
        if (match != null)
            match.PhaseChanged -= OnPhase;
    }

    private void OnPhase(TdmPhase phase)
    {
        if (phase == TdmPhase.Live)
            StartCoroutine(UIAnim.Fade(combatGroup, 1f, BlockforgeTheme.Fade(BlockforgeTheme.MotionPanel)));
    }

    // Texte du HUD : ombre portee shadow-hud
    private TextMeshProUGUI HudText(Transform parent, string name, FontFamily family, float size, int weight, Color color, string value,
                                    TextAlignmentOptions align, bool caps = false, float tracking = 0f)
    {
        var t = kit.Text(parent, name, new TypeSpec(family, size, size, weight, tracking), color, value, align, caps);
        UIKit.HudShadow(t);
        return t;
    }

    // =========================================================
    // TEAM ROSTER
    // =========================================================

    private void BuildRoster(RectTransform parent, bool ally)
    {
        var team = match.Roster(ally);
        var col = kit.Rect(parent, ally ? "RosterAlly" : "RosterEnemy");
        float height = team.Count * RowHeight + Mathf.Max(0, team.Count - 1) * RowGap;
        UIKit.Place(col, ally ? UIKit.TopLeft : UIKit.TopRight, new Vector2(ally ? Edge : -Edge, -Edge), new Vector2(RosterWidth, height));

        var P = kit.P;
        for (int i = 0; i < team.Count; i++)
        {
            var p = team[i];
            bool self = p.IsLocal;
            // Coin exterieur coupe : celui qui regarde le bord de l'ecran le plus proche (en haut, cote centre)
            var plate = ChamferedPlate.Build(kit, col, "Row_" + p.Name, ally ? PlateCorners.TopRight : PlateCorners.TopLeft,
                                             BlockforgeTheme.HudChamfer, P.SurfaceHud, ally ? P.PlateAlly : P.PlateEnemy);
            UIKit.Place(plate.Rect, UIKit.TopLeft, new Vector2(0f, -i * (RowHeight + RowGap)), new Vector2(RosterWidth, RowHeight));
            if (self)
                plate.Tint.SetColors(P.PlateSelf, P.PlateSelf);
            else if (!ally)
                plate.Tint.SetColors(BlockforgeTheme.WithAlpha(P.PlateEnemy, 0f), P.PlateEnemy);

            // Badge sur le bord exterieur
            var badge = kit.Image(plate.Content, "Badge", P.SurfaceBase);
            UIKit.Place(badge.rectTransform, ally ? UIKit.MiddleLeft : UIKit.MiddleRight, Vector2.zero, new Vector2(RowHeight, RowHeight));
            var icon = kit.Icon(badge.rectTransform, "Icon", self ? "user" : ally ? "ally" : "enemy", 12f,
                                self ? P.Accent : ally ? P.TeamAlly : P.TeamEnemy);
            UIKit.Place(icon.rectTransform, UIKit.Center, Vector2.zero, new Vector2(12f, 12f));

            // Score (kills) cote interieur, nom entre les deux
            var score = HudText(plate.Content, "Score", FontFamily.Mono, 12f, 500, P.InkSecondary, "0",
                                ally ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
            UIKit.Place(score.rectTransform, ally ? UIKit.MiddleRight : UIKit.MiddleLeft, new Vector2(ally ? -BlockforgeTheme.Space4 : BlockforgeTheme.Space4, 0f),
                        new Vector2(28f, RowHeight));
            float nameX = RowHeight + BlockforgeTheme.Space2;
            float nameW = RosterWidth - nameX - BlockforgeTheme.Space4 - 28f - BlockforgeTheme.Space2;
            var name = HudText(plate.Content, "Name", FontFamily.Sans, 13f, self ? 600 : 500, P.InkPrimary, p.Name,
                               ally ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight);
            UIKit.Place(name.rectTransform, ally ? UIKit.MiddleLeft : UIKit.MiddleRight, new Vector2(ally ? nameX : -nameX, 0f), new Vector2(nameW, RowHeight));
            UIKit.Ellipsis(name);

            rows.Add(new RosterRow { p = p, group = kit.Group(plate.Rect), badgeIcon = icon, name = name, score = score });
        }
    }

    private void UpdateRosters()
    {
        var P = kit.P;
        foreach (var row in rows)
        {
            bool down = !row.p.Alive && match.Phase == TdmPhase.Live;
            if (down != row.down)
            {
                row.down = down;
                row.group.alpha = down ? BlockforgeTheme.OpacityGhost : 1f;
                row.name.color = down ? P.InkMuted : P.InkPrimary;
                bool ally = match.IsAlly(row.p);
                string icon = row.p.IsLocal ? "user" : down ? "skull" : ally ? "ally" : "enemy";
                row.badgeIcon.sprite = DSAssets.Icon(icon);
            }
            if (row.kills != row.p.Kills)
            {
                row.kills = row.p.Kills;
                row.score.text = BlockforgeTheme.Num(row.kills);
            }
        }
    }

    // =========================================================
    // SCORE BAR (deathmatch)
    // =========================================================

    private void BuildScoreBar(RectTransform parent)
    {
        var P = kit.P;
        float gap = BlockforgeTheme.Space1;
        float width = BarWidth * 2f + MidWidth + gap * 2f;
        var bar = kit.Rect(parent, "ScoreBar");
        UIKit.Place(bar, UIKit.TopCenter, new Vector2(0f, -12f), new Vector2(width, DmHeight + 2f + 16f));
        float barY = -(DmHeight - BarHeight) * 0.5f;

        // Allies : a gauche, remplissage depuis le bord exterieur gauche
        var allyBar = kit.Polygon(bar, "Ally");
        UIKit.Place(allyBar.rectTransform, UIKit.TopLeft, new Vector2(0f, barY), new Vector2(BarWidth, BarHeight));
        allyBar.cutTopLeft = BlockforgeTheme.HudChamfer;
        allyBar.SetSolid(P.SurfaceHud);
        allyFillPoly = kit.Polygon(allyBar.rectTransform, "Fill");
        allyFill = allyFillPoly.rectTransform;
        UIKit.Place(allyFill, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 0f));
        allyFillPoly.SetSolid(P.TeamAlly);
        allyNum = HudText(allyBar.rectTransform, "Count", FontFamily.Mono, 11f, 600, P.InkPrimary, "0", TextAlignmentOptions.MidlineRight);
        UIKit.Stretch(allyNum.rectTransform, 6f, 0f, 6f, 0f);

        // Ennemis : a droite, remplissage depuis le bord exterieur droit
        var enemyBar = kit.Polygon(bar, "Enemy");
        UIKit.Place(enemyBar.rectTransform, UIKit.TopRight, new Vector2(0f, barY), new Vector2(BarWidth, BarHeight));
        enemyBar.cutTopRight = BlockforgeTheme.HudChamfer;
        enemyBar.SetSolid(P.SurfaceHud);
        enemyFillPoly = kit.Polygon(enemyBar.rectTransform, "Fill");
        enemyFill = enemyFillPoly.rectTransform;
        UIKit.Place(enemyFill, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 0f));
        enemyFillPoly.SetSolid(P.TeamEnemy);
        enemyNum = HudText(enemyBar.rectTransform, "Count", FontFamily.Mono, 11f, 600, P.InkPrimary, "0", TextAlignmentOptions.MidlineLeft);
        UIKit.Stretch(enemyNum.rectTransform, 6f, 0f, 6f, 0f);

        // Centre : plaque trapeze (crane + objectif), chrono dessous
        var mid = kit.Rect(bar, "Mid");
        UIKit.Place(mid, UIKit.TopCenter, Vector2.zero, new Vector2(MidWidth, DmHeight + 2f + 16f));
        var dm = kit.Polygon(mid, "Deathmatch");
        float ch = BlockforgeTheme.HudChamfer;
        // Haut retreci des deux cotes : bord gauche incline (slantLeft) et bord droit incline vers l'exterieur en bas
        UIKit.Place(dm.rectTransform, UIKit.TopLeft, new Vector2((MidWidth - DmWidth) * 0.5f, 0f), new Vector2(DmWidth - ch, DmHeight));
        dm.slantLeft = ch;
        dm.slantRight = -ch;
        dm.SetSolid(P.SurfaceHud);
        var skull = kit.Icon(mid, "Skull", "skull", 16f, P.InkPrimary);
        UIKit.Place(skull.rectTransform, UIKit.TopCenter, new Vector2(-11f, -(DmHeight - 16f) * 0.5f), new Vector2(16f, 16f));
        targetNum = HudText(mid, "Target", FontFamily.Mono, 13f, 600, P.InkSecondary, BlockforgeTheme.Num(match.ScoreToWin), TextAlignmentOptions.MidlineLeft);
        UIKit.Place(targetNum.rectTransform, UIKit.TopCenter, new Vector2(15f, 0f), new Vector2(26f, DmHeight));
        clock = HudText(mid, "Clock", FontFamily.Mono, 12f, 500, P.InkSecondary, BlockforgeTheme.Clock(System.TimeSpan.FromSeconds(match.MatchSeconds)),
                        TextAlignmentOptions.Center);
        UIKit.Place(clock.rectTransform, UIKit.TopCenter, new Vector2(0f, -(DmHeight + 2f)), new Vector2(MidWidth + 20f, 16f));
    }

    private void UpdateScoreBar()
    {
        int ally = match.AllyScore, enemy = match.EnemyScore;
        if (ally != shownAlly || enemy != shownEnemy)
        {
            shownAlly = ally;
            shownEnemy = enemy;
            float max = Mathf.Max(1, match.ScoreToWin);
            float aw = BarWidth * Mathf.Clamp01(ally / max);
            float ew = BarWidth * Mathf.Clamp01(enemy / max);
            allyFill.sizeDelta = new Vector2(aw, 0f);
            enemyFill.sizeDelta = new Vector2(ew, 0f);
            // La coupe de la barre suit le remplissage tant qu'il est plus etroit qu'elle
            allyFillPoly.cutTopLeft = Mathf.Min(BlockforgeTheme.HudChamfer, aw);
            enemyFillPoly.cutTopRight = Mathf.Min(BlockforgeTheme.HudChamfer, ew);
            allyFillPoly.enabled = aw > 0.5f;
            enemyFillPoly.enabled = ew > 0.5f;
            allyFillPoly.SetVerticesDirty();
            enemyFillPoly.SetVerticesDirty();
            allyNum.text = BlockforgeTheme.Num(ally);
            enemyNum.text = BlockforgeTheme.Num(enemy);
        }
        float left = match.Phase == TdmPhase.Live || match.Phase == TdmPhase.Ended ? match.TimeLeft : match.MatchSeconds;
        int seconds = Mathf.CeilToInt(left);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            clock.text = BlockforgeTheme.Clock(System.TimeSpan.FromSeconds(seconds));
        }
    }

    // =========================================================
    // NAME PLATES
    // =========================================================

    private void BuildPlates()
    {
        var P = kit.P;
        foreach (var p in match.Participants)
        {
            if (p.IsLocal)
                continue;
            bool ally = match.IsAlly(p);
            var rt = kit.Rect(platesLayer, "Plate_" + p.Name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            var color = ally ? P.TeamAlly : P.TeamEnemy;

            var mark = kit.Icon(rt, "Mark", ally ? "ally" : "enemy", 11f, color);
            var name = HudText(rt, "Name", FontFamily.Sans, 13f, 600, P.InkPrimary, p.Name, TextAlignmentOptions.MidlineLeft);
            var dist = HudText(rt, "Distance", FontFamily.Mono, 11f, 500, P.InkSecondary, "", TextAlignmentOptions.MidlineLeft);
            float nameW = Mathf.Min(180f, UIKit.PreferredWidth(name) + 2f);
            const float distW = 44f, rowH = 16f;
            float rowW = 11f + 5f + nameW + 4f + distW;
            rt.sizeDelta = new Vector2(rowW, rowH + 2f + 2f);
            UIKit.Place(mark.rectTransform, UIKit.TopLeft, new Vector2(0f, -(rowH - 11f) * 0.5f), new Vector2(11f, 11f));
            UIKit.Place(name.rectTransform, UIKit.TopLeft, new Vector2(16f, 0f), new Vector2(nameW, rowH));
            UIKit.Ellipsis(name);
            UIKit.Place(dist.rectTransform, UIKit.TopLeft, new Vector2(16f + nameW + 4f, 0f), new Vector2(distW, rowH));

            // Ligne de vie : 2 px sous le nom
            var track = kit.Image(rt, "Track", new Color(0f, 0f, 0f, 0.45f));
            UIKit.Place(track.rectTransform, UIKit.BottomLeft, Vector2.zero, new Vector2(rowW, 2f));
            var fill = kit.Image(track.rectTransform, "Fill", color);
            var fillRt = fill.rectTransform;
            UIKit.Place(fillRt, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(rowW, 0f));

            var group = kit.Group(rt);
            group.alpha = 0f;
            plates.Add(new Plate { p = p, rt = rt, group = group, name = name, dist = dist, mark = mark, fill = fillRt, rowWidth = rowW, ally = ally });
        }
    }

    private void UpdatePlates()
    {
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;
        Vector3 eye = cam.transform.position;
        int mask = ~((1 << RobotTestSpawner.RobotLayer) | (1 << 2));
        foreach (var plate in plates)
        {
            var p = plate.p;
            bool show = p.Alive && match.Phase == TdmPhase.Live;
            Vector3 anchor = Vector3.zero;
            float dist = 0f;
            if (show)
            {
                anchor = PlateAnchor(p);
                dist = Vector3.Distance(eye, anchor);
                show = dist <= (plate.ally ? 400f : 220f);
            }
            Vector3 screen = Vector3.zero;
            if (show)
            {
                screen = cam.WorldToScreenPoint(anchor);
                show = screen.z > 0.5f && screen.x > -50f && screen.x < Screen.width + 50f && screen.y > -50f && screen.y < Screen.height + 50f;
            }
            if (show && Time.time >= plate.nextOcclusion)
            {
                // Derriere le relief : cachee (re-test toutes les 0,15 s)
                plate.nextOcclusion = Time.time + 0.15f;
                Vector3 dir = anchor - eye;
                plate.occluded = Physics.Raycast(eye, dir.normalized, out var hit, dir.magnitude - 1f, mask, QueryTriggerInteraction.Ignore)
                                 && hit.distance < dir.magnitude - 1.5f;
            }
            show &= !plate.occluded;
            plate.group.alpha = show ? 1f : 0f;
            if (!show)
                continue;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(platesLayer, screen, null, out var local);
            plate.rt.anchoredPosition = local;
            float scale = Mathf.Lerp(1.1f, 0.93f, Mathf.Clamp01(dist / 200f));   // 13 px x 0,93 = 12 px au plus petit
            plate.rt.localScale = new Vector3(scale, scale, 1f);
            int meters = Mathf.RoundToInt(dist);
            if (meters != plate.shownMeters)
            {
                plate.shownMeters = meters;
                plate.dist.text = BlockforgeTheme.Qty(meters, "m");
            }
            plate.fill.sizeDelta = new Vector2(plate.rowWidth * p.HealthFraction, 0f);
        }
    }

    // Au-dessus du haut du robot
    private static Vector3 PlateAnchor(TdmParticipant p)
    {
        float top = p.Loco != null ? p.Loco.RobotHeight : 1.5f;
        return p.Robot.transform.position + Vector3.up * (top + 1.2f);
    }

    // =========================================================
    // RETICULE AU-DESSUS D'UN ALLIE
    // =========================================================

    private void UpdateReticle()
    {
        var hud = match.LocalTestHud;
        var local = match.Local;
        if (hud == null || local == null || !local.Alive || cam == null)
            return;
        bool onAlly = false;
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int n = Physics.RaycastNonAlloc(ray, aimHits, 400f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Collider first = null;
        for (int i = 0; i < n; i++)
        {
            var c = aimHits[i].collider;
            if (c == null || (local.Robot != null && c.transform.IsChildOf(local.Robot.transform)))
                continue;
            if (aimHits[i].distance < best)
            {
                best = aimHits[i].distance;
                first = c;
            }
        }
        if (first != null)
        {
            var p = TdmCombatant.Of(first);
            onAlly = p != null && match.IsAlly(p);
        }
        hud.SetReticleAlly(onAlly);
    }

    private void LateUpdate()
    {
        if (match == null)
            return;
        UpdateRosters();
        UpdateScoreBar();
        UpdatePlates();
        UpdateReticle();
    }
}
