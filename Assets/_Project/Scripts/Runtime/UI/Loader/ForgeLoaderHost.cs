using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.Video;

// =========================================================
// HOTE DU FORGE LOADER (unique, DontDestroyOnLoad)
// Un UIDocument au-dessus de tout (LoaderPanelSettings : 1920 x
// 1080, echelle sur la hauteur) qui survit aux changements de
// scene et presente les trois contextes :
// - LoadingService.Splash     -> ForgeSplash.uxml (plein ecran)
// - LoadingService.ScreenLoad -> ForgeScreenLoad.uxml (plaque sur
//   l'ecran precedent, assombri)
// - MatchmakingService        -> ForgeMatchmaking.uxml (bandeau bas,
//   cinematique plein ecran si LoaderTheme en fournit une)
// Fin d'un chargement : le mark se verrouille et se tient (porte
// de transition de la session), puis l'overlay s'efface en 220 ms
// (140 ms en animations reduites).
// Pendant le matchmaking, les notices (Toast) sont masquees : rien
// ne se pose sur la cinematique. Echap / bouton Est de la manette
// annulent la recherche.
// Heberge aussi les coroutines de SceneLoader, MockLoad et BootSplash.
// =========================================================

public sealed class ForgeLoaderHost : MonoBehaviour
{
    private const int FallbackSortingOrder = 1100;

    private static ForgeLoaderHost instance;

    private LoaderTheme theme;
    private UIDocument document;
    private VisualElement root;

    private LoaderProgressView splashView, screenView;
    private MatchmakingStripView stripView;

    private LoadingSession presented;
    private LoaderProgressView presentedView;
    // Un fondu par vue : un fondu de sortie n'est jamais coupe par l'entree d'une autre vue
    private readonly Dictionary<VisualElement, Coroutine> fades = new();
    private bool stripLeaving;

    private VideoPlayer video;
    private RenderTexture videoTarget;

    public static bool Exists => instance != null;
    public PanelSettings PanelSettings => document != null ? document.panelSettings : null;
    public UIDocument Document => document;

    public static ForgeLoaderHost Ensure()
    {
        if (instance != null)
            return instance;

        var theme = LoaderTheme.Load();
        var go = new GameObject("[ForgeLoader]");
        go.SetActive(false);
        DontDestroyOnLoad(go);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = theme.panelSettings != null ? theme.panelSettings : RuntimePanelSettings();
        instance = go.AddComponent<ForgeLoaderHost>();
        instance.theme = theme;
        instance.document = doc;
        go.SetActive(true);
        return instance;
    }

    // Sans asset de setup : memes reglages, crees a la volee
    private static PanelSettings RuntimePanelSettings()
    {
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.name = "LoaderPanelSettings (runtime)";
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 1f;
        ps.sortingOrder = FallbackSortingOrder;
        return ps;
    }

    public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

    private void Awake()
    {
        ForgeTimeline.Ease = LoaderTheme.Load().Ease;
        LoadingService.Began += OnBegan;
        LoadingService.Changed += OnChanged;
        LoadingService.Loaded += OnLoaded;
        LoadingService.Ended += OnEnded;
        MatchmakingService.Started += OnSearchStarted;
        MatchmakingService.Changed += OnSearchChanged;
        MatchmakingService.MatchFound += OnMatchFound;
        MatchmakingService.Ended += OnSearchEnded;
    }

    private void OnDestroy()
    {
        LoadingService.Began -= OnBegan;
        LoadingService.Changed -= OnChanged;
        LoadingService.Loaded -= OnLoaded;
        LoadingService.Ended -= OnEnded;
        MatchmakingService.Started -= OnSearchStarted;
        MatchmakingService.Changed -= OnSearchChanged;
        MatchmakingService.MatchFound -= OnMatchFound;
        MatchmakingService.Ended -= OnSearchEnded;
        ToastHost.Hidden = false;
        if (videoTarget != null)
            videoTarget.Release();
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        MatchmakingService.Tick(Time.unscaledDeltaTime);

        if (MatchmakingService.State == MatchmakingState.Searching && IsVisible(stripView?.Root) && CancelPressed())
            MatchmakingService.Cancel();
    }

