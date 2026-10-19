using System.Collections;
using UnityEngine;

// =========================================================
// SCENE D'ATTENTE DU MATCHMAKING (Matchmaking_Space)
// « Find a match » sur l'ecran Play : la recherche demarre, un
// petit fondu au noir (SceneFade) amene cette scene — l'espace
// et le Super Destroyer — sous le bandeau du Forge Loader.
// - Match trouve : le callback de PlayMenuController lance
//   MatchIntro (ecran Versus) qui charge la map ; si l'intro est
//   annulee et qu'on est encore ici, retour au menu.
// - Recherche annulee (Echap, bouton Cancel du bandeau) : fondu
//   de retour vers le menu, qui rouvre l'ecran Play.
// Ouverte directement dans l'editeur (Play sur la scene) : rien
// ne se passe, la scene sert d'apercu.
// Decor genere par Blockforge > Setup Matchmaking Space Scene.
// Animation du vaisseau : plus tard (le pivot « SuperDestroyer »
// est l'objet a animer).
// =========================================================

public class MatchmakingSpaceScene : MonoBehaviour
{
    public const string SceneName = "Matchmaking_Space";

    // Pose par l'ecran Play au lancement de la recherche
    public static bool EnteredFromSearch;

    [Tooltip("Scene rechargee si la recherche est annulee")]
    public string returnSceneName = "MainMenu";

    private bool leaving;

    private void OnEnable()
    {
        MatchmakingService.Ended += OnSearchEnded;
    }

    private void OnDisable()
    {
        MatchmakingService.Ended -= OnSearchEnded;
    }

    private void Start()
    {
        if (!EnteredFromSearch)
            return; // apercu dans l'editeur

        // Recherche annulee pendant le fondu d'arrivee : on repart aussitot
        if (!MatchmakingService.IsActive && !MatchIntro.IsRunning)
            ReturnToMenu();
    }

    private void OnSearchEnded(bool matched)
    {
        if (!EnteredFromSearch)
            return;
        if (matched)
            StartCoroutine(WatchIntro());
        else
            ReturnToMenu();
    }

    // Ended part avant le callback qui lance MatchIntro : on lui laisse une frame
    private IEnumerator WatchIntro()
    {
        yield return null;
        while (MatchIntro.IsRunning)
            yield return null;
        // Encore ici : intro annulee (Echap) ou map introuvable
        ReturnToMenu();
    }

    private void ReturnToMenu()
    {
        if (leaving)
            return;
        leaving = true;
        EnteredFromSearch = false;
        PlayMenuController.ReopenOnReturn = true;
        SceneFade.Load(returnSceneName);
    }

    private void OnDestroy()
    {
        // La map a pris le relais : la prochaine entree viendra d'une nouvelle recherche
        if (!leaving)
            EnteredFromSearch = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        EnteredFromSearch = false;
    }
}
