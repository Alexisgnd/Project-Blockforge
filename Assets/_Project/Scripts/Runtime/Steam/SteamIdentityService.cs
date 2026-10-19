using System;
using UnityEngine;
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
using Steamworks;
#endif

// =========================================================
// IDENTITE STEAM DU JOUEUR
// SteamID64, pseudo (persona name) et avatar (184 px) lus
// localement des que Steam est initialise — sans reseau, donc
// disponibles aussi quand Steam est en mode hors ligne.
// Le SteamID est l'identifiant du joueur ; le pseudo n'est
// qu'un affichage (modifiable, non unique).
// Fournit aussi le ticket Web API (GetAuthTicketForWebApi)
// que PocketBase verifie aupres de Valve (route steam-auth).
// Toute la classe est inerte si Steamworks.NET est absent.
// =========================================================

public static class SteamIdentityService
{
    // Chaine d'identite partagee avec le serveur : seuls les tickets
    // emis pour elle sont acceptes par la route PocketBase.
    public const string WebApiIdentity = "blockforge-backend";

    public static bool HasIdentity => SteamId != 0;
    public static ulong SteamId { get; private set; }
    public static string PersonaName { get; private set; } = "";

    // Avatar : texture + sprite prets a l'affichage (null tant que Steam ne l'a pas livre)
    public static Texture2D Avatar { get; private set; }
    public static Sprite AvatarSprite { get; private set; }
    public static event Action AvatarReady;

    // Empreinte de l'avatar envoyee au serveur (pour afficher d'autres joueurs plus tard)
    public static string AvatarHash { get; private set; } = "";

#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
    private static Callback<AvatarImageLoaded_t> avatarLoaded;
    private static Callback<GetTicketForWebApiResponse_t> ticketResponse;
    private static Callback<PersonaStateChange_t> personaChanged;

    // Ticket en attente (une seule demande a la fois)
    private static HAuthTicket pendingTicket = HAuthTicket.Invalid;
    private static bool ticketDone;
    private static EResult ticketResult;
    private static string ticketHex;
#endif

    // =========================================================
    // LECTURE LOCALE (appelee par SteamBootstrap apres Init)
    // =========================================================

    public static void Refresh()
    {
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
        if (!SteamBootstrap.IsAvailable)
            return;

        var id = SteamUser.GetSteamID();
        SteamId = id.m_SteamID;
        PersonaName = SteamFriends.GetPersonaName() ?? "";

        avatarLoaded ??= Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
        personaChanged ??= Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);

        // -1 : image pas encore chargee, Steam la livrera via AvatarImageLoaded_t ;
        //  0 : pas d'avatar ; > 0 : handle utilisable tout de suite.
        int handle = SteamFriends.GetLargeFriendAvatar(id);
        if (handle > 0)
            LoadAvatar(handle);
#endif
    }

    public static void Shutdown()
    {
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
        CancelTicket();
        avatarLoaded?.Dispose();
        avatarLoaded = null;
        personaChanged?.Dispose();
        personaChanged = null;
        ticketResponse?.Dispose();
        ticketResponse = null;
#endif
    }

