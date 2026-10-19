using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// =========================================================
// ECRAN DE DEMARRAGE (scene Boot) — compte du joueur
// Relie l'UI Toolkit (BootAuth.uxml) a SessionService.
// Trois panneaux :
// - steam-panel    : Steam tourne -> connexion automatique par
//                    ticket ; en cas d'echec, RETRY / PLAY OFFLINE
//                    ou bascule vers un compte email ;
// - login-panel    : email / mot de passe (compte standard), avec
//                    PLAY OFFLINE (dernier compte vu sur cette
//                    machine, sinon invite) ;
// - register-panel : creation d'un compte standard.
// Le chemin est choisi par SessionService.ShouldUseSteam (client
// Steam present et pas de preference « compte email »).
// Le client PocketBase est cree ici et partage par les scenes.
// =========================================================

[RequireComponent(typeof(UIDocument))]
public class BootAuthController : MonoBehaviour
{
    [Header("PocketBase")]
    [Tooltip("URL de base du serveur PocketBase, ex: https://pb.mondomaine.com")]
    [SerializeField] private string pocketBaseUrl = "https://pb.example.com";

    [Header("Navigation")]
    [SerializeField] private string nextSceneName = "MainMenu";

    private PocketBaseClient client;

    // Elements UI
    private VisualElement authContainer;
    private VisualElement steamPanel;
    private VisualElement loginPanel;
    private VisualElement registerPanel;
    private TextField loginEmail;
    private TextField loginPassword;
    private TextField registerEmail;
    private TextField registerUsername;
    private TextField registerPassword;
    private TextField registerPasswordConfirm;
    private Label statusLabel;

    // Panneau Steam
    private Label steamTitle;
    private VisualElement steamAvatar;
    private VisualElement steamAvatarIcon;
    private Label steamName;
    private Label steamMessage;
    private Button steamRetry;
    private Button steamOffline;
    private VisualElement steamHint;

    private bool busy;
    private bool leaving;

    private void Awake()
    {
        // Client partage ; conserve si la scene Boot est rechargee (reconnexion)
        client = PocketBaseClient.Instance != null && PocketBaseClient.Instance.BaseUrl == pocketBaseUrl.TrimEnd('/')
            ? PocketBaseClient.Instance
            : new PocketBaseClient(pocketBaseUrl);
        PocketBaseClient.Instance = client;
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        authContainer = root.Q<VisualElement>("auth-container");
        steamPanel = root.Q<VisualElement>("steam-panel");
        loginPanel = root.Q<VisualElement>("login-panel");
        registerPanel = root.Q<VisualElement>("register-panel");
        loginEmail = root.Q<TextField>("login-email");
        loginPassword = root.Q<TextField>("login-password");
        registerEmail = root.Q<TextField>("register-email");
        registerUsername = root.Q<TextField>("register-username");
        registerPassword = root.Q<TextField>("register-password");
        registerPasswordConfirm = root.Q<TextField>("register-password-confirm");
        statusLabel = root.Q<Label>("status-label");

        steamTitle = root.Q<Label>("steam-title");
        steamAvatar = root.Q<VisualElement>("steam-avatar");
        steamAvatarIcon = root.Q<VisualElement>("steam-avatar-icon");
        steamName = root.Q<Label>("steam-name");
        steamMessage = root.Q<Label>("steam-message");
        steamRetry = root.Q<Button>("steam-retry");
        steamOffline = root.Q<Button>("steam-offline");
        steamHint = root.Q<VisualElement>("steam-hint");

        root.Q<Button>("login-button").clicked += OnLoginClicked;
        root.Q<Button>("register-button").clicked += OnRegisterClicked;
        root.Q<Button>("guest-button").clicked += OnPlayOfflineClicked;
        root.Q<Button>("forgot-password").clicked += OnForgotPasswordClicked;
        root.Q<Button>("goto-register").clicked += ShowRegisterPanel;
        root.Q<Button>("goto-login").clicked += ShowLoginPanel;
        root.Q<Button>("goto-standard").clicked += OnUseStandardAccountClicked;
        root.Q<Button>("goto-steam").clicked += OnUseSteamClicked;
        if (steamRetry != null) steamRetry.clicked += OnSteamRetryClicked;
        if (steamOffline != null) steamOffline.clicked += OnPlayOfflineClicked;

        BindPasswordToggle(root, "login-password-toggle", loginPassword);
        BindPasswordToggle(root, "register-password-toggle", registerPassword);
        BindPasswordToggle(root, "register-password-confirm-toggle", registerPasswordConfirm);

        SteamIdentityService.AvatarReady += RefreshSteamIdentity;

        // Le lien « Sign in with Steam instead » n'a de sens que si Steam tourne
        SetVisible(steamHint, SteamBootstrap.IsAvailable);

        // Panneau de depart : decide dans Start (SessionService lit l'etat de Steam)
        SetVisible(steamPanel, false);
        SetVisible(loginPanel, false);
        SetVisible(registerPanel, false);
    }

