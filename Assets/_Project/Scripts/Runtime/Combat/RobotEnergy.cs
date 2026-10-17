using UnityEngine;

// =========================================================
// ENERGIE DES ARMES DU ROBOT (match, facon Robocraft)
// Reserve commune a toutes les armes : chaque tir en consomme
// (WeaponFireController), elle remonte toute seule des qu'on ne
// tire plus (apres un court delai). Affichee par le HUD autour
// du reticule (RobotTestHUD, jauge en deux moities).
// Un tir refuse (pas assez d'energie) previent le HUD
// (Refused) pour un clignotement.
// =========================================================

public class RobotEnergy : MonoBehaviour
{
    [Header("Reserve")]
    public float max = 275f;
    [Tooltip("Points par seconde une fois le delai passe")]
    public float regenPerSecond = 26f;
    [Tooltip("Secondes sans tir avant que l'energie remonte")]
    public float regenDelay = 0.55f;

    public float Current { get; private set; }
    public float Fraction => max > 0f ? Mathf.Clamp01(Current / max) : 0f;
    public bool Regenerating { get; private set; }

    public event System.Action Refused;

    private float lastSpend = -10f;

    private void Awake()
    {
        Current = max;
    }

    public bool CanSpend(float cost) => Current >= cost - 1e-3f;

    // Debite l'energie du tir ; false (et Refused) s'il n'y en a pas assez
    public bool TrySpend(float cost)
    {
        if (!CanSpend(cost))
        {
            Refused?.Invoke();
            return false;
        }
        Current = Mathf.Max(0f, Current - cost);
        lastSpend = Time.time;
        return true;
    }

    // Consommation continue (lame de Tesla) : true tant qu'il reste de l'energie
    public bool Drain(float perSecond)
    {
        float cost = perSecond * Time.deltaTime;
        if (Current <= 0f)
        {
            Refused?.Invoke();
            return false;
        }
        Current = Mathf.Max(0f, Current - cost);
        lastSpend = Time.time;
        return true;
    }

    private void Update()
    {
        Regenerating = Time.time - lastSpend >= regenDelay && Current < max;
        if (Regenerating)
            Current = Mathf.Min(max, Current + regenPerSecond * Time.deltaTime);
    }
}
