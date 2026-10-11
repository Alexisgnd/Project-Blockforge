using System.Collections.Generic;
using UnityEngine;

// =========================================================
// EFFETS DU PISTOLET A PEINTURE (v3)
// - Teinte : liquide, bulles, sortie de buse et elements
//   lumineux (anneau de buse, bandeaux, LED de l'index) suivent
//   la couleur de la molette, avec un fondu court.
// - Bulles : remontent en continu dans le reservoir (vitesse,
//   taille et oscillation aleatoires), grossissent en montant et
//   eclatent a la surface ; une gerbe part a chaque cran de
//   molette et a chaque coup de peinture.
// - Houle : la surface du liquide reste a peu pres horizontale
//   quand le pistolet s'incline et ballotte avec les mouvements
//   du joueur (4 shape keys Slosh_* du liquide, ressort amorti).
// - Jet : tant que la gachette est tenue, un filet continu de
//   peinture part de la buse vers le point vise. C'est un tube
//   lisse (maillage reconstruit a chaque image) qui suit des
//   points emis a chaque image et lances sur une trajectoire
//   legerement balistique arrivant pile sur la cible : quand on
//   bouge le viseur, le filet ondule comme un jet de tuyau ; quand
//   on relache, la queue se detache et file jusqu'a la cible.
//   Une fine bruine de gouttelettes accompagne le filet et
//   rebondit au point d'impact. TravelTime donne le temps de vol,
//   Splash l'eclaboussure quand un bloc change de couleur.
// - (ancien) Pulverisation : gouttelettes de peinture projetees depuis la
//   buse a chaque bloc peint (Spray), anneau de buse plus vif.
// Les reperes (Bubble_Spawn, Liquid_Top, Bubble_Radius) viennent
// du modele : rien n'est code en dur sur les axes de l'export.
// =========================================================

