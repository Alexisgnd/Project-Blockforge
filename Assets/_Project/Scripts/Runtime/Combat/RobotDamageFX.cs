using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// EFFETS DE DEGATS D'UN ROBOT (match)
// Pose a cote de RobotHealth sur chaque robot assemble (joueur,
// ennemi). Ecoute BlockChanged / Destroyed :
// - Bloc bien abime (PV < damagedBelow) : arcs electriques qui
//   crepitent sur le bloc et legere fumee, de plus en plus denses
//   a mesure qu'il s'approche de 0
// - Bloc detruit : petite explosion (flash, etincelles, fumee) et
//   le bloc se detache tel quel (copie de son modele : cube, roue,
//   arme...), retombe, eclate en morceaux de sa couleur puis
//   disparait
// - Robot detruit : grosse explosion, tous les blocs restants
//   sont projetes en morceaux
// Les particules passent par le shader Blockforge/DustParticle
// (teinte par systeme), les arcs par Blockforge/Hologram en mode
// aretes, les morceaux par HDRP/Lit (couleur de bloc via
// MaterialPropertyBlock). Tout est detruit avec le robot.
// =========================================================

[RequireComponent(typeof(RobotHealth))]
public class RobotDamageFX : MonoBehaviour
{
    [Header("Bloc abime")]
    [Tooltip("Part des PV sous laquelle un bloc crepite et fume")]
    public float damagedBelow = 0.45f;
    public float smokePerSecondMax = 7f;
    public Color arcColor = new(0.62f, 0.9f, 1f, 1f);

    [Header("Morceaux")]
    public float pieceSeconds = 0.4f;     // le cube entier tombe avant d'eclater
    public float shardSeconds = 1.6f;     // vie d'un eclat avant de retrecir
    public int shardsPerBlock = 6;

    private RobotHealth health;
    private RobotLocomotion loco;
    private Rigidbody body;
    private Transform fxRoot;
    private ParticleSystem smoke, sparks, flash, embers;
    private Material arcMaterial, pieceMaterial;
    private readonly List<Material> owned = new();
    private readonly Stack<LineRenderer> arcPool = new();
    private readonly List<(LineRenderer line, float until)> arcs = new();
    private readonly Dictionary<RobotHealth.Block, State> states = new();
    private ParticleSystem.EmitParams emit;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static MaterialPropertyBlock props;

    private class State
    {
        public bool destroyed;
        public float nextSmoke, nextArc;
        public Color color = new(0.5f, 0.5f, 0.52f, 1f);
        public Vector3 size = Vector3.one * 0.9f;
    }

    private void Awake()
    {
        health = GetComponent<RobotHealth>();
        loco = GetComponent<RobotLocomotion>();
        body = GetComponent<Rigidbody>();
        BuildFX();
    }

