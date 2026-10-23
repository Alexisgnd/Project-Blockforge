using UnityEngine;

// =========================================================
// ALERTES DU MATCH PAR EQUIPES (TdmMatch)
// Pose sur l'objet de SfxManager, lit TdmMatch.Instance a
// chaque image (aucun branchement dans la logique du match).
// Joue SfxId.MatchAlert, uniquement en phase Live :
// - chrono : passage sous 1:00, 0:30 et 0:10 (un match plus
//   court que le seuil ne le declenche pas au depart)
// - score : une equipe arrive a un kill de la victoire
//   (ScoreToWin - 1), une fois par equipe et par match
// Deux alertes dans la meme image = un seul son.
// Reapparition du joueur (robot detruit) : SfxId.RespawnTick a
// chaque seconde du compte a rebours « Respawn in » de
// SpectateOverlay (memes secondes arrondies au-dessus : 0:09 ...
// 0:01), et un dernier quand le bouton Respawn devient
// disponible. Rien au moment de la destruction (0:10), rien
// pendant l'attente de la reapparition automatique.
// =========================================================

public class MatchAlertSfx : MonoBehaviour
{
    private static readonly float[] TimeMarks = { 60f, 30f, 10f };

    private TdmMatch match;
    private float lastTimeLeft = -1f;
    private readonly bool[] sideWarned = new bool[2];
    private int respawnShown = -1;   // secondes affichees avant de pouvoir reapparaitre (-1 : en vie)

    private void Update()
    {
        var m = TdmMatch.Instance;
        if (m != match)
        {
            match = m;
            lastTimeLeft = -1f;
            sideWarned[0] = sideWarned[1] = false;
            respawnShown = -1;
        }
        if (m == null || m.Phase != TdmPhase.Live)
        {
            respawnShown = -1;
            return;
        }

        UpdateRespawnTicks(m);

        bool alert = false;

        float timeLeft = m.TimeLeft;
        if (lastTimeLeft >= 0f)
        {
            foreach (float mark in TimeMarks)
            {
                if (lastTimeLeft > mark && timeLeft <= mark)
                    alert = true;
            }
        }
        lastTimeLeft = timeLeft;

        int oneKillLeft = m.ScoreToWin - 1;
        if (oneKillLeft > 0)
        {
            for (int side = 0; side < 2; side++)
            {
                if (sideWarned[side] || m.SideScore(side) < oneKillLeft)
                    continue;
                sideWarned[side] = true;
                alert = true;
            }
        }

        if (alert)
            SfxManager.Play(SfxId.MatchAlert);
    }

    // Un bip chaque fois que la seconde affichee descend (9, 8 ... 1, puis 0 = pret)
    private void UpdateRespawnTicks(TdmMatch m)
    {
        var local = m.Local;
        int shown = -1;
        if (local != null && !local.Alive && local.DiedAt >= 0f)
        {
            float left = local.RespawnReadyAt - Time.time;
            shown = left > 0f ? Mathf.CeilToInt(left) : 0;
        }
        if (shown >= 0 && respawnShown >= 0 && shown < respawnShown)
            SfxManager.Play(SfxId.RespawnTick);
        respawnShown = shown;
    }
}
