using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// ARMES ET MODULES DU ROBOT EN MATCH (slots 1, 2, 3)
// Ajoute au robot par RobotTestSpawner. Lit les blocs montes
// (PlacedBlockView, dans l'ordre du blueprint) et en tire au
// plus RobotLoadout.MaxTypes slots : un type d'arme ou de
// module par slot, avec sa quantite. Un robot construit avant
// la regle des 3 types ne garde que ses 3 premiers.
// Touches 1 / 2 / 3 de la rangee du haut (touches physiques :
// & é " sur AZERTY) ou du pave numerique : choisit le slot
// actif. Le premier slot est actif au depart.
// SelectedChanged previent le HUD ; le futur systeme de tir
// lira Selected / SelectedBlocks.
// =========================================================

public class RobotLoadoutState : MonoBehaviour
{
    public class Slot
    {
        public BlockDefinition definition;
        public LoadoutKind kind;
        public readonly List<PlacedBlockView> blocks = new();
        public int Count => blocks.Count;
    }

    public readonly List<Slot> slots = new();
    public int SelectedIndex { get; private set; } = -1;
    public Slot Selected => SelectedIndex >= 0 && SelectedIndex < slots.Count ? slots[SelectedIndex] : null;
    public IReadOnlyList<PlacedBlockView> SelectedBlocks => Selected != null ? Selected.blocks : System.Array.Empty<PlacedBlockView>();

    public event System.Action<int> SelectedChanged;

    private void Awake()
    {
        Rebuild();
    }

    // Relit les blocs du robot (appele a la creation)
    public void Rebuild()
    {
        slots.Clear();
        var byDef = new Dictionary<BlockDefinition, Slot>();
        foreach (var view in GetComponentsInChildren<PlacedBlockView>())
        {
            var def = view.definition;
            if (!RobotLoadout.Counts(def))
                continue;
            if (!byDef.TryGetValue(def, out var slot))
            {
                if (slots.Count >= RobotLoadout.MaxTypes)
                    continue; // au-dela de la limite : ignore (robot anterieur a la regle)
                slot = new Slot { definition = def, kind = RobotLoadout.Kind(def) };
                byDef[def] = slot;
                slots.Add(slot);
            }
            slot.blocks.Add(view);
        }
        SelectedIndex = slots.Count > 0 ? 0 : -1;
    }

    public void Select(int index)
    {
        if (index < 0 || index >= slots.Count || index == SelectedIndex)
            return;
        SelectedIndex = index;
        SelectedChanged?.Invoke(index);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || slots.Count == 0)
            return;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Select(0);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Select(1);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Select(2);
    }
}
