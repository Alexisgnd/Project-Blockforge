using System.Globalization;
using TMPro;
using UnityEngine;

// =========================================================
// PANNEAU "STATISTIQUES" DU ROBOT SELECTIONNE
// Purement visuel : recoit un preset, met a jour les textes.
// =========================================================

public class RobotStatsUI : MonoBehaviour
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    [Header("Valeurs (remplies par le setup)")]
    public TMP_Text capacityValue;
    public TMP_Text offensiveValue;
    public TMP_Text healthValue;
    public TMP_Text speedValue;
    public TMP_Text energyValue;
    public TMP_Text weightValue;
    public TMP_Text blocksValue;

    public void Refresh(RobotPreset preset)
    {
        if (preset == null)
            return;

        capacityValue.text = preset.capacity.ToString("N0", Fr);
        offensiveValue.text = preset.offensivePower.ToString("N0", Fr);
        healthValue.text = preset.health.ToString("N0", Fr);
        speedValue.text = $"{preset.speedKmh.ToString("N0", Fr)} km/h";
        energyValue.text = $"{preset.energyPerSec.ToString("N0", Fr)} /s";
        weightValue.text = $"{preset.weightKg.ToString("N0", Fr)} kg";
        blocksValue.text = preset.blockCount.ToString("N0", Fr);
    }
}
