using System.Globalization;
using TMPro;
using UnityEngine;

// =========================================================
// HUD GAUCHE DU GARAGE : capacites du robot en construction
// Valeurs demo pour l'instant ; le gameplay appellera
// Refresh() / les setters quand la construction evoluera.
// =========================================================

public class GarageStatsHUD : MonoBehaviour
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

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
    public string slotLabel = "SLOT 1/3 – PRÉDATEUR MK1";

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
        capacityValue.text = $"{capacity.ToString("N0", Fr)} / {capacityMax.ToString("N0", Fr)}";
        healthValue.text = health.ToString("N0", Fr);
        speedValue.text = $"{speedKmh.ToString("N0", Fr)} km/h";
        powerValue.text = power.ToString("N0", Fr);
        weightValue.text = $"{weightT.ToString("0.#", Fr)} t / {weightMaxT.ToString("0.#", Fr)} t";
        slotText.text = slotLabel;

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
