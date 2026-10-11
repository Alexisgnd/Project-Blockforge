using UnityEngine;

// =========================================================
// EFFET "PROPULSEUR" D'UN MOTEUR DU VAISSEAU (garage)
// Pose sur un point d'ancrage oriente : +Z local = sens du
// jet (sortie du moteur). Construit a l'activation, en jeu
// comme dans l'editeur (apercu) :
// - "Plume" : 4 plans croises dans l'axe du jet (technique
//   classique des flammes : bords doux vus de cote), texture
//   additive cyan striee qui s'estompe vers le bout ; le
//   tout tourne sur l'axe et vacille ;
// - "Core"  : meme principe, plus court, plus etroit, blanc ;
// - "Sparks": particules etirees ejectees dans l'axe ;
// - la lumiere ponctuelle enfant (creee par le setup) vacille
//   avec la flamme.
// Les plans et les particules sont generes (non sauvegardes
// dans la scene) : les reglages ci-dessous suffisent.
// Setup : Blockforge > Setup Garage Thrusters.
// =========================================================

[ExecuteAlways]
public class ShipThrusterFX : MonoBehaviour
{
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    [Header("Materiaux (remplis par le setup)")]
    public Material plumeMaterial;
    public Material coreMaterial;
    public Material sparkMaterial;
    public Light flameLight;

    [Header("Forme (m) : demi-largeur a la tuyere / au bout, longueur")]
    public float jetBaseHalfWidth = 2.9f;
    public float jetTipHalfWidth = 0.5f;
    public float jetLength = 22f;
    public float coreBaseHalfWidth = 1.5f;
    public float coreTipHalfWidth = 0.2f;
    public float coreLength = 9f;

    [Header("Eclat (emissif, hors exposition)")]
    public Color jetColor = new(0.6f, 0.95f, 1f);
    public float jetGlow = 4f;
    public Color coreColor = Color.white;
    public float coreGlow = 6f;

    [Header("Animation")]
    [Tooltip("Amplitude du vacillement (longueur, eclat)")]
    public float flicker = 0.08f;
    public float flickerSpeed = 12f;
    [Tooltip("Rotation du panache sur son axe (degres / s) : fait defiler les stries")]
    public float spinSpeed = 120f;

    [Header("Etincelles")]
    public bool sparks = true;
    public float sparkRate = 40f;
    public Vector2 sparkSpeed = new(14f, 26f);

    private Transform plume;
    private Transform core;
    private Renderer plumeRenderer;
    private Renderer coreRenderer;
    private MaterialPropertyBlock block;
    private float lightBaseIntensity = -1f;
    private float seed;

    private void OnEnable()
    {
        seed = Random.value * 100f;
        block ??= new MaterialPropertyBlock();
        Build();
    }

    private void OnDisable()
    {
        Clear();
        if (flameLight != null && lightBaseIntensity >= 0f)
            flameLight.intensity = lightBaseIntensity;
        lightBaseIntensity = -1f;
    }

    private void OnValidate()
    {
        // Rebuild differe : OnValidate ne doit pas creer d'objets directement
        if (isActiveAndEnabled)
            rebuildRequested = true;
    }

    private bool rebuildRequested;

    private void Update()
    {
        if (rebuildRequested)
        {
            rebuildRequested = false;
            Clear();
            Build();
        }

        if (plume == null)
            return;

        float t = Application.isPlaying ? Time.time : (float)(System.DateTime.Now.TimeOfDay.TotalSeconds % 1000.0);
        float n1 = Mathf.PerlinNoise(t * flickerSpeed, seed) * 2f - 1f;
        float n2 = Mathf.PerlinNoise(seed, t * flickerSpeed * 1.7f) * 2f - 1f;

        float plumeFlicker = 1f + flicker * n1;
        float coreFlicker = 1f + flicker * 1.5f * n2;
        float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;

        plume.localScale = new Vector3(1f + flicker * 0.4f * n2, 1f + flicker * 0.4f * n2, plumeFlicker);
        plume.Rotate(0f, 0f, spinSpeed * dt, Space.Self);
        core.localScale = new Vector3(1f, 1f, coreFlicker);
        core.Rotate(0f, 0f, -spinSpeed * 1.6f * dt, Space.Self);

        SetEmission(plumeRenderer, jetColor * (jetGlow * plumeFlicker));
        SetEmission(coreRenderer, coreColor * (coreGlow * coreFlicker));

        // En edition, la lumiere n'est pas touchee (sa valeur est sauvegardee dans la scene)
        if (flameLight != null && Application.isPlaying)
        {
            if (lightBaseIntensity < 0f)
                lightBaseIntensity = flameLight.intensity;
            flameLight.intensity = lightBaseIntensity * (1f + flicker * 1.5f * n1);
        }
    }

