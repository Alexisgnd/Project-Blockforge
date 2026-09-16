using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// SYSTEME DE POSE DE BLOCS SUR LA GRILLE (14x14, hauteur 50)
// - Ghost transparent sur la case visee (vert = ok, rouge = invalide)
// - Un bloc occupe toutes les cases de sa boite d'empreinte
//   (BlockFootprint) orientee autour de la case visee ; seule la
//   case visee + l'orientation sont sauvegardees (PlacedBlock)
// - Les blocs "orientToFace" (tout sauf le chassis) plaquent leur
//   face d'ancrage contre la face visee du support (dessus,
//   dessous, flancs) ; le chassis reste droit
// - Clic gauche : poser / clic droit : retirer
// - Molette : quart de tour autour de la face visee (ou de Y)
// - Recharge le blueprint existant (purge les blocs qui ne tiennent
//   plus), met a jour stats et Dirty
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

    private readonly Dictionary<Vector3Int, PlacedBlockView> occupied = new(); // une entree par case occupee
    private readonly HashSet<PlacedBlockView> placedViews = new();             // une entree par bloc
    private readonly Dictionary<string, BlockDefinition> defsById = new();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private readonly List<Vector3Int> cellBuffer = new();

    private Transform robotRoot;
    private GameObject ghost;
    private string ghostBlockId;
    private int spinIndex; // quarts de tour a la molette, autour de la face visee (ou de Y)
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

    // Coupe par GarageToolSwitcher quand le spray est en main :
    // le ghost ne doit pas rester affiche sur la grille.
    private void OnDisable()
    {
        HideGhost();
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

        bool hasTarget = TryGetTarget(out Vector3Int placeCell, out PlacedBlockView hoverBlock, out Vector3Int faceNormal);
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

        // Orientation : les blocs qui s'accrochent a la face visee (armes,
        // mouvement, defense...) y plaquent leur face d'ancrage ; le chassis
        // garde son orientation naturelle. La molette tourne autour de la face.
        int face = def.orientToFace ? BlockFootprint.FaceIndex(faceNormal) : BlockFootprint.NaturalFace;
        int rotation = BlockFootprint.Compose(face, spinIndex);

        bool valid = CanPlace(def, placeCell, rotation) && CapacityAllows(def);
        ShowGhost(def, placeCell, rotation, valid);

        if (valid && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            PlaceBlock(def, placeCell, rotation);
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

        // Les boites d'empreinte (visuel, collider) utilisent une case uniforme
        // cellY sur les trois axes : une boite tournee de 90 degres doit garder
        // sa taille. Les cellules doivent donc etre carrees.
        if (Mathf.Abs(cellX - cellZ) > 0.01f * cellY)
        {
            Debug.LogWarning($"[GarageBuild] Cellules non carrees ({cellX:0.###} x {cellZ:0.###} m) : les blocs " +
                             $"multi-cases sont poses avec une case de {cellY:0.###} m et se decaleront des lignes.");
        }
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

    private Vector3Int WorldToCell(Vector3 point)
    {
        return new Vector3Int(
            Mathf.FloorToInt((point.x - gridMin.x) / cellX),
            Mathf.FloorToInt((point.y - topY) / cellY),
            Mathf.FloorToInt((point.z - gridMin.z) / cellZ));
    }

    private bool IsCellFree(Vector3Int cell)
    {
        return cell.x >= 0 && cell.x < RobotBlueprint.GridWidth
            && cell.z >= 0 && cell.z < RobotBlueprint.GridDepth
            && cell.y >= 0 && cell.y < RobotBlueprint.GridHeight
            && !occupied.ContainsKey(cell);
    }

    // Toutes les cases de la boite d'empreinte (tournee) sont dans la grille et libres
    private bool CanPlace(BlockDefinition def, Vector3Int anchorCell, int rotation)
    {
        BlockFootprint.GetCells(def, anchorCell, rotation, cellBuffer);
        foreach (var c in cellBuffer)
        {
            if (!IsCellFree(c))
                return false;
        }
        return true;
    }

    private bool CapacityAllows(BlockDefinition def)
    {
        return statsHud == null || totalCpu + def.costCpu <= statsHud.capacityMax;
    }

    // =========================================================
    // VISEE
    // =========================================================

    // cell = case libre visee, faceNormal = normale (axe entier) de la face du
    // support qui la designe (+Y pour le sol de la grille)
    private bool TryGetTarget(out Vector3Int cell, out PlacedBlockView hoverBlock, out Vector3Int faceNormal)
    {
        cell = default;
        hoverBlock = null;
        faceNormal = Vector3Int.up;

        var ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Bloc pose le plus proche sur le rayon
        int count = Physics.RaycastNonAlloc(ray, hitBuffer, maxReach, ~0, QueryTriggerInteraction.Ignore);
        float blockDist = float.MaxValue;
        PlacedBlockView view = null;
        Vector3 normal = Vector3.up;
        Vector3 hitPoint = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            var candidate = hitBuffer[i].collider.GetComponentInParent<PlacedBlockView>();
            if (candidate == null || hitBuffer[i].distance >= blockDist)
                continue;
            blockDist = hitBuffer[i].distance;
            view = candidate;
            normal = hitBuffer[i].normal;
            hitPoint = hitBuffer[i].point;
        }

        // Plan de la grille
        var plane = new Plane(Vector3.up, new Vector3(0f, topY, 0f));
        bool planeHit = plane.Raycast(ray, out float planeDist) && planeDist <= maxReach;

        if (view != null && (!planeHit || blockDist <= planeDist))
        {
            // Un bloc multi-cases n'a qu'une case d'ancrage : la case touchee
            // est celle de ses cases dont le centre est le plus proche du point
            // d'impact (un point d'une face n'est jamais equidistant de deux
            // cases du meme bloc, sauf sur une arete).
            hoverBlock = view;
            faceNormal = NormalToDelta(normal);
            cell = NearestCell(view, hitPoint) + faceNormal;
            return true;
        }

        if (planeHit)
        {
            cell = WorldToCell(ray.GetPoint(planeDist));
            cell.y = 0;
            return cell.x >= 0 && cell.x < RobotBlueprint.GridWidth
                && cell.z >= 0 && cell.z < RobotBlueprint.GridDepth;
        }

        return false;
    }

    private Vector3Int NearestCell(PlacedBlockView view, Vector3 point)
    {
        Vector3Int best = view.cell;
        float bestDist = float.MaxValue;
        foreach (var c in view.cells)
        {
            float d = (CellToWorld(c) - point).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }
        return best;
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

        spinIndex = (spinIndex + (scrollY > 0 ? 1 : 3)) % 4;
        if (plierPreview != null)
            plierPreview.extraYaw = spinIndex * 90f;
    }

    // =========================================================
    // GHOST
    // =========================================================

    private void ShowGhost(BlockDefinition def, Vector3Int cell, int rotation, bool valid)
    {
        if (ghost == null || ghostBlockId != def.name)
        {
            if (ghost != null)
                Destroy(ghost);
            ghost = BlockPreviewFactory.CreateVisual(def, cellY, out _);
            ghost.name = "BuildGhost";
            ghostBlockId = def.name;
        }

        ghost.SetActive(true);
        ghost.transform.SetPositionAndRotation(CellToWorld(cell), BlockFootprint.Rotation(def, rotation));

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
        foreach (var c in view.cells)
            occupied.Remove(c);
        placedViews.Remove(view);
        RobotSession.Blueprint.blocks.RemoveAll(b => b.x == view.cell.x && b.y == view.cell.y && b.z == view.cell.z);
        Destroy(view.gameObject);
        RobotSession.Dirty = true;
        UpdateStats();
    }

    private void SpawnView(BlockDefinition def, Vector3Int cell, int rotation)
    {
        var root = new GameObject($"Block_{cell.x}_{cell.y}_{cell.z}");
        root.transform.SetParent(robotRoot, false);
        root.transform.SetPositionAndRotation(CellToWorld(cell), BlockFootprint.Rotation(def, rotation));

        // Collider a la taille de la boite d'empreinte, dans le repere local de la
        // racine (il tourne avec elle) : la case visee est a l'origine.
        var box = BlockFootprint.LocalBox(def);
        var collider = root.AddComponent<BoxCollider>();
        collider.center = box.center * cellY;
        collider.size = box.size * (cellY * 0.98f);

        var view = root.AddComponent<PlacedBlockView>();
        view.cell = cell;
        view.definition = def;
        BlockFootprint.GetCells(def, cell, rotation, cellBuffer);
        view.cells = cellBuffer.ToArray();

        var visual = BlockPreviewFactory.CreateVisual(def, cellY, out _);
        visual.transform.SetParent(root.transform, false);

        foreach (var c in view.cells)
            occupied[c] = view;
        placedViews.Add(view);
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

        // Pose dans l'ordre de sauvegarde ; un bloc qui ne tient plus (hors
        // grille ou chevauchement, ex. empreintes agrandies depuis la sauvegarde)
        // est purge de la meme facon.
        var purged = new List<PlacedBlock>();
        foreach (var placed in RobotSession.Blueprint.blocks)
        {
            var cell = new Vector3Int(placed.x, placed.y, placed.z);
            var def = defsById[placed.blockId];
            if (CanPlace(def, cell, placed.rotation))
                SpawnView(def, cell, placed.rotation);
            else
                purged.Add(placed);
        }

        if (purged.Count > 0)
        {
            RobotSession.Blueprint.blocks.RemoveAll(purged.Contains);
            var names = new List<string>();
            foreach (var p in purged)
                names.Add($"{p.blockId}@({p.x},{p.y},{p.z})");
            Debug.LogWarning($"[GarageBuild] {purged.Count} bloc(s) retires du blueprint (empreinte qui chevauche " +
                             $"un autre bloc ou sort de la grille) ; la prochaine sauvegarde les perd : {string.Join(", ", names)}");
            RobotSession.Dirty = true;
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
        foreach (var view in placedViews)
        {
            totalCpu += view.definition.costCpu;
            totalHp += view.definition.resistanceHp;
            totalKg += view.definition.weightKg;
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
