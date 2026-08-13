using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// ECRAN "CHOISIR UN SLOT DE CONSTRUCTION"
// Charge les robots du joueur depuis PocketBase :
// slot occupe -> MODIFIER, slot libre -> CREER.
// =========================================================

public class BuildMenuController : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public BuildSlotCard[] cards = new BuildSlotCard[3];
    public TMP_Text usedCountText;
    public Button presetsButton;

    [Header("Navigation")]
    public string garageSceneName = "Garage";

    private RobotRecord[] recordsBySlot;

    private void Awake()
    {
        if (presetsButton != null)
            presetsButton.onClick.AddListener(() =>
                Debug.Log("[BuildMenu] Bibliothèque de presets — à venir."));
    }

    private void Start()
    {
        RefreshFromServer();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        RefreshFromServer();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // =========================================================
    // CHARGEMENT DEPUIS POCKETBASE
    // =========================================================

    private async void RefreshFromServer()
    {
        var client = PocketBaseClient.Instance;
        if (client == null || !client.IsAuthenticated)
            return; // AuthGuard renvoie deja vers Boot

        if (usedCountText != null)
            usedCountText.text = "CHARGEMENT...";

        try
        {
            var robots = await client.ListRobotsAsync();
            if (this == null)
                return;

            recordsBySlot = new RobotRecord[cards.Length];
            foreach (var robot in robots)
            {
                if (robot.slot >= 0 && robot.slot < recordsBySlot.Length)
                    recordsBySlot[robot.slot] = robot;
            }

            int used = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                var record = recordsBySlot[i];
                var data = new BuildSlotData();
                if (record != null)
                {
                    used++;
                    data.occupied = true;
                    data.robotName = record.name;
                    data.tierLabel = $"{record.stats.cpu} CPU";
                    data.health = record.stats.health;
                    data.speedKmh = record.stats.speedKmh;
                    data.energy = record.stats.energy;
                    data.massT = record.stats.weightKg / 1000f;
                }

                if (cards[i] != null)
                    cards[i].Bind(i, data, OnModifySlot, OnCreateSlot);
            }

            if (usedCountText != null)
                usedCountText.text = $"{used} / {cards.Length} SLOTS UTILISÉS";
        }
        catch (PocketBaseException e)
        {
            Debug.LogWarning($"[BuildMenu] Chargement des robots impossible : {e.Message}");
            if (this == null)
                return;
            if (usedCountText != null)
                usedCountText.text = "ERREUR DE CONNEXION";
        }
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    private void OnModifySlot(int index)
    {
        var record = recordsBySlot != null && index < recordsBySlot.Length ? recordsBySlot[index] : null;
        if (record == null)
            return;

        RobotSession.Load(record);
        SceneManager.LoadScene(garageSceneName);
    }

    private void OnCreateSlot(int index)
    {
        RobotSession.StartNew(index);
        SceneManager.LoadScene(garageSceneName);
    }
}