    private void OnDisable()
    {
        SteamIdentityService.AvatarReady -= RefreshSteamIdentity;
    }

    private void Start()
    {
        // Retour sur Boot alors qu'une session en ligne existe (ne devrait pas arriver) : on passe
        if (SessionService.IsOnline)
        {
            LoadNextScene();
            return;
        }

        if (SessionService.ShouldUseSteam)
        {
            BeginSteamSignIn();
            return;
        }

        ShowLoginPanel();
        PrefillLastEmail();
#if UNITY_EDITOR
        TryDevAutoLogin();
#endif
    }

    // =========================================================
    // STEAM
    // =========================================================

    private async void BeginSteamSignIn()
    {
        ShowSteamPanel();
        RefreshSteamIdentity();
        SetSteamBusy(true, "Checking your Steam ticket with the server…");
        ShowInfo("");

        var result = await SessionService.SignInWithSteamAsync();
        if (this == null)
            return;

        if (result.ok)
        {
            Toast.Success($"Signed in with Steam. Welcome back, {SessionService.DisplayName}.");
            ShowSuccess($"Welcome back, {SessionService.DisplayName}.");
            LoadNextScene();
            return;
        }

        // Echec : Steam hors ligne, serveur injoignable ou ticket refuse
        string message;
        if (result.steamOffline)
            message = "Steam is in offline mode, so the server can't verify your account. You can play offline with bots; your robots are saved on this computer and will sync later.";
        else if (result.networkError)
            message = "The server can't be reached right now. You can retry, or play offline with bots.";
        else
            message = $"The server didn't accept the Steam sign-in. {result.error}";

        SetSteamBusy(false, message);
        if (!result.steamOffline && !result.networkError)
            ShowError(result.error);
    }

    private void OnSteamRetryClicked()
    {
        if (busy || leaving)
            return;
        if (!SteamBootstrap.IsAvailable)
        {
            ShowError("Steam isn't running anymore. Start Steam, or use an email account.");
            return;
        }
        BeginSteamSignIn();
    }

    private void RefreshSteamIdentity()
    {
        if (steamName != null)
            steamName.text = string.IsNullOrEmpty(SteamIdentityService.PersonaName) ? "Steam player" : SteamIdentityService.PersonaName;

        if (steamAvatar == null)
            return;
        var texture = SteamIdentityService.Avatar;
        if (texture == null && SessionService.Identity != null && SessionService.Identity.IsSteam && SessionService.Avatar != null)
            texture = SessionService.Avatar.texture; // avatar en cache (Steam hors ligne)
        if (texture != null)
        {
            steamAvatar.style.backgroundImage = new StyleBackground(texture);
            SetVisible(steamAvatarIcon, false);
        }
        else
        {
            steamAvatar.style.backgroundImage = StyleKeyword.None;
            SetVisible(steamAvatarIcon, true);
        }
    }

    private void SetSteamBusy(bool connecting, string message)
    {
        busy = connecting;
        if (steamTitle != null)
            steamTitle.text = connecting ? "SIGNING IN WITH STEAM" : "STEAM SIGN-IN FAILED";
        if (steamMessage != null)
            steamMessage.text = message;
        SetVisible(steamRetry, !connecting);
        SetVisible(steamOffline, !connecting);
    }

    // =========================================================
    // BASCULES ENTRE PANNEAUX
    // =========================================================

    private void ShowSteamPanel()
    {
        SetVisible(steamPanel, true);
        SetVisible(loginPanel, false);
        SetVisible(registerPanel, false);
        ShowInfo("");
    }

