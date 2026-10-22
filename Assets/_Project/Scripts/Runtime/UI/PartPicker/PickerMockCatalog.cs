using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =========================================================
// CATALOGUE SIMULE DU PART PICKER (ScriptableObject)
// Resources/PartPicker/PickerMockCatalog.asset : 38 pieces
// possedees (dont 9 armes ; « turret » en trouve 6 : 4 armes,
// 1 electronique, 1 cosmetique ; « plasmo » propose « plasma »),
// 42 pieces au Store, budget 785 / 820 CPU, 120 / 120 weapon CPU
// (la reserve d'armes est pleine), 12 / 40 cosmetic CPU.
// Le setup y relie vignettes et prefabs des BlockDefinition du
// projet (sourceBlockId) ; sans asset, memes donnees sans image.
// =========================================================

[Serializable]
public class MockPart
{
    public PartDefinition part = new();
    [Tooltip("BlockDefinition dont le setup reprend l'icone et le prefab d'apercu")]
    public string sourceBlockId;
}

[CreateAssetMenu(menuName = "Blockforge/Part Picker Mock Catalog", fileName = "PickerMockCatalog")]
public class PickerMockCatalog : ScriptableObject
{
    public const string ResourcePath = "PartPicker/PickerMockCatalog";

    public List<MockPart> owned = new();
    public int notOwnedCount = 42;
    public int baseUsed = 785, baseMax = 820;
    public int weaponUsed = 120, weaponMax = 120;
    public int cosmeticUsed = 12, cosmeticMax = 40;

    public static PickerMockCatalog Load()
    {
        var asset = Resources.Load<PickerMockCatalog>(ResourcePath);
        if (asset != null && asset.owned.Count > 0)
            return asset;
        var fallback = CreateInstance<PickerMockCatalog>();
        fallback.hideFlags = HideFlags.DontSave;
        fallback.owned = DefaultParts();
        return fallback;
    }

    // ---------- Catalogue par defaut ----------

    private static MockPart Part(string id, string name, PartCategory category, string family, int cpu, CpuPool pool,
                                 string description, string source, params PartStat[] stats)
    {
        return new MockPart
        {
            sourceBlockId = source,
            part = new PartDefinition
            {
                id = id, name = name, category = category, family = family, cpuCost = cpu, cpuPool = pool,
                description = description, stats = stats.ToList(),
            },
        };
    }

    private static PartStat[] BlockStats(int cpu, float massKg, int faces) => new[]
    {
        new PartStat("cpu", "Base CPU", cpu, "per cube", "0"),
        new PartStat("mass", "Mass", massKg, "kg", "0.0"),
        new PartStat("connections", "Connections", faces, faces == 1 ? "face" : "faces", "0"),
    };

    private static PartStat[] BasicStats(string cpuLabel, int cpu, float massKg) => new[]
    {
        new PartStat("cpu", cpuLabel, cpu, "CPU", "0"),
        new PartStat("mass", "Mass", massKg, "kg", "0.#"),
    };

    // Armes : barres de ratio calculees ensuite (valeur / maximum des armes possedees)
    private static PartStat[] WeaponStats(float damage, float rate, float range, int cpu, float massKg) => new[]
    {
        new PartStat("damage", "Damage", damage, "per shot", "0"),
        new PartStat("fireRate", "Fire rate", rate, "/s", "0.0"),
        new PartStat("range", "Range", range, "m", "0"),
        new PartStat("cpu", "Weapon CPU", cpu, "CPU", "0"),
        new PartStat("mass", "Mass", massKg, "kg", "0.#"),
    };

