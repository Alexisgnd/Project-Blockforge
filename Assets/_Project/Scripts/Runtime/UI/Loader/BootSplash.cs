using System.Collections;
using UnityEngine;

// =========================================================
// SPLASH DU BOOT (scene Boot)
// Pose dans Boot par Blockforge > Loader > Setup Forge Loader.
// L'ecran de connexion (BootAuthController : etat de la connexion
// Steam, Retry / Play offline, compte email) s'affiche d'abord,
// comme avant. Connexion Steam automatique : « Signing in with
// Steam » reste visible au moins 1,6 s, puis « Signed in with
// Steam » 1,1 s, avant le logo (reglables dans l'Inspector). Le splash ne demarre QUE quand la connexion est
// acceptee, c'est-a-dire quand BootAuthController ouvre le menu
// (Steam accepte, compte email, ou Play offline) : ce premier
// SceneLoader.Load est intercepte et presente par le splash.
// Rien ne se charge pendant l'ecran de connexion (pas d'accroc qui
// masquerait l'etat Steam) : tout part une fois la connexion acceptee.
// Etapes (progression reelle, LoadingService, splash) :
//   Loading parts       0-25 %   ressources de l'interface (polices
//                                du DS, logo, musique, donnees mock)
//   Building the arena  25-50 %  chargement de la scene MainMenu
//                                (le hangar), activation retenue
//   Waking the robots   50-75 %  robots du joueur rapproches du
//                                serveur (RobotStore.ListAsync)
//   Syncing the match   75-100 % activation du hangar sous le splash
//   Ready               100 %    le mark se verrouille, le splash s'efface
// =========================================================

public sealed class BootSplash : MonoBehaviour
{
    [Tooltip("Scene prechargee pendant la connexion (celle que BootAuthController ouvre ensuite)")]
    [SerializeField] private string preloadScene = "MainMenu";

    [Tooltip("Ressources chargees a l'etape « Loading parts » (chemins Resources, sans extension)")]
    [SerializeField] private string[] warmResources =
    {
        "DesignSystem/Fonts/Barlow-Regular SDF",
        "DesignSystem/Fonts/Barlow-Medium SDF",
        "DesignSystem/Fonts/Barlow-SemiBold SDF",
        "DesignSystem/Fonts/BarlowCondensed-Bold SDF",
        "DesignSystem/Fonts/BarlowCondensed-SemiBold SDF",
        "DesignSystem/Fonts/JetBrainsMono-Medium SDF",
        "DesignSystem/Logo/logo-horizontal",
        "DesignSystem/Logo/mark",
        "MusicLibrary",
        "Meta/MockMetaData",
    };

    [Tooltip("Garde-fou : la synchro des robots ne retient pas le splash plus longtemps (s)")]
    [SerializeField] private float syncTimeout = 8f;

    [Tooltip("Connexion Steam automatique : l'etat « Signing in with Steam » reste visible au moins ce temps depuis l'ouverture du Boot (s)")]
    [SerializeField] private float minConnectingSeconds = 1.6f;

    [Tooltip("Connexion Steam acceptee : l'etat « Signed in with Steam » reste visible ce temps avant le logo (s)")]
    [SerializeField] private float signedInHoldSeconds = 1.1f;

    [Tooltip("Compte email ou Play offline (le joueur vient d'agir) : court temps avant le logo (s)")]
    [SerializeField] private float manualHoldSeconds = 0.3f;

    private string handoff;
    private bool abandoned;
    private System.Func<string, bool> interceptor;

    private void Awake()
    {
        // Avant tout Start : BootAuthController peut ouvrir le menu des son Start
        interceptor = name =>
        {
            handoff ??= name;
            return true;
        };
        SceneLoader.Interceptor = interceptor;
        ForgeLoaderHost.Ensure().Run(Run(preloadScene, warmResources, syncTimeout));
    }

    private void OnDestroy()
    {
        // Boot quitte sans connexion acceptee : ne plus detourner les chargements
        if (handoff == null)
        {
            abandoned = true;
            if (SceneLoader.Interceptor == interceptor)
                SceneLoader.Interceptor = null;
        }
    }

