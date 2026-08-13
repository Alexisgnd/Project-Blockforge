using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// GARDE D'AUTHENTIFICATION
// Les scenes qui exigent un joueur connecte appellent
// EnsureAuthenticated() dans Awake : si la session PocketBase
// est absente ou invalide, retour a l'ecran Boot.
// =========================================================

public static class AuthGuard
{
    public const string BootSceneName = "Boot";

    public static bool EnsureAuthenticated()
    {
        var client = PocketBaseClient.Instance;
        if (client != null && client.IsAuthenticated)
            return true;

        // L'UI du Boot a besoin du curseur (le Garage le verrouille)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[AuthGuard] Aucune session active — retour à l'écran de connexion.");
        SceneManager.LoadScene(BootSceneName);
        return false;
    }
}
