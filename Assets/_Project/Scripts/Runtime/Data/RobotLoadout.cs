using System.Collections.Generic;
using UnityEngine;

// =========================================================
// CHARGEMENT D'UN ROBOT : ARMES ET MODULES
// Regle du builder : un robot embarque au plus MaxTypes (3)
// types d'armes et de modules confondus (2 armes + 1 module,
// 1 arme + 2 modules, 3 armes...), toutes quantites confondues
// (8 Laser Wasp = un seul type). Un type = un bloc du catalogue
// (blockId) : Laser Wasp et Laser Hornet sont deux types.
// - Arme  : categorie Armes (lasers, plasma, rail, Tesla) et
//           arme healer (famille "Nano dispenser" : Mender, soins,
//           et Constructor, reconstruction)
// - Module: categorie Special (disque de bouclier, radar) et IEM
//           (Nano Blinder, brouillage) ; les electroplates restent
//           du blindage
// Verrouillage de l'Early Access (IsLocked) : les modules et
// l'arme healer n'ont pas encore d'effet en match. Ils restent
// visibles dans le garage (inventaire, part picker) avec un
// cadenas mais ne se selectionnent pas, ne se posent plus et ne comptent pas dans la limite ni dans les
// slots du match (robot anterieur qui en porte : blocs inertes).
// Pour les rouvrir : vider LockedFamilies / LockedBlockIds et
// passer LockModules a false.
// L'ordre des types est celui de leur premiere apparition dans
// le blueprint : il donne les slots 1, 2, 3 du HUD de match.
// Partage par le garage (GarageBuildController, popup de
// remplacement) et le match (RobotLoadoutState, RobotTestHUD).
// =========================================================

public enum LoadoutKind { None, Weapon, Module }

public static class RobotLoadout
{
    public const int MaxTypes = 3;
    // Arme healer (soins, reconstruction) ; le Blinder de la meme famille est l'IEM
    public const string HealerFamily = "Nano dispenser";
    // IEM : bloc de la famille Nano classe en module (id = nom de l'asset)
    public const string EmpBlockId = "Block_Nano_N1_Blinder";

    // ---------- Verrouillage Early Access ----------
    // Tous les modules (disque de bouclier, radar, IEM) sont verrouilles
    public static readonly bool LockModules = true;
    // Familles d'armes verrouillees (arme healer)
    private static readonly string[] LockedFamilies = { HealerFamily };
    // Blocs verrouilles un par un (en plus des regles ci-dessus)
    private static readonly string[] LockedBlockIds = { };

    public const string LockedReason = "Locked in this version: not available yet.";

    public static bool IsEmp(BlockDefinition def) => def != null && def.name == EmpBlockId;

    public static LoadoutKind Kind(BlockDefinition def)
    {
        if (def == null)
            return LoadoutKind.None;
        if (def.category == BlockCategory.Special || IsEmp(def))
            return LoadoutKind.Module;
        if (def.category == BlockCategory.Armes || def.family == HealerFamily)
            return LoadoutKind.Weapon;
        return LoadoutKind.None;
    }

    // Bloc indisponible dans cette version (garage et match)
    public static bool IsLocked(BlockDefinition def)
    {
        if (def == null)
            return false;
        if (LockModules && Kind(def) == LoadoutKind.Module)
            return true;
        foreach (var f in LockedFamilies)
        {
            if (def.family == f)
                return true;
        }
        foreach (var id in LockedBlockIds)
        {
            if (def.name == id)
                return true;
        }
        return false;
    }

    // Compte dans la limite de types et donne un slot en match (bloc verrouille : non)
    public static bool Counts(BlockDefinition def) => Kind(def) != LoadoutKind.None && !IsLocked(def);

    public static string KindLabel(LoadoutKind kind) => kind switch
    {
        LoadoutKind.Weapon => "Weapon",
        LoadoutKind.Module => "Module",
        _ => "",
    };

    // Pictogramme du DS quand le bloc n'a pas d'icone
    public static string KindIcon(LoadoutKind kind) => kind == LoadoutKind.Module ? "electronics" : "weapon";

    public struct Entry
    {
        public BlockDefinition definition;
        public int count;
        public LoadoutKind kind;
    }

    // Types d'armes et de modules d'une liste de blocs, dans l'ordre de leur
    // premiere apparition, avec leur quantite
    public static List<Entry> Collect(IEnumerable<BlockDefinition> blocks)
    {
        var result = new List<Entry>();
        var index = new Dictionary<BlockDefinition, int>();
        foreach (var def in blocks)
        {
            if (!Counts(def))
                continue;
            if (index.TryGetValue(def, out int i))
            {
                var e = result[i];
                e.count++;
                result[i] = e;
            }
            else
            {
                index[def] = result.Count;
                result.Add(new Entry { definition = def, count = 1, kind = Kind(def) });
            }
        }
        return result;
    }

    public static List<Entry> Collect(RobotBlueprint blueprint, IReadOnlyDictionary<string, BlockDefinition> defs)
    {
        var list = new List<BlockDefinition>();
        if (blueprint != null && defs != null)
        {
            foreach (var b in blueprint.blocks)
            {
                if (b != null && !string.IsNullOrEmpty(b.blockId) && defs.TryGetValue(b.blockId, out var def))
                    list.Add(def);
            }
        }
        return Collect(list);
    }

    // Poser def ajouterait-il un type au-dela de la limite ?
    public static bool WouldExceed(List<Entry> current, BlockDefinition def)
    {
        if (!Counts(def))
            return false;
        foreach (var e in current)
        {
            if (e.definition == def)
                return false;
        }
        return current.Count >= MaxTypes;
    }
}
