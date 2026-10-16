using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// OUTIL DE TEST : REGLE DES 3 ARMES / MODULES (garage)
// Menu Blockforge > Debug > Garage : robot test 3 armes/modules
// En Play mode, dans le garage (de preference sur une baie
// libre) : remplace le robot en cours par un mur de cubes
// portant deja 3 types (Laser Wasp, Plasma Pulser, Radar) et
// recharge la scene. Poser ensuite une 4e arme ou un module
// (Rail, Tesla, disque de bouclier...) ouvre le dialogue de
// remplacement. Rien n'est sauvegarde sans la touche T.
// =========================================================

public static class LoadoutDebugMenu
{
    [MenuItem("Blockforge/Debug/Garage : robot test 3 armes-modules")]
    public static void LoadTestRobot()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[LoadoutDebug] Lance d'abord le garage en Play mode.");
            return;
        }

        var bp = new RobotBlueprint { name = "LOADOUT TEST" };
        for (int x = 12; x <= 18; x++)
            for (int y = 0; y <= 3; y++)
                bp.blocks.Add(new PlacedBlock { blockId = "Block_01_Cube", x = x, y = y, z = 15 });

        int top = BlockFootprint.Compose(1, 0);
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Laser_N1_Wasp", x = 12, y = 4, z = 15, rotation = top });
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Laser_N1_Wasp", x = 13, y = 4, z = 15, rotation = top });
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Plasma_N1_Pulser", x = 15, y = 4, z = 15, rotation = top });
        bp.blocks.Add(new PlacedBlock { blockId = "Block_19_Radar", x = 18, y = 4, z = 15, rotation = top });

        RobotSession.Blueprint = bp;
        RobotSession.Dirty = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Debug.Log("[LoadoutDebug] Robot test charge : 3 types montes (Laser Wasp x2, Plasma Pulser, Radar).");
    }
}
