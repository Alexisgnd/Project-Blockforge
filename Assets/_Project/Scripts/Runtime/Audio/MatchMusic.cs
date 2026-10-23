using UnityEngine;

// =========================================================
// MUSIQUE DU MATCH PAR EQUIPES (TdmMatch)
// Pose sur l'objet de MusicManager, lit TdmMatch.Instance a
// chaque image (aucun branchement dans la logique du match).
// - Arrivee sur la carte (Map_MARS, Map_MARS_Spectate) : Mars
//   Combat Loop, en boucle.
// - Premiere alarme (TdmMatch.FinalStretch : 1:00 restante, ou
//   une equipe a un kill de la victoire), une fois par match :
//   la musique de combat s'efface pendant le jingle de
//   transition, puis Mars Protocol entre a la fin du jingle,
//   depart a 1:25 (MusicLibrary.startSeconds), et boucle.
// - Robot du joueur qui apparait (depart du match et chaque
//   reapparition) : jingle RobotSpawn. Rien pour les bots, ni
//   en match de bots sans joueur.
// Fin du match : la musique en cours continue jusqu'au retour
// au hub (MainMenu remet la musique du menu).
// =========================================================

public class MatchMusic : MonoBehaviour
{
    private const float CombatFadeOut = 1.2f;    // la musique de combat s'efface sous le jingle
    private const float ProtocolFadeIn = 0.4f;   // Mars Protocol entre vite : on arrive en plein morceau
    private const float ProtocolOverlap = 0.2f;  // ... juste avant la fin du jingle

    private TdmMatch match;
    private bool started, finale;
    private float protocolAt = -1f;
    private GameObject lastLocalRobot;

    private void Update()
    {
        var m = TdmMatch.Instance;
        if (m != match)
        {
            match = m;
            started = finale = false;
            protocolAt = -1f;
            lastLocalRobot = null;
        }
        var music = MusicManager.Instance;
        if (m == null || music == null)
            return;

        // Jingle d'apparition : un nouveau robot pour le joueur (TdmMatch en
        // reconstruit un a chaque apparition)
        var robot = m.Local != null ? m.Local.Robot : null;
        if (robot != null && robot != lastLocalRobot)
            music.PlayStinger(MusicTrack.RobotSpawn);
        lastLocalRobot = robot;

        if (!started && m.Phase != TdmPhase.Ended)
        {
            started = true;
            if (!m.FinalStretch)
                music.Play(MusicTrack.MarsCombat);
        }

        if (!finale && m.Phase == TdmPhase.Live && m.FinalStretch)
        {
            finale = true;
            music.Play(MusicTrack.None, CombatFadeOut);
            float length = music.PlayStinger(MusicTrack.FinalTransition);
            protocolAt = Time.unscaledTime + Mathf.Max(0f, length - ProtocolOverlap);
        }

        if (protocolAt >= 0f && Time.unscaledTime >= protocolAt)
        {
            protocolAt = -1f;
            music.Play(MusicTrack.MarsProtocol, ProtocolFadeIn);
        }
    }
}