    private void ShowLoginPanel()
    {
        SetVisible(steamPanel, false);
        SetVisible(loginPanel, true);
        SetVisible(registerPanel, false);
        ShowInfo("");
        loginEmail.Focus();
    }

    private void ShowRegisterPanel()
    {
        SetVisible(steamPanel, false);
        SetVisible(loginPanel, false);
        SetVisible(registerPanel, true);
        ShowInfo("");
        registerEmail.Focus();
    }

    // Steam present mais le joueur prefere son compte email : memorise pour les prochains lancements
    private void OnUseStandardAccountClicked()
    {
        if (busy)
            return;
        SessionService.PreferStandardAccount = true;
        ShowLoginPanel();
        PrefillLastEmail();
        ShowInfo("Email account selected. You can switch back to Steam from this screen.");
    }

    private void OnUseSteamClicked()
    {
        if (busy)
            return;
        if (!SteamBootstrap.IsAvailable)
        {
            ShowError("Steam isn't running. Start Steam and relaunch the game.");
            return;
        }
        SessionService.PreferStandardAccount = false;
        BeginSteamSignIn();
    }

    private void PrefillLastEmail()
    {
        var last = IdentityCache.LoadLast();
        if (last != null && last.method == AuthMethod.Password && !string.IsNullOrEmpty(last.email) && string.IsNullOrEmpty(loginEmail.value))
            loginEmail.value = last.email;
    }

#if UNITY_EDITOR
    // =========================================================
    // AUTO-LOGIN DEV (editeur uniquement, chemin compte standard)
    // Lit dev_autologin.json a la racine du projet (gitignore,
    // jamais inclus dans un build) et lance la connexion.
    // =========================================================

    [System.Serializable]
    private class DevAutoLoginConfig
    {
        public string email;
        public string password;
    }

    private void TryDevAutoLogin()
    {
        string path = System.IO.Path.Combine(
            System.IO.Directory.GetParent(Application.dataPath).FullName,
            "dev_autologin.json");

        if (!System.IO.File.Exists(path))
            return;

        DevAutoLoginConfig config;
        try
        {
            config = JsonUtility.FromJson<DevAutoLoginConfig>(System.IO.File.ReadAllText(path));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BootAuth] dev_autologin.json illisible : {e.Message}");
            return;
        }

        if (config == null || string.IsNullOrEmpty(config.email))
            return;

        loginEmail.value = config.email;

        if (string.IsNullOrEmpty(config.password))
        {
            ShowInfo("Dev auto sign-in: add your \"password\" to dev_autologin.json.");
            return;
        }

        loginPassword.value = config.password;
        Debug.Log($"[BootAuth] Auto-login dev : {config.email}");
        OnLoginClicked();
    }
