using UnityEngine;

// =========================================================
// GAINE DE PLASMA (entree atmospherique) SUR L'AVANT DU VAISSEAU
// Pose sur un point d'ancrage au centre de la face avant,
// oriente : +Z local = vers l'arriere (le long de la coque),
// X = travers, Y = haut. Construit a l'activation, en jeu comme
// dans l'editeur (apercu) :
// - « Shock » : calotte qui epouse toute la face avant (contour
//   en rectangle arrondi aux dimensions de la coque, un peu plus
//   large), bombee de quelques metres vers l'avant et rabattue
//   derriere l'arete ; texture radiale (centre chaud, bord en
//   compression), bords qui ondulent ;
// - « Ribbons » : langues de flamme contigues sur tout le
//   pourtour (elles se chevauchent a la base : gaine continue qui
//   s'effiloche vers l'arriere), couchees sur la coque, chacune
//   avec sa longueur, son ondulation et son defilement (UV qui
//   glissent : la texture est periodique dans la longueur) ;
// - « Filaments » : particules etirees arrachees au pourtour et
//   filant le long de la coque ; « Embers » : braises.
// intensity (0-1) allume l'ensemble progressivement ; boost (> 1)
// allonge et avive tout pendant une acceleration.
// Les enfants sont generes (non sauvegardes dans la scene).
// Setup : Blockforge > Setup Matchmaking Cinematic.
// =========================================================

[ExecuteAlways]
public class ReentryFlameFX : MonoBehaviour
{
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
    private const int CapRings = 10, CapSectors = 40;
    private const int RibbonSegments = 14;

    [Header("Face avant (m, rempli par le setup)")]
    public float faceWidth = 60f;
    public float faceHeight = 45f;
    [Tooltip("Rayon des coins du contour (rectangle arrondi)")]
    public float cornerRadius = 10f;
    [Tooltip("Marge de la gaine autour de la coque")]
    public float margin = 4f;

    [Header("Calotte (choc)")]
    public Material capMaterial;
    public Color capColor = new(1f, 0.8f, 0.5f);
    public float capGlow = 2f;
    [Tooltip("Bombement vers l'avant au centre / rabat derriere l'arete (m)")]
    public float capFront = 1.5f;
    public float capWrap = 10f;
    [Tooltip("Forme du contour : 2 = ellipse, 4 = rectangle arrondi")]
    public float capRoundness = 2f;
    public float capRipple = 0.22f;

    [Header("Rubans")]
    public Material ribbonMaterial;
    public Color ribbonColor = new(1f, 0.62f, 0.25f);
    public float ribbonGlow = 2.2f;
    public int ribbonCount = 56;
    public Vector2 ribbonLength = new(25f, 110f);
    [Tooltip("Largeur a la base, en multiple de l'espacement entre langues (> 1 : elles se chevauchent)")]
    public float ribbonOverlap = 1.8f;
    [Tooltip("Effilage : plus petit = les langues restent larges plus longtemps")]
    public float ribbonTaper = 0.6f;
    [Tooltip("Longueur d'une repetition de la texture le long du ruban (m) et vitesse de defilement (repetitions / s)")]
    public float ribbonTile = 40f;
    public float ribbonFlow = 2.5f;
    [Tooltip("Ondulation laterale (m) et ecartement de la coque en bout de ruban (m)")]
    public float ribbonWave = 3.5f;
    public float ribbonLift = 5f;
    public float flicker = 0.18f;
    public float flickerSpeed = 7f;

    [Header("Filaments et braises")]
    public Material streakMaterial;
    public float streakRate = 260f;
    public Vector2 streakSpeed = new(80f, 160f);
    public Material emberMaterial;
    public float emberRate = 100f;

    [Header("Etat")]
    [Range(0f, 1f)] public float intensity = 1f;
    [Tooltip("> 1 pendant une acceleration : rubans plus longs, tout plus vif")]
    public float boost = 1f;

