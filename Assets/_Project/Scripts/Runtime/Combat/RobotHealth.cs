using System.Collections.Generic;
using UnityEngine;

// =========================================================
// SANTE DU ROBOT : DEGATS BLOC PAR BLOC (match)
// Pose sur chaque robot assemble (joueur, ennemi). Chaque bloc
// (PlacedBlockView) a ses points de vie (BlockDefinition
// .resistanceHp). Un impact (Damage) touche le bloc le plus
// proche du point d'impact ; une zone (radius > 0) touche tous
// les blocs dans le rayon, degats decroissants.
// - Bloc a 0 : detruit -> invisible, sans collision, eclat de
//   debris ; un bloc de mouvement detruit ne porte plus
//   (RobotLocomotion.SetUnitDisabled), une arme detruite ne tire
//   plus (PlacedBlockView.destroyed)
// - Regeneration : apres regenDelay secondes sans degats, les
//   blocs se reparent (regenPerSecond en part des PV max,
//   repartie) et les blocs detruits reviennent des qu'ils ont
//   des PV
// - Robot detruit quand la somme des PV passe sous destroyFraction
//   des PV max : explosion, tout cache, event Destroyed (le
//   spawner fait reapparaitre le robot)
// Expose l'etat au HUD (heatmap des blocs : ratio par bloc,
// Damaged, LastDamageTime) et aux IA (IsDead).
// =========================================================

public class RobotHealth : MonoBehaviour
{
    [Header("Regles")]
    [Tooltip("Part des PV max sous laquelle le robot est detruit")]
    public float destroyFraction = 0.15f;
    public float regenDelay = 6f;
    [Tooltip("Part des PV max reparee par seconde")]
    public float regenPerSecond = 0.12f;
    [Tooltip("Degats minimaux par impact pour qu'un bloc soit atteint")]
    public float minDamage = 1f;

    public class Block
    {
        public PlacedBlockView view;
        public float max, hp;
        public Renderer[] renderers;
        public Collider[] colliders;
        public Vector3 localCenter;
        public float Ratio => max > 0f ? hp / max : 0f;
        public bool Destroyed => hp <= 0f;
    }

    public readonly List<Block> blocks = new();
    public float MaxHp { get; private set; }
    public float Hp { get; private set; }
    public float Fraction => MaxHp > 0f ? Mathf.Clamp01(Hp / MaxHp) : 1f;
    public bool IsDead { get; private set; }
    public bool Damaged => Hp < MaxHp - 0.5f;
    public bool Regenerating { get; private set; }
    public float LastDamageTime { get; private set; } = -100f;
    public float LastFullTime { get; private set; } = -100f;

    public event System.Action<Block> BlockChanged;      // PV d'un bloc modifies (detruit / repare compris)
    public event System.Action HealthChanged;
    public event System.Action Destroyed;

    private RobotLocomotion loco;
    private ParticleSystem debris;
    private Material debrisMat;
    private ParticleSystem.EmitParams emit;
    private readonly Dictionary<PlacedBlockView, Block> byView = new();

    private void Awake()
    {
        loco = GetComponent<RobotLocomotion>();
        foreach (var view in GetComponentsInChildren<PlacedBlockView>())
        {
            float max = view.definition != null ? Mathf.Max(1, view.definition.resistanceHp) : 1000f;
            var b = new Block
            {
                view = view,
                max = max,
                hp = max,
                renderers = view.GetComponentsInChildren<Renderer>(true),
                colliders = view.GetComponentsInChildren<Collider>(true),
                localCenter = transform.InverseTransformPoint(view.transform.position),
            };
            blocks.Add(b);
            byView[view] = b;
            MaxHp += max;
        }
        Hp = MaxHp;
        BuildDebris();
    }

    private void OnDestroy()
    {
        if (debris != null) Destroy(debris.gameObject);
        if (debrisMat != null) Destroy(debrisMat);
    }

    public Block Get(PlacedBlockView view) => view != null && byView.TryGetValue(view, out var b) ? b : null;

    // ---------- Degats ----------

    // Impact en 'point' ; radius > 0 : zone (degats decroissants jusqu'au bord)
    public void Damage(Vector3 point, float amount, float radius = 0f)
    {
        if (IsDead || amount < minDamage)
            return;
        bool any = false;
        if (radius <= 0f)
        {
            var target = Nearest(point, 2.5f);
            if (target != null)
            {
                Apply(target, amount);
                any = true;
            }
        }
        else
        {
            foreach (var b in blocks)
            {
                if (b.Destroyed)
                    continue;
                float d = Vector3.Distance(transform.TransformPoint(b.localCenter), point);
                if (d > radius)
                    continue;
                Apply(b, amount * Mathf.Lerp(1f, 0.25f, d / radius));
                any = true;
            }
        }
        if (!any)
            return;
        LastDamageTime = Time.time;
        Regenerating = false;
        Recompute();
        if (Hp <= MaxHp * destroyFraction)
            Die();
    }

