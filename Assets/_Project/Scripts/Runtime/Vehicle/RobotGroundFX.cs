using System.Collections.Generic;
using UnityEngine;

// =========================================================
// POUSSIERE AU SOL DES ROUES, CHENILLES ET PATTES (match)
// Pose sur le robot par RobotTestSpawner, lit RobotLocomotion :
// trois ParticleSystem (un par regime, espace monde, shader
// Blockforge/DustParticle = disque doux teinte par le materiau,
// fondu d'apres l'age de la particule transmis par le flux de
// sommets AgePercent), alimentes a la main (Emit) a chaque
// contact au sol :
// - Roulage : petits nuages sable derriere le contact, de plus
//   en plus denses avec la vitesse (a partir de 1,5 m/s)
// - Derapage : derive laterale du contact (slip) au-dela de
//   1,5 m/s -> bouffees sombres, denses, projetees du cote
//   oppose a la glisse
// - Acceleration / patinage : gaz a fond, adherence saturee
//   (traction > 0,8) et robot encore lent -> gros nuages clairs
//   projetes vers l'arriere, comme un depart en trombe
// Rien en l'air, ni pour les lames de survol, rotors,
// propulseurs. Le robot etant sur son propre calque, la
// poussiere reste sur Default : la minimap ne la photographie
// pas plus que le robot (elle est prise une fois au depart).
// =========================================================

[RequireComponent(typeof(RobotLocomotion))]
public class RobotGroundFX : MonoBehaviour
{
    [Header("Roulage")]
    public float rollMinSpeed = 1.5f;
    public float rollMaxRate = 18f;             // particules / s / contact a pleine vitesse
    public Color rollColor = new(0.58f, 0.55f, 0.50f, 0.42f);

    [Header("Derapage")]
    public float slipThreshold = 1.5f;          // m/s de derive laterale
    public float slipRate = 45f;
    public Color slipColor = new(0.36f, 0.34f, 0.32f, 0.6f);

    [Header("Acceleration")]
    public float burnoutTraction = 0.8f;        // part d'adherence consommee
    public float burnoutRate = 40f;
    public Color burnoutColor = new(0.62f, 0.58f, 0.52f, 0.55f);

    private const int Roll = 0, Slip = 1, Burnout = 2;

    private RobotLocomotion loco;
    private GameObject host;
    private readonly ParticleSystem[] systems = new ParticleSystem[3];
    private readonly Material[] materials = new Material[3];
    private float[] accumulators;               // un par contact (toutes unites confondues)
    private ParticleSystem.EmitParams emit;

    private void Awake()
    {
        loco = GetComponent<RobotLocomotion>();
        BuildSystems();
    }

    private void Start()
    {
        int n = 0;
        foreach (var u in loco.units)
            n += u.contacts.Count;
        accumulators = new float[n];
    }

    private void OnDestroy()
    {
        foreach (var m in materials)
        {
            if (m != null)
                Destroy(m);
        }
        if (host != null)
            Destroy(host);
    }

    private void BuildSystems()
    {
        var shader = Shader.Find("Blockforge/DustParticle");
        if (shader == null)
        {
            Debug.LogWarning("[RobotGroundFX] Shader Blockforge/DustParticle introuvable : pas de poussiere.");
            return;
        }

        // Hors du robot (espace monde, calque Default) : detruit avec lui (OnDestroy)
        host = new GameObject("GroundDust");
        host.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
        systems[Roll] = BuildSystem(shader, Roll, "Roll", rollColor, 400);
        systems[Slip] = BuildSystem(shader, Slip, "Slip", slipColor, 300);
        systems[Burnout] = BuildSystem(shader, Burnout, "Burnout", burnoutColor, 300);
    }

    private ParticleSystem BuildSystem(Shader shader, int index, string name, Color tint, int maxParticles)
    {
        var go = new GameObject("Dust_" + name);
        go.transform.SetParent(host.transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.startLifetime = 1f;
        main.startSpeed = 0f;
        main.startSize = 0.5f;
        main.gravityModifier = -0.02f;          // la poussiere monte un peu

        var emission = system.emission;
        emission.enabled = false;               // tout passe par Emit()

        var shape = system.shape;
        shape.enabled = false;

        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.6f));

        var limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.35f;
        limit.limit = 1.5f;

