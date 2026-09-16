using System.Collections.Generic;
using UnityEngine;

// =========================================================
// APPARITION DU ROBOT DANS LA SCENE DE TEST
// Au Start (synchrone, sans animation) : assemble le blueprint
// en memoire (RobotSession, rempli par le garage avant la
// touche P) ou, si la scene est lancee directement, un robot
// par defaut (6 cubes + 4 roues Scout) ; le pose sur le
// SpawnPoint, lui ajoute un Rigidbody et la conduite ZQSD
// (RobotTestDriver) et branche la camera de poursuite.
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

    public GameObject Robot { get; private set; }

    private void Start()
    {
        var defs = new Dictionary<string, BlockDefinition>();
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
        if (isDefault)
            blueprint = CreateDefaultTestBlueprint();

        Robot = RobotAssembler.Build(blueprint, defs, cellSize, null, out int placed, out int weightKg);

        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.up * 0.2f;
        Quaternion rotation = spawnPoint != null ? Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f) : Quaternion.identity;
        Robot.transform.SetPositionAndRotation(position, rotation);

        // Rigidbody ajoute avant le pilote : [RequireComponent] en creerait un
        // par defaut sinon. La masse n'influe pas sur la conduite (pilotee en
        // vitesse), seulement sur les chocs.
        var rb = Robot.AddComponent<Rigidbody>();
        rb.mass = Mathf.Clamp(weightKg * 0.01f, 1f, 100f);
        Robot.AddComponent<RobotTestDriver>();

        if (followCamera == null)
            followCamera = FindAnyObjectByType<RobotFollowCamera>();
        if (followCamera != null)
            followCamera.SetTarget(Robot.transform);

        Debug.Log($"[RobotTest] Robot '{blueprint.name}' assemble : {placed} bloc(s), {weightKg} kg, case {cellSize} m" +
                  (isDefault ? " (robot par defaut : aucun blueprint en memoire)" : "") +
                  $" ; ECHAP x2 -> '{RobotSession.ReturnSceneName}'.");
    }

    // Robot de demonstration au centre de la grille 31 x 31 : 2 x 3 cubes et
    // quatre roues Scout (1x2x2, moyeu cote -X) plaquees sur les flancs, face 3
    // (+X) a droite et face 4 (-X) a gauche, comme les poserait le garage.
    public static RobotBlueprint CreateDefaultTestBlueprint()
    {
        var bp = new RobotBlueprint { name = "ROBOT TEST" };
        for (int x = 15; x <= 16; x++)
        {
            for (int z = 14; z <= 16; z++)
                bp.blocks.Add(new PlacedBlock { blockId = "Block_01_Cube", x = x, y = 0, z = z });
        }

        int right = BlockFootprint.Compose(3, 0);
        int left = BlockFootprint.Compose(4, 0);
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Wheel_N1_Scout", x = 17, y = 0, z = 13, rotation = right });
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Wheel_N1_Scout", x = 17, y = 0, z = 16, rotation = right });
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Wheel_N1_Scout", x = 14, y = 0, z = 14, rotation = left });
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Wheel_N1_Scout", x = 14, y = 0, z = 17, rotation = left });
        return bp;
    }
}
