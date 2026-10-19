using System.Collections;
using UnityEngine;

// =========================================================
// CHARGEMENT SIMULE (scene LoaderPreview, menu Debug)
// Un chargement credible : vite jusqu'a 45 %, lentement jusqu'a
// 60 %, arret net de 0,5 s, puis vite jusqu'a 100 % (profil dans
// LoaderTheme.mockLoad). Passe par le LoadingService comme un
// vrai chargement : meme affichage, meme fin sur le mark
// verrouille.
// =========================================================

public static class MockLoad
{
    public static bool IsRunning { get; private set; }

    // Progression du profil au temps t (s) ; 1 a la fin
    public static float ProgressAt(MockLoadProfile p, float t)
    {
        float a = p.fastSeconds, b = a + p.slowSeconds, c = b + p.stallSeconds, d = c + p.quickSeconds;
        if (t <= 0f) return 0f;
        if (t < a) return Mathf.Lerp(0f, p.fastTo, ForgeTimeline.Ease(t / a));
        if (t < b) return Mathf.Lerp(p.fastTo, p.slowTo, (t - a) / p.slowSeconds); // lent : lineaire
        if (t < c) return p.slowTo;
        if (t < d) return Mathf.Lerp(p.slowTo, 1f, ForgeTimeline.Ease((t - c) / p.quickSeconds));
        return 1f;
    }

    public static LoadingSession Start(LoadContext context, string[] steps, float speed = 1f)
    {
        var session = LoadingService.Begin(context, steps);
        ForgeLoaderHost.Ensure().Run(Run(session, LoaderTheme.Load().mockLoad, speed));
        return session;
    }

    public static IEnumerator Run(LoadingSession session, MockLoadProfile profile, float speed = 1f)
    {
        IsRunning = true;
        float t = 0f;
        float total = profile.TotalSeconds;
        while (t < total && !session.IsEnded && !session.IsLoaded)
        {
            session.Report(ProgressAt(profile, t));
            yield return null;
            t += Time.unscaledDeltaTime * Mathf.Max(0.01f, speed);
        }

        if (!session.IsEnded)
        {
            session.MarkLoaded();
            yield return session.WaitForTransition();
            yield return new WaitForSecondsRealtime(BlockforgeTheme.MotionPanel); // la transition simulee
            session.End();
        }
        IsRunning = false;
    }
}
