using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// TRANSITION « PLAY WITH BOTS » -> ECRAN VERSUS -> MATCH
// (design « Play to Versus », theme hud du design system)
// Un hote unique (DontDestroyOnLoad) au-dessus des menus, qui
// survit au chargement de la scene du mode :
//   0 ms      clic : l'ecran Play s'assombrit a 60 % en 140 ms
//   0-320 ms  deux volets entrent sur une courbe ease-out, bleu
//             depuis la gauche, rouge depuis la droite, chacun
//             coupe a 12 degres (langage des coins du HUD) ; ils
//             se rejoignent au centre a 320 ms
//   320-420   le mark tombe sur la couture (1.15 -> 1)
//   420 ms    les volets se posent dans la mise en page Versus
//             (la couture devient la colonne VS) et les rosters
//             arrivent en cascade de haut en bas, 40 ms d'ecart
// La barre de chargement court des deux bords vers le centre
// pendant tout le chargement ; chaque ligne porte sa propre
// progression (le joueur suit le chargement reel de la scene ;
// les bots sont prets d'emblee : 100 % des l'affichage). Echap / Cancel
// annule tant que la scene n'a pas commence a charger ; ensuite
// le pied de page passe a « Loading… ». Scene prete : fondu de
// sortie en 220 ms. Animations reduites : fondu de 140 ms, pas
// de volets.
// Gauche = toujours l'equipe du joueur (regle du HUD).
// =========================================================

public class MatchParticipant
{
    public string name;
    public string robot;
    public int cpu;
    public bool isBot;
    public bool isSelf;
}

public class MatchIntroData
{
    public string modeTitle = "Team Deathmatch";
    public string mapName = "Hex valley";
    public string summary = "first to 15 · 10:00";
    public string allyTeamName = "Blue team";
    public string enemyTeamName = "Red team";
    public string tip = "A robot loses the blocks it is hit on. Put your weapons where the armour is.";
    // Mode joue (regles du match : TdmMatch les relit dans la scene de la carte)
    public GameModeDefinition mode;
    public readonly List<MatchParticipant> allies = new();
    public readonly List<MatchParticipant> enemies = new();

    private static readonly string[] BotNames =
    {
        "Refticus", "Necro", "serz", "Starshock", "Keegard", "Dima", "Taker", "Zdziefan", "xRyus",
        "Vexa", "Orbit", "Kelvin", "Marrow", "Pyre", "Quill", "Sable",
    };

    // Les robots des bots : ceux que TdmMatch assemble vraiment (BotRobotCatalog)
    private static string[] BotRobots => BotRobotCatalog.Names;

    private static readonly string[] Tips =
    {
        "A robot loses the blocks it is hit on. Put your weapons where the armour is.",
        "Keys 1, 2 and 3 switch between your weapon and module types.",
        "Press R to self-right a robot that landed on its back.",
        "Heavier builds take more hits, lighter builds get there first.",
    };

    // Match contre des bots : le joueur et teamSize - 1 bots en bleu, teamSize bots en rouge
    public static MatchIntroData Bots(GameModeDefinition mode, string playerName, RobotRecord robot)
    {
        var data = new MatchIntroData { mode = mode };
        if (mode != null)
        {
            data.modeTitle = mode.title;
            data.mapName = mode.mapName;
            data.summary = mode.versusSummary;
        }
        var rng = new System.Random();
        data.tip = Tips[rng.Next(Tips.Length)];

        var stats = robot?.stats ?? new RobotStats();
        data.allies.Add(new MatchParticipant
        {
            name = playerName, robot = robot?.name ?? "Robot", cpu = stats.cpu, isSelf = true,
        });

        int teamSize = mode != null ? Mathf.Max(1, mode.teamSize) : 5;
        var names = new List<string>(BotNames);
        var robots = new List<string>(BotRobots);
        int baseCpu = stats.cpu > 0 ? stats.cpu : 1700;
        MatchParticipant Bot()
        {
            int n = rng.Next(names.Count), r = rng.Next(robots.Count);
            var bot = new MatchParticipant
            {
                name = "Bot " + names[n], robot = robots[r], isBot = true,
                // Autour du CPU du joueur (de -25 % a +30 %) : des adversaires de son calibre
                cpu = Mathf.Max(60, Mathf.RoundToInt(baseCpu * (0.75f + 0.55f * (float)rng.NextDouble()))),
            };
            names.RemoveAt(n);
            robots.RemoveAt(r);
            if (names.Count == 0) names.AddRange(BotNames);
            if (robots.Count == 0) robots.AddRange(BotRobots);
            return bot;
        }
        for (int i = 1; i < teamSize; i++) data.allies.Add(Bot());
        for (int i = 0; i < teamSize; i++) data.enemies.Add(Bot());
        return data;
    }
}

public class MatchIntro : MonoBehaviour
{
    public static bool IsRunning => instance != null;
    private static MatchIntro instance;