    private void SetEmission(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;
        renderer.GetPropertyBlock(block);
        block.SetColor(EmissiveColorId, color);
        renderer.SetPropertyBlock(block);
    }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void Build()
    {
        plume = CreateJet("Plume", jetBaseHalfWidth, jetTipHalfWidth, jetLength, plumeMaterial, out plumeRenderer);
        core = CreateJet("Core", coreBaseHalfWidth, coreTipHalfWidth, coreLength, coreMaterial, out coreRenderer);
        if (sparks && sparkMaterial != null)
            CreateSparks();
    }

    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if ((child.hideFlags & HideFlags.DontSave) == 0)
                continue; // la lumiere (sauvegardee par le setup) reste
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
        plume = core = null;
        plumeRenderer = coreRenderer = null;
    }

    private GameObject CreateChild(string childName)
    {
        var go = new GameObject(childName) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        return go;
    }

    private Transform CreateJet(string childName, float baseHalfWidth, float tipHalfWidth, float length,
                                Material material, out Renderer renderer)
    {
        var go = CreateChild(childName);
        go.AddComponent<MeshFilter>().sharedMesh = BuildCrossedPlanesMesh(baseHalfWidth, tipHalfWidth, length);
        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer = meshRenderer;
        return go.transform;
    }

    // Plans croises le long de +Z (4 plans a 45 degres) : U = travers du plan
    // (0 et 1 aux bords, ou la texture s'estompe), V = longueur. La largeur se
    // resserre vite pres de la tuyere puis plus lentement.
    private static Mesh BuildCrossedPlanesMesh(float baseHalfWidth, float tipHalfWidth, float length)
    {
        const int planes = 4;
        const int rows = 16;
        int perPlane = (rows + 1) * 2;
        var vertices = new Vector3[planes * perPlane];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[planes * rows * 6];

        int k = 0;
        for (int p = 0; p < planes; p++)
        {
            float angle = p * Mathf.PI / planes;
            var across = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            int start = p * perPlane;
            for (int j = 0; j <= rows; j++)
            {
                float v = j / (float)rows;
                float half = Mathf.Lerp(baseHalfWidth, tipHalfWidth, Mathf.Pow(v, 0.7f));
                var center = new Vector3(0f, 0f, v * length);
                vertices[start + j * 2] = center - across * half;
                vertices[start + j * 2 + 1] = center + across * half;
                uvs[start + j * 2] = new Vector2(0f, v);
                uvs[start + j * 2 + 1] = new Vector2(1f, v);
            }
            for (int j = 0; j < rows; j++)
            {
                int a = start + j * 2;
                triangles[k++] = a; triangles[k++] = a + 2; triangles[k++] = a + 1;
                triangles[k++] = a + 1; triangles[k++] = a + 2; triangles[k++] = a + 3;
            }
        }

        var mesh = new Mesh { name = "ThrusterJet", hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateSparks()
    {
        var go = CreateChild("Sparks");
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(sparkSpeed.x, sparkSpeed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = system.emission;
        emission.rateOverTime = sparkRate;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone; // emet le long de +Z local
        shape.angle = 6f;
        shape.radius = jetBaseHalfWidth * 0.4f;

        // Le materiau HDRP/Unlit ignore la couleur des particules : on les fait
        // disparaitre en reduisant leur taille.
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sparkMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale = 1f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        system.Play();
    }
}
