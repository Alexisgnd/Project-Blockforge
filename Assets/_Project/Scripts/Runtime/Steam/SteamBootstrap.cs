using UnityEngine;
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
using Steamworks;
#endif

// =========================================================
// AMORCE STEAM (Steamworks.NET)
// Cree tout seul au lancement (avant la premiere scene), vit
// jusqu'a la fermeture du jeu. Steam est OPTIONNEL : si le
// client Steam ne tourne pas, Init echoue en silence et le jeu
// continue avec un compte standard (email / mot de passe) — voir
// BootAuthController et SessionService.
// Le package Steamworks.NET est reference par l'asmdef
// Blockforge.Runtime ; BLOCKFORGE_STEAM (versionDefine) est donc
// defini tant qu'il est installe. Les `#if` ci-dessous servent a
// retirer Steam d'une build en une ligne (enlever la reference).
// - IsAvailable : SteamAPI.Init() a reussi (client Steam present)
// - IsLoggedOn  : Steam est connecte a ses serveurs (sinon mode
//                 hors ligne de Steam : identite en cache, pas de
//                 ticket)
// Pas de RestartAppIfNecessary : il forcerait le lancement via
// Steam, ce que la decision « Steam optionnel » exclut.
// App ID 1926750 (steam_appid.txt a la racine du projet pour
// l'editeur ; Steam fournit l'App ID lui-meme dans une build
// lancee depuis la bibliotheque).
// =========================================================

public sealed class SteamBootstrap : MonoBehaviour
{
    public const uint AppId = 1926750;

    public static SteamBootstrap Instance { get; private set; }

    // Steamworks.NET est compile dans le projet (package present)
    public static bool IsCompiledIn =>
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
        true;
#else
        false;
#endif

    public static bool IsAvailable { get; private set; }

    public static bool IsLoggedOn
    {
        get
        {
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
            return IsAvailable && SteamUser.BLoggedOn();
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOnLoad()
    {
        if (Instance != null)
            return;
        var go = new GameObject("SteamBootstrap");
        DontDestroyOnLoad(go);
        go.AddComponent<SteamBootstrap>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Initialize();
    }

    private void Initialize()
    {
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
        try
        {
            if (!Packsize.Test())
                Debug.LogWarning("[Steam] Packsize.Test() a echoue : la version de Steamworks.NET ne correspond pas a la plateforme.");
            if (!DllCheck.Test())
                Debug.LogWarning("[Steam] DllCheck.Test() a echoue : une DLL Steamworks est absente ou d'une mauvaise version.");

            IsAvailable = SteamAPI.Init();
        }
        catch (System.DllNotFoundException e)
        {
            // steam_api64.dll absente : le jeu tourne sans Steam
            Debug.LogWarning($"[Steam] Bibliotheque Steam introuvable, Steam desactive : {e.Message}");
            IsAvailable = false;
        }

        if (IsAvailable)
        {
            Debug.Log($"[Steam] Initialise — App {AppId}, joueur {SteamFriends.GetPersonaName()} ({SteamUser.GetSteamID().m_SteamID}), " +
                      (SteamUser.BLoggedOn() ? "connecte" : "hors ligne"));
            SteamIdentityService.Refresh();
        }
        else
        {
            // Pas d'erreur : c'est le cas normal d'un lancement sans Steam
            Debug.Log("[Steam] Client Steam absent ou non initialise : compte standard.");
        }
#else
        IsAvailable = false;
        Debug.Log("[Steam] Steamworks.NET non compile (package absent) : compte standard.");
#endif
    }

    private void Update()
    {
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
        if (IsAvailable)
            SteamAPI.RunCallbacks();
#endif
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        Instance = null;
#if BLOCKFORGE_STEAM && !DISABLESTEAMWORKS
        if (IsAvailable)
        {
            SteamIdentityService.Shutdown();
            SteamAPI.Shutdown();
        }
#endif
        IsAvailable = false;
    }
}
