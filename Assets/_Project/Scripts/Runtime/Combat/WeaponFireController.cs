using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// TIR DES ARMES (match)
// Pose sur le robot par RobotTestSpawner. Clic gauche (curseur
// verrouille) : les armes du slot selectionne (RobotLoadoutState)
// tirent, a tour de role, en puisant dans RobotEnergy. Un module
// selectionne (radar, bouclier, nano) ne tire pas.
// Familles (lues dans BlockDefinition.family / nom, palier N1..N6
// dans le nom) :
// - Laser  : bolt lumineux (WeaponProjectile) tire comme une
//            balle, tres rapide, droit (sans gravite), faible
//            cout, etincelles a l'impact
// - Plasma : boule lumineuse (WeaponProjectile) soumise a une
//            gravite reduite, degats de zone a l'impact, cout moyen
// - Rail   : tir instantane lourd et lent, gros cout, long trait
//            blanc, recul sur le robot
// - Tesla  : corps a corps : tant qu'on tient le clic, arcs
//            autour des lames et degats a tout ce qui les touche,
//            consommation continue
// Chaque tir part de la bouche de la tourelle (WeaponAimController)
// vers le point vise si le canon y est presque aligne, sinon dans
// l'axe du canon. Les cibles mobiles touchees recoivent Hit()
// (MovingTarget : un pop-up se couche). Les robots n'ont pas
// encore de degats : les impacts sur un bloc n'ont qu'un effet.
// Effets : traits = LineRenderer (shader Blockforge/Hologram,
// mode aretes = couleur pleine additive), etincelles = particules
// additives (Blockforge/DustParticle), tout en espace monde.
// =========================================================

[RequireComponent(typeof(RobotLoadoutState), typeof(RobotEnergy), typeof(WeaponAimController))]
public class WeaponFireController : MonoBehaviour
{
    public enum Kind { None, Laser, Plasma, Rail, Tesla }

    public struct Stats
    {
        public Kind kind;
        public float rate;          // tirs par seconde pour l'ensemble des armes du slot
        public float cost;          // energie par tir (Tesla : par seconde et par lame)
        public float damage;
        public float speed;         // projectile (m/s)
        public float gravity;       // part de la gravite subie par le projectile
        public float splash;        // rayon de zone (m)
    }

    [Header("Portee")]
    public float maxRange = 320f;

    private RobotLoadoutState loadout;
    private RobotEnergy energy;
    private WeaponAimController aim;
    private Rigidbody body;
    private int hitMask;

    private float nextShot;
    private int roundRobin;
    private float nextTeslaTick;

    // Effets
    private Material beamMaterial, railMaterial, plasmaMaterial;
    private readonly List<(LineRenderer line, float until)> beams = new();
    private readonly Stack<LineRenderer> beamPool = new();
    private Transform fxRoot;
    private ParticleSystem sparks, plasmaBurst, teslaArcs;
    private ParticleSystem.EmitParams emit;

    private static readonly Regex TierPattern = new(@"_N(\d)", RegexOptions.IgnoreCase);
    private static readonly Color LaserColor = new(0.45f, 0.92f, 1f);
    private static readonly Color RailColor = new(0.85f, 0.95f, 1f);
    private static readonly Color PlasmaColor = new(0.78f, 0.4f, 1f);
    private static readonly Color TeslaColor = new(0.55f, 0.85f, 1f);

    public static Stats Resolve(BlockDefinition def)
    {
        var s = new Stats { kind = Kind.None };
        if (def == null)
            return s;
        string key = ((def.family ?? "") + " " + def.name).ToLowerInvariant();
        var m = TierPattern.Match(def.name ?? "");
        int tier = m.Success ? int.Parse(m.Groups[1].Value) : 1;
        if (key.Contains("laser"))
            s = new Stats { kind = Kind.Laser, rate = 7f, cost = 3f + 0.6f * tier, damage = 8f * tier, speed = 150f, gravity = 0f };
        else if (key.Contains("plasma"))
            s = new Stats { kind = Kind.Plasma, rate = 2f, cost = 13f + 2f * tier, damage = 30f * tier, speed = 55f, gravity = 0.55f, splash = 2.5f + 0.4f * tier };
        else if (key.Contains("rail"))
            s = new Stats { kind = Kind.Rail, rate = 0.8f, cost = 28f + 3f * tier, damage = 90f * tier };
        else if (key.Contains("tesla"))
            s = new Stats { kind = Kind.Tesla, rate = 4f, cost = 7f, damage = 20f * tier, splash = 2.2f };
        return s;
    }

