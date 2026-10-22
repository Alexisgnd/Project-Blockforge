using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// REDRESSEMENT DU ROBOT (match, facon Robocraft)
// Touche R (RobotTestDriver -> Begin). Le robot est souleve en
// levitation (corps cinematique, liftHeight), remis a plat en
// gardant son cap, puis relache : il retombe sur ses appuis.
// Pendant la manoeuvre la locomotion est coupee. Refuse en l'air
// (seulement au sol, meme retourne) et pendant la recharge.
// Effets (particules additives Blockforge/DustParticle, teinte
// accent) : anneau d'impulsion au sol au depart, panache
// d'energie qui monte autour du robot pendant la rotation,
// second anneau a l'atterrissage. Temps de recharge entre deux
// redressements (cooldown) ; le HUD peut lire Running /
// CooldownFraction.
// BeginAt(point) : meme manoeuvre, mais le robot est repose plus
// loin (sortie de blocage des bots).
// =========================================================

[RequireComponent(typeof(Rigidbody))]
public class RobotRecovery : MonoBehaviour
{
    [Header("Manoeuvre")]
    public float duration = 1.1f;
    public float liftHeight = 1.6f;
    public float cooldown = 3f;
    public Color effectColor = new(0.22f, 0.72f, 0.91f, 1f);

    public bool Running { get; private set; }
    public float ReadyAt { get; private set; }
    public float CooldownFraction => Mathf.Clamp01(1f - (ReadyAt - Time.time) / Mathf.Max(0.01f, cooldown));

