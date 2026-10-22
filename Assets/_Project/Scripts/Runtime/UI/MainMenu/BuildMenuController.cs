using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =========================================================
// ECRAN "CHOISIR UN SLOT DE CONSTRUCTION"
// Charge les robots du joueur via RobotStore (cache local +
// PocketBase si en ligne) :
// slot occupe -> MODIFIER / SUPPRIMER, slot libre -> CREER.
// La suppression passe par une popup de confirmation
// (action irreversible ; hors ligne, elle est portee au serveur
// a la prochaine synchro).
// Chaque baie occupee affiche un vrai apercu 3D du robot
// (RobotThumbnailRenderer), rendu a chaque rechargement.
// =========================================================

public class BuildMenuController : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public BuildSlotCard[] cards = new BuildSlotCard[3];
    public TMP_Text usedCountText;

    [Header("Popup de suppression (remplie par le setup)")]
    public GameObject deletePopup;
    public TMP_Text deleteMessageText;
    public Button deleteConfirmButton;
    public Button deleteCancelButton;

    [Header("Navigation")]
    public string garageSceneName = "Garage";

    private RobotRecord[] recordsBySlot;
    private readonly RenderTexture[] previews = new RenderTexture[3];
    private HangarRobotShowcase showcase;
    private int pendingDeleteSlot = -1;
    private bool deleting;

    private void Awake()
    {
        if (deleteConfirmButton != null)
            deleteConfirmButton.onClick.AddListener(ConfirmDelete);
        if (deleteCancelButton != null)
            deleteCancelButton.onClick.AddListener(CloseDeletePopup);
        if (deletePopup != null)
            deletePopup.SetActive(false);
    }

    private void Start()
    {
        RefreshFromServer();
    }

    private void Update()
    {
        // ECHAP ferme la popup (sauf pendant l'appel serveur)
        if (deletePopup != null && deletePopup.activeSelf && !deleting &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseDeletePopup();
        }
    }

    public void Open()
    {
        gameObject.SetActive(true);
        RefreshFromServer();
    }

    public void Close()
    {
        if (!deleting)
            CloseDeletePopup();
        gameObject.SetActive(false);
    }

    // =========================================================
    // CHARGEMENT (RobotStore : cache local + PocketBase si en ligne)
    // =========================================================

    private async void RefreshFromServer()
    {
        if (!SessionService.HasIdentity)
            return; // AuthGuard renvoie deja vers Boot

        if (usedCountText != null)
            usedCountText.text = "Loading…";

        try
        {
            var robots = await RobotStore.ListAsync();
            if (this == null)
                return;

            recordsBySlot = new RobotRecord[cards.Length];
            foreach (var robot in robots)
            {
                if (robot.slot >= 0 && robot.slot < recordsBySlot.Length)
                    recordsBySlot[robot.slot] = robot;
            }

            int used = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                var record = recordsBySlot[i];
                var data = new BuildSlotData();
                if (record != null)
                {
                    var stats = record.stats ?? new RobotStats();
                    used++;
                    data.occupied = true;
                    data.robotName = record.name;
                    data.tierLabel = BlockforgeTheme.Qty(stats.cpu, "CPU");
                    data.health = stats.health;
                    data.speedKmh = RobotStatsUI.SpeedOf(record, ref showcase);
                    data.energy = stats.energy;
                    data.massT = stats.weightKg / 1000f;
                }

                if (cards[i] != null)
                {
                    cards[i].Bind(i, data, OnModifySlot, OnCreateSlot, OnDeleteSlot);
                    cards[i].SetPreview(record != null ? RenderPreview(i, record) : null);
                }
            }

            if (usedCountText != null)
            {
                int pending = RobotStore.PendingChanges;
                usedCountText.text = SessionService.IsOnline || pending == 0
                    ? $"{used} / {cards.Length} bays used"
                    : $"{used} / {cards.Length} bays used · {pending} waiting to sync";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Toast.Error($"Couldn't load your robots. {e.Message}");
            if (this == null)
                return;
            if (usedCountText != null)
                usedCountText.text = "Unavailable";
        }
    }

    // =========================================================
    // APERCUS 3D
    // =========================================================

    private Texture RenderPreview(int index, RobotRecord record)
    {
        if (index < 0 || index >= previews.Length || record.data == null)
            return null;
        if (showcase == null)
            showcase = FindAnyObjectByType<HangarRobotShowcase>();
        if (showcase == null || showcase.Catalog == null)
            return null;

        // Texture a 2x la taille affichee pour rester nette
        Vector2 size = cards[index].PreviewPixelSize * 2f;
        if (size.x < 1f || size.y < 1f)
            size = new Vector2(448f, 384f);

        if (string.IsNullOrEmpty(record.data.name))
            record.data.name = record.name;
        // Studio photo isole (loin du hangar : ni ses lumieres ni son exposition)
        previews[index] = RobotThumbnailRenderer.Render(record.data, showcase.Catalog, RobotThumbnailRenderer.DefaultStage,
                                                        Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), previews[index]);
        return previews[index];
    }

    private void OnDestroy()
    {
        for (int i = 0; i < previews.Length; i++)
        {
            if (previews[i] != null)
            {
                previews[i].Release();
                Destroy(previews[i]);
            }
        }
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    private RobotRecord RecordAt(int index) =>
        recordsBySlot != null && index >= 0 && index < recordsBySlot.Length ? recordsBySlot[index] : null;

    private void OnModifySlot(int index)
    {
        var record = RecordAt(index);
        if (record == null)
            return;

        RobotSession.Load(record);
        SceneLoader.Load(garageSceneName);
    }

    private void OnCreateSlot(int index)
    {
        RobotSession.StartNew(index);
        SceneLoader.Load(garageSceneName);
    }

    // =========================================================
    // SUPPRESSION (popup de confirmation)
    // =========================================================

    private void OnDeleteSlot(int index)
    {
        var record = RecordAt(index);
        if (record == null || deleting)
            return;

        if (deletePopup == null)
        {
            Debug.LogWarning("[BuildMenu] Popup de suppression absente : lance " +
                             "Blockforge > MainMenu Build Screen - Add Delete.");
            return;
        }

        pendingDeleteSlot = index;
        SetDeleteMessage($"{record.name} in bay {index + 1:00} is deleted for good. You can't undo this.");
        SetDeleteButtonsInteractable(true);
        deletePopup.SetActive(true);
        SetConfirmLabel($"Delete {record.name}"); // apres l'activation : la mesure du texte en a besoin
    }

    private async void ConfirmDelete()
    {
        var record = RecordAt(pendingDeleteSlot);
        if (record == null || deleting)
            return;
        if (!AuthGuard.EnsureAuthenticated())
            return;

        deleting = true;
        SetDeleteButtonsInteractable(false);
        SetDeleteMessage($"Deleting {record.name}…");
        var toast = Toast.Loading($"Deleting {record.name}…");

        try
        {
            await RobotStore.DeleteAsync(record);
            if (SessionService.IsOnline)
                toast.Success($"{record.name} deleted. Bay {record.slot + 1:00} is free.");
            else
                toast.Warning($"{record.name} deleted on this computer. Bay {record.slot + 1:00} is free; the server will follow when you're back online.");
            if (this == null)
                return;

            Debug.Log($"[BuildMenu] Robot '{record.name}' (slot {record.slot + 1}) supprimé.");
            deleting = false;
            CloseDeletePopup();
            RefreshFromServer();
        }
        catch (PocketBaseException e)
        {
            Debug.LogWarning($"[BuildMenu] Suppression impossible ({e.StatusCode}) : {e.Message}");
            toast.Error($"Couldn't delete {record.name}. {e.Message}");
            if (this == null)
                return;

            deleting = false;
            SetDeleteMessage($"Couldn't delete {record.name}. {e.Message}");
            if (deleteCancelButton != null)
                deleteCancelButton.interactable = true;
        }
    }

    private void CloseDeletePopup()
    {
        if (deleting)
            return;
        pendingDeleteSlot = -1;
        if (deletePopup != null)
            deletePopup.SetActive(false);
    }

    private void SetDeleteMessage(string message)
    {
        if (deleteMessageText != null)
            deleteMessageText.text = message;
    }

    // Dialogue : le bouton qui confirme nomme le verbe et l'objet
    private void SetConfirmLabel(string label)
    {
        if (deleteConfirmButton == null)
            return;
        var text = deleteConfirmButton.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            return;
        text.text = label;
        text.ForceMeshUpdate(true, true);
        // Le bouton garde la largeur de son libelle (en capitales), aligne a droite
        var rect = (RectTransform)deleteConfirmButton.transform;
        float width = Mathf.Min(420f, Mathf.Max(140f, text.GetPreferredValues().x + 2 * BlockforgeTheme.Space6));
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        if (deleteCancelButton != null)
        {
            var cancel = (RectTransform)deleteCancelButton.transform;
            cancel.anchoredPosition = new Vector2(rect.anchoredPosition.x - width - BlockforgeTheme.Space3, cancel.anchoredPosition.y);
        }
    }

    private void SetDeleteButtonsInteractable(bool interactable)
    {
        if (deleteConfirmButton != null)
            deleteConfirmButton.interactable = interactable;
        if (deleteCancelButton != null)
            deleteCancelButton.interactable = interactable;
    }
}
