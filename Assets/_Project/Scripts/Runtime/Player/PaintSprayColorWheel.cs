using System;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// MOLETTE DE COULEUR DU PISTOLET (v3)
// La bague des 12 pastilles (Colour_Ring_Pivot) tourne sous un
// index fixe (Selected_Colour_Marker) : un cran de molette souris
// = un cran de bague (360 / 12 = 30 degres).
// - Rotation pilotee par un ressort amorti : la bague part vite,
//   depasse un peu le cran puis s'y cale (effet "clic"), et le
//   pistolet recoit un petit a-coup a chaque cran.
// - L'angle est recalcule a chaque image depuis la rotation de
//   repos (pas de rotation cumulee qui deriverait).
// - Axe et ordre des pastilles deduits de la geometrie au
//   demarrage : insensible aux conventions d'axes de l'export.
// SelectedColorIndex (1..12, BlockPalette) est lu par la peinture
// des blocs ; ColorChanged previent PaintSprayFX (teinte du liquide,
// des bulles et des bandeaux lumineux).
// =========================================================

public class PaintSprayColorWheel : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Transform wheelCenter;       // Colour_Wheel
    public Transform needle;            // Colour_Ring_Pivot : la bague qui tourne
    public Transform needleArrow;       // Selected_Colour_Marker : l'index fixe
    public Transform[] wedges;          // Colour_Swatch_1_00..11
    [Tooltip("Modele recevant l'a-coup de chaque cran (PaintSpray_Model)")]
    public Transform kickTarget;
    [Tooltip("La molette est ignoree quand l'inventaire est ouvert")]
    public GarageInventoryController inventory;

    [Header("Crans")]
    public bool invertScroll;
    [Tooltip("Raideur du ressort de la bague")]
    public float stiffness = 520f;
    [Tooltip("Amortissement (< 2 * racine(raideur) : la bague depasse le cran puis revient)")]
    public float damping = 30f;
    [Tooltip("A-coup du pistolet a chaque cran (degres)")]
    public float kickDegrees = 2.2f;

    public event Action<Color, int> ColorChanged;

    // Teinte de la case sous l'index dans la palette des blocs (1..12)
    public int SelectedColorIndex { get; private set; } = 1;
    public Color SelectedColor { get; private set; } = Color.white;

    private Vector3 axisLocal;          // axe de la bague, dans le repere du parent de la bague
    private Quaternion ringRest;        // rotation de repos de la bague
    private float[] wedgeAngles;        // angle de chaque pastille par rapport a l'index (repos)
    private float stepDegrees;

    private float targetAngle;          // angle vise (multiple du cran, non borne)
    private float angle;                // angle affiche
    private float velocity;

    private Quaternion kickRest;
    private float kick, kickVelocity;

    private void Awake()
    {
        if (wheelCenter == null || needle == null || needleArrow == null || wedges == null || wedges.Length < 2)
        {
            Debug.LogWarning("[PaintSprayColorWheel] References manquantes, molette desactivee.");
            enabled = false;
            return;
        }

        ringRest = needle.localRotation;
        if (kickTarget != null)
            kickRest = kickTarget.localRotation;
        BuildGeometry();
        Select(Nearest(0f), notify: true);
    }

    private void OnDisable()
    {
        // Pose propre si l'outil est range pendant une animation
        angle = targetAngle;
        velocity = 0f;
        ApplyAngle();
        kick = kickVelocity = 0f;
        if (kickTarget != null)
            kickTarget.localRotation = kickRest;
    }

    private void Update()
    {
        HandleScroll();
        Animate(Time.deltaTime);
    }

    // =====================================================
    // GEOMETRIE
    // =====================================================

    private void BuildGeometry()
    {
        // Positions dans le repere du parent de la bague (fixe pendant la rotation)
        var space = needle.parent != null ? needle.parent : wheelCenter;
        Vector3 center = space.InverseTransformPoint(wheelCenter.position);
        var dirs = new Vector3[wedges.Length];
        for (int i = 0; i < wedges.Length; i++)
            dirs[i] = space.InverseTransformPoint(Center(wedges[i])) - center;

        // Normale du plan des pastilles : produit vectoriel le moins degenere
        // entre ECARTS de pastilles (les vecteurs centre -> pastille ne sont pas
        // dans ce plan : la bague est decalee sur le flanc, le centre est dans
        // l'axe du corps).
        Vector3 best = Vector3.zero;
        int n = dirs.Length;
        for (int i = 1; i < n; i++)
        {
            var c = Vector3.Cross(dirs[i] - dirs[0], dirs[(i + n / 4) % n] - dirs[0]);
            if (c.sqrMagnitude > best.sqrMagnitude)
                best = c;
        }
        axisLocal = best.normalized;

        Vector3 marker = Vector3.ProjectOnPlane(space.InverseTransformPoint(Center(needleArrow)) - center, axisLocal);
        wedgeAngles = new float[wedges.Length];
        for (int i = 0; i < wedges.Length; i++)
            wedgeAngles[i] = Vector3.SignedAngle(marker, Vector3.ProjectOnPlane(dirs[i], axisLocal), axisLocal);
        stepDegrees = 360f / wedges.Length;
    }

    private static Vector3 Center(Transform t)
    {
        return t.TryGetComponent<Renderer>(out var r) ? r.bounds.center : t.position;
    }

    // Pastille sous l'index pour un angle de bague donne
    private int Nearest(float ringAngle)
    {
        int best = 0;
        float bestDelta = float.MaxValue;
        for (int i = 0; i < wedgeAngles.Length; i++)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(0f, wedgeAngles[i] + ringAngle));
            if (d < bestDelta)
            {
                bestDelta = d;
                best = i;
            }
        }
        return best;
    }

    // =====================================================
    // MOLETTE
    // =====================================================

    private void HandleScroll()
    {
        if (inventory != null && inventory.IsOpen)
            return;
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) < 0.01f)
            return;

        int step = scrollY > 0f ? 1 : -1;
        if (invertScroll)
            step = -step;

        // Cale la cible sur le cran exact de la pastille suivante
        int current = Nearest(targetAngle);
        float aligned = -wedgeAngles[current];
        targetAngle = aligned + Mathf.Round((targetAngle - aligned) / 360f) * 360f + step * stepDegrees;
        Select(Nearest(targetAngle), notify: true);

        kickVelocity += step * kickDegrees * 18f;
    }

    private void Select(int wedge, bool notify)
    {
        var renderer = wedges[wedge] != null ? wedges[wedge].GetComponent<Renderer>() : null;
        if (renderer == null || renderer.sharedMaterial == null)
            return;

        Color hue = renderer.sharedMaterial.HasProperty("_BaseColor")
            ? renderer.sharedMaterial.GetColor("_BaseColor")
            : renderer.sharedMaterial.color;
        hue.a = 1f;
        SelectedColor = hue;
        SelectedColorIndex = BlockPalette.Nearest(hue);
        if (notify)
            ColorChanged?.Invoke(SelectedColor, SelectedColorIndex);
    }

    // =====================================================
    // ANIMATION (ressorts amortis, pas fixe)
    // =====================================================

    private void Animate(float dt)
    {
        dt = Mathf.Min(dt, 0.05f);
        if (BlockforgeTheme.ReduceMotion)
        {
            angle = targetAngle;
            velocity = 0f;
            kick = kickVelocity = 0f;
        }
        else
        {
            const float sub = 1f / 240f;
            for (float t = 0f; t < dt; t += sub)
            {
                float h = Mathf.Min(sub, dt - t);
                velocity += ((targetAngle - angle) * stiffness - velocity * damping) * h;
                angle += velocity * h;
                kickVelocity += (-kick * 900f - kickVelocity * 26f) * h;
                kick += kickVelocity * h;
            }
        }
        ApplyAngle();
        if (kickTarget != null)
            kickTarget.localRotation = kickRest * Quaternion.AngleAxis(kick, KickAxis());
    }

    private void ApplyAngle()
    {
        needle.localRotation = Quaternion.AngleAxis(angle, axisLocal) * ringRest;
    }

    // L'a-coup fait rouler le pistolet autour de l'axe de la molette
    private Vector3 KickAxis()
    {
        var space = needle.parent != null ? needle.parent : wheelCenter;
        Vector3 world = space.TransformDirection(axisLocal);
        return kickTarget.parent != null ? kickTarget.parent.InverseTransformDirection(world) : world;
    }
}
