using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// =========================================================
// COULOIRS ET ROLES DES BOTS (repartition sur la carte)
// Sans cette couche, les dix bots prenaient tous le chemin le
// plus court vers la meme cible (ou la base adverse) et se
// retrouvaient au centre. Ici, chaque bot recoit un role, et
// chaque role un couloir (suite de points de patrouille) calcule
// a partir de la geometrie du match (TdmMatch : OwnBase,
// EnemyBase, Center ; MatchNavMesh pour poser les points) :
// - Left / Right : couloirs lateraux, ecartes de l'axe base ->
//   base de (distance entre bases x LaneWidthFactor), en arc
//   (ecart maximal a mi-carte) ;
// - Center : l'axe base -> Marker_Center -> base adverse ;
// - FlankLeft / FlankRight : flanc profond (ecart x FlankFactor),
//   qui pousse plus loin chez l'adversaire ;
// - Sniper : deux ou trois points de tir de sa moitie de carte
//   (bots a rail seulement, MaxSnipers par equipe). Plus de points
//   hauts depuis le 23/10 : pas de pathfinding en hauteur, ils
//   restent sur le sol relie aux bases (MatchNavMesh, zone
//   principale) et sont choisis bien repartis ;
// - Marauder : volants, boucle qui passe d'un couloir a l'autre.
// Repere propre a chaque equipe (axe de sa base vers la base
// adverse) : avec la symetrie a 180 degres de la carte, la
// "gauche" de A croise la "droite" de B, d'ou trois fronts.
// Repartition equilibree dans chaque equipe (penalite par bot
// deja sur le role), teintee par le style (prudent : centre /
// tireur ; agressif, opportuniste : flancs).
// Les points sont recalcules une fois le NavMesh pret (avant,
// ils sont seulement poses sur le sol). Un point hors carte ou
// hors NavMesh est rapproche de l'axe jusqu'a en trouver un.
// Le registre (participant -> role, cerveau) survit aux
// reapparitions (un nouveau BotBrain a chaque robot) et est
// vide quand un autre TdmMatch le lit (nouveau match).
// =========================================================

public enum BotRole { Left, Center, Right, FlankLeft, FlankRight, Sniper, Marauder }

public static class BotLanes
{
    // ---------- Reglages ----------
    public const float LaneWidthFactor = 0.24f;      // ecart lateral des couloirs = distance entre bases x ce facteur
    public const float LaneWidthMin = 90f, LaneWidthMax = 300f;
    public const float FlankFactor = 1.6f;           // flanc profond : ecart lateral x ce facteur
    public const float SnapRadius = 30f;             // un point de couloir est ramene sur le NavMesh dans ce rayon
    public const int MaxSnipers = 1;                 // tireurs par equipe
    public const float SameRolePenalty = 2f;         // score retire par bot deja sur ce role (equilibre)
    public const float RerollKeepPenalty = 1f;       // au re-tirage, garder le meme role coute ce score
    public const float RoleRandomness = 1.2f;        // part de hasard dans le choix du role

    private static readonly float[] LaneT = { 0.2f, 0.38f, 0.55f, 0.72f };
    private static readonly float[] FlankT = { 0.22f, 0.42f, 0.62f, 0.8f };
    private const int RoleCount = 7;

    private sealed class Entry
    {
        public BotRole Role;
        public bool Assigned;
        public BotBrain Brain;
    }

    private sealed class Lane
    {
        public List<Vector3> Points;
        public bool OnNav;   // calcule avec le NavMesh pret
    }

