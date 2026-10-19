using System;
using UnityEngine;

// =========================================================
// ETAT DE MATCHMAKING
// Recherche en ligne : Idle -> Searching -> (Found | annule).
// Le bandeau du Forge Loader (ForgeLoaderHost) s'y abonne et
// affiche les sieges, le temps ecoule et l'estimation ; l'attente
// est indeterminee (pas de pourcentage).
// - Begin(...) lance une recherche ; une source (IMatchmakingSource)
//   remplit les sieges : MockMatchmakingSource tant que le service
//   en ligne n'existe pas, sinon le backend appelle Report(found).
// - Cancel() : Echap / bouton Cancel de la manette.
// - Plein : Found ; le bandeau laisse le mark se verrouiller, puis
//   onFinished(true) part (ex. MatchIntro).
// MockEnabled (Debug > Loader) rend « Find a match » cliquable sur
// l'ecran Play avec la source simulee ; desactive par defaut.
// =========================================================

public enum MatchmakingState { Idle, Searching, Found }

public interface IMatchmakingSource
{
    // Appelee chaque frame pendant la recherche ; renvoie le nombre de joueurs trouves
    int Tick(float deltaSeconds, int found, int total);
}

public static class MatchmakingService
{
    private const string MockKey = "Blockforge.MatchmakingMock";

    public static MatchmakingState State { get; private set; } = MatchmakingState.Idle;
    public static string ModeTitle { get; private set; } = "";
    public static string Region { get; private set; } = "";
    public static int Found { get; private set; }
    public static int Total { get; private set; }
    public static float ElapsedSeconds { get; private set; }
    public static float EstimatedSeconds { get; private set; }

    public static bool IsActive => State != MatchmakingState.Idle;

    public static event Action Started;
    public static event Action Changed;
    public static event Action MatchFound;      // plein : le bandeau verrouille le mark
    public static event Action<bool> Ended;     // true = match trouve, false = annule

    private static IMatchmakingSource source;
    private static Action<bool> onFinished;

    public static bool MockEnabled
    {
        get => PlayerPrefs.GetInt(MockKey, 0) == 1;
        set { PlayerPrefs.SetInt(MockKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void Begin(string modeTitle, string region, int total, float estimatedSeconds,
                             IMatchmakingSource src = null, Action<bool> finished = null)
    {
        if (IsActive)
            Finish(false); // une recherche (ou une fin de recherche) en cours est abandonnee
        ModeTitle = modeTitle ?? "";
        Region = region ?? "";
        Total = Mathf.Max(1, total);
        Found = 1; // le joueur local occupe le premier siege
        ElapsedSeconds = 0f;
        EstimatedSeconds = Mathf.Max(0f, estimatedSeconds);
        source = src;
        onFinished = finished;
        State = MatchmakingState.Searching;
        ForgeLoaderHost.Ensure();
        Started?.Invoke();
        Changed?.Invoke();
        if (Found >= Total)
        {
            State = MatchmakingState.Found;
            MatchFound?.Invoke();
        }
    }

    // Backend : nombre de joueurs trouves (joueur local compris)
    public static void Report(int found)
    {
        if (State != MatchmakingState.Searching)
            return;
        found = Mathf.Clamp(found, 1, Total);
        if (found == Found)
            return;
        Found = found;
        Changed?.Invoke();
        if (Found >= Total)
        {
            State = MatchmakingState.Found;
            MatchFound?.Invoke();
        }
    }

    // Seulement pendant la recherche : une fois le match trouve, la transition suit son cours
    public static void Cancel()
    {
        if (State != MatchmakingState.Searching)
            return;
        Finish(false);
    }

    // Appele par le bandeau une fois le mark verrouille et le bandeau parti
    public static void CompleteTransition()
    {
        if (State == MatchmakingState.Found)
            Finish(true);
    }

    private static void Finish(bool matched)
    {
        State = MatchmakingState.Idle;
        source = null;
        var callback = onFinished;
        onFinished = null;
        Ended?.Invoke(matched);
        callback?.Invoke(matched);
    }

    // Horloge : ForgeLoaderHost.Update (temps reel)
    internal static void Tick(float deltaSeconds)
    {
        if (State != MatchmakingState.Searching)
            return;
        int before = Mathf.FloorToInt(ElapsedSeconds);
        ElapsedSeconds += deltaSeconds;
        if (source != null)
            Report(source.Tick(deltaSeconds, Found, Total));
        if (Mathf.FloorToInt(ElapsedSeconds) != before)
            Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        State = MatchmakingState.Idle;
        source = null;
        onFinished = null;
        Started = null;
        Changed = null;
        MatchFound = null;
        Ended = null;
    }
}

// Joueurs simules : un nouveau toutes les 0,8 a 3,5 s (temps reel x vitesse)
public sealed class MockMatchmakingSource : IMatchmakingSource
{
    private readonly System.Random random;
    private readonly float minGap, maxGap;
    private float wait;
    public float Speed = 1f;

    public MockMatchmakingSource(int seed = 0, float minGap = 0.8f, float maxGap = 3.5f)
    {
        random = seed == 0 ? new System.Random() : new System.Random(seed);
        this.minGap = minGap;
        this.maxGap = maxGap;
        wait = Next();
    }

    private float Next() => minGap + (float)random.NextDouble() * (maxGap - minGap);

    public int Tick(float deltaSeconds, int found, int total)
    {
        wait -= deltaSeconds * Speed;
        if (wait > 0f || found >= total)
            return found;
        wait = Next();
        // Parfois deux joueurs arrivent ensemble (un groupe)
        int add = random.NextDouble() < 0.2 ? 2 : 1;
        return Mathf.Min(total, found + add);
    }
}
