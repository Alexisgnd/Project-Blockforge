using UnityEngine;

// =========================================================
// SONS DE CONSTRUCTION DU GARAGE
// Ecoute GarageBuildController (BlockPlaced / BlockRemoved),
// raccorde par SfxManager a chaque scene chargee.
// - Un clic = un son, que le miroir pose / retire un ou deux
//   blocs (le controleur n'emet qu'un evenement par action).
// - Remplacement d'un type d'arme (LoadoutReplaceDialog) :
//   retrait puis pose dans la meme image -> seul le son de pose
//   est joue.
// - Annuler / retablir (A / E) : pas de son.
// =========================================================

public class GarageBuildSfx : MonoBehaviour
{
    private GarageBuildController hooked;
    private bool placed, removed;

    public void Hook(GarageBuildController controller)
    {
        if (controller == hooked)
            return;
        Unhook();
        hooked = controller;
        if (hooked != null)
        {
            hooked.BlockPlaced += OnPlaced;
            hooked.BlockRemoved += OnRemoved;
        }
    }

    private void Unhook()
    {
        if (hooked != null)
        {
            hooked.BlockPlaced -= OnPlaced;
            hooked.BlockRemoved -= OnRemoved;
        }
        hooked = null;
        placed = removed = false;
    }

    private void OnDestroy() => Unhook();

    private void OnPlaced() => placed = true;
    private void OnRemoved() => removed = true;

    // Apres les Update du controleur : decide une fois par image
    private void LateUpdate()
    {
        if (placed)
            SfxManager.Play(SfxId.GarageBlockPlace);
        else if (removed)
            SfxManager.Play(SfxId.GarageBlockRemove);
        placed = removed = false;
    }
}
