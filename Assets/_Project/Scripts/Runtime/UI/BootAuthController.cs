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
            ShowError("Entre ton email et ton mot de passe.");
            return;
        }

        SetBusy(true);
        ShowInfo("Connexion...");

        try
        {
            var auth = await client.LoginAsync(email, password);
            if (this == null) return;

            ShowSuccess($"Bon retour, {DisplayName(auth.record)} !");
            LoadNextScene();
        }
        catch (PocketBaseException e)
        {
            if (this == null) return;
            SetBusy(false);
            ShowError(e.StatusCode == 400 ? "Email ou mot de passe incorrect." : e.Message);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
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
            ShowError("Entre une adresse email valide.");
            return;
        }
        if (username.Length < 3)
        {
            ShowError("Le pseudo doit faire au moins 3 caractères.");
            return;
        }
        if (password.Length < 8)
        {
            ShowError("Le mot de passe doit faire au moins 8 caractères.");
            return;
        }
        if (password != passwordConfirm)
        {
            ShowError("Les mots de passe ne correspondent pas.");
            return;
        }
        SetBusy(true);
        ShowInfo("Création du compte...");

        try
        {
            await client.RegisterAsync(email, username, password, passwordConfirm);
            if (this == null) return;

            // Connexion directe apres l'inscription
            var auth = await client.LoginAsync(email, password);
            if (this == null) return;

            ShowSuccess($"Compte créé. Bienvenue, {DisplayName(auth.record)} !");
            LoadNextScene();
        }
        catch (PocketBaseException e)
        {
            if (this == null) return;
            SetBusy(false);
            ShowError(e.Message);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
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
            ShowError("Entre d'abord ton email ci-dessus.");
            return;
        }

        SetBusy(true);
        ShowInfo("Envoi de l'email de réinitialisation...");

        try
        {
            await client.RequestPasswordResetAsync(email);
            if (this == null) return;
            ShowSuccess("Email de réinitialisation envoyé. Vérifie ta boîte mail.");
        }
        catch (PocketBaseException e)
        {
            if (this == null) return;
            ShowError(e.Message);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
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
        ShowError("Un compte est requis pour jouer (sauvegarde en ligne des robots).");
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
            button.text = field.isPasswordField ? "VOIR" : "CACHER";
        };
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
        statusLabel.text = message;
        statusLabel.RemoveFromClassList("status--error");
        statusLabel.RemoveFromClassList("status--ok");
        if (!string.IsNullOrEmpty(ussClass))
            statusLabel.AddToClassList(ussClass);
    }
}
