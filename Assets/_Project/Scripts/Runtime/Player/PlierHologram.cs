using System.Collections.Generic;
using UnityEngine;

// =========================================================
// HOLOGRAMME DU BLOC SELECTIONNE (pince v2)
// Le bloc choisi dans l'inventaire flotte en hologramme entre
// les griffes, projete par la lentille de la tete :
// - Petit : il tient dans une sphere de rayon |Holo_Size -
//   Holo_Anchor| (repere du modele), quelle que soit sa taille
//   reelle (un bloc 1x1 et un chassis 3x4 ont la meme presence)
// - Aretes vives soulignees par des lignes lumineuses (maillage de
//   lignes calcule une fois par modele : aretes entre faces a plus
//   de 25 degres et bords libres)
// - Shader Blockforge/Hologram : remplissage leger, aretes
//   brillantes (Fresnel), lignes de balayage, scintillement
// - A chaque changement de bloc, il se materialise du bas vers
//   le haut (balayage + bande lumineuse)
// - Oriente comme il sera pose : quart de tour de la molette
//   (SetSpin, transmis par GarageBuildController), anime
// - Flotte doucement (lent balancement), un cone de projection
//   relie la lentille a l'hologramme
// - Flash (pose), teinte danger (pose refusee) : PlierToolFX
// =========================================================

