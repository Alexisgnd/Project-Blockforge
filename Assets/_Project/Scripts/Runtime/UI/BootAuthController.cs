using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// =========================================================
// ECRAN D'AUTHENTIFICATION (scene Boot)
// Relie l'UI Toolkit (BootAuth.uxml) au client PocketBase.
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
    private VisualElement loginPanel;
    private VisualElement registerPanel;
    private TextField loginEmail;
    private TextField loginPassword;
    private TextField registerEmail;
    private TextField registerUsername;
    private TextField registerPassword;
    private TextField registerPasswordConfirm;
    private Label statusLabel;

    private void Awake()
    {
        client = new PocketBaseClient(pocketBaseUrl);
        PocketBaseClient.Instance = client;
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        authContainer = root.Q<VisualElement>("auth-container");
        loginPanel = root.Q<VisualElement>("login-panel");
        registerPanel = root.Q<VisualElement>("register-panel");
        loginEmail = root.Q<TextField>("login-email");
        loginPassword = root.Q<TextField>("login-password");
        registerEmail = root.Q<TextField>("register-email");
        registerUsername = root.Q<TextField>("register-username");
        registerPassword = root.Q<TextField>("register-password");
        registerPasswordConfirm = root.Q<TextField>("register-password-confirm");
        statusLabel = root.Q<Label>("status-label");

        root.Q<Button>("login-button").clicked += OnLoginClicked;
        root.Q<Button>("register-button").clicked += OnRegisterClicked;
        root.Q<Button>("guest-button").clicked += OnGuestClicked;
        root.Q<Button>("forgot-password").clicked += OnForgotPasswordClicked;
        root.Q<Button>("goto-register").clicked += ShowRegisterPanel;
        root.Q<Button>("goto-login").clicked += ShowLoginPanel;

        BindPasswordToggle(root, "login-password-toggle", loginPassword);
        BindPasswordToggle(root, "register-password-toggle", registerPassword);
        BindPasswordToggle(root, "register-password-confirm-toggle", registerPasswordConfirm);

        // Par defaut : seul le bloc connexion est visible
        ShowLoginPanel();
    }

    private void Start()
    {
#if UNITY_EDITOR
        TryDevAutoLogin();
#endif
    }

#if UNITY_EDITOR
    // =========================================================
    // AUTO-LOGIN DEV (editeur uniquement)
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
    // BASCULE CONNEXION / INSCRIPTION
    // =========================================================

    private void ShowLoginPanel()
    {
        loginPanel.style.display = DisplayStyle.Flex;
        registerPanel.style.display = DisplayStyle.None;
        ShowInfo("");
        loginEmail.Focus();
    }

    private void ShowRegisterPanel()
    {
        loginPanel.style.display = DisplayStyle.None;
        registerPanel.style.display = DisplayStyle.Flex;
        ShowInfo("");
        registerEmail.Focus();
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    private async void OnLoginClicked()
    {
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

        try
        {
            var auth = await client.LoginAsync(email, password);
            // Le toast survit au changement de scene : affiche en arrivant au menu
            toast.Success($"Signed in. Welcome back, {DisplayName(auth.record)}.");
            if (this == null) return;

            ShowSuccess($"Welcome back, {DisplayName(auth.record)}.");
            LoadNextScene();
        }
        catch (PocketBaseException e)
        {
            string message = e.StatusCode == 400 ? "Wrong email or password." : e.Message;
            toast.Error($"Couldn't sign in. {message}");
            if (this == null) return;
            SetBusy(false);
            ShowError(message);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            toast.Error("Couldn't sign in. Something went wrong; see the Console.");
            if (this == null) return;
            SetBusy(false);
            ShowError("Erreur inattendue — voir la Console.");
        }
    }

    private async void OnRegisterClicked()
    {
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
            var auth = await client.LoginAsync(email, password);
            toast.Success($"Account created. Welcome, {DisplayName(auth.record)}.");
            if (this == null) return;

            ShowSuccess($"Account created. Welcome, {DisplayName(auth.record)}.");
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
            ShowError("Erreur inattendue — voir la Console.");
        }
    }

    private async void OnForgotPasswordClicked()
    {
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
            ShowError("Erreur inattendue — voir la Console.");
        }
        finally
        {
            if (this != null)
                SetBusy(false);
        }
    }

    private void OnGuestClicked()
    {
        // La sauvegarde des robots exige un compte : pas d'acces invite pour l'instant
        ShowError("You need an account to play: your robots are saved online.");
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

    private static string DisplayName(PocketBaseUser user)
    {
        if (!string.IsNullOrEmpty(user.username)) return user.username;
        if (!string.IsNullOrEmpty(user.name)) return user.name;
        return user.email;
    }

    private void SetBusy(bool busy)
    {
        authContainer.SetEnabled(!busy);
    }

    private void LoadNextScene()
    {
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
