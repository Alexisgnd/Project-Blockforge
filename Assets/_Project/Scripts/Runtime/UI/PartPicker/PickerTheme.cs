using System;
using UnityEngine;
using UnityEngine.UIElements;

// =========================================================
// THEME DU PART PICKER (ScriptableObject)
// Resources/PartPicker/PickerTheme.asset, cree par
// Blockforge > Part Picker > Setup Part Picker. Deux moities,
// comme le Forge Loader :
// - ce fichier : textes (anglais, sentence case, sans emoji),
//   onglets et touches, sous-filtres par famille, tris, durees,
//   PanelSettings ;
// - Resources/PartPicker/PickerTheme.uss : TOUTES les couleurs et
//   mesures en px (variables USS). Aucun code d'ecran n'ecrit une
//   couleur ou un pixel en dur.
// Sans asset, Load() renvoie les memes valeurs par defaut.
// =========================================================

public enum PickerSort
{
    Recent = 0,
    Alphabetical = 1,
    ByCpu = 2,
    ByDamage = 3,
}

[Serializable]
public class PickerFamily
{
    [Tooltip("Libelle du sous-filtre ; PartDefinition.family doit valoir exactement ce libelle")]
    public string label;
    [Tooltip("Badge de l'apercu 3D (Laser, Plasma…) ; vide = badge de la categorie")]
    public string badge;

    public PickerFamily() { }
    public PickerFamily(string label, string badge) { this.label = label; this.badge = badge; }
}

[Serializable]
public class PickerSortOption
{
    public PickerSort sort;
    public string label;

    public PickerSortOption() { }
    public PickerSortOption(PickerSort sort, string label) { this.sort = sort; this.label = label; }
}

[Serializable]
public class PickerTabSpec
{
    [Tooltip("Libelle de l'onglet et titre de la colonne (All parts, Weapons…)")]
    public string title;
    [Tooltip("Lettre du raccourci, affichee en haut a gauche de l'onglet")]
    public string key;
    [Tooltip("Classe USS du pictogramme (pp-icon--grid, pp-icon--weapon…)")]
    public string iconClass;
    [Tooltip("Categorie filtree ; ignore pour l'onglet « All parts » (le premier)")]
    public PartCategory category;
    [Tooltip("Compteur : « 9 weapons owned » (pluriel) / « 1 weapon owned » (singulier)")]
    public string nounPlural;
    public string nounSingular;
    [Tooltip("Badge de l'apercu quand la famille n'en donne pas (Block, Wheel…)")]
    public string badge;
    public PickerFamily[] families = Array.Empty<PickerFamily>();
    public bool sortByDamage;

    public PickerTabSpec() { }

    public PickerTabSpec(string title, string key, string iconClass, PartCategory category, string nounPlural,
                         string nounSingular, string badge, bool sortByDamage, params PickerFamily[] families)
    {
        this.title = title;
        this.key = key;
        this.iconClass = iconClass;
        this.category = category;
        this.nounPlural = nounPlural;
        this.nounSingular = nounSingular;
        this.badge = badge;
        this.sortByDamage = sortByDamage;
        this.families = families;
    }
}

[Serializable]
public class PickerPoolCopy
{
    public CpuPool pool;
    [Tooltip("Titre de l'avertissement : « {title} is full »")]
    public string title;
    [Tooltip("Unite dans les phrases : « of 820 CPU », « 165 weapon CPU »")]
    public string unit;
    [Tooltip("« Remove a weapon first. »")]
    public string removeNoun;

    public PickerPoolCopy() { }
    public PickerPoolCopy(CpuPool pool, string title, string unit, string removeNoun)
    {
        this.pool = pool;
        this.title = title;
        this.unit = unit;
        this.removeNoun = removeNoun;
    }
}

[CreateAssetMenu(menuName = "Blockforge/Part Picker Theme", fileName = "PickerTheme")]
public class PickerTheme : ScriptableObject
{
    public const string ResourcePath = "PartPicker/PickerTheme";
    public const string TemplatePath = "PartPicker/PartPicker";

    [Header("Rendu")]
    [Tooltip("PanelSettings du picker : 1920 x 1080, mise a l'echelle sur la hauteur")]
    public PanelSettings panelSettings;
    [Tooltip("Resolution de reference (PanelSettings cree a la volee sans asset)")]
    public Vector2Int referenceResolution = new(1920, 1080);
    [Tooltip("Au-dessus du HUD uGUI du garage, sous le Forge Loader (1100)")]
    public int sortingOrder = 900;

    [Header("Onglets (le premier = toutes les pieces)")]
    public PickerTabSpec[] tabs =
    {
        new("All parts", "A", "pp-icon--grid", PartCategory.Blocks, "parts", "part", "", false),
        new("Blocks", "B", "pp-icon--cube", PartCategory.Blocks, "blocks", "block", "Block", false,
            new PickerFamily("Cubes", "Block"), new PickerFamily("Slopes", "Block"), new PickerFamily("Corners", "Block"),
            new PickerFamily("Rods", "Rod"), new PickerFamily("Armour", "Armour")),
        new("Movement", "M", "pp-icon--mobility", PartCategory.Movement, "movement parts", "movement part", "Movement", false,
            new PickerFamily("Wheels", "Wheel"), new PickerFamily("Legs", "Leg"), new PickerFamily("Tracks", "Track"),
            new PickerFamily("Hover", "Hover"), new PickerFamily("Thrusters", "Thruster"), new PickerFamily("Rotors", "Rotor"),
            new PickerFamily("Rudders", "Rudder")),
        new("Weapons", "W", "pp-icon--weapon", PartCategory.Weapons, "weapons", "weapon", "Weapon", true,
            new PickerFamily("Lasers", "Laser"), new PickerFamily("Plasma", "Plasma"), new PickerFamily("Rail", "Rail"),
            new PickerFamily("Flak", "Flak"), new PickerFamily("Tesla", "Tesla"), new PickerFamily("Support", "Support")),
        new("Electronics", "E", "pp-icon--electronics", PartCategory.Electronics, "electronics", "electronic", "Electronics", false,
            new PickerFamily("Sensors", "Sensor"), new PickerFamily("Shields", "Shield"), new PickerFamily("Modules", "Module")),
        new("Cosmetics", "C", "pp-icon--cosmetic", PartCategory.Cosmetics, "cosmetics", "cosmetic", "Cosmetic", false,
            new PickerFamily("Decals", "Decal"), new PickerFamily("Props", "Prop"), new PickerFamily("Lights", "Light")),
    };

