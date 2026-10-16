using System.Collections.Generic;
using UnityEngine;

// =========================================================
// APPARITION DU ROBOT DANS LA SCENE DE TEST
// Au Start (synchrone, sans animation) : assemble le blueprint
// en memoire (RobotSession, rempli par le garage avant la
// touche P) ou, si la scene est lancee directement, un robot
// par defaut (12 cubes + 4 roues Scout) ; le pose sur le
// SpawnPoint (un peu au-dessus : les suspensions se posent),
// lui ajoute la physique (RobotLocomotion), l'animation des
// blocs de mouvement (RobotPartAnimator), le pilotage
// (RobotTestDriver), le HUD de match (RobotTestHUD : vie,
// reticule, etat du robot, minimap) et branche la camera.
// Le robot passe sur le calque RobotLayer : la minimap le
// remplace par son marqueur au lieu de le photographier.
// F5 : passe au robot suivant (le robot du garage, puis des
// robots de demonstration : chenilles, pattes, survol,
// helico avec propulseur et gouverne).
// =========================================================

public class RobotTestSpawner : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Transform spawnPoint;
    public RobotFollowCamera followCamera;
    [Tooltip("Catalogue des blocs (Data/Blocks) : le blueprint reference les blocs par nom d'asset")]
    public BlockDefinition[] blocks;

    [Header("Reglages")]
    [Tooltip("Taille monde d'une case, independante du garage (qui mesure la sienne sur les lignes du vaisseau, " +
             "voir le log [GarageBuild] Grille mesuree). 1 m = l'echelle des blocs Blender et du Mothership.")]
    public float cellSize = 1f;

    public const int RobotLayer = 29;

    public GameObject Robot { get; private set; }

    private readonly Dictionary<string, BlockDefinition> defs = new();
    private GameObject hudHost;
    private int demoIndex;   // 0 = robot du garage (ou robot par defaut), 1.. = robots de demonstration

    private void Start()
    {
        if (blocks != null)
        {
            foreach (var def in blocks)
            {
                if (def != null)
                    defs[def.name] = def;
            }
        }

        var blueprint = RobotSession.Blueprint;
        bool isDefault = blueprint == null || blueprint.blocks == null || blueprint.blocks.Count == 0;
        Spawn(isDefault ? CreateDefaultTestBlueprint() : blueprint, isDefault);
    }

    // F5 : robot suivant (robot du garage, puis les demonstrations de chaque famille de mouvement)
    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.f5Key.wasPressedThisFrame)
            return;
        var demos = DemoBlueprints();
        demoIndex = (demoIndex + 1) % (demos.Count + 1);
        if (demoIndex == 0)
        {
            var bp = RobotSession.Blueprint;
            bool isDefault = bp == null || bp.blocks == null || bp.blocks.Count == 0;
            Spawn(isDefault ? CreateDefaultTestBlueprint() : bp, isDefault);
        }
        else
        {
            Spawn(demos[demoIndex - 1], false);
        }
    }

    private void Spawn(RobotBlueprint blueprint, bool isDefault)
    {
        if (Robot != null)
            Destroy(Robot);
        if (hudHost != null)
            Destroy(hudHost);

        Robot = RobotAssembler.Build(blueprint, defs, cellSize, null, out int placed, out int weightKg);

        Vector3 position = (spawnPoint != null ? spawnPoint.position : Vector3.zero) + Vector3.up * 0.4f;
        Quaternion rotation = spawnPoint != null ? Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f) : Quaternion.identity;
        Robot.transform.SetPositionAndRotation(position, rotation);
        CheckGround(position);

        SetLayer(Robot.transform, RobotLayer);

        // Physique (masse = poids / 100), animations, pilotage
        Robot.AddComponent<Rigidbody>();
        var locomotion = Robot.AddComponent<RobotLocomotion>();
        locomotion.Setup(cellSize, weightKg);
        Robot.AddComponent<RobotPartAnimator>().Build(locomotion);
        Robot.AddComponent<RobotTestDriver>();

        // HUD de match (theme hud du design system), recree avec le robot
        int maxHealth = 0;
        foreach (var view in Robot.GetComponentsInChildren<PlacedBlockView>())
            maxHealth += view.definition != null ? view.definition.resistanceHp : 0;
        hudHost = new GameObject("MatchHUD_" + blueprint.name);
        hudHost.AddComponent<RobotTestHUD>().Bind(locomotion, blueprint.name, maxHealth, weightKg);

        if (followCamera == null)
            followCamera = FindAnyObjectByType<RobotFollowCamera>();
        if (followCamera != null)
            followCamera.SetTarget(Robot.transform);

        Debug.Log($"[RobotTest] Robot '{blueprint.name}' assemble : {placed} bloc(s), {weightKg} kg, case {cellSize} m" +
                  (isDefault ? " (robot par defaut : aucun blueprint en memoire)" : "") +
                  $" ; F5 : robot suivant ; ECHAP x2 -> '{RobotSession.ReturnSceneName}'.");
    }

    // ---------- Robots de demonstration (une famille de mouvement chacun) ----------

    private static PlacedBlock B(string id, int x, int y, int z, int face = 0, int spin = 0) =>
        new() { blockId = id, x = x, y = y, z = z, rotation = face == 0 && spin == 0 ? 0 : BlockFootprint.Compose(face, spin) };

    private static void Slab(RobotBlueprint bp, int x0, int x1, int y, int z0, int z1)
    {
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                bp.blocks.Add(B("Block_01_Cube", x, y, z));
    }

    public static List<RobotBlueprint> DemoBlueprints()
    {
        var list = new List<RobotBlueprint>();

        var tracks = new RobotBlueprint { name = "DEMO TRACKS" };
        Slab(tracks, 13, 17, 1, 13, 16);
        tracks.blocks.Add(B("Block_TankTrack_N1_Bison", 13, 0, 14, 1));
        tracks.blocks.Add(B("Block_TankTrack_N1_Bison", 17, 0, 14, 1));
        tracks.blocks.Add(B("Block_TankTrack_N1_Bison", 13, 0, 16, 1));
        tracks.blocks.Add(B("Block_TankTrack_N1_Bison", 17, 0, 16, 1));
        list.Add(tracks);

        var legs = new RobotBlueprint { name = "DEMO LEGS" };
        Slab(legs, 14, 16, 2, 14, 16);
        foreach (int z in new[] { 14, 16 })
        {
            legs.blocks.Add(B("Block_InsectLeg_N1_Walker", 13, 1, z, 4));
            legs.blocks.Add(B("Block_InsectLeg_N1_Walker", 17, 1, z, 3));
        }
        list.Add(legs);

        var hover = new RobotBlueprint { name = "DEMO HOVER" };
        Slab(hover, 14, 17, 1, 14, 17);
        // Lames plaquees sur les flancs par leur charniere : le disque reste a plat
        foreach (int z in new[] { 14, 16 })
        {
            hover.blocks.Add(B("Block_HoverBlade_N1_Squall", 13, 1, z, 4));
            hover.blocks.Add(B("Block_HoverBlade_N1_Squall", 18, 1, z, 3));
        }
        list.Add(hover);

        var heli = new RobotBlueprint { name = "DEMO HELI" };
        Slab(heli, 14, 16, 0, 13, 16);
        heli.blocks.Add(B("Block_RotorBlade_Recon", 15, 1, 15, 1));
        heli.blocks.Add(B("Block_Rudder_N1_Hawk", 15, 1, 13, 1));
        heli.blocks.Add(B("Block_Thruster_N1_Lynx", 15, 0, 12, 6));
        list.Add(heli);

        return list;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayer(child, layer);
    }

    // Le spawn a-t-il un sol sous lui ? Une map sans collider (marquage pris pour
    // une surface, decor exclu par erreur) laisserait le robot tomber sans fin.
    private static void CheckGround(Vector3 position)
    {
        var origin = position + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            float drop = position.y - hit.point.y;
            string surface = hit.collider != null ? hit.collider.gameObject.name : "?";
            if (drop < -0.5f)
                Debug.LogWarning($"[RobotTest] Le point d'apparition est {-drop:0.##} m SOUS le sol ('{surface}') : le robot peut rester coince.");
            else
                Debug.Log($"[RobotTest] Sol sous le point d'apparition : '{surface}' a {drop:0.##} m.");
            return;
        }
        Debug.LogWarning("[RobotTest] Aucun sol (collider) sous le point d'apparition : le robot va tomber. " +
                         "Relance Blockforge > Setup Map Test Scene, ou verifie le filtre des maillages sans collision.");
    }

    // Robot par defaut (scene lancee sans robot du garage), au centre de la
    // grille 31 x 31 : chassis 2 x 5 cubes, cabine de 2 cubes a l'arriere et
    // quatre roues Scout (1x2x2, moyeu cote -X) plaquees sur les flancs, face 3
    // (+X) a droite et face 4 (-X) a gauche, comme les poserait le garage.
    // La roue est centree sur sa case en Y et en Z quelle que soit la face
    // (BlockFootprint.LocalBox) : gauche et droite prennent donc les MEMES z.
    // (L'ancienne version decalait les roues gauches d'une case vers l'avant :
    // robot de travers.) Symetrique : centre XZ des roues = centre du chassis.
    public static RobotBlueprint CreateDefaultTestBlueprint()
    {
        var bp = new RobotBlueprint { name = "ROBOT TEST" };
        for (int x = 15; x <= 16; x++)
        {
            for (int z = 13; z <= 17; z++)
                bp.blocks.Add(new PlacedBlock { blockId = "Block_01_Cube", x = x, y = 0, z = z });
            bp.blocks.Add(new PlacedBlock { blockId = "Block_01_Cube", x = x, y = 1, z = 13 });   // cabine (arriere)
        }

        int right = BlockFootprint.Compose(3, 0);
        int left = BlockFootprint.Compose(4, 0);
        foreach (int z in new[] { 13, 17 })
        {
            bp.blocks.Add(new PlacedBlock { blockId = "Block_Wheel_N1_Scout", x = 17, y = 0, z = z, rotation = right });
            bp.blocks.Add(new PlacedBlock { blockId = "Block_Wheel_N1_Scout", x = 14, y = 0, z = z, rotation = left });
        }
        return bp;
    }
}
