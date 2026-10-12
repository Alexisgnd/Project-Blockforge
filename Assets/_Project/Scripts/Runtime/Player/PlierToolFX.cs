using System.Collections.Generic;
using UnityEngine;

// =========================================================
// EFFETS DE LA PINCE V2 (modele Tools/Blender/plier_v2_pipeline)
// - Molette : la tete (3 griffes) tourne d'un tiers de tour par
//   cran, avec un ressort (leger depassement puis retour)
// - Pose d'un bloc : les griffes se referment d'un coup sec puis
//   se rouvrent, l'outil recule un peu, les lumieres flashent et
//   l'hologramme pulse
// - Retrait : les griffes s'ouvrent grand puis reviennent
// - Pose refusee : petit tressautement, lumieres et hologramme
//   passent brievement au rouge (danger du design system)
// - Au repos : griffes qui "respirent", cellule d'energie et
//   lentille qui pulsent doucement
// Les axes sont deduits des reperes du modele (rotations cuites
// dans les maillages a l'export) : axe de la tete = tete ->
// ancre de l'hologramme, charniere = axe x rayon de la griffe.
// Respecte BlockforgeTheme.ReduceMotion (pas de rebonds).
// =========================================================

public class PlierToolFX : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public GarageBuildController build;
    public PlierHologram hologram;
    public Transform model;              // recul
    public Transform head;
    public Transform[] jaws;
    public Transform holoAnchor;
    public Renderer[] glows;             // anneaux, bandeaux, liseres (Glow_Accent)
    public Renderer[] cellCores;         // barreaux de la cellule d'energie
    public Renderer lens;                // lentille de l'emetteur

    [Header("Tete")]
    public float stepAngle = 120f;
    public float headStiffness = 260f;
    public float headDamping = 20f;

    [Header("Griffes (degres, + = ouvert)")]
    public float jawRest = 5f;
    public float jawBreath = 1.2f;
    public float placeClose = -16f;
    public float removeOpen = 24f;
    public float jawFollow = 28f;        // reactivite (1/s)
    public float kickDecay = 7f;

    [Header("Recul (x taille de l'outil)")]
    public float recoil = 0.035f;

    [Header("Lumieres")]
    public Color glowColor = new(0.2f, 0.85f, 1f, 1f);
    public float glowNits = 800f;
    public float lensNits = 2400f;

    private Vector3 headAxis;            // repere du parent de la tete
    private Quaternion headBase;
    private float headAngle, headTarget, headVelocity;

    private Vector3[] hingeAxes;         // repere de la tete
    private Quaternion[] jawBase;
    private float jawAngle, jawKick;

    private Vector3 modelBase, recoilDir;
    private float recoilAmount, toolSize = 0.3f;
    private float flash, danger;
    private MaterialPropertyBlock block;
    private static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");

    private void Awake()
    {
        block = new MaterialPropertyBlock();

        if (head != null && holoAnchor != null)
        {
            headBase = head.localRotation;
            var parent = head.parent;
            Vector3 axisWorld = holoAnchor.position - head.position;
            headAxis = parent != null ? parent.InverseTransformDirection(axisWorld).normalized : axisWorld.normalized;

            // Charnieres : dans le repere de la tete (au repos : meme orientation que son parent)
            Vector3 axisHead = Quaternion.Inverse(headBase) * headAxis;
            int count = jaws != null ? jaws.Length : 0;
            hingeAxes = new Vector3[count];
            jawBase = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                if (jaws[i] == null)
                    continue;
                jawBase[i] = jaws[i].localRotation;
                Vector3 radial = Vector3.ProjectOnPlane(jaws[i].localPosition, axisHead).normalized;
                hingeAxes[i] = Vector3.Cross(axisHead, radial).normalized;
            }
        }

        if (model != null)
        {
            modelBase = model.localPosition;
            if (head != null && holoAnchor != null && model.parent != null)
                recoilDir = -model.parent.InverseTransformDirection(holoAnchor.position - head.position).normalized;
            if (BlockPreviewFactory.TryGetBounds(model.gameObject, out var b) && model.parent != null)
                toolSize = Mathf.Max(b.size.x, b.size.y, b.size.z) / Mathf.Max(model.parent.lossyScale.x, 1e-5f);
        }
        jawAngle = jawRest;
    }

    private void OnEnable()
    {
        if (build == null)
            return;
        build.SpinStepped += OnSpin;
        build.BlockPlaced += OnPlaced;
        build.BlockRemoved += OnRemoved;
        build.PlaceRefused += OnRefused;
    }

    private void OnDisable()
    {
        if (build == null)
            return;
        build.SpinStepped -= OnSpin;
        build.BlockPlaced -= OnPlaced;
        build.BlockRemoved -= OnRemoved;
        build.PlaceRefused -= OnRefused;
    }

    // ---------- Evenements ----------

    private void OnSpin(int direction)
    {
        headTarget += stepAngle * Mathf.Sign(direction);
    }

    private void OnPlaced()
    {
        jawKick = placeClose - jawRest;
        recoilAmount = 1f;
        flash = 1f;
        hologram?.Flash();
    }

    private void OnRemoved()
    {
        jawKick = removeOpen - jawRest;
        flash = 0.6f;
    }

    private void OnRefused()
    {
        jawKick = -6f;
        danger = 1f;
        hologram?.Refuse();
    }

    // ---------- Animation ----------

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        bool still = BlockforgeTheme.ReduceMotion;

        // Tete : ressort amorti vers l'angle cible (sans rebond en mouvement reduit)
        if (head != null)
        {
            if (still)
            {
                headAngle = headTarget;
                headVelocity = 0f;
            }
            else
            {
                headVelocity += (headTarget - headAngle) * headStiffness * dt;
                headVelocity *= Mathf.Exp(-headDamping * dt);
                headAngle += headVelocity * dt;
            }
            // angles bornes (evite la derive des flottants apres des centaines de crans)
            if (Mathf.Abs(headTarget) > 3600f && Mathf.Abs(headTarget - headAngle) < 0.01f)
            {
                float wrap = Mathf.Floor(headTarget / 360f) * 360f;
                headTarget -= wrap;
                headAngle -= wrap;
            }
            head.localRotation = Quaternion.AngleAxis(headAngle, headAxis) * headBase;
        }

        // Griffes : repos qui respire + impulsion (pose / retrait) qui retombe
        jawKick *= Mathf.Exp(-kickDecay * dt);
        float breath = still ? 0f : Mathf.Sin(Time.time * 1.7f) * jawBreath;
        float jawTarget = jawRest + breath + jawKick;
        jawAngle = Mathf.Lerp(jawAngle, jawTarget, 1f - Mathf.Exp(-jawFollow * dt));
        if (jaws != null && hingeAxes != null)
        {
            for (int i = 0; i < jaws.Length; i++)
            {
                if (jaws[i] != null)
                    jaws[i].localRotation = Quaternion.AngleAxis(jawAngle, hingeAxes[i]) * jawBase[i];
            }
        }

        // Recul : coup sec vers l'arriere puis retour
        recoilAmount = Mathf.MoveTowards(recoilAmount, 0f, dt * 6f);
        if (model != null)
        {
            float k = still ? 0f : Mathf.Sin(recoilAmount * Mathf.PI * 0.5f);
            model.localPosition = modelBase + recoilDir * (k * recoil * toolSize);
        }

        // Lumieres
        flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
        danger = Mathf.MoveTowards(danger, 0f, dt * 2.2f);
        Color tint = Color.Lerp(glowColor, BlockforgeTheme.Danger, danger);
        float pulse = still ? 1f : 0.9f + 0.1f * Mathf.Sin(Time.time * 2.4f);
        SetEmission(glows, tint * glowNits * (1f + flash * 1.5f));
        SetEmission(cellCores, tint * glowNits * pulse * (1f + flash * 2f));
        if (lens != null)
            SetEmission(lens, tint * lensNits * pulse * (1f + flash * 2f));
    }

    private void SetEmission(Renderer[] renderers, Color emissive)
    {
        if (renderers == null)
            return;
        foreach (var r in renderers)
            SetEmission(r, emissive);
    }

    private void SetEmission(Renderer renderer, Color emissive)
    {
        if (renderer == null)
            return;
        renderer.GetPropertyBlock(block);
        block.SetColor(EmissiveId, emissive);
        renderer.SetPropertyBlock(block);
    }
}
