using System;

// =========================================================
// IDENTITE DU JOUEUR (ce que les menus affichent)
// Une identite vient soit de Steam (SteamID64, pseudo et avatar
// Steam), soit d'un compte standard PocketBase (email / mot de
// passe), soit du profil local « Invite » (hors ligne sans
// aucun compte connu sur cette machine).
// `localKey` nomme le dossier de cache local
// (persistentDataPath/BlockForge/<localKey>/) : il ne depend
// pas du serveur, donc il est connu meme hors ligne.
// =========================================================

public enum AuthMethod
{
    None,
    Steam,
    Password,
    Guest,
}

[Serializable]
public class PlayerIdentity
{
    public const string GuestKey = "guest";

    public AuthMethod method = AuthMethod.None;

    // Id du record PocketBase `users` (vide tant que ce compte ne s'est jamais connecte)
    public string userId = "";
    public string displayName = "";

    // Steam uniquement
    public string steamId = "";
    public string avatarHash = "";

    // Compte standard uniquement (pour pre-remplir l'ecran de connexion)
    public string email = "";

    // Derniere connexion reussie au serveur (ISO 8601, vide si jamais)
    public string lastOnlineAt = "";

    public bool IsGuest => method == AuthMethod.Guest;
    public bool IsSteam => method == AuthMethod.Steam;

    // Dossier de cache : stable et connu hors ligne
    public string LocalKey => method switch
    {
        AuthMethod.Steam => "steam-" + steamId,
        AuthMethod.Password => "pb-" + userId,
        AuthMethod.Guest => GuestKey,
        _ => GuestKey,
    };

    public static PlayerIdentity Guest() => new()
    {
        method = AuthMethod.Guest,
        displayName = "Guest",
    };

    public static PlayerIdentity FromSteam(ulong steamId, string personaName, string avatarHash) => new()
    {
        method = AuthMethod.Steam,
        steamId = steamId.ToString(),
        displayName = string.IsNullOrEmpty(personaName) ? "Pilot" : personaName,
        avatarHash = avatarHash ?? "",
    };

    public static PlayerIdentity FromUser(PocketBaseUser user, AuthMethod method)
    {
        var identity = new PlayerIdentity
        {
            method = method,
            userId = user?.id ?? "",
            steamId = user?.steamId ?? "",
            avatarHash = user?.avatarHash ?? "",
            email = user?.email ?? "",
            displayName = DisplayNameOf(user),
        };
        return identity;
    }

    public static string DisplayNameOf(PocketBaseUser user)
    {
        if (user == null) return "Pilot";
        if (!string.IsNullOrEmpty(user.displayName)) return user.displayName;
        if (!string.IsNullOrEmpty(user.username)) return user.username;
        if (!string.IsNullOrEmpty(user.name)) return user.name;
        // Jamais l'email a l'ecran (capture, stream, killfeed)
        return "Pilot";
    }
}
