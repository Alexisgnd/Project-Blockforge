using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// CHEF D'ORCHESTRE DU MENU D'ACCUEIL
// Relie la selection de robot du joueur a l'affichage, gere les
// boutons Jouer / Construire et les infos du joueur.
// =========================================================

public class MainMenuController : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public RobotSlotManager slotManager;
    public RobotStatsUI statsUI;
    public TMP_Text bayLabelText;
    public TMP_Text robotNameText;
    public TMP_Text playerNameText;

    [Header("Ecrans")]
    public GameObject homeScreen;
    public PlayMenuController playMenu;
    public BuildMenuController buildMenu;

    [Header("Navigation")]
    public string arenaSceneName = "Arena";
    public string garageSceneName = "Garage";

    [Header("Onglets (auto-détectés si vide)")]
    public Transform navButtonsRoot;

    // Onglet -> composant DS (Tabs nav), construit depuis les enfants "Nav_XXX"
    private readonly Dictionary<string, DSElement> navTabs = new();

    private void Awake()
    {
        // Scene reservee aux joueurs connectes
        if (!AuthGuard.EnsureAuthenticated())
            return;

        if (navButtonsRoot == null)
        {
            var found = GameObject.Find("NavButtons");
            if (found != null)
                navButtonsRoot = found.transform;
        }

        if (navButtonsRoot == null)
            return;

        foreach (Transform child in navButtonsRoot)
        {
            string section = child.name.StartsWith("Nav_") ? child.name.Substring(4) : child.name;
            if (child.TryGetComponent<DSElement>(out var element))
                navTabs[section] = element;
        }
    }

    private void SetActiveTab(string section)
    {
        // Tabs nav : souligne en accent, jamais de rangee sans selection
        foreach (var pair in navTabs)
            pair.Value.Selected = pair.Key == section;
    }

    private void OnEnable()
    {
        if (slotManager != null)
        {
            slotManager.SelectionChanged += OnRobotSelected;
            slotManager.EmptySlotClicked += OnEmptySlotClicked;
        }
    }

    private void OnDisable()
    {
        if (slotManager != null)
        {
            slotManager.SelectionChanged -= OnRobotSelected;
            slotManager.EmptySlotClicked -= OnEmptySlotClicked;
        }
    }

    private void Start()
    {
        // Pseudo du joueur connecte (session PocketBase), sinon invite
        var user = PocketBaseClient.Instance?.User;
        string pseudo = user != null && !string.IsNullOrEmpty(user.name) ? user.name : "Guest";
        if (playerNameText != null)
            playerNameText.text = pseudo;

        SetActiveTab("ACCUEIL");

        // En attendant la reponse de PocketBase (RobotSlotManager.Refresh)
        if (bayLabelText != null)
            bayLabelText.text = "Hangar";
        if (robotNameText != null)
            robotNameText.text = "Loading…";
        if (statsUI != null)
            statsUI.Clear();
    }

    // Robot du joueur selectionne dans les slots de l'accueil (null = aucun robot)
    private void OnRobotSelected(RobotRecord robot, int slotIndex)
    {
        if (bayLabelText != null)
            bayLabelText.text = robot != null ? $"Bay {slotIndex + 1:00}" : "Empty hangar";
        if (robotNameText != null)
            robotNameText.text = robot != null ? (robot.name ?? "Robot") : "Build your first robot in a free bay.";
        if (statsUI != null)
            statsUI.Refresh(robot);
    }

    // Slot vide sur l'accueil : la creation se fait depuis l'ecran Construire
    private void OnEmptySlotClicked(int slotIndex)
    {
        ShowBuildMenu();
    }

    // ---------- Ecrans ----------

    public void ShowPlayMenu()
    {
        // Pas encore d'ecran modes de jeu ? On lance directement l'arene.
        if (playMenu == null)
        {
            SceneManager.LoadScene(arenaSceneName);
            return;
        }

        if (homeScreen != null)
            homeScreen.SetActive(false);
        if (buildMenu != null)
            buildMenu.Close();
        playMenu.Open();
        SetActiveTab("JOUER");
    }

    public void ShowBuildMenu()
    {
        // Pas encore d'ecran de slots ? On ouvre directement le garage.
        if (buildMenu == null)
        {
            SceneManager.LoadScene(garageSceneName);
            return;
        }

        if (homeScreen != null)
            homeScreen.SetActive(false);
        if (playMenu != null)
            playMenu.Close();
        buildMenu.Open();
        SetActiveTab("CONSTRUIRE");
    }

    public void ShowHome()
    {
        if (playMenu != null)
            playMenu.Close();
        if (buildMenu != null)
            buildMenu.Close();
        if (homeScreen != null)
            homeScreen.SetActive(true);
        SetActiveTab("ACCUEIL");

        // Un robot a pu etre cree ou supprime depuis Construire
        if (slotManager != null)
            slotManager.Refresh();
    }

    // ---------- Boutons ----------

    public void OnPlayClicked()
    {
        ShowPlayMenu();
    }

    public void OnBuildClicked()
    {
        ShowBuildMenu();
    }

    public void OnRobotSettingsClicked()
    {
        Debug.Log("[MainMenu] Paramètres du robot — à venir.");
    }

    public void OnNavClicked(string section)
    {
        switch (section)
        {
            case "ACCUEIL":
                ShowHome();
                break;
            case "JOUER":
                ShowPlayMenu();
                break;
            case "CONSTRUIRE":
                ShowBuildMenu();
                break;
            default:
                Debug.Log($"[MainMenu] Section '{section}' — à venir.");
                break;
        }
    }
}
