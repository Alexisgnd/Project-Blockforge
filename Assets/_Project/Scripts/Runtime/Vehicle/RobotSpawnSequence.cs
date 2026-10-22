using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// APPARITION DU ROBOT (match) : assemblage holographique
// Lance par RobotTestSpawner juste apres l'assemblage :
// 1. Le robot est pose en levitation (hoverHeight au-dessus du
//    spawn), corps cinematique, invisible ; pilotage, physique,
//    visee et poussiere desactives
// 2. Assemblage : les blocs apparaissent un a un (du bas vers
//    le haut, puis de l'arriere vers l'avant) en hologramme
//    (shader Blockforge/Hologram, teinte accent), chacun avec
//    un petit "pop" d'echelle ; cadence calculee pour tenir
//    dans ~1,2 s quel que soit le nombre de blocs
// 3. Materialisation : un front monte du bas vers le haut du
//    robot et rend a chaque bloc ses vrais materiaux (couleurs
//    de peinture comprises) avec une pulsation
// 4. Le corps redevient dynamique : le robot retombe sur ses
//    suspensions et les controles sont rendus au joueur
// "Reduire les animations" (BlockforgeTheme) saute tout.
// =========================================================

public class RobotSpawnSequence : MonoBehaviour
{
    [Header("Reglages")]
    public float hoverHeight = 1.3f;
    public float assembleDuration = 1.2f;       // phase 2, pour tous les blocs
    public float materializeDuration = 0.45f;   // phase 3
    public float popDuration = 0.16f;
    public Color hologramColor = new(0.22f, 0.72f, 0.91f, 1f); // accent 39B8E8
    public float hologramIntensity = 1.3f;

    public bool Running { get; private set; }

    private class Piece
    {
        public PlacedBlockView view;
        public Transform visual;
        public Vector3 baseScale;
        public Renderer[] renderers;
        public Material[][] materials;
        public float localY;            // centre, repere du robot
        public float popStart = -1f;
        public bool materialized;
    }

    private readonly List<Piece> pieces = new();
    private Material holoMat;
    private Rigidbody body;
    private CollisionDetectionMode bodyMode;
    private Behaviour[] toEnable;

    // Fige le robot, le rend invisible et lance la sequence ; 'controls' sont
    // reactives a la fin (pilotage, locomotion, visee, poussiere...)
    public void Begin(Behaviour[] controls)
    {
        toEnable = controls;
        body = GetComponent<Rigidbody>();
        foreach (var b in toEnable)
        {
            if (b != null)
                b.enabled = false;
        }

        if (BlockforgeTheme.ReduceMotion || Shader.Find("Blockforge/Hologram") == null)
        {
            Finish();
            return;
        }

        Collect();
        Running = true;
        StartCoroutine(Run());
    }

