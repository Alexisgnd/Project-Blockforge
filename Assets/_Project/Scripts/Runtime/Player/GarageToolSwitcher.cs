using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// SWITCH D'OUTIL DU GARAGE : PINCE <-> SPRAY PAINT
// Touche 1 = pince, touche 2 = spray (touches physiques,
// OK sur AZERTY). Un seul outil actif a la fois, la pince
// par defaut. L'outil inactif est desactive entierement :
// il ne consomme donc pas la molette ni l'update.
// Le systeme de pose de blocs est coupe en mode spray
// (et retabli en mode pince) : pas de ghost ni de clics de
// construction pendant la peinture.
// =========================================================

public class GarageToolSwitcher : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public GameObject plierRoot;
    public GameObject sprayRoot;
    [Tooltip("Coupe en mode spray, retabli en mode pince")]
    public GarageBuildController buildController;
    [Tooltip("Le switch est ignore quand l'inventaire est ouvert")]
    public GarageInventoryController inventory;

    [Header("Touches")]
    public Key plierKey = Key.Digit1;
    public Key sprayKey = Key.Digit2;

    public bool PlierIsActive { get; private set; } = true;

    private void Start()
    {
        // Etat par defaut : la pince
        Apply(true);
    }

    private void Update()
    {
        if (inventory != null && inventory.IsOpen)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard[plierKey].wasPressedThisFrame && !PlierIsActive)
            Apply(true);
        else if (keyboard[sprayKey].wasPressedThisFrame && PlierIsActive)
            Apply(false);
    }

    private void Apply(bool plier)
    {
        PlierIsActive = plier;

        if (plierRoot != null)
            plierRoot.SetActive(plier);

        if (sprayRoot != null)
            sprayRoot.SetActive(!plier);

        if (buildController != null)
            buildController.enabled = plier;
    }
}
