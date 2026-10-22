using UnityEditor;
using UnityEngine;

// =========================================================
// DEBUG DES RETOURS DE COMBAT (Play mode)
// Blockforge > Debug > Combat feedback : declenche le hitmarker,
// les chiffres de degats et les lignes de la killfeed (match
// contre les bots) sans tirer, pour
// verifier les 3 etats, la fusion des impacts rapproches et les
// mouvements reduits, sur fond clair comme sur fond sombre.
// Les impacts visent le premier robot trouve dans la scene (pour
// que la fusion par cible fonctionne), sinon aucune cible.
// =========================================================

public static class CombatFeedbackDebugMenu
{
    private const string Root = "Blockforge/Debug/Combat feedback/";

    [MenuItem(Root + "Hit", false, 300)]
    private static void Hit() => Fire(CombatFeedbackHUD.HitKind.Hit, 140f);

    [MenuItem(Root + "Point faible", false, 301)]
    private static void WeakSpot() => Fire(CombatFeedbackHUD.HitKind.WeakSpot, 640f);

    [MenuItem(Root + "Kill", false, 302)]
    private static void Kill() => Fire(CombatFeedbackHUD.HitKind.Kill, 1100f);

    // Cinq impacts a 80 ms d'ecart sur la meme cible : un seul chiffre qui s'additionne
    [MenuItem(Root + "Rafale de 5 impacts", false, 320)]
    private static void Burst()
    {
        burstLeft = 5;
        nextBurst = EditorApplication.timeSinceStartup;
        EditorApplication.update -= BurstTick;
        EditorApplication.update += BurstTick;
    }

    // ---------- Killfeed (match contre les bots) ----------
    private const string Feed = Root + "Killfeed : ";

    [MenuItem(Feed + "coequipier tue", false, 400)]
    private static void FeedAlly() => Preview(TdmKillfeed.PreviewCase.AllyKills);

    [MenuItem(Feed + "ennemi tue", false, 401)]
    private static void FeedEnemy() => Preview(TdmKillfeed.PreviewCase.EnemyKills);

    [MenuItem(Feed + "je tue", false, 402)]
    private static void FeedIKill() => Preview(TdmKillfeed.PreviewCase.IKill);

    [MenuItem(Feed + "je meurs", false, 403)]
    private static void FeedIDie() => Preview(TdmKillfeed.PreviewCase.IDie);

    [MenuItem(Feed + "arene", false, 404)]
    private static void FeedArena() => Preview(TdmKillfeed.PreviewCase.Arena);

    [MenuItem(Feed + "auto-destruction", false, 405)]
    private static void FeedSelf() => Preview(TdmKillfeed.PreviewCase.SelfDestruct);

    [MenuItem(Feed + "coequipier tue", true)]
    [MenuItem(Feed + "ennemi tue", true)]
    [MenuItem(Feed + "je tue", true)]
    [MenuItem(Feed + "je meurs", true)]
    [MenuItem(Feed + "arene", true)]
    [MenuItem(Feed + "auto-destruction", true)]
    private static bool CanPreviewFeed() => Application.isPlaying && TdmKillfeed.Active != null;

    private static void Preview(TdmKillfeed.PreviewCase which)
    {
        if (TdmKillfeed.Active != null)
            TdmKillfeed.Active.Preview(which);
    }

    [MenuItem(Root + "Mouvements reduits", false, 340)]
    private static void ToggleReducedMotion()
    {
        var theme = CombatFeedbackTheme.Load();
        theme.reducedMotion = !theme.reducedMotion;
        Debug.Log($"[CombatFeedback] Mouvements reduits : {(theme.reducedMotion ? "oui" : "non")}");
    }

    [MenuItem(Root + "Hit", true)]
    [MenuItem(Root + "Point faible", true)]
    [MenuItem(Root + "Kill", true)]
    [MenuItem(Root + "Rafale de 5 impacts", true)]
    private static bool CanFire() => Application.isPlaying && CombatFeedbackHUD.Active != null;

    [MenuItem(Root + "Mouvements reduits", true)]
    private static bool CanToggle()
    {
        Menu.SetChecked(Root + "Mouvements reduits", Application.isPlaying && CombatFeedbackTheme.Load().reducedMotion);
        return Application.isPlaying;
    }

    private static int burstLeft;
    private static double nextBurst;

    private static void BurstTick()
    {
        if (!Application.isPlaying || CombatFeedbackHUD.Active == null || burstLeft <= 0)
        {
            EditorApplication.update -= BurstTick;
            return;
        }
        if (EditorApplication.timeSinceStartup < nextBurst)
            return;
        burstLeft--;
        nextBurst = EditorApplication.timeSinceStartup + 0.08;
        Fire(burstLeft == 0 ? CombatFeedbackHUD.HitKind.WeakSpot : CombatFeedbackHUD.HitKind.Hit, 220f);
    }

    private static void Fire(CombatFeedbackHUD.HitKind kind, float damage)
    {
        var hud = CombatFeedbackHUD.Active;
        if (hud == null)
            return;
        hud.Show(Object.FindAnyObjectByType<RobotHealth>(), damage, kind);
    }
}
