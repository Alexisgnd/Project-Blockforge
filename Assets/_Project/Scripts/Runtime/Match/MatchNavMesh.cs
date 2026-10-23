using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// =========================================================
// NAVMESH DU MATCH (calcule au lancement de la carte)
// Les bots au sol suivent des chemins NavMesh ; aucune donnee
// n'est cuite dans la scene : les maillages sont construits au
// chargement, pendant le compte a rebours, a partir des
// colliders de la carte (module AI integre a Unity, sans
// package). Quatre maillages, du plus sur au plus permissif :
// - "sur" : pente 25 degres, marche 0,45 m. Les chemins restent
//   sur les routes et les rampes ; les talus de 30 degres des
//   routes encaissees et les bords de dalle des ponts (ou roues,
//   pattes et chenilles patinaient ou butaient) en sont exclus.
// - "raide" : pente 34 degres, marche 0,9 m (l'ancien reglage) :
//   secours quand le maillage sur n'offre aucun chemin complet
//   (fond de crevasse, par exemple).
// Chacun en deux rayons : 1,6 m (robot moyen) et 3,2 m (robots
// larges : pattes, chenilles, caisses de 5 cases ; leurs chemins
// passent loin des rochers et des bords). Une requete essaie
// dans l'ordre : sur-large (si large), sur, et garde le premier
// chemin COMPLET.
// Zone principale (definitif, 23/10) : seul compte le sol relie
// aux bases par des pentes de 25 degres au plus (chemin COMPLET
// vers la base A ou B, IsMainRegion, verifie par cases de 4 m et
// mis en cache). Les ilots du maillage sur les collines, plateaux
// et pitons (morceaux plats isoles) n'existent plus pour les
// bots : ni depart, ni destination, ni point de couloir.
// Chemins COMPLETS seulement : plus de chemin partiel qui
// s'arretait au pied de la colline la plus proche d'une cible
// inaccessible (le bot s'y collait). Destination sur une colline
// ou derriere une colline sans detour praticable : pas de chemin.
// Destination ecartee du bord du maillage (EdgeMargin) : le nez
// d'un long robot ne touche plus la pente.
// Collines (strict, 23/10) : les chemins, points de couloir et
// points de deblocage n'utilisent QUE les maillages surs ; les
// maillages raides ne servent plus qu'a sortir d'une crevasse
// (allowSteep, BotBrain.FindPitExit). Le depart d'un chemin
// (StartPoint) n'est jamais au-dessus du robot : le point du
// maillage le plus proche en 3D pouvait etre le haut d'un plateau
// voisin (6 m plus haut, « a 8 m »), et le bot foncait dans la
// pente pour l'atteindre. On cherche autour de lui, au niveau du
// sol, un point qui ne monte pas plus que MaxStartRise + 15 %.
// - Bounds : les colliders non-trigger de la scene hors robots
//   (calque RobotTestSpawner.RobotLayer) et hors decor geant
//   (> 3 km : decor exterieur sans collision de toute facon)
// - Construction asynchrone (UpdateNavMeshDataAsync) ; tant
//   qu'aucun maillage n'est fini, Ready est faux et les bots
//   roulent en ligne droite avec leurs antennes d'evitement
// - Clear() au depart de la scene (TdmMatch.OnDestroy)
// Build : en dehors de l'editeur, un MeshCollider dont le mesh
// n'est pas lisible (Read/Write) est ignore par la construction ;
// le nombre de maillages ignores est journalise.
// =========================================================

public static class MatchNavMesh
{
    public const float WideRadius = 3.2f;
    public const float SafeSlope = 25f, SafeClimb = 0.45f;
    public const float SteepSlope = 34f, SteepClimb = 0.9f;

    private class Variant
    {
        public int index;
        public string name;
        public float radius, slope, climb;
        public bool wide, safe, ready;
        public int typeId = -1;
        public NavMeshData data;
        public NavMeshDataInstance instance;
        public NavMeshQueryFilter Filter => new() { agentTypeID = typeId, areaMask = NavMesh.AllAreas };
    }

    // Ordre de construction = ordre de preference pour un robot large
    private static readonly Variant[] variants =
    {
        new() { index = 0, name = "sur", radius = 1.6f, slope = SafeSlope, climb = SafeClimb, safe = true },
        new() { index = 1, name = "sur large", radius = WideRadius, slope = SafeSlope, climb = SafeClimb, safe = true, wide = true },
        new() { index = 2, name = "raide", radius = 1.6f, slope = SteepSlope, climb = SteepClimb },
        new() { index = 3, name = "raide large", radius = WideRadius, slope = SteepSlope, climb = SteepClimb, wide = true },
    };

