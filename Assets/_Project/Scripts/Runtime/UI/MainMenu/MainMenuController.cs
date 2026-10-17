using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// CHEF D'ORCHESTRE DU MENU D'ACCUEIL
// Relie la selection de robot du joueur a l'affichage, gere les
// sections de la navigation du haut (Home, Play, Build, Tech
// tree, Workshop, Store) et les infos du joueur dans le header
// (puce joueur, credits).
// =========================================================

public class MainMenuController : MonoBehaviour
{
    // Identifiants des sections (noms des onglets « Nav_<Section> »)
    public const string SectionHome = "Home";
    public const string SectionPlay = "Play";
    public const string SectionBuild = "Build";
    public const string SectionTechTree = "TechTree";
    public const string SectionWorkshop = "Workshop";
    public const string SectionStore = "Store";

    [Header("References (remplies par le setup)")]
    public RobotSlotManager slotManager;
    public RobotStatsUI statsUI;
    public TMP_Text bayLabelText;
    public TMP_Text robotNameText;
    public TMP_Text playerNameText;
    [Tooltip("Solde de credits du header (readout, grade-masterwork)")]
    public TMP_Text creditsText;

    [Header("Ecrans")]
    public GameObject homeScreen;
    public PlayMenuController playMenu;
    public BuildMenuController buildMenu;

    [Header("Navigation")]
    public string arenaSceneName = "Map_Test";
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
        // Pas encore d'economie : solde a zero, deja au format du DS (readout, milliers groupes)
        if (creditsText != null)
            creditsText.text = BlockforgeTheme.Num(0);

        SetActiveTab(SectionHome);

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
        // Pas encore d'ecran Play ? On lance directement l'arene.
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
        SetActiveTab(SectionPlay);
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
        SetActiveTab(SectionBuild);
    }

    public void ShowHome()
    {
        if (playMenu != null)
            playMenu.Close();
        if (buildMenu != null)
            buildMenu.Close();
        if (homeScreen != null)
            homeScreen.SetActive(true);
        SetActiveTab(SectionHome);

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
            // Anciens identifiants francais des scenes generees avant la refonte du header
            case "ACCUEIL":
            case SectionHome:
                ShowHome();
                break;
            case "JOUER":
            case SectionPlay:
                ShowPlayMenu();
                break;
            case "CONSTRUIRE":
            case SectionBuild:
                ShowBuildMenu();
                break;
            case SectionTechTree:
                Toast.Info("Tech tree is coming soon.");
                break;
            case SectionWorkshop:
                Toast.Info("Workshop is coming soon.");
                break;
            case SectionStore:
                Toast.Info("Store is coming soon.");
                break;
            default:
                Debug.Log($"[MainMenu] Section '{section}' — à venir.");
                break;
        }
    }
}