    // Derniere composition affichee : la scene de la carte la relit (TdmMatch)
    // pour poser les memes participants que l'ecran Versus.
    public static MatchIntroData LastData { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLastData() => LastData = null;

    // Lue une fois par la scene du match (TdmMatch), puis oubliee
    public static MatchIntroData TakeLastData()
    {
        var data = LastData;
        LastData = null;
        return data;
    }

    // ---------- Geometrie (px de reference 1920 x 1080) ----------
    private const float W = 1920f, H = 1080f;
    private const float Slant = 230f;          // tan(12 deg) x 1080
    private const float PlateW = 1075f;        // le volet atteint la couture : 1075 en haut, 845 en bas
    private const float Edge = 80f;            // marge des rosters et du pied de page
    private const float RosterTop = 200f, RosterW = 700f, RowH = 116f, RowGap = 8f;
    private const float Chamfer = 14f;

    // ---------- Durees (s) ----------
    private const float DimTime = 0.14f, WipeTime = 0.32f, MarkTime = 0.10f, SettleTime = 0.22f;
    private const float RowStagger = 0.04f, RowTime = 0.14f;
    private const float CancelWindow = 1.6f;   // apres l'arrivee des rosters, avant le chargement
    private const float MinVersus = 3.2f;      // affichage minimum de l'ecran Versus
    private const float HoldAfterLoad = 0.35f; // premiere image de la scene avant le fondu

    private MatchIntroData data;
    private string sceneName;
    private bool reduced;
    private bool cancelled, loading;
    private float versusShownAt;

    // Elements animes
    private CanvasGroup rootGroup, versusGroup, footerGroup;
    private Image dim;
    private HudPolygon backdrop, plateBlue, plateRed, edgeBlue, edgeRed, seam;
    private RectTransform plateBlueRect, plateRedRect, markRect, topPlate;
    private CanvasGroup markGroup, wordBlue, wordRed, topGroup, vsGroup;
    private readonly List<CanvasGroup> cascade = new();   // en-tetes puis lignes, ordre d'apparition
    private readonly List<RectTransform> cascadeRects = new();
    private HudPolygon barLeft, barRight;
    private RectTransform barTrack;
    private GameObject cancelButton;
    private TMP_Text footerStatus;

    private class RowView
    {
        public MatchParticipant p;
        public RectTransform fill;
        public TMP_Text pct;
        public float load;           // 0..1
        public float startAt;        // temps (depuis le debut) ou la ligne commence a charger
        public bool ready;
    }
    private readonly List<RowView> rows = new();

    // =========================================================
    // API
    // =========================================================

    public static void Begin(MatchIntroData data, string sceneName)
    {
        if (instance != null)
            return;
        LastData = data;
        var go = new GameObject("[MatchIntro]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MatchIntro>();
        instance.data = data;
        instance.sceneName = sceneName;
        instance.reduced = BlockforgeTheme.ReduceMotion;
        instance.Build();
        instance.StartCoroutine(instance.Run());
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            Cancel();
    }

    private void Cancel()
    {
        if (cancelled || loading || versusShownAt <= 0f)
            return;
        cancelled = true;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndDestroy(BlockforgeTheme.Duration(BlockforgeTheme.MotionPanel)));
    }

    // =========================================================
    // DEROULE
    // =========================================================

    private IEnumerator Run()
    {
        float t0 = Time.unscaledTime;
        float Now() => Time.unscaledTime - t0;

        if (reduced)
        {
            // Pas de volets : l'ecran Versus arrive en fondu de 140 ms, deja en place
            SetSettled(1f);
            SetCascade(1f);
            rootGroup.alpha = 0f;
            yield return Fade(rootGroup, 1f, BlockforgeTheme.Duration(BlockforgeTheme.MotionPanel));
            versusShownAt = Now();
        }
        else
        {
            // 0 -> 320 ms : voile et volets
            while (Now() < WipeTime)
            {
                float t = Now();
                dim.color = WithAlpha(dim.color, 0.6f * Ease(t / DimTime));
                float k = Ease(t / WipeTime);
                plateBlueRect.anchoredPosition = new Vector2(-PlateW * (1f - k), 0f);
                plateRedRect.anchoredPosition = new Vector2(PlateW * (1f - k), 0f);
                yield return null;
            }
            dim.color = WithAlpha(dim.color, 0.6f);
            plateBlueRect.anchoredPosition = plateRedRect.anchoredPosition = Vector2.zero;

            // 320 -> 420 ms : le mark tombe sur la couture
            while (Now() < WipeTime + MarkTime)
            {
                float k = Ease((Now() - WipeTime) / MarkTime);
                markGroup.alpha = k;
                markRect.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
                yield return null;
            }
            markGroup.alpha = 1f;
            markRect.localScale = Vector3.one;

            // 420 ms : les volets se posent, les rosters arrivent en cascade
            versusShownAt = Now();
            int lastRank = 0;
            foreach (var o in cascadeOrder) lastRank = Mathf.Max(lastRank, o);
            float cascadeEnd = SettleTime + (lastRank + 1) * RowStagger + RowTime;
            while (Now() - versusShownAt < cascadeEnd)
            {
                float t = Now() - versusShownAt;
                SetSettled(Ease(t / SettleTime));
                SetCascade(t);
                yield return null;
            }
            SetSettled(1f);
            SetCascade(1e9f);
        }

        // Les bots se connectent des que leur ligne est la ; le joueur attend la scene
        foreach (var row in rows)
            row.startAt = versusShownAt;

        // Fenetre d'annulation, puis chargement reel
        while (Now() - versusShownAt < CancelWindow)
        {
            TickLoading(Now(), null);
            yield return null;
        }

        loading = true;
        if (cancelButton != null)
            cancelButton.SetActive(false);
        if (footerStatus != null)
            footerStatus.text = "Loading " + data.mapName + "…";

        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            // Scene absente du build : on rend la main au menu
            Toast.Error($"Couldn't load '{sceneName}'. Is the scene in the build settings?");
            yield return FadeOutAndDestroy(BlockforgeTheme.Duration(BlockforgeTheme.MotionPanel));
            yield break;
        }
        op.allowSceneActivation = false;

        while (true)
        {
            TickLoading(Now(), op);
            bool botsReady = true;
            foreach (var row in rows)
                if (!row.p.isSelf && !row.ready) { botsReady = false; break; }
            if (op.progress >= 0.9f && botsReady && Now() - versusShownAt >= MinVersus)
                break;
            yield return null;
        }

        // Tout le monde est pret : la scene prend la main sous l'ecran Versus
        foreach (var row in rows)
            SetRowLoad(row, 1f);
        SetBars(1f, 1f);
        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;
        yield return new WaitForSecondsRealtime(HoldAfterLoad);

        yield return FadeOutAndDestroy(BlockforgeTheme.Duration(BlockforgeTheme.MotionScreen));
    }

