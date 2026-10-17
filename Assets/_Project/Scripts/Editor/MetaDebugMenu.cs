using Blockforge.Meta;
using UnityEditor;
using UnityEngine;

// =========================================================
// MENU DEBUG DES ECRANS META (editeur, Play mode)
// Blockforge > Debug > Match end / Hub :
// - Fire MatchEnded (victory / defeat / draw) : la source mock
//   emet un resultat ; MatchEndFlow (installe par la scene de
//   match, ou cree a la volee) enchaine overlay -> resultats -> hub
// - Open Results : l'ecran de resultats directement
// - Add 1 000 credits / Add 500 TP : portefeuille mock
// - Toggle ReduceMotion : reglage « reduire les animations »
// =========================================================

public static class MetaDebugMenu
{
    private const string Root = "Blockforge/Debug/";

    private static bool Playing()
    {
        if (Application.isPlaying)
            return true;
        Debug.LogWarning("[Debug] Entre en Play mode d'abord.");
        return false;
    }

    private static MatchEndFlow Flow()
    {
        var flow = Object.FindAnyObjectByType<MatchEndFlow>();
        return flow != null ? flow : MatchEndFlow.Install(null);
    }

    [MenuItem(Root + "Match end/Fire MatchEnded (victory)")]
    public static void FireVictory()
    {
        if (!Playing()) return;
        Flow();
        MockServices.Instance.MatchResults.Fire(MatchOutcome.Victory);
    }

    [MenuItem(Root + "Match end/Fire MatchEnded (defeat)")]
    public static void FireDefeat()
    {
        if (!Playing()) return;
        Flow();
        MockServices.Instance.MatchResults.Fire(MatchOutcome.Defeat);
    }

    [MenuItem(Root + "Match end/Fire MatchEnded (draw)")]
    public static void FireDraw()
    {
        if (!Playing()) return;
        Flow();
        MockServices.Instance.MatchResults.Fire(MatchOutcome.Draw);
    }

    [MenuItem(Root + "Match end/Open Results")]
    public static void OpenResults()
    {
        if (!Playing()) return;
        Flow().OpenResults(MockServices.Instance.MatchResults.Build(MatchOutcome.Victory));
    }

    [MenuItem(Root + "Hub/Add 1 000 credits")]
    public static void AddCredits()
    {
        if (!Playing()) return;
        MockServices.Instance.Wallet.AddCredits(1000);
        Debug.Log($"[Debug] Credits : {BlockforgeTheme.Num(MockServices.Instance.Wallet.Credits)}");
    }

    [MenuItem(Root + "Hub/Add 500 TP")]
    public static void AddTechPoints()
    {
        if (!Playing()) return;
        MockServices.Instance.Wallet.AddTechPoints(500);
        Debug.Log($"[Debug] Tech points : {BlockforgeTheme.Num(MockServices.Instance.Wallet.TechPoints)}");
    }

    [MenuItem(Root + "Toggle ReduceMotion")]
    public static void ToggleReduceMotion()
    {
        BlockforgeTheme.ReduceMotion = !BlockforgeTheme.ReduceMotion;
        Debug.Log($"[Debug] ReduceMotion = {BlockforgeTheme.ReduceMotion}");
    }

    [MenuItem(Root + "Toggle ReduceMotion", true)]
    public static bool ToggleReduceMotionValidate()
    {
        Menu.SetChecked(Root + "Toggle ReduceMotion", BlockforgeTheme.ReduceMotion);
        return true;
    }
}
