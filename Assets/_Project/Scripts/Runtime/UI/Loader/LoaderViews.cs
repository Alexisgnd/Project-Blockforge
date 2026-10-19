using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// =========================================================
// VUES DES TROIS CONTEXTES (liaison des templates UXML)
// - LoaderProgressView : splash (ForgeSplash.uxml) et
//   chargement d'ecran (ForgeScreenLoad.uxml) ; SetProgress(p, step).
//   Le wordmark du splash suit la pose du mark : il monte de
//   --forge-wordmark-rise px et apparait au verrouillage, s'efface
//   en fin de cycle.
// - MatchmakingStripView : bandeau (ForgeMatchmaking.uxml) ;
//   SetMatchmaking(found, total, elapsed, estimated).
// Les pourcentages sont reels (LoadingService) ; la boucle du
// mark en est independante.
// =========================================================

public sealed class LoaderProgressView
{
    private static readonly CustomStyleProperty<float> RiseProp = new("--forge-wordmark-rise");

    public readonly LoadContext Context;
    public readonly TemplateContainer Root;
    public readonly ForgeLoader Loader;

    private readonly ForgeBar bar;
    private readonly Label step, percent, build, tip;
    private readonly VisualElement wordmark;
    private float rise;
    private int shownPercent = -1;

    public LoaderProgressView(LoadContext context, TemplateContainer root)
    {
        Context = context;
        Root = root;
        Loader = root.Q<ForgeLoader>("mark");
        bar = root.Q<ForgeBar>("bar");
        step = root.Q<Label>("step");
        percent = root.Q<Label>("percent");
        build = root.Q<Label>("build");
        tip = root.Q<Label>("tip");
        wordmark = root.Q<VisualElement>("wordmark");

        if (wordmark != null)
        {
            wordmark.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(RiseProp, out var px))
                    rise = px;
            });
        }
        if (Loader != null)
            Loader.PoseApplied += OnPose;
    }

    public void SetProgress(float progress, string stepLabel)
    {
        progress = Mathf.Clamp01(progress);
        if (bar != null)
            bar.Value = progress;
        int pct = Mathf.FloorToInt(progress * 100f + 1e-4f);
        if (percent != null && pct != shownPercent)
        {
            shownPercent = pct;
            percent.text = pct + "%";
        }
        if (step != null && stepLabel != null)
            step.text = stepLabel;
    }

    public void SetFooter(string buildText, string tipText)
    {
        if (build != null) build.text = buildText ?? "";
        if (tip != null) tip.text = tipText ?? "";
    }

    public void Reset()
    {
        shownPercent = -1;
        SetProgress(0f, null);
        Loader?.FollowGlobalReduceMotion();
        Loader?.Restart();
    }

    private void OnPose(ForgeLoader loader)
    {
        if (wordmark == null)
            return;
        var pose = loader.Pose;
        wordmark.style.opacity = pose.wordmark;
        wordmark.style.translate = new Translate(0f, rise * pose.wordmarkRise);
    }
}

public sealed class MatchmakingStripView
{
    public const string SeatClass = "forge-seat";
    public const string SeatFoundClass = "forge-seat--found";
    public const string SeatSelfClass = "forge-seat--self";

    public readonly TemplateContainer Root;
    public readonly ForgeLoader Loader;
    public readonly VisualElement Cinematic;
    public readonly VisualElement Strip;

    private readonly VisualElement seats;
    private readonly Label mode, count, elapsed, estimated;
    private readonly List<VisualElement> seatViews = new();
    private int lastFound = -1, lastTotal = -1, lastElapsed = -1, lastEstimated = -1;

    public event Action CancelClicked;

    public MatchmakingStripView(TemplateContainer root)
    {
        Root = root;
        Loader = root.Q<ForgeLoader>("mark");
        Cinematic = root.Q<VisualElement>("cinematic");
        Strip = root.Q<VisualElement>("strip");
        seats = root.Q<VisualElement>("seats");
        mode = root.Q<Label>("mode");
        count = root.Q<Label>("count");
        elapsed = root.Q<Label>("elapsed");
        estimated = root.Q<Label>("estimated");
        var cancel = root.Q<Button>("cancel");
        if (cancel != null)
            cancel.clicked += () => CancelClicked?.Invoke();
    }

    public void SetMode(string modeTitle, string region)
    {
        if (mode == null)
            return;
        mode.text = string.IsNullOrEmpty(region) ? modeTitle : $"{modeTitle} · {region}";
    }

    public void SetMatchmaking(int found, int total, float elapsedSeconds, float estimatedSeconds)
    {
        total = Mathf.Max(1, total);
        found = Mathf.Clamp(found, 0, total);
        if (total != lastTotal)
            BuildSeats(total);

        if (found != lastFound || total != lastTotal)
        {
            for (int i = 0; i < seatViews.Count; i++)
            {
                // Siege 0 = joueur local ; les suivants se remplissent dans l'ordre d'arrivee
                seatViews[i].EnableInClassList(SeatSelfClass, i == 0 && found > 0);
                seatViews[i].EnableInClassList(SeatFoundClass, i > 0 && i < found);
            }
            if (count != null)
                count.text = $"{found} / {total}";
        }

        int e = Mathf.FloorToInt(elapsedSeconds);
        if (e != lastElapsed && elapsed != null)
            elapsed.text = BlockforgeTheme.Clock(TimeSpan.FromSeconds(e));
        int est = Mathf.RoundToInt(estimatedSeconds);
        if (est != lastEstimated && estimated != null)
            estimated.text = est > 0 ? BlockforgeTheme.Clock(TimeSpan.FromSeconds(est)) : "–";

        lastFound = found;
        lastTotal = total;
        lastElapsed = e;
        lastEstimated = est;
    }

    public void Reset()
    {
        lastFound = lastTotal = lastElapsed = lastEstimated = -1;
        Loader?.FollowGlobalReduceMotion();
        Loader?.Restart();
    }

    private void BuildSeats(int total)
    {
        if (seats == null)
            return;
        seats.Clear();
        seatViews.Clear();
        for (int i = 0; i < total; i++)
        {
            var seat = new ChamferPanel { name = "seat-" + i, pickingMode = PickingMode.Ignore };
            seat.AddToClassList(SeatClass);
            seats.Add(seat);
            seatViews.Add(seat);
        }
    }
}
