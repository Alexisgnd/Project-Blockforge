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
//            gravite reduite, explosion a l'impact (PlasmaExplosion :
//            eclair, onde de choc et anneau au rayon exact de la
//            zone, gerbe, braises, fumee, secousse camera) ; degats
//            de zone degressifs et souffle sur les corps rigides.
//            Cadence selon la rarete (N1 2,4 tirs/s -> N6 6 tirs/s),
//            cout par tir ajuste pour garder la meme consommation
//            d'energie par seconde
// - Rail   : tir instantane lourd et lent, gros cout, long trait
//            blanc, recul sur le robot
// - Tesla  : corps a corps : tant qu'on tient le clic, arcs
//            autour des lames et degats a tout ce qui les touche,
//            consommation continue
// Chaque tir part de la bouche de la tourelle (WeaponAimController)
// vers le point vise si le canon y est presque aligne, sinon dans
// l'axe du canon. Une cible touchee (ShootableTarget) est
// detruite puis reapparait ; un robot touche (RobotHealth) perd
// des PV sur le bloc atteint (zone pour le plasma). Le robot
// tireur ignore ses propres colliders. manual = true : un robot
// IA tient FireHeld au lieu du clic.
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

    [Header("Pilotage externe (IA)")]
    [Tooltip("Vrai : le tir vient de FireHeld (robot ennemi) au lieu du clic gauche")]
    public bool manual;
    public bool FireHeld { get; set; }

    private RobotLoadoutState loadout;
    private RobotEnergy energy;
    private WeaponAimController aim;
    private RobotHealth health;
    private Rigidbody body;
    private readonly RaycastHit[] hits = new RaycastHit[24];

    private float nextShot;
    private int roundRobin;
    private float nextTeslaTick;

    // Effets
    private Material beamMaterial, railMaterial, plasmaMaterial;
    private readonly List<(LineRenderer line, float until)> beams = new();
    private readonly Stack<LineRenderer> beamPool = new();
    private Transform fxRoot;
    private ParticleSystem sparks, plasmaBurst, plasmaEmbers, plasmaSmoke, teslaArcs, muzzleFlash, muzzleSmoke;
    private Shader hologramShader;
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
            s = PlasmaStats(tier);
        else if (key.Contains("rail"))
            s = new Stats { kind = Kind.Rail, rate = 0.8f, cost = 28f + 3f * tier, damage = 90f * tier };
        else if (key.Contains("tesla"))
            s = new Stats { kind = Kind.Tesla, rate = 4f, cost = 7f, damage = 20f * tier, splash = 2.2f };
        return s;
    }

    // Plasma par rarete (N1..N6) : cadence +20 % par niveau. L'energie depensee par
    // seconde reste celle de l'ancien reglage (2 tirs/s) : le cout par tir baisse quand
    // la cadence monte. Les degats par tir baissent un peu aux hauts niveaux pour que
    // les degats par seconde montent sans exploser (N1 72/s -> N6 ~760/s).
    public static Stats PlasmaStats(int tier)
    {
        tier = Mathf.Clamp(tier, 1, 6);
        float rate = 2.4f * Mathf.Pow(1.2f, tier - 1);
        float energyPerSecond = (13f + 2f * tier) * 2f;
        float damage = 30f * tier * Mathf.Lerp(1f, 0.7f, (tier - 1) / 5f);
        return new Stats
        {
            kind = Kind.Plasma,
            rate = rate,
            cost = energyPerSecond / rate,
            damage = damage,
            speed = 58f + 4f * tier,
            gravity = 0.55f,
            splash = 2.5f + 0.4f * tier,
        };
    }

    private void Awake()
    {
        loadout = GetComponent<RobotLoadoutState>();
        energy = GetComponent<RobotEnergy>();
        aim = GetComponent<WeaponAimController>();
        body = GetComponent<Rigidbody>();
        health = GetComponent<RobotHealth>();
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
        if (health == null)
            health = GetComponent<RobotHealth>();
        if (health != null && health.IsDead)
            return;
        bool firing;
        if (manual)
        {
            firing = FireHeld;
        }
        else
        {
            var mouse = Mouse.current;
            firing = mouse != null && mouse.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
        }
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
            if (slot.blocks.Contains(t.view) && !t.view.destroyed)
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
        if (WeaponAimController.RaycastIgnoring(body, hits, muzzle, dir, maxRange, out var hit))
        {
            end = hit.point;
            ApplyHit(hit.collider, hit.point, hit.normal, stats, color, sparkCount);
        }
        ShowBeam(muzzle, end, material, width, seconds);
        MuzzleFX(muzzle, dir, color, 1.8f);
    }

    // Bolt laser : capsule etiree dans l'axe du tir, courte trainee, tout droit
    // Depart du coup : flash en bout de canon, etincelles, petite bouffee
    private void MuzzleFX(Vector3 muzzle, Vector3 dir, Color color, float scale)
    {
        Burst(muzzleFlash, muzzle + dir * 0.2f, dir, 2, color, 0.28f * scale, 0.45f * scale, 0f, 0.5f, 0.07f);
        Burst(sparks, muzzle + dir * 0.1f, dir, Mathf.RoundToInt(6 * scale), color, 0.06f, 0.13f, 4f, 9f, 0.14f);
        Burst(muzzleSmoke, muzzle + dir * 0.25f, dir, Mathf.RoundToInt(2 * scale), color, 0.15f * scale, 0.3f * scale, 0.6f, 1.4f, 0.5f);
    }

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
        bolt.Launch(this, body, dir * stats.speed, stats, beamMaterial, trailTime: 0.06f, trailWidth: 0.12f, pulse: false, alignToVelocity: true);
        MuzzleFX(muzzle, dir, LaserColor, 0.8f);
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
        bolt.Launch(this, body, dir * stats.speed, stats, plasmaMaterial, trailTime: 0.22f, trailWidth: 0.34f, pulse: true, alignToVelocity: false);
        MuzzleFX(muzzle, dir, PlasmaColor, 1.4f);
    }

    private void FireTesla(RobotLoadoutState.Slot slot, Stats stats)
    {
        if (!energy.Drain(stats.cost * slot.Count))
            return;
        foreach (var view in slot.blocks)
        {
            if (view.destroyed)
                continue;
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
            if (view.destroyed)
                continue;
            var visual = BlockColliders.Visual(view.transform);
            Vector3 center = visual != null && TryBounds(visual, out var b) ? b.center : view.transform.position;
            foreach (var col in Physics.OverlapSphere(center, stats.splash, ~0, QueryTriggerInteraction.Collide))
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

    // Impact : cible touchee (ShootableTarget : detruite puis reapparait), robot
    // touche (RobotHealth : degats au bloc), zone eventuelle, etincelles
    public void ApplyHit(Collider collider, Vector3 point, Vector3 normal, Stats stats, Color color, int sparkCount)
    {
        ShootableTarget directTarget = null;
        RobotHealth directRobot = null;
        if (collider != null)
        {
            directTarget = collider.GetComponentInParent<ShootableTarget>();
            if (directTarget != null)
                directTarget.Hit(stats.damage);
            directRobot = collider.GetComponentInParent<RobotHealth>();
            if (directRobot != null && directRobot.gameObject != gameObject)
                directRobot.Damage(point, stats.damage, stats.kind == Kind.Plasma ? stats.splash : 0f, transform.position);
        }
        if (stats.splash > 0f && stats.kind == Kind.Plasma)
            PlasmaBlast(point, normal, stats, directTarget, directRobot);
        else
            Burst(sparks, point, normal, sparkCount, color, 0.06f, 0.14f, 3f, 8f, 0.2f);
    }

    // Zone du plasma : degats degressifs (60 % au centre -> 15 % au bord) a ce qui n'a
    // pas pris le coup direct, souffle sur les corps rigides, effets d'explosion
    private void PlasmaBlast(Vector3 point, Vector3 normal, Stats stats, ShootableTarget directTarget, RobotHealth directRobot)
    {
        float radius = stats.splash;
        var hitRobots = new HashSet<RobotHealth>();
        var hitTargets = new HashSet<ShootableTarget>();
        var pushed = new HashSet<Rigidbody>();
        foreach (var col in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Collide))
        {
            Vector3 closest = col.bounds.ClosestPoint(point);   // (ClosestPoint refuse les MeshCollider concaves)
            float falloff = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(Vector3.Distance(closest, point) / radius));

            var target = col.GetComponentInParent<ShootableTarget>();
            if (target != null && target != directTarget && hitTargets.Add(target))
                target.Hit(stats.damage * 0.6f * falloff);

            var robot = col.GetComponentInParent<RobotHealth>();
            if (robot != null && robot.gameObject != gameObject && robot != directRobot && hitRobots.Add(robot))
                robot.Damage(point, stats.damage * 0.6f, radius, transform.position);

            // Souffle : robots ennemis, debris detaches, caisses (jamais le tireur)
            var rb = col.attachedRigidbody;
            if (rb != null && rb != body && !rb.isKinematic && pushed.Add(rb))
            {
                // Un robot encaisse une petite poussee (sinon il serait jongle a 6 tirs/s),
                // un debris ou une caisse est projete
                bool isRobot = rb.GetComponent<RobotHealth>() != null;
                float kick = isRobot ? 0.45f + 0.04f * stats.damage / 30f : 3f;
                rb.AddExplosionForce(rb.mass * kick, point, radius * 1.3f, 0.3f, ForceMode.Impulse);
            }
        }

        // Effets : la zone est montree au rayon exact
        PlasmaExplosion.Spawn(point, normal, radius, PlasmaColor, hologramShader);
        float k = Mathf.Clamp(radius / 3f, 0.8f, 1.8f);
        Burst(plasmaBurst, point + normal * 0.2f, normal, Mathf.RoundToInt(26 * k), PlasmaColor, 0.35f * k, 0.9f * k, 2f * k, 8f * k, 0.4f);
        Burst(plasmaEmbers, point + normal * 0.1f, normal, Mathf.RoundToInt(34 * k), Color.white, 0.05f, 0.12f, 6f * k, 15f * k, 0.6f);
        Burst(sparks, point, normal, Mathf.RoundToInt(14 * k), Color.white, 0.08f, 0.16f, 4f, 10f, 0.25f);
        Burst(plasmaSmoke, point + normal * 0.4f, normal, Mathf.RoundToInt(10 * k), PlasmaColor, 0.8f * k, 1.6f * k, 0.6f, 1.8f, 1.3f);
        RobotFollowCamera.Shake(point, 0.22f + 0.05f * radius, 28f);
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
        hologramShader = holo;
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
            plasmaBurst = BuildParticles(dust, "PlasmaBurst", 300, stretch: false, PlasmaColor);
            plasmaEmbers = BuildParticles(dust, "PlasmaEmbers", 400, stretch: true, new Color(1f, 0.75f, 1f));
            plasmaSmoke = BuildParticles(dust, "PlasmaSmoke", 160, stretch: false, new Color(0.28f, 0.22f, 0.34f, 0.45f), additive: false);
            teslaArcs = BuildParticles(dust, "TeslaArcs", 300, stretch: true, TeslaColor);
            muzzleFlash = BuildParticles(dust, "MuzzleFlash", 80, stretch: false, new Color(1f, 0.93f, 0.8f));
            muzzleSmoke = BuildParticles(dust, "MuzzleSmoke", 120, stretch: false, new Color(0.5f, 0.5f, 0.52f, 0.3f), additive: false);
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

    private ParticleSystem BuildParticles(Shader shader, string name, int max, bool stretch, Color tint, bool additive = true)
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
        mat.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
        mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        mat.SetFloat("_Additive", additive ? 1f : 0f);
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
