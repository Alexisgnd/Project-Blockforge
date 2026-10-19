using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// ECRAN PLAY (design « Play to Versus »)
// Une seule vue : a gauche la liste des modes (une ligne par
// mode) et le robot qui part au combat ; a droite la fiche du
// mode selectionne (apercu de la carte, description, trois
// chiffres) et les actions. Cliquer un mode met la ligne en
// selection et remplit la fiche ; il n'y a plus d'ecran de
// details separe.
// « Play with bots » (action principale, touche Entree) : le
// robot selectionne sur l'accueil est charge dans RobotSession
// puis MatchIntro joue la transition (voile, volets bleu /
// rouge, ecran Versus 5 v 5) et charge la scene du mode.
// « Find a match » (en ligne) n'existe pas encore : desactive,
// avec un cadenas et sa raison au survol (regle du DS). Avec le
// matchmaking simule (Blockforge > Debug > Loader), il lance une
// recherche : bandeau du Forge Loader (MatchmakingService), puis
// la meme transition Versus que « Play with bots ».
// =========================================================

public class PlayMenuController : MonoBehaviour
{
    [Header("Donnees")]
    public GameModeDefinition[] definitions;
    [Tooltip("Robot selectionne sur l'accueil (pour la baie « Your robot »)")]
    public RobotSlotManager slotManager;

    [Header("Liste des modes (remplie par le setup)")]
    public GameModeCard[] cards;

    [Header("Fiche du mode (remplie par le setup)")]
    public TMP_Text detailTitle;
    public TMP_Text detailDescription;
    public TMP_Text detailPlayers;
    public TMP_Text detailTarget;
    public TMP_Text detailTime;
    public TMP_Text mapChipText;
    public TMP_Text teamsChipText;
    public TMP_Text mapPlaceholderText;
    public Image mapImage;

    [Header("Your robot (remplie par le setup)")]
    public DSElement robotSlot;
    public TMP_Text robotBayText;
    public TMP_Text robotNameText;
    public TMP_Text robotStatsText;
    public Button changeRobotButton;

    [Header("Actions (remplies par le setup)")]
    [Tooltip("Play with bots : action principale")]
    public Button playButton;
    [Tooltip("Find a match : en ligne, pas encore disponible")]
    public Button findMatchButton;
    [Tooltip("Raison affichee au survol d'une action indisponible")]
    public TMP_Text reasonText;

    // Choix du joueur au lancement, lu par la scene de jeu
    public static bool PlayWithBots { get; private set; } = true;

    public GameModeDefinition Current { get; private set; }

    private MainMenuController menu;
    private RobotRecord robot;
    private string blockedReason;

    private void Awake()
    {
        menu = GetComponentInParent<MainMenuController>(true);
        if (menu == null)
            menu = FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);

