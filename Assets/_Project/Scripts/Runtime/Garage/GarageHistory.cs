using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// =========================================================
// ANNULER / RETABLIR DANS LE GARAGE
// Ajoute au demarrage par GarageBuildController (qui garde les
// piles d'historique). Toujours actif, quel que soit l'outil en
// main ou l'inventaire ouvert.
// - A : annuler la derniere action (pose, retrait, trait de
//   peinture ; le jumeau miroir fait partie de l'action)
// - E : retablir
// - Pas de Ctrl : Ctrl fait descendre le joueur. Touches
//   physiques (Input System) : A d'un clavier AZERTY = qKey,
//   E est au meme endroit sur les deux.
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
        if (kb == null || !CanUseShortcuts())
            return;

        bool undo = kb.qKey.wasPressedThisFrame;   // touche A (AZERTY)
        bool redo = kb.eKey.wasPressedThisFrame;
        if (!undo && !redo)
            return;

        painter?.CancelPending();
        bool done = undo ? build.Undo() : build.Redo();
        if (!done)
            Toast.Info(undo ? "Nothing to undo." : "Nothing to redo.");
    }

    private bool CanUseShortcuts()
    {
        // Saisie de texte en cours : les lettres appartiennent au champ
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.TryGetComponent<TMP_InputField>(out var field) && field.isFocused)
            return false;
        // Curseur libre hors inventaire = popup ouverte (nom, quitter)
        bool inventoryOpen = inventory != null && inventory.IsOpen;
        return Cursor.lockState == CursorLockMode.Locked || inventoryOpen;
    }
}