    public static List<MockPart> DefaultParts()
    {
        const PartCategory B = PartCategory.Blocks, M = PartCategory.Movement, W = PartCategory.Weapons,
                           E = PartCategory.Electronics, C = PartCategory.Cosmetics;
        var list = new List<MockPart>
        {
            // Blocs (10)
            Part("mock_cube", "Cube", B, "Cubes", 1, CpuPool.Base, "The basic block. Solid, light and easy to build on.", "Block_01_Cube", BlockStats(1, 1.8f, 6)),
            Part("mock_slope", "Slope", B, "Slopes", 1, CpuPool.Base, "An angled face that deflects incoming fire.", "Block_02_Pente", BlockStats(1, 1.5f, 4)),
            Part("mock_round_slope", "Round slope", B, "Slopes", 1, CpuPool.Base, "A slope with a curved profile for smooth bodywork.", "Block_Chassis_PenteArrondie", BlockStats(1, 1.5f, 4)),
            Part("mock_pyramid", "Pyramid", B, "Slopes", 1, CpuPool.Base, "A four-sided tip for spikes and deflectors.", "Block_Chassis_Pyramide", BlockStats(1, 1.1f, 1)),
            Part("mock_cone", "Cone", B, "Slopes", 1, CpuPool.Base, "A conical tip for noses and bumpers.", "Block_Chassis_Cone", BlockStats(1, 1.0f, 1)),
            Part("mock_corner", "Corner", B, "Corners", 1, CpuPool.Base, "A finishing angle for bodywork.", "Block_03_Coin", BlockStats(1, 1.2f, 3)),
            Part("mock_inner_corner", "Inner corner", B, "Corners", 1, CpuPool.Base, "A recessed angle that joins two slopes.", "Block_04_Interieur", BlockStats(1, 1.7f, 5)),
            Part("mock_short_rod", "Short rod", B, "Rods", 1, CpuPool.Base, "A short bar that links two close points.", "Block_Rod_Court", BlockStats(1, 0.6f, 2)),
            Part("mock_long_rod", "Long rod", B, "Rods", 2, CpuPool.Base, "A long bar for arms and outrigger booms.", "Block_Rod_Long", BlockStats(2, 1.1f, 2)),
            Part("mock_armour_plate", "Armour plate", B, "Armour", 4, CpuPool.Base, "A bevelled plate that soaks up angled fire.", "Block_Electroplate_T5", BlockStats(4, 6.0f, 1)),

            // Mouvement (9)
            Part("mock_scout_wheel", "Scout wheel", M, "Wheels", 4, CpuPool.Base, "A light wheel. Top speed, minimal grip.", "Block_Wheel_N1_Scout", BasicStats("Base CPU", 4, 9)),
            Part("mock_discover_wheel", "Discover wheel", M, "Wheels", 6, CpuPool.Base, "A versatile wheel. A good balance of speed and grip.", "Block_Wheel_N2_Discover", BasicStats("Base CPU", 6, 12)),
            Part("mock_monster_wheel", "Monster wheel", M, "Wheels", 18, CpuPool.Base, "A giant wheel. Crushes obstacles but weighs the chassis down.", "Block_Wheel_N6_Monster", BasicStats("Base CPU", 18, 42)),
            Part("mock_walker_leg", "Walker leg", M, "Legs", 10, CpuPool.Base, "A light jointed leg. Climbs smoothly and quietly.", "Block_InsectLeg_N1_Walker", BasicStats("Base CPU", 10, 19)),
            Part("mock_bison_track", "Bison track", M, "Tracks", 10, CpuPool.Base, "A compact three-wheel track. Reliable traction on any terrain.", "Block_TankTrack_N1_Bison", BasicStats("Base CPU", 10, 40)),
            Part("mock_squall_hover", "Squall hover blade", M, "Hover", 8, CpuPool.Base, "Low-altitude lift. Fast, twitchy gliding.", "Block_HoverBlade_N1_Squall", BasicStats("Base CPU", 8, 13)),
            Part("mock_lynx_thruster", "Lynx thruster", M, "Thrusters", 9, CpuPool.Base, "A booster thruster. Short thrust, low consumption.", "Block_Thruster_N1_Lynx", BasicStats("Base CPU", 9, 15)),
            Part("mock_recon_rotor", "Recon rotor", M, "Rotors", 10, CpuPool.Base, "A light two-blade rotor. Agile, quiet hovering.", "Block_RotorBlade_Recon", BasicStats("Base CPU", 10, 12)),
            Part("mock_hawk_rudder", "Hawk rudder", M, "Rudders", 6, CpuPool.Base, "A light rudder. Sharp turns at low speed.", "Block_Rudder_N1_Hawk", BasicStats("Base CPU", 6, 6)),

            // Armes (9) : 4 « turret »
            Part("mock_wasp_turret", "Wasp laser turret", W, "Lasers", 40, CpuPool.Weapon, "A light laser turret with a high fire rate.", "Block_Laser_N1_Wasp", WeaponStats(42, 7f, 320, 40, 12)),
            Part("mock_hornet_laser", "Hornet laser", W, "Lasers", 55, CpuPool.Weapon, "A twin-capacitor laser. A good damage-to-weight balance.", "Block_Laser_N2_Hornet", WeaponStats(64, 6f, 340, 55, 16)),
            Part("mock_pulser_turret", "Pulser plasma turret", W, "Plasma", 70, CpuPool.Weapon, "A compact plasma launcher. Fast short-range bursts.", "Block_Plasma_N1_Pulser", WeaponStats(96, 2.4f, 180, 70, 22)),
            Part("mock_bombarder", "Bombarder plasma", W, "Plasma", 165, CpuPool.Weapon, "Lobbed shots with a wide blast area. Ideal against groups.", "Block_Plasma_N3_Bombarder", WeaponStats(210, 1.7f, 220, 165, 34)),
            Part("mock_piercer_rail", "Piercer rail", W, "Rail", 120, CpuPool.Weapon, "A light rail cannon. Punches through thin blocks in one shot.", "Block_Rail_N1_Piercer", WeaponStats(360, 0.8f, 600, 120, 40)),
            Part("mock_flak_turret", "Flak turret", W, "Flak", 65, CpuPool.Weapon, "Bursting shells that shred rotors and hover blades.", "Block_Plasma_N2_Disruptor", WeaponStats(48, 5f, 260, 65, 20)),
            Part("mock_arc_turret", "Arc tesla turret", W, "Tesla", 90, CpuPool.Weapon, "A star-discharge emitter. Hits every adjacent block.", "Block_Tesla_N3_Nova", WeaponStats(130, 4f, 12, 90, 18)),
            Part("mock_slicer_blade", "Slicer blade", W, "Tesla", 45, CpuPool.Weapon, "A short electrified blade. Cuts on contact.", "Block_Tesla_N1_Slicer", WeaponStats(78, 4f, 8, 45, 10)),
            Part("mock_mender_beam", "Mender beam", W, "Support", 60, CpuPool.Weapon, "Repairs the blocks of an ally robot in its beam.", "Block_Nano_N2_Mender", WeaponStats(0, 1f, 60, 60, 14)),

            // Electronique (6) : 1 « turret »
            Part("mock_radar", "Radar sensor", E, "Sensors", 18, CpuPool.Base, "Shows nearby enemies on the minimap.", "Block_19_Radar", BasicStats("Base CPU", 18, 7)),
            Part("mock_proximity", "Proximity sensor", E, "Sensors", 6, CpuPool.Base, "Warns you when an enemy closes in from behind.", "Block_19_Radar", BasicStats("Base CPU", 6, 2)),
            Part("mock_shield_disk", "Shield disk", E, "Shields", 26, CpuPool.Base, "Projects a directional energy barrier.", "Block_20_DisqueBouclier", BasicStats("Base CPU", 26, 14)),
            Part("mock_stabiliser", "Turret stabiliser", E, "Modules", 12, CpuPool.Base, "Steadies your weapons while you drive over rough ground.", "Block_Nano_N3_Constructor", BasicStats("Base CPU", 12, 5)),
            Part("mock_blinder", "Nano blinder", E, "Modules", 14, CpuPool.Base, "A blinding nanite cloud. Scrambles enemy sensors.", "Block_Nano_N1_Blinder", BasicStats("Base CPU", 14, 9)),
            Part("mock_energy_cell", "Energy cell", E, "Modules", 10, CpuPool.Base, "Stores extra energy for your weapons.", "Block_Electroplate_T2", BasicStats("Base CPU", 10, 6)),

            // Cosmetiques (4) : 1 « turret »
            Part("mock_turret_flag", "Turret flag", C, "Props", 2, CpuPool.Cosmetic, "A pennant that flies from the top of your turret.", "Block_Rod_Diag3D", BasicStats("Cosmetic CPU", 2, 0.4f)),
            Part("mock_antenna", "Antenna whip", C, "Props", 1, CpuPool.Cosmetic, "A long whip antenna that sways as you drive.", "Block_Rod_Arc", BasicStats("Cosmetic CPU", 1, 0.2f)),
            Part("mock_hazard", "Hazard stripes", C, "Decals", 1, CpuPool.Cosmetic, "Yellow and black stripes for bumpers and edges.", "Block_Electroplate_T3", BasicStats("Cosmetic CPU", 1, 0f)),
            Part("mock_headlight", "Headlight", C, "Lights", 2, CpuPool.Cosmetic, "A forward lamp for night arenas.", "Block_Chassis_Cone", BasicStats("Cosmetic CPU", 2, 0.5f)),
        };
        ApplyWeaponRatios(list.Select(m => m.part));
        return list;
    }

    // Barres des armes : chaque stat rapportee au maximum des armes de la liste
    public static void ApplyWeaponRatios(IEnumerable<PartDefinition> parts)
    {
        var weapons = parts.Where(p => p != null && p.category == PartCategory.Weapons && p.stats != null).ToList();
        var max = new Dictionary<string, float>();
        foreach (var w in weapons)
        foreach (var s in w.stats)
            max[s.id] = Mathf.Max(max.TryGetValue(s.id, out var m) ? m : 0f, s.value);
        foreach (var w in weapons)
        {
            for (int i = 0; i < w.stats.Count; i++)
            {
                var s = w.stats[i];
                s.ratio = max.TryGetValue(s.id, out var m) && m > 0f ? s.value / m : 0f;
                w.stats[i] = s;
            }
        }
    }
}