    private void TickLoading(float now, AsyncOperation op)
    {
        float allySum = 0f, enemySum = 0f;
        int allyN = 0, enemyN = 0;
        foreach (var row in rows)
        {
            float load;
            if (row.p.isSelf)
                load = op == null ? 0f : Mathf.Clamp01(op.progress / 0.9f);
            else
                load = 1f; // bots : rien a charger, prets des le clic sur Play with bots
            SetRowLoad(row, load);
            bool ally = data.allies.Contains(row.p);
            if (ally) { allySum += load; allyN++; } else { enemySum += load; enemyN++; }
        }
        SetBars(allyN > 0 ? allySum / allyN : 0f, enemyN > 0 ? enemySum / enemyN : 0f);
    }

    private void SetRowLoad(RowView row, float load)
    {
        row.load = load;
        row.ready = load >= 1f;
        if (row.fill != null)
            row.fill.anchorMax = new Vector2(load, 1f);
        if (row.pct != null)
        {
            row.pct.text = row.ready ? "Ready" : Mathf.FloorToInt(load * 100f) + "%";
            row.pct.color = row.ready ? BlockforgeTheme.Hud.Success : BlockforgeTheme.Hud.InkSecondary;
        }
    }

    private void SetBars(float ally, float enemy)
    {
        // Chaque moitie court de son bord vers le centre
        if (barLeft != null)
            barLeft.rectTransform.anchorMax = new Vector2(0.5f * ally, 1f);
        if (barRight != null)
            barRight.rectTransform.anchorMin = new Vector2(1f - 0.5f * enemy, 0f);
    }

