using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// ECRAN "MODES DE JEU" + ECRAN DETAILS D'UN MODE
// Liste les modes, ouvre la fiche detail, lance la partie.
// Pas d'apercu (image de couverture / de carte) : la fiche
// reprend le pictogramme de la carte cliquee, et la carte
// d'exemple est une ligne du panneau Details.
// Lancement : "Play with bots" (actif) ou "Play online"
// (desactive tant que le jeu en ligne n'existe pas).
// =========================================================

public class PlayMenuController : MonoBehaviour
{
    [Header("Vues")]
    public GameObject modesView;
    public GameObject detailsView;

    [Header("Donnees")]
    public GameModeDefinition[] definitions;

    [Header("Cartes (remplies par le setup)")]
    public GameModeCard[] cards;

    [Header("Fiche detail (remplie par le setup)")]
    public Image detailIcon;
    public TMP_Text detailTitle;
    public TMP_Text detailTagline;
    public TMP_Text detailDescription;
    public TMP_Text detailTeams;
    public TMP_Text detailDuration;
    public TMP_Text detailTickets;
    public TMP_Text detailRespawn;
    public TMP_Text detailTeamSize;
    public TMP_Text detailMapName;
    public TMP_Text detailObjectivePrimary;
    public TMP_Text detailObjectiveSecondary;
    public Button backButton;
    [Tooltip("Play with bots")]
    public Button playButton;
    [Tooltip("Play online : desactive pour l'instant")]
    public Button playOnlineButton;

    // Choix du joueur au lancement, lu par la scene de jeu
    public static bool PlayWithBots { get; private set; } = true;

    private GameModeDefinition current;

    private void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(ShowModesList);
        if (playButton != null)
            playButton.onClick.AddListener(LaunchCurrentMode);
        if (playOnlineButton != null)
            playOnlineButton.interactable = false; // en ligne : pas encore disponible
    }

    private void Start()
    {
        for (int i = 0; i < cards.Length && i < definitions.Length; i++)
        {
            if (cards[i] != null && definitions[i] != null)
                cards[i].Bind(definitions[i], OpenDetails);
        }
    }

    // ---------- Navigation ----------

    public void Open()
    {
        gameObject.SetActive(true);
        ShowModesList();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void ShowModesList()
    {
        modesView.SetActive(true);
        detailsView.SetActive(false);
    }

    public void OpenDetails(GameModeDefinition def, GameModeCard card)
    {
        current = def;
        modesView.SetActive(false);
        detailsView.SetActive(true);

        Set(detailTitle, def.title);
        Set(detailTagline, def.tagline);
        Set(detailDescription, def.description);
        Set(detailTeams, def.teamsLabel);
        Set(detailDuration, def.durationLabel);
        Set(detailTickets, def.ticketsLabel);
        Set(detailRespawn, def.respawnLabel);
        Set(detailTeamSize, def.teamSizeLabel);
        Set(detailMapName, def.mapName);
        Set(detailObjectivePrimary, def.objectivePrimary);
        Set(detailObjectiveSecondary, def.objectiveSecondary);

        // Meme pictogramme que la carte cliquee
        Sprite icon = def.icon != null ? def.icon : card != null ? card.IconSprite : null;
        if (detailIcon != null && icon != null)
            detailIcon.sprite = icon;
    }

    private static void Set(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private void LaunchCurrentMode()
    {
        if (current == null)
            return;

        if (current.isCustom)
        {
            Debug.Log("[PlayMenu] Configuration de partie personnalisée — à venir.");
            return;
        }

        PlayWithBots = true;
        SceneManager.LoadScene(current.sceneName);
    }
}
