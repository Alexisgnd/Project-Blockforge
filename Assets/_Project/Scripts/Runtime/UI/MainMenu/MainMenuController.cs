using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// CHEF D'ORCHESTRE DU MENU D'ACCUEIL
// Relie la selection de robot a l'affichage, gere les
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
    public Color tabActiveBg = new(0.09f, 0.16f, 0.28f, 1f);
    public Color tabTextActive = new(0.86f, 0.91f, 0.96f);
    public Color tabTextInactive = new(0.48f, 0.56f, 0.66f);

    // Onglet -> (fond, libelle), construit depuis les enfants "Nav_XXX"
    private readonly Dictionary<string, (Image bg, TMP_Text label)> navTabs = new();

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
            var bg = child.GetComponent<Image>();
            var label = child.GetComponentInChildren<TMP_Text>();
            if (bg != null && label != null)
                navTabs[section] = (bg, label);
        }
    }

    private void SetActiveTab(string section)
    {
        foreach (var pair in navTabs)
        {
            bool active = pair.Key == section;
            pair.Value.bg.color = active ? tabActiveBg : Color.clear;
            pair.Value.label.color = active ? tabTextActive : tabTextInactive;
        }
    }

    private void OnEnable()
    {
        if (slotManager != null)
            slotManager.SelectionChanged += OnRobotSelected;
    }

    private void OnDisable()
    {
        if (slotManager != null)
            slotManager.SelectionChanged -= OnRobotSelected;
    }

    private void Start()
    {
        // Pseudo du joueur connecte (session PocketBase), sinon invite
        var user = PocketBaseClient.Instance?.User;
        string pseudo = user != null && !string.IsNullOrEmpty(user.name) ? user.name : "INVITÉ";
        if (playerNameText != null)
            playerNameText.text = pseudo.ToUpperInvariant();

        SetActiveTab("ACCUEIL");
    }

    private void OnRobotSelected(RobotPreset preset)
    {
        if (bayLabelText != null)
            bayLabelText.text = preset.bayLabel;
        if (robotNameText != null)
            robotNameText.text = preset.robotName;
        if (statsUI != null)
            statsUI.Refresh(preset);
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