    private void Collect()
    {
        pieces.Clear();
        foreach (var view in GetComponentsInChildren<PlacedBlockView>())
        {
            var visual = BlockColliders.Visual(view.transform);
            if (visual == null)
                continue;
            var renderers = new List<Renderer>();
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;
                renderers.Add(r);
            }
            var piece = new Piece
            {
                view = view,
                visual = visual,
                baseScale = visual.localScale,
                renderers = renderers.ToArray(),
                materials = new Material[renderers.Count][],
                localY = transform.InverseTransformPoint(view.transform.position).y,
            };
            for (int i = 0; i < renderers.Count; i++)
            {
                piece.materials[i] = renderers[i].sharedMaterials;
                renderers[i].enabled = false;
            }
            pieces.Add(piece);
        }
        // Du bas vers le haut, puis de l'arriere vers l'avant, puis de gauche a droite
        pieces.Sort((a, b) =>
        {
            int c = a.view.cell.y.CompareTo(b.view.cell.y);
            if (c != 0) return c;
            c = a.view.cell.z.CompareTo(b.view.cell.z);
            return c != 0 ? c : a.view.cell.x.CompareTo(b.view.cell.x);
        });
    }

    private IEnumerator Run()
    {
        // 1. Levitation, corps fige
        Vector3 hoverPos = transform.position + Vector3.up * hoverHeight;
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            // (un corps cinematique n'accepte que la detection speculative)
            bodyMode = body.collisionDetectionMode;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
        }
        transform.position = hoverPos;

        // Hologramme : un seul materiau pour tout le robot, lignes continues
        // d'un bloc a l'autre (hauteur absolue), devoile en entier
        holoMat = new Material(Shader.Find("Blockforge/Hologram")) { name = "SpawnHologram (instance)" };
        holoMat.SetColor("_Color", hologramColor);
        holoMat.SetFloat("_Intensity", hologramIntensity);
        holoMat.SetFloat("_FillAlpha", 0.34f);
        holoMat.SetFloat("_RimStrength", 1.5f);
        holoMat.SetFloat("_LineStrength", 0.5f);
        holoMat.SetFloat("_LineDensity", 14f);
        holoMat.SetFloat("_Flicker", 0.12f);
        holoMat.SetFloat("_Reveal", 1f);
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in pieces)
        {
            minY = Mathf.Min(minY, p.localY);
            maxY = Mathf.Max(maxY, p.localY);
        }
        if (pieces.Count == 0)
            minY = maxY = 0f;
        float halfHeight = Mathf.Max(0.5f, (maxY - minY) * 0.5f + 0.5f);
        float centerY = (minY + maxY) * 0.5f;

        // 2. Assemblage bloc par bloc
        float interval = Mathf.Clamp(assembleDuration / Mathf.Max(1, pieces.Count), 0.012f, 0.08f);
        float t = 0f;
        int next = 0;
        float lastAppear = -1f;
        while (next < pieces.Count || t < lastAppear + popDuration)
        {
            float dt = Time.deltaTime;
            t += dt;
            while (next < pieces.Count && t >= next * interval)
            {
                Appear(pieces[next], t);
                lastAppear = t;
                next++;
            }
            Bob(hoverPos, t);
            UpdateHologram(centerY, halfHeight);
            Pop(t);
            yield return null;
        }

        yield return new WaitForSeconds(0.12f);

        // 3. Materialisation : front du bas vers le haut
        float sweep = 0f;
        while (sweep < 1f)
        {
            float dt = Time.deltaTime;
            sweep += dt / Mathf.Max(0.05f, materializeDuration);
            t += dt;
            float front = Mathf.Lerp(minY - 0.3f, maxY + 0.3f, Mathf.SmoothStep(0f, 1f, sweep));
            foreach (var p in pieces)
            {
                if (!p.materialized && p.localY <= front)
                    Materialize(p, t);
            }
            Bob(hoverPos, t);
            UpdateHologram(centerY, halfHeight);
            Pop(t);
            yield return null;
        }
        foreach (var p in pieces)
        {
            if (!p.materialized)
                Materialize(p, t);
            p.visual.localScale = p.baseScale;
        }

        // 4. Chute et controles
        transform.position = hoverPos;
        Finish();
    }

    private void Appear(Piece p, float now)
    {
        foreach (var r in p.renderers)
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = holoMat;
            r.sharedMaterials = mats;
            r.enabled = true;
        }
        p.popStart = now;
        p.visual.localScale = p.baseScale * 0.5f;
    }

    private void Materialize(Piece p, float now)
    {
        for (int i = 0; i < p.renderers.Length; i++)
            p.renderers[i].sharedMaterials = p.materials[i];
        p.materialized = true;
        p.popStart = now - popDuration * 0.45f;   // courte pulsation (fin du pop)
    }

    // Pop d'echelle des blocs apparus (ease-out-back)
    private void Pop(float now)
    {
        foreach (var p in pieces)
        {
            if (p.popStart < 0f)
                continue;
            float k = (now - p.popStart) / popDuration;
            if (k >= 1f)
            {
                p.visual.localScale = p.baseScale;
                p.popStart = -1f;
                continue;
            }
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = k - 1f;
            float back = 1f + c3 * u * u * u + c1 * u * u;
            p.visual.localScale = p.baseScale * Mathf.LerpUnclamped(0.5f, 1f, back);
        }
    }

    private void Bob(Vector3 hoverPos, float t)
    {
        transform.position = hoverPos + Vector3.up * (Mathf.Sin(t * 2.2f) * 0.06f);
    }

    private void UpdateHologram(float centerY, float halfHeight)
    {
        if (holoMat == null)
            return;
        holoMat.SetVector("_HoloCenter", transform.TransformPoint(new Vector3(0f, centerY, 0f)));
        holoMat.SetVector("_HoloUp", transform.up);
        holoMat.SetFloat("_HoloHalfHeight", halfHeight);
    }

    private void Finish()
    {
        foreach (var p in pieces)
        {
            for (int i = 0; i < p.renderers.Length; i++)
            {
                if (p.renderers[i] == null)
                    continue;
                if (!p.materialized)
                    p.renderers[i].sharedMaterials = p.materials[i];
                p.renderers[i].enabled = true;
            }
            if (p.visual != null)
                p.visual.localScale = p.baseScale;
        }
        if (body != null && body.isKinematic)
        {
            body.isKinematic = false;
            body.collisionDetectionMode = bodyMode;
        }
        foreach (var b in toEnable)
        {
            if (b != null)
                b.enabled = true;
        }
        if (holoMat != null)
        {
            Destroy(holoMat);
            holoMat = null;
        }
        Running = false;
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (holoMat != null)
            Destroy(holoMat);
    }
}
