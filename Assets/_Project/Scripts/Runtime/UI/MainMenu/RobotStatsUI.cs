using TMPro;
using UnityEngine;

// =========================================================
// PANNEAU "STATISTIQUES" DU ROBOT SELECTIONNE
// Purement visuel : recoit le robot du joueur (stats
// PocketBase), met a jour les textes.
// =========================================================

public class RobotStatsUI : MonoBehaviour
{
    private const string Empty = "–";

    [Header("Valeurs (remplies par le setup)")]
    public TMP_Text capacityValue;
    public TMP_Text offensiveValue;
    public TMP_Text healthValue;
    public TMP_Text speedValue;
    public TMP_Text energyValue;
    public TMP_Text weightValue;
    public TMP_Text blocksValue;

    public void Refresh(RobotRecord robot)
    {
        if (robot == null)
        {
            Clear();
            return;
        }

        var stats = robot.stats ?? new RobotStats();
        int blocks = robot.data?.blocks?.Count ?? 0;

        // StatLine : chiffre en mono, unite a part en ink-muted
        Set(capacityValue, BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(stats.cpu), "CPU"));
        Set(offensiveValue, Empty); // pas encore calculee (combat non commence)
        Set(healthValue, BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(stats.health), "HP"));
        Set(speedValue, BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(stats.speedKmh), "km/h"));
        Set(energyValue, BlockforgeTheme.Num(stats.energy));
        Set(weightValue, BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(stats.weightKg), "kg"));
        Set(blocksValue, BlockforgeTheme.Num(blocks));
    }

    public void Clear()
    {
        Set(capacityValue, Empty);
        Set(offensiveValue, Empty);
        Set(healthValue, Empty);
        Set(speedValue, Empty);
        Set(energyValue, Empty);
        Set(weightValue, Empty);
        Set(blocksValue, Empty);
    }

    private static void Set(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
