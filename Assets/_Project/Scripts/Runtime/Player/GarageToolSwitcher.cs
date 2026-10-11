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
// La barre d'outils (GarageToolbar) reste synchronisee dans les
// deux sens : un clic sur son bouton change l'outil en main.
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
    [Tooltip("Barre d'outils du HUD ; retrouvee dans la scene si vide")]
    public GarageToolbar toolbar;

    [Header("Touches")]
    public Key plierKey = Key.Digit1;
    public Key sprayKey = Key.Digit2;

    public bool PlierIsActive { get; private set; } = true;

    // Abonnement pour toute la vie du composant : le switch est coupe
    // pendant l'inventaire (disableWhileOpen), c'est justement la que
    // l'on clique sur la barre d'outils.
    private void Awake()
    {
        if (toolbar == null)
            toolbar = Object.FindAnyObjectByType<GarageToolbar>(FindObjectsInactive.Include);
        if (toolbar != null)
            toolbar.ToolChanged += OnToolChanged;
    }

    private void OnDestroy()
    {
        if (toolbar != null)
            toolbar.ToolChanged -= OnToolChanged;
    }

    private void Start()
    {
        // Etat par defaut : la pince
        Apply(true);
    }

    // Outil 0 = pince, 1 = pistolet (barre d'outils, clic ou touche)
    private void OnToolChanged(int index)
    {
        bool plier = index != 1;
        if (plier != PlierIsActive)
            Apply(plier);
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

        if (toolbar != null)
            toolbar.Select(plier ? 0 : 1);
    }
}
