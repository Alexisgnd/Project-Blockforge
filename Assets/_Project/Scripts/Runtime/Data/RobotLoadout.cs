using System.Collections.Generic;
using UnityEngine;

// =========================================================
// CHARGEMENT D'UN ROBOT : ARMES ET MODULES
// Regle du builder : un robot embarque au plus MaxTypes (3)
// types d'armes et de modules confondus (2 armes + 1 module,
// 1 arme + 2 modules, 3 armes...), toutes quantites confondues
// (8 Laser Wasp = un seul type). Un type = un bloc du catalogue
// (blockId) : Laser Wasp et Laser Hornet sont deux types.
// - Arme  : categorie Armes (lasers, plasma, rail, Tesla)
// - Module: categorie Special (radar, disque de bouclier) et
//           famille "Nano dispenser" (soins, reconstruction,
//           brouillage) ; les electroplates restent du blindage
// L'ordre des types est celui de leur premiere apparition dans
// le blueprint : il donne les slots 1, 2, 3 du HUD de match.
// Partage par le garage (GarageBuildController, popup de
// remplacement) et le match (RobotLoadoutState, RobotTestHUD).
// =========================================================

public enum LoadoutKind { None, Weapon, Module }

public static class RobotLoadout
{
    public const int MaxTypes = 3;
    public const string ModuleFamily = "Nano dispenser";

    public static LoadoutKind Kind(BlockDefinition def)
    {
        if (def == null)
            return LoadoutKind.None;
        if (def.category == BlockCategory.Armes)
            return LoadoutKind.Weapon;
        if (def.category == BlockCategory.Special || def.family == ModuleFamily)
            return LoadoutKind.Module;
        return LoadoutKind.None;
    }

    public static bool Counts(BlockDefinition def) => Kind(def) != LoadoutKind.None;

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