        var rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-40f * Mathf.Deg2Rad, 40f * Mathf.Deg2Rad);

        var material = new Material(shader) { name = $"Dust {name} (instance)" };
        material.SetColor("_Tint", tint);
        materials[index] = material;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.alignment = ParticleSystemRenderSpace.View;
        // Age de la particule (0..1) dans TEXCOORD0.z : le shader fait le fondu
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.AgePercent,
        });

        system.Play();
        return system;
    }

    private void Update()
    {
        if (host == null || loco == null || !loco.enabled || accumulators == null || loco.Body == null)
            return;
        float dt = Time.deltaTime;
        Vector3 forward = transform.forward;
        float throttle = loco.throttle;
        int index = 0;

        foreach (var unit in loco.units)
        {
            bool dusty = unit.kind == LocomotionKind.Wheel || unit.kind == LocomotionKind.Track || unit.kind == LocomotionKind.Leg;
            float maxSpeed = Mathf.Max(1f, unit.maxSpeed);
            bool burnout = dusty && unit.anyGrounded && Mathf.Abs(throttle) > 0.5f
                           && unit.traction >= burnoutTraction && Mathf.Abs(unit.groundSpeed) < maxSpeed * 0.55f;

            foreach (var c in unit.contacts)
            {
                if (index >= accumulators.Length)
                    return;
                float acc = accumulators[index];
                if (!dusty || !c.grounded)
                {
                    accumulators[index++] = 0f;
                    continue;
                }

                float speed = Mathf.Abs(c.forwardSpeed);
                float slip = Mathf.Abs(c.slip);
                float rate = 0f;
                int regime = Roll;
                if (slip > slipThreshold)
                {
                    regime = Slip;
                    rate = slipRate * Mathf.Clamp01(slip / 6f);
                }
                else if (burnout)
                {
                    regime = Burnout;
                    rate = burnoutRate * Mathf.Clamp01(Mathf.Abs(throttle));
                }
                else if (speed > rollMinSpeed)
                {
                    rate = rollMaxRate * Mathf.Clamp01((speed - rollMinSpeed) / (maxSpeed - rollMinSpeed));
                }

                acc += rate * dt;
                int count = Mathf.FloorToInt(acc);
                acc -= count;
                accumulators[index++] = acc;
                for (int i = 0; i < count; i++)
                    EmitOne(c, unit, regime, forward, throttle);
            }
        }
    }

    private void EmitOne(LocomotionContact c, LocomotionUnit unit, int regime, Vector3 forward, float throttle)
    {
        var system = systems[regime];
        if (system == null)
            return;
        Vector3 fwd = Vector3.ProjectOnPlane(forward, c.normal).normalized;
        float dir = Mathf.Sign(c.forwardSpeed == 0f ? throttle : c.forwardSpeed);
        Vector3 jitter = Random.insideUnitSphere * 0.12f;
        Vector3 origin = c.point + c.normal * 0.08f + jitter;
        float r = Mathf.Max(0.15f, unit.radius);

        switch (regime)
        {
            case Slip: // du cote oppose a la glisse, nuages serres
                emit.position = origin - fwd * (dir * r * 0.4f);
                emit.velocity = -c.lateral * (Mathf.Sign(c.slip) * Mathf.Min(Mathf.Abs(c.slip), 6f) * 0.35f)
                                + c.normal * Random.Range(0.8f, 1.6f) + Random.insideUnitSphere * 0.4f;
                emit.startSize = Random.Range(0.45f, 0.75f) * r * 2f;
                emit.startLifetime = Random.Range(0.5f, 0.8f);
                break;
            case Burnout: // projetes vers l'arriere et sur les cotes, gros et clairs
                emit.position = origin - fwd * (Mathf.Sign(throttle) * r * 0.9f);
                emit.velocity = -fwd * (Mathf.Sign(throttle) * Random.Range(3.5f, 6.5f))
                                + c.lateral * Random.Range(-2.5f, 2.5f)
                                + c.normal * Random.Range(1.2f, 2.4f) + Random.insideUnitSphere * 0.6f;
                emit.startSize = Random.Range(0.9f, 1.4f) * r * 2f;
                emit.startLifetime = Random.Range(1f, 1.5f);
                break;
            default: // roulage : traine legere derriere le contact
                float speed = Mathf.Abs(c.forwardSpeed);
                emit.position = origin - fwd * (dir * r * 0.8f);
                emit.velocity = -fwd * (dir * speed * 0.12f) + c.normal * Random.Range(0.4f, 0.9f) + Random.insideUnitSphere * 0.3f;
                emit.startSize = Random.Range(0.5f, 0.8f) * r * 2f;
                emit.startLifetime = Random.Range(0.7f, 1.1f);
                break;
        }
        emit.rotation = Random.Range(0f, 360f);
        system.Emit(emit, 1);
    }
}
