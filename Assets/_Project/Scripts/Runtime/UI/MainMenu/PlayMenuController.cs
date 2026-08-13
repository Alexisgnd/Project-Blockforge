using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// ECRAN "MODES DE JEU" + ECRAN DETAILS D'UN MODE
// Liste les modes, ouvre la fiche detail, lance la partie.
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
    public Image detailCover;
    public TMP_Text detailTeams;
    public TMP_Text detailDuration;
    public TMP_Text detailTickets;
    public TMP_Text detailRespawn;
    public TMP_Text detailTeamSize;
    public TMP_Text detailObjectivePrimary;
    public TMP_Text detailObjectiveSecondary;
    public Image detailMapImage;
    public TMP_Text detailMapName;
    public Button backButton;
    public Button playButton;

    private GameModeDefinition current;

    private void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(ShowModesList);
        if (playButton != null)
            playButton.onClick.AddListener(LaunchCurrentMode);
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

    public void OpenDetails(GameModeDefinition def)
    {
        current = def;
        modesView.SetActive(false);
        detailsView.SetActive(true);

        detailTitle.text = def.title;
        detailTagline.text = def.tagline;
        detailDescription.text = def.description;
        detailTeams.text = def.teamsLabel;
        detailDuration.text = def.durationLabel;
        detailTickets.text = def.ticketsLabel;
        detailRespawn.text = def.respawnLabel;
        detailTeamSize.text = def.teamSizeLabel;
        detailObjectivePrimary.text = def.objectivePrimary;
        detailObjectiveSecondary.text = def.objectiveSecondary;
        detailMapName.text = def.mapName;

        if (def.icon != null)
            detailIcon.sprite = def.icon;
        if (def.coverImage != null)
            detailCover.sprite = def.coverImage;
        if (def.mapImage != null)
            detailMapImage.sprite = def.mapImage;
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

        SceneManager.LoadScene(current.sceneName);
    }
}
