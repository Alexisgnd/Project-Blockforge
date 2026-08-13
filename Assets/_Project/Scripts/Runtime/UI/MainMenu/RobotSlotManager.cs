using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// GESTION DES 3 SLOTS DE ROBOTS DU MENU D'ACCUEIL
// Connait les slots et les presets ; notifie la selection.
// =========================================================

public class RobotSlotManager : MonoBehaviour
{
    [Header("Donnees")]
    public RobotPreset[] presets = new RobotPreset[3];

    [Header("Widgets des slots (remplis par le setup)")]
    public Button[] slotButtons = new Button[3];
    public TMP_Text[] slotTierLabels = new TMP_Text[3];
    public TMP_Text[] slotNameLabels = new TMP_Text[3];
    public GameObject[] selectedBadges = new GameObject[3];

    public event Action<RobotPreset> SelectionChanged;

    public int SelectedIndex { get; private set; }
    public RobotPreset SelectedPreset =>
        SelectedIndex >= 0 && SelectedIndex < presets.Length ? presets[SelectedIndex] : null;

    private void Awake()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i; // capture pour la closure
            if (slotButtons[i] != null)
                slotButtons[i].onClick.AddListener(() => SelectSlot(index));
        }
    }

    private void Start()
    {
        RefreshSlotLabels();
        SelectSlot(0);
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= presets.Length || presets[index] == null)
            return;

        SelectedIndex = index;

        for (int i = 0; i < selectedBadges.Length; i++)
        {
            if (selectedBadges[i] != null)
                selectedBadges[i].SetActive(i == index);
        }

        SelectionChanged?.Invoke(presets[index]);
    }

    private void RefreshSlotLabels()
    {
        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] == null)
                continue;
            if (slotTierLabels[i] != null)
                slotTierLabels[i].text = presets[i].tier;
            if (slotNameLabels[i] != null)
                slotNameLabels[i].text = presets[i].robotName;
        }
    }
}