    private static TdmMatch owner;
    private static readonly Dictionary<TdmParticipant, Entry> entries = new();
    private static readonly Dictionary<int, Lane> lanes = new();
    private static NavMeshPath probePath;
    private static int terrainMask = -1;
    private static int TerrainMask => terrainMask != -1 ? terrainMask : (terrainMask = ~((1 << RobotTestSpawner.RobotLayer) | (1 << 2)));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        owner = null;
        entries.Clear();
        lanes.Clear();
        probePath = null;
    }

    private static void Bind(TdmMatch match)
    {
        if (owner == match)
            return;
        owner = match;
        entries.Clear();
        lanes.Clear();
    }

    // ---------- Registre ----------

    public static void Register(TdmMatch match, TdmParticipant p, BotBrain brain)
    {
        Bind(match);
        if (!entries.TryGetValue(p, out var e))
        {
            e = new Entry();
            entries[p] = e;
        }
        e.Brain = brain;
    }

    public static BotBrain BrainOf(TdmMatch match, TdmParticipant p)
    {
        Bind(match);
        return p != null && entries.TryGetValue(p, out var e) && e.Brain != null ? e.Brain : null;
    }

    public static bool HasRole(TdmMatch match, TdmParticipant p, out BotRole role)
    {
        Bind(match);
        role = BotRole.Center;
        if (p == null || !entries.TryGetValue(p, out var e) || !e.Assigned)
            return false;
        role = e.Role;
        return true;
    }

    // Allies (bots) partis soutenir "ally", sauf "except"
    public static int SupportersOf(TdmMatch match, TdmParticipant ally, TdmParticipant except)
    {
        Bind(match);
        int n = 0;
        foreach (var kv in entries)
        {
            if (kv.Key == except || !kv.Key.Alive || kv.Value.Brain == null)
                continue;   // une epave garde son cerveau jusqu'a la reapparition : ne compte pas
            if (kv.Value.Brain.SupportAlly == ally)
                n++;
        }
        return n;
    }

    // Choix du role : equilibre dans l'equipe, style, un peu de hasard
    public static BotRole Assign(TdmMatch match, TdmParticipant p, bool flier, bool rail, BotStyle style, bool reroll)
    {
        Bind(match);
        if (!entries.TryGetValue(p, out var e))
        {
            e = new Entry();
            entries[p] = e;
        }
        var counts = new int[RoleCount];
        foreach (var kv in entries)
        {
            if (kv.Key == p || kv.Key.Side != p.Side || !kv.Value.Assigned)
                continue;
            counts[(int)kv.Value.Role]++;
        }
        int flankers = counts[(int)BotRole.FlankLeft] + counts[(int)BotRole.FlankRight];

        BotRole best = BotRole.Center;
        float bestScore = float.MinValue;
        for (int i = 0; i < RoleCount; i++)
        {
            var r = (BotRole)i;
            if (r == BotRole.Marauder && !flier)
                continue;
            if (r == BotRole.Sniper && (!rail || flier || counts[i] >= MaxSnipers))
                continue;
            int c = r == BotRole.FlankLeft || r == BotRole.FlankRight ? flankers : counts[i];
            float score = -SameRolePenalty * c + StylePreference(style, r) + Random.Range(0f, RoleRandomness);
            if (r == BotRole.Sniper)
                score += 1.5f;
            if (flier)
                score += r == BotRole.Marauder ? 2f : (r == BotRole.Center ? -1f : 0f);
            if (reroll && e.Assigned && r == e.Role)
                score -= RerollKeepPenalty;
            if (score > bestScore)
            {
                bestScore = score;
                best = r;
            }
        }
        e.Role = best;
        e.Assigned = true;
        return best;
    }

    private static float StylePreference(BotStyle style, BotRole r)
    {
        bool flank = r == BotRole.FlankLeft || r == BotRole.FlankRight;
        bool side = r == BotRole.Left || r == BotRole.Right;
        switch (style)
        {
            case BotStyle.Cautious:
                return r == BotRole.Sniper ? 1.5f : r == BotRole.Center ? 0.8f : side ? 0.3f : flank ? -1.5f : 0f;
            case BotStyle.Aggressive:
                return flank ? 1f : side ? 0.5f : r == BotRole.Center ? 0.3f : r == BotRole.Sniper ? -0.5f : 0f;
            case BotStyle.Opportunist:
                return flank ? 1.2f : side ? 0.4f : r == BotRole.Marauder ? 0.5f : 0f;
            default:
                return side || r == BotRole.Center ? 0.5f : 0f;
        }
    }

    public static string RoleName(BotRole r) => r switch
    {
        BotRole.Left => "gauche",
        BotRole.Center => "centre",
        BotRole.Right => "droite",
        BotRole.FlankLeft => "flanc gauche",
        BotRole.FlankRight => "flanc droit",
        BotRole.Sniper => "tireur",
        _ => "maraudeur",
    };

    // ---------- Couloirs ----------

    // Points de patrouille du role pour une equipe (de sa base vers la base adverse)
    public static List<Vector3> Points(TdmMatch match, int side, BotRole role)
    {
        Bind(match);
        int key = Mathf.Clamp(side, 0, 1) * 16 + (int)role;
        if (!lanes.TryGetValue(key, out var lane) || (!lane.OnNav && MatchNavMesh.Ready))
        {
            lane = new Lane { OnNav = MatchNavMesh.Ready, Points = Build(match, Mathf.Clamp(side, 0, 1), role) };
            lanes[key] = lane;
        }
        return lane.Points;
    }

    // Distance a plat d'un point au couloir (segments entre points successifs)
    public static float DistanceToLane(TdmMatch match, int side, BotRole role, Vector3 at)
    {
        var pts = Points(match, side, role);
        if (pts == null || pts.Count == 0)
            return 0f;
        var q = new Vector2(at.x, at.z);
        float best = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            var a = new Vector2(pts[i].x, pts[i].z);
            if (i + 1 < pts.Count)
            {
                var ab = new Vector2(pts[i + 1].x, pts[i + 1].z) - a;
                float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (a + ab * t - q).magnitude);
            }
            else
                best = Mathf.Min(best, (a - q).magnitude);
        }
        return best;
    }

    public static int NearestIndex(List<Vector3> pts, Vector3 at)
    {
        int best = 0;
        float bestD = float.MaxValue;
        if (pts == null)
            return 0;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 d = pts[i] - at;
            d.y = 0f;
            if (d.sqrMagnitude < bestD)
            {
                bestD = d.sqrMagnitude;
                best = i;
            }
        }
        return best;
    }

    private static List<Vector3> Build(TdmMatch match, int side, BotRole role)
    {
        Vector3 a = match.OwnBase(side), b = match.EnemyBase(side), c = match.Center;
        Vector3 axis = b - a;
        axis.y = 0f;
        float d = axis.magnitude;
        if (d < 1f)
        {
            axis = Vector3.forward;
            d = 1f;
        }
        else
            axis /= d;
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        float width = Mathf.Clamp(d * LaneWidthFactor, LaneWidthMin, LaneWidthMax);
        var pts = new List<Vector3>();

        switch (role)
        {
            case BotRole.Sniper:
                SniperPoints(match, side, a, b, c, right, width, pts);
                if (pts.Count > 0)
                    return pts;
                break;   // aucun point haut : il suit le couloir du centre
            case BotRole.Marauder:
                // Boucle autour du centre, d'un couloir lateral a l'autre
                pts.Add(Place(Spine(a, c, b, 0.5f), -right * width));
                pts.Add(Place(Spine(a, c, b, 0.65f), Vector3.zero));
                pts.Add(Place(Spine(a, c, b, 0.5f), right * width));
                pts.Add(Place(Spine(a, c, b, 0.35f), Vector3.zero));
                return pts;
        }

        float sign = role == BotRole.Left || role == BotRole.FlankLeft ? -1f
                   : role == BotRole.Right || role == BotRole.FlankRight ? 1f : 0f;
        bool flank = role == BotRole.FlankLeft || role == BotRole.FlankRight;
        float lateral = width * (flank ? FlankFactor : 1f);
        var ts = flank ? FlankT : LaneT;
        foreach (float t in ts)
            pts.Add(Place(Spine(a, c, b, t), right * (sign * lateral * Mathf.Sin(Mathf.PI * t))));
        return pts;
    }

    // Axe base -> centre -> base adverse
    private static Vector3 Spine(Vector3 a, Vector3 c, Vector3 b, float t)
    {
        return t < 0.5f ? Vector3.Lerp(a, c, t * 2f) : Vector3.Lerp(c, b, (t - 0.5f) * 2f);
    }

    // Pose un point (axe + ecart lateral) sur le sol et le NavMesh ; rapproche de l'axe si
    // le point tombe hors carte ou loin du NavMesh
    private static Vector3 Place(Vector3 spine, Vector3 lateral)
    {
        float[] factors = { 1f, 0.8f, 0.6f, 0.4f, 0.2f, 0f };
        foreach (float f in factors)
        {
            Vector3 p = spine + lateral * f;
            if (!Physics.Raycast(new Vector3(p.x, p.y + 400f, p.z), Vector3.down, out var hit, 1200f, TerrainMask, QueryTriggerInteraction.Ignore))
                continue;
            p.y = hit.point.y;
            if (MatchNavMesh.Ready)
            {
                if (!MatchNavMesh.Snap(p, SnapRadius, out var s))
                    continue;
                p = s;
            }
            return p;
        }
        return spine;
    }

    // Points de tir de sa moitie de carte (de 30 a 48 % du trajet), sur la zone principale du
    // NavMesh (le sol relie aux bases : jamais une colline ni un plateau) quand il est pret,
    // atteignables depuis la base, ecartes d'au moins 60 m entre eux. Plus de tri par
    // hauteur : pas de pathfinding en hauteur.
    private static void SniperPoints(TdmMatch match, int side, Vector3 a, Vector3 b, Vector3 c, Vector3 right, float width, List<Vector3> result)
    {
        var candidates = new List<Vector3>();
        for (int i = 0; i < 4; i++)
        {
            float t = 0.3f + i * 0.06f;
            Vector3 spine = Spine(a, c, b, t);
            for (int k = -3; k <= 3; k++)
            {
                Vector3 p = spine + right * (width * k / 3f);
                if (!Physics.Raycast(new Vector3(p.x, p.y + 400f, p.z), Vector3.down, out var hit, 1200f, TerrainMask, QueryTriggerInteraction.Ignore))
                    continue;
                p.y = hit.point.y;
                if (MatchNavMesh.Ready)
                {
                    // Sur la zone principale tout pres : colline, plateau, piton ne comptent pas
                    if (!MatchNavMesh.Snap(p, 6f, out var s))
                        continue;
                    p = s;
                }
                candidates.Add(p);
            }
        }

        Vector3 from = a;
        foreach (var p in match.Participants)
        {
            if (p.Side == side)
            {
                from = p.SpawnPosition;
                break;
            }
        }
        int checks = 0;
        foreach (var p in candidates)
        {
            if (result.Count >= 3)
                break;
            bool apart = true;
            foreach (var q in result)
            {
                Vector3 dd = q - p;
                dd.y = 0f;
                if (dd.magnitude < 60f) { apart = false; break; }
            }
            if (!apart)
                continue;
            if (MatchNavMesh.Ready)
            {
                if (checks >= 10)
                    break;
                checks++;
                probePath ??= new NavMeshPath();
                if (!MatchNavMesh.TryCompletePath(from, p, probePath, out _))
                    continue;
            }
            result.Add(p);
        }
    }
}