    // ---------- Zone principale : le sol relie aux bases ----------

    private const float RegionCell = 4f;      // cache : une reponse par case de 4 x 4 x 4 m
    public const float EdgeMargin = 5f;       // destination a au moins cette distance du bord du maillage (m)
    private static readonly Dictionary<long, bool>[] regionCache = { new(), new(), new(), new() };
    private static readonly Vector3[,] anchors = new Vector3[4, 2];
    private static readonly int[] anchorCount = { -1, -1, -1, -1 };   // -1 : pas encore cherche
    private static NavMeshPath regionPath;

    // Ancres de la zone principale : centres des deux bases du match (TdmMatch), poses sur
    // ce maillage. Sans match (autre scene), pas de filtre.
    private static int Anchors(Variant v)
    {
        if (anchorCount[v.index] >= 0)
            return anchorCount[v.index];
        var match = TdmMatch.Instance;
        if (match == null)
            return 0;   // pas de cache : le match peut arriver plus tard
        int n = 0;
        for (int side = 0; side < 2; side++)
        {
            if (NavMesh.SamplePosition(match.OwnBase(side), out var hit, 40f, v.Filter))
                anchors[v.index, n++] = hit.position;
        }
        if (n > 0 || v.ready)
            anchorCount[v.index] = n;
        return n;
    }

    // Point (deja sur ce maillage) relie a une base par un chemin complet
    private static bool IsMainRegion(Vector3 p, Variant v)
    {
        int n = Anchors(v);
        if (n == 0)
            return true;
        long key = ((long)(Mathf.FloorToInt(p.x / RegionCell) & 0x1FFFFF) << 42)
                 | ((long)(Mathf.FloorToInt(p.y / RegionCell) & 0x1FFFFF) << 21)
                 | (long)(Mathf.FloorToInt(p.z / RegionCell) & 0x1FFFFF);
        var cache = regionCache[v.index];
        if (cache.TryGetValue(key, out bool ok))
            return ok;
        regionPath ??= new NavMeshPath();
        ok = false;
        for (int i = 0; i < n && !ok; i++)
            ok = NavMesh.CalculatePath(p, anchors[v.index, i], v.Filter, regionPath) && regionPath.status == NavMeshPathStatus.PathComplete;
        cache[key] = ok;
        return ok;
    }

