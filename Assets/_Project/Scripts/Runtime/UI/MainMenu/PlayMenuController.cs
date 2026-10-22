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
// « Find a match » (en ligne, action principale : bouton
// primary control-lg, touche Entree) lance une recherche :
// bandeau du Forge Loader (MatchmakingService ; source simulee
// tant que le service en ligne n'existe pas) et
// petit fondu au noir vers la scene d'attente Matchmaking_Space
// (l'espace et le Super Destroyer, MatchmakingSpaceScene), puis
// la meme transition Versus que « Play with bots ». Recherche
// annulee : retour au menu, qui rouvre cet ecran
// (ReopenOnReturn).
// « Play with bots » (secondaire, control-md) : le robot
// selectionne sur l'accueil est charge dans RobotSession puis
// MatchIntro joue la transition (voile, volets bleu / rouge,
// ecran Versus 5 v 5) et charge la scene du mode.
// Sans robot, les deux actions sont indisponibles (cadenas du
// DS absent, raison au survol).
// Early Access : pas encore de multijoueur (OnlineMatchesAvailable
// = false). « Find a match » est desactive avec le cadenas du DS
// et sa raison au survol ; Entree et l'indication de touche
// passent sur « Play with bots ».
// Modes indisponibles (GameModeDefinition.available = false,
// Battle Arena en Early Access) : la ligne reste affichee avec
// l'opacite disabled, le cadenas et la raison au survol
// (GameModeCard) ; elle ne peut etre ni selectionnee ni lancee
// et la selection par defaut tombe sur le premier mode
// disponible.
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
    [Tooltip("Play with bots : action secondaire")]
    public Button playButton;
    [Tooltip("Find a match : en ligne, action principale (Entree)")]
    public Button findMatchButton;
    [Tooltip("Raison affichee au survol d'une action indisponible")]
    public TMP_Text reasonText;

    // Multijoueur en ligne : false tant qu'il n'existe pas (Early Access contre les bots)
    public static readonly bool OnlineMatchesAvailable = false;
    public const string OnlineLockedReason = "Online matches arrive in a future update. Play with bots for now.";

    // Choix du joueur au lancement, lu par la scene de jeu
    public static bool PlayWithBots { get; private set; } = true;

    // Retour de la scene d'attente du matchmaking : le menu rouvre l'ecran Play
    public static bool ReopenOnReturn;

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
            findMatchButton.onClick.AddListener(FindMatch);
            // Cadenas du DS tant que le multijoueur n'existe pas
            var lockIcon = findMatchButton.transform.Find("LockIcon");
            if (lockIcon != null)
                lockIcon.gameObject.SetActive(!OnlineMatchesAvailable);
            if (!OnlineMatchesAvailable)
            {
                findMatchButton.interactable = false;
                // Indication de touche : Entree lance « Play with bots »
                var hintCaption = findMatchButton.transform.parent != null
                    ? findMatchButton.transform.parent.Find("KeyHint/Caption")?.GetComponent<TMP_Text>()
                    : null;
                if (hintCaption != null)
                    hintCaption.text = "Play with bots";
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
        if (Current == null)
            SelectFirstAvailable();
    }

    private void Update()
    {
        // Entree : action principale de l'ecran (KeyHint « Enter · Find a match »)
        var kb = Keyboard.current;
        if (kb == null || MatchIntro.IsRunning || MatchmakingService.IsActive || SceneFade.IsRunning)
            return;
        // Sans multijoueur, Entree lance « Play with bots »
        var primary = OnlineMatchesAvailable ? findMatchButton : playButton;
        if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && primary != null && primary.interactable)
        {
            // Pas quand un champ de saisie a le focus
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || selected.GetComponent<TMP_InputField>() == null)
            {
                if (OnlineMatchesAvailable)
                    FindMatch();
                else
                    LaunchCurrentMode();
            }
        }
    }

    // ---------- Navigation ----------

    public void Open()
    {
        gameObject.SetActive(true);
        RefreshRobot(slotManager != null ? slotManager.SelectedRobot : null, slotManager != null ? slotManager.SelectedIndex : -1);
        if (Current == null || !Current.available)
            SelectFirstAvailable();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // ---------- Mode selectionne ----------

    public void SelectCard(GameModeCard card)
    {
        // Mode indisponible : ni selection ni lancement
        if (card == null || card.Definition == null || !card.Available)
            return;
        Current = card.Definition;
        foreach (var c in cards)
        {
            if (c != null)
                c.Selected = c == card;
        }
        FillDetails(Current);
    }

    // Selection par defaut : le premier mode disponible de la liste
    private void SelectFirstAvailable()
    {
        if (cards == null)
            return;
        foreach (var c in cards)
        {
            if (c != null && c.Definition != null && c.Available)
            {
                SelectCard(c);
                return;
            }
        }
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
        foreach (var button in new[] { findMatchButton, playButton })
        {
            if (button == null)
                continue;
            bool online = button == findMatchButton;
            bool available = has && (!online || OnlineMatchesAvailable);
            button.interactable = available;
            string reason = online && !OnlineMatchesAvailable ? OnlineLockedReason : blockedReason;
            HoverReason.Attach(button.gameObject, this, reason);
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

        StartMatch(Current, robot);
    }

    // Statique : appele aussi depuis la scene d'attente du matchmaking,
    // quand cet ecran (scene MainMenu) n'existe plus
    private static void StartMatch(GameModeDefinition mode, RobotRecord record)
    {
        if (mode == null || record == null || MatchIntro.IsRunning)
            return;
        // Garde-fou : un mode indisponible ne se lance pas (Entree, matchmaking)
        if (!mode.available)
        {
            Toast.Info(string.IsNullOrEmpty(mode.unavailableReason) ? "Coming in a future update." : mode.unavailableReason);
            return;
        }

        PlayWithBots = true;

        // Le robot de l'accueil part au combat ; Echap x2 / Leave match revient ici
        RobotSession.Load(record);
        RobotSession.ReturnSceneName = "MainMenu";

        if (!Application.CanStreamedLevelBeLoaded(mode.sceneName))
        {
            Toast.Error($"Couldn't start the match. Scene '{mode.sceneName}' is not in the build.");
            return;
        }

        MusicManager.PlayTrack(MusicTrack.Matchmaking);

        var data = MatchIntroData.Bots(mode, SessionService.DisplayName, record);
        MatchIntro.Begin(data, mode.sceneName);
    }

    // ---------- Matchmaking (simule tant que le service en ligne n'existe pas) ----------

    private void FindMatch()
    {
        if (!OnlineMatchesAvailable)
        {
            Toast.Info(OnlineLockedReason);
            return;
        }
        if (Current == null || MatchIntro.IsRunning || MatchmakingService.IsActive || SceneFade.IsRunning)
            return;
        if (robot == null)
        {
            Toast.Warning("Build a robot first. Pick a free bay on the home screen.");
            return;
        }

        var theme = LoaderTheme.Load();
        var mode = Current;
        var record = robot;
        MusicManager.PlayTrack(MusicTrack.Matchmaking);
        MatchmakingService.Begin(mode.title, theme.defaultRegion, mode.teamSize * 2, theme.estimatedSeconds,
                                 MatchmakingService.HoldAtOnePlayer ? (IMatchmakingSource)new HoldMatchmakingSource() : new MockMatchmakingSource(),
                                 found =>
                                 {
                                     // Match complet : meme transition Versus que « Play with bots ».
                                     // Cet ecran a disparu avec le menu : mode et robot captures au clic.
                                     if (found)
                                         StartMatch(mode, record);
                                 });

        // Petit fondu au noir vers l'espace : le bandeau reste par-dessus
        if (Application.CanStreamedLevelBeLoaded(MatchmakingSpaceScene.SceneName))
        {
            MatchmakingSpaceScene.EnteredFromSearch = true;
            MatchmakingCinematic.SelectedBay = record.slot + 1; // le vaisseau garage de cette baie sort du Super Destroyer
            SceneFade.Load(MatchmakingSpaceScene.SceneName);
        }
        else
        {
            Debug.LogWarning($"[PlayMenu] Scene '{MatchmakingSpaceScene.SceneName}' absente du build : la recherche reste sur le menu. " +
                             "Lance Blockforge > Setup Matchmaking Space Scene.");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PlayWithBots = true;
        ReopenOnReturn = false;
    }
}