    private Transform capRoot, ribbonRoot;
    private Mesh capMesh, ribbonMesh;
    private Renderer capRenderer, ribbonRenderer;
    private Vector3[] capVertices, ribbonVertices;
    private Vector2[] ribbonUvs;
    private ParticleSystem streaks, embers;
    private MaterialPropertyBlock block;
    private float seed;
    private bool rebuildRequested;

    // Par ruban : parametre sur le contour, longueur, largeur, phases
    private struct Ribbon { public float s, length, width, phase, speed, tilt, startZ; public Vector2 origin, normal; }
    private Ribbon[] ribbons;

    private void OnEnable()
    {
        seed = Random.value * 100f;
        block ??= new MaterialPropertyBlock();
        Build();
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
            rebuildRequested = true;
    }

    private void Update()
    {
        if (rebuildRequested)
        {
            rebuildRequested = false;
            Clear();
            Build();
        }

        float t = Application.isPlaying ? Time.time : (float)(System.DateTime.Now.TimeOfDay.TotalSeconds % 1000.0);
        float k = Mathf.Clamp01(intensity);
        float b = Mathf.Max(1f, boost);
        float fl = 1f + flicker * (Mathf.PerlinNoise(t * flickerSpeed, seed) * 2f - 1f);

        UpdateCap(t, k, b, fl);
        UpdateRibbons(t, k, b, fl);
        UpdateParticles(streaks, streakRate, k, b);
        UpdateParticles(embers, emberRate, k, b);
    }

    // =========================================================
    // CONTOUR DE LA FACE AVANT : rectangle arrondi, parametre s dans [0, 1)
    // =========================================================

    private float HalfW => faceWidth * 0.5f + margin;
    private float HalfH => faceHeight * 0.5f + margin;

    private float PerimeterLength
    {
        get
        {
            float hw = HalfW, hh = HalfH;
            float rc = Mathf.Min(cornerRadius + margin, hw, hh);
            return 4f * (hw - rc) + 4f * (hh - rc) + 2f * Mathf.PI * rc;
        }
    }

    // Point du contour et normale sortante (dans le plan XY)
    private void Outline(float s, out Vector2 point, out Vector2 normal)
    {
        float hw = HalfW, hh = HalfH;
        float rc = Mathf.Min(cornerRadius + margin, hw, hh);
        float straightW = 2f * (hw - rc), straightH = 2f * (hh - rc);
        float arc = 0.5f * Mathf.PI * rc;
        float total = 2f * straightW + 2f * straightH + 4f * arc;
        float d = Mathf.Repeat(s, 1f) * total;

        // Depart au milieu du bord haut, sens horaire vu de l'avant : haut, coin, droite, ...
        (Vector2 from, Vector2 dir, float len)[] edges =
        {
            (new Vector2(0f, hh), Vector2.right, straightW * 0.5f),
            (new Vector2(hw - rc, hh), Vector2.right, 0f), // coin haut-droit (arc)
            (new Vector2(hw, hh - rc), Vector2.down, straightH),
            (new Vector2(hw, -hh + rc), Vector2.down, 0f), // coin bas-droit
            (new Vector2(hw - rc, -hh), Vector2.left, straightW),
            (new Vector2(-hw + rc, -hh), Vector2.left, 0f), // coin bas-gauche
            (new Vector2(-hw, -hh + rc), Vector2.up, straightH),
            (new Vector2(-hw, hh - rc), Vector2.up, 0f), // coin haut-gauche
            (new Vector2(-hw + rc, hh), Vector2.right, straightW * 0.5f),
        };
        Vector2[] corners = { new(hw - rc, hh - rc), new(hw - rc, -hh + rc), new(-hw + rc, -hh + rc), new(-hw + rc, hh - rc) };
        float[] startAngle = { 90f, 0f, -90f, 180f };

        int cornerIndex = 0;
        foreach (var e in edges)
        {
            if (e.len > 0f)
            {
                if (d <= e.len)
                {
                    point = e.from + e.dir * d;
                    normal = new Vector2(-e.dir.y, e.dir.x); // sortante (parcours horaire, Y vers le haut)
                    return;
                }
                d -= e.len;
            }
            else
            {
                if (d <= arc)
                {
                    float a = (startAngle[cornerIndex] - 90f * (d / arc)) * Mathf.Deg2Rad;
                    normal = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    point = corners[cornerIndex] + normal * rc;
                    return;
                }
                d -= arc;
                cornerIndex++;
            }
        }
        point = new Vector2(0f, hh);
        normal = Vector2.up;
    }

