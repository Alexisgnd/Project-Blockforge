using UnityEngine;

// =========================================================
// PORTE DU MATCH (gel des controles)
// Frozen = vrai pendant le compte a rebours d'avant match et
// apres la fin : le pilotage du joueur (RobotTestDriver), le
// tir (WeaponFireController) et les bots (BotBrain) ne font
// plus rien. La camera reste libre. Pose et leve par TdmMatch ;
// remis a faux au chargement de chaque scene de jeu et quand le
// match est detruit (retour au menu).
// =========================================================

public static class MatchGate
{
    public static bool Frozen { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Frozen = false;
}