#endif

    // =========================================================
    // COMPTE STANDARD
    // =========================================================

    private async void OnLoginClicked()
    {
        if (busy || leaving)
            return;
        string email = loginEmail.value.Trim();
        string password = loginPassword.value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Enter your email and password.");
            return;
        }

        SetBusy(true);
        ShowInfo("Signing in…");
        var toast = Toast.Loading("Signing in…");

        var result = await SessionService.SignInWithPasswordAsync(email, password);
        if (result.ok)
        {
            // Le toast survit au changement de scene : affiche en arrivant au menu
            toast.Success($"Signed in. Welcome back, {SessionService.DisplayName}.");
            if (this == null) return;
            ShowSuccess($"Welcome back, {SessionService.DisplayName}.");
            LoadNextScene();
            return;
        }

        toast.Error($"Couldn't sign in. {result.error}");
        if (this == null) return;
        SetBusy(false);
        ShowError(result.networkError
            ? "Can't reach the server. Check your connection, or play offline."
            : result.error);
    }

    private async void OnRegisterClicked()
    {
        if (busy || leaving)
            return;
        string email = registerEmail.value.Trim();
        string username = registerUsername.value.Trim();
        string password = registerPassword.value;
        string passwordConfirm = registerPasswordConfirm.value;

        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            ShowError("Enter a valid email address.");
            return;
        }
        if (username.Length < 3)
        {
            ShowError("Your username needs at least 3 characters.");
            return;
        }
        if (password.Length < 8)
        {
            ShowError("Your password needs at least 8 characters.");
            return;
        }
        if (password != passwordConfirm)
        {
            ShowError("The passwords don't match.");
            return;
        }
        SetBusy(true);
        ShowInfo("Creating your account…");
        var toast = Toast.Loading("Creating your account…");

        try
        {
            await client.RegisterAsync(email, username, password, passwordConfirm);
            if (this == null) return;

            // Connexion directe apres l'inscription
            toast.Info("Account created. Signing in…");
            var result = await SessionService.SignInWithPasswordAsync(email, password);
            if (!result.ok)
                throw new PocketBaseException(0, result.error);
            toast.Success($"Account created. Welcome, {SessionService.DisplayName}.");
            if (this == null) return;

            ShowSuccess($"Account created. Welcome, {SessionService.DisplayName}.");
            LoadNextScene();
        }
        catch (PocketBaseException e)
        {
            toast.Error($"Couldn't create your account. {e.Message}");
            if (this == null) return;
            SetBusy(false);
            ShowError(e.Message);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            toast.Error("Couldn't create your account. Something went wrong; see the Console.");
            if (this == null) return;
            SetBusy(false);
            ShowError("Unexpected error — see the Console.");
        }
    }

    private async void OnForgotPasswordClicked()
    {
        if (busy)
            return;
        string email = loginEmail.value.Trim();
        if (string.IsNullOrEmpty(email))
        {
            ShowError("Enter your email above first.");
            return;
        }

        SetBusy(true);
        ShowInfo("Sending the reset email…");
        var toast = Toast.Loading("Sending the reset email…");

        try
        {
            await client.RequestPasswordResetAsync(email);
            toast.Success("Reset email sent. Check your inbox.");
            if (this == null) return;
            ShowSuccess("Reset email sent. Check your inbox.");
        }
        catch (PocketBaseException e)
        {
            toast.Error($"Couldn't send the reset email. {e.Message}");
            if (this == null) return;
            ShowError(e.Message);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            toast.Error("Couldn't send the reset email. Something went wrong; see the Console.");
            if (this == null) return;
            ShowError("Unexpected error — see the Console.");
        }
        finally
        {
            if (this != null)
                SetBusy(false);
        }
    }

    // =========================================================
    // HORS LIGNE
    // =========================================================

    // Steam en cache, sinon dernier compte vu sur cette machine, sinon invite
    private void OnPlayOfflineClicked()
    {
        if (busy || leaving)
            return;

        SessionService.ContinueOffline();
        var identity = SessionService.Identity;
        if (identity != null && identity.IsGuest)
            Toast.Info("Playing as a guest: your robots are saved on this computer only.");
        else
            Toast.Info($"Playing offline as {SessionService.DisplayName}. Your robots will sync when you're back online.");
        LoadNextScene();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void BindPasswordToggle(VisualElement root, string buttonName, TextField field)
    {
        var button = root.Q<Button>(buttonName);
        button.clicked += () =>
        {
            field.isPasswordField = !field.isPasswordField;
            // IconButton du DS : pictogramme visible / hidden, nom accessible dans le tooltip
            button.EnableInClassList("is-visible", !field.isPasswordField);
            button.tooltip = field.isPasswordField ? "Show password" : "Hide password";
        };
        button.text = "";
        button.tooltip = "Show password";
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        authContainer.SetEnabled(!value);
    }

    private void LoadNextScene()
    {
        if (leaving)
            return;
        leaving = true;
        SceneManager.LoadScene(nextSceneName);
    }

    private void ShowError(string message) => SetStatus(message, "status--error");
    private void ShowSuccess(string message) => SetStatus(message, "status--ok");
    private void ShowInfo(string message) => SetStatus(message, null);

    private void SetStatus(string message, string ussClass)
    {
        // Notice du DS : fond *-soft, bord et texte de la tonalite, pictogramme
        statusLabel.text = message;
        statusLabel.RemoveFromClassList("status--error");
        statusLabel.RemoveFromClassList("status--ok");
        statusLabel.RemoveFromClassList("status--info");
        statusLabel.EnableInClassList("status--hidden", string.IsNullOrEmpty(message));
        if (!string.IsNullOrEmpty(message))
            statusLabel.AddToClassList(string.IsNullOrEmpty(ussClass) ? "status--info" : ussClass);
    }
}