    // Point de la zone principale le plus proche de 'point' a moins de 'radius' : le plus
    // proche en 3D s'il en fait partie, sinon recherche en anneaux au niveau du sol
    private static bool MainPoint(Vector3 point, float radius, Variant v, out Vector3 result)
    {
        result = point;
        if (NavMesh.SamplePosition(point, out var hit, radius, v.Filter) && IsMainRegion(hit.position, v))
        {
            result = hit.position;
            return true;
        }
        float best = float.MaxValue;
        foreach (float k in new[] { 0.33f, 0.66f, 1f })
        {
            float r = radius * k;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = point + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * r;
                p.y = Ground(p, point.y + 40f);
                if (!NavMesh.SamplePosition(p, out var h, 3f, v.Filter) || !IsMainRegion(h.position, v))
                    continue;
                float d = (h.position - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    result = h.position;
                }
            }
            if (best < float.MaxValue)
                return true;
        }
        return false;
    }

    // Destination : zone principale, ecartee du bord du maillage
    private static bool DestinationPoint(Vector3 to, float radius, Variant v, out Vector3 result)
    {
        if (!MainPoint(to, radius, v, out result))
            return false;
        if (NavMesh.FindClosestEdge(result, out var edge, v.Filter) && edge.distance < EdgeMargin)
        {
            Vector3 away = result - edge.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-4f)
            {
                Vector3 inward = result + away.normalized * (EdgeMargin - edge.distance);
                if (NavMesh.SamplePosition(inward, out var h, 2f, v.Filter) && IsMainRegion(h.position, v))
                    result = h.position;
            }
        }
        return true;
    }

    // Hauteur libre exigee au-dessus du maillage : pas de chemin sous un pont plus
    // bas. 4 m jusqu'au 22/10 : des bots de 11,5 m passaient sous les ponts de la
    // map v6 (jour 1,5 a 17 m selon l'endroit) et s'y coincaient. Les presets au sol
    // font 8,7 m au plus (BotPresetDebugMenu.Validate le verifie). Mesure du jour
    // sous les ponts : Blockforge > Debug > Bots > Measure heights and bridges.
    public const float AgentHeight = 9.5f;

    public static bool Ready { get; private set; }
    public static bool Building { get; private set; }
    public static bool WideReady => variants[1].ready;
    // Compatibilite : type d'agent et filtre du maillage principal (le premier pret)
    public static int AgentTypeId => First()?.typeId ?? -1;
    public static NavMeshQueryFilter Filter => First()?.Filter ?? new NavMeshQueryFilter { areaMask = NavMesh.AllAreas };

    private static Variant First()
    {
        foreach (var v in variants)
            if (v.ready) return v;
        return null;
    }

    // Variantes a essayer pour un robot, dans l'ordre ; les raides seulement si
    // permises (sortie de crevasse)
    private static IEnumerable<Variant> Order(bool wide, bool steep)
    {
        if (wide && variants[1].ready) yield return variants[1];
        if (variants[0].ready) yield return variants[0];
        if (!steep)
            yield break;
        if (wide && variants[3].ready) yield return variants[3];
        if (variants[2].ready) yield return variants[2];
    }

    // ---------- Depart d'un chemin : jamais au-dessus du robot ----------

    // Montee tolere entre le robot et son point de depart (m), plus StartGrade par
    // metre a plat (~8,5 degres : moins que n'importe quelle pente refusee)
    public const float MaxStartRise = 1.5f;
    private const float StartGrade = 0.15f;
    private static readonly float[] StartRings = { 4f, 8f, 13f, 20f, 28f };

    // Point du maillage d'ou un robot pose en 'from' peut partir sans gravir de pente
    private static bool StartPoint(Vector3 from, Variant v, out Vector3 start)
    {
        start = from;
        if (NavMesh.SamplePosition(from, out var hit, 3f, v.Filter) && hit.position.y <= from.y + MaxStartRise && IsMainRegion(hit.position, v))
        {
            start = hit.position;
            return true;
        }
        float best = float.MaxValue;
        foreach (float r in StartRings)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = from + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * r;
                p.y = Ground(p, from.y);
                if (!NavMesh.SamplePosition(p, out var h, 2.5f, v.Filter) || !IsMainRegion(h.position, v))
                    continue;
                float flat = new Vector2(h.position.x - from.x, h.position.z - from.z).magnitude;
                float rise = h.position.y - from.y;
                if (rise > MaxStartRise + flat * StartGrade)
                    continue;
                float score = flat + Mathf.Max(0f, rise) * 3f;
                if (score < best)
                {
                    best = score;
                    start = h.position;
                }
            }
            if (best < float.MaxValue)
                return true;
        }
        return false;
    }

    // Sol sous p, rayon parti de 10 m au-dessus de la hauteur de reference (un
    // tablier de pont plus haut est ignore)
    private static float Ground(Vector3 p, float referenceY)
    {
        int mask = ~((1 << RobotTestSpawner.RobotLayer) | (1 << 2));
        return Physics.Raycast(new Vector3(p.x, referenceY + 10f, p.z), Vector3.down, out var hit, 80f, mask, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : referenceY;
    }

    // Point du maillage sur ou un robot hors maillage (pousse dans une pente) peut
    // retourner sans monter
    public static bool SnapReachable(Vector3 from, out Vector3 point, bool wide = false)
    {
        point = from;
        foreach (var v in Order(wide, false))
        {
            if (StartPoint(from, v, out point))
                return true;
        }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Ready = false;
        Building = false;
        ClearRegions();
        foreach (var v in variants)
        {
            v.ready = false;
            v.typeId = -1;
            v.data = null;
            v.instance = default;
        }
    }

    public static IEnumerator Build(int robotLayer)
    {
        Clear();
        Building = true;
        float t0 = Time.realtimeSinceStartup;

        int mask = ~((1 << robotLayer) | (1 << 2));   // ni robots, ni Ignore Raycast
        if (!TryWorldBounds(mask, out var bounds))
        {
            Debug.LogWarning("[MatchNav] Aucun collider de decor : pas de NavMesh, les bots rouleront en ligne droite.");
            Building = false;
            yield break;
        }

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        int unreadable = 0;
#if !UNITY_EDITOR
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i].shape == NavMeshBuildSourceShape.Mesh && sources[i].sourceObject is Mesh m && !m.isReadable)
            {
                sources.RemoveAt(i);
                unreadable++;
            }
        }
