using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// SYSTEME DE POSE DE BLOCS SUR LA GRILLE (14x14, hauteur 50)
// - Ghost transparent sur la cellule visee (vert = ok, rouge = invalide)
// - Clic gauche : poser / clic droit : retirer
// - Molette : rotation du bloc par pas de 90 degres
// - Recharge le blueprint existant, met a jour stats et Dirty
// La grille est mesuree depuis les renderers de "buildGrid".
// =========================================================

public class GarageBuildController : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Camera viewCamera;
    public Transform gridRoot;
    public GarageInventoryController inventory;
    public GarageToolbar toolbar;
    public GarageStatsHUD statsHud;
    public PlierBlockPreview plierPreview;

    [Header("Ghost")]
    public Material ghostValidMaterial;
    public Material ghostInvalidMaterial;

    [Header("Reglages")]
    public float maxReach = 40f;

    private Vector3 gridMin;
    private float topY;
    private float cellX, cellZ, cellY;

    private readonly Dictionary<Vector3Int, PlacedBlockView> occupied = new();
    private readonly Dictionary<string, BlockDefinition> defsById = new();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    private Transform robotRoot;
    private GameObject ghost;
    private string ghostBlockId;
    private int rotationIndex;
    private int totalCpu;

    private BlockDefinition Selected => inventory != null ? inventory.CurrentBlock : null;

    private void Start()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;

        ComputeGridFromRenderers();

        if (inventory != null)
        {
            foreach (var def in inventory.blocks)
            {
                if (def != null)
                    defsById[def.name] = def;
            }
        }

        robotRoot = new GameObject("RobotRoot").transform;
        LoadBlueprint();
        UpdateStats();
    }

    private void OnDestroy()
    {
        if (ghost != null)
            Destroy(ghost);
    }

    private void Update()
    {
        // Curseur libre = popup ou inventaire ouvert : pas de construction
        if (Cursor.lockState != CursorLockMode.Locked || viewCamera == null)
        {
            HideGhost();
            return;
        }

        // Outil 1 (pince constructeur) uniquement
        if (toolbar != null && toolbar.SelectedIndex != 0)
        {
            HideGhost();
            return;
        }

        HandleWheelRotation();

        var def = Selected;
        if (def == null)
        {
            HideGhost();
            return;
        }

        bool hasTarget = TryGetTarget(out Vector3Int placeCell, out PlacedBlockView hoverBlock);
        var mouse = Mouse.current;

        // Retrait au clic droit (independant de la validite du ghost)
        if (hasTarget && hoverBlock != null && mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            RemoveBlock(hoverBlock);
            HideGhost();
            return;
        }

        if (!hasTarget)
        {
            HideGhost();
            return;
        }

        bool valid = IsCellFree(placeCell) && CapacityAllows(def);
        ShowGhost(def, placeCell, valid);

        if (valid && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            PlaceBlock(def, placeCell, rotationIndex);
            HideGhost();
        }
    }

    // =========================================================
    // GRILLE
    // =========================================================

    private void ComputeGridFromRenderers()
    {
        if (gridRoot == null)
        {
            Debug.LogError("[GarageBuild] gridRoot (buildGrid) non assigne.");
            enabled = false;
            return;
        }

        // La zone de jeu est delimitee par les lignes gx0..gx14 / gz0..gz14.
        // On ignore les bordures decoratives et la centerLine (qui depasse de la grille).
        Bounds bounds = default;
        bool hasBounds = false;
        float lineThickness = 0f;

        foreach (var r in gridRoot.GetComponentsInChildren<Renderer>())
        {
            string n = r.gameObject.name;
            if (!n.StartsWith("gx") && !n.StartsWith("gz"))
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }

            // Epaisseur d'une ligne = sa plus petite dimension horizontale
            float t = Mathf.Min(r.bounds.size.x, r.bounds.size.z);
            if (lineThickness <= 0f || t < lineThickness)
                lineThickness = t;
        }

        // Secours si les lignes ne sont pas trouvees : bounds complets (ancien comportement)
        if (!hasBounds)
        {
            var renderers = gridRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError("[GarageBuild] Aucun renderer sous buildGrid pour mesurer la grille.");
                enabled = false;
                return;
            }
            bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            Debug.LogWarning("[GarageBuild] Lignes gx*/gz* introuvables — mesure approximative sur tout buildGrid.");
        }

        // Les cases vont de centre de ligne a centre de ligne : on retire une
        // demi-epaisseur de chaque cote de l'union.
        float half = lineThickness * 0.5f;
        gridMin = new Vector3(bounds.min.x + half, 0f, bounds.min.z + half);
        topY = bounds.min.y; // base des lignes = surface de la grille (pas de flottement)
        cellX = (bounds.size.x - lineThickness) / RobotBlueprint.GridWidth;
        cellZ = (bounds.size.z - lineThickness) / RobotBlueprint.GridDepth;
        cellY = Mathf.Min(cellX, cellZ); // hauteur d'un etage = taille de cellule

        Debug.Log($"[GarageBuild] Grille mesuree : cellules {cellX:0.###} x {cellZ:0.###} m, " +
                  $"surface y={topY:0.###}, origine ({gridMin.x:0.##}, {gridMin.z:0.##}).");
    }

    // Visualisation des cellules calculees dans la Scene view (objet BuildSystem selectionne)
    private void OnDrawGizmosSelected()
    {
        if (cellX <= 0f || cellZ <= 0f)
            return;

        Gizmos.color = Color.cyan;
        for (int x = 0; x <= RobotBlueprint.GridWidth; x++)
        {
            var a = new Vector3(gridMin.x + x * cellX, topY + 0.01f, gridMin.z);
            var b = new Vector3(gridMin.x + x * cellX, topY + 0.01f, gridMin.z + RobotBlueprint.GridDepth * cellZ);
            Gizmos.DrawLine(a, b);
        }
        for (int z = 0; z <= RobotBlueprint.GridDepth; z++)
        {
            var a = new Vector3(gridMin.x, topY + 0.01f, gridMin.z + z * cellZ);
            var b = new Vector3(gridMin.x + RobotBlueprint.GridWidth * cellX, topY + 0.01f, gridMin.z + z * cellZ);
            Gizmos.DrawLine(a, b);
        }
    }

    private Vector3 CellToWorld(Vector3Int cell)
    {
        return new Vector3(
            gridMin.x + (cell.x + 0.5f) * cellX,
            topY + (cell.y + 0.5f) * cellY,
            gridMin.z + (cell.z + 0.5f) * cellZ);
    }

    private bool IsCellFree(Vector3Int cell)
    {
        return cell.x >= 0 && cell.x < RobotBlueprint.GridWidth
            && cell.z >= 0 && cell.z < RobotBlueprint.GridDepth
            && cell.y >= 0 && cell.y < RobotBlueprint.GridHeight
            && !occupied.ContainsKey(cell);
    }

    private bool CapacityAllows(BlockDefinition def)
    {
        return statsHud == null || totalCpu + def.costCpu <= statsHud.capacityMax;
    }

    // =========================================================
    // VISEE
    // =========================================================

    private bool TryGetTarget(out Vector3Int cell, out PlacedBlockView hoverBlock)
    {
        cell = default;
        hoverBlock = null;

        var ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Bloc pose le plus proche sur le rayon
        int count = Physics.RaycastNonAlloc(ray, hitBuffer, maxReach, ~0, QueryTriggerInteraction.Ignore);
        float blockDist = float.MaxValue;
        PlacedBlockView view = null;
        Vector3 normal = Vector3.up;
        for (int i = 0; i < count; i++)
        {
            var candidate = hitBuffer[i].collider.GetComponentInParent<PlacedBlockView>();
            if (candidate == null || hitBuffer[i].distance >= blockDist)
                continue;
            blockDist = hitBuffer[i].distance;
            view = candidate;
            normal = hitBuffer[i].normal;
        }

        // Plan de la grille
        var plane = new Plane(Vector3.up, new Vector3(0f, topY, 0f));
        bool planeHit = plane.Raycast(ray, out float planeDist) && planeDist <= maxReach;

        if (view != null && (!planeHit || blockDist <= planeDist))
        {
            hoverBlock = view;
            cell = view.cell + NormalToDelta(normal);
            return true;
        }

        if (planeHit)
        {
            Vector3 point = ray.GetPoint(planeDist);
            int cx = Mathf.FloorToInt((point.x - gridMin.x) / cellX);
            int cz = Mathf.FloorToInt((point.z - gridMin.z) / cellZ);
            if (cx < 0 || cx >= RobotBlueprint.GridWidth || cz < 0 || cz >= RobotBlueprint.GridDepth)
                return false;
            cell = new Vector3Int(cx, 0, cz);
            return true;
        }

        return false;
    }

    private static Vector3Int NormalToDelta(Vector3 normal)
    {
        float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
        if (ax >= ay && ax >= az) return new Vector3Int(normal.x > 0 ? 1 : -1, 0, 0);
        if (ay >= az) return new Vector3Int(0, normal.y > 0 ? 1 : -1, 0);
        return new Vector3Int(0, 0, normal.z > 0 ? 1 : -1);
    }

    // =========================================================
    // ROTATION (molette)
    // =========================================================

    private void HandleWheelRotation()
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) < 0.01f)
            return;

        rotationIndex = (rotationIndex + (scrollY > 0 ? 1 : 3)) % 4;
        if (plierPreview != null)
            plierPreview.extraYaw = rotationIndex * 90f;
    }

    // =========================================================
    // GHOST
    // =========================================================

    private void ShowGhost(BlockDefinition def, Vector3Int cell, bool valid)
    {
        if (ghost == null || ghostBlockId != def.name)
        {
            if (ghost != null)
                Destroy(ghost);
            ghost = BlockPreviewFactory.CreateVisual(def, cellY * 0.96f, out _);
            ghost.name = "BuildGhost";
            ghostBlockId = def.name;
        }

        ghost.SetActive(true);
        ghost.transform.SetPositionAndRotation(CellToWorld(cell), Quaternion.Euler(0f, rotationIndex * 90f, 0f));

        var material = valid ? ghostValidMaterial : ghostInvalidMaterial;
        if (material != null)
        {
            // Remplace tous les sous-materiaux (les FBX d'armes en ont plusieurs)
            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }
    }

    private void HideGhost()
    {
        if (ghost != null)
            ghost.SetActive(false);
    }

    // =========================================================
    // POSE / RETRAIT
    // =========================================================

    private void PlaceBlock(BlockDefinition def, Vector3Int cell, int rotation)
    {
        SpawnView(def, cell, rotation);
        RobotSession.Blueprint.blocks.Add(new PlacedBlock
        {
            blockId = def.name,
            x = cell.x,
            y = cell.y,
            z = cell.z,
            rotation = rotation,
        });
        RobotSession.Dirty = true;
        UpdateStats();
    }

    private void RemoveBlock(PlacedBlockView view)
    {
        occupied.Remove(view.cell);
        RobotSession.Blueprint.blocks.RemoveAll(b => b.x == view.cell.x && b.y == view.cell.y && b.z == view.cell.z);
        Destroy(view.gameObject);
        RobotSession.Dirty = true;
        UpdateStats();
    }

    private void SpawnView(BlockDefinition def, Vector3Int cell, int rotation)
    {
        var root = new GameObject($"Block_{cell.x}_{cell.y}_{cell.z}");
        root.transform.SetParent(robotRoot, false);
        root.transform.SetPositionAndRotation(CellToWorld(cell), Quaternion.Euler(0f, rotation * 90f, 0f));

        var collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(cellX * 0.98f, cellY * 0.98f, cellZ * 0.98f);

        var view = root.AddComponent<PlacedBlockView>();
        view.cell = cell;
        view.definition = def;

        var visual = BlockPreviewFactory.CreateVisual(def, cellY * 0.96f, out _);
        visual.transform.SetParent(root.transform, false);

        occupied[cell] = view;
    }

    private void LoadBlueprint()
    {
        // Purge les blocs qui n'existent plus dans l'inventaire (ex: anciennes
        // armes remplacees par les lasers) : la prochaine sauvegarde nettoie
        // le blueprint cote serveur.
        int removed = RobotSession.Blueprint.blocks.RemoveAll(b => !defsById.ContainsKey(b.blockId));
        if (removed > 0)
        {
            Debug.LogWarning($"[GarageBuild] {removed} bloc(s) inconnus retires du blueprint (blocs supprimes du jeu).");
            RobotSession.Dirty = true;
        }

        foreach (var placed in RobotSession.Blueprint.blocks)
        {
            var cell = new Vector3Int(placed.x, placed.y, placed.z);
            if (IsCellFree(cell))
                SpawnView(defsById[placed.blockId], cell, placed.rotation);
        }
    }

    // =========================================================
    // STATS
    // =========================================================

    private void UpdateStats()
    {
        totalCpu = 0;
        int totalHp = 0;
        int totalKg = 0;
        foreach (var pair in occupied)
        {
            totalCpu += pair.Value.definition.costCpu;
            totalHp += pair.Value.definition.resistanceHp;
            totalKg += pair.Value.definition.weightKg;
        }

        if (statsHud != null)
        {
            statsHud.capacity = totalCpu;
            statsHud.health = totalHp;
            statsHud.weightT = totalKg / 1000f;
            statsHud.Refresh();
        }
    }
}
