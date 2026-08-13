using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// CARTE D'UN SLOT DE CONSTRUCTION
// Deux etats : robot existant (stats + MODIFIER)
// ou slot vide (+ CREER).
// =========================================================

public class BuildSlotCard : MonoBehaviour
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    [Header("Commun (rempli par le setup)")]
    public TMP_Text numberText;
    public TMP_Text titleText;

    [Header("Etat robot existant")]
    public GameObject filledGroup;
    public TMP_Text tierText;
    public Image coverImage;
    public TMP_Text healthValue;
    public TMP_Text speedValue;
    public TMP_Text energyValue;
    public TMP_Text massValue;
    public Button modifyButton;

    [Header("Etat slot vide")]
    public GameObject emptyGroup;
    public Button createButton;

    private int slotIndex;
    private Action<int> onModify;
    private Action<int> onCreate;

    public void Bind(int index, RobotPreset preset, Action<int> modifyCallback, Action<int> createCallback)
    {
        slotIndex = index;
        onModify = modifyCallback;
        onCreate = createCallback;

        numberText.text = (index + 1).ToString();

        bool hasRobot = preset != null;
        filledGroup.SetActive(hasRobot);
        emptyGroup.SetActive(!hasRobot);

        if (hasRobot)
        {
            titleText.text = preset.robotName;
            tierText.text = preset.tier;
            healthValue.text = preset.health.ToString("N0", Fr);
            speedValue.text = $"{preset.speedKmh.ToString("N0", Fr)} km/h";
            energyValue.text = preset.energyPerSec.ToString("N0", Fr);
            massValue.text = $"{(preset.weightKg / 1000f).ToString("0.#", Fr)} t";
        }
        else
        {
            titleText.text = "SLOT DISPONIBLE";
        }

        modifyButton.onClick.RemoveAllListeners();
        modifyButton.onClick.AddListener(() => onModify?.Invoke(slotIndex));
        createButton.onClick.RemoveAllListeners();
        createButton.onClick.AddListener(() => onCreate?.Invoke(slotIndex));
    }
}
