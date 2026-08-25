using UnityEngine;

// =========================================================
// APERCU DU BLOC SELECTIONNE SUR LA PINCE
// L'apercu est un objet independant (pas parente a la plaque,
// qui a une echelle non uniforme) : il suit la surface du
// support_plate en position/rotation a chaque frame.
// ExtraYaw reflete la rotation choisie a la molette.
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

    [Header("Rotation (pilotee par le systeme de pose)")]
    public float extraYaw;

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

    // L'apercu vit a la racine de la scene (pas parente a la plaque) :
    // il suit l'activation de la pince a la main, sinon il resterait
    // fige en l'air quand on passe au spray paint (GarageToolSwitcher).
    // NB : ce composant est exclu de disableWhileOpen, donc ces
    // callbacks ne se declenchent qu'au switch d'outil.
    private void OnEnable()
    {
        if (current != null)
            current.SetActive(true);
    }

    private void OnDisable()
    {
        if (current != null)
            current.SetActive(false);
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

        Quaternion rotation = Quaternion.AngleAxis(extraYaw, up) * Quaternion.LookRotation(forward, up);
        current.transform.SetPositionAndRotation(position, rotation);
    }

    public void Show(BlockDefinition def)
    {
        if (current != null)
            Destroy(current);
        if (def == null)
            return;

        current = BlockPreviewFactory.CreateVisual(def, blockSize, out currentHalfHeight);
        current.name = "SelectedBlockPreview";

        // Bloc choisi dans l'inventaire pendant que le spray est en main :
        // l'apercu nait cache, il apparaitra au retour de la pince.
        current.SetActive(isActiveAndEnabled);

        LateUpdate(); // positionne immediatement
    }
}