    // Etat « pose » des volets : 0 = volets de transition, 1 = sols de l'ecran Versus
    private void SetSettled(float k)
    {
        backdrop.color = WithAlpha(Color.white, k);
        plateBlue.SetColors(Color.Lerp(Hex(0x0F2140), Hex(0x122440), k), Color.Lerp(Hex(0x1B3660), Hex(0x0F1D33, 0f), k));
        plateRed.SetColors(Color.Lerp(Hex(0x5A2A21), Hex(0x2D1612, 0f), k), Color.Lerp(Hex(0x3A1A15), Hex(0x3A1A15), k));
        edgeBlue.color = edgeRed.color = WithAlpha(Color.white, 1f - k);
        wordBlue.alpha = wordRed.alpha = 0.9f * (1f - k);
        markGroup.alpha = 1f - k;
        markRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, k);
        seam.color = WithAlpha(Color.white, 0.9f * k);
        vsGroup.alpha = k;
        topGroup.alpha = k;
        topPlate.anchoredPosition = new Vector2(0f, -40f + 12f * (1f - k));
        versusGroup.alpha = 1f;
        dim.color = WithAlpha(dim.color, 0.6f * (1f - k));
    }

    // Cascade des rosters : t en secondes depuis la pose ; chaque element entre 40 ms apres le precedent
    private void SetCascade(float t)
    {
        int last = 0;
        foreach (var o in cascadeOrder) last = Mathf.Max(last, o);
        for (int i = 0; i < cascade.Count; i++)
        {
            float local = (t - SettleTime - cascadeOrder[i] * RowStagger) / RowTime;
            float k = Ease(local);
            cascade[i].alpha = k;
            var rt = cascadeRects[i];
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY[i] + 12f * (1f - k));
        }
        footerGroup.alpha = Ease((t - SettleTime - (last + 1) * RowStagger) / RowTime);
    }
    private readonly List<float> baseY = new();
    private readonly List<int> cascadeOrder = new();

    private IEnumerator FadeOutAndDestroy(float duration)
    {
        yield return Fade(rootGroup, 0f, duration);
        Destroy(gameObject);
    }

    private static IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        float from = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = target;
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, target, Ease(t / duration));
            yield return null;
        }
        group.alpha = target;
    }

    private static float Ease(float t) => BlockforgeTheme.EaseOut(Mathf.Clamp01(t));

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950; // au-dessus des menus et du HUD, sous les notices (1000)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(W, H);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        rootGroup = gameObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 1f;
        var root = (RectTransform)transform;

        // Voile sur l'ecran Play (bloque aussi les clics dessous)
        dim = NewImage(root, "Dim");
        Stretch(dim.rectTransform);
        dim.color = WithAlpha(Hex(0x0B0E14), 0f);
        dim.raycastTarget = true;

        // Fond de l'ecran Versus (apparait a la pose)
        backdrop = NewPolygon(root, "Backdrop");
        Stretch(backdrop.rectTransform);
        backdrop.vertical = true;
        backdrop.SetColors(Hex(0x141A24), Hex(0x0C1017));
        backdrop.color = WithAlpha(Color.white, 0f);

        // Volets : bleu depuis la gauche, rouge depuis la droite, bord a 12 degres
        plateBlue = NewPolygon(root, "PlateBlue");
        plateBlueRect = plateBlue.rectTransform;
        plateBlueRect.anchorMin = new Vector2(0f, 0f);
        plateBlueRect.anchorMax = new Vector2(0f, 1f);
        plateBlueRect.pivot = new Vector2(0f, 0.5f);
        plateBlueRect.sizeDelta = new Vector2(PlateW, 0f);
        plateBlueRect.anchoredPosition = new Vector2(reduced ? 0f : -PlateW, 0f);
        plateBlue.slantRight = Slant;

        edgeBlue = NewPolygon(plateBlueRect, "Edge");
        var eb = edgeBlue.rectTransform;
        eb.anchorMin = new Vector2(1f, 0f);
        eb.anchorMax = new Vector2(1f, 1f);
        eb.pivot = new Vector2(1f, 0.5f);
        eb.sizeDelta = new Vector2(Slant + 14f, 0f);
        eb.anchoredPosition = Vector2.zero;
        edgeBlue.slantLeft = edgeBlue.slantRight = Slant;
        edgeBlue.SetSolid(BlockforgeTheme.Hud.TeamAlly);

        wordBlue = TeamWord(plateBlueRect, "Blue", BlockforgeTheme.Hud.TeamAlly, false);

        plateRed = NewPolygon(root, "PlateRed");
        plateRedRect = plateRed.rectTransform;
        plateRedRect.anchorMin = new Vector2(1f, 0f);
        plateRedRect.anchorMax = new Vector2(1f, 1f);
        plateRedRect.pivot = new Vector2(1f, 0.5f);
        plateRedRect.sizeDelta = new Vector2(PlateW, 0f);
        plateRedRect.anchoredPosition = new Vector2(reduced ? 0f : PlateW, 0f);
        plateRed.slantLeft = Slant;

        edgeRed = NewPolygon(plateRedRect, "Edge");
        var er = edgeRed.rectTransform;
        er.anchorMin = new Vector2(0f, 0f);
        er.anchorMax = new Vector2(0f, 1f);
        er.pivot = new Vector2(0f, 0.5f);
        er.sizeDelta = new Vector2(Slant + 14f, 0f);
        er.anchoredPosition = Vector2.zero;
        edgeRed.slantLeft = edgeRed.slantRight = Slant;
        edgeRed.SetSolid(BlockforgeTheme.Hud.TeamEnemy);

        wordRed = TeamWord(plateRedRect, "Red", BlockforgeTheme.Hud.TeamEnemy, true);

        // Couture inclinee au centre (bleu -> blanc -> rouge)
        seam = NewPolygon(root, "Seam");
        var sr = seam.rectTransform;
        sr.anchorMin = new Vector2(0.5f, 0f);
        sr.anchorMax = new Vector2(0.5f, 1f);
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.sizeDelta = new Vector2(Slant + 6f, 0f);
        sr.anchoredPosition = Vector2.zero;
        seam.slantLeft = seam.slantRight = Slant;
        seam.vertical = true;
        seam.SetColors(BlockforgeTheme.Hud.TeamAlly, BlockforgeTheme.Hud.TeamEnemy);
        // Mark sur la couture (phase 320-420 ms)
        var mark = new GameObject("Mark", typeof(RectTransform));
        mark.transform.SetParent(root, false);
        markRect = (RectTransform)mark.transform;
        markRect.anchorMin = markRect.anchorMax = markRect.pivot = new Vector2(0.5f, 0.5f);
        markRect.sizeDelta = new Vector2(400f, 220f);
        markGroup = mark.AddComponent<CanvasGroup>();
        markGroup.alpha = 0f;
        var markImage = NewImage(markRect, "Logo");
        markImage.sprite = DSAssets.Logo("mark");
        markImage.preserveAspect = true;
        markImage.color = Color.white;
        var mir = markImage.rectTransform;
        mir.anchorMin = mir.anchorMax = new Vector2(0.5f, 1f);
        mir.pivot = new Vector2(0.5f, 1f);
        mir.sizeDelta = new Vector2(150f, 150f);
        mir.anchoredPosition = new Vector2(0f, -10f);
        var entering = HudText(markRect, "Entering", DSTextStyle.Label, BlockforgeTheme.Hud.InkPrimary, "Entering match", 14f, true);
        entering.characterSpacing = 18f;
        var enr = entering.rectTransform;
        enr.anchorMin = new Vector2(0f, 0f);
        enr.anchorMax = new Vector2(1f, 0f);
        enr.pivot = new Vector2(0.5f, 0f);
        enr.sizeDelta = new Vector2(0f, 20f);
        enr.anchoredPosition = new Vector2(0f, 14f);
        entering.alignment = TextAlignmentOptions.Center;

        // ---------- Ecran Versus ----------
        var versus = new GameObject("Versus", typeof(RectTransform));
        versus.transform.SetParent(root, false);
        Stretch((RectTransform)versus.transform);
        versusGroup = versus.AddComponent<CanvasGroup>();
        var vroot = (RectTransform)versus.transform;

        BuildTopPlate(vroot);
        BuildVs(vroot);
        BuildRoster(vroot, data.allies, data.allyTeamName, "Your team", false);
        BuildRoster(vroot, data.enemies, data.enemyTeamName, "Bots", true);
        BuildFooter(vroot);

        // Etat initial : rien de l'ecran Versus n'est encore visible
        SetSettled(0f);
        SetCascade(-10f);
        dim.color = WithAlpha(dim.color, 0f);
        markGroup.alpha = 0f; // le mark n'arrive qu'a 320 ms
    }

    private void BuildTopPlate(RectTransform parent)
    {
        var plate = NewPolygon(parent, "TopPlate");
        topPlate = plate.rectTransform;
        topPlate.anchorMin = topPlate.anchorMax = new Vector2(0.5f, 1f);
        topPlate.pivot = new Vector2(0.5f, 1f);
        topPlate.anchoredPosition = new Vector2(0f, -40f);
        plate.cutTopLeft = plate.cutTopRight = Chamfer;
        plate.SetSolid(BlockforgeTheme.Hud.SurfaceHud);
        topGroup = plate.gameObject.AddComponent<CanvasGroup>();
        var layout = plate.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 16, 18);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = plate.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = HudText(topPlate, "Mode", DSTextStyle.DisplayLG, BlockforgeTheme.Hud.InkPrimary, data.modeTitle, 40f, true);
        title.alignment = TextAlignmentOptions.Center;
        title.characterSpacing = 3f;

        var line = new GameObject("Summary", typeof(RectTransform));
        line.transform.SetParent(topPlate, false);
        var hl = line.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10f;
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        var icon = NewImage((RectTransform)line.transform, "Icon");
        icon.sprite = DSAssets.Icon("objective");
        icon.preserveAspect = true;
        icon.color = BlockforgeTheme.Hud.InkSecondary;
        var ile = icon.gameObject.AddComponent<LayoutElement>();
        ile.preferredWidth = ile.preferredHeight = ile.minWidth = ile.minHeight = 14f;
        var sub = HudText((RectTransform)line.transform, "Text", DSTextStyle.TitleSM, BlockforgeTheme.Hud.InkSecondary,
                          $"{data.mapName} · {data.summary}", 14f, false);
        var medium = DSAssets.Font(DSFont.Medium);
        if (medium != null) { sub.font = medium; sub.fontSharedMaterial = medium.material; }
    }

    private void BuildVs(RectTransform parent)
    {
        var vs = new GameObject("VS", typeof(RectTransform));
        vs.transform.SetParent(parent, false);
        var rt = (RectTransform)vs.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(400f, 240f);
        vsGroup = vs.AddComponent<CanvasGroup>();
        var text = HudText(rt, "Text", DSTextStyle.DisplayXL, BlockforgeTheme.Hud.InkPrimary, "VS", 220f, true);
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 2f;
        Stretch(text.rectTransform);
    }

    private void BuildRoster(RectTransform parent, List<MatchParticipant> team, string teamName, string tag, bool enemy)
    {
        var teamColor = enemy ? BlockforgeTheme.Hud.TeamEnemy : BlockforgeTheme.Hud.TeamAlly;
        var section = new GameObject(enemy ? "RedTeam" : "BlueTeam", typeof(RectTransform));
        section.transform.SetParent(parent, false);
        var srt = (RectTransform)section.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(enemy ? 1f : 0f, 1f);
        srt.pivot = new Vector2(enemy ? 1f : 0f, 1f);
        srt.sizeDelta = new Vector2(RosterW, 0f);
        srt.anchoredPosition = new Vector2(enemy ? -Edge : Edge, -RosterTop);

        // En-tete : silhouette, nom de l'equipe, etiquette ; filet de 2 px couleur de l'equipe
        var header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(srt, false);
        var hrt = (RectTransform)header.transform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, 68f);
        hrt.anchoredPosition = Vector2.zero;
        var hg = header.AddComponent<CanvasGroup>();
        Cascade(hg, hrt, 0);

        var rule = NewImage(hrt, "Rule");
        rule.color = teamColor;
        var rr = rule.rectTransform;
        rr.anchorMin = new Vector2(0f, 0f);
        rr.anchorMax = new Vector2(1f, 0f);
        rr.pivot = new Vector2(0.5f, 0f);
        rr.sizeDelta = new Vector2(0f, 2f);

        var mark = NewImage(hrt, "Mark");
        mark.sprite = DSAssets.Icon(enemy ? "enemy" : "ally");
        mark.preserveAspect = true;
        mark.color = teamColor;
        var mr = mark.rectTransform;
        mr.anchorMin = mr.anchorMax = new Vector2(enemy ? 1f : 0f, 0.5f);
        mr.pivot = new Vector2(enemy ? 1f : 0f, 0.5f);
        mr.sizeDelta = new Vector2(24f, 24f);
        mr.anchoredPosition = new Vector2(enemy ? -8f : 8f, 4f);

        var title = HudText(hrt, "Title", DSTextStyle.DisplayLG, teamColor, teamName, 56f, true);
        title.alignment = enemy ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        title.characterSpacing = 3f;
        var tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(enemy ? 120f : 56f, 8f);
        tr.offsetMax = new Vector2(enemy ? -56f : -120f, 0f);

        var tagText = HudText(hrt, "Tag", DSTextStyle.Label, BlockforgeTheme.Hud.InkSecondary, tag, 13f, true);
        tagText.characterSpacing = 8f;
        tagText.alignment = enemy ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
        var tgr = tagText.rectTransform;
        tgr.anchorMin = new Vector2(0f, 0f);
        tgr.anchorMax = new Vector2(1f, 1f);
        tgr.offsetMin = new Vector2(0f, 8f);
        tgr.offsetMax = new Vector2(0f, 0f);

        // Lignes
        float y = -(68f + 14f);
        for (int i = 0; i < team.Count; i++)
        {
            var row = BuildRow(srt, team[i], enemy, y, i + 1);
            rows.Add(row);
            y -= RowH + RowGap;
        }
    }

    private RowView BuildRow(RectTransform parent, MatchParticipant p, bool enemy, float y, int order)
    {
        var teamColor = enemy ? BlockforgeTheme.Hud.TeamEnemy : BlockforgeTheme.Hud.TeamAlly;
        var plateTint = p.isSelf ? BlockforgeTheme.Hud.PlateSelf : enemy ? BlockforgeTheme.Hud.PlateEnemy : BlockforgeTheme.Hud.PlateAlly;
        var accent = p.isSelf ? BlockforgeTheme.Hud.Accent : teamColor;

        var row = new GameObject(p.name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, RowH);
        rt.anchoredPosition = new Vector2(0f, y);
        var group = row.AddComponent<CanvasGroup>();
        Cascade(group, rt, order);

        // Plaque translucide, coin exterieur coupe, teinte de l'equipe qui s'efface vers le centre
        var basePlate = NewPolygon(rt, "Plate");
        Stretch(basePlate.rectTransform);
        if (enemy) basePlate.cutTopRight = Chamfer; else basePlate.cutTopLeft = Chamfer;
        basePlate.SetSolid(BlockforgeTheme.Hud.SurfaceHud);
        var tint = NewPolygon(rt, "Tint");
        Stretch(tint.rectTransform);
        if (enemy) tint.cutTopRight = Chamfer; else tint.cutTopLeft = Chamfer;
        var clear = WithAlpha(plateTint, 0f);
        if (p.isSelf) tint.SetSolid(plateTint);
        else tint.SetColors(enemy ? clear : plateTint, enemy ? plateTint : clear);

        // Vignette : cadre 2 px couleur de l'equipe (accent pour le joueur), fond surface-base
        float x = 20f;
        var frame = NewImage(rt, "Avatar");
        frame.sprite = DSAssets.Shape(DSRadius.Sm, "fill");
        frame.type = Image.Type.Sliced;
        frame.color = accent;
        var fr = frame.rectTransform;
        fr.anchorMin = fr.anchorMax = new Vector2(enemy ? 1f : 0f, 0.5f);
        fr.pivot = new Vector2(enemy ? 1f : 0f, 0.5f);
        fr.sizeDelta = new Vector2(80f, 80f);
        fr.anchoredPosition = new Vector2(enemy ? -x : x, 0f);
        var inner = NewImage(fr, "Inner");
        inner.sprite = frame.sprite;
        inner.type = Image.Type.Sliced;
        inner.color = BlockforgeTheme.Hud.SurfaceBase;
        Stretch(inner.rectTransform);
        inner.rectTransform.offsetMin = new Vector2(2f, 2f);
        inner.rectTransform.offsetMax = new Vector2(-2f, -2f);
        var face = NewImage(fr, "Icon");
        face.sprite = DSAssets.Icon("user");
        face.preserveAspect = true;
        face.color = p.isSelf ? BlockforgeTheme.Hud.Accent : BlockforgeTheme.Hud.InkSecondary;
        var fir = face.rectTransform;
        fir.anchorMin = fir.anchorMax = fir.pivot = new Vector2(0.5f, 0.5f);
        fir.sizeDelta = new Vector2(36f, 36f);

        // Colonne texte : nom, robot, progression
        float textLeft = x + 80f + 20f;
        float rightCol = 24f + 110f + 20f + (p.isBot ? 44f + 12f : 0f);
        var column = new GameObject("Text", typeof(RectTransform));
        column.transform.SetParent(rt, false);
        var crt = (RectTransform)column.transform;
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = new Vector2(enemy ? rightCol : textLeft, 0f);
        crt.offsetMax = new Vector2(enemy ? -textLeft : -rightCol, 0f);

        var name = HudText(crt, "Name", DSTextStyle.TitleLG, p.isSelf ? BlockforgeTheme.Hud.Accent : BlockforgeTheme.Hud.InkPrimary, p.name, 22f, true);
        name.alignment = enemy ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        var nr = name.rectTransform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.sizeDelta = new Vector2(0f, 26f);
        nr.anchoredPosition = new Vector2(0f, -22f);

        var robot = HudText(crt, "Robot", DSTextStyle.TitleSM, BlockforgeTheme.Hud.InkSecondary, p.robot, 14f, false);
        var regular = DSAssets.Font(DSFont.Regular);
        if (regular != null) { robot.font = regular; robot.fontSharedMaterial = regular.material; }
        robot.alignment = enemy ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        var rr = robot.rectTransform;
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, 18f);
        rr.anchoredPosition = new Vector2(0f, -22f - 26f - 3f);

        // Progression : piste de 6 px, remplissage couleur de l'equipe, pourcentage en mono
        var track = NewPolygon(crt, "Track");
        var tkr = track.rectTransform;
        tkr.anchorMin = new Vector2(0f, 1f);
        tkr.anchorMax = new Vector2(1f, 1f);
        tkr.pivot = new Vector2(0.5f, 1f);
        tkr.sizeDelta = new Vector2(0f, 6f);
        tkr.anchoredPosition = new Vector2(0f, -22f - 26f - 3f - 18f - 10f);
        tkr.offsetMin = new Vector2(enemy ? 54f : 0f, tkr.offsetMin.y);
        tkr.offsetMax = new Vector2(enemy ? 0f : -54f, tkr.offsetMax.y);
        if (enemy) track.cutTopLeft = 4f; else track.cutBottomRight = 4f;
        track.SetSolid(BlockforgeTheme.Hud.SurfaceBase);
        var fill = NewImage(tkr, "Fill");
        fill.color = accent;
        var flr = fill.rectTransform;
        flr.anchorMin = new Vector2(enemy ? 1f : 0f, 0f);
        flr.anchorMax = new Vector2(enemy ? 1f : 0f, 1f);
        flr.offsetMin = flr.offsetMax = Vector2.zero;

        var pct = HudText(crt, "Pct", DSTextStyle.ReadoutMD, BlockforgeTheme.Hud.InkSecondary, "0%", 13f, false);
        pct.alignment = enemy ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
        var pr = pct.rectTransform;
        pr.anchorMin = new Vector2(enemy ? 0f : 1f, 1f);
        pr.anchorMax = new Vector2(enemy ? 0f : 1f, 1f);
        pr.pivot = new Vector2(enemy ? 0f : 1f, 1f);
        pr.sizeDelta = new Vector2(44f, 16f);
        pr.anchoredPosition = new Vector2(0f, -22f - 26f - 3f - 18f - 5f);

        // Chiffre CPU a l'exterieur, puce Bot entre le texte et le chiffre
        var cpu = HudText(rt, "Cpu", DSTextStyle.ReadoutMD, BlockforgeTheme.Hud.InkSecondary, BlockforgeTheme.Qty(p.cpu, "CPU"), 18f, false);
        cpu.alignment = enemy ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
        var cr = cpu.rectTransform;
        cr.anchorMin = new Vector2(enemy ? 0f : 1f, 0f);
        cr.anchorMax = new Vector2(enemy ? 0f : 1f, 1f);
        cr.pivot = new Vector2(enemy ? 0f : 1f, 0.5f);
        cr.sizeDelta = new Vector2(110f, 0f);
        cr.anchoredPosition = new Vector2(enemy ? 24f : -24f, 0f);

        if (p.isBot)
        {
            var chip = NewImage(rt, "BotChip");
            chip.sprite = DSAssets.Shape(DSRadius.Pill, "fill");
            chip.type = Image.Type.Sliced;
            chip.color = BlockforgeTheme.Hud.SurfaceInset;
            var chr = chip.rectTransform;
            chr.anchorMin = chr.anchorMax = new Vector2(enemy ? 0f : 1f, 0.5f);
            chr.pivot = new Vector2(enemy ? 0f : 1f, 0.5f);
            chr.sizeDelta = new Vector2(44f, 20f);
            chr.anchoredPosition = new Vector2(enemy ? 24f + 110f + 20f : -(24f + 110f + 20f), 0f);
            var chipText = HudText(chr, "Label", DSTextStyle.Label, BlockforgeTheme.Hud.InkMuted, "Bot", 10f, false);
            chipText.alignment = TextAlignmentOptions.Center;
            chipText.characterSpacing = 10f;
            Stretch(chipText.rectTransform);
        }

        var view = new RowView { p = p, fill = flr, pct = pct };
        SetRowLoad(view, p.isBot ? 1f : 0f);
        return view;
    }

    private void BuildFooter(RectTransform parent)
    {
        var footer = new GameObject("Footer", typeof(RectTransform));
        footer.transform.SetParent(parent, false);
        var frt = (RectTransform)footer.transform;
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(1f, 0f);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.offsetMin = new Vector2(Edge, 56f);
        frt.offsetMax = new Vector2(-Edge, 56f + 10f + 14f + 40f);
        footerGroup = footer.AddComponent<CanvasGroup>();

        // Astuce a gauche
        var tipRow = new GameObject("Tip", typeof(RectTransform));
        tipRow.transform.SetParent(frt, false);
        var trt = (RectTransform)tipRow.transform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(0f, 1f);
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = Vector2.zero;
        var tl = tipRow.AddComponent<HorizontalLayoutGroup>();
        tl.spacing = 10f;
        tl.childAlignment = TextAnchor.MiddleLeft;
        tl.childControlWidth = tl.childControlHeight = true;
        tl.childForceExpandWidth = tl.childForceExpandHeight = false;
        var tf = tipRow.AddComponent<ContentSizeFitter>();
        tf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        tf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var info = NewImage(trt, "Icon");
        info.sprite = DSAssets.Icon("info");
        info.preserveAspect = true;
        info.color = BlockforgeTheme.Hud.InkSecondary;
        var ile = info.gameObject.AddComponent<LayoutElement>();
        ile.preferredWidth = ile.preferredHeight = ile.minWidth = ile.minHeight = 14f;
        var tip = HudText(trt, "Text", DSTextStyle.TitleSM, BlockforgeTheme.Hud.InkSecondary, "Tip: " + data.tip, 14f, false);
        var medium = DSAssets.Font(DSFont.Medium);
        if (medium != null) { tip.font = medium; tip.fontSharedMaterial = medium.material; }
        tip.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        tip.alignment = TextAlignmentOptions.MidlineLeft;

        // Cancel a droite (Esc), remplace par l'etat du chargement une fois la scene lancee
        cancelButton = new GameObject("Cancel", typeof(RectTransform), typeof(Image));
        cancelButton.transform.SetParent(frt, false);
        var cbr = (RectTransform)cancelButton.transform;
        cbr.anchorMin = cbr.anchorMax = new Vector2(1f, 1f);
        cbr.pivot = new Vector2(1f, 1f);
        cbr.anchoredPosition = Vector2.zero;
        cbr.sizeDelta = new Vector2(150f, 40f);
        var cl = cancelButton.AddComponent<HorizontalLayoutGroup>();
        cl.padding = new RectOffset(16, 16, 0, 0);
        cl.spacing = 10f;
        cl.childAlignment = TextAnchor.MiddleCenter;
        cl.childControlWidth = cl.childControlHeight = true;
        cl.childForceExpandWidth = cl.childForceExpandHeight = false;
        var cf = cancelButton.AddComponent<ContentSizeFitter>();
        cf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var cancel = cancelButton.AddComponent<Button>();
        cancel.transition = Selectable.Transition.None;
        cancel.onClick.AddListener(Cancel);
        var cap = new GameObject("Key", typeof(RectTransform), typeof(Image));
        cap.transform.SetParent(cbr, false);
        var capLayout = cap.AddComponent<HorizontalLayoutGroup>();
        capLayout.padding = new RectOffset(6, 6, 2, 2);
        capLayout.childAlignment = TextAnchor.MiddleCenter;
        capLayout.childControlWidth = capLayout.childControlHeight = true;
        var capLe = cap.AddComponent<LayoutElement>();
        capLe.minHeight = 22f;
        capLe.minWidth = 22f;
        var capText = PlainText(cap.transform, "Label", DSTextStyle.Key, BlockforgeTheme.Hud.InkPrimary, "Esc");
        capText.alignment = TextAlignmentOptions.Center;
        Element(cap, DSRole.KeyCap, capText);
        var cancelText = PlainText(cbr, "Label", DSTextStyle.ButtonMD, BlockforgeTheme.Hud.InkSecondary, "Cancel");
        cancelText.characterSpacing = 6f;
        cancelText.alignment = TextAlignmentOptions.Center;
        Element(cancelButton, DSRole.ButtonGhost, cancelText);

        footerStatus = HudText(frt, "Status", DSTextStyle.TitleSM, BlockforgeTheme.Hud.InkSecondary, "", 14f, false);
        footerStatus.alignment = TextAlignmentOptions.MidlineRight;
        var fsr = footerStatus.rectTransform;
        fsr.anchorMin = new Vector2(1f, 1f);
        fsr.anchorMax = new Vector2(1f, 1f);
        fsr.pivot = new Vector2(1f, 1f);
        fsr.sizeDelta = new Vector2(420f, 40f);
        fsr.anchoredPosition = Vector2.zero;

        // Barre : piste en trapeze, remplissages des deux bords vers le centre
        var track = NewPolygon(frt, "Bar");
        barTrack = track.rectTransform;
        barTrack.anchorMin = new Vector2(0f, 0f);
        barTrack.anchorMax = new Vector2(1f, 0f);
        barTrack.pivot = new Vector2(0.5f, 0f);
        barTrack.sizeDelta = new Vector2(0f, 10f);
        barTrack.anchoredPosition = Vector2.zero;
        track.cutBottomLeft = track.cutBottomRight = 10f;
        track.SetSolid(BlockforgeTheme.Hud.SurfaceHud);

        barLeft = NewPolygon(barTrack, "Ally");
        var blr = barLeft.rectTransform;
        blr.anchorMin = new Vector2(0f, 0f);
        blr.anchorMax = new Vector2(0f, 1f);
        blr.offsetMin = blr.offsetMax = Vector2.zero;
        barLeft.cutBottomLeft = 10f;
        barLeft.SetSolid(BlockforgeTheme.Hud.TeamAlly);

        barRight = NewPolygon(barTrack, "Enemy");
        var brr = barRight.rectTransform;
        brr.anchorMin = new Vector2(1f, 0f);
        brr.anchorMax = new Vector2(1f, 1f);
        brr.offsetMin = brr.offsetMax = Vector2.zero;
        barRight.cutBottomRight = 10f;
        barRight.SetSolid(BlockforgeTheme.Hud.TeamEnemy);
    }

    // ---------- Petits outils ----------

    // Les deux rosters arrivent ensemble : en-tetes d'abord (rang 0), puis les lignes (rang 1..n)
    private void Cascade(CanvasGroup group, RectTransform rect, int order)
    {
        cascade.Add(group);
        cascadeRects.Add(rect);
        cascadeOrder.Add(order);
        baseY.Add(rect.anchoredPosition.y);
        group.alpha = 0f;
    }

    private CanvasGroup TeamWord(RectTransform plate, string word, Color color, bool right)
    {
        var text = HudText(plate, "Word", DSTextStyle.DisplayXL, color, word, 160f, false);
        text.alignment = right ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        text.characterSpacing = 3f;
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(right ? 1f : 0f, 1f);
        rt.pivot = new Vector2(right ? 1f : 0f, 1f);
        rt.sizeDelta = new Vector2(700f, 180f);
        rt.anchoredPosition = new Vector2(right ? -96f : 96f, -440f);
        var group = text.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0.9f;
        return group;
    }

    private static Image NewImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static HudPolygon NewPolygon(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(HudPolygon));
        go.transform.SetParent(parent, false);
        var poly = go.GetComponent<HudPolygon>();
        poly.raycastTarget = false;
        return poly;
    }

    // Texte du HUD : style du DS, taille surchargee, ombre portee (shadow-hud : 0 1px 2px
    // noir 85 %) rendue par l'underlay du shader TMP sur une instance du materiau
    private static TextMeshProUGUI HudText(Transform parent, string name, DSTextStyle style, Color color, string value, float size, bool shadow)
    {
        var text = PlainText(parent, name, style, color, value);
        text.fontSize = size;
        if (shadow)
        {
            var mat = text.fontMaterial; // instance propre a ce texte
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
        }
        return text;
    }

    private static TextMeshProUGUI PlainText(Transform parent, string name, DSTextStyle style, Color color, string value)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        DSText.Apply(text, style, color);
        text.text = value;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static DSElement Element(GameObject go, DSRole role, params TMP_Text[] labels)
    {
        var element = go.GetComponent<DSElement>();
        if (element == null)
            element = go.AddComponent<DSElement>();
        element.role = role;
        element.labels = labels;
        element.EnsureParts();
        element.ApplyShape();
        element.RefreshImmediate();
        return element;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

    private static Color Hex(int rgb, float alpha = 1f) => new(
        ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
}