public class PlierHologram : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public GarageInventoryController inventory;
    [Tooltip("Centre de l'hologramme (entre les pointes des griffes)")]
    public Transform anchor;
    [Tooltip("|sizeMarker - anchor| = rayon de l'hologramme")]
    public Transform sizeMarker;
    [Tooltip("Lentille de l'emetteur : depart du cone de projection")]
    public Transform lens;
    [Tooltip("Repere au-dessus de l'outil : le haut de l'hologramme")]
    public Transform topMarker;
    [Tooltip("Materiau Blockforge/Hologram (copie a l'execution)")]
    public Material hologramMaterial;

    [Header("Reglages")]
    public Color color = new(0.2f, 0.85f, 1f, 1f);
    [Range(0.3f, 1.2f)] public float sizeFactor = 1f;
    public float brightness = 4.2f;
    [Range(0f, 1f)] public float fill = 0.32f;
    public float rimStrength = 1.6f;
    public float edgeBrightness = 1.2f;   // x brightness (les lignes s'additionnent sur les pieces detaillees)
    public float revealDuration = 0.4f;
    public float bobAmplitude = 0.06f;   // x rayon
    public float bobSpeed = 1.8f;
    public float swayDegrees = 7f;
    public float tiltDegrees = 18f;      // penche vers le joueur : on voit le dessus
    public float yawSmoothTime = 0.12f;

    private GameObject pivot;            // racine de l'hologramme (suit l'ancre)
    private GameObject current;          // visuel du bloc
    private GameObject beam;
    private Material holoMat, beamMat, edgeMat;
    private static readonly Dictionary<Mesh, Mesh> EdgeCache = new();
    private Mesh beamMesh;
    private readonly List<Renderer> holoRenderers = new();

    private float radius = 0.03f;
    private float reveal = 1f;
    private float yaw, yawTarget, yawVelocity;
    private float flash;                 // 0..1, decroit
    private float danger;                // 0..1, decroit
    private Camera viewCamera;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int CenterId = Shader.PropertyToID("_HoloCenter");
    private static readonly int UpId = Shader.PropertyToID("_HoloUp");
    private static readonly int HalfHeightId = Shader.PropertyToID("_HoloHalfHeight");
    private static readonly int LineDensityId = Shader.PropertyToID("_LineDensity");
    private static readonly int BeamId = Shader.PropertyToID("_Beam");

    public float Radius => radius;

    private void Awake()
    {
        if (anchor == null)
            anchor = transform;
        if (hologramMaterial == null)
        {
            var shader = Shader.Find("Blockforge/Hologram");
            if (shader != null)
                hologramMaterial = new Material(shader);
        }
        if (hologramMaterial != null)
        {
            holoMat = new Material(hologramMaterial) { name = "Hologram (instance)" };
            beamMat = new Material(hologramMaterial) { name = "Hologram Beam (instance)" };
            beamMat.SetFloat(BeamId, 1f);
            edgeMat = new Material(hologramMaterial) { name = "Hologram Edges (instance)" };
            edgeMat.SetFloat(BeamId, 2f);
            holoMat.SetFloat("_FillAlpha", fill);
            holoMat.SetFloat("_RimStrength", rimStrength);
            holoMat.SetFloat("_LineStrength", 0.5f);
        }

        pivot = new GameObject("Hologram");
        pivot.layer = anchor.gameObject.layer;
        pivot.transform.SetParent(anchor, false);

        if (inventory != null)
            inventory.BlockSelected += Show;
    }

    private void Start()
    {
        viewCamera = Camera.main;
        if (current == null && inventory != null && inventory.CurrentBlock != null)
            Show(inventory.CurrentBlock);
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.BlockSelected -= Show;
        if (holoMat != null) Destroy(holoMat);
        if (edgeMat != null) Destroy(edgeMat);
        if (beamMat != null) Destroy(beamMat);
        if (beamMesh != null) Destroy(beamMesh);
    }

    // Quart de tour choisi a la molette (0..3)
    public void SetSpin(int spinIndex)
    {
        float target = spinIndex * 90f;
        // chemin le plus court depuis l'angle courant
        yawTarget = yaw + Mathf.DeltaAngle(yaw, target);
    }

    public void Flash() => flash = 1f;
    public void Refuse() => danger = 1f;

    // =========================================================
    // CREATION
    // =========================================================

    public void Show(BlockDefinition def)
    {
        if (current != null)
            Destroy(current);
        holoRenderers.Clear();
        if (def == null || holoMat == null)
            return;

        radius = sizeMarker != null && anchor != null
            ? Vector3.Distance(sizeMarker.position, anchor.position) * sizeFactor
            : 0.03f;

        // Visuel a l'echelle 1 (une case = 1), centre sur le pivot, puis
        // reduit pour tenir dans la sphere de l'hologramme
        current = BlockPreviewFactory.CreateVisual(def, 1f, out _, fill: 1f, centerOnPivot: true);
        current.name = "Hologram_" + def.name;
        current.transform.SetParent(pivot.transform, false);
        current.transform.localPosition = Vector3.zero;
        current.transform.localRotation = Quaternion.identity;
        current.transform.localScale = Vector3.one;

        if (BlockPreviewFactory.TryGetBounds(current, out var bounds))
        {
            // bornes dans le repere du pivot (rotation actuelle neutralisee)
            Vector3 localCenter = pivot.transform.InverseTransformPoint(bounds.center);
            float worldScale = pivot.transform.lossyScale.x;
            float extent = bounds.extents.magnitude / Mathf.Max(worldScale, 1e-5f);
            float scale = radius / Mathf.Max(worldScale, 1e-5f) / Mathf.Max(extent, 1e-4f);
            current.transform.localScale = Vector3.one * scale;
            current.transform.localPosition = -localCenter * scale;
        }

        int layer = anchor.gameObject.layer;
        foreach (var renderer in current.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = holoMat;
            renderer.sharedMaterials = mats;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.gameObject.layer = layer;
            holoRenderers.Add(renderer);
        }
        AddEdges(current, layer);
        foreach (var collider in current.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        EnsureBeam();
        reveal = BlockforgeTheme.ReduceMotion ? 1f : 0f;
    }

    // Lignes lumineuses sur les aretes vives de chaque maillage du bloc
    private void AddEdges(GameObject root, int layer)
    {
        if (edgeMat == null)
            return;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null)
                continue;
            if (!EdgeCache.TryGetValue(mesh, out var edges))
            {
                edges = BuildEdgeMesh(mesh);
                EdgeCache[mesh] = edges;
            }
            if (edges == null)
                continue;
            var go = new GameObject("Edges", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            go.transform.SetParent(filter.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = edges;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = edgeMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // Aretes "dures" : entre deux faces dont les normales different de plus de 25
    // degres, ou bord libre. Sommets soudes par position (les modeles dupliquent
    // les sommets aux aretes vives).
    private static Mesh BuildEdgeMesh(Mesh source)
    {
        // Modeles des blocs importes en Read/Write (Setup Plier V2) ; sinon pas de lignes
        if (!source.isReadable)
            return null;
        Vector3[] verts;
        int[] tris;
        try
        {
            verts = source.vertices;
            tris = source.triangles;
        }
        catch (System.Exception)
        {
            return null; // maillage non lisible en build
        }
        if (tris.Length < 3)
            return null;

        var bounds = source.bounds;
        float quant = Mathf.Max(bounds.size.magnitude * 1e-4f, 1e-6f);
        var weld = new Dictionary<Vector3Int, int>();
        var welded = new List<Vector3>();
        var map = new int[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(verts[i].x / quant), Mathf.RoundToInt(verts[i].y / quant),
                                     Mathf.RoundToInt(verts[i].z / quant));
            if (!weld.TryGetValue(key, out int index))
            {
                index = welded.Count;
                weld[key] = index;
                welded.Add(verts[i]);
            }
            map[i] = index;
        }

        var edgeNormals = new Dictionary<long, (Vector3 n0, Vector3 n1, int count)>();
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int a = map[tris[t]], b = map[tris[t + 1]], c = map[tris[t + 2]];
            Vector3 n = Vector3.Cross(welded[b] - welded[a], welded[c] - welded[a]);
            if (n.sqrMagnitude < 1e-20f)
                continue;
            n.Normalize();
            AddEdge(edgeNormals, a, b, n);
            AddEdge(edgeNormals, b, c, n);
            AddEdge(edgeNormals, c, a, n);
        }

        float cosLimit = Mathf.Cos(25f * Mathf.Deg2Rad);
        var indices = new List<int>();
        foreach (var kv in edgeNormals)
        {
            var (n0, n1, count) = kv.Value;
            if (count == 1 || Vector3.Dot(n0, n1) < cosLimit)
            {
                indices.Add((int)(kv.Key >> 32));
                indices.Add((int)(kv.Key & 0xffffffff));
            }
        }
        if (indices.Count == 0 || indices.Count > 60000)
            return null;

        var mesh = new Mesh { name = source.name + "_HoloEdges" };
        if (welded.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(welded);
        var normals = new Vector3[welded.Count];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;
        mesh.normals = normals;
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddEdge(Dictionary<long, (Vector3, Vector3, int)> edges, int a, int b, Vector3 n)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (edges.TryGetValue(key, out var e))
            edges[key] = (e.Item1, e.Item3 == 1 ? n : e.Item2, e.Item3 + 1);
        else
            edges[key] = (n, n, 1);
    }

    // Cone de projection : de la lentille au bas de l'hologramme
    private void EnsureBeam()
    {
        if (lens == null || beamMat == null)
            return;
        if (beam == null)
        {
            beam = new GameObject("HologramBeam", typeof(MeshFilter), typeof(MeshRenderer));
            beam.layer = anchor.gameObject.layer;
            beam.transform.SetParent(anchor, false);
            var r = beam.GetComponent<MeshRenderer>();
            r.sharedMaterial = beamMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // Cone dans le repere de l'ancre : sommet tronque a la lentille, base autour du centre
        Vector3 lensLocal = anchor.InverseTransformPoint(lens.position);
        float length = lensLocal.magnitude;
        if (length < 1e-4f)
            return;
        float scale = Mathf.Max(anchor.lossyScale.x, 1e-5f);
        float r0 = radius * 0.45f / scale;   // a la lentille
        float r1 = radius * 1.05f / scale;   // a l'hologramme
        beamMesh = BuildCone(beamMesh, r0, r1, length, 24);
        beam.GetComponent<MeshFilter>().sharedMesh = beamMesh;
        beam.transform.localPosition = lensLocal;
        beam.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, -lensLocal.normalized);
    }

    private static Mesh BuildCone(Mesh mesh, float r0, float r1, float length, int sides)
    {
        if (mesh == null)
            mesh = new Mesh { name = "HologramBeam" };
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            var n = new Vector3(dir.x, dir.y, (r0 - r1) / length).normalized;
            verts.Add(dir * r0);
            verts.Add(dir * r1 + Vector3.forward * length);
            normals.Add(n);
            normals.Add(n);
            uvs.Add(new Vector2(i / (float)sides, 0f));
            uvs.Add(new Vector2(i / (float)sides, 1f));
        }
        for (int i = 0; i < sides; i++)
        {
            int b = i * 2;
            tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        }
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private void LateUpdate()
    {
        if (pivot == null || holoMat == null)
            return;

        float dt = Time.deltaTime;
        bool still = BlockforgeTheme.ReduceMotion;
        reveal = Mathf.MoveTowards(reveal, 1f, dt / Mathf.Max(0.01f, revealDuration));
        flash = Mathf.MoveTowards(flash, 0f, dt * 3.5f);
        danger = Mathf.MoveTowards(danger, 0f, dt * 2.5f);
        yaw = still ? yawTarget : Mathf.SmoothDampAngle(yaw, yawTarget, ref yawVelocity, yawSmoothTime);

        // Repere : haut de l'outil, face tournee vers le joueur
        Vector3 up = topMarker != null ? (topMarker.position - anchor.position) : anchor.up;
        up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.up;
        if (viewCamera == null)
            viewCamera = Camera.main;
        Vector3 toCamera = viewCamera != null ? viewCamera.transform.position - anchor.position : -anchor.forward;
        Vector3 facing = Vector3.ProjectOnPlane(toCamera, up);
        if (facing.sqrMagnitude < 1e-8f)
            facing = Vector3.ProjectOnPlane(anchor.forward, up);
        facing.Normalize();
        Vector3 side = Vector3.Cross(up, facing);

        float t = Time.time;
        float bob = still ? 0f : Mathf.Sin(t * bobSpeed) * bobAmplitude * radius;
        float sway = still ? 0f : Mathf.Sin(t * bobSpeed * 0.5f) * swayDegrees;
        // Materialisation : l'hologramme "monte" un peu en apparaissant
        float rise = (1f - Mathf.SmoothStep(0f, 1f, reveal)) * -0.25f * radius;

        Quaternion rotation = Quaternion.AngleAxis(-tiltDegrees, side)
                              * Quaternion.LookRotation(-facing, up)
                              * Quaternion.AngleAxis(yaw + sway, Vector3.up);
        pivot.transform.SetPositionAndRotation(anchor.position + up * (bob + rise), rotation);

        // Parametres du shader (positions absolues : HDRP les compare en monde)
        Color c = Color.Lerp(color, BlockforgeTheme.Danger, danger);
        float intensity = brightness * (1f + flash * 1.4f);
        holoMat.SetColor(ColorId, c);
        holoMat.SetFloat(IntensityId, intensity);
        holoMat.SetFloat(RevealId, Mathf.SmoothStep(0f, 1f, reveal));
        holoMat.SetVector(CenterId, pivot.transform.position);
        holoMat.SetVector(UpId, up);
        holoMat.SetFloat(HalfHeightId, radius);
        holoMat.SetFloat(LineDensityId, 9f / Mathf.Max(radius, 1e-4f));
        if (edgeMat != null)
        {
            edgeMat.SetColor(ColorId, c);
            edgeMat.SetFloat(IntensityId, intensity * edgeBrightness);
            edgeMat.SetFloat(RevealId, Mathf.SmoothStep(0f, 1f, reveal));
            edgeMat.SetVector(CenterId, pivot.transform.position);
            edgeMat.SetVector(UpId, up);
            edgeMat.SetFloat(HalfHeightId, radius);
            edgeMat.SetFloat(LineDensityId, 9f / Mathf.Max(radius, 1e-4f));
        }
        if (beamMat != null)
        {
            beamMat.SetColor(ColorId, c);
            beamMat.SetFloat(IntensityId, brightness * (1.1f + flash * 1.5f) * (0.4f + 0.6f * reveal));
            beamMat.SetVector(CenterId, pivot.transform.position);
            beamMat.SetVector(UpId, up);
            beamMat.SetFloat(HalfHeightId, radius);
            beamMat.SetFloat(LineDensityId, 9f / Mathf.Max(radius, 1e-4f));
        }
    }
}
