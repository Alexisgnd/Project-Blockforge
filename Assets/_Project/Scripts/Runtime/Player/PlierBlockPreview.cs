using UnityEngine;

// =========================================================
// APERCU DU BLOC SELECTIONNE SUR LA PINCE
// L'apercu est un objet independant (pas parente a la plaque,
// qui a une echelle non uniforme) : il suit la surface du
// support_plate en position/rotation a chaque frame.
// Placeholder par forme/couleur en attendant les vrais modeles
// (renseigner BlockDefinition.previewPrefab pour les remplacer).
// =========================================================

public class PlierBlockPreview : MonoBehaviour
{
    [Header("References")]
    public GarageInventoryController inventory;
    public Transform supportPlate;

    [Header("Placement")]
    [Tooltip("Taille de reference (monde) d'un bloc 1x1x1")]
    public float blockSize = 0.22f;
    public float surfaceGap = 0.005f;
    [Tooltip("Ajustement manuel (droite / normale / avant de la surface)")]
    public Vector3 manualOffset = Vector3.zero;

    private GameObject current;
    private float currentHalfHeight;
    private Renderer plateRenderer;
    private Vector3 plateNormalLocal;  // axe local de la plaque qui pointe vers sa surface
    private float plateTopDistance;    // du centre de la plaque a sa surface, le long de cet axe

    private void Awake()
    {
        if (supportPlate == null)
            supportPlate = transform;

        plateRenderer = supportPlate.GetComponent<Renderer>();

        // La plaque vient de Blender avec une rotation bakee : son axe "surface"
        // n'est pas forcement +Y local. On prend l'axe local le plus proche du
        // haut du monde dans la pose initiale.
        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        float best = float.MinValue;
        int bestIndex = 1;
        float bestSign = 1f;
        for (int i = 0; i < axes.Length; i++)
        {
            float dot = Vector3.Dot(supportPlate.TransformDirection(axes[i]), Vector3.up);
            if (Mathf.Abs(dot) > best)
            {
                best = Mathf.Abs(dot);
                bestIndex = i;
                bestSign = Mathf.Sign(dot);
            }
        }
        plateNormalLocal = axes[bestIndex] * bestSign;

        var meshFilter = supportPlate.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 extents = meshFilter.sharedMesh.bounds.extents;
            Vector3 lossy = supportPlate.lossyScale;
            plateTopDistance = extents[bestIndex] * Mathf.Abs(lossy[bestIndex]);
        }

