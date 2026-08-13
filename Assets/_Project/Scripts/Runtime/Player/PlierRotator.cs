using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// ROTATION DE LA TETE DE PINCE A LA MOLETTE
// Chaque cran de molette fait tourner les machoires et leur
// support (wrist_hub) de 120 degres (360/3) autour de l'axe
// du bras, avec une animation. Les crans s'additionnent si
// le joueur enchaine.
// =========================================================

public class PlierRotator : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Transform wristHub;
    [Tooltip("Ce qui tourne : CW_Plier_Rig (machoires), wrist_hub, wrist_ring...")]
    public Transform[] rotatingParts;
    [Tooltip("Les 3 machoires, pour calculer l'axe de rotation (leur centre)")]
    public Transform[] jaws;
    [Tooltip("La molette est ignoree quand l'inventaire est ouvert")]
    public GarageInventoryController inventory;

    [Header("Rotation")]
    public float stepAngle = 120f;      // 360 / 3 machoires
    public float rotationSpeed = 360f;  // degres par seconde

    private float currentAngle;
    private float targetAngle;

    private void Update()
    {
        HandleScroll();
        Animate();
    }

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

        // Un cran = un pas de 120° (le sens suit le sens de la molette)
        targetAngle += stepAngle * Mathf.Sign(scrollY);
    }

    private void Animate()
    {
        if (Mathf.Approximately(currentAngle, targetAngle) || wristHub == null)
            return;

        float next = Mathf.MoveTowards(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        float delta = next - currentAngle;
        currentAngle = next;

        Vector3 axis = ComputeAxis();
        Vector3 pivot = wristHub.position;

        foreach (var part in rotatingParts)
        {
            if (part != null)
                part.RotateAround(pivot, axis, delta);
        }
    }

    // Axe du bras : du centre du wrist_hub vers le centre des 3 machoires.
    // Robuste aux rotations bakees de l'export Blender, et suit le bras
    // quand le joueur bouge (recalcule a chaque frame).
    private Vector3 ComputeAxis()
    {
        if (jaws != null && jaws.Length > 0)
        {
            Vector3 centroid = Vector3.zero;
            int count = 0;
            foreach (var jaw in jaws)
            {
                if (jaw == null)
                    continue;
                centroid += jaw.position;
                count++;
            }

            if (count > 0)
            {
                centroid /= count;
                Vector3 axis = centroid - wristHub.position;
                if (axis.sqrMagnitude > 0.0001f)
                    return axis.normalized;
            }
        }

        // Secours : direction racine du bras -> wrist_hub
        Vector3 fallback = wristHub.position - transform.position;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : wristHub.forward;
    }
}
