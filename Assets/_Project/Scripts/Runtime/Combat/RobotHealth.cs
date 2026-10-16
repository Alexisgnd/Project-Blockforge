using System.Collections.Generic;
using UnityEngine;

// =========================================================
// SANTE DU ROBOT : DEGATS BLOC PAR BLOC (match)
// Pose sur chaque robot assemble (joueur, ennemi). Chaque bloc
// (PlacedBlockView) a ses points de vie (BlockDefinition
// .resistanceHp x hpScale : les valeurs du catalogue sont
// pensees pour la fiche, pas pour des combats de quelques
// secondes). La carrosserie s'assombrit avec les degats (Tint). Un impact (Damage) touche le bloc le plus
// proche du point d'impact ; une zone (radius > 0) touche tous
// les blocs dans le rayon, degats decroissants.
// - Bloc a 0 : detruit -> invisible, sans collision, eclat de
//   debris ; un bloc de mouvement detruit ne porte plus
//   (RobotLocomotion.SetUnitDisabled), une arme detruite ne tire
//   plus (PlacedBlockView.destroyed)
// - Connexite : les blocs sont voisins par leurs cases (cells) ;
//   quand un bloc casse coupe le robot en morceaux, seul le plus
//   gros morceau (PV) reste, les autres sont detruits
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
    [Tooltip("PV d'un bloc = resistanceHp du catalogue x ce facteur (un cube de 1000 -> 100 : ~12 bolts de laser N1, 3 plasmas N1, 1 rail N1)")]
    public float hpScale = 0.1f;
    [Tooltip("Couleur d'un bloc a 0 PV : la carrosserie s'assombrit avec les degats")]
    public Color damagedTint = new(0.22f, 0.2f, 0.19f, 1f);

    public class Block
    {
        public PlacedBlockView view;
        public float max, hp;
        public Renderer[] renderers;
        public Collider[] colliders;
        public Vector3 localCenter;
        public Color[][] baseColors;     // _BaseColor d'origine de chaque materiau (null : pas de base color)
        public readonly List<Block> neighbours = new();   // blocs en contact par une face (connexite)
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
    public event System.Action<Vector3, float> Hit;   // impact subi (position du tireur ou point d'impact, degats) : HUD
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
            float max = Mathf.Max(1f, (view.definition != null ? view.definition.resistanceHp : 1000f) * hpScale);
            var b = new Block
            {
                view = view,
                max = max,
                hp = max,
                renderers = view.GetComponentsInChildren<Renderer>(true),
                colliders = view.GetComponentsInChildren<Collider>(true),
                localCenter = transform.InverseTransformPoint(view.transform.position),
            };
            b.baseColors = new Color[b.renderers.Length][];
            for (int i = 0; i < b.renderers.Length; i++)
            {
                var mats = b.renderers[i].sharedMaterials;
                b.baseColors[i] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                    b.baseColors[i][m] = mats[m] != null && mats[m].HasProperty(BaseColorId) ? mats[m].GetColor(BaseColorId) : new Color(0f, 0f, 0f, -1f);
            }
            blocks.Add(b);
            byView[view] = b;
            MaxHp += max;
        }
        Hp = MaxHp;
        BuildNeighbours();
        BuildDebris();
    }

    // Voisinage par les cases occupees (PlacedBlockView.cells) : deux blocs
    // sont voisins quand une case de l'un touche une case de l'autre par une
    // face. Un bloc sans voisin (robot d'un seul bloc) n'est jamais detache.
    private void BuildNeighbours()
    {
        var byCell = new Dictionary<Vector3Int, Block>();
        foreach (var b in blocks)
        {
            var cells = b.view.cells ?? new[] { b.view.cell };
            foreach (var c in cells)
                byCell[c] = b;
        }
        Vector3Int[] dirs = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
        foreach (var b in blocks)
        {
            var cells = b.view.cells ?? new[] { b.view.cell };
            foreach (var c in cells)
            {
                foreach (var d in dirs)
                {
                    if (byCell.TryGetValue(c + d, out var other) && other != b && !b.neighbours.Contains(other))
                        b.neighbours.Add(other);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (debris != null) Destroy(debris.gameObject);
        if (debrisMat != null) Destroy(debrisMat);
    }

    public Block Get(PlacedBlockView view) => view != null && byView.TryGetValue(view, out var b) ? b : null;

    // ---------- Degats ----------

    // Impact en 'point' ; radius > 0 : zone (degats decroissants jusqu'au bord)
    // from : position du tireur (direction affichee par le HUD), sinon le point d'impact
    public void Damage(Vector3 point, float amount, float radius = 0f, Vector3? from = null)
    {
        if (IsDead || amount < minDamage)
            return;
        bool any = false, broke = false;
        if (radius <= 0f)
        {
            var target = Nearest(point, 2.5f);
            if (target != null)
            {
                broke |= Apply(target, amount);
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
                broke |= Apply(b, amount * Mathf.Lerp(1f, 0.25f, d / radius));
                any = true;
            }
        }
        if (!any)
            return;
        if (broke)
            DropDetached();
        LastDamageTime = Time.time;
        Hit?.Invoke(from ?? point, amount);
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

    // Retourne vrai si le bloc vient d'etre detruit
    private bool Apply(Block b, float amount)
    {
        b.hp = Mathf.Max(0f, b.hp - amount);
        bool broke = b.Destroyed;
        if (broke)
            SetDestroyed(b, true);
        Tint(b);
        BlockChanged?.Invoke(b);
        return broke;
    }

    // Un robot coupe en deux n'a plus qu'un corps : seul le plus gros morceau
    // (somme des PV) reste ; les blocs des autres morceaux, plus attaches a
    // rien, sont detruits avec leurs debris.
    private readonly List<Block> island = new();
    private readonly HashSet<Block> visited = new();

    private void DropDetached()
    {
        visited.Clear();
        List<Block> best = null;
        float bestHp = -1f;
        var islands = new List<List<Block>>();
        foreach (var start in blocks)
        {
            if (start.Destroyed || visited.Contains(start))
                continue;
            var part = new List<Block>();
            float hp = 0f;
            island.Clear();
            island.Add(start);
            visited.Add(start);
            while (island.Count > 0)
            {
                var b = island[island.Count - 1];
                island.RemoveAt(island.Count - 1);
                part.Add(b);
                hp += b.hp;
                foreach (var n in b.neighbours)
                {
                    if (!n.Destroyed && visited.Add(n))
                        island.Add(n);
                }
            }
            islands.Add(part);
            if (hp > bestHp)
            {
                bestHp = hp;
                best = part;
            }
        }
        if (islands.Count <= 1)
            return;
        foreach (var part in islands)
        {
            if (part == best)
                continue;
            foreach (var b in part)
            {
                b.hp = 0f;
                SetDestroyed(b, true);
                Tint(b);
                BlockChanged?.Invoke(b);
            }
        }
    }

    // Un bloc detruit ne repousse que contre un bloc vivant
    private bool CanRegrow(Block b)
    {
        if (!b.Destroyed || b.neighbours.Count == 0)
            return true;
        foreach (var n in b.neighbours)
        {
            if (!n.Destroyed)
                return true;
        }
        return false;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static MaterialPropertyBlock propertyBlock;

    // La carrosserie perd sa couleur avec les PV (MaterialPropertyBlock : les
    // materiaux partages ne sont pas touches)
    private void Tint(Block b)
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
        float k = Mathf.Clamp01(b.Ratio);
        for (int i = 0; i < b.renderers.Length; i++)
        {
            var r = b.renderers[i];
            if (r == null || b.baseColors[i].Length == 0 || b.baseColors[i][0].a < 0f)
                continue;
            r.GetPropertyBlock(propertyBlock);
            Color baseColor = b.baseColors[i][0];
            // A 0 PV le bloc garde un tiers de sa couleur : abime, pas noir
            Color worst = Color.Lerp(damagedTint, baseColor, 0.35f);
            propertyBlock.SetColor(BaseColorId, Color.Lerp(worst, baseColor, k));
            r.SetPropertyBlock(propertyBlock);
        }
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

    // Centre des blocs encore en vie (cible des IA) ; Center() sinon
    public Vector3 AliveCenter()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var b in blocks)
        {
            if (b.Destroyed)
                continue;
            sum += b.localCenter;
            n++;
        }
        return n > 0 ? sum / n : Center();
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
            if (b.hp < b.max && CanRegrow(b))
                damaged++;
        }
        if (damaged == 0)
            return;
        float each = budget / damaged;
        bool regrown = false;
        foreach (var b in blocks)
        {
            if (b.hp >= b.max || !CanRegrow(b))
                continue;
            bool wasDestroyed = b.Destroyed;
            b.hp = Mathf.Min(b.max, b.hp + each);
            if (wasDestroyed && !b.Destroyed)
            {
                SetDestroyed(b, false);
                regrown = true;
            }
            Tint(b);
            BlockChanged?.Invoke(b);
        }
        Recompute();
        if (regrown)
            Resettle();
    }

    // Un robot reduit a quelques blocs tombe de travers, voire le nez dans le
    // sol : quand des blocs repoussent, on s'assure qu'ils ne reapparaissent
    // pas dans le decor. Si le robot est penche ou qu'un bloc vivant traverse
    // le sol, il est remis d'aplomb (lacet conserve) et pose sur le sol.
    private void Resettle()
    {
        var body = GetComponent<Rigidbody>();
        if (body == null || body.isKinematic)
            return;
        int mask = ~((1 << RobotTestSpawner.RobotLayer) | (1 << 2));   // ni robots, ni debris (Ignore Raycast)
        bool tilted = Vector3.Angle(transform.up, Vector3.up) > 20f;
        bool buried = false;
        Vector3 half = Vector3.one * 0.42f;
        foreach (var b in blocks)
        {
            if (b.Destroyed)
                continue;
            if (Physics.CheckBox(transform.TransformPoint(b.localCenter), half, transform.rotation, mask, QueryTriggerInteraction.Ignore))
            {
                buried = true;
                break;
            }
        }
        if (!tilted && !buried)
            return;

        // D'aplomb, puis pose : le bas des blocs vivants a 10 cm du sol
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        float minY = float.MaxValue;
        Vector3 center = transform.TransformPoint(AliveCenter());
        foreach (var b in blocks)
        {
            if (!b.Destroyed)
                minY = Mathf.Min(minY, transform.TransformPoint(b.localCenter).y - 0.5f);
        }
        if (minY == float.MaxValue)
            return;
        float groundY = Physics.Raycast(center + Vector3.up * 30f, Vector3.down, out var hit, 80f, mask, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : minY;
        float lift = groundY + 0.1f - minY;
        if (lift > 0f)
            transform.position += Vector3.up * lift;
        body.position = transform.position;
        body.rotation = transform.rotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
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