public class PaintSprayFX : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public PaintSprayColorWheel wheel;
    public SkinnedMeshRenderer liquid;     // Paint_Liquid
    public Renderer outlet;                // Paint_Outlet
    public Renderer[] glows;               // Nozzle_Glow, Status_Strip(_2), Marker_Led(_2)
    public Transform bubbleTemplate;       // Bubble_Template
    public Transform bubbleSpawn;          // Bubble_Spawn
    public Transform bubbleRadius;         // Bubble_Radius
    public Transform liquidTop;            // Liquid_Top

    [Header("Bulles")]
    public int poolSize = 28;
    [Tooltip("Bulles par seconde au repos")]
    public float idleRate = 7f;
    public Vector2 riseSpeed = new(0.035f, 0.075f);   // m/s (echelle du modele)
    public Vector2 bubbleScale = new(0.45f, 1.25f);   // x la bulle modele
    public int burstOnNotch = 6;
    public int burstOnSpray = 9;

    [Header("Houle")]
    [Range(0f, 1f)] public float levelKeeping = 0.55f;
    public float inertia = 0.010f;
    public float sloshStiffness = 70f;
    public float sloshDamping = 5.5f;

    [Header("Teinte")]
    public float glowNits = 900f;
    public float liquidNits = 4f;
    public float tintDuration = 0.22f;

    [Header("Jet de peinture")]
    [Tooltip("Vitesse du jet (m/s) : fixe le temps de vol jusqu'a la cible")]
    public float jetSpeed = 14f;
    [Tooltip("Gouttelettes de bruine par seconde autour du filet")]
    public float jetRate = 24f;
    [Tooltip("Rayon du filet de peinture (m, monde)")]
    public float jetRadius = 0.025f;
    [Tooltip("Dispersion de la bruine a l'arrivee (m)")]
    public float jetSpread = 0.05f;
    [Tooltip("Cotes du tube du filet")]
    public int streamSides = 10;
    [Tooltip("Part de la gravite appliquee aux gouttes (courbe du jet)")]
    public float jetGravity = 0.35f;
    public int splashDroplets = 12;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    private Material liquidMat, bubbleMat, glowMat, dropletMat;
    private Color fromColor, toColor, currentColor;
    private float tintT = 1f;
    private float glowBoost;

    private struct Bubble
    {
        public Transform tr;
        public float height, speed, size, phase, wobble, angle, radius, pop;
        public bool alive;
    }

    private Bubble[] bubbles;
    private float spawnDebt;
    private float bubbleBaseScale;

    private struct Droplet
    {
        public Transform tr;
        public Vector3 velocity;
        public float age, life, size, gravity, drag;
        public bool streak;
    }

    private float jetDebt;
    private bool firingSolvent;
    private Material solventMat;
    private float meshRadius = -1f;

    // Filet continu : points emis a chaque image, du plus ancien au plus recent
    private struct StreamPoint
    {
        public Vector3 pos, velocity;
        public float age, life, phase;
        public int segment;
        public bool solvent;
    }

    private readonly List<StreamPoint> stream = new();
    private int streamSegment;
    private bool firedThisFrame, firedLastFrame, lastSolvent;
    private Mesh streamMesh;
    private GameObject streamGo;
    private MeshRenderer streamRenderer;
    private readonly List<Vector3> sVerts = new();
    private readonly List<Vector3> sNormals = new();
    private readonly List<int> sTrisPaint = new();
    private readonly List<int> sTrisSolvent = new();
    private readonly List<Vector3> sPath = new();
    private readonly List<float> sRadius = new();

    private readonly List<Droplet> droplets = new();
    private readonly Stack<Transform> dropletPool = new();
    private Transform dropletRoot;

    // Houle
    private int[] slosh = new int[4];          // PX, NX, PY, NY
    private Vector3[] sloshDir = new Vector3[4]; // direction de montee de chaque shape (repere du liquide)
    private float sloshWeightMax = 100f;         // poids maxi : surface inclinee d'environ 15 degres
    private Vector2 tilt, tiltVelocity;
    private Vector3 lastPos, lastVelocity;
    private bool hasLast;

    // =====================================================
    // CYCLE DE VIE
    // =====================================================

    private void Awake()
    {
        CreateMaterials();
        BuildBubbles();
        PrepareSlosh();
        currentColor = toColor = fromColor = wheel != null ? wheel.SelectedColor : new Color(0.85f, 0.13f, 0.13f);
        ApplyTint(currentColor, 0f);
    }

    private void OnEnable()
    {
        if (wheel != null)
            wheel.ColorChanged += OnColorChanged;
        hasLast = false;
    }

    private void OnDisable()
    {
        if (wheel != null)
            wheel.ColorChanged -= OnColorChanged;
        // Les gouttelettes sont dans le monde : on les range
        foreach (var d in droplets)
            Recycle(d.tr);
        droplets.Clear();
        stream.Clear();
        firedThisFrame = firedLastFrame = false;
        if (streamGo != null)
            streamGo.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (var m in new[] { liquidMat, bubbleMat, glowMat, dropletMat, solventMat })
            if (m != null) Destroy(m);
        if (dropletRoot != null)
            Destroy(dropletRoot.gameObject);
        if (streamGo != null)
            Destroy(streamGo);
        if (streamMesh != null)
            Destroy(streamMesh);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateTint(dt);
        UpdateBubbles(dt);
        UpdateSlosh(dt);
        UpdateDroplets(dt);
        UpdateStream(dt);
    }

    // Le filet est construit apres les mouvements du pistolet (buse a jour)
    private void LateUpdate()
    {
        BuildStreamMesh();
        firedLastFrame = firedThisFrame;
        firedThisFrame = false;
    }

    // =====================================================
    // API
    // =====================================================

    // Sortie de la buse (monde)
    public Vector3 Nozzle => outlet != null ? outlet.bounds.center : transform.position;

    // Temps de vol du jet jusqu'a un point
    public float TravelTime(Vector3 target)
    {
        return Mathf.Max(0.05f, Vector3.Distance(Nozzle, target) / Mathf.Max(jetSpeed, 0.1f));
    }

    // Gachette tenue : emet le jet vers 'target' pendant dt
    // solvent = jet de diluant (clic droit) : filet blanc
    public void Fire(Vector3 target, float dt, bool solvent = false)
    {
        firingSolvent = solvent;
        glowBoost = Mathf.Max(glowBoost, 0.6f);
        if (outlet == null)
            return;

        // Nouveau filet si on vient d'appuyer ou de changer de bouton
        if (!firedLastFrame || solvent != lastSolvent)
            streamSegment++;
        lastSolvent = solvent;
        firedThisFrame = true;

        Vector3 origin = Nozzle;
        float t = TravelTime(target);
        stream.Add(new StreamPoint
        {
            pos = origin,
            velocity = LaunchVelocity(origin, target, t),
            life = t,
            phase = Random.value * 10f,
            segment = streamSegment,
            solvent = solvent,
        });

        // Bruine autour du filet
        if (BlockforgeTheme.ReduceMotion)
            return;
        jetDebt += jetRate * dt;
        int n = Mathf.Min((int)jetDebt, 6);
        jetDebt -= (int)jetDebt;
        for (int i = 0; i < n; i++)
        {
            Vector3 end = target + Random.insideUnitSphere * jetSpread;
            float tm = TravelTime(end) * Random.Range(0.9f, 1.1f);
            Spawn(origin, LaunchVelocity(origin, end, tm), tm, jetRadius * Random.Range(0.25f, 0.45f), jetGravity, 0f, true);
        }
    }

    // Vitesse initiale pour arriver pile en 'end' au bout de t (gravite reduite)
    private Vector3 LaunchVelocity(Vector3 origin, Vector3 end, float t)
    {
        return (end - origin) / t - 0.5f * jetGravity * t * Physics.gravity;
    }

    // Impact : eclaboussure vers 'away' (normale approximative), gerbe dans le reservoir
    public void Splash(Vector3 point, Vector3 away)
    {
        glowBoost = 1f;
        Burst(burstOnSpray);
        if (BlockforgeTheme.ReduceMotion)
            return;
        away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.up;
        for (int i = 0; i < splashDroplets; i++)
        {
            Vector3 dir = (away + Random.insideUnitSphere * 0.9f).normalized;
            Spawn(point, dir * Random.Range(0.8f, 2.2f), Random.Range(0.18f, 0.32f),
                  jetRadius * Random.Range(0.35f, 0.8f), 1f, 3f, false);
        }
    }

    private void OnColorChanged(Color color, int index)
    {
        Burst(burstOnNotch);
    }

    // =====================================================
    // TEINTE
    // =====================================================

    private void CreateMaterials()
    {
        if (liquid != null && liquid.sharedMaterial != null)
        {
            liquidMat = new Material(liquid.sharedMaterial) { name = "Spray_Liquid (runtime)" };
            liquid.sharedMaterial = liquidMat;
        }
        if (outlet != null && liquidMat != null)
            outlet.sharedMaterial = liquidMat;

        if (bubbleTemplate != null && bubbleTemplate.TryGetComponent<Renderer>(out var br) && br.sharedMaterial != null)
        {
            bubbleMat = new Material(br.sharedMaterial) { name = "Spray_Bubbles (runtime)" };
            br.sharedMaterial = bubbleMat;
            dropletMat = new Material(liquidMat != null ? liquidMat : br.sharedMaterial) { name = "Spray_Droplets (runtime)" };
            solventMat = new Material(br.sharedMaterial) { name = "Spray_Solvent (runtime)" };
            Tint(solventMat, new Color(0.9f, 0.95f, 1f), liquidNits);
        }

        if (glows != null && glows.Length > 0 && glows[0] != null)
        {
            glowMat = new Material(glows[0].sharedMaterial) { name = "Spray_Glow (runtime)" };
            foreach (var g in glows)
                if (g != null) g.sharedMaterial = glowMat;
        }
    }

    private void UpdateTint(float dt)
    {
        // Couleur de la molette suivie en continu (l'evenement peut partir
        // avant l'abonnement selon l'ordre d'initialisation des composants)
        if (wheel != null && wheel.SelectedColor != toColor)
        {
            fromColor = currentColor;
            toColor = wheel.SelectedColor;
            tintT = 0f;
        }
        glowBoost = Mathf.MoveTowards(glowBoost, 0f, dt * 3f);
        if (tintT < 1f)
        {
            tintT = tintDuration <= 0f || BlockforgeTheme.ReduceMotion ? 1f : Mathf.Min(1f, tintT + dt / tintDuration);
            currentColor = Color.Lerp(fromColor, toColor, BlockforgeTheme.EaseOut(tintT));
        }
        ApplyTint(currentColor, glowBoost);
    }

    private void ApplyTint(Color c, float boost)
    {
        Color light = Color.Lerp(c, Color.white, 0.45f);
        Tint(liquidMat, c, liquidNits);
        Tint(dropletMat, c, liquidNits);
        Tint(bubbleMat, light, liquidNits * 0.8f);
        if (glowMat != null)
        {
            glowMat.SetColor(BaseColorId, c);
            glowMat.SetColor(EmissiveColorId, c * glowNits * (1f + boost * 1.5f));
        }
    }

    private static void Tint(Material mat, Color rgb, float nits)
    {
        if (mat == null)
            return;
        float alpha = mat.GetColor(BaseColorId).a;
        mat.SetColor(BaseColorId, new Color(rgb.r, rgb.g, rgb.b, alpha));
        mat.SetColor(EmissiveColorId, rgb * nits);
    }

    // =====================================================
    // BULLES (repere du parent de la bulle modele = reservoir)
    // =====================================================

    private Vector3 SpawnLocal => bubbleTemplate.parent.InverseTransformPoint(bubbleSpawn.position);
    private Vector3 TopLocal => bubbleTemplate.parent.InverseTransformPoint(liquidTop.position);

    private void BuildBubbles()
    {
        if (bubbleTemplate == null || bubbleSpawn == null || liquidTop == null || bubbleRadius == null)
        {
            bubbles = new Bubble[0];
            return;
        }
        bubbleBaseScale = bubbleTemplate.localScale.x;
        bubbleTemplate.gameObject.SetActive(false);
        bubbles = new Bubble[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var clone = Instantiate(bubbleTemplate.gameObject, bubbleTemplate.parent, false);
            clone.name = $"Bubble_{i:00}";
            clone.SetActive(false);
            bubbles[i].tr = clone.transform;
        }
        // Reservoir deja vivant a l'ouverture : bulles reparties sur la hauteur
        for (int i = 0; i < poolSize / 3; i++)
            Spawn(Random.value);
    }

    private void Burst(int count)
    {
        for (int i = 0; i < count; i++)
            Spawn(Random.Range(0f, 0.15f), fast: true);
    }

    private void Spawn(float startHeight, bool fast = false)
    {
        for (int i = 0; i < bubbles.Length; i++)
        {
            if (bubbles[i].alive)
                continue;
            ref var b = ref bubbles[i];
            b.alive = true;
            b.height = startHeight;
            b.speed = Random.Range(riseSpeed.x, riseSpeed.y) * (fast ? 1.6f : 1f);
            b.size = Random.Range(bubbleScale.x, bubbleScale.y) * (fast ? 0.7f : 1f);
            b.phase = Random.value * Mathf.PI * 2f;
            b.wobble = Random.Range(0.15f, 0.45f);
            b.angle = Random.value * Mathf.PI * 2f;
            b.radius = Mathf.Sqrt(Random.value) * 0.85f;
            b.pop = 0f;
            b.tr.gameObject.SetActive(true);
            return;
        }
    }

    private void UpdateBubbles(float dt)
    {
        if (bubbles.Length == 0)
            return;

        Vector3 spawn = SpawnLocal;
        Vector3 axis = TopLocal - spawn;
        float length = axis.magnitude;
        if (length < 1e-5f)
            return;
        Vector3 up = axis / length;
        Vector3 radial = bubbleTemplate.parent.InverseTransformPoint(bubbleRadius.position) - spawn;
        float radiusMax = Vector3.ProjectOnPlane(radial, up).magnitude;
        Vector3 u = Vector3.ProjectOnPlane(radial, up).normalized;
        Vector3 v = Vector3.Cross(up, u);

        spawnDebt += idleRate * dt;
        while (spawnDebt >= 1f)
        {
            spawnDebt -= 1f;
            Spawn(0f);
        }

        float time = Time.time;
        for (int i = 0; i < bubbles.Length; i++)
        {
            ref var b = ref bubbles[i];
            if (!b.alive)
                continue;

            if (b.pop > 0f)
            {
                // Eclatement a la surface : gonfle puis disparait
                b.pop += dt / 0.09f;
                if (b.pop >= 1f)
                {
                    b.alive = false;
                    b.tr.gameObject.SetActive(false);
                    continue;
                }
                b.tr.localScale = Vector3.one * (bubbleBaseScale * b.size * (1f + b.pop * 0.6f) * (1f - b.pop));
                continue;
            }

            // Acceleration en montant (les bulles grossissent et filent)
            b.height += b.speed * (0.7f + b.height * 0.8f) * dt / length;
            float wob = Mathf.Sin(time * 9f + b.phase) * b.wobble * 0.18f;
            float a = b.angle + Mathf.Sin(time * 2.3f + b.phase) * 0.6f;
            float r = Mathf.Clamp01(b.radius + wob) * radiusMax;
            float h = Mathf.Min(b.height, 1f);
            // la surface penche avec la houle : les bulles eclatent a sa hauteur locale
            float surface = 1f - SurfaceDrop(Mathf.Cos(a) * r / Mathf.Max(radiusMax, 1e-5f), Mathf.Sin(a) * r / Mathf.Max(radiusMax, 1e-5f));
            b.tr.localPosition = spawn + up * (h * length) + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r;
            float grow = Mathf.Lerp(0.7f, 1.15f, h);
            b.tr.localScale = Vector3.one * (bubbleBaseScale * b.size * grow);
            if (b.height >= surface)
                b.pop = 0.0001f;
        }
    }

    // Abaissement relatif de la surface (0..~0.15) au point (x, y) normalise, selon la houle
    private float SurfaceDrop(float x, float y)
    {
        return Mathf.Max(0f, -(tilt.x * x + tilt.y * y)) * 0.15f;
    }

    // =====================================================
    // HOULE
    // =====================================================

    private void PrepareSlosh()
    {
        if (liquid == null || liquid.sharedMesh == null)
            return;
        var mesh = liquid.sharedMesh;
        if (!mesh.isReadable)
        {
            Debug.LogWarning("[PaintSprayFX] Maillage du liquide non lisible (Read/Write) : houle desactivee. " +
                             "Relance Blockforge > Setup Paint Spray.");
            slosh[0] = -1;
            return;
        }
        string[] names = { "Slosh_PX", "Slosh_NX", "Slosh_PY", "Slosh_NY" };
        var verts = mesh.vertices;
        var deltas = new Vector3[verts.Length];
        // axe du reservoir dans le repere du maillage du liquide
        Vector3 upLocal = liquidTop != null && bubbleSpawn != null
            ? liquid.transform.InverseTransformDirection(liquidTop.position - bubbleSpawn.position).normalized
            : Vector3.up;
        float maxDelta = 0f;
        Vector3 centroid = Vector3.zero;
        foreach (var p in verts)
            centroid += p;
        centroid /= Mathf.Max(1, verts.Length);
        for (int k = 0; k < 4; k++)
        {
            slosh[k] = mesh.GetBlendShapeIndex(names[k]);
            sloshDir[k] = Vector3.zero;
            if (slosh[k] < 0)
                continue;
            mesh.GetBlendShapeFrameVertices(slosh[k], 0, deltas, null, null);
            // direction ou la surface monte : positions ponderees par le deplacement le long de l'axe
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < verts.Length; i++)
                sum += Vector3.ProjectOnPlane(verts[i] - centroid, upLocal) * Vector3.Dot(deltas[i], upLocal);
            sloshDir[k] = sum;
            maxDelta = Mathf.Max(maxDelta, MaxAlong(deltas, upLocal));
        }

        // Amplitude independante de l'echelle d'import : au poids maxi, le bord
        // monte de 25 % du rayon du liquide (~15 degres)
        float radius = Vector3.ProjectOnPlane(mesh.bounds.extents, upLocal).magnitude / 1.41421f;
        if (maxDelta > 1e-6f && radius > 1e-6f)
            sloshWeightMax = Mathf.Clamp(100f * (0.25f * radius) / maxDelta, 0f, 100f);
        Debug.Log($"[PaintSprayFX] Houle du liquide : deplacement maxi {maxDelta:0.####}, rayon {radius:0.####}, " +
                  $"poids maxi {sloshWeightMax:0.#}.");
    }

    private static float MaxAlong(Vector3[] deltas, Vector3 axis)
    {
        float m = 0f;
        foreach (var d in deltas)
            m = Mathf.Max(m, Mathf.Abs(Vector3.Dot(d, axis)));
        return m;
    }

    private void UpdateSlosh(float dt)
    {
        if (liquid == null || slosh[0] < 0 || dt <= 0f)
            return;

        // Axes du reservoir en monde
        Vector3 axisW = (liquidTop.position - bubbleSpawn.position).normalized;
        Vector3 xW = Vector3.ProjectOnPlane(liquid.transform.TransformDirection(sloshDir[0] - sloshDir[1]), axisW).normalized;
        Vector3 yW = Vector3.ProjectOnPlane(liquid.transform.TransformDirection(sloshDir[2] - sloshDir[3]), axisW).normalized;

        // Pesanteur apparente = gravite - acceleration du pistolet : la surface
        // cherche a lui rester perpendiculaire (horizontale au repos, elle monte
        // a l'arriere quand on accelere). Elle monte du cote ou elle "tombe".
        Vector3 pos = transform.position;
        Vector3 vel = hasLast ? (pos - lastPos) / dt : Vector3.zero;
        Vector3 acc = hasLast ? (vel - lastVelocity) / dt : Vector3.zero;
        lastPos = pos;
        lastVelocity = vel;
        hasLast = true;
        acc = Vector3.ClampMagnitude(acc, 40f);
        Vector3 apparent = Physics.gravity * levelKeeping - acc * inertia * 100f;
        Vector3 fall = Vector3.ProjectOnPlane(apparent.normalized, axisW);
        Vector2 target = Vector2.ClampMagnitude(new Vector2(Vector3.Dot(fall, xW), Vector3.Dot(fall, yW)) * 3f, 1f);
        if (BlockforgeTheme.ReduceMotion)
        {
            tilt = target;
            tiltVelocity = Vector2.zero;
        }
        else
        {
            tiltVelocity += ((target - tilt) * sloshStiffness - tiltVelocity * sloshDamping) * dt;
            tilt += tiltVelocity * dt;
            tilt = Vector2.ClampMagnitude(tilt, 1f);
        }

        liquid.SetBlendShapeWeight(slosh[0], Mathf.Max(0f, tilt.x) * sloshWeightMax);
        liquid.SetBlendShapeWeight(slosh[1], Mathf.Max(0f, -tilt.x) * sloshWeightMax);
        liquid.SetBlendShapeWeight(slosh[2], Mathf.Max(0f, tilt.y) * sloshWeightMax);
        liquid.SetBlendShapeWeight(slosh[3], Mathf.Max(0f, -tilt.y) * sloshWeightMax);
    }

    // =====================================================
    // FILET CONTINU
    // =====================================================

    private void UpdateStream(float dt)
    {
        for (int i = stream.Count - 1; i >= 0; i--)
        {
            var p = stream[i];
            p.age += dt;
            if (p.age >= p.life)
            {
                // Arrivee : quelques gouttelettes rebondissent au point d'impact
                if (!BlockforgeTheme.ReduceMotion && Random.value < 0.35f)
                {
                    Vector3 back = -p.velocity.normalized;
                    Vector3 dir = (back * 0.6f + Vector3.up + Random.insideUnitSphere * 0.8f).normalized;
                    firingSolvent = p.solvent;
                    Spawn(p.pos, dir * Random.Range(0.6f, 1.6f), Random.Range(0.15f, 0.28f),
                          jetRadius * Random.Range(0.3f, 0.55f), 1f, 3f, false);
                }
                stream.RemoveAt(i);
                continue;
            }
            p.velocity += Physics.gravity * (jetGravity * dt);
            p.pos += p.velocity * dt;
            stream[i] = p;
        }
    }

    private void EnsureStreamObject()
    {
        if (streamGo != null)
            return;
        streamGo = new GameObject("PaintSpray_Stream");
        streamGo.AddComponent<MeshFilter>().sharedMesh = streamMesh = new Mesh { name = "PaintSpray_Stream" };
        streamMesh.MarkDynamic();
        streamMesh.subMeshCount = 2;
        streamRenderer = streamGo.AddComponent<MeshRenderer>();
        streamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        streamRenderer.sharedMaterials = new[] { dropletMat, solventMat != null ? solventMat : dropletMat };
    }

    // Un tube par filet (segment) : les points du segment, du plus ancien
    // (tete, vers la cible) au plus recent, puis la buse si on tire encore
    private void BuildStreamMesh()
    {
        if (stream.Count == 0 && !firedThisFrame)
        {
            if (streamGo != null && streamGo.activeSelf)
                streamGo.SetActive(false);
            return;
        }
        EnsureStreamObject();
        if (!streamGo.activeSelf)
            streamGo.SetActive(true);

        sVerts.Clear();
        sNormals.Clear();
        sTrisPaint.Clear();
        sTrisSolvent.Clear();

        int i = 0;
        while (i < stream.Count)
        {
            int seg = stream[i].segment;
            bool solvent = stream[i].solvent;
            sPath.Clear();
            sRadius.Clear();
            int start = i;
            while (i < stream.Count && stream[i].segment == seg)
            {
                var p = stream[i];
                // Filet un peu plus fin a la sortie, ondulation legere de liquide
                float swell = Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(p.age / 0.12f));
                float ripple = 1f + 0.12f * Mathf.Sin(p.age * 38f + p.phase);
                sPath.Add(p.pos);
                sRadius.Add(jetRadius * swell * ripple);
                i++;
            }
            // Filet en cours : il reste accroche a la buse
            if (firedThisFrame && seg == streamSegment)
            {
                sPath.Add(Nozzle);
                sRadius.Add(jetRadius * 0.75f);
            }
            if (sPath.Count >= 2)
                AddTube(sPath, sRadius, solvent ? sTrisSolvent : sTrisPaint);
        }

        streamMesh.Clear();
        streamMesh.SetVertices(sVerts);
        streamMesh.SetNormals(sNormals);
        streamMesh.subMeshCount = 2;
        streamMesh.SetTriangles(sTrisPaint, 0, false);
        streamMesh.SetTriangles(sTrisSolvent, 1, false);
        streamMesh.RecalculateBounds();
    }

    private void AddTube(List<Vector3> path, List<float> radius, List<int> tris)
    {
        int sides = Mathf.Max(3, streamSides);
        int n = path.Count;
        int baseIndex = sVerts.Count;
        Vector3 prevNormal = Vector3.zero;

        for (int k = 0; k < n; k++)
        {
            Vector3 tangent = path[Mathf.Min(k + 1, n - 1)] - path[Mathf.Max(k - 1, 0)];
            if (tangent.sqrMagnitude < 1e-10f)
                tangent = Vector3.forward;
            tangent.Normalize();

            // Repere qui tourne le moins possible d'un anneau a l'autre
            Vector3 normal = k == 0
                ? Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.95f ? Vector3.up : Vector3.right)
                : Vector3.ProjectOnPlane(prevNormal, tangent);
            if (normal.sqrMagnitude < 1e-8f)
                normal = Vector3.Cross(tangent, Vector3.right);
            normal.Normalize();
            prevNormal = normal;
            Vector3 binormal = Vector3.Cross(tangent, normal);

            // Bouts arrondis : tete et queue s'affinent
            float r = radius[k];
            if (k == 0 || k == n - 1)
                r *= 0.55f;

            for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2f / sides;
                Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                sVerts.Add(path[k] + dir * r);
                sNormals.Add(dir);
            }
        }

        for (int k = 0; k < n - 1; k++)
        {
            int ring = baseIndex + k * sides;
            int next = ring + sides;
            for (int j = 0; j < sides; j++)
            {
                int j2 = (j + 1) % sides;
                tris.Add(ring + j); tris.Add(ring + j2); tris.Add(next + j);
                tris.Add(ring + j2); tris.Add(next + j2); tris.Add(next + j);
            }
        }

        // Capuchons (disques) aux deux bouts
        AddCap(path[0], -(path[Mathf.Min(1, n - 1)] - path[0]).normalized, baseIndex, sides, tris, true);
        AddCap(path[n - 1], (path[n - 1] - path[Mathf.Max(n - 2, 0)]).normalized, baseIndex + (n - 1) * sides, sides, tris, false);
    }

    private void AddCap(Vector3 center, Vector3 outward, int ring, int sides, List<int> tris, bool reverse)
    {
        int c = sVerts.Count;
        sVerts.Add(center + outward * 0.01f);
        sNormals.Add(outward);
        for (int j = 0; j < sides; j++)
        {
            int j2 = (j + 1) % sides;
            if (reverse) { tris.Add(c); tris.Add(ring + j2); tris.Add(ring + j); }
            else { tris.Add(c); tris.Add(ring + j); tris.Add(ring + j2); }
        }
    }

    // =====================================================
    // GOUTTELETTES
    // =====================================================

    private void Spawn(Vector3 pos, Vector3 velocity, float life, float radius, float gravity, float drag, bool streak)
    {
        var tr = TakeDroplet();
        if (tr == null)
            return;
        tr.position = pos;
        if (tr.TryGetComponent<Renderer>(out var rend))
            rend.sharedMaterial = firingSolvent && solventMat != null ? solventMat : dropletMat;
        droplets.Add(new Droplet
        {
            tr = tr,
            velocity = velocity,
            life = life,
            size = radius / Mathf.Max(meshRadius, 1e-5f),
            gravity = gravity,
            drag = drag,
            streak = streak,
        });
        Orient(droplets[droplets.Count - 1], 0f);
    }

    private Transform TakeDroplet()
    {
        if (bubbleTemplate == null)
            return null;
        if (dropletRoot == null)
            dropletRoot = new GameObject("PaintSpray_Droplets").transform;
        if (meshRadius < 0f)
        {
            var mf = bubbleTemplate.GetComponent<MeshFilter>();
            meshRadius = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.x : 0.006f;
        }
        if (dropletPool.Count > 0)
        {
            var t = dropletPool.Pop();
            t.gameObject.SetActive(true);
            return t;
        }
        if (droplets.Count > 400)
            return null;
        var go = Instantiate(bubbleTemplate.gameObject, dropletRoot, false);
        go.name = "Droplet";
        go.SetActive(true);
        if (go.TryGetComponent<Renderer>(out var r) && dropletMat != null)
        {
            r.sharedMaterial = dropletMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return go.transform;
    }

    private void Recycle(Transform t)
    {
        if (t == null)
            return;
        t.gameObject.SetActive(false);
        dropletPool.Push(t);
    }

    // Goutte du jet : etiree dans le sens de la vitesse ; eclaboussure : ronde, retrecit
    private static void Orient(Droplet d, float k)
    {
        if (d.streak)
        {
            if (d.velocity.sqrMagnitude > 1e-6f)
                d.tr.rotation = Quaternion.LookRotation(d.velocity);
            float s = d.size * (k < 0.15f ? Mathf.Lerp(0.5f, 1f, k / 0.15f) : 1f);
            d.tr.localScale = new Vector3(s, s, s * 4f);
        }
        else
        {
            d.tr.localScale = Vector3.one * (d.size * (1f - k * k));
        }
    }

    private void UpdateDroplets(float dt)
    {
        for (int i = droplets.Count - 1; i >= 0; i--)
        {
            var d = droplets[i];
            d.age += dt;
            if (d.age >= d.life)
            {
                Recycle(d.tr);
                droplets.RemoveAt(i);
                continue;
            }
            d.velocity += Physics.gravity * (d.gravity * dt);
            if (d.drag > 0f)
                d.velocity *= Mathf.Max(0f, 1f - d.drag * dt);
            d.tr.position += d.velocity * dt;
            Orient(d, d.age / d.life);
            droplets[i] = d;
        }
    }
}
