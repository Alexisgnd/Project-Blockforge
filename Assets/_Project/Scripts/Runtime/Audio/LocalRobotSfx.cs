using UnityEngine;

// =========================================================
// SONS DU ROBOT REGARDE (match, spectateur, terrain d'essai)
// Pose sur l'objet de SfxManager. Le robot qui « parle » est
// celui que suit la camera de match (RobotFollowCamera) :
// - en jeu : le robot du joueur
// - joueur detruit : le coequipier regarde (SpectateOverlay)
// - match de bots (BotSpectatorView) : le bot regarde
// - camera libre (F, camera de suivi coupee) ou robot detruit :
//   aucun son
// Les alertes du match (MatchAlertSfx) restent pour tous.
// Changer de robot regarde coupe les sons en cours du precedent.
//
// - Tir refuse faute d'energie (RobotEnergy.Refused) :
//   SfxId.WeaponNoEnergy. Refused est emis a chaque tentative
//   (et a chaque image pour la Tesla) : SfxLibrary.noOverlap
//   ne relance le son que s'il ne joue pas deja.
// - Regeneration (RobotHealth.regenDelay = 10 s sans degats) :
//   - 5 dernieres secondes d'attente : SfxId.RegenCountdown en
//     boucle
//   - un degat pendant cette attente : la boucle s'arrete et
//     SfxId.RegenInterrupted est joue une fois
//   - la regeneration demarre : la boucle s'arrete, SfxId
//     .RobotRegen est joue ; il est coupe si le robot est touche
//     ou detruit pendant le son, va au bout s'il est repare avant
// Un robot intact n'attend rien : pas de bips.
// =========================================================

public class LocalRobotSfx : MonoBehaviour
{
    // Secondes avant la regeneration pendant lesquelles les bips tournent
    public const float CountdownSeconds = 5f;
    private const float CameraSearchInterval = 1f;

    private RobotFollowCamera followCamera;
    private float cameraSearchTimer;

    private Transform watched;
    private RobotHealth health;
    private RobotEnergy energy;

    private float seenDamageTime;
    private bool wasRegenerating;
    private AudioSource countdownVoice;
    private AudioSource regenVoice;

    // Appele par SfxManager a chaque scene chargee
    public void FindCamera()
    {
        followCamera = FindAnyObjectByType<RobotFollowCamera>();
        cameraSearchTimer = CameraSearchInterval;
    }

    private void OnDestroy()
    {
        Watch(null);
    }

    private void Update()
    {
        // TdmMatch peut ajouter la camera de suivi apres le chargement de la scene
        if (followCamera == null)
        {
            cameraSearchTimer -= Time.unscaledDeltaTime;
            if (cameraSearchTimer <= 0f)
                FindCamera();
        }

        Transform target = followCamera != null && followCamera.isActiveAndEnabled ? followCamera.target : null;
        if (target != watched)
            Watch(target);

        if (health == null)
            return;
        if (health.IsDead)
        {
            StopCountdown();
            StopRegen();
            wasRegenerating = false;
            seenDamageTime = health.LastDamageTime;
            return;
        }

        // Degat subi depuis la derniere image
        bool hit = health.LastDamageTime > seenDamageTime;
        seenDamageTime = health.LastDamageTime;
        if (hit)
        {
            if (countdownVoice != null)
            {
                StopCountdown();
                SfxManager.Play(SfxId.RegenInterrupted);
            }
            StopRegen();
        }

        bool regenerating = health.Regenerating;
        if (regenerating && !wasRegenerating)
        {
            StopCountdown();
            regenVoice = SfxManager.Play(SfxId.RobotRegen);
        }
        wasRegenerating = regenerating;

        // Attente : robot abime, pas encore en regeneration, dans les 5 dernieres secondes
        float sinceDamage = Time.time - health.LastDamageTime;
        bool waiting = health.Damaged && !regenerating
                       && sinceDamage >= health.regenDelay - CountdownSeconds
                       && sinceDamage < health.regenDelay;
        if (!waiting)
            StopCountdown();
        else if (countdownVoice == null)
            countdownVoice = SfxManager.Play(SfxId.RegenCountdown);
    }

    // Change de robot ecoute : coupe les sons du precedent
    private void Watch(Transform target)
    {
        StopCountdown();
        StopRegen();
        if (energy != null)
            energy.Refused -= OnRefused;

        watched = target;
        health = target != null ? target.GetComponentInParent<RobotHealth>() : null;
        energy = target != null ? target.GetComponentInParent<RobotEnergy>() : null;
        if (energy != null)
            energy.Refused += OnRefused;

        // Etat du nouveau robot pris tel quel : pas de son pour un degat
        // ou une regeneration deja en cours au moment ou on le rejoint
        seenDamageTime = health != null ? health.LastDamageTime : 0f;
        wasRegenerating = health != null && health.Regenerating;
    }

    private void OnRefused()
    {
        if (health == null || !health.IsDead)
            SfxManager.Play(SfxId.WeaponNoEnergy);
    }

    private void StopCountdown()
    {
        SfxManager.Stop(countdownVoice, SfxId.RegenCountdown);
        countdownVoice = null;
    }

    private void StopRegen()
    {
        SfxManager.Stop(regenVoice, SfxId.RobotRegen);
        regenVoice = null;
    }
}
