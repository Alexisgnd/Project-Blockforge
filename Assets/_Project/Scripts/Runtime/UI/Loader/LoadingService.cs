using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// SERVICE DE CHARGEMENT
// Tout chargement de scene ou operation asynchrone longue s'y
// declare ; le splash et le chargement d'ecran s'y abonnent
// (ForgeLoaderHost). Une session a la fois.
//
//   var s = LoadingService.Begin(LoadContext.ScreenLoad, steps);
//   s.Report(0.4f);                 // progression REELLE 0..1
//   s.MarkLoaded();                 // le travail est fini (100 %, « Ready »)
//   yield return s.WaitForTransition(); // le mark rejoint l'etat verrouille
//   ... activer la scene ...
//   s.End();                        // l'overlay s'efface
//
// L'etape affichee se deduit de la progression : les etapes
// avant la derniere se partagent [0, 1[ a parts egales, la
// derniere (« Ready ») vaut 100 %. Report(p, step) force un
// libelle. La boucle du mark ne lit jamais la progression.
// =========================================================

public enum LoadContext
{
    Splash,     // lancement du jeu, plein ecran
    ScreenLoad, // garage <-> hub <-> match, plaque sur l'ecran precedent
    Silent,     // operation suivie sans affichage (l'ecran a deja sa propre UI)
}

public sealed class LoadingSession
{
    public LoadContext Context { get; }
    public IReadOnlyList<string> Steps { get; }
    public float Progress { get; private set; }
    public string Step { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool IsEnded { get; private set; }

    // Pose par l'affichage : vrai quand le mark est verrouille et tenu
    private Func<bool> transitionGate;

    internal LoadingSession(LoadContext context, IReadOnlyList<string> steps)
    {
        Context = context;
        Steps = steps != null && steps.Count > 0 ? steps : new[] { "Loading", "Ready" };
        Step = Steps[0];
    }

    public static string StepAt(IReadOnlyList<string> steps, float progress)
    {
        if (steps == null || steps.Count == 0)
            return "";
        int n = steps.Count;
        if (progress >= 1f || n == 1)
            return steps[n - 1];
        int working = n - 1;
        int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * working), 0, working - 1);
        return steps[index];
    }

    public void Report(float progress) => Report(progress, null);

    public void Report(float progress, string step)
    {
        if (IsEnded)
            return;
        // Jamais de recul : une progression reelle peut stagner, pas revenir en arriere
        Progress = Mathf.Max(Progress, Mathf.Clamp01(progress));
        if (!IsLoaded && Progress >= 1f)
            Progress = 0.999f; // 100 % seulement quand le travail est vraiment fini
        Step = step ?? StepAt(Steps, Progress);
        LoadingService.RaiseChanged(this);
    }

    // Le travail est fini : 100 %, derniere etape ; l'affichage verrouille le mark
    public void MarkLoaded()
    {
        if (IsLoaded || IsEnded)
            return;
        IsLoaded = true;
        Progress = 1f;
        Step = Steps[Steps.Count - 1];
        LoadingService.RaiseChanged(this);
        LoadingService.RaiseLoaded(this);
    }

    // Vrai quand la transition peut partir (aucun affichage, ou mark verrouille)
    public bool CanTransition => IsEnded || transitionGate == null || transitionGate();

    public IEnumerator WaitForTransition()
    {
        while (!CanTransition)
            yield return null;
    }

    public void SetTransitionGate(Func<bool> gate) => transitionGate = gate;

    public void End()
    {
        if (IsEnded)
            return;
        IsEnded = true;
        LoadingService.RaiseEnded(this);
    }
}

public static class LoadingService
{
    public static LoadingSession Current { get; private set; }
    public static bool IsBusy => Current != null && !Current.IsEnded;

    public static event Action<LoadingSession> Began;
    public static event Action<LoadingSession> Changed;
    public static event Action<LoadingSession> Loaded;
    public static event Action<LoadingSession> Ended;

    public static LoadingSession Begin(LoadContext context, IReadOnlyList<string> steps)
    {
        if (IsBusy)
            Current.End();
        Current = new LoadingSession(context, steps);
        ForgeLoaderHost.Ensure(); // l'affichage s'abonne avant le premier evenement
        Began?.Invoke(Current);
        RaiseChanged(Current);
        return Current;
    }

    internal static void RaiseChanged(LoadingSession s) => Changed?.Invoke(s);
    internal static void RaiseLoaded(LoadingSession s) => Loaded?.Invoke(s);

    internal static void RaiseEnded(LoadingSession s)
    {
        Ended?.Invoke(s);
        if (Current == s)
            Current = null;
    }

    // Domain reload desactive : repartir propre a chaque Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        Began = null;
        Changed = null;
        Loaded = null;
        Ended = null;
    }
}
