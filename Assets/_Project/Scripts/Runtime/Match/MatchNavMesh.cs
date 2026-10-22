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
// dans l'ordre : sur-large (si large), sur, raide-large (si
// large), raide, et garde le premier chemin COMPLET.
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
        new() { name = "sur", radius = 1.6f, slope = SafeSlope, climb = SafeClimb, safe = true },
        new() { name = "sur large", radius = WideRadius, slope = SafeSlope, climb = SafeClimb, safe = true, wide = true },
        new() { name = "raide", radius = 1.6f, slope = SteepSlope, climb = SteepClimb },
        new() { name = "raide large", radius = WideRadius, slope = SteepSlope, climb = SteepClimb, wide = true },
    };

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

    // Variantes a essayer pour un robot, dans l'ordre
    private static IEnumerable<Variant> Order(bool wide)
    {
        if (wide && variants[1].ready) yield return variants[1];
        if (variants[0].ready) yield return variants[0];
        if (wide && variants[3].ready) yield return variants[3];
        if (variants[2].ready) yield return variants[2];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Ready = false;
        Building = false;
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

    public static void Clear()
    {
        Ready = false;
        Building = false;
        buildGeneration++;
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

    // Point du NavMesh le plus proche (rayon en m), sur le premier maillage pret pour ce robot
    public static bool Snap(Vector3 point, float radius, out Vector3 snapped, bool wide = false)
    {
        snapped = point;
        foreach (var v in Order(wide))
        {
            if (NavMesh.SamplePosition(point, out var hit, radius, v.Filter))
            {
                snapped = hit.position;
                return true;
            }
        }
        return false;
    }

    // Chemin entre deux points : premier maillage (dans l'ordre sur -> raide) qui donne un
    // chemin COMPLET ; a defaut, le premier chemin partiel trouve. Faux si aucun.
    public static bool TryPath(Vector3 from, Vector3 to, NavMeshPath path, bool wide = false)
    {
        if (path == null)
            return false;
        bool partial = false;
        foreach (var v in Order(wide))
        {
            if (!NavMesh.SamplePosition(from, out var a, 8f, v.Filter) || !NavMesh.SamplePosition(to, out var b, 25f, v.Filter))
                continue;
            if (!NavMesh.CalculatePath(a.position, b.position, v.Filter, path) || path.status == NavMeshPathStatus.PathInvalid || path.corners.Length == 0)
                continue;
            if (path.status == NavMeshPathStatus.PathComplete)
                return true;
            partial = true;
        }
        if (partial)
        {
            // On refait le dernier chemin partiel utilisable (path a pu etre ecrase par un essai rate)
            foreach (var v in Order(wide))
            {
                if (NavMesh.SamplePosition(from, out var a, 8f, v.Filter) && NavMesh.SamplePosition(to, out var b, 25f, v.Filter)
                    && NavMesh.CalculatePath(a.position, b.position, v.Filter, path) && path.status == NavMeshPathStatus.PathPartial && path.corners.Length > 0)
                    return true;
            }
        }
        return false;
    }

    // Chemin COMPLET seulement (sortie de crevasse) : longueur en m, faux si partiel
    public static bool TryCompletePath(Vector3 from, Vector3 to, NavMeshPath path, out float length, bool wide = false)
    {
        length = 0f;
        if (path == null)
            return false;
        foreach (var v in Order(wide))
        {
            if (!NavMesh.SamplePosition(from, out var a, 8f, v.Filter) || !NavMesh.SamplePosition(to, out var b, 8f, v.Filter))
                continue;
            if (!NavMesh.CalculatePath(a.position, b.position, v.Filter, path) || path.status != NavMeshPathStatus.PathComplete)
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