#endif
        int generation = ++buildGeneration;
        foreach (var v in variants)
        {
            float t1 = Time.realtimeSinceStartup;
            var settings = NavMesh.CreateSettings();
            settings.agentRadius = v.radius;
            settings.agentHeight = AgentHeight;
            settings.agentSlope = v.slope;
            settings.agentClimb = v.climb;
            settings.minRegionArea = 12f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.4f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            v.typeId = settings.agentTypeID;
            v.data = new NavMeshData(v.typeId);
            v.instance = NavMesh.AddNavMeshData(v.data);
            var op = NavMeshBuilder.UpdateNavMeshDataAsync(v.data, settings, sources, bounds);
            if (op != null)
                yield return op;
            if (generation != buildGeneration || v.data == null)
                yield break;   // Clear() pendant la construction
            v.ready = NavMesh.SamplePosition(bounds.center, out _, Mathf.Max(bounds.extents.x, bounds.extents.z), v.Filter);
            Ready |= v.ready;
            Debug.Log($"[MatchNav] NavMesh {v.name} (rayon {v.radius} m, pente {v.slope:0} deg, marche {v.climb:0.00} m) {(v.ready ? "pret" : "VIDE")} " +
                      $"en {Time.realtimeSinceStartup - t1:0.0} s" + (v == variants[0] ? $" : {sources.Count} source(s), zone {bounds.size.x:0} x {bounds.size.z:0} m" +
                      (unreadable > 0 ? $", {unreadable} maillage(s) non lisible(s) ignore(s)" : "") : "") + ".");
        }
        Building = false;
        Debug.Log($"[MatchNav] Maillages prets en {Time.realtimeSinceStartup - t0:0.0} s.");
    }

    private static int buildGeneration;

    private static void ClearRegions()
    {
        for (int i = 0; i < 4; i++)
        {
            regionCache[i].Clear();
            anchorCount[i] = -1;
        }
        regionPath = null;
    }

    public static void Clear()
    {
        Ready = false;
        Building = false;
        buildGeneration++;
        ClearRegions();
        foreach (var v in variants)
        {
            v.ready = false;
            if (v.instance.valid)
                NavMesh.RemoveNavMeshData(v.instance);
            v.instance = default;
            v.data = null;
            if (v.typeId >= 0)
                NavMesh.RemoveSettings(v.typeId);
            v.typeId = -1;
        }
    }

    // Point du NavMesh sur le plus proche (rayon en m), sur le premier maillage pret pour ce robot
    public static bool Snap(Vector3 point, float radius, out Vector3 snapped, bool wide = false)
    {
        snapped = point;
        foreach (var v in Order(wide, false))
        {
            if (MainPoint(point, radius, v, out snapped))
                return true;
        }
        snapped = point;
        return false;
    }

    // Chemin entre deux points, COMPLET seulement (plus de chemin partiel : il finissait au
    // pied de la colline la plus proche de la cible), sur un maillage sur (raide seulement si
    // allowSteep). Depart : StartPoint (jamais au-dessus du robot, zone principale) ;
    // arrivee : DestinationPoint (zone principale, loin du bord). Faux si aucun.
    public static bool TryPath(Vector3 from, Vector3 to, NavMeshPath path, bool wide = false, bool allowSteep = false)
    {
        if (path == null)
            return false;
        foreach (var v in Order(wide, allowSteep))
        {
            if (!StartPoint(from, v, out var a) || !DestinationPoint(to, 25f, v, out var b))
                continue;
            if (NavMesh.CalculatePath(a, b, v.Filter, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 0)
                return true;
        }
        return false;
    }

    // Chemin COMPLET seulement (sortie de crevasse avec allowSteep, verification des points
    // de couloir sans) : longueur en m, faux si partiel
    public static bool TryCompletePath(Vector3 from, Vector3 to, NavMeshPath path, out float length, bool wide = false, bool allowSteep = false)
    {
        length = 0f;
        if (path == null)
            return false;
        foreach (var v in Order(wide, allowSteep))
        {
            if (!StartPoint(from, v, out var a) || !MainPoint(to, 8f, v, out var b))
                continue;
            if (!NavMesh.CalculatePath(a, b, v.Filter, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;
            var c = path.corners;
            for (int i = 1; i < c.Length; i++)
                length += Vector3.Distance(c[i - 1], c[i]);
            return c.Length > 0;
        }
        return false;
    }

    // Point aleatoire du NavMesh autour d'une position (sortie d'un blocage)
    public static bool RandomPointNear(Vector3 center, float radius, out Vector3 point, bool wide = false)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            if (Snap(center + new Vector3(r.x, 0f, r.y), 8f, out point, wide))
                return true;
        }
        point = center;
        return false;
    }

    private static bool TryWorldBounds(int mask, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
        {
            if (col.isTrigger || ((1 << col.gameObject.layer) & mask) == 0 || col.attachedRigidbody != null)
                continue;
            var b = col.bounds;
            if (b.size.x > 3000f || b.size.z > 3000f)
                continue;
            if (!any) { bounds = b; any = true; }
            else bounds.Encapsulate(b);
        }
        if (any)
            bounds.Expand(new Vector3(4f, 10f, 4f));
        return any;
    }
}