    private void Awake()
    {
        loadout = GetComponent<RobotLoadoutState>();
        energy = GetComponent<RobotEnergy>();
        aim = GetComponent<WeaponAimController>();
        body = GetComponent<Rigidbody>();
        hitMask = ~(1 << RobotTestSpawner.RobotLayer);
        BuildFX();
    }

    private void OnDestroy()
    {
        if (beamMaterial != null) Destroy(beamMaterial);
        if (railMaterial != null) Destroy(railMaterial);
        if (plasmaMaterial != null) Destroy(plasmaMaterial);
        if (fxRoot != null) Destroy(fxRoot.gameObject);
    }

    public Material PlasmaMaterial => plasmaMaterial;

    // =========================================================
    // ENTREES
    // =========================================================

    private void Update()
    {
        UpdateBeams();
        var mouse = Mouse.current;
        bool firing = mouse != null && mouse.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
        if (!firing)
            return;

        var slot = loadout.Selected;
        if (slot == null || slot.kind != LoadoutKind.Weapon)
            return;
        var stats = Resolve(slot.definition);
        if (stats.kind == Kind.None)
            return;

        if (stats.kind == Kind.Tesla)
        {
            FireTesla(slot, stats);
            return;
        }

        if (Time.time < nextShot)
            return;
        // Tourelles du slot (armes visees) a tour de role
        var turrets = new List<WeaponAimController.Turret>();
        foreach (var t in aim.Turrets)
        {
            if (slot.blocks.Contains(t.view))
                turrets.Add(t);
        }
        if (turrets.Count == 0)
            return;
        if (!energy.TrySpend(stats.cost))
        {
            nextShot = Time.time + 0.15f;   // pas de rafale de refus
            return;
        }
        nextShot = Time.time + 1f / Mathf.Max(0.1f, stats.rate);
        var turret = turrets[roundRobin++ % turrets.Count];

        Vector3 muzzle = turret.Muzzle;
        Vector3 toAim = (aim.AimPoint - muzzle).normalized;
        Vector3 dir = Vector3.Angle(turret.Forward, toAim) < 12f ? toAim : turret.Forward;

        switch (stats.kind)
        {
            case Kind.Laser: FireLaser(muzzle, dir, stats); break;
            case Kind.Rail: FireHitscan(muzzle, dir, stats, railMaterial, RailColor, 0.18f, 0.14f, 28); Recoil(dir, muzzle, 1.1f); break;
            case Kind.Plasma: FirePlasma(muzzle, dir, stats); break;
        }
    }

    // =========================================================
    // TIRS
    // =========================================================

    private void FireHitscan(Vector3 muzzle, Vector3 dir, Stats stats, Material material, Color color, float width, float seconds, int sparkCount)
    {
        Vector3 end = muzzle + dir * maxRange;
        if (Physics.Raycast(muzzle, dir, out var hit, maxRange, hitMask, QueryTriggerInteraction.Collide))
        {
            end = hit.point;
            ApplyHit(hit.collider, hit.point, hit.normal, stats, color, sparkCount);
        }
        ShowBeam(muzzle, end, material, width, seconds);
        Burst(sparks, muzzle, dir, 4, color, 0.08f, 0.14f, 3f, 6f, 0.12f);
    }

