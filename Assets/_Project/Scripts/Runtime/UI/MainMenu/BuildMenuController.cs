using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// ECRAN "CHOISIR UN SLOT DE CONSTRUCTION"
// 3 slots : modifier un robot existant ou en creer un.
// =========================================================

public class BuildMenuController : MonoBehaviour
{
    [Header("Donnees (null = slot vide)")]
    public RobotPreset[] slots = new RobotPreset[3];

    [Header("References (remplies par le setup)")]
    public BuildSlotCard[] cards = new BuildSlotCard[3];
    public TMP_Text usedCountText;
    public Button presetsButton;

    [Header("Navigation")]
    public string garageSceneName = "Garage";

    private void Awake()
    {
        if (presetsButton != null)
            presetsButton.onClick.AddListener(() =>
                Debug.Log("[BuildMenu] Bibliothèque de presets — à venir."));
    }

    private void Start()
    {
        Refresh();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        int used = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            var preset = i < slots.Length ? slots[i] : null;
            if (preset != null)
                used++;
            if (cards[i] != null)
                cards[i].Bind(i, preset, OnModifySlot, OnCreateSlot);
        }

        if (usedCountText != null)
            usedCountText.text = $"{used} / {cards.Length} SLOTS UTILISÉS";
    }

    private void OnModifySlot(int index)
    {
        Debug.Log($"[BuildMenu] Modification du robot du slot {index + 1}.");
        SceneManager.LoadScene(garageSceneName);
    }

    private void OnCreateSlot(int index)
    {
        Debug.Log($"[BuildMenu] Création d'un nouveau robot dans le slot {index + 1}.");
        SceneManager.LoadScene(garageSceneName);
    }
}
