using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// =========================================================
// FONDU AU NOIR ENTRE DEUX SCENES (petit, sans ecran de chargement)
//   SceneFade.Load("Matchmaking_Space");
// - la scene se charge en asynchrone pendant le fondu de sortie
//   (activation retenue) ;
// - ecran noir : la scene s'active, une frame pour ses Start,
//   puis fondu d'entree ;
// - un voile UI Toolkit DontDestroyOnLoad, JUSTE SOUS le Forge
//   Loader (sortingOrder du loader - 1) : le bandeau de
//   matchmaking reste visible par-dessus pendant tout le fondu ;
// - une seule transition a la fois (demande en double ignoree) ;
// - animations reduites : fondus raccourcis (MotionPanel).
// Pour les chargements « lourds » (garage, map), garder
// SceneLoader et son ecran du Forge Loader.
// =========================================================

public sealed class SceneFade : MonoBehaviour
{
    public const float DefaultOutSeconds = 0.35f;
    public const float DefaultInSeconds = 0.6f;
    private const int FallbackSortingOrder = 1099; // loader : 1100

    private static SceneFade instance;
    public static bool IsRunning => instance != null;

    private VisualElement veil;

    public static void Load(string sceneName, float outSeconds = DefaultOutSeconds, float inSeconds = DefaultInSeconds)
    {
        if (string.IsNullOrEmpty(sceneName) || instance != null)
            return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Toast.Error($"Couldn't load '{sceneName}'. Is the scene in the build settings?");
            return;
        }

        var go = new GameObject("[SceneFade]");
        go.SetActive(false);
        DontDestroyOnLoad(go);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = CreatePanelSettings();
        instance = go.AddComponent<SceneFade>();
        go.SetActive(true);
        instance.StartCoroutine(instance.Run(sceneName, BlockforgeTheme.Fade(outSeconds), BlockforgeTheme.Fade(inSeconds)));
    }

    // Memes reglages que le loader, un cran en dessous
    private static PanelSettings CreatePanelSettings()
    {
        var loader = ForgeLoaderHost.Exists ? ForgeLoaderHost.Ensure().PanelSettings : null;
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.name = "SceneFadePanelSettings (runtime)";
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 1f;
        ps.sortingOrder = loader != null ? loader.sortingOrder - 1 : FallbackSortingOrder;
        if (loader != null)
            ps.themeStyleSheet = loader.themeStyleSheet;
        return ps;
    }

    private void BuildVeil()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        veil = new VisualElement { name = "SceneFadeVeil" };
        veil.style.position = Position.Absolute;
        veil.style.left = veil.style.top = veil.style.right = veil.style.bottom = 0;
        veil.style.backgroundColor = Color.black;
        veil.style.opacity = 0f;
        veil.pickingMode = PickingMode.Position; // bloque les clics pendant la transition
        root.Add(veil);
    }

    private IEnumerator Run(string sceneName, float outSeconds, float inSeconds)
    {
        yield return null; // UIDocument pret
        BuildVeil();

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        yield return Fade(0f, 1f, outSeconds);
        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;
        yield return null; // Start de la nouvelle scene (camera en place)

        yield return Fade(1f, 0f, inSeconds);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            veil.style.opacity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        veil.style.opacity = to;
    }

    private void OnDestroy()
    {
        var doc = GetComponent<UIDocument>();
        if (doc != null && doc.panelSettings != null)
            Destroy(doc.panelSettings);
        if (instance == this)
            instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }
}