    // Bolt laser : capsule etiree dans l'axe du tir, courte trainee, tout droit
    private void FireLaser(Vector3 muzzle, Vector3 dir, Stats stats)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "LaserBolt";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = muzzle + dir * 0.4f;
        go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f); // la capsule est en Y
        go.transform.localScale = new Vector3(0.09f, 0.55f, 0.09f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = beamMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var bolt = go.AddComponent<WeaponProjectile>();
        bolt.Launch(this, dir * stats.speed, stats, hitMask, beamMaterial, trailTime: 0.06f, trailWidth: 0.12f, pulse: false, alignToVelocity: true);
        Burst(sparks, muzzle, dir, 4, LaserColor, 0.08f, 0.14f, 3f, 6f, 0.12f);
    }

    private void FirePlasma(Vector3 muzzle, Vector3 dir, Stats stats)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PlasmaBolt";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = muzzle + dir * 0.3f;
        go.transform.localScale = Vector3.one * 0.38f;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = plasmaMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var bolt = go.AddComponent<WeaponProjectile>();
        bolt.Launch(this, dir * stats.speed, stats, hitMask, plasmaMaterial, trailTime: 0.22f, trailWidth: 0.34f, pulse: true, alignToVelocity: false);
        Burst(sparks, muzzle, dir, 6, PlasmaColor, 0.1f, 0.18f, 2f, 5f, 0.14f);
    }

    private void FireTesla(RobotLoadoutState.Slot slot, Stats stats)
    {
        if (!energy.Drain(stats.cost * slot.Count))
            return;
        foreach (var view in slot.blocks)
        {
            var visual = BlockColliders.Visual(view.transform);
            Vector3 center = visual != null && TryBounds(visual, out var b) ? b.center : view.transform.position;
            // Arcs autour de la lame, en continu
            if (Random.value < 0.6f)
                Burst(teslaArcs, center + Random.insideUnitSphere * 0.6f, Random.onUnitSphere, 2, TeslaColor, 0.06f, 0.16f, 1.5f, 4f, 0.12f);
        }
        if (Time.time < nextTeslaTick)
            return;
        nextTeslaTick = Time.time + 1f / stats.rate;
        foreach (var view in slot.blocks)
        {
            var visual = BlockColliders.Visual(view.transform);
            Vector3 center = visual != null && TryBounds(visual, out var b) ? b.center : view.transform.position;
            foreach (var col in Physics.OverlapSphere(center, stats.splash, hitMask, QueryTriggerInteraction.Collide))
            {
                if (col.attachedRigidbody == body)
                    continue;
                Vector3 point = col.bounds.ClosestPoint(center);
                ApplyHit(col, point, (center - point).normalized, stats, TeslaColor, 8);
            }
        }
    }

    private void Recoil(Vector3 dir, Vector3 at, float strength)
    {
        if (body != null && !body.isKinematic)
            body.AddForceAtPosition(-dir * (body.mass * strength), at, ForceMode.Impulse);
    }

    // Impact : cible mobile touchee (Hit), zone eventuelle, etincelles
    public void ApplyHit(Collider collider, Vector3 point, Vector3 normal, Stats stats, Color color, int sparkCount)
    {
        if (collider != null)
        {
            var target = collider.GetComponentInParent<MovingTarget>();
            if (target != null)
                target.Hit(stats.damage);
        }
        if (stats.splash > 0f && stats.kind == Kind.Plasma)
        {
            foreach (var col in Physics.OverlapSphere(point, stats.splash, hitMask, QueryTriggerInteraction.Collide))
            {
                var target = col.GetComponentInParent<MovingTarget>();
                if (target != null)
                    target.Hit(stats.damage * 0.5f);
            }
            Burst(plasmaBurst, point, normal, 18, PlasmaColor, 0.3f, 0.7f, 2f, 7f, 0.45f);
            Burst(sparks, point, normal, 10, Color.white, 0.08f, 0.16f, 4f, 9f, 0.25f);
        }
        else
        {
            Burst(sparks, point, normal, sparkCount, color, 0.06f, 0.14f, 3f, 8f, 0.2f);
        }
    }

    private static bool TryBounds(Transform t, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    // =========================================================
    // EFFETS
    // =========================================================

    private void BuildFX()
    {
        fxRoot = new GameObject("WeaponFX").transform;
        var holo = Shader.Find("Blockforge/Hologram");
        if (holo != null)
        {
            beamMaterial = BeamMaterial(holo, "LaserBeam", LaserColor, 2.6f);
            railMaterial = BeamMaterial(holo, "RailBeam", RailColor, 3.2f);
            plasmaMaterial = BeamMaterial(holo, "PlasmaBolt", PlasmaColor, 3f);
        }
        var dust = Shader.Find("Blockforge/DustParticle");
        if (dust != null)
        {
            sparks = BuildParticles(dust, "Sparks", 400, stretch: true, new Color(0.8f, 0.95f, 1f));
            plasmaBurst = BuildParticles(dust, "PlasmaBurst", 200, stretch: false, PlasmaColor);
            teslaArcs = BuildParticles(dust, "TeslaArcs", 300, stretch: true, TeslaColor);
        }
    }

    // Couleur pleine additive (mode aretes du shader hologramme, devoilee en entier)
    private static Material BeamMaterial(Shader shader, string name, Color color, float intensity)
    {
        var m = new Material(shader) { name = name + " (instance)" };
        m.SetFloat("_Beam", 2f);
        m.SetColor("_Color", color);
        m.SetFloat("_Intensity", intensity);
        m.SetFloat("_Reveal", 1f);
        m.SetFloat("_Flicker", 0.08f);
        m.SetFloat("_LineDensity", 0.5f);
        m.SetFloat("_LineStrength", 0f);
        m.SetVector("_HoloCenter", Vector4.zero);
        m.SetVector("_HoloUp", new Vector4(0f, 1f, 0f, 0f));
        m.SetFloat("_HoloHalfHeight", 100000f);
        return m;
    }

    private ParticleSystem BuildParticles(Shader shader, string name, int max, bool stretch, Color tint)
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
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 0.1f;
        main.gravityModifier = stretch ? 0.6f : 0.1f;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = !stretch;
        limit.dampen = 0.5f;
        limit.limit = 1f;

        var mat = new Material(shader) { name = name + " (instance)" };
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_Additive", 1f);
        mat.SetFloat("_Softness", stretch ? 0.9f : 0.8f);
        mat.SetFloat("_FadeIn", 0.05f);
        mat.SetFloat("_FadeOut", 0.3f);
        mat.SetColor("_Tint", tint);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = stretch ? 0.05f : 0f;
        r.lengthScale = stretch ? 1.5f : 1f;
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

    // Bouffee de particules autour d'une direction (la teinte est celle du systeme ;
    // 'color' est gardee pour les appels, sans effet par particule : voir le shader)
    public void Burst(ParticleSystem ps, Vector3 at, Vector3 dir, int count, Color color, float sizeMin, float sizeMax,
                      float speedMin, float speedMax, float life)
    {
        if (ps == null)
            return;
        for (int i = 0; i < count; i++)
        {
            Vector3 v = (dir + Random.insideUnitSphere * 0.9f).normalized * Random.Range(speedMin, speedMax);
            emit.position = at;
            emit.velocity = v;
            emit.startSize = Random.Range(sizeMin, sizeMax);
            emit.startLifetime = life * Random.Range(0.7f, 1.3f);
            emit.rotation = Random.Range(0f, 360f);
            ps.Emit(emit, 1);
        }
    }

    public void ShowBeam(Vector3 from, Vector3 to, Material material, float width, float seconds)
    {
        if (material == null)
            return;
        LineRenderer line = beamPool.Count > 0 ? beamPool.Pop() : NewLine();
        line.gameObject.SetActive(true);
        line.sharedMaterial = material;
        line.startWidth = width;
        line.endWidth = width * 0.6f;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        beams.Add((line, Time.time + seconds));
    }

    private LineRenderer NewLine()
    {
        var go = new GameObject("Beam");
        go.transform.SetParent(fxRoot, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void UpdateBeams()
    {
        for (int i = beams.Count - 1; i >= 0; i--)
        {
            if (Time.time < beams[i].until)
                continue;
            beams[i].line.gameObject.SetActive(false);
            beamPool.Push(beams[i].line);
            beams.RemoveAt(i);
        }
    }
}
