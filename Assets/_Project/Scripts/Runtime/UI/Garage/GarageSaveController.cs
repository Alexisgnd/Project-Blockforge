using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// SAUVEGARDE ET SORTIE DU GARAGE
// T      : popup "nom du robot" puis envoi vers PocketBase.
// ECHAP  : ferme popup/inventaire, sinon quitte le garage —
//          avec confirmation si des modifications ne sont
//          pas sauvegardees (RobotSession.Dirty).
// =========================================================

public class GarageSaveController : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public GarageStatsHUD statsHud;
    public GarageInventoryController inventory;

    [Header("Popup nom du robot")]
    public GameObject namePopup;
    public TMP_InputField nameInput;
    public Button nameConfirmButton;
    public Button nameCancelButton;
    [Tooltip("Bouton EDIT du bandeau de slot (equivalent de la touche R)")]
    public Button editButton;

    [Header("Popup quitter")]
    public GameObject quitPopup;
    public Button quitConfirmButton;
    public Button quitCancelButton;

    [Header("Desactives pendant une popup (joueur, pince...)")]
    public MonoBehaviour[] disableWhileOpen;

    [Header("Navigation")]
    public string mainMenuSceneName = "MainMenu";

    private bool saving;

    private void Awake()
    {
        if (nameConfirmButton != null)
            nameConfirmButton.onClick.AddListener(ConfirmName);
        if (nameCancelButton != null)
            nameCancelButton.onClick.AddListener(() => SetModal(namePopup, false));
        if (quitConfirmButton != null)
            quitConfirmButton.onClick.AddListener(QuitToMenu);
        if (quitCancelButton != null)
            quitCancelButton.onClick.AddListener(() => SetModal(quitPopup, false));
        if (editButton != null)
            editButton.onClick.AddListener(OpenNamePopup);
    }

    private void Start()
    {
        RefreshSlotLabel();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            HandleEscape();
            return;
        }

        // Popup nom ouverte : Entree valide, le reste est bloque
        if (namePopup != null && namePopup.activeSelf)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                ConfirmName();
            return;
        }

        if (quitPopup != null && quitPopup.activeSelf)
            return;

        if (inventory != null && inventory.IsOpen)
            return;

        if (kb.tKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame)
            OpenNamePopup();
    }

    // =========================================================
    // ECHAP : fermer ce qui est ouvert, sinon quitter
    // =========================================================

    private void HandleEscape()
    {
        if (namePopup != null && namePopup.activeSelf)
        {
            SetModal(namePopup, false);
            return;
        }
        if (quitPopup != null && quitPopup.activeSelf)
        {
            SetModal(quitPopup, false);
            return;
        }
        if (inventory != null && inventory.IsOpen)
        {
            inventory.Close();
            return;
        }

        if (RobotSession.Dirty)
            SetModal(quitPopup, true);
        else
            QuitToMenu();
    }

    private void QuitToMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // =========================================================
    // SAUVEGARDE (avec popup de nom)
    // =========================================================

    public void OpenNamePopup()
    {
        if (saving || (namePopup != null && namePopup.activeSelf))
            return;

        // Si l'inventaire est ouvert (clic sur EDIT), on le referme d'abord
        if (inventory != null && inventory.IsOpen)
            inventory.Close();

        SetModal(namePopup, true);
        if (nameInput != null)
        {
            nameInput.text = RobotSession.Blueprint.name;
            nameInput.ActivateInputField();
        }
    }

    private void ConfirmName()
    {
        string newName = nameInput != null ? nameInput.text.Trim() : "";
        SetModal(namePopup, false);

        if (!string.IsNullOrEmpty(newName) && newName != RobotSession.Blueprint.name)
        {
            RobotSession.Blueprint.name = newName;
            RobotSession.Dirty = true;
        }

        Save();
    }

    private async void Save()
    {
        if (saving)
            return;

        var client = PocketBaseClient.Instance;
        if (client == null || !client.IsAuthenticated)
        {
            AuthGuard.EnsureAuthenticated();
            return;
        }

        saving = true;
        SetSlotText("SAUVEGARDE EN COURS...");

        // Stats actuelles du HUD (calculees par le gameplay plus tard)
        var stats = new RobotStats
        {
            cpu = statsHud.capacity,
            health = statsHud.health,
            speedKmh = statsHud.speedKmh,
            energy = statsHud.power,
            weightKg = Mathf.RoundToInt(statsHud.weightT * 1000f),
        };

        try
        {
            var record = await client.SaveRobotAsync(
                RobotSession.RecordId, RobotSession.Blueprint.name, RobotSession.Slot,
                RobotSession.Blueprint, stats);
            if (this == null)
                return;

            RobotSession.RecordId = record.id;
            RobotSession.Dirty = false;
            SetSlotText("ROBOT SAUVEGARDÉ !");
            await Awaitable.WaitForSecondsAsync(1.5f);
        }
        catch (PocketBaseException e)
        {
            Debug.LogWarning($"[GarageSave] Échec de la sauvegarde : {e.Message}");
            if (this == null)
                return;
            SetSlotText("ERREUR DE SAUVEGARDE");
            await Awaitable.WaitForSecondsAsync(2.5f);
        }
        finally
        {
            saving = false;
        }

        if (this != null)
            RefreshSlotLabel();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void SetModal(GameObject popup, bool open)
    {
        if (popup == null)
            return;

        popup.SetActive(open);
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        foreach (var behaviour in disableWhileOpen)
        {
            if (behaviour != null)
                behaviour.enabled = !open;
        }
    }

    private void RefreshSlotLabel()
    {
        SetSlotText($"SLOT {RobotSession.Slot + 1}/3 – {RobotSession.Blueprint.name}");
    }

    private void SetSlotText(string text)
    {
        if (statsHud != null && statsHud.slotText != null)
        {
            statsHud.slotLabel = text;
            statsHud.slotText.text = text;
        }
    }
}
