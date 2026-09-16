using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// SYSTEME DE POSE DE BLOCS SUR LA GRILLE (31x31x31 max)
// - La grille est mesuree sur les lignes du vaisseau (gx*/gz*
//   ou BuildGrid_X/Y_*) : nombre de cases = lignes - 1 par axe
//   (Mothership : 32 lignes = 31 cases de 1 m ; ancien vaisseau :
//   15 lignes = 14 cases), plafonne par RobotBlueprint
// - Ghost transparent sur la case visee (vert = ok, rouge = invalide)
// - Un bloc occupe toutes les cases de sa boite d'empreinte
//   (BlockFootprint) orientee autour de la case visee ; seule la
//   case visee + l'orientation sont sauvegardees (PlacedBlock)
// - Les blocs "orientToFace" (tout sauf le chassis) plaquent leur
//   face d'ancrage contre la face visee du support (dessus,
//   dessous, flancs) ; le chassis reste droit. Ils doivent aussi
//   toucher une piece deja posee : seul le chassis se pose seul
//   sur le sol de la baie (ghost rouge sinon)
// - Une roue ou une patte montee sur le flanc du plancher
//   descend sous lui : le robot entier est souleve (lift) pour
//   qu'elle pose sur le sol au lieu de s'y encastrer
// - Clic gauche : poser / clic droit : retirer
// - Molette : quart de tour autour de la face visee (ou de Y)
// - M : mode miroir, le symetrique du bloc par le plan de la ligne
//   centrale est affiche, pose et retire en meme temps (image
//   miroir vraie : echelle -1 sur l'axe du plan, PlacedBlock.mirrored)
// - Recharge le blueprint existant (purge les blocs qui ne tiennent
//   plus), met a jour stats et Dirty
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
    private int cellsX = RobotBlueprint.GridWidth; // cases mesurees sur les lignes (max : constantes du blueprint)
    private int cellsZ = RobotBlueprint.GridDepth;

    private readonly Dictionary<Vector3Int, PlacedBlockView> occupied = new(); // une entree par case occupee
    private readonly HashSet<PlacedBlockView> placedViews = new();             // une entree par bloc
    private readonly Dictionary<string, BlockDefinition> defsById = new();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private readonly List<Vector3Int> cellBuffer = new();
    private readonly List<Vector3Int> supportBuffer = new();

    // Hauteur dont tout le robot est souleve au-dessus du sol de la baie pour
    // que la piece la plus basse (roue, patte) pose dessus sans s'y encastrer.
    private float lift;

    private static readonly Vector3Int[] Neighbours =
    {
        Vector3Int.up, Vector3Int.down, Vector3Int.right,
        Vector3Int.left, Vector3Int.forward, Vector3Int.back,
    };

    private Transform robotRoot;
    private GameObject ghost;
    private string ghostBlockId;
    private int spinIndex; // quarts de tour a la molette, autour de la face visee (ou de Y)
    private int totalCpu;

    [Header("Miroir (touche M)")]
    [Tooltip("Affiche, pose et retire aussi le symetrique du bloc par rapport a la ligne centrale de la grille")]
    public bool mirrorMode;
    [Tooltip("Puce de la touche M dans la legende (Key_MIROIR), coloree quand le miroir est actif ; retrouvee par son nom si vide")]
    public Image mirrorChip;
    [Tooltip("Libelle MIROIR de la legende (Ctrl_MIROIR) ; retrouve par son nom si vide")]
    public TMP_Text mirrorLabel;
    public Color mirrorOnColor = new(0.23f, 0.51f, 0.96f);

    private bool mirrorAlongX = true; // la ligne centrale court le long de Z : le miroir inverse X
    private GameObject mirrorGhost;
    private string mirrorGhostBlockId;
    private Color mirrorChipOffColor;
    private readonly List<Vector3Int> mirrorBuffer = new();

    private BlockDefinition Selected => inventory != null ? inventory.CurrentBlock : null;

    private void Start()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;

        ComputeGridFromRenderers();
        DetectMirrorAxis();

        // Indicateur de la legende : cable par le setup, sinon retrouve par nom
        if (mirrorLabel == null)
        {
            var mirrorLabelGo = GameObject.Find("Ctrl_MIROIR");
            if (mirrorLabelGo != null)
                mirrorLabel = mirrorLabelGo.GetComponent<TMP_Text>();
        }
        if (mirrorChip == null)
        {
            var mirrorChipGo = GameObject.Find("Key_MIROIR");
            if (mirrorChipGo != null)
                mirrorChip = mirrorChipGo.GetComponent<Image>();
        }
        if (mirrorChip != null)
            mirrorChipOffColor = mirrorChip.color;
        RefreshMirrorIndicator();

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
        if (mirrorGhost != null)
            Destroy(mirrorGhost);
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
        HandleMirrorToggle();

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

        bool valid = CanPlace(def, placeCell, rotation) && CapacityAllows(def)
                     && HasSupport(def, placeCell, rotation, false);
        ShowGhost(def, placeCell, rotation, valid);

        // Mode miroir : le symetrique est pose avec le bloc s'il tient (cases
        // libres, hors du bloc lui-meme, capacite pour les deux) ; un bloc
        // deja symetrique (a cheval sur la ligne) n'a pas de jumeau.
        Vector3Int mirrorCell = MirrorCell(placeCell);
        bool placeMirror = false;
        if (mirrorMode)
        {
            bool symmetric = false;
            placeMirror = valid && MirrorPlaceable(def, placeCell, rotation, out symmetric)
                          && (statsHud == null || totalCpu + 2 * def.costCpu <= statsHud.capacityMax)
                          && HasSupport(def, mirrorCell, rotation, true);
            if (symmetric)
                HideMirrorGhost();
            else
                ShowMirrorGhost(def, mirrorCell, rotation, placeMirror);
        }
        else
        {
            HideMirrorGhost();
        }

        if (valid && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            PlaceBlock(def, placeCell, rotation, false);
            if (placeMirror)
                PlaceBlock(def, mirrorCell, rotation, true);
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

        // La zone de jeu est delimitee par les lignes de la grille (gx*/gz* sur
        // l'ancien vaisseau, BuildGrid_X_*/BuildGrid_Y_* sur le Mothership).
        // On ignore les bordures decoratives et la centerLine (qui depasse).
        Bounds bounds = default;
        bool hasBounds = false;
        float lineThickness = 0f;
        int linesAlongX = 0; // lignes qui courent le long de X (Z constant) : separent les rangees
        int linesAlongZ = 0; // lignes qui courent le long de Z (X constant) : separent les colonnes

        foreach (var r in gridRoot.GetComponentsInChildren<Renderer>())
        {
            if (!IsGridLine(r.gameObject.name))
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
            var size = r.bounds.size;
            float t = Mathf.Min(size.x, size.z);
            if (lineThickness <= 0f || t < lineThickness)
                lineThickness = t;

            if (size.x > size.z * 2f)
                linesAlongX++;
            else if (size.z > size.x * 2f)
                linesAlongZ++;
        }

        // Nombre de cases = lignes - 1 sur chaque axe, borne par le blueprint
        // (contrat 31 x 31 ; l'ancien vaisseau n'offre que 14 x 14)
        if (linesAlongX >= 2 && linesAlongZ >= 2)
        {
            cellsX = Mathf.Min(linesAlongZ - 1, RobotBlueprint.GridWidth);
            cellsZ = Mathf.Min(linesAlongX - 1, RobotBlueprint.GridDepth);
        }
        else
        {
            cellsX = RobotBlueprint.GridWidth;
            cellsZ = RobotBlueprint.GridDepth;
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
        cellX = (bounds.size.x - lineThickness) / cellsX;
        cellZ = (bounds.size.z - lineThickness) / cellsZ;
        cellY = Mathf.Min(cellX, cellZ); // hauteur d'un etage = taille de cellule

        Debug.Log($"[GarageBuild] Grille mesuree : {cellsX} x {cellsZ} cases de {cellX:0.###} x {cellZ:0.###} m " +
                  $"(hauteur {RobotBlueprint.GridHeight}), surface y={topY:0.###}, origine ({gridMin.x:0.##}, {gridMin.z:0.##}).");

        if (cellsX != RobotBlueprint.GridWidth || cellsZ != RobotBlueprint.GridDepth)
        {
            Debug.LogWarning($"[GarageBuild] Ce vaisseau n'offre que {cellsX} x {cellsZ} cases (contrat " +
                             $"{RobotBlueprint.GridWidth} x {RobotBlueprint.GridDepth}) : les blueprints construits ici " +
                             "occuperont le coin d'une grille complete.");
        }

        // Les boites d'empreinte (visuel, collider) utilisent une case uniforme
        // cellY sur les trois axes : une boite tournee de 90 degres doit garder
        // sa taille. Les cellules doivent donc etre carrees.
        if (Mathf.Abs(cellX - cellZ) > 0.01f * cellY)
        {
            Debug.LogWarning($"[GarageBuild] Cellules non carrees ({cellX:0.###} x {cellZ:0.###} m) : les blocs " +
                             $"multi-cases sont poses avec une case de {cellY:0.###} m et se decaleront des lignes.");
        }
    }

    // Lignes de la grille du vaisseau : gx0..gx14 / gz0..gz14 (ShipGarage) ou
    // BuildGrid_X_00..31 / BuildGrid_Y_00..31 (Mothership)
    private static bool IsGridLine(string name)
    {
        return name.StartsWith("gx") || name.StartsWith("gz")
            || name.StartsWith("BuildGrid_", System.StringComparison.OrdinalIgnoreCase);
    }

    // Visualisation des cellules calculees dans la Scene view (objet BuildSystem selectionne)
    private void OnDrawGizmosSelected()
    {
        if (cellX <= 0f || cellZ <= 0f)
            return;

        Gizmos.color = Color.cyan;
        for (int x = 0; x <= cellsX; x++)
        {
            var a = new Vector3(gridMin.x + x * cellX, topY + 0.01f, gridMin.z);
            var b = new Vector3(gridMin.x + x * cellX, topY + 0.01f, gridMin.z + cellsZ * cellZ);
            Gizmos.DrawLine(a, b);
        }
        for (int z = 0; z <= cellsZ; z++)
        {
            var a = new Vector3(gridMin.x, topY + 0.01f, gridMin.z + z * cellZ);
            var b = new Vector3(gridMin.x + cellsX * cellX, topY + 0.01f, gridMin.z + z * cellZ);
            Gizmos.DrawLine(a, b);
        }
    }

    // Centre d'une case dans le repere de la grille, robot non souleve
    private Vector3 CellToWorldRaw(Vector3Int cell)
    {
        return new Vector3(
            gridMin.x + (cell.x + 0.5f) * cellX,
            topY + (cell.y + 0.5f) * cellY,
            gridMin.z + (cell.z + 0.5f) * cellZ);
    }

    // Centre d'une case tel que le robot est reellement pose (avec le relevage)
    private Vector3 CellToWorld(Vector3Int cell)
    {
        var world = CellToWorldRaw(cell);
        world.y += lift;
        return world;
    }

    private Vector3Int WorldToCell(Vector3 point)
    {
        return new Vector3Int(
            Mathf.FloorToInt((point.x - gridMin.x) / cellX),
            Mathf.FloorToInt((point.y - topY - lift) / cellY),
            Mathf.FloorToInt((point.z - gridMin.z) / cellZ));
    }

    // Les cases sous le sol de la baie (y < 0) sont permises : c'est la place
    // des roues et des pattes montees sur le flanc du plancher, et le robot est
    // souleve d'autant (RefreshLift) pour qu'elles posent au sol.
    private bool IsCellFree(Vector3Int cell)
    {
        return cell.x >= 0 && cell.x < cellsX
            && cell.z >= 0 && cell.z < cellsZ
            && cell.y >= -RobotBlueprint.GridUnderfloor && cell.y < RobotBlueprint.GridHeight
            && !occupied.ContainsKey(cell);
    }

    // =========================================================
    // RELEVAGE DU ROBOT
    // =========================================================

    // Souleve tout le robot pour que le point le plus bas de ses blocs affleure
    // le sol de la baie : une roue ou une patte fixee sur le flanc du plancher
    // descend sous lui et doit poser dessus, pas le traverser. Sans piece
    // debordante, le relevage est nul et le chassis reste sur le sol.
    private void RefreshLift()
    {
        float lowest = topY;
        foreach (var view in placedViews)
        {
            if (view == null || view.definition == null)
                continue;

            var box = BlockFootprint.LocalBox(view.definition);
            Vector3 min = box.min * cellY, max = box.max * cellY;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x,
                                         (i & 2) == 0 ? min.y : max.y,
                                         (i & 4) == 0 ? min.z : max.z);
                // La pose courante contient deja le relevage : on le retire
                // pour raisonner dans le repere de la grille.
                lowest = Mathf.Min(lowest, view.transform.TransformPoint(corner).y - lift);
            }
        }

        float target = Mathf.Max(0f, topY - lowest);
        if (Mathf.Abs(target - lift) < 1e-4f)
            return;

        float previous = lift;
        lift = target;
        foreach (var view in placedViews)
        {
            if (view != null)
                view.transform.position = CellToWorld(view.cell);
        }
        Debug.Log($"[GarageBuild] Robot souleve de {lift:0.###} m ({lift / cellY:0.##} case) pour que la piece la plus " +
                  $"basse pose sur le sol (avant : {previous:0.###} m).");
    }

    // =========================================================
    // ATTACHE
    // =========================================================

    // Un bloc qui s'accroche a une face (tout sauf le chassis) doit toucher une
    // piece deja posee : une roue, un propulseur ou une arme ne tient pas en
    // l'air au milieu de la baie. Le chassis, lui, se pose librement sur le sol :
    // c'est par lui que le robot commence.
    private bool HasSupport(BlockDefinition def, Vector3Int anchorCell, int rotation, bool mirrored)
    {
        if (!def.orientToFace)
            return true;

        GetBlockCells(def, anchorCell, rotation, mirrored, supportBuffer);
        foreach (var cell in supportBuffer)
        {
            foreach (var direction in Neighbours)
            {
                if (occupied.ContainsKey(cell + direction))
                    return true;
            }
        }
        return false;
    }

    // Toutes les cases de la boite d'empreinte (orientee, eventuellement en
    // miroir) sont dans la grille et libres
    private bool CanPlace(BlockDefinition def, Vector3Int anchorCell, int rotation, bool mirrored = false)
    {
        GetBlockCells(def, anchorCell, rotation, mirrored, cellBuffer);
        foreach (var c in cellBuffer)
        {
            if (!IsCellFree(c))
                return false;
        }
        return true;
    }

    // Cases d'un bloc ; en miroir, ce sont les cases du bloc d'origine (ancre
    // sur la case en face) reflechies par le plan central.
    private void GetBlockCells(BlockDefinition def, Vector3Int anchorCell, int rotation, bool mirrored, List<Vector3Int> result)
    {
        if (!mirrored)
        {
            BlockFootprint.GetCells(def, anchorCell, rotation, result);
            return;
        }
        BlockFootprint.GetCells(def, MirrorCell(anchorCell), rotation, result);
        for (int i = 0; i < result.Count; i++)
            result[i] = MirrorCell(result[i]);
    }

    // =========================================================
    // MIROIR (plan de la ligne centrale de la grille)
    // =========================================================

    // La ligne centrale (rouge) donne l'axe du miroir : longue sur un axe
    // horizontal, fine sur l'autre ; le plan du miroir est perpendiculaire a
    // l'axe fin. Par defaut : ligne le long de Z, miroir sur X.
    private void DetectMirrorAxis()
    {
        if (gridRoot == null)
            return;

        foreach (var r in gridRoot.GetComponentsInChildren<Renderer>())
        {
            if (r.gameObject.name.IndexOf("center", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            mirrorAlongX = r.bounds.size.x <= r.bounds.size.z;
            RobotSession.MirrorAlongX = mirrorAlongX;
            Debug.Log($"[GarageBuild] Ligne centrale '{r.gameObject.name}' : miroir sur l'axe {(mirrorAlongX ? "X" : "Z")}.");
            return;
        }
        RobotSession.MirrorAlongX = mirrorAlongX;
        Debug.LogWarning("[GarageBuild] Ligne centrale introuvable sous la grille : miroir sur l'axe X par defaut.");
    }

    // Case symetrique par le plan central : entre deux colonnes si le nombre
    // de cases est pair (14 : colonnes 6 et 7), au milieu de la colonne
    // centrale s'il est impair (31 : colonne 15, qui est sa propre image)
    private Vector3Int MirrorCell(Vector3Int c)
    {
        return mirrorAlongX
            ? new Vector3Int(cellsX - 1 - c.x, c.y, c.z)
            : new Vector3Int(c.x, c.y, cellsZ - 1 - c.z);
    }

    private Vector3 MirrorScale => BlockFootprint.MirrorScale(mirrorAlongX);

    private Quaternion MirrorRotation(Quaternion q) => BlockFootprint.MirrorRotation(q, mirrorAlongX);

    // Le symetrique du bloc vise tient-il ? symmetric = le bloc est son propre
    // symetrique (a cheval sur la ligne), auquel cas il n'y a rien a ajouter.
    private bool MirrorPlaceable(BlockDefinition def, Vector3Int anchorCell, int rotation, out bool symmetric)
    {
        BlockFootprint.GetCells(def, anchorCell, rotation, cellBuffer);
        GetBlockCells(def, MirrorCell(anchorCell), rotation, true, mirrorBuffer);

        symmetric = true;
        bool ok = true;
        foreach (var c in mirrorBuffer)
        {
            bool overlapsSelf = cellBuffer.Contains(c);
            if (!overlapsSelf)
                symmetric = false;
            if (overlapsSelf || !IsCellFree(c))
                ok = false;
        }
        return ok;
    }

    private Keyboard mirrorKeyboard;
    private UnityEngine.InputSystem.Controls.KeyControl mirrorKey;

    private void HandleMirrorToggle()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        // Keyboard.mKey designe la position physique QWERTY (le point-virgule
        // en AZERTY) : on cherche la touche par son nom sur la disposition
        // courante, avec repli sur la position physique.
        if (mirrorKeyboard != kb || mirrorKey == null)
        {
            mirrorKeyboard = kb;
            mirrorKey = kb.mKey;
            foreach (var key in kb.allKeys)
            {
                if (string.Equals(key.displayName, "m", System.StringComparison.OrdinalIgnoreCase))
                {
                    mirrorKey = key;
                    break;
                }
            }
        }

        bool pressed = mirrorKey.wasPressedThisFrame || (mirrorKey != kb.mKey && kb.mKey.wasPressedThisFrame);
        if (!pressed)
            return;

        mirrorMode = !mirrorMode;
        RefreshMirrorIndicator();
        if (!mirrorMode)
            HideMirrorGhost();
        Debug.Log($"[GarageBuild] Mode miroir {(mirrorMode ? "active" : "desactive")} (axe {(mirrorAlongX ? "X" : "Z")}).");
    }

    // Legende : puce de la touche coloree et libelle "MIROIR  [ON]" quand actif
    private void RefreshMirrorIndicator()
    {
        if (mirrorLabel != null)
            mirrorLabel.text = mirrorMode ? "MIROIR  [ON]" : "MIROIR";
        if (mirrorChip != null)
            mirrorChip.color = mirrorMode ? mirrorOnColor : mirrorChipOffColor;
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
            return cell.x >= 0 && cell.x < cellsX
                && cell.z >= 0 && cell.z < cellsZ;
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
        ApplyGhostMaterial(ghost, valid);
    }

    private void ShowMirrorGhost(BlockDefinition def, Vector3Int cell, int rotation, bool valid)
    {
        if (mirrorGhost == null || mirrorGhostBlockId != def.name)
        {
            if (mirrorGhost != null)
                Destroy(mirrorGhost);
            mirrorGhost = BlockPreviewFactory.CreateVisual(def, cellY, out _);
            mirrorGhost.name = "BuildGhostMirror";
            mirrorGhostBlockId = def.name;
        }

        mirrorGhost.SetActive(true);
        mirrorGhost.transform.SetPositionAndRotation(CellToWorld(cell), MirrorRotation(BlockFootprint.Rotation(def, rotation)));
        mirrorGhost.transform.localScale = MirrorScale;
        ApplyGhostMaterial(mirrorGhost, valid);
    }

    private void ApplyGhostMaterial(GameObject target, bool valid)
    {
        var material = valid ? ghostValidMaterial : ghostInvalidMaterial;
        if (material == null)
            return;

        // Remplace tous les sous-materiaux (les FBX d'armes en ont plusieurs)
        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private void HideGhost()
    {
        if (ghost != null)
            ghost.SetActive(false);
        HideMirrorGhost();
    }

    private void HideMirrorGhost()
    {
        if (mirrorGhost != null)
            mirrorGhost.SetActive(false);
    }

    // =========================================================
    // POSE / RETRAIT
    // =========================================================

    private void PlaceBlock(BlockDefinition def, Vector3Int cell, int rotation, bool mirrored)
    {
        SpawnView(def, cell, rotation, mirrored);
        RobotSession.Blueprint.blocks.Add(new PlacedBlock
        {
            blockId = def.name,
            x = cell.x,
            y = cell.y,
            z = cell.z,
            rotation = rotation,
            mirrored = mirrored,
        });
        RobotSession.Dirty = true;
        RefreshLift();
        UpdateStats();
    }

    private void RemoveBlock(PlacedBlockView view)
    {
        // Mode miroir : le jumeau (meme bloc, ancre sur la case en face) part aussi
        if (mirrorMode && occupied.TryGetValue(MirrorCell(view.cell), out var twin)
            && twin != view && twin.definition == view.definition)
        {
            RemoveSingle(twin);
        }
        RemoveSingle(view);
        RobotSession.Dirty = true;
        RefreshLift();
        UpdateStats();
    }

    private void RemoveSingle(PlacedBlockView view)
    {
        foreach (var c in view.cells)
            occupied.Remove(c);
        placedViews.Remove(view);
        RobotSession.Blueprint.blocks.RemoveAll(b => b.x == view.cell.x && b.y == view.cell.y && b.z == view.cell.z);
        Destroy(view.gameObject);
    }

    private void SpawnView(BlockDefinition def, Vector3Int cell, int rotation, bool mirrored)
    {
        var root = new GameObject($"Block_{cell.x}_{cell.y}_{cell.z}");
        root.transform.SetParent(robotRoot, false);

        // Image miroir : racine reflechie (echelle -1 sur l'axe du plan, rotation
        // conjuguee) ; Unity retourne les faces des meshes a echelle negative.
        var rot = BlockFootprint.Rotation(def, rotation);
        root.transform.SetPositionAndRotation(CellToWorld(cell), mirrored ? MirrorRotation(rot) : rot);
        if (mirrored)
            root.transform.localScale = MirrorScale;

        // Collider a la taille de la boite d'empreinte, dans le repere local de la
        // racine (il tourne avec elle) : la case visee est a l'origine.
        var box = BlockFootprint.LocalBox(def);
        var collider = root.AddComponent<BoxCollider>();
        collider.center = box.center * cellY;
        collider.size = box.size * (cellY * 0.98f);

        var view = root.AddComponent<PlacedBlockView>();
        view.cell = cell;
        view.definition = def;
        view.mirrored = mirrored;
        GetBlockCells(def, cell, rotation, mirrored, cellBuffer);
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
            if (CanPlace(def, cell, placed.rotation, placed.mirrored))
                SpawnView(def, cell, placed.rotation, placed.mirrored);
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

        RefreshLift();
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