#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
    private static void OnPersonaStateChange(PersonaStateChange_t change)
    {
        if (change.m_ulSteamID != SteamId)
            return;
        if ((change.m_nChangeFlags & EPersonaChange.k_EPersonaChangeName) != 0)
            PersonaName = SteamFriends.GetPersonaName() ?? PersonaName;
        if ((change.m_nChangeFlags & EPersonaChange.k_EPersonaChangeAvatar) != 0)
        {
            int handle = SteamFriends.GetLargeFriendAvatar(new CSteamID(SteamId));
            if (handle > 0)
                LoadAvatar(handle);
        }
    }

    private static void OnAvatarImageLoaded(AvatarImageLoaded_t loaded)
    {
        if (loaded.m_steamID.m_SteamID != SteamId || loaded.m_iImage <= 0)
            return;
        LoadAvatar(loaded.m_iImage);
    }

    // Copie l'image Steam (RGBA, lignes de haut en bas) dans une Texture2D (lignes de bas en haut)
    private static void LoadAvatar(int imageHandle)
    {
        if (!SteamUtils.GetImageSize(imageHandle, out uint width, out uint height) || width == 0 || height == 0)
            return;

        int w = (int)width, h = (int)height;
        var rgba = new byte[w * h * 4];
        if (!SteamUtils.GetImageRGBA(imageHandle, rgba, rgba.Length))
            return;

        var flipped = new byte[rgba.Length];
        int stride = w * 4;
        for (int y = 0; y < h; y++)
            Buffer.BlockCopy(rgba, y * stride, flipped, (h - 1 - y) * stride, stride);

        if (Avatar == null || Avatar.width != w || Avatar.height != h)
        {
            if (Avatar != null)
                UnityEngine.Object.Destroy(Avatar);
            Avatar = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "SteamAvatar",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }
        Avatar.LoadRawTextureData(flipped);
        Avatar.Apply(false, false);

        if (AvatarSprite != null)
            UnityEngine.Object.Destroy(AvatarSprite);
        AvatarSprite = Sprite.Create(Avatar, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        AvatarSprite.name = "SteamAvatar";

        AvatarHash = ComputeHash(flipped);
        AvatarReady?.Invoke();
    }

    // Empreinte courte et stable du contenu (FNV-1a 64 bits, en hexadecimal)
    private static string ComputeHash(byte[] data)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (int i = 0; i < data.Length; i++)
        {
            hash ^= data[i];
            hash *= prime;
        }
        return hash.ToString("x16");
    }
#endif

    // =========================================================
    // TICKET WEB API (verifie cote PocketBase)
    // =========================================================

    // Demande un ticket a Steam et le renvoie en hexadecimal (format attendu par
    // ISteamUserAuth/AuthenticateUserTicket). Null si Steam est absent, hors ligne,
    // refuse ou ne repond pas dans le delai. Le ticket reste actif jusqu'a CancelTicket()
    // (a appeler une fois la reponse du serveur recue).
    public static async Awaitable<string> RequestWebApiTicketAsync(float timeoutSeconds = 10f)
    {
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
        if (!SteamBootstrap.IsLoggedOn)
            return null;

        CancelTicket();
        ticketResponse ??= Callback<GetTicketForWebApiResponse_t>.Create(OnTicketResponse);

        ticketDone = false;
        ticketHex = null;
        pendingTicket = SteamUser.GetAuthTicketForWebApi(WebApiIdentity);
        if (pendingTicket == HAuthTicket.Invalid)
        {
            Debug.LogWarning("[Steam] GetAuthTicketForWebApi a renvoye un handle invalide.");
            return null;
        }

        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!ticketDone && Time.realtimeSinceStartup < deadline)
            await Awaitable.NextFrameAsync();

        if (!ticketDone)
        {
            Debug.LogWarning("[Steam] Pas de reponse de Steam pour le ticket Web API (delai depasse).");
            CancelTicket();
            return null;
        }
        if (ticketResult != EResult.k_EResultOK || string.IsNullOrEmpty(ticketHex))
        {
            Debug.LogWarning($"[Steam] Ticket Web API refuse : {ticketResult}");
            CancelTicket();
            return null;
        }
        return ticketHex;
#else
        await Awaitable.NextFrameAsync();
        return null;
#endif
    }

    public static void CancelTicket()
    {
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
        if (pendingTicket != HAuthTicket.Invalid && SteamBootstrap.IsAvailable)
            SteamUser.CancelAuthTicket(pendingTicket);
        pendingTicket = HAuthTicket.Invalid;
        ticketHex = null;
        ticketDone = false;
#endif
    }

#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
    private static void OnTicketResponse(GetTicketForWebApiResponse_t response)
    {
        if (response.m_hAuthTicket != pendingTicket)
            return;

        ticketResult = response.m_eResult;
        if (response.m_eResult == EResult.k_EResultOK && response.m_cubTicket > 0 && response.m_rgubTicket != null)
        {
            int length = Mathf.Min(response.m_cubTicket, response.m_rgubTicket.Length);
            var sb = new System.Text.StringBuilder(length * 2);
            for (int i = 0; i < length; i++)
                sb.Append(response.m_rgubTicket[i].ToString("x2"));
            ticketHex = sb.ToString();
        }
        ticketDone = true;
    }
#endif
}
