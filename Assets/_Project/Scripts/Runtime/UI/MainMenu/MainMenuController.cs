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
// La puce joueur du header (PlayerChip) affiche le pseudo et
// l'avatar Steam quand il y en a un, et l'etat de la session
// (en ligne / hors ligne / invite) ; un clic hors ligne retente
// la connexion.
// Tech tree, Workshop et Store sont des HubScreen crees au
// lancement sous le header (HubScreenHost) : ils restent vivants
// entre deux onglets et se fondent en 220 ms. Le header serialise
// est adopte par TopBar (Q / E et gachettes pour changer d'onglet) ;
// le solde de credits suit le portefeuille (MetaServices.Wallet).
// Fond de menu : un aplat surface-base fixe (MenuBackdrop) passe
// sous les ecrans Play, Build et du hub. Il s'affiche sans fondu,
// seul le contenu des ecrans se fond : le hangar 3D ne transparait
// plus entre deux onglets. Il disparait sur Home (hangar visible).
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
    public string arenaSceneName = "Map_MARS";
    public string garageSceneName = "Garage";

    [Header("Onglets (auto-détectés si vide)")]
    public Transform navButtonsRoot;

    // Onglet -> composant DS (Tabs nav), construit depuis les enfants "Nav_XXX"
    private readonly Dictionary<string, DSElement> navTabs = new();

    // Ecrans du hub (Tech tree, Workshop, Store) et header adopte
    private HubScreenHost hubScreens;
    private TopBar topBar;
    private Blockforge.Meta.IWallet wallet;
    private UnityEngine.UI.Image menuBackdrop;

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
        if (topBar != null && TopBar.TryParse(section, out var s))
            topBar.Active = s;
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

    private void OnDestroy()
    {
        if (wallet != null)
            wallet.CreditsChanged -= OnCreditsChanged;
    }

    private void OnCreditsChanged(int credits)
    {
        if (creditsText != null)
            creditsText.text = BlockforgeTheme.Num(credits);
    }

    // Hote des ecrans du hub, sous le header ; le header devient une TopBar (Q / E)
    private void SetupHubScreens()
    {
        var nav = navButtonsRoot != null ? navButtonsRoot.parent : GameObject.Find("TopNavigation")?.transform;
        if (nav == null)
            return;
        topBar = TopBar.Adopt(nav);
        topBar.Selected += section => OnNavClicked(TopBar.KeyOf(section));
        // Puce joueur : pseudo, avatar Steam et pastille d'etat (clic = reconnexion)
        PlayerChip.Attach(nav.Find("Right/Player"));
        var canvas = nav.parent;
        hubScreens = HubScreenHost.Create(canvas, topBar, BlockforgeTheme.TopBar.Height);
        CreateMenuBackdrop(canvas);
        UIKit.EnsureEventSystem();
    }

    // Aplat opaque sous tous les ecrans (premier enfant du canvas) : jamais fondu
    private void CreateMenuBackdrop(Transform canvas)
    {
        var kit = UIKit.Dark;
        menuBackdrop = kit.Image(canvas, "MenuBackdrop", kit.P.SurfaceBase);
        UIKit.Stretch(menuBackdrop.rectTransform);
        menuBackdrop.raycastTarget = true;
        menuBackdrop.transform.SetAsFirstSibling();
        menuBackdrop.gameObject.SetActive(false);
    }

    private void SetMenuBackdrop(bool visible)
    {
        if (menuBackdrop != null)
            menuBackdrop.gameObject.SetActive(visible);
    }

    public void ShowHubScreen(HubSection section)
    {
        if (hubScreens == null || !hubScreens.Handles(section))
        {
            Toast.Info($"{TopBar.LabelOf(section)} is coming soon.");
            return;
        }
        if (homeScreen != null)
            homeScreen.SetActive(false);
        if (playMenu != null)
            playMenu.Close();
        if (buildMenu != null)
            buildMenu.Close();
        SetMenuBackdrop(true);
        hubScreens.Show(section);
        SetActiveTab(TopBar.KeyOf(section));
    }

    // Les ecrans historiques reprennent la main : le hote s'efface, l'ecran arrive en fondu
    private void LeaveHubScreens(GameObject shown)
    {
        if (hubScreens != null)
            hubScreens.HideAll();
        if (shown == null)
            return;
        var group = UIKit.GetOrAdd<CanvasGroup>(shown);
        group.alpha = 0f;
        StartCoroutine(UIAnim.Fade(group, 1f, BlockforgeTheme.Fade(BlockforgeTheme.MotionScreen)));
    }

    private void Start()
    {
        // Pseudo du joueur (Steam, compte standard ou invite) ; PlayerChip le tient a jour ensuite
        if (playerNameText != null)
            playerNameText.text = SessionService.DisplayName;
        // Pas encore d'economie : solde a zero, deja au format du DS (readout, milliers groupes)
        wallet = Blockforge.Meta.MetaServices.Wallet;
        wallet.CreditsChanged += OnCreditsChanged;
        OnCreditsChanged(wallet.Credits);
        SetupHubScreens();

        SetActiveTab(SectionHome);

        // Retour d'une recherche de match annulee (scene Matchmaking_Space) : ecran Play
        if (PlayMenuController.ReopenOnReturn)
        {
            PlayMenuController.ReopenOnReturn = false;
            ShowPlayMenu();
        }

        // Le cache local (RobotStore) peut avoir deja repondu dans cette frame :
        // on affiche alors directement le robot selectionne au lieu du texte d'attente
        if (slotManager != null && slotManager.SelectedIndex >= 0)
        {
            OnRobotSelected(slotManager.SelectedRobot, slotManager.SelectedIndex);
            return;
        }

        // En attendant la reponse de RobotSlotManager.Refresh
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
            SceneLoader.Load(arenaSceneName);
            return;
        }

        if (homeScreen != null)
            homeScreen.SetActive(false);
        if (buildMenu != null)
            buildMenu.Close();
        SetMenuBackdrop(true);
        playMenu.Open();
        LeaveHubScreens(playMenu.gameObject);
        SetActiveTab(SectionPlay);
    }

    public void ShowBuildMenu()
    {
        // Pas encore d'ecran de slots ? On ouvre directement le garage.
        if (buildMenu == null)
        {
            SceneLoader.Load(garageSceneName);
            return;
        }

        if (homeScreen != null)
            homeScreen.SetActive(false);
        if (playMenu != null)
            playMenu.Close();
        SetMenuBackdrop(true);
        buildMenu.Open();
        LeaveHubScreens(buildMenu.gameObject);
        SetActiveTab(SectionBuild);
    }

    public void ShowHome()
    {
        if (playMenu != null)
            playMenu.Close();
        if (buildMenu != null)
            buildMenu.Close();
        SetMenuBackdrop(false);
        if (homeScreen != null)
            homeScreen.SetActive(true);
        LeaveHubScreens(homeScreen);
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
                ShowHubScreen(HubSection.TechTree);
                break;
            case SectionWorkshop:
                ShowHubScreen(HubSection.Workshop);
                break;
            case SectionStore:
                ShowHubScreen(HubSection.Store);
                break;
            default:
                Debug.Log($"[MainMenu] Section '{section}' — à venir.");
                break;
        }
    }
}