    private static bool CancelPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            return true;
        var pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }

    // =========================================================
    // CONSTRUCTION (paresseuse : le UIDocument est pret)
    // =========================================================

    private VisualElement Root
    {
        get
        {
            if (root == null)
            {
                root = document.rootVisualElement;
                root.pickingMode = PickingMode.Ignore;
            }
            return root;
        }
    }

    private TemplateContainer Instantiate(string resourcePath)
    {
        var asset = Resources.Load<VisualTreeAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogError($"[ForgeLoader] Template introuvable : Resources/{resourcePath}.uxml");
            return null;
        }
        var container = asset.Instantiate();
        container.pickingMode = PickingMode.Ignore;
        container.style.position = Position.Absolute;
        container.style.left = container.style.top = container.style.right = container.style.bottom = 0;
        container.style.display = DisplayStyle.None;
        Root.Add(container);
        return container;
    }

    private LoaderProgressView SplashView()
    {
        if (splashView == null)
        {
            var c = Instantiate(LoaderTheme.SplashTemplate);
            if (c != null)
                splashView = new LoaderProgressView(LoadContext.Splash, c);
        }
        return splashView;
    }

    private LoaderProgressView ScreenView()
    {
        if (screenView == null)
        {
            var c = Instantiate(LoaderTheme.ScreenLoadTemplate);
            if (c != null)
                screenView = new LoaderProgressView(LoadContext.ScreenLoad, c);
        }
        return screenView;
    }

    private MatchmakingStripView StripView()
    {
        if (stripView == null)
        {
            var c = Instantiate(LoaderTheme.MatchmakingTemplate);
            if (c != null)
            {
                stripView = new MatchmakingStripView(c);
                stripView.CancelClicked += MatchmakingService.Cancel;
            }
        }
        return stripView;
    }

    // =========================================================
    // CHARGEMENTS (splash, chargement d'ecran)
    // =========================================================

    private void OnBegan(LoadingSession session)
    {
        if (session.Context == LoadContext.Silent)
            return;
        var view = session.Context == LoadContext.Splash ? SplashView() : ScreenView();
        if (view == null)
            return;

        if (presentedView != null && presentedView != view)
            Hide(presentedView.Root);
        StopFade(view.Root);

        presented = session;
        presentedView = view;
        view.Reset();
        view.SetProgress(session.Progress, session.Step);
        if (session.Context == LoadContext.Splash)
            view.SetFooter(theme.BuildString(), theme.RandomTip());

        var loader = view.Loader;
        session.SetTransitionGate(() => loader == null || loader.IsLocked);

        bool wasVisible = IsVisible(view.Root);
        Show(view.Root);
        Bring(view.Root);
        // Entree en fondu court (le splash part de l'ecran de connexion, sans coupe)
        if (!wasVisible)
            StartFade(view.Root, 0f, 1f, BlockforgeTheme.MotionPanel, null);
        else
            view.Root.style.opacity = 1f;
    }

    private void OnChanged(LoadingSession session)
    {
        if (session == presented)
            presentedView.SetProgress(session.Progress, session.Step);
    }

    private void OnLoaded(LoadingSession session)
    {
        if (session == presented)
            presentedView.Loader?.Finish(null);
    }

    private void OnEnded(LoadingSession session)
    {
        if (session != presented)
            return;
        var view = presentedView;
        presented = null;
        presentedView = null;
        StartFade(view.Root, view.Root.resolvedStyle.opacity, 0f,
                  BlockforgeTheme.Fade(BlockforgeTheme.MotionScreen), () => Hide(view.Root));
    }

    // =========================================================
    // MATCHMAKING
    // =========================================================

    private void OnSearchStarted()
    {
        var view = StripView();
        if (view == null)
            return;
        stripLeaving = false;
        view.Reset();
        view.SetMode(MatchmakingService.ModeTitle, MatchmakingService.Region);
        view.SetMatchmaking(MatchmakingService.Found, MatchmakingService.Total,
                            MatchmakingService.ElapsedSeconds, MatchmakingService.EstimatedSeconds);
        StartCinematic(view);
        ToastHost.Hidden = true; // rien ne se pose sur la cinematique
        Show(view.Root);
        Bring(view.Root);
        StartFade(view.Root, 0f, 1f, BlockforgeTheme.MotionPanel, null); // coupe un fondu de sortie precedent
    }

    private void OnSearchChanged()
    {
        stripView?.SetMatchmaking(MatchmakingService.Found, MatchmakingService.Total,
                                  MatchmakingService.ElapsedSeconds, MatchmakingService.EstimatedSeconds);
    }

    // Plein : verrouiller le mark, le tenir, retirer le bandeau, puis laisser partir la suite
    private void OnMatchFound()
    {
        var view = stripView;
        if (view == null || view.Loader == null)
        {
            HideStrip();
            MatchmakingService.CompleteTransition();
            return;
        }
        view.Loader.Finish(() =>
        {
            if (MatchmakingService.State != MatchmakingState.Found)
                return; // recherche relancee entre-temps
            stripLeaving = true;
            StartFade(view.Root, view.Root.resolvedStyle.opacity, 0f,
                      BlockforgeTheme.Fade(BlockforgeTheme.MotionScreen), () =>
                      {
                          HideStrip();
                          MatchmakingService.CompleteTransition();
                      });
        });
    }

    private void OnSearchEnded(bool matched)
    {
        if (matched || stripView == null || stripLeaving)
            return;
        // Annule : fondu court
        var view = stripView;
        stripLeaving = true;
        StartFade(view.Root, view.Root.resolvedStyle.opacity, 0f, BlockforgeTheme.MotionPanel, HideStrip);
    }

    private void HideStrip()
    {
        stripLeaving = false;
        if (stripView != null)
            Hide(stripView.Root);
        StopCinematic();
        ToastHost.Hidden = false;
    }

    private void StartCinematic(MatchmakingStripView view)
    {
        var clip = theme.matchmakingCinematic;
        if (view.Cinematic == null)
            return;
        if (clip == null)
        {
            // Pas de cinematique : le bandeau se pose sur l'ecran courant
            view.Cinematic.style.display = DisplayStyle.None;
            view.Cinematic.pickingMode = PickingMode.Ignore;
            return;
        }

        if (video == null)
        {
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = true;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.audioOutputMode = VideoAudioOutputMode.None; // la musique Matchmaking joue deja
        }
        int w = Mathf.Max(16, (int)clip.width), h = Mathf.Max(16, (int)clip.height);
        if (videoTarget == null || videoTarget.width != w || videoTarget.height != h)
        {
            if (videoTarget != null)
                videoTarget.Release();
            videoTarget = new RenderTexture(w, h, 0) { name = "MatchmakingCinematic" };
        }
        video.clip = clip;
        video.targetTexture = videoTarget;
        video.Play();
        view.Cinematic.style.backgroundImage = Background.FromRenderTexture(videoTarget);
        view.Cinematic.style.display = DisplayStyle.Flex;
        view.Cinematic.pickingMode = PickingMode.Position;
    }

    private void StopCinematic()
    {
        if (video != null)
            video.Stop();
    }

    // =========================================================
    // APERCU (scene LoaderPreview) : vues sans session
    // =========================================================

    public LoaderProgressView ShowPreview(LoadContext context)
    {
        var view = context == LoadContext.Splash ? SplashView() : ScreenView();
        if (view == null)
            return null;
        StopFade(view.Root);
        view.Reset();
        if (context == LoadContext.Splash)
            view.SetFooter(theme.BuildString(), theme.RandomTip());
        Show(view.Root);
        Bring(view.Root);
        view.Root.style.opacity = 1f;
        return view;
    }

    public MatchmakingStripView ShowStripPreview(string mode, string region, int total, float estimatedSeconds)
    {
        var view = StripView();
        if (view == null)
            return null;
        StopFade(view.Root);
        view.Reset();
        view.SetMode(mode, region);
        view.SetMatchmaking(1, total, 0f, estimatedSeconds);
        StartCinematic(view);
        Show(view.Root);
        Bring(view.Root);
        view.Root.style.opacity = 1f;
        return view;
    }

    public void HidePreviews()
    {
        if (splashView != null && presentedView != splashView) { StopFade(splashView.Root); Hide(splashView.Root); }
        if (screenView != null && presentedView != screenView) { StopFade(screenView.Root); Hide(screenView.Root); }
        if (stripView != null && !MatchmakingService.IsActive) HideStrip();
    }

    // =========================================================
    // OUTILS
    // =========================================================

    private static bool IsVisible(VisualElement e) => e != null && e.style.display != DisplayStyle.None;

    private static void Show(VisualElement e)
    {
        if (e != null)
            e.style.display = DisplayStyle.Flex;
    }

    private static void Hide(VisualElement e)
    {
        if (e != null)
            e.style.display = DisplayStyle.None;
    }

    private static void Bring(VisualElement e) => e?.BringToFront();

    // Remplace le fondu en cours de cette vue (son callback n'est pas appele)
    private void StartFade(VisualElement e, float from, float to, float seconds, Action done)
    {
        if (e == null)
            return;
        StopFade(e);
        fades[e] = StartCoroutine(Fade(e, from, to, seconds, done));
    }

    private void StopFade(VisualElement e)
    {
        if (e != null && fades.TryGetValue(e, out var routine))
        {
            if (routine != null)
                StopCoroutine(routine);
            fades.Remove(e);
        }
    }

    private IEnumerator Fade(VisualElement e, float from, float to, float seconds, Action done)
    {
        float t = 0f;
        while (t < seconds)
        {
            e.style.opacity = Mathf.Lerp(from, to, ForgeTimeline.Ease(t / seconds));
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        e.style.opacity = to;
        fades.Remove(e);
        done?.Invoke();
    }
}
