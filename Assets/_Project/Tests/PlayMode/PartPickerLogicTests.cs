using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// =========================================================
// TESTS DU PART PICKER (regles pures, donnees simulees)
// Recherche par nom sur les pieces possedees, portee par
// categorie, « Did you mean », tris, budget par reserve.
// =========================================================

public class PartPickerLogicTests
{
    private static List<PartDefinition> Owned() => PickerMockCatalog.DefaultParts().Select(m => m.part).ToList();

    [Test]
    public void MockCatalog_MatchesTheDesignNumbers()
    {
        var owned = Owned();
        Assert.AreEqual(38, owned.Count, "38 parts owned");
        Assert.AreEqual(9, owned.Count(p => p.category == PartCategory.Weapons), "9 weapons owned");
        Assert.AreEqual(owned.Count, owned.Select(p => p.id).Distinct().Count(), "ids uniques");
    }

    [Test]
    public void Search_Turret_FindsSixAcrossThreeCategories()
    {
        var matches = PartPickerLogic.Search(Owned(), "turret");
        Assert.AreEqual(6, matches.Count);
        Assert.AreEqual(4, matches.Count(p => p.category == PartCategory.Weapons));
        Assert.AreEqual(1, matches.Count(p => p.category == PartCategory.Electronics));
        Assert.AreEqual(1, matches.Count(p => p.category == PartCategory.Cosmetics));
    }

    [Test]
    public void Search_IsCaseInsensitive_AndNameOnly()
    {
        Assert.AreEqual(6, PartPickerLogic.Search(Owned(), "TuRReT").Count);
        // « laser » n'est que dans les descriptions et les familles de certaines pieces : le nom seul compte
        Assert.IsTrue(PartPickerLogic.Search(Owned(), "laser").All(p => p.name.ToLowerInvariant().Contains("laser")));
    }

    [Test]
    public void Suggest_Plasmo_ProposesPlasma()
    {
        Assert.AreEqual(0, PartPickerLogic.Search(Owned(), "plasmo").Count);
        Assert.AreEqual("plasma", PartPickerLogic.Suggest(Owned(), "plasmo", 0.34f));
    }

    [Test]
    public void Suggest_Gibberish_ProposesNothing()
    {
        Assert.IsNull(PartPickerLogic.Suggest(Owned(), "zzqxv", 0.34f));
    }

    [Test]
    public void Sort_ByCpu_IsAscending_ByDamage_IsDescending()
    {
        var weapons = Owned().Where(p => p.category == PartCategory.Weapons).ToList();
        PartPickerLogic.Sort(weapons, PickerSort.ByCpu, null, null);
        for (int i = 1; i < weapons.Count; i++)
            Assert.LessOrEqual(weapons[i - 1].cpuCost, weapons[i].cpuCost);

        PartPickerLogic.Sort(weapons, PickerSort.ByDamage, null, null);
        for (int i = 1; i < weapons.Count; i++)
        {
            weapons[i - 1].TryGetStat("damage", out var a);
            weapons[i].TryGetStat("damage", out var b);
            Assert.GreaterOrEqual(a.value, b.value);
        }
    }

    [Test]
    public void Sort_Recent_PutsLastUsedFirst_ThenNewestAcquired()
    {
        var parts = Owned();
        var order = new Dictionary<string, int>();
        for (int i = 0; i < parts.Count; i++)
            order[parts[i].id] = i;
        var recent = new List<string> { "mock_flak_turret", "mock_cube" };
        PartPickerLogic.Sort(parts, PickerSort.Recent, recent, order);
        Assert.AreEqual("mock_flak_turret", parts[0].id);
        Assert.AreEqual("mock_cube", parts[1].id);
        Assert.AreEqual("mock_headlight", parts[2].id, "puis la derniere acquise");
    }

    [Test]
    public void Budget_WeaponPoolFull_BlocksWeapons_NotBlocks()
    {
        var catalog = UnityEngine.ScriptableObject.CreateInstance<PickerMockCatalog>();
        var budget = new MockRobotBudget(catalog);
        var owned = Owned();
        var bombarder = owned.First(p => p.id == "mock_bombarder");
        var cube = owned.First(p => p.id == "mock_cube");

        Assert.IsFalse(PartPickerLogic.Fits(bombarder, budget, out int wUsed, out int wMax));
        Assert.AreEqual(120, wUsed);
        Assert.AreEqual(120, wMax);
        Assert.IsTrue(PartPickerLogic.Fits(cube, budget, out int used, out int max));
        Assert.AreEqual(785, used);
        Assert.AreEqual(820, max);
        UnityEngine.Object.DestroyImmediate(catalog);
    }

    [Test]
    public void WeaponRatios_AreRelativeToTheStrongestWeapon()
    {
        var weapons = Owned().Where(p => p.category == PartCategory.Weapons).ToList();
        foreach (var w in weapons)
            Assert.IsTrue(w.stats.All(s => s.HasBar && s.ratio >= 0f && s.ratio <= 1f), w.name);
        Assert.IsTrue(weapons.Any(w => w.TryGetStat("damage", out var s) && UnityEngine.Mathf.Approximately(s.ratio, 1f)));
        // Pas de ligne de PV, ni grade, ni rarete
        Assert.IsFalse(Owned().Any(p => p.stats.Any(s => s.id == "health" || s.label.ToLowerInvariant().Contains("health"))));
    }
}
