using System;
using UnityEngine;

// =========================================================
// SESSION DU JOUEUR (machine d'etats)
//   None       : rien n'a encore ete decide (avant l'ecran Boot)
//   Connecting : authentification en cours
//   Online     : token PocketBase valide, tout est disponible
//   Offline    : identite connue (Steam en cache ou dernier
//                compte vu sur cette machine), garage sur le
//                cache local, matchs contre des bots
//   Guest      : hors ligne sans aucune identite connue
// Chemin choisi au Boot : Steam si le client tourne (sauf si le
// joueur a demande un compte standard dans les parametres),
// sinon email / mot de passe. Les deux menent au meme type de
// record `users` ; voir claude/steam-integration.md.
// Le token n'est jamais ecrit sur le disque : hors ligne, aucune
// requete serveur n'est tentee.
// =========================================================

public enum SessionState
{
    None,
    Connecting,
    Online,
    Offline,
    Guest,
}

public struct SessionResult
{
    public bool ok;
    public string error;      // message affichable (anglais, comme le reste de l'UI)
    public bool networkError; // serveur injoignable (par opposition a un refus)
    public bool steamOffline; // Steam tourne mais n'est pas connecte a ses serveurs

    public static SessionResult Success() => new() { ok = true };
    public static SessionResult Fail(string error, bool networkError = false, bool steamOffline = false) =>
        new() { ok = false, error = error, networkError = networkError, steamOffline = steamOffline };
}

public static class SessionService
{
    private const string PreferStandardKey = "Blockforge.Auth.PreferStandard";

    public static SessionState State { get; private set; } = SessionState.None;
    public static PlayerIdentity Identity { get; private set; }

    // Avatar affiche dans les menus : Steam (live ou en cache), sinon null = pictogramme par defaut
    public static Sprite Avatar { get; private set; }

    public static event Action<SessionState> StateChanged;
    public static event Action IdentityChanged; // pseudo ou avatar

    // Une identite en cours de reconnexion reste une identite : la garde ne renvoie
    // vers Boot que si l'ecran de demarrage n'a jamais ete passe (State == None).
    public static bool HasIdentity => Identity != null && State != SessionState.None;
    public static bool IsOnline => State == SessionState.Online;
    public static bool CanUseOnlineFeatures => IsOnline;
    public static string DisplayName => Identity?.displayName ?? "Guest";

    public static PocketBaseClient Client => PocketBaseClient.Instance;