        if (playButton != null)
            playButton.onClick.AddListener(LaunchCurrentMode);
        if (changeRobotButton != null)
            changeRobotButton.onClick.AddListener(ChangeRobot);
        if (findMatchButton != null)
        {
            if (MatchmakingService.MockEnabled)
            {
                // Recherche simulee (outil de dev) : le bandeau de matchmaking du Forge Loader
                findMatchButton.interactable = true;
                findMatchButton.onClick.AddListener(FindMatch);
            }
            else
            {
                findMatchButton.interactable = false; // en ligne : pas encore disponible
                HoverReason.Attach(findMatchButton.gameObject, this, "Online matchmaking is coming soon.");
            }
        }
        if (reasonText != null)
            reasonText.text = "";
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
        for (int i = 0; i < cards.Length && i < definitions.Length; i++)
        {
            if (cards[i] != null && definitions[i] != null)
                cards[i].Bind(definitions[i], SelectCard);
        }
        if (Current == null && cards.Length > 0 && cards[0] != null && cards[0].Definition != null)
            SelectCard(cards[0]);
    }

    private void Update()
    {
        // Entree : action principale de l'ecran (KeyHint « Enter · Play with bots »)
        var kb = Keyboard.current;
        if (kb == null || MatchIntro.IsRunning || MatchmakingService.IsActive)
            return;
        if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && playButton != null && playButton.interactable)
        {
            // Pas quand un champ de saisie a le focus
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || selected.GetComponent<TMP_InputField>() == null)
                LaunchCurrentMode();
        }
    }

    // ---------- Navigation ----------

    public void Open()
    {
        gameObject.SetActive(true);
        RefreshRobot(slotManager != null ? slotManager.SelectedRobot : null, slotManager != null ? slotManager.SelectedIndex : -1);
        if (Current == null && cards != null && cards.Length > 0 && cards[0] != null && cards[0].Definition != null)
            SelectCard(cards[0]);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // ---------- Mode selectionne ----------

    public void SelectCard(GameModeCard card)
    {
        if (card == null || card.Definition == null)
            return;
        Current = card.Definition;
        foreach (var c in cards)
        {
            if (c != null)
                c.Selected = c == card;
        }
        FillDetails(Current);
    }

    private void FillDetails(GameModeDefinition def)
    {
        Set(detailTitle, def.title);
        Set(detailDescription, def.description);
        Set(detailPlayers, def.playersLabel);
        Set(detailTarget, def.targetLabel);
        Set(detailTime, def.timeLabel);
        Set(mapChipText, def.mapName);
        Set(teamsChipText, def.playersLabel);
        Set(mapPlaceholderText, $"[Map preview — {def.mapName}]");
        if (mapImage != null)
        {
            mapImage.sprite = def.mapImage;
            mapImage.enabled = def.mapImage != null;
        }
        if (mapPlaceholderText != null)
            mapPlaceholderText.gameObject.SetActive(def.mapImage == null);
    }

    private static void Set(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // ---------- Your robot ----------

    private void OnRobotSelected(RobotRecord record, int slotIndex)
    {
        RefreshRobot(record, slotIndex);
    }

    private void RefreshRobot(RobotRecord record, int slotIndex)
    {
        robot = record;
        bool has = record != null;
        if (robotSlot != null)
        {
            robotSlot.Empty = !has;
            robotSlot.Selected = has;
        }
        Set(robotBayText, has ? $"Bay {slotIndex + 1:00}" : "No robot");
        Set(robotNameText, has ? (record.name ?? "Robot") : "Build one in a free bay");
        if (has)
        {
            var stats = record.stats ?? new RobotStats();
            Set(robotStatsText, $"{BlockforgeTheme.Qty(stats.cpu, "CPU")}    {BlockforgeTheme.Qty(stats.weightKg, "kg")}");
        }
        else
        {
            Set(robotStatsText, "");
        }

        // Sans robot, pas de match : action indisponible avec sa raison
        blockedReason = has ? null : "Build a robot in a free bay first.";
        if (playButton != null)
        {
            playButton.interactable = has;
            HoverReason.Attach(playButton.gameObject, this, blockedReason);
        }
    }

    private void ChangeRobot()
    {
        // Les baies sont sur l'accueil : on y retourne
        if (menu != null)
            menu.ShowHome();
    }

    // Raison d'une action indisponible (affichee au survol, effacee a la sortie)
    public void ShowReason(string reason)
    {
        if (reasonText != null)
            reasonText.text = reason ?? "";
    }

    // ---------- Lancement ----------

    private void LaunchCurrentMode()
    {
        if (Current == null || MatchIntro.IsRunning)
            return;
        if (robot == null)
        {
            Toast.Warning("Build a robot first. Pick a free bay on the home screen.");
            return;
        }

        PlayWithBots = true;

        // Le robot de l'accueil part au combat ; Echap x2 / Leave match revient ici
        RobotSession.Load(robot);
        RobotSession.ReturnSceneName = "MainMenu";

        if (!Application.CanStreamedLevelBeLoaded(Current.sceneName))
        {
            Toast.Error($"Couldn't start the match. Scene '{Current.sceneName}' is not in the build.");
            return;
        }

        MusicManager.PlayTrack(MusicTrack.Matchmaking);

        var data = MatchIntroData.Bots(Current, SessionService.DisplayName, robot);
        MatchIntro.Begin(data, Current.sceneName);
    }

    // ---------- Matchmaking (simule tant que le service en ligne n'existe pas) ----------

    private void FindMatch()
    {
        if (Current == null || MatchIntro.IsRunning || MatchmakingService.IsActive)
            return;
        if (robot == null)
        {
            Toast.Warning("Build a robot first. Pick a free bay on the home screen.");
            return;
        }

        var theme = LoaderTheme.Load();
        var mode = Current;
        MusicManager.PlayTrack(MusicTrack.Matchmaking);
        MatchmakingService.Begin(mode.title, theme.defaultRegion, mode.teamSize * 2, theme.estimatedSeconds,
                                 new MockMatchmakingSource(), found =>
                                 {
                                     // Match complet : meme transition Versus que « Play with bots »
                                     if (found && this != null && Current == mode)
                                         LaunchCurrentMode();
                                 });
    }
}
