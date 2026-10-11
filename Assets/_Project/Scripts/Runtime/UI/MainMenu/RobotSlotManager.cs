using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// GESTION DES 3 SLOTS DE ROBOTS DU MENU D'ACCUEIL
// Charge les robots du joueur depuis PocketBase (memes
// donnees que l'ecran Construire) ; notifie la selection.
// Slot vide : un clic ouvre l'ecran Construire.
// La selection est memorisee par compte (PlayerPrefs).
// Design system : chaque baie est un RobotSlot (DSElement) ;
// selection = surface-selected + border-accent, baie vide =
// pointilles + "New robot". Memes deux chiffres sur chaque
// baie remplie (CPU et masse) pour comparer d'un coup d'oeil.
// =========================================================

public class RobotSlotManager : MonoBehaviour
{
    private const string SelectedSlotKeyPrefix = "Blockforge.SelectedSlot.";

    [Header("Widgets des slots (remplis par le setup)")]
    public Button[] slotButtons = new Button[3];
    public TMP_Text[] slotTierLabels = new TMP_Text[3];
    public TMP_Text[] slotNameLabels = new TMP_Text[3];
    public GameObject[] selectedBadges = new GameObject[3];
    [Tooltip("Ligne de chiffres de chaque baie (readout-sm) : CPU et masse")]
    public TMP_Text[] slotStatLabels = new TMP_Text[3];

    // Robot selectionne (null si le joueur n'a aucun robot) + index du slot (-1 si aucun)
    public event Action<RobotRecord, int> SelectionChanged;

    // Clic sur un slot vide
    public event Action<int> EmptySlotClicked;

    public int SelectedIndex { get; private set; } = -1;
    public RobotRecord SelectedRobot =>
        recordsBySlot != null && SelectedIndex >= 0 && SelectedIndex < recordsBySlot.Length
            ? recordsBySlot[SelectedIndex]
            : null;

    private RobotRecord[] recordsBySlot;
    private bool loading;

    private void Awake()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i; // capture pour la closure
            if (slotButtons[i] != null)
                slotButtons[i].onClick.AddListener(() => OnSlotClicked(index));
        }
    }

    private void Start()
    {
        Refresh();
    }

    // =========================================================
    // CHARGEMENT DEPUIS POCKETBASE
    // =========================================================

    // Recharge les robots du joueur (au demarrage et au retour sur l'accueil,
    // pour refleter une creation ou une suppression faite dans Construire).
    public async void Refresh()
    {
        if (loading)
            return;

        var client = PocketBaseClient.Instance;
        if (client == null || !client.IsAuthenticated)
            return; // AuthGuard renvoie deja vers Boot

        loading = true;
        if (recordsBySlot == null)
        {
            for (int i = 0; i < slotButtons.Length; i++)
                SetSlotTexts(i, BayLabel(i), "Loading…", "");
        }

        try
        {
            var robots = await client.ListRobotsAsync();
            if (this == null)
                return;

            recordsBySlot = new RobotRecord[slotButtons.Length];
            foreach (var robot in robots)
            {
                if (robot.slot >= 0 && robot.slot < recordsBySlot.Length)
                    recordsBySlot[robot.slot] = robot;
            }

            RefreshSlotLabels();
            SelectSlot(PickInitialSlot());
        }
        catch (PocketBaseException e)
        {
            Debug.LogWarning($"[RobotSlots] Chargement des robots impossible : {e.Message}");
            // Meme message que l'ecran Construire : le toast fusionne les doublons
            Toast.Error($"Couldn't load your robots. {e.Message}");
            if (this == null)
                return;
            recordsBySlot = null;
            for (int i = 0; i < slotButtons.Length; i++)
                SetSlotTexts(i, BayLabel(i), "Offline", "");
            SetSelection(-1);
        }
        finally
        {
            loading = false;
        }
    }

    // =========================================================
    // SELECTION
    // =========================================================

    private void OnSlotClicked(int index)
    {
        if (recordsBySlot == null)
            return; // pas encore charge (ou erreur reseau)

        if (recordsBySlot[index] == null)
        {
            EmptySlotClicked?.Invoke(index);
            return;
        }

        SelectSlot(index);
    }

    public void SelectSlot(int index)
    {
        if (recordsBySlot == null || index < 0 || index >= recordsBySlot.Length || recordsBySlot[index] == null)
        {
            SetSelection(-1);
            return;
        }

        SetSelection(index);
        PlayerPrefs.SetInt(SelectedSlotKey(), index);
        PlayerPrefs.Save();
    }

    private void SetSelection(int index)
    {
        SelectedIndex = index;

        for (int i = 0; i < selectedBadges.Length; i++)
        {
            if (selectedBadges[i] != null)
                selectedBadges[i].SetActive(i == index);
        }
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] != null && slotButtons[i].TryGetComponent<DSElement>(out var element))
                element.Selected = i == index;
        }

        SelectionChanged?.Invoke(SelectedRobot, index);
    }

    // Slot memorise s'il contient toujours un robot, sinon le premier slot occupe, sinon aucun
    private int PickInitialSlot()
    {
        int saved = PlayerPrefs.GetInt(SelectedSlotKey(), -1);
        if (saved >= 0 && saved < recordsBySlot.Length && recordsBySlot[saved] != null)
            return saved;

        for (int i = 0; i < recordsBySlot.Length; i++)
        {
            if (recordsBySlot[i] != null)
                return i;
        }
        return -1;
    }

    private static string SelectedSlotKey()
    {
        string userId = PocketBaseClient.Instance?.User?.id;
        return SelectedSlotKeyPrefix + (string.IsNullOrEmpty(userId) ? "guest" : userId);
    }

    // =========================================================
    // AFFICHAGE DES CARTES
    // =========================================================

    private void RefreshSlotLabels()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var record = recordsBySlot[i];
            if (slotButtons[i] != null && slotButtons[i].TryGetComponent<DSElement>(out var element))
                element.Empty = record == null;

            if (record != null)
            {
                var stats = record.stats ?? new RobotStats();
                SetSlotTexts(i, BayLabel(i), record.name,
                             $"{BlockforgeTheme.Qty(stats.cpu, "CPU")}    {BlockforgeTheme.Qty(stats.weightKg, "kg")}");
            }
            else
            {
                SetSlotTexts(i, BayLabel(i), "New robot", "");
            }
        }
    }

    private static string BayLabel(int index) => $"Bay {index + 1:00}";

    private void SetSlotTexts(int index, string bay, string robotName, string stats)
    {
        if (index < slotTierLabels.Length && slotTierLabels[index] != null)
            slotTierLabels[index].text = bay;
        if (index < slotNameLabels.Length && slotNameLabels[index] != null)
            slotNameLabels[index].text = robotName;
        if (slotStatLabels != null && index < slotStatLabels.Length && slotStatLabels[index] != null)
            slotStatLabels[index].text = stats;
    }
}