    // =========================================================
    // CALOTTE
    // =========================================================

    private void BuildCap()
    {
        if (capMaterial == null)
            return;
        int count = 1 + CapRings * CapSectors;
        capVertices = new Vector3[count];
        var uvs = new Vector2[count];
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int r = 1; r <= CapRings; r++)
        {
            float f = r / (float)CapRings;
            for (int sct = 0; sct < CapSectors; sct++)
            {
                float a = sct / (float)CapSectors * 2f * Mathf.PI;
                uvs[1 + (r - 1) * CapSectors + sct] = new Vector2(0.5f + 0.5f * f * Mathf.Cos(a), 0.5f + 0.5f * f * Mathf.Sin(a));
            }
        }
        var tris = new System.Collections.Generic.List<int>();
        for (int sct = 0; sct < CapSectors; sct++)
        {
            int a = 1 + sct, bIdx = 1 + (sct + 1) % CapSectors;
            tris.Add(0); tris.Add(bIdx); tris.Add(a);
        }
        for (int r = 1; r < CapRings; r++)
        {
            for (int sct = 0; sct < CapSectors; sct++)
            {
                int i0 = 1 + (r - 1) * CapSectors + sct, i1 = 1 + (r - 1) * CapSectors + (sct + 1) % CapSectors;
                int o0 = i0 + CapSectors, o1 = i1 + CapSectors;
                tris.Add(i0); tris.Add(i1); tris.Add(o0);
                tris.Add(i1); tris.Add(o1); tris.Add(o0);
            }
        }
        capMesh = new Mesh { name = "ReentryCap", hideFlags = HideFlags.DontSave };
        capMesh.MarkDynamic();
        capMesh.vertices = capVertices;
        capMesh.uv = uvs;
        capMesh.triangles = tris.ToArray();
        var go = CreateChild("Shock");
        capRoot = go.transform;
        capRenderer = AddRenderer(go, capMesh, capMaterial);
    }

    private void UpdateCap(float t, float k, float b, float fl)
    {
        if (capRoot == null)
            return;
        capRenderer.enabled = k > 0.001f;
        if (!capRenderer.enabled)
            return;
        float spread = 0.6f + 0.4f * k;
        capVertices[0] = new Vector3(0f, 0f, -capFront * spread);
        for (int r = 1; r <= CapRings; r++)
        {
            float f = r / (float)CapRings;
            for (int sct = 0; sct < CapSectors; sct++)
            {
                float a = sct / (float)CapSectors * 2f * Mathf.PI;
                // Contour en superellipse (rectangle arrondi) etire par f
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float n = Mathf.Max(2f, capRoundness);
                float den = Mathf.Pow(Mathf.Pow(Mathf.Abs(c), n) + Mathf.Pow(Mathf.Abs(s), n), 1f / n);
                // Bord irregulier : deux ondulations qui tournent en sens inverse, plus fortes vers le bord
                float n1 = Mathf.PerlinNoise(a * 0.9f + t * 0.7f, seed + f * 2f) * 2f - 1f;
                float n2 = Mathf.PerlinNoise(a * 2.3f - t * 1.6f, seed + 5f + f) * 2f - 1f;
                float ripple = 1f + capRipple * f * f * (0.7f * n1 + 0.5f * n2);
                float x = HalfW * f * c / den * ripple * spread;
                float y = HalfH * f * s / den * ripple * spread;
                float z = Mathf.Lerp(-capFront * spread, capWrap, f * f);
                capVertices[1 + (r - 1) * CapSectors + sct] = new Vector3(x, y, z);
            }
        }
        capMesh.vertices = capVertices;
        capMesh.RecalculateBounds();
        SetEmission(capRenderer, capColor * (capGlow * k * fl * Mathf.Sqrt(b)));
    }

    // =========================================================
    // RUBANS
    // =========================================================

    private void BuildRibbons()
    {
        if (ribbonMaterial == null || ribbonCount <= 0)
            return;
        var rng = new System.Random(Mathf.RoundToInt(seed * 1000f));
        float Next(float a, float c) => a + (float)rng.NextDouble() * (c - a);
        float spacing = PerimeterLength / ribbonCount;
        ribbons = new Ribbon[ribbonCount];
        for (int i = 0; i < ribbonCount; i++)
        {
            ribbons[i] = new Ribbon
            {
                s = (i + Next(-0.15f, 0.15f)) / ribbonCount,
                length = Next(ribbonLength.x, ribbonLength.y),
                width = spacing * ribbonOverlap * Next(0.8f, 1.2f),
                phase = Next(0f, 100f),
                speed = Next(0.7f, 1.4f),
                tilt = Next(-6f, 6f) * Mathf.Deg2Rad,
                startZ = Next(0f, 9f), // base desalignee, toujours sur la coque (jamais devant la face)
            };
            Outline(ribbons[i].s, out ribbons[i].origin, out ribbons[i].normal);
        }

        int perRibbon = (RibbonSegments + 1) * 2;
        ribbonVertices = new Vector3[ribbonCount * perRibbon];
        ribbonUvs = new Vector2[ribbonVertices.Length];
        var tris = new int[ribbonCount * RibbonSegments * 6];
        int k = 0;
        for (int i = 0; i < ribbonCount; i++)
        {
            int start = i * perRibbon;
            for (int j = 0; j < RibbonSegments; j++)
            {
                int a = start + j * 2;
                tris[k++] = a; tris[k++] = a + 2; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = a + 2; tris[k++] = a + 3;
            }
        }
        ribbonMesh = new Mesh { name = "ReentryRibbons", hideFlags = HideFlags.DontSave };
        ribbonMesh.MarkDynamic();
        ribbonMesh.vertices = ribbonVertices;
        ribbonMesh.uv = ribbonUvs;
        ribbonMesh.triangles = tris;
        var go = CreateChild("Ribbons");
        ribbonRoot = go.transform;
        ribbonRenderer = AddRenderer(go, ribbonMesh, ribbonMaterial);
    }

    private void UpdateRibbons(float t, float k, float b, float fl)
    {
        if (ribbonRoot == null)
            return;
        ribbonRenderer.enabled = k > 0.001f;
        if (!ribbonRenderer.enabled)
            return;

        float lengthScale = (0.4f + 0.6f * k) * (0.7f + 0.3f * b);
        float widthScale = 0.5f + 0.5f * k;
        int perRibbon = (RibbonSegments + 1) * 2;
        for (int i = 0; i < ribbons.Length; i++)
        {
            var rb = ribbons[i];
            Vector2 p0 = rb.origin, n2 = rb.normal;
            var n = new Vector3(n2.x, n2.y, 0f);
            var tangent = new Vector3(-n2.y, n2.x, 0f);
            // Le ruban est couche sur la coque (plan tangent), legerement incline
            var across = (tangent * Mathf.Cos(rb.tilt) + n * Mathf.Sin(rb.tilt)).normalized;

            float pulse = 1f + 0.3f * (Mathf.PerlinNoise(t * 1.7f * rb.speed, rb.phase) * 2f - 1f);
            float length = rb.length * lengthScale * pulse;
            float flow = t * ribbonFlow * rb.speed * b;
            int start = i * perRibbon;
            for (int j = 0; j <= RibbonSegments; j++)
            {
                float v = j / (float)RibbonSegments;
                float z = v * length;
                // Decolle un peu de la coque en s'eloignant, ondule lateralement de plus en plus
                float lift = ribbonLift * v * v;
                float wave = ribbonWave * v * Mathf.Sin(v * 5f + t * 3f * rb.speed + rb.phase);
                var center = new Vector3(p0.x, p0.y, rb.startZ) + n * lift + tangent * wave + Vector3.forward * z;
                float half = rb.width * widthScale * 0.5f * Mathf.Pow(1f - v, Mathf.Max(0.2f, ribbonTaper));
                ribbonVertices[start + j * 2] = center - across * half;
                ribbonVertices[start + j * 2 + 1] = center + across * half;
                float uvV = z / ribbonTile - flow;
                ribbonUvs[start + j * 2] = new Vector2(0f, uvV);
                ribbonUvs[start + j * 2 + 1] = new Vector2(1f, uvV);
            }
        }
        ribbonMesh.vertices = ribbonVertices;
        ribbonMesh.uv = ribbonUvs;
        ribbonMesh.RecalculateBounds();
        SetEmission(ribbonRenderer, ribbonColor * (ribbonGlow * k * fl * Mathf.Sqrt(b)));
    }

    // =========================================================
    // PARTICULES, CONSTRUCTION, OUTILS
    // =========================================================

    private static void UpdateParticles(ParticleSystem system, float rate, float k, float b)
    {
        if (system == null)
            return;
        var emission = system.emission;
        emission.rateOverTime = rate * k * b;
        var main = system.main;
        main.simulationSpeed = b;
    }

    private void Build()
    {
        BuildCap();
        BuildRibbons();
        streaks = BuildParticles("Filaments", streakMaterial, streakRate, streakSpeed, new Vector2(0.5f, 1f),
                                 new Vector2(1.2f, 2.6f), 0.06f, 1400, box: true);
        embers = BuildParticles("Embers", emberMaterial, emberRate, new Vector2(100f, 220f), new Vector2(0.6f, 1.3f),
                                new Vector2(0.8f, 2f), 0.05f, 600, box: false);
    }

    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if ((child.hideFlags & HideFlags.DontSave) == 0)
                continue;
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
        capRoot = ribbonRoot = null;
        capMesh = ribbonMesh = null;
        capRenderer = ribbonRenderer = null;
        streaks = embers = null;
    }

    private GameObject CreateChild(string childName)
    {
        var go = new GameObject(childName) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        return go;
    }

    private static MeshRenderer AddRenderer(GameObject go, Mesh mesh, Material material)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return renderer;
    }

    private ParticleSystem BuildParticles(string childName, Material material, float rate, Vector2 speed, Vector2 life,
                                          Vector2 size, float velocityScale, int max, bool box)
    {
        if (material == null)
            return null;
        var go = CreateChild(childName);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = max;

        var emission = system.emission;
        emission.rateOverTime = rate;

        var shape = system.shape;
        if (box)
        {
            // Toute la face avant, emis vers +Z (l'arriere)
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(faceWidth + 2f * margin, faceHeight + 2f * margin, 2f);
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = Mathf.Min(HalfW, HalfH) * 0.8f;
        }

        // HDRP/Unlit ignore la couleur des particules : elles s'eteignent en retrecissant
        var sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = velocityScale;
        renderer.lengthScale = 1f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        system.Play();
        return system;
    }

    private void SetEmission(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;
        renderer.GetPropertyBlock(block);
        block.SetColor(EmissiveColorId, color);
        renderer.SetPropertyBlock(block);
    }
}