    private Rigidbody body;
    private RobotLocomotion loco;
    private ParticleSystem ring, plume;
    private Material ringMat, plumeMat;
    private ParticleSystem.EmitParams emit;
    private float robotHeight = 1f, robotRadius = 1.5f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        loco = GetComponent<RobotLocomotion>();
        var shader = Shader.Find("Blockforge/DustParticle");
        if (shader != null)
        {
            ring = Build(shader, "RecoveryRing", out ringMat, stretch: true);
            plume = Build(shader, "RecoveryPlume", out plumeMat, stretch: false);
        }
    }

    private void OnDestroy()
    {
        if (ring != null) Destroy(ring.gameObject);
        if (plume != null) Destroy(plume.gameObject);
        if (ringMat != null) Destroy(ringMat);
        if (plumeMat != null) Destroy(plumeMat);
    }

    // Lance le redressement si possible (false : deja en cours, en recharge, ou en
    // l'air : il faut etre au sol, meme retourne)
    public bool Begin() => BeginAt(null);

    // Variante des bots : souleve le robot et le repose plus loin (point du NavMesh),
    // quand le redressement sur place ne suffit pas (chassis pose sur une crete,
    // chenilles dans le vide : le retomber au meme endroit le rebloque)
    public bool BeginAt(Vector3? moveTo)
    {
        if (Running || Time.time < ReadyAt)
            return false;
        var seq = GetComponent<RobotSpawnSequence>();
        if (seq != null && seq.Running)
            return false;
        var health = GetComponent<RobotHealth>();
        if (health != null && health.IsDead)
            return false;
        if (!OnGround())
        {
            Toast.Warning("Self-righting only works on the ground.");
            return false;
        }
        StartCoroutine(Run(moveTo));
        return true;
    }

    // Au sol : un appui porte, ou le decor est a portee sous le robot (retourne,
    // couche sur le flanc, pose sur son toit)
    private bool OnGround()
    {
        if (loco != null && loco.GroundedContacts > 0)
            return true;
        float height = loco != null ? loco.RobotHeight : 1f;
        Vector3 origin = body != null ? body.worldCenterOfMass : transform.position;
        float reach = height * 0.8f + 0.6f;
        int mask = ~(1 << RobotTestSpawner.RobotLayer);
        foreach (var dir in new[] { Vector3.down, -transform.up, transform.up, transform.right, -transform.right, transform.forward, -transform.forward })
        {
            if (Physics.Raycast(origin, dir, reach, mask, QueryTriggerInteraction.Ignore))
                return true;
        }
        return false;
    }

    private IEnumerator Run(Vector3? moveTo)
    {
        Running = true;
        if (loco != null)
        {
            robotHeight = loco.RobotHeight;
            if (loco.TryGetLocalBounds(transform, out var rb))
                robotRadius = Mathf.Max(0.8f, rb.extents.x, rb.extents.z);
            loco.throttle = loco.steer = loco.lift = 0f;
            loco.enabled = false;
        }
        var mode = body.collisionDetectionMode;
        if (!body.isKinematic)   // deja fige (sequence d'apparition) : pas de vitesse a poser
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 1e-4f)
            flatForward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
        Quaternion endRot = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        // Hauteur finale : assez haut pour que le robot a plat ne touche pas le sol
        float ground = startPos.y;
        if (Physics.Raycast(startPos + Vector3.up * 2f, Vector3.down, out var hit, 30f, ~(1 << RobotTestSpawner.RobotLayer), QueryTriggerInteraction.Ignore))
            ground = hit.point.y;
        Vector3 endPos = new Vector3(startPos.x, Mathf.Max(startPos.y, ground) + liftHeight + robotHeight * 0.5f, startPos.z);
        float time = duration;
        float landGround = ground;
        if (moveTo.HasValue)
        {
            // Deplacement : on monte d'abord au-dessus du relief, puis on glisse jusqu'au point
            Vector3 to = moveTo.Value;
            landGround = to.y;
            if (Physics.Raycast(new Vector3(to.x, to.y + 6f, to.z), Vector3.down, out var th, 30f, ~(1 << RobotTestSpawner.RobotLayer), QueryTriggerInteraction.Ignore))
                landGround = th.point.y;
            endPos = new Vector3(to.x, landGround + liftHeight + robotHeight * 0.5f, to.z);
            time = Mathf.Clamp(duration * (1f + Vector3.Distance(startPos, endPos) / 10f), duration, 2.6f);
        }
        float arc = moveTo.HasValue ? Mathf.Max(2f, robotHeight) : 0f;

        Pulse(new Vector3(startPos.x, ground + 0.1f, startPos.z), 46, 1f);

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.05f, time));
            float k = Mathf.SmoothStep(0f, 1f, t);
            Vector3 pos = Vector3.Lerp(startPos, endPos, k) + Vector3.up * (arc * Mathf.Sin(k * Mathf.PI));
            transform.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, endRot, k));
            Plume(transform.position, 3);
            yield return null;
        }

        body.isKinematic = false;
        body.collisionDetectionMode = mode;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        if (loco != null)
            loco.enabled = true;
        ReadyAt = Time.time + cooldown;

        // Anneau a l'atterrissage : des que le robot retouche le sol (ou au bout d'une seconde)
        float wait = 0f;
        while (wait < 1f && (loco == null || loco.GroundedContacts == 0))
        {
            wait += Time.deltaTime;
            yield return null;
        }
        Pulse(new Vector3(transform.position.x, landGround + 0.1f, transform.position.z), 32, 0.7f);
        Running = false;
    }

    // Anneau d'impulsion horizontal
    private void Pulse(Vector3 at, int count, float strength)
    {
        if (ring == null)
            return;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count + Random.Range(-0.05f, 0.05f);
            var dir = new Vector3(Mathf.Cos(a), 0.08f, Mathf.Sin(a));
            emit.position = at + dir * 0.3f;
            emit.velocity = dir * Random.Range(7f, 10f) * strength;
            emit.startSize = Random.Range(0.18f, 0.32f);
            emit.startLifetime = Random.Range(0.35f, 0.55f);
            ring.Emit(emit, 1);
        }
    }

    // Panache qui monte autour du robot
    private void Plume(Vector3 center, int count)
    {
        if (plume == null)
            return;
        for (int i = 0; i < count; i++)
        {
            Vector2 c = Random.insideUnitCircle.normalized * Random.Range(robotRadius * 0.6f, robotRadius * 1.3f);
            emit.position = center + new Vector3(c.x, -robotHeight * 0.5f + Random.Range(-0.2f, 0.2f), c.y);
            emit.velocity = Vector3.up * Random.Range(2.5f, 4.5f) + Random.insideUnitSphere * 0.4f;
            emit.startSize = Random.Range(0.25f, 0.5f);
            emit.startLifetime = Random.Range(0.5f, 0.9f);
            plume.Emit(emit, 1);
        }
    }

    private ParticleSystem Build(Shader shader, string name, out Material material, bool stretch)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = stretch ? 0.25f : 0.1f;
        limit.limit = stretch ? 2f : 3f;

        material = new Material(shader) { name = name + " (instance)" };
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_Additive", 1f);
        material.SetFloat("_Softness", 0.85f);
        material.SetFloat("_FadeIn", 0.05f);
        material.SetFloat("_FadeOut", 0.35f);
        material.SetColor("_Tint", effectColor);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = stretch ? 0.08f : 0f;
        r.lengthScale = stretch ? 1.2f : 1f;
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
