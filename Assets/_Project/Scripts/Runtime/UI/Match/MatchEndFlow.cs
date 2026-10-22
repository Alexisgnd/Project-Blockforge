using System.Collections;
using Blockforge.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// DEROULE DE FIN DE MATCH
// Installe dans la scene de match par RobotTestSpawner (au-dessus
// du HUD, inactif tant que rien ne se passe). A MatchEnded :
// 1. tout le HUD est cache (rosters, score, sante, reticule,
//    slots, plaques : le canvas du HUD entier), l'arene est figee
//    (timeScale 0), le curseur libere, la pause bloquee ;
// 2. MatchEndOverlay sur l'arene ; apres 5 s ou un confirm
//    (Espace / Entree / A / clic sur Skip) -> fondu vers
// 3. ResultsScreen ; apres 20 s ou Continue -> le hub (MainMenu).
//    Echap ne fait rien ici : il n'y a pas de retour a l'arene.
// La source des resultats est MetaServices.MatchResults (mock
// pilote par le menu Debug tant que le serveur de match n'existe
// pas) ; FireLocal() permet au gameplay de fournir un resultat
// (TdmMatch), Show() l'ouvre directement avec une autre source.
// =========================================================

public sealed class MatchEndFlow : MonoBehaviour
{
    public static bool IsRunning => instance != null && instance.running;
    private static MatchEndFlow instance;

    [Tooltip("Racine du HUD de match a cacher entierement (canvas MatchHUD_*)")]
    public GameObject hudRoot;
    [Tooltip("Scene du hub chargee apres Continue")]
    public string hubSceneName = "MainMenu";

    private RectTransform uiRoot;
    private IMatchResultSource source;
    private bool running;
    private MatchEndOverlay overlay;
    private ResultsScreen results;

    public static MatchEndFlow Install(GameObject hudRoot, string hubScene = "MainMenu")
    {
        if (instance != null)
        {
            instance.hudRoot = hudRoot;
            return instance;
        }
        var go = new GameObject("[MatchEndFlow]");
        instance = go.AddComponent<MatchEndFlow>();
        instance.hudRoot = hudRoot;
        instance.hubSceneName = hubScene;
        return instance;
    }

    private void Awake()
    {
        if (instance == null)
            instance = this;
        UIKit.ScreenCanvas(gameObject, BlockforgeTheme.Order.MatchEnd);
        uiRoot = (RectTransform)transform;
        source = MetaServices.MatchResults;
        source.MatchEnded += OnMatchEnded;
    }

    private void OnDestroy()
    {
        if (source != null)
            source.MatchEnded -= OnMatchEnded;
        if (instance == this)
            instance = null;
    }

    // Point d'entree du gameplay (ou du menu Debug) : une fin de match locale
    public static void FireLocal(MatchResult result)
    {
        if (MetaServices.MatchResults is MockMatchResultSource mock)
            mock.Fire(result);
    }

    // Fin de match fournie directement (source de resultats autre que le mock)
    public static void Show(MatchResult result)
    {
        if (instance != null)
            instance.OnMatchEnded(result);
    }

    private void OnMatchEnded(MatchResult result)
    {
        if (running)
            return;
        running = true;
        StartCoroutine(Run(result));
    }

    private IEnumerator Run(MatchResult result)
    {
        // 0 ms : plus rien du HUD, arene figee, curseur libre
        if (hudRoot != null)
            hudRoot.SetActive(false);
        foreach (var hud in FindObjectsByType<RobotTestHUD>(FindObjectsInactive.Exclude))
            hud.gameObject.SetActive(false);
        Time.timeScale = 0f;
        RobotTestDriver.SetCursorLocked(false);
        UIKit.EnsureEventSystem();

        overlay = MatchEndOverlay.Show(uiRoot, result);
        bool confirmed = false;
        overlay.Confirmed += () => confirmed = true;
        while (!confirmed)
            yield return null;

        // Fondu vers les resultats (220 ms)
        results = ResultsScreen.Show(uiRoot, result);
        yield return overlay.FadeOut(BlockforgeTheme.MotionScreen);
        Destroy(overlay.gameObject);
        overlay = null;

        bool continued = false;
        results.Continued += () => continued = true;
        while (!continued)
            yield return null;

        yield return results.FadeOut(BlockforgeTheme.MotionScreen);
        Time.timeScale = 1f;
        RobotSession.ReturnSceneName = hubSceneName;
        EnsureMenuSession();
        SceneLoader.Load(hubSceneName);
    }

    // Match lance directement (editeur, scene de test) : aucune session, et le garde
    // du menu renverrait vers Boot. On ouvre une session hors ligne (identite en
    // cache, sinon invite) pour que le retour se fasse bien sur le menu.
    private static void EnsureMenuSession()
    {
        if (!SessionService.HasIdentity)
            SessionService.ContinueOffline();
    }

    // Menu Debug : ouvre directement les resultats (sans overlay)
    public void OpenResults(MatchResult result)
    {
        if (running)
            return;
        running = true;
        StartCoroutine(RunResultsOnly(result));
    }

    private IEnumerator RunResultsOnly(MatchResult result)
    {
        if (hudRoot != null)
            hudRoot.SetActive(false);
        Time.timeScale = 0f;
        RobotTestDriver.SetCursorLocked(false);
        UIKit.EnsureEventSystem();
        results = ResultsScreen.Show(uiRoot, result);
        bool continued = false;
        results.Continued += () => continued = true;
        while (!continued)
            yield return null;
        yield return results.FadeOut(BlockforgeTheme.MotionScreen);
        Time.timeScale = 1f;
        RobotSession.ReturnSceneName = hubSceneName;
        EnsureMenuSession();
        SceneLoader.Load(hubSceneName);
    }
}