    private IEnumerator Run(string sceneToPreload, string[] resources, float timeout)
    {
        // ---------- Ecran de connexion : rien ne se charge, rien ne masque l'etat Steam ----------
        // Temps « vu » : chaque frame compte au plus 0,1 s, pour qu'un accroc
        // (entree en Play mode, compilation de shaders) ne mange pas l'affichage
        float connectingSeen = 0f;
        while (handoff == null)
        {
            if (abandoned)
                yield break;
            connectingSeen += SeenDelta();
            yield return null;
        }

        // ---------- Connexion acceptee : la laisser voir, puis le splash ----------
        SceneLoader.Interceptor = null;
        bool steamAuto = SessionService.IsOnline && SessionService.Identity != null && SessionService.Identity.IsSteam;
        float hold = steamAuto
            ? Mathf.Max(minConnectingSeconds - connectingSeen, signedInHoldSeconds)
            : manualHoldSeconds;
        Debug.Log($"[BootSplash] Connexion acceptee ({(steamAuto ? "Steam" : "manuelle")}) apres {connectingSeen:0.00} s d'ecran de connexion ; etat affiche encore {hold:0.00} s");
        float held = 0f;
        while (held < hold)
        {
            yield return null;
            held += SeenDelta();
        }
        float splashStart = Time.realtimeSinceStartup;

        var session = LoadingService.Begin(LoadContext.Splash, LoaderTheme.Load().splashSteps);

        // 1. Loading parts
        var requests = new ResourceRequest[resources.Length];
        for (int i = 0; i < resources.Length; i++)
            requests[i] = Resources.LoadAsync<Object>(resources[i]);
        while (!AllDone(requests))
        {
            session.Report(0.25f * Average(requests));
            yield return null;
        }
        session.Report(0.25f);

        if (handoff != sceneToPreload)
        {
            // Destination inattendue : le chargement d'ecran prend le relais
            session.End();
            SceneLoader.Load(handoff);
            yield break;
        }

        AsyncOperation scene = SceneLoader.Preload(sceneToPreload);

        // 2. Building the arena
        while (scene != null && scene.progress < 0.9f)
        {
            session.Report(0.25f + 0.25f * scene.progress / 0.9f);
            yield return null;
        }
        session.Report(0.5f);

        // 3. Waking the robots (robots du joueur, une synchro par session)
        bool synced = false;
        SyncRobots(() => synced = true);
        float waited = 0f;
        while (!synced && waited < timeout)
        {
            session.Report(0.5f + 0.2f * Mathf.Clamp01(waited / timeout));
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        session.Report(0.75f);

        // 4. Syncing the match : le hangar s'active sous le splash opaque
        if (scene != null)
        {
            scene.allowSceneActivation = true;
            SceneLoader.ReleasePreload(scene);
            while (!scene.isDone)
            {
                session.Report(0.75f + 0.2f * scene.progress);
                yield return null;
            }
            yield return null; // premiere frame du hangar (Start)
        }
        else
        {
            // Prechargement impossible (scene hors build) : chargement classique
            session.End();
            SceneLoader.Load(handoff);
            yield break;
        }

        // Ready : le mark se verrouille et se tient, puis le splash s'efface
        session.MarkLoaded();
        yield return session.WaitForTransition();
        session.End();
        Debug.Log($"[BootSplash] Logo de chargement : {Time.realtimeSinceStartup - splashStart:0.00} s jusqu'au hangar");
    }

    private static float SeenDelta() => Mathf.Min(Time.unscaledDeltaTime, 0.1f);

    private static bool AllDone(ResourceRequest[] requests)
    {
        foreach (var r in requests)
            if (r != null && !r.isDone)
                return false;
        return true;
    }

    private static float Average(ResourceRequest[] requests)
    {
        if (requests.Length == 0)
            return 1f;
        float sum = 0f;
        foreach (var r in requests)
            sum += r == null ? 1f : r.progress;
        return sum / requests.Length;
    }

    private static async void SyncRobots(System.Action done)
    {
        try
        {
            await RobotStore.ListAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BootSplash] Synchro des robots : {e.Message}");
        }
        finally
        {
            done();
        }
    }
}
