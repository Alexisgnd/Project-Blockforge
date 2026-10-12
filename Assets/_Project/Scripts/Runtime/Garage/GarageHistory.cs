using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// =========================================================
// ANNULER / RETABLIR DANS LE GARAGE
// Ajoute au demarrage par GarageBuildController (qui garde les
// piles d'historique). Toujours actif, quel que soit l'outil en
// main ou l'inventaire ouvert.
// - Ctrl+Z : annuler la derniere action (pose, retrait, trait
//   de peinture ; le jumeau miroir fait partie de l'action)
// - Ctrl+Y ou Ctrl+Maj+Z : retablir
// - Touches physiques (Input System) : Z est la touche Z d'un
//   clavier AZERTY (wKey), Y est au meme endroit sur les deux.
// - Ignore pendant la saisie d'un texte (recherche de pieces,
//   nom du robot) et derriere une popup (curseur libre hors
//   inventaire).
// - Un trait de peinture se ferme quand le jet est relache et
//   que les derniers jets en vol sont arrives.
// =========================================================

public class GarageHistory : MonoBehaviour
{
    [HideInInspector] public GarageBuildController build;
    [HideInInspector] public GaragePaintController painter;

    private GarageInventoryController inventory;

    private void Start()
    {
        inventory = build != null ? build.inventory : null;
    }

    private void Update()
    {
        if (build == null)
            return;

        var mouse = Mouse.current;
        if (build.PaintStrokeOpen && mouse != null && !mouse.leftButton.isPressed && !mouse.rightButton.isPressed
            && (painter == null || painter.PendingCount == 0))
            build.EndPaintStroke();

        var kb = Keyboard.current;
        if (kb == null || !Held(kb.ctrlKey) || !CanUseShortcuts())
            return;

        bool shift = Held(kb.shiftKey);
        bool undo = kb.wKey.wasPressedThisFrame && !shift;
        bool redo = kb.yKey.wasPressedThisFrame || (kb.wKey.wasPressedThisFrame && shift);
        if (!undo && !redo)
            return;

        painter?.CancelPending();
        bool done = undo ? build.Undo() : build.Redo();
        if (!done)
            Toast.Info(undo ? "Nothing to undo." : "Nothing to redo.");
    }

    // Modificateur tenu, ou appuye/relache dans cette image (frappe tres rapide)
    private static bool Held(UnityEngine.InputSystem.Controls.ButtonControl key) =>
        key.isPressed || key.wasPressedThisFrame || key.wasReleasedThisFrame;

    private bool CanUseShortcuts()
    {
        // Saisie de texte en cours : Ctrl+Z appartient au champ
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.TryGetComponent<TMP_InputField>(out var field) && field.isFocused)
            return false;
        // Curseur libre hors inventaire = popup ouverte (nom, quitter)
        bool inventoryOpen = inventory != null && inventory.IsOpen;
        return Cursor.lockState == CursorLockMode.Locked || inventoryOpen;
    }
}