    // Le joueur a Steam mais veut son compte email (Parametres > « Use an email account »)
    public static bool PreferStandardAccount
    {
        get => PlayerPrefs.GetInt(PreferStandardKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PreferStandardKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // Steam decide du chemin : client present et pas de preference contraire
    public static bool ShouldUseSteam => SteamBootstrap.IsAvailable && !PreferStandardAccount;

    private static bool avatarHooked;
    private static Texture2D cachedAvatarTexture;

    // =========================================================
    // STEAM
    // =========================================================

    public static async Awaitable<SessionResult> SignInWithSteamAsync()
    {
        if (!SteamBootstrap.IsAvailable || !SteamIdentityService.HasIdentity)
            return SessionResult.Fail("Steam isn't running.");

        AdoptSteamIdentity();
        SetState(SessionState.Connecting);

        if (!SteamBootstrap.IsLoggedOn)
        {
            EnterOffline();
            return SessionResult.Fail("Steam is in offline mode.", steamOffline: true);
        }

        var client = Client;
        if (client == null)
        {
            EnterOffline();
            return SessionResult.Fail("The server client isn't configured.");
        }

        string ticket = await SteamIdentityService.RequestWebApiTicketAsync();
        if (string.IsNullOrEmpty(ticket))
        {
            EnterOffline();
            return SessionResult.Fail("Steam didn't issue a sign-in ticket.", steamOffline: true);
        }

        try
        {
            var auth = await client.LoginWithSteamAsync(ticket, SteamIdentityService.PersonaName, SteamIdentityService.AvatarHash);
            CompleteOnline(auth.record, AuthMethod.Steam);
            return SessionResult.Success();
        }
        catch (PocketBaseException e)
        {
            Debug.LogWarning($"[Session] Connexion Steam refusee ou impossible ({e.StatusCode}) : {e.Message}");
            EnterOffline();
            return SessionResult.Fail(e.Message, networkError: e.IsNetworkError);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EnterOffline();
            return SessionResult.Fail("Something went wrong while signing in with Steam.");
        }
        finally
        {
            SteamIdentityService.CancelTicket();
        }
    }

    // Identite Steam locale, completee par ce que le cache sait de ce compte (userId...)
    private static void AdoptSteamIdentity()
    {
        var steam = PlayerIdentity.FromSteam(SteamIdentityService.SteamId, SteamIdentityService.PersonaName, SteamIdentityService.AvatarHash);
        var cached = IdentityCache.Load(steam.LocalKey);
        if (cached != null)
        {
            steam.userId = cached.userId;
            steam.lastOnlineAt = cached.lastOnlineAt;
            steam.email = cached.email;
        }
        Identity = steam;
        HookSteamAvatar();
        RefreshAvatar();
        IdentityChanged?.Invoke();
    }

    // =========================================================
    // COMPTE STANDARD
    // =========================================================

    public static async Awaitable<SessionResult> SignInWithPasswordAsync(string email, string password)
    {
        var client = Client;
        if (client == null)
            return SessionResult.Fail("The server client isn't configured.");

        SetState(SessionState.Connecting);
        try
        {
            var auth = await client.LoginAsync(email, password);
            CompleteOnline(auth.record, AuthMethod.Password);
            return SessionResult.Success();
        }
        catch (PocketBaseException e)
        {
            // On revient a l'etat precedent : l'ecran Boot decide de la suite
            SetState(SessionState.None);
            string message = e.StatusCode == 400 ? "Wrong email or password." : e.Message;
            return SessionResult.Fail(message, networkError: e.IsNetworkError);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetState(SessionState.None);
            return SessionResult.Fail("Something went wrong while signing in.");
        }
    }

    // =========================================================
    // HORS LIGNE / RECONNEXION
    // =========================================================

    // Continue sans serveur : identite Steam si disponible, sinon dernier compte connu, sinon invite
    public static void ContinueOffline()
    {
        if (Identity == null || Identity.method == AuthMethod.None)
        {
            if (ShouldUseSteam && SteamIdentityService.HasIdentity)
                AdoptSteamIdentity();
            else
                Identity = IdentityCache.LoadLast() ?? PlayerIdentity.Guest();
        }
        EnterOffline();
    }

    // Reprend la connexion sans repasser par l'ecran Boot (bouton Retry du hangar, retour reseau).
    // false = impossible sans mot de passe (compte standard sans token) : l'UI renvoie vers Boot.
    public static async Awaitable<bool> TryReconnectAsync()
    {
        if (IsOnline)
            return true;

        if (Identity != null && Identity.IsSteam && SteamBootstrap.IsAvailable)
        {
            var result = await SignInWithSteamAsync();
            return result.ok;
        }

        var client = Client;
        if (client != null && client.IsAuthenticated && Identity != null && Identity.method == AuthMethod.Password)
        {
            SetState(SessionState.Connecting);
            try
            {
                var auth = await client.AuthRefreshAsync();
                CompleteOnline(auth.record, AuthMethod.Password);
                return true;
            }
            catch (PocketBaseException e)
            {
                Debug.LogWarning($"[Session] Rafraichissement du token impossible ({e.StatusCode}) : {e.Message}");
                if (!e.IsNetworkError)
                    client.Logout(); // token refuse : il faudra un mot de passe
                EnterOffline();
                return false;
            }
        }

        return false;
    }

    // Coupure constatee en cours de session (requete reseau en echec)
    public static void GoOffline(string reason = null)
    {
        if (State == SessionState.Offline || State == SessionState.Guest)
            return;
        if (Identity == null)
            Identity = IdentityCache.LoadLast() ?? PlayerIdentity.Guest();
        if (!string.IsNullOrEmpty(reason))
            Debug.Log($"[Session] Passage hors ligne : {reason}");
        EnterOffline();
        Toast.Warning("You're offline. Your robots are saved on this computer and will sync when you're back online.");
    }

    public static void SignOut()
    {
        Client?.Logout();
        SteamIdentityService.CancelTicket();
        Identity = null;
        Avatar = null;
        SetState(SessionState.None);
        IdentityChanged?.Invoke();
    }

    // =========================================================
    // INTERNE
    // =========================================================

    private static void CompleteOnline(PocketBaseUser user, AuthMethod method)
    {
        var identity = PlayerIdentity.FromUser(user, method);
        if (method == AuthMethod.Steam)
        {
            // Le pseudo et l'avatar live priment sur ce que le serveur a en cache
            identity.steamId = string.IsNullOrEmpty(identity.steamId) ? SteamIdentityService.SteamId.ToString() : identity.steamId;
            if (!string.IsNullOrEmpty(SteamIdentityService.PersonaName))
                identity.displayName = SteamIdentityService.PersonaName;
            identity.avatarHash = SteamIdentityService.AvatarHash;
            HookSteamAvatar();
        }
        identity.lastOnlineAt = DateTime.UtcNow.ToString("o");
        Identity = identity;
        IdentityCache.Save(identity);
        RefreshAvatar();
        SetState(SessionState.Online);
        IdentityChanged?.Invoke();
    }

    private static void EnterOffline()
    {
        if (Identity == null)
            Identity = PlayerIdentity.Guest();
        IdentityCache.Save(Identity, markAsLast: !Identity.IsGuest);
        RefreshAvatar();
        SetState(Identity.IsGuest ? SessionState.Guest : SessionState.Offline);
        IdentityChanged?.Invoke();
    }

    private static void SetState(SessionState state)
    {
        if (State == state)
            return;
        State = state;
        Debug.Log($"[Session] {state} — {Identity?.displayName ?? "?"} ({Identity?.method})");
        StateChanged?.Invoke(state);
    }

    // ---------- Avatar ----------

    private static void HookSteamAvatar()
    {
        if (avatarHooked)
            return;
        avatarHooked = true;
        SteamIdentityService.AvatarReady += OnSteamAvatarReady;
    }

    private static void OnSteamAvatarReady()
    {
        if (Identity == null || !Identity.IsSteam)
            return;
        // Mis en cache pour les lancements hors ligne de Steam
        IdentityCache.SaveAvatar(Identity.LocalKey, SteamIdentityService.Avatar);
        if (!string.IsNullOrEmpty(SteamIdentityService.AvatarHash) && Identity.avatarHash != SteamIdentityService.AvatarHash)
        {
            Identity.avatarHash = SteamIdentityService.AvatarHash;
            IdentityCache.Save(Identity, markAsLast: false);
        }
        RefreshAvatar();
        IdentityChanged?.Invoke();
    }

    private static void RefreshAvatar()
    {
        if (Identity == null || !Identity.IsSteam)
        {
            Avatar = null;
            return;
        }
        if (SteamIdentityService.AvatarSprite != null)
        {
            Avatar = SteamIdentityService.AvatarSprite;
            return;
        }
        if (Avatar != null)
            return; // avatar en cache deja charge

        if (cachedAvatarTexture != null)
            UnityEngine.Object.Destroy(cachedAvatarTexture);
        cachedAvatarTexture = IdentityCache.LoadAvatar(Identity.LocalKey);
        if (cachedAvatarTexture != null)
        {
            Avatar = Sprite.Create(cachedAvatarTexture,
                                   new Rect(0f, 0f, cachedAvatarTexture.width, cachedAvatarTexture.height),
                                   new Vector2(0.5f, 0.5f), 100f);
            Avatar.name = "CachedAvatar";
        }
    }
}