    private void Start()
    {
        foreach (var b in health.blocks)
            states[b] = new State { destroyed = b.Destroyed, color = BlockColor(b), size = BlockSize(b) };
        health.BlockChanged += OnBlockChanged;
        health.Destroyed += OnRobotDestroyed;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.BlockChanged -= OnBlockChanged;
            health.Destroyed -= OnRobotDestroyed;
        }
        if (fxRoot != null)
            Destroy(fxRoot.gameObject);
        foreach (var m in owned)
        {
            if (m != null)
                Destroy(m);
        }
    }

    // ---------- Evenements ----------

    private void OnBlockChanged(RobotHealth.Block b)
    {
        if (!states.TryGetValue(b, out var st))
            states[b] = st = new State { color = BlockColor(b), size = BlockSize(b) };
        if (b.Destroyed && !st.destroyed && !health.IsDead)
            Detach(b, st, 1f);
        st.destroyed = b.Destroyed;
    }

    private void OnRobotDestroyed()
    {
        Vector3 c = transform.TransformPoint(health.Center());
        Puff(flash, c, Vector3.up, 4, 2.6f, 4.2f, 0f, 0.4f, 0.22f);
        Puff(sparks, c, Vector3.up, 90, 0.08f, 0.2f, 5f, 14f, 0.5f);
        Puff(embers, c, Vector3.up, 40, 0.1f, 0.24f, 3f, 9f, 1.1f);
        Puff(smoke, c, Vector3.up, 36, 0.9f, 2.2f, 1f, 4f, 2.4f);
        foreach (var b in health.blocks)
        {
            if (b.hp <= 0f || !states.TryGetValue(b, out var st))
                continue;
            Detach(b, st, 2.2f);
            st.destroyed = true;
        }
        // Les blocs sont caches par RobotHealth : plus rien a crepiter
        foreach (var st in states.Values)
            st.nextSmoke = st.nextArc = float.MaxValue;
    }

    // ---------- Bloc abime : arcs et fumee ----------

    private void Update()
    {
        UpdateArcs();
        if (health.IsDead)
            return;
        float now = Time.time;
        foreach (var b in health.blocks)
        {
            if (b.Destroyed || b.Ratio >= damagedBelow || !states.TryGetValue(b, out var st))
                continue;
            float severity = 1f - b.Ratio / damagedBelow;       // 0 a la limite, 1 a 0 PV
            Vector3 center = transform.TransformPoint(b.localCenter);
            Vector3 half = st.size * 0.5f;
            if (now >= st.nextSmoke)
            {
                st.nextSmoke = now + 1f / Mathf.Lerp(1.2f, smokePerSecondMax, severity);
                Vector3 at = center + new Vector3(Random.Range(-half.x, half.x), half.y * 0.8f, Random.Range(-half.z, half.z));
                emit.position = at;
                emit.velocity = Vector3.up * Random.Range(0.5f, 1.1f) + Random.insideUnitSphere * 0.25f + Drift() * 0.35f;
                emit.startSize = Random.Range(0.22f, 0.45f) * (0.7f + severity * 0.6f);
                emit.startLifetime = Random.Range(0.9f, 1.5f);
                emit.rotation = Random.Range(0f, 360f);
                smoke.Emit(emit, 1);
            }
            if (now >= st.nextArc)
            {
                st.nextArc = now + Random.Range(0.08f, 0.5f) / (0.4f + severity);
                Arc(center, half);
                Puff(sparks, center + Random.insideUnitSphere * 0.3f, Vector3.up, 3, 0.05f, 0.1f, 1.5f, 4f, 0.18f);
            }
        }
    }

    // Eclair en zigzag entre deux points de la surface du bloc
    private void Arc(Vector3 center, Vector3 half)
    {
        if (arcMaterial == null)
            return;
        var line = arcPool.Count > 0 ? arcPool.Pop() : NewArc();
        line.gameObject.SetActive(true);
        Vector3 a = center + Vector3.Scale(Random.onUnitSphere, half) * 1.05f;
        Vector3 b = center + Vector3.Scale(Random.onUnitSphere, half) * 1.05f;
        int n = line.positionCount;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            Vector3 p = Vector3.Lerp(a, b, t);
            if (i > 0 && i < n - 1)
                p += Random.insideUnitSphere * 0.09f;
            line.SetPosition(i, p);
        }
        float w = Random.Range(0.02f, 0.045f);
        line.startWidth = w;
        line.endWidth = w;
        arcs.Add((line, Time.time + Random.Range(0.05f, 0.1f)));
    }

    private LineRenderer NewArc()
    {
        var go = new GameObject("Arc");
        go.transform.SetParent(fxRoot, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 6;
        line.numCapVertices = 1;
        line.alignment = LineAlignment.View;
        line.sharedMaterial = arcMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void UpdateArcs()
    {
        for (int i = arcs.Count - 1; i >= 0; i--)
        {
            if (Time.time < arcs[i].until)
                continue;
            arcs[i].line.gameObject.SetActive(false);
            arcPool.Push(arcs[i].line);
            arcs.RemoveAt(i);
        }
    }

    // ---------- Bloc detruit : explosion et morceau ----------

    // force : 1 pour un bloc isole, plus pour l'explosion du robot
    private void Detach(RobotHealth.Block b, State st, float force)
    {
        Vector3 center = transform.TransformPoint(b.localCenter);
        Vector3 away = (center - transform.TransformPoint(health.Center()));
        away.y = 0f;
        away = (away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere) + Vector3.up * 0.8f;

        Puff(flash, center, Vector3.up, 1, 0.9f, 1.3f, 0f, 0f, 0.12f);
        Puff(sparks, center, away, 22, 0.06f, 0.14f, 3f, 8f, 0.3f);
        Puff(embers, center, away, 8, 0.08f, 0.16f, 1.5f, 4f, 0.8f);
        Puff(smoke, center, Vector3.up, 6, 0.35f, 0.7f, 0.6f, 1.6f, 1.4f);

        // Le bloc entier (son vrai modele : cube, roue, arme...) se detache, puis eclate
        var piece = MakePiece(b, center);
        var rb = piece.GetComponent<Rigidbody>();
        rb.linearVelocity = Drift() + away.normalized * Random.Range(2.5f, 4.5f) * force;
        rb.angularVelocity = Random.insideUnitSphere * 6f;
        StartCoroutine(PieceLife(piece, st, force));
    }

    private IEnumerator PieceLife(GameObject piece, State st, float force)
    {
        yield return new WaitForSeconds(pieceSeconds * Random.Range(0.8f, 1.3f));
        if (piece == null)
            yield break;
        Vector3 at = piece.transform.position;
        Vector3 v = piece.GetComponent<Rigidbody>().linearVelocity;
        Quaternion rot = piece.transform.rotation;
        Destroy(piece);
        Puff(flash, at, Vector3.up, 1, 0.6f, 0.9f, 0f, 0f, 0.1f);
        Puff(sparks, at, Vector3.up, 10, 0.05f, 0.1f, 2f, 5f, 0.25f);
        Puff(smoke, at, Vector3.up, 3, 0.25f, 0.5f, 0.4f, 1f, 1.1f);
        int n = shardsPerBlock + Random.Range(-1, 2);
        for (int i = 0; i < n; i++)
        {
            Vector3 size = Vector3.Scale(st.size, new Vector3(Random.Range(0.22f, 0.4f), Random.Range(0.22f, 0.4f), Random.Range(0.22f, 0.4f)));
            Vector3 offset = rot * Vector3.Scale(Random.insideUnitSphere, st.size * 0.3f);
            var shard = MakeCube("Shard", at + offset, rot * Quaternion.Euler(Random.insideUnitSphere * 40f), size, st.color);
            var rb = shard.GetComponent<Rigidbody>();
            rb.linearVelocity = v * 0.6f + (offset.normalized + Vector3.up * 0.5f) * Random.Range(1.5f, 3.5f) * Mathf.Sqrt(force);
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            StartCoroutine(ShardLife(shard));
        }
    }

    private IEnumerator ShardLife(GameObject shard)
    {
        yield return new WaitForSeconds(shardSeconds * Random.Range(0.7f, 1.3f));
        if (shard == null)
            yield break;
        Vector3 start = shard.transform.localScale;
        float t = 0f;
        while (t < 0.45f && shard != null)
        {
            t += Time.deltaTime;
            shard.transform.localScale = start * (1f - t / 0.45f);
            yield return null;
        }
        if (shard != null)
            Destroy(shard);
    }

    // Copie du bloc tel qu'il est (modele, peinture, collider d'empreinte),
    // rendue visible, detachee du robot et dotee d'un corps physique
    private GameObject MakePiece(RobotHealth.Block b, Vector3 at)
    {
        var src = b.view != null ? b.view.gameObject : null;
        if (src == null)
            return MakeCube("Piece", at, Quaternion.identity, Vector3.one * 0.9f, new Color(0.5f, 0.5f, 0.52f, 1f));
        var go = Instantiate(src, at, src.transform.rotation, fxRoot);
        go.name = "Piece_" + src.name;
        go.transform.localScale = src.transform.lossyScale;
        var view = go.GetComponent<PlacedBlockView>();
        if (view != null)
            Destroy(view);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        bool anyCollider = false;
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = true;
            c.excludeLayers = 1 << RobotTestSpawner.RobotLayer;
            anyCollider = true;
        }
        if (!anyCollider)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.9f;
            box.excludeLayers = 1 << RobotTestSpawner.RobotLayer;
        }
        SetLayer(go.transform, 2);   // Ignore Raycast : invisible aux rayons (visee, sol), collisions normales
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 6f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.excludeLayers = 1 << RobotTestSpawner.RobotLayer;
        return go;
    }

    private static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayer(child, layer);
    }

    private GameObject MakeCube(string name, Vector3 at, Quaternion rotation, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.layer = 2;   // Ignore Raycast
        go.transform.SetParent(fxRoot, false);
        go.transform.SetPositionAndRotation(at, rotation);
        go.transform.localScale = size;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = pieceMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        props ??= new MaterialPropertyBlock();
        props.Clear();
        props.SetColor(BaseColorId, color);
        r.SetPropertyBlock(props);
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.3f, size.x * size.y * size.z * 12f);
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Les morceaux ne bousculent jamais les robots (ni le leur, ni l'adversaire)
        rb.excludeLayers = 1 << RobotTestSpawner.RobotLayer;
        go.GetComponent<Collider>().excludeLayers = 1 << RobotTestSpawner.RobotLayer;
        return go;
    }

    // ---------- Outils ----------

    private Vector3 Drift() => body != null ? body.linearVelocity : Vector3.zero;

    private static Color BlockColor(RobotHealth.Block b)
    {
        if (b.baseColors != null)
        {
            foreach (var mats in b.baseColors)
            {
                if (mats == null)
                    continue;
                foreach (var c in mats)
                {
                    if (c.a >= 0f)
                        return new Color(c.r, c.g, c.b, 1f);
                }
            }
        }
        return new Color(0.5f, 0.5f, 0.52f, 1f);
    }

    private static Vector3 BlockSize(RobotHealth.Block b)
    {
        Bounds bounds = default;
        bool any = false;
        foreach (var r in b.renderers)
        {
            if (r == null)
                continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        if (!any)
            return Vector3.one * 0.9f;
        Vector3 s = bounds.size;
        return new Vector3(Mathf.Clamp(s.x, 0.3f, 1.2f), Mathf.Clamp(s.y, 0.3f, 1.2f), Mathf.Clamp(s.z, 0.3f, 1.2f)) * 0.9f;
    }

    private void Puff(ParticleSystem ps, Vector3 at, Vector3 dir, int count, float sizeMin, float sizeMax,
                      float speedMin, float speedMax, float life)
    {
        if (ps == null)
            return;
        for (int i = 0; i < count; i++)
        {
            emit.position = at + Random.insideUnitSphere * 0.15f;
            emit.velocity = (dir + Random.insideUnitSphere * 0.9f).normalized * Random.Range(speedMin, speedMax);
            emit.startSize = Random.Range(sizeMin, sizeMax);
            emit.startLifetime = life * Random.Range(0.7f, 1.3f);
            emit.rotation = Random.Range(0f, 360f);
            ps.Emit(emit, 1);
        }
    }

    // ---------- Construction ----------

    private void BuildFX()
    {
        fxRoot = new GameObject("DamageFX_" + name).transform;
        var dust = Shader.Find("Blockforge/DustParticle");
        if (dust != null)
        {
            smoke = BuildParticles(dust, "Smoke", 300, additive: false, new Color(0.3f, 0.3f, 0.32f, 0.4f), gravity: -0.03f, stretch: false, fadeIn: 0.15f, fadeOut: 0.6f, grow: true);
            sparks = BuildParticles(dust, "Sparks", 500, additive: true, new Color(0.85f, 0.95f, 1f, 1f), gravity: 0.7f, stretch: true, fadeIn: 0.02f, fadeOut: 0.4f, grow: false);
            embers = BuildParticles(dust, "Embers", 300, additive: true, new Color(1f, 0.55f, 0.2f, 1f), gravity: 0.5f, stretch: false, fadeIn: 0.02f, fadeOut: 0.5f, grow: false);
            flash = BuildParticles(dust, "Flash", 40, additive: true, new Color(1f, 0.85f, 0.6f, 1f), gravity: 0f, stretch: false, fadeIn: 0.05f, fadeOut: 0.7f, grow: true);
        }
        var holo = Shader.Find("Blockforge/Hologram");
        if (holo != null)
        {
            arcMaterial = new Material(holo) { name = "DamageArc (instance)" };
            arcMaterial.SetFloat("_Beam", 2f);
            arcMaterial.SetColor("_Color", arcColor);
            arcMaterial.SetFloat("_Intensity", 3f);
            arcMaterial.SetFloat("_Reveal", 1f);
            arcMaterial.SetFloat("_Flicker", 0.3f);
            arcMaterial.SetFloat("_LineDensity", 0.5f);
            arcMaterial.SetFloat("_LineStrength", 0f);
            arcMaterial.SetVector("_HoloUp", new Vector4(0f, 1f, 0f, 0f));
            arcMaterial.SetFloat("_HoloHalfHeight", 100000f);
            owned.Add(arcMaterial);
        }
        var lit = Shader.Find("HDRP/Lit");
        if (lit != null)
        {
            pieceMaterial = new Material(lit) { name = "DamagePiece (instance)" };
            pieceMaterial.SetFloat("_Smoothness", 0.35f);
            owned.Add(pieceMaterial);
        }
    }

    private ParticleSystem BuildParticles(Shader shader, string name, int max, bool additive, Color tint, float gravity,
                                          bool stretch, float fadeIn, float fadeOut, bool grow)
    {
        var go = new GameObject(name);
        go.transform.SetParent(fxRoot, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.startSpeed = 0f;
        main.startSize = 0.2f;
        main.gravityModifier = gravity;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        if (grow)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        }
        if (!stretch)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.4f;
            limit.limit = 1.2f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        }

        var mat = new Material(shader) { name = name + " (instance)" };
        if (additive)
        {
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_Additive", 1f);
        }
        else
        {
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_Additive", 0f);
        }
        mat.SetFloat("_Softness", stretch ? 0.9f : 0.75f);
        mat.SetFloat("_FadeIn", fadeIn);
        mat.SetFloat("_FadeOut", fadeOut);
        mat.SetColor("_Tint", tint);
        owned.Add(mat);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = stretch ? 0.05f : 0f;
        r.lengthScale = stretch ? 1.6f : 1f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent,
        });
        ps.Play();
        return ps;
    }
}
