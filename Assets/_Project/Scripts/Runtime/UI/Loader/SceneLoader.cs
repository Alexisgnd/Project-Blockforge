using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// CHANGEMENT DE SCENE AVEC LE FORGE LOADER
// Remplace SceneManager.LoadScene dans tout le jeu :
//   SceneLoader.Load("Garage");
// - chargement asynchrone, activation retenue ; la progression
//   reelle (op.progress) est declaree au LoadingService, que le
//   chargement d'ecran affiche par-dessus l'ecran precedent ;
// - charge (90 %) : la session passe a 100 % « Ready », le mark
//   rejoint l'etat verrouille, PUIS la scene s'active, puis
//   l'overlay s'efface ;
// - une seule transition a la fois (les demandes en double sont
//   ignorees) ;
// - Preload : le splash du lancement precharge le hangar pendant
//   la connexion ; Load reprend ce prechargement.
// - Interceptor : pose par le splash du lancement, qui presente
//   lui-meme le premier chargement.
// MatchIntro garde son propre ecran Versus (et son chargement).
// =========================================================

public static class SceneLoader
{
    public static bool IsLoading { get; private set; }
    private static bool activating;

    // Vrai = la demande est prise en charge ailleurs (splash du lancement)
    public static Func<string, bool> Interceptor;

    private static string preloadName;
    private static AsyncOperation preloadOp;
    private static string pendingScene; // demande arrivee pendant une transition (Awake de la nouvelle scene…)

    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;
        if (Interceptor != null && Interceptor(sceneName))
            return;
        if (IsLoading)
        {
            // Pendant l'activation, la nouvelle scene peut deja demander la suivante
            // (BootAuthController, AuthGuard) : on la garde pour la fin de la transition
            if (activating)
                pendingScene = sceneName;
            else
                Debug.Log($"[SceneLoader] Transition deja en cours : '{sceneName}' ignore.");
            return;
        }
        IsLoading = true;
        ForgeLoaderHost.Ensure().Run(LoadRoutine(sceneName));
    }

    // Charge la scene sans l'activer (une seule a la fois)
    public static AsyncOperation Preload(string sceneName)
    {
        if (preloadOp != null)
            return preloadName == sceneName ? preloadOp : null;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
            return null;
        preloadOp = SceneManager.LoadSceneAsync(sceneName);
        if (preloadOp == null)
            return null;
        preloadOp.allowSceneActivation = false;
        preloadName = sceneName;
        return preloadOp;
    }

    // Le prechargement est consomme par celui qui l'active
    internal static void ReleasePreload(AsyncOperation op)
    {
        if (op == preloadOp)
        {
            preloadOp = null;
            preloadName = null;
        }
    }

    private static IEnumerator LoadRoutine(string sceneName)
    {
        var theme = LoaderTheme.Load();
        var session = LoadingService.Begin(LoadContext.ScreenLoad, theme.StepsFor(sceneName));

        AsyncOperation op = null;
        if (preloadOp != null)
        {
            if (preloadName == sceneName)
            {
                op = preloadOp;
            }
            else
            {
                // Un autre prechargement retient la file : on le laisse passer d'abord
                var other = preloadOp;
                other.allowSceneActivation = true;
                ReleasePreload(other);
                while (!other.isDone)
                    yield return null;
            }
        }

        if (op == null)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Toast.Error($"Couldn't load '{sceneName}'. Is the scene in the build settings?");
                session.End();
                IsLoading = false;
                yield break;
            }
            op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
        }

        while (op.progress < 0.9f)
        {
            session.Report(op.progress / 0.9f);
            yield return null;
        }

        // Fini : le mark se verrouille et se tient, puis seulement la scene change
        session.MarkLoaded();
        yield return session.WaitForTransition();
        activating = true;
        op.allowSceneActivation = true;
        ReleasePreload(op);
        while (!op.isDone)
            yield return null;
        activating = false;
        IsLoading = false; // la nouvelle scene peut demander la suivante des son Start
        yield return null; // premiere frame de la nouvelle scene (Start)

        session.End();

        if (pendingScene != null)
        {
            string next = pendingScene;
            pendingScene = null;
            Load(next);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsLoading = false;
        activating = false;
        pendingScene = null;
        Interceptor = null;
        preloadOp = null;
        preloadName = null;
    }
}
