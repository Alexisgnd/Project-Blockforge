using TMPro;
using UnityEngine;

// =========================================================
// HUD GAUCHE DU GARAGE : capacites du robot en construction
// Valeurs demo pour l'instant ; le gameplay appellera
// Refresh() / les setters quand la construction evoluera.
// =========================================================

public class GarageStatsHUD : MonoBehaviour
{
    [Header("Valeurs (demo, a brancher sur le gameplay)")]
    public int capacity = 680;
    public int capacityMax = 1000;
    public int health = 9800;
    public int healthMax = 12000;
    public int speedKmh = 92;
    public int speedMax = 150;
    public int power = 120;
    public int powerMax = 200;
    public float weightT = 8.5f;
    public float weightMaxT = 20f;
    public string slotLabel = "Bay 01 · Predator Mk1";

    [Header("References (remplies par le setup)")]
    public RectTransform capacityFill;
    public RectTransform healthFill;
    public RectTransform speedFill;
    public RectTransform powerFill;
    public RectTransform weightFill;
    public TMP_Text capacityValue;
    public TMP_Text healthValue;
    public TMP_Text speedValue;
    public TMP_Text powerValue;
    public TMP_Text weightValue;
    public TMP_Text slotText;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        // CPU : budget a plafond (CapacityMeter) -> warning des 90 %, danger au-dela
        float cpuRatio = capacityMax > 0 ? capacity / (float)capacityMax : 0f;
        string cpu = $"{BlockforgeTheme.Num(capacity)} / {BlockforgeTheme.Num(capacityMax)}";
        if (cpuRatio > 1f)
            cpu = $"<color=#{ColorUtility.ToHtmlStringRGB(BlockforgeTheme.DangerInk)}>{cpu}</color>";
        capacityValue.text = BlockforgeTheme.QtyMuted(cpu, "CPU");
        healthValue.text = BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(health), "HP");
        speedValue.text = BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(speedKmh), "km/h");
        powerValue.text = BlockforgeTheme.Num(power);
        weightValue.text = BlockforgeTheme.QtyMuted($"{BlockforgeTheme.Num(weightT)} / {BlockforgeTheme.Num(weightMaxT)}", "t");
        slotText.text = slotLabel;

        if (capacityFill != null && capacityFill.TryGetComponent<DSElement>(out var cpuFill))
            cpuFill.Tone = cpuRatio > 1f ? DSTone.Danger : cpuRatio >= 0.9f ? DSTone.Warning : DSTone.Neutral;

        SetFill(capacityFill, capacity / (float)capacityMax);
        SetFill(healthFill, health / (float)healthMax);
        SetFill(speedFill, speedKmh / (float)speedMax);
        SetFill(powerFill, power / (float)powerMax);
        SetFill(weightFill, weightT / weightMaxT);
    }

    private static void SetFill(RectTransform fill, float ratio)
    {
        if (fill == null)
            return;
        fill.anchorMin = new Vector2(0, 0);
        fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }
}