        if (inventory != null)
            inventory.BlockSelected += Show;
    }

    private void Start()
    {
        // Si l'inventaire a deja selectionne son bloc par defaut (Cube)
        if (current == null && inventory != null && inventory.CurrentBlock != null)
            Show(inventory.CurrentBlock);
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.BlockSelected -= Show;
        if (current != null)
            Destroy(current);
    }

    private void LateUpdate()
    {
        if (current == null)
            return;

        // Repere de la surface : normale reelle + tangentes
        Vector3 up = supportPlate.TransformDirection(plateNormalLocal).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(supportPlate.forward, up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(supportPlate.right, up);
        forward.Normalize();
        Vector3 right = Vector3.Cross(up, forward);

        Vector3 plateCenter = plateRenderer != null ? plateRenderer.bounds.center : supportPlate.position;
        Vector3 position = plateCenter
                           + up * (plateTopDistance + currentHalfHeight + surfaceGap + manualOffset.y)
                           + right * manualOffset.x
                           + forward * manualOffset.z;

        current.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
    }

    // =========================================================
    // CONSTRUCTION DE L'APERCU
    // =========================================================

    public void Show(BlockDefinition def)
    {
        if (current != null)
            Destroy(current);
        if (def == null)
            return;

        if (def.previewPrefab != null)
        {
            current = Instantiate(def.previewPrefab);
            currentHalfHeight = blockSize * 0.5f;
        }
        else
        {
            var (primitive, scale) = PickShape(def.blockName);
            current = GameObject.CreatePrimitive(primitive);

            var collider = current.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            current.transform.localScale = scale * blockSize;
            currentHalfHeight = HalfHeight(primitive, scale.y * blockSize);

            var renderer = current.GetComponent<Renderer>();
            renderer.material.color = CategoryColor(def.category);
        }

        current.name = "SelectedBlockPreview";
        LateUpdate(); // positionne immediatement
    }

    // Forme placeholder par nom de bloc (echelles en unites de blockSize)
    private static (PrimitiveType, Vector3) PickShape(string blockName)
    {
        string n = blockName.ToLowerInvariant();

        if (n.Contains("tige")) return (PrimitiveType.Cylinder, new Vector3(0.12f, 0.6f, 0.12f));
        if (n.Contains("roue")) return (PrimitiveType.Cylinder, new Vector3(0.9f, 0.1f, 0.9f));
        if (n.Contains("chenille")) return (PrimitiveType.Cube, new Vector3(1.4f, 0.35f, 0.5f));
        if (n.Contains("patte")) return (PrimitiveType.Capsule, new Vector3(0.25f, 0.5f, 0.25f));
        if (n.Contains("survol")) return (PrimitiveType.Cube, new Vector3(1.1f, 0.12f, 0.6f));
        if (n.Contains("hélice") || n.Contains("helice")) return (PrimitiveType.Cylinder, new Vector3(1.2f, 0.05f, 1.2f));
        if (n.Contains("aile")) return (PrimitiveType.Cube, new Vector3(1.5f, 0.08f, 0.7f));
        if (n.Contains("propulseur")) return (PrimitiveType.Cylinder, new Vector3(0.35f, 0.45f, 0.35f));
        if (n.Contains("laser")) return (PrimitiveType.Cube, new Vector3(1.3f, 0.22f, 0.22f));
        if (n.Contains("plasma")) return (PrimitiveType.Capsule, new Vector3(0.3f, 0.55f, 0.3f));
        if (n.Contains("canon")) return (PrimitiveType.Cube, new Vector3(1.5f, 0.3f, 0.3f));
        if (n.Contains("tesla")) return (PrimitiveType.Cube, new Vector3(0.9f, 0.5f, 0.08f));
        if (n.Contains("nano")) return (PrimitiveType.Sphere, new Vector3(0.6f, 0.6f, 0.6f));
        if (n.Contains("blindage")) return (PrimitiveType.Cube, new Vector3(1f, 0.15f, 1f));
        if (n.Contains("radar")) return (PrimitiveType.Sphere, new Vector3(0.5f, 0.5f, 0.5f));
        if (n.Contains("bouclier") || n.Contains("disque")) return (PrimitiveType.Cylinder, new Vector3(0.7f, 0.05f, 0.7f));
        if (n.Contains("pente")) return (PrimitiveType.Cube, new Vector3(1f, 0.5f, 1f));
        if (n.Contains("coin")) return (PrimitiveType.Cube, new Vector3(0.55f, 0.55f, 0.55f));
        if (n.Contains("intérieur") || n.Contains("interieur")) return (PrimitiveType.Cube, new Vector3(0.9f, 0.9f, 0.9f));

        return (PrimitiveType.Cube, Vector3.one); // Cube et defaut
    }

    // Demi-hauteur monde selon la primitive (cylindre/capsule font 2 unites de haut)
    private static float HalfHeight(PrimitiveType primitive, float scaleY)
    {
        return primitive switch
        {
            PrimitiveType.Cylinder => scaleY,
            PrimitiveType.Capsule => scaleY,
            _ => scaleY * 0.5f,
        };
    }

    private static Color CategoryColor(BlockCategory category)
    {
        return category switch
        {
            BlockCategory.Chassis => new Color(0.75f, 0.78f, 0.82f),
            BlockCategory.Mouvement => new Color(1.00f, 0.62f, 0.20f),
            BlockCategory.Armes => new Color(0.95f, 0.30f, 0.30f),
            BlockCategory.Defense => new Color(0.35f, 0.85f, 0.50f),
            BlockCategory.Special => new Color(0.30f, 0.75f, 1.00f),
            _ => Color.white,
        };
    }
}
