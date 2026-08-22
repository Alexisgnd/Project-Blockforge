using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// MOLETTE DE COULEUR DU SPRAY PAINT
// Un cran de molette souris deplace l'aiguille sur le wedge
// voisin de la roue (ordre angulaire reel), et teinte le
// liquide, la surface et les bulles avec la couleur du
// wedge vise (lue sur son materiau).
// Toute la geometrie (axe de la roue, angles) est calculee
// depuis les transforms a l'execution : robuste aux
// rotations bakees de l'export Blender, cf. PlierRotator.
// =========================================================

public class PaintSprayColorWheel : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Transform wheelCenter;       // color_wheel
    public Transform needle;            // pivot de l'aiguille
    public Transform needleArrow;       // fleche : donne la direction courante
    public Transform[] wedges;          // wedge_0..11
    public Renderer liquidRenderer;     // can_liquid
    public Renderer surfaceRenderer;    // liquid_surface
    public Renderer[] bubbleRenderers;  // bubble_0..6
    [Tooltip("La molette est ignoree quand l'inventaire est ouvert")]
    public GarageInventoryController inventory;

    [Header("Rotation")]
    public float rotationSpeed = 540f;  // degres par seconde
    public bool invertScroll;

    [Header("Teinte")]
    [Range(0f, 1f)]
    [Tooltip("Eclaircissement des bulles/surface par rapport au liquide")]
    public float bubbleLighten = 0.5f;
    [Tooltip("Emission du liquide en nits (exposition fixe EV 9.5)")]
    public float emissiveNits = 45f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    private int[] angularOrder;         // indices de wedges tries par angle
    private int orderPos;               // position courante dans angularOrder
    private Vector3 wheelNormalLocal;   // normale de la roue, en local wheelCenter

    private Material liquidMat;
    private Material bubbleMat;         // partage par les 7 bulles
    private Material surfaceMat;

    private int SelectedWedge => angularOrder[orderPos];

    private void Awake()
    {
        if (wheelCenter == null || needle == null || needleArrow == null ||
            wedges == null || wedges.Length < 2)
        {
            Debug.LogWarning("[PaintSprayColorWheel] References manquantes, molette desactivee.");
            enabled = false;
            return;
        }

        BuildAngularOrder();
        CreateMaterialInstances();

        // Selection initiale = wedge le plus proche de l'aiguille
        orderPos = ClosestOrderPos();
        ApplyColor();
    }

    private void OnDestroy()
    {
        if (liquidMat != null) Destroy(liquidMat);
        if (bubbleMat != null) Destroy(bubbleMat);
        if (surfaceMat != null) Destroy(surfaceMat);
    }

    private void Update()
    {
        HandleScroll();
        AnimateNeedle();
    }

    // =====================================================
    // GEOMETRIE DE LA ROUE
    // =====================================================

    private Vector3 WedgeDirLocal(int i)
    {
        return wheelCenter.InverseTransformPoint(wedges[i].position);
    }

    private void BuildAngularOrder()
    {
        // Normale du plan de la roue : produit vectoriel le moins
        // degenere entre le premier wedge et un autre.
        Vector3 d0 = WedgeDirLocal(0);
        Vector3 best = Vector3.zero;
        for (int i = 1; i < wedges.Length; i++)
        {
            Vector3 c = Vector3.Cross(d0, WedgeDirLocal(i));
            if (c.sqrMagnitude > best.sqrMagnitude)
                best = c;
        }
        wheelNormalLocal = best.normalized;

        // Tri des wedges par angle signe autour de la normale
        var angles = new float[wedges.Length];
        angularOrder = new int[wedges.Length];
        for (int i = 0; i < wedges.Length; i++)
        {
            angularOrder[i] = i;
            angles[i] = Vector3.SignedAngle(d0, WedgeDirLocal(i), wheelNormalLocal);
        }
        System.Array.Sort(angularOrder, (a, b) => angles[a].CompareTo(angles[b]));
    }

    private Vector3 AxisWorld()
    {
        return wheelCenter.TransformDirection(wheelNormalLocal);
    }

    private Vector3 NeedleDirWorld()
    {
        return Vector3.ProjectOnPlane(
            needleArrow.position - wheelCenter.position, AxisWorld());
    }

    private Vector3 WedgeDirWorld(int i)
    {
        return Vector3.ProjectOnPlane(
            wedges[i].position - wheelCenter.position, AxisWorld());
    }

    private int ClosestOrderPos()
    {
        Vector3 dir = NeedleDirWorld();
        int bestPos = 0;
        float bestAngle = float.MaxValue;
        for (int pos = 0; pos < angularOrder.Length; pos++)
        {
            float a = Mathf.Abs(Vector3.Angle(dir, WedgeDirWorld(angularOrder[pos])));
            if (a < bestAngle)
            {
                bestAngle = a;
                bestPos = pos;
            }
        }
        return bestPos;
    }

    // =====================================================
    // MOLETTE + ANIMATION DE L'AIGUILLE
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

        int n = angularOrder.Length;
        orderPos = (orderPos + step + n) % n;
        ApplyColor();
    }

    private void AnimateNeedle()
    {
        Vector3 axis = AxisWorld();
        Vector3 current = NeedleDirWorld();
        Vector3 target = WedgeDirWorld(SelectedWedge);
        if (current.sqrMagnitude < 1e-8f || target.sqrMagnitude < 1e-8f)
            return;

        float remaining = Vector3.SignedAngle(current, target, axis);
        if (Mathf.Abs(remaining) < 0.05f)
            return;

        float delta = Mathf.MoveTowards(0f, remaining, rotationSpeed * Time.deltaTime);
        needle.RotateAround(wheelCenter.position, axis, delta);
    }

    // =====================================================
    // TEINTE DU LIQUIDE
    // =====================================================

    private void CreateMaterialInstances()
    {
        if (liquidRenderer != null)
        {
            liquidMat = new Material(liquidRenderer.sharedMaterial);
            liquidRenderer.sharedMaterial = liquidMat;
        }

        if (bubbleRenderers != null && bubbleRenderers.Length > 0 &&
            bubbleRenderers[0] != null)
        {
            bubbleMat = new Material(bubbleRenderers[0].sharedMaterial);
            foreach (var r in bubbleRenderers)
            {
                if (r != null)
                    r.sharedMaterial = bubbleMat;
            }
        }

        if (surfaceRenderer != null)
        {
            surfaceMat = new Material(surfaceRenderer.sharedMaterial);
            // La surface se dessine apres le liquide (sinon elle
            // apparait noyee sous la teinte du liquide).
            surfaceMat.renderQueue = surfaceMat.renderQueue + 2;
            surfaceRenderer.sharedMaterial = surfaceMat;
        }
    }

    private void ApplyColor()
    {
        var wedgeRenderer = wedges[SelectedWedge] != null
            ? wedges[SelectedWedge].GetComponent<Renderer>()
            : null;
        if (wedgeRenderer == null || wedgeRenderer.sharedMaterial == null)
            return;

        Color hue = wedgeRenderer.sharedMaterial.GetColor(BaseColorId);
        Color light = Color.Lerp(hue, Color.white, bubbleLighten);

        if (liquidMat != null)
            Tint(liquidMat, hue, emissiveNits);

        if (bubbleMat != null)
            Tint(bubbleMat, light, emissiveNits * 0.6f);

        if (surfaceMat != null)
            Tint(surfaceMat, light, emissiveNits * 0.6f);
    }

    // Change la teinte en conservant l'alpha du materiau
    private static void Tint(Material mat, Color rgb, float nits)
    {
        float alpha = mat.GetColor(BaseColorId).a;
        mat.SetColor(BaseColorId, new Color(rgb.r, rgb.g, rgb.b, alpha));
        mat.SetColor(EmissiveColorId, rgb * nits);
    }
}
