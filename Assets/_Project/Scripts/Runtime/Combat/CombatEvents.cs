using UnityEngine;

// =========================================================
// EVENEMENTS DE COMBAT (retours visuels du HUD)
// - RobotHit : emis par RobotHealth apres chaque impact qui
//   retire des PV, quel que soit le tireur ; le HUD du joueur
//   local filtre sur son propre robot (attacker). Contenu :
//   - damage         : PV reellement retires (pas la valeur
//                      nominale de l'arme ; un rail a 540 sur un
//                      cube de 56 PV donne 56)
//   - detachedBlocks : blocs tombes parce que l'impact a detruit
//                      un bloc qui soutenait une partie du robot
//                      (base du "point faible", seuil dans
//                      CombatFeedbackTheme.weakSpotMinBlocks)
//   - isKill         : cet impact a detruit le robot
//   - weapon         : bloc d'arme qui a tire (null : inconnu)
// - Kill : emis par TdmMatch a chaque robot detruit (killfeed).
//   killer null : mort sans tireur (arene) ou auto-destruction.
// =========================================================

public struct RobotHitEvent
{
    public RobotHealth target;
    public GameObject attacker;
    public BlockDefinition weapon;
    public Vector3 point;
    public float damage;
    public int detachedBlocks;
    public bool isKill;
}

public enum KillCause { Weapon, Environment, SelfDestruct }

public struct KillEvent
{
    public TdmParticipant killer;     // null : arene ou auto-destruction
    public TdmParticipant victim;
    public KillCause cause;
    public BlockDefinition weapon;    // bloc d'arme qui a porte le coup (null : inconnu)
}

public static class CombatEvents
{
    public static event System.Action<RobotHitEvent> RobotHit;
    public static event System.Action<KillEvent> Kill;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        RobotHit = null;
        Kill = null;
    }

    public static void RaiseHit(RobotHitEvent e) => RobotHit?.Invoke(e);
    public static void RaiseKill(KillEvent e) => Kill?.Invoke(e);
}