    [Header("Filtres et tris")]
    public string familyAllLabel = "All";
    public PickerSortOption[] allPartsSorts =
    {
        new(PickerSort.Recent, "Recent"), new(PickerSort.Alphabetical, "A – Z"), new(PickerSort.ByCpu, "By CPU"),
    };
    public PickerSortOption[] categorySorts =
    {
        new(PickerSort.ByCpu, "By CPU"), new(PickerSort.Alphabetical, "A – Z"), new(PickerSort.ByDamage, "By damage"),
    };
    public string scopeAllLabel = "All categories";

    [Header("Textes")]
    public string searchPlaceholder = "Search parts · /";
    public string searchTitleFormat = "Results for \"{0}\"";
    [Tooltip("{0} = nombre ; {1} = nom (parts, weapons…). Les nombres passent en mono.")]
    public string ownedCountFormat = "{0} {1} owned";
    [Tooltip("{0} = resultats ; {1} = pieces possedees")]
    public string matchCountFormat = "{0} of {1} parts match";
    public string storeLinkFormat = "{0} more parts in the Store once researched";
    public string clearSearchLink = "Clear search · Esc";
    public string emptyTitleFormat = "No part matches \"{0}\"";
    [Tooltip("{0} = mot propose (lien qui relance la recherche)")]
    public string emptySuggestionFormat = "Did you mean {0}?";
    public string emptyExplanation = "Only parts you own are searched — the rest is in the Store once researched.";
    public string clearSearchButton = "Clear search";
    public string searchStoreButton = "Search the Store";
    public string detailEmpty = "Select a part to see its stats and place it.";
    public string placeLabel = "Place";
    public string pinLabel = "Pin to hotbar";
    public string closeLabel = "Close";
    public string escKey = "Esc";
    [Tooltip("{0} utilise ; {1} apres la pose ; {2} maximum ; {3} unite de la reserve")]
    public string fitsFormat = "Fits your budget: {0} → {1} of {2} {3}.";
    [Tooltip("{0} = titre de la reserve")]
    public string fullTitleFormat = "{0} is full";
    [Tooltip("{0} cout ; {1} unite ; {2} restant ; {3} maximum ; {4} piece a retirer")]
    public string fullBodyFormat = "Placing this needs {0} {1}; you have {2} of {3} left. Remove a {4} first.";
    public PickerPoolCopy[] pools =
    {
        new(CpuPool.Base, "CPU", "CPU", "part"),
        new(CpuPool.Weapon, "Weapon CPU", "weapon CPU", "weapon"),
        new(CpuPool.Cosmetic, "Cosmetic CPU", "cosmetic CPU", "cosmetic"),
    };

    [Header("Mouvement (reduce motion : tout instantane)")]
    [Tooltip("Fondu de la fenetre, du voile et du HUD (ease-out)")]
    public float fadeSeconds = 0.14f;
    [Tooltip("Mise a jour de la colonne detail apres un clic")]
    public float detailSeconds = 0.14f;
    [Tooltip("Recherche a la frappe")]
    public float searchDebounceSeconds = 0.12f;
    [Tooltip("Rotation de l'apercu 3D")]
    public float previewDegreesPerSecond = 28f;

    [Header("Recherche")]
    [Tooltip("Distance d'edition maximale de la proposition « Did you mean » (en part de la longueur du mot)")]
    [Range(0.1f, 0.6f)] public float suggestionTolerance = 0.34f;

    private static PickerTheme cached;

    public static PickerTheme Load()
    {
        if (cached != null)
            return cached;
        cached = Resources.Load<PickerTheme>(ResourcePath);
        if (cached == null)
        {
            Debug.LogWarning("[PartPicker] Resources/PartPicker/PickerTheme.asset absent : valeurs par defaut. " +
                             "Lance Blockforge > Part Picker > Setup Part Picker.");
            cached = CreateInstance<PickerTheme>();
            cached.hideFlags = HideFlags.DontSave;
        }
        return cached;
    }

    public PickerPoolCopy Pool(CpuPool pool)
    {
        foreach (var p in pools)
        {
            if (p != null && p.pool == pool)
                return p;
        }
        return new PickerPoolCopy(pool, pool.ToString() + " CPU", "CPU", "part");
    }

    // Onglet de la categorie (index >= 1), -1 si absent
    public int TabOf(PartCategory category)
    {
        for (int i = 1; i < tabs.Length; i++)
        {
            if (tabs[i] != null && tabs[i].category == category)
                return i;
        }
        return -1;
    }

    public string BadgeFor(PartDefinition part)
    {
        int tab = TabOf(part.category);
        if (tab < 0)
            return part.category.ToString();
        var spec = tabs[tab];
        if (spec.families != null)
        {
            foreach (var f in spec.families)
            {
                if (f != null && string.Equals(f.label, part.family, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(f.badge))
                    return f.badge;
            }
        }
        return spec.badge;
    }
}