    private Block Nearest(Vector3 point, float maxDistance)
    {
        Block best = null;
        float bestD = maxDistance;
        foreach (var b in blocks)
        {
            if (b.Destroyed)
                continue;
            float d = Vector3.Distance(transform.TransformPoint(b.localCenter), point);
            if (d < bestD)
            {
                bestD = d;
                best = b;
            }
        }
        return best;
    }

    private void Apply(Block b, float amount)
    {
        b.hp = Mathf.Max(0f, b.hp - amount);
        if (b.Destroyed)
            SetDestroyed(b, true);
        BlockChanged?.Invoke(b);
    }

    private void SetDestroyed(Block b, bool destroyed)
    {
        foreach (var r in b.renderers)
        {
            if (r != null)
                r.enabled = !destroyed;
        }
        foreach (var c in b.colliders)
        {
            if (c != null)
                c.enabled = !destroyed;
        }
        b.view.destroyed = destroyed;
        if (loco != null)
            loco.SetUnitDisabled(b.view, destroyed);
        if (destroyed)
            Burst(transform.TransformPoint(b.localCenter), 14, 0.12f, 0.3f, 2f, 6f, 0.6f);
        else
            Burst(transform.TransformPoint(b.localCenter), 6, 0.1f, 0.2f, 0.5f, 1.5f, 0.4f);
    }

    private void Recompute()
    {
        float sum = 0f;
        foreach (var b in blocks)
            sum += b.hp;
        Hp = sum;
        if (Hp >= MaxHp - 0.5f)
            LastFullTime = Time.time;
        HealthChanged?.Invoke();
    }

    private void Die()
    {
        if (IsDead)
            return;
        IsDead = true;
        Vector3 c = transform.TransformPoint(Center());
        Burst(c, 70, 0.3f, 0.9f, 4f, 12f, 1.2f);
        foreach (var b in blocks)
        {
            foreach (var r in b.renderers)
            {
                if (r != null)
                    r.enabled = false;
            }
            foreach (var col in b.colliders)
            {
                if (col != null)
                    col.enabled = false;
            }
        }
        if (loco != null)
            loco.enabled = false;
        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        Destroyed?.Invoke();
    }

    public Vector3 Center()
    {
        if (blocks.Count == 0)
            return Vector3.zero;
        Vector3 sum = Vector3.zero;
        foreach (var b in blocks)
            sum += b.localCenter;
        return sum / blocks.Count;
    }

    // ---------- Regeneration ----------

    private void Update()
    {
        if (IsDead || !Damaged)
        {
            Regenerating = false;
            return;
        }
        if (Time.time - LastDamageTime < regenDelay)
            return;
        Regenerating = true;
        float budget = MaxHp * regenPerSecond * Time.deltaTime;
        int damaged = 0;
        foreach (var b in blocks)
        {
            if (b.hp < b.max)
                damaged++;
        }
        if (damaged == 0)
            return;
        float each = budget / damaged;
        foreach (var b in blocks)
        {
            if (b.hp >= b.max)
                continue;
            bool wasDestroyed = b.Destroyed;
            b.hp = Mathf.Min(b.max, b.hp + each);
            if (wasDestroyed && !b.Destroyed)
                SetDestroyed(b, false);
            BlockChanged?.Invoke(b);
        }
        Recompute();
    }

    // ---------- Debris ----------

    private void BuildDebris()
    {
        var shader = Shader.Find("Blockforge/DustParticle");
        if (shader == null)
            return;
        var go = new GameObject("Debris_" + name);
        debris = go.AddComponent<ParticleSystem>();
        debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = debris.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        main.startSpeed = 0f;
        main.gravityModifier = 0.9f;
        var emission = debris.emission;
        emission.enabled = false;
        var shape = debris.shape;
        shape.enabled = false;
        var rot = debris.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

        debrisMat = new Material(shader) { name = "Debris (instance)" };
        debrisMat.SetFloat("_Softness", 0.35f);
        debrisMat.SetFloat("_FadeIn", 0.02f);
        debrisMat.SetFloat("_FadeOut", 0.6f);
        debrisMat.SetColor("_Tint", new Color(0.35f, 0.33f, 0.32f, 0.95f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = debrisMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent,
        });
        debris.Play();
    }

    private void Burst(Vector3 at, int count, float sizeMin, float sizeMax, float speedMin, float speedMax, float life)
    {
        if (debris == null)
            return;
        for (int i = 0; i < count; i++)
        {
            emit.position = at + Random.insideUnitSphere * 0.3f;
            emit.velocity = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(speedMin, speedMax);
            emit.startSize = Random.Range(sizeMin, sizeMax);
            emit.startLifetime = life * Random.Range(0.6f, 1.4f);
            emit.rotation = Random.Range(0f, 360f);
            debris.Emit(emit, 1);
        }
    }
}
