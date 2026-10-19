using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// GARDE DE SESSION
// Les scenes qui exigent un joueur identifie appellent
// EnsureAuthenticated() dans Awake. Depuis l'arrivee de Steam
// et du mode hors ligne, « identifie » veut dire : la session a
// une identite (En ligne, Hors ligne ou Invite) — on ne renvoie
// vers Boot que si l'ecran de demarrage n'a jamais ete passe
// (scene lancee directement dans l'editeur, par exemple).
// =========================================================

public static class AuthGuard
{
    public const string BootSceneName = "Boot";

    public static bool EnsureAuthenticated()
    {
        if (SessionService.HasIdentity)
            return true;

        // L'UI du Boot a besoin du curseur (le Garage le verrouille)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[AuthGuard] Aucune session — retour à l'écran de démarrage.");
        SceneManager.LoadScene(BootSceneName);
        return false;
    }

    // Vrai si une fonction en ligne peut etre utilisee tout de suite ; sinon
    // affiche la raison (toast) et renvoie faux.
    public static bool RequireOnline(string feature)
    {
        if (SessionService.IsOnline)
            return true;
        Toast.Warning($"{feature} needs a connection. Click your name in the header to reconnect.");
        return false;
    }
}
