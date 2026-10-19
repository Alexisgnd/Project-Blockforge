using UnityEditor;
using UnityEngine;

// =========================================================
// MENU DEBUG DU FORGE LOADER
// Blockforge > Debug > Loader :
// - Matchmaking mock on Play screen : rend « Find a match »
//   cliquable sur l'ecran Play avec la recherche simulee (case a
//   cocher, memorisee) ;
// - Start mock matchmaking / Mock screen load / Mock splash
//   (Play mode) : les trois contextes sur l'ecran courant ;
// - Reduce motion: follow system : oublie le choix du joueur.
// =========================================================

public static class LoaderDebugMenu
{
    private const string Root = "Blockforge/Debug/Loader/";

    private static bool Playing()
    {
        if (Application.isPlaying)
            return true;
        Debug.LogWarning("[Debug] Entre en Play mode d'abord.");
        return false;
    }

    [MenuItem(Root + "Matchmaking mock on Play screen")]
    public static void ToggleMock()
    {
        MatchmakingService.MockEnabled = !MatchmakingService.MockEnabled;
        Debug.Log($"[Debug] Matchmaking simule sur l'ecran Play : {MatchmakingService.MockEnabled} (effet au prochain chargement du menu).");
    }

    [MenuItem(Root + "Matchmaking mock on Play screen", true)]
    public static bool ToggleMockValidate()
    {
        Menu.SetChecked(Root + "Matchmaking mock on Play screen", MatchmakingService.MockEnabled);
        return true;
    }

    [MenuItem(Root + "Start mock matchmaking")]
    public static void StartMatchmaking()
    {
        if (!Playing()) return;
        var theme = LoaderTheme.Load();
        MatchmakingService.Begin("Team Deathmatch", theme.defaultRegion, theme.seats, theme.estimatedSeconds, new MockMatchmakingSource());
    }

    [MenuItem(Root + "Mock screen load")]
    public static void MockScreenLoad()
    {
        if (!Playing()) return;
        MockLoad.Start(LoadContext.ScreenLoad, LoaderTheme.Load().StepsFor("Garage"));
    }

    [MenuItem(Root + "Mock splash")]
    public static void MockSplash()
    {
        if (!Playing()) return;
        MockLoad.Start(LoadContext.Splash, LoaderTheme.Load().splashSteps);
    }

    [MenuItem(Root + "Reduce motion: follow system")]
    public static void FollowSystem()
    {
        BlockforgeTheme.ResetReduceMotionToSystem();
        Debug.Log($"[Debug] ReduceMotion suit le systeme : {BlockforgeTheme.SystemReduceMotion}");
    }
}
