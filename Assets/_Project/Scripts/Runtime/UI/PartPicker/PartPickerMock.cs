using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =========================================================
// INVENTAIRE ET BUDGET SIMULES DU PART PICKER
// IInventory / IRobotBudget sur le PickerMockCatalog (scene vide,
// banc d'essai, tests). Poser une piece la compte dans le budget.
// =========================================================

public sealed class MockInventory : IInventory
{
    private readonly List<PartDefinition> owned;
    private readonly HashSet<string> ids;

    public MockInventory(IEnumerable<PartDefinition> parts, int notOwned)
    {
        owned = parts.Where(p => p != null).ToList();
        ids = new HashSet<string>(owned.Select(p => p.id));
        NotOwnedCount = notOwned;
    }

    public bool IsOwned(string id) => id != null && ids.Contains(id);
    public IReadOnlyList<PartDefinition> Owned => owned;
    public int NotOwnedCount { get; }
    public event Action Changed { add { } remove { } }
}

public sealed class MockRobotBudget : IRobotBudget
{
    private readonly int[] used = new int[3];
    private readonly int[] max = new int[3];

    public event Action Changed;

    public MockRobotBudget(PickerMockCatalog c)
    {
        used[(int)CpuPool.Base] = c.baseUsed; max[(int)CpuPool.Base] = c.baseMax;
        used[(int)CpuPool.Weapon] = c.weaponUsed; max[(int)CpuPool.Weapon] = c.weaponMax;
        used[(int)CpuPool.Cosmetic] = c.cosmeticUsed; max[(int)CpuPool.Cosmetic] = c.cosmeticMax;
    }

    public int Used(CpuPool pool) => used[(int)pool];
    public int Max(CpuPool pool) => max[(int)pool];

    public void Add(CpuPool pool, int cpu)
    {
        used[(int)pool] += cpu;
        Changed?.Invoke();
    }
}
