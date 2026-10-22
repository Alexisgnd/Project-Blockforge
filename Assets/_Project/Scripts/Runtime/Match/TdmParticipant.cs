using System.Collections.Generic;
using UnityEngine;

// =========================================================
// PARTICIPANT D'UN MATCH PAR EQUIPES (etat du match, sans affichage)
// Un par siege (joueur local ou bot) : identite, equipe (Side :
// 0 = base A, 1 = base B), statistiques, point d'apparition
// attribue au debut du match (il y revient a chaque
// reapparition), robot en jeu et minuterie de reapparition.
// TdmCombatant le rattache au robot : un collider touche donne
// son participant (GetComponentInParent<TdmCombatant>).
// L'etat vit ici, l'affichage le lit : un futur serveur de
// match pourra le remplir sans toucher a l'interface.
// =========================================================

public sealed class TdmParticipant
{
    public int Id;
    public string Name;
    public string RobotName;
    public int Side;                 // 0 = base A, 1 = base B
    public bool IsBot;
    public bool IsLocal;
    public RobotBlueprint Blueprint;
    public BotPersonality Personality;
    public BotNavStats Nav;   // bots : bilan de navigation du match (BotBrain)
    public TdmParticipant BotTarget;   // bots : cible en cours (concentration du feu)   // bots seulement

    // Statistiques
    public int Kills, Deaths, Assists;
    public float Damage;
    public int Score => Kills * 100 + Assists * 50 + Mathf.RoundToInt(Damage / 10f);

    // Apparition
    public Transform Spawn;
    public Vector3 SpawnPosition;
    public Quaternion SpawnRotation = Quaternion.identity;

    // Robot en jeu
    public GameObject Robot;
    public RobotHealth Health;
    public RobotLocomotion Loco;
    public bool Alive => Robot != null && Health != null && !Health.IsDead;
    // Assemblage holographique en cours : intouchable et ignore des bots
    public RobotSpawnSequence SpawnSequence;
    public bool Spawning => SpawnSequence != null && SpawnSequence.Running;
    public bool Targetable => Alive && !Spawning;
    public float HealthFraction
    {
        get
        {
            if (!Alive)
                return 0f;
            // Le robot est detruit sous destroyFraction : la vie affichee va de ce seuil a 100 %
            float floor = Health.destroyFraction;
            return Mathf.Clamp01((Health.Fraction - floor) / Mathf.Max(0.01f, 1f - floor));
        }
    }

    // Mort et reapparition
    public float DiedAt = -1f;
    public float RespawnReadyAt;     // le joueur peut reapparaitre a partir d'ici
    public float AutoRespawnAt;      // reapparition forcee
    public TdmParticipant LastKiller;
    public string LastKillerWeapon;
    public BlockDefinition LastKillerWeaponDef;   // bloc d'arme du kill (pictogramme de la killfeed)
    public float LastKillerDistance;

    // Attribution des degats recus (kills et assists)
    internal readonly Dictionary<TdmParticipant, float> RecentAttackers = new();
    internal TdmParticipant LastAttacker;
    internal float LastAttackerTime = -100f;
    internal string LastAttackerWeapon;
    internal BlockDefinition LastAttackerWeaponDef;
    internal float LastAttackerDistance;

    public Vector3 Position => Robot != null ? Robot.transform.position : SpawnPosition;

    // Centre des blocs encore en vie (cible des bots, plaques de nom)
    public Vector3 AimPoint
    {
        get
        {
            if (Robot == null)
                return SpawnPosition;
            return Health != null ? Robot.transform.TransformPoint(Health.AliveCenter()) : Robot.transform.position + Vector3.up;
        }
    }
}

// Rattache un robot en jeu a son participant
public sealed class TdmCombatant : MonoBehaviour
{
    [System.NonSerialized] public TdmParticipant Participant;

    public static TdmParticipant Of(Component c)
    {
        if (c == null)
            return null;
        var tag = c.GetComponentInParent<TdmCombatant>();
        return tag != null ? tag.Participant : null;
    }

    public static TdmParticipant Of(GameObject go) => go != null ? Of(go.transform) : null;
}

// =========================================================
// RELATION AU JOUEUR LOCAL (couleur des tirs)
// Seul endroit qui decide si un robot est allie ou ennemi pour
// l'affichage : JAMAIS une couleur fixee par l'equipe (Side). Le
// joueur local et ses allies sont Ally (bleu), les autres Enemy
// (rouge), cote client : un futur serveur dira seulement qui est
// avec qui.
// - Match (TdmMatch) : equipe du participant (TdmCombatant)
//   comparee a celle du joueur local (TdmMatch.TeamOf)
// - Hors match (Map_Test, CombatArena) ou robot sans participant :
//   le robot pilote par le joueur (WeaponFireController.manual
//   faux) est Ally, le reste Enemy
// Couleurs : jetons du theme hud (BlockforgeTheme.Hud.TeamAlly /
// TeamEnemy), les memes que le HUD.
// =========================================================

public static class TeamRelation
{
    public static Blockforge.Meta.Team Of(GameObject robot)
    {
        if (robot == null)
            return Blockforge.Meta.Team.Enemy;
        var match = TdmMatch.Instance;
        var p = match != null ? TdmCombatant.Of(robot) : null;
        if (p != null)
            return match.TeamOf(p);
        var fire = robot.GetComponentInParent<WeaponFireController>();
        return fire != null && !fire.manual ? Blockforge.Meta.Team.Ally : Blockforge.Meta.Team.Enemy;
    }

    public static Color ShotColor(Blockforge.Meta.Team relation) =>
        relation == Blockforge.Meta.Team.Ally ? BlockforgeTheme.Hud.TeamAlly : BlockforgeTheme.Hud.TeamEnemy;
}
