using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// =========================================================
// BANC D'ESSAI DU FORGE LOADER (scene LoaderPreview, hors build)
// Creee par Blockforge > Loader > Setup Loader Preview Scene.
// Panneau de controle (UI Toolkit, au-dessus du loader) :
// - Context : Splash / Screen load / Matchmaking ;
// - Speed : multiplie le temps du mark (et du chargement simule) ;
// - Progress : pilote a la main la barre (ou les sieges) ;
// - Simulate load : passe par le vrai LoadingService avec le
//   chargement simule (vite 45 %, lent 60 %, arret 0,5 s, vite 100 %),
//   ou une recherche simulee pour le matchmaking ;
// - Finish now : termine le chargement en cours a l'instant
//   (n'importe ou dans le cycle) pour verifier la fin verrouillee ;
// - Reduce motion : le reglage du joueur.
// Derriere le loader, un faux « ecran precedent » montre
// l'assombrissement du chargement d'ecran. Tailles de Game view :
// 1920 x 1080, 2560 x 1080, 1280 x 800 (ajoutees par le setup).
// =========================================================

public sealed class LoaderPreview : MonoBehaviour
{
    private enum PreviewContext { Splash, ScreenLoad, Matchmaking }

    private static readonly string[] ContextNames = { "Splash", "Screen load", "Matchmaking" };

    public VisualTreeAsset controlsTemplate;
    public VisualTreeAsset backdropTemplate;
    [Tooltip("Scene dont les etapes servent a l'apercu du chargement d'ecran")]
    public string screenLoadScene = "Garage";

    private ForgeLoaderHost host;
    private PreviewContext context = PreviewContext.Splash;
    private LoaderProgressView progressView;
    private MatchmakingStripView stripView;

    private Slider speed, progress;
    private Label readout;
    private LoadingSession simulated;
    private bool reshowPending;

    private void Start()
    {
        host = ForgeLoaderHost.Ensure();
        var panel = host.PanelSettings;

        if (backdropTemplate != null)
            MakeDocument("PreviewBackdrop", panel, backdropTemplate, -10f);

        if (controlsTemplate != null)
        {
            var root = MakeDocument("PreviewControls", panel, controlsTemplate, 100f);
            var dropdown = root.Q<DropdownField>("context");
            dropdown.choices = new System.Collections.Generic.List<string>(ContextNames);
            dropdown.index = 0;
            dropdown.RegisterValueChangedCallback(e => Show((PreviewContext)Mathf.Max(0, dropdown.index)));

            speed = root.Q<Slider>("speed");
            speed.RegisterValueChangedCallback(e => ForgeLoader.SpeedMultiplier = e.newValue);

            progress = root.Q<Slider>("progress");
            progress.RegisterValueChangedCallback(e => SetManualProgress(e.newValue));

            root.Q<Button>("simulate").clicked += Simulate;
            root.Q<Button>("finish").clicked += FinishNow;

            var reduce = root.Q<Toggle>("reduce");
            reduce.value = BlockforgeTheme.ReduceMotion;
            reduce.RegisterValueChangedCallback(e => BlockforgeTheme.ReduceMotion = e.newValue);

            readout = root.Q<Label>("readout");
        }

        LoadingService.Ended += OnSessionEnded;
        MatchmakingService.Ended += OnSearchEnded;
        Show(context);
    }

    private void OnDestroy()
    {
        ForgeLoader.SpeedMultiplier = 1f;
        LoadingService.Ended -= OnSessionEnded;
        MatchmakingService.Ended -= OnSearchEnded;
    }

    private VisualElement MakeDocument(string name, PanelSettings panel, VisualTreeAsset template, float order)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = panel;
        doc.visualTreeAsset = template;
        doc.sortingOrder = order;
        go.SetActive(true);
        return doc.rootVisualElement;
    }

    // ---------- Contexte ----------

    private void Show(PreviewContext ctx)
    {
        context = ctx;
        if (simulated != null && !simulated.IsEnded)
            simulated.End();
        simulated = null;
        if (MatchmakingService.IsActive)
            MatchmakingService.Cancel();

        host.HidePreviews();
        progressView = null;
        stripView = null;

        var theme = LoaderTheme.Load();
        switch (ctx)
        {
            case PreviewContext.Splash:
                progressView = host.ShowPreview(LoadContext.Splash);
                break;
            case PreviewContext.ScreenLoad:
                progressView = host.ShowPreview(LoadContext.ScreenLoad);
                break;
            default:
                stripView = host.ShowStripPreview("Team Deathmatch", theme.defaultRegion, theme.seats, theme.estimatedSeconds);
                break;
        }
        SetManualProgress(progress != null ? progress.value : 0f);
    }

    private string[] Steps()
    {
        var theme = LoaderTheme.Load();
        return context == PreviewContext.Splash ? theme.splashSteps : theme.StepsFor(screenLoadScene);
    }

    private void SetManualProgress(float value)
    {
        if (simulated != null && !simulated.IsEnded)
            return; // la simulation pilote
        if (progressView != null)
        {
            progressView.SetProgress(value, LoadingSession.StepAt(Steps(), value));
        }
        else if (stripView != null && !MatchmakingService.IsActive)
        {
            var theme = LoaderTheme.Load();
            int found = 1 + Mathf.RoundToInt(value * (theme.seats - 1));
            stripView.SetMatchmaking(found, theme.seats, value * theme.estimatedSeconds, theme.estimatedSeconds);
        }
    }

    // ---------- Simulation ----------

    private void Simulate()
    {
        float s = speed != null ? speed.value : 1f;
        var theme = LoaderTheme.Load();
        if (context == PreviewContext.Matchmaking)
        {
            host.HidePreviews();
            MatchmakingService.Begin("Team Deathmatch", theme.defaultRegion, theme.seats, theme.estimatedSeconds,
                                     new MockMatchmakingSource { Speed = s });
            return;
        }
        host.HidePreviews();
        simulated = MockLoad.Start(context == PreviewContext.Splash ? LoadContext.Splash : LoadContext.ScreenLoad, Steps(), s);
    }

    private void FinishNow()
    {
        if (context == PreviewContext.Matchmaking)
        {
            if (MatchmakingService.State == MatchmakingState.Searching)
                MatchmakingService.Report(MatchmakingService.Total);
            else
                stripView?.Loader.Finish(null);
            return;
        }
        if (simulated != null && !simulated.IsEnded)
            simulated.MarkLoaded(); // MockLoad voit IsLoaded et attend le verrouillage
        else
            progressView?.Loader.Finish(null);
    }

    private void OnSessionEnded(LoadingSession session)
    {
        if (session == simulated && !reshowPending)
            StartCoroutine(ReshowAfter(0.8f));
    }

    private void OnSearchEnded(bool matched)
    {
        if (!reshowPending)
            StartCoroutine(ReshowAfter(0.8f));
    }

    private IEnumerator ReshowAfter(float seconds)
    {
        reshowPending = true;
        yield return new WaitForSecondsRealtime(seconds);
        reshowPending = false;
        simulated = null;
        Show(context);
    }

    // ---------- Lecture ----------

    private void Update()
    {
        if (readout == null)
            return;
        var loader = stripView?.Loader ?? progressView?.Loader;
        if (simulated != null && !simulated.IsEnded)
            readout.text = $"{simulated.Step} · {Mathf.FloorToInt(simulated.Progress * 100f)}%";
        else if (loader != null)
            readout.text = $"Cycle {loader.TimeMs:0} ms{(loader.IsLocked ? " · locked" : "")}{(loader.ReduceMotion ? " · reduced" : "")}";
    }
}
