using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// PART PICKER : MODELE DE DONNEES
// - PartDefinition : une piece telle que le picker l'affiche
//   (id stable, nom, categorie, famille, description, vignette,
//   prefab d'apercu 3D, lignes de stats, cout CPU et reserve).
// - IInventory : pieces possedees (seules listees ; quantite
//   illimitee une fois possedee, le CPU est la seule limite) ;
//   une piece possedee peut etre verrouillee (locked) : affichee
//   atenuee avec un cadenas, jamais selectionnable ;
//   NotOwnedCount alimente le lien « N more parts in the Store ».
// - IRobotBudget : CPU utilise / maximum par reserve du robot en
//   cours (base, armes, cosmetiques).
// Aucun grade, aucune rarete, aucune ligne de PV : le picker n'en
// affiche jamais.
// =========================================================

public enum PartCategory
{
    Blocks = 0,
    Movement = 1,
    Weapons = 2,
    Electronics = 3,
    Cosmetics = 4,
}

public enum CpuPool
{
    Base = 0,
    Weapon = 1,
    Cosmetic = 2,
}

// Ligne de stat de la colonne detail : libelle a gauche, valeur mono + unite a droite,
// barre de ratio optionnelle (ratio < 0 = pas de barre).
[Serializable]
public struct PartStat
{
    [Tooltip("Cle stable (damage, fireRate, range, cpu, mass, connections…) : sert au tri « By damage »")]
    public string id;
    public string label;
    public float value;
    [Tooltip("Format numerique (BlockforgeTheme.Num) : 0, 0.#, 0.0…")]
    public string format;
    public string unit;
    [Tooltip("Remplissage de la barre (0..1) ; negatif = pas de barre")]
    public float ratio;

    public PartStat(string id, string label, float value, string unit, string format = "0.#", float ratio = -1f)
    {
        this.id = id;
        this.label = label;
        this.value = value;
        this.unit = unit;
        this.format = format;
        this.ratio = ratio;
    }

    public bool HasBar => ratio >= 0f;
}

[Serializable]
public class PartDefinition
{
    [Tooltip("Identifiant stable (le garage y met le nom de l'asset BlockDefinition)")]
    public string id;
    public string name;
    public PartCategory category;
    [Tooltip("Famille, au libelle des sous-filtres du PickerTheme (Lasers, Plasma, Rail…)")]
    public string family;
    [TextArea] public string description;
    public Sprite thumbnail;
    [Tooltip("Modele de l'apercu 3D tournant ; vide = la vignette est affichee a la place")]
    public GameObject previewPrefab;
    public List<PartStat> stats = new();
    public int cpuCost;
    public CpuPool cpuPool;
    [Tooltip("Piece visible mais indisponible (verrou Early Access) : tuile atenuee + cadenas, jamais selectionnee ni posee")]
    public bool locked;
    [Tooltip("Raison affichee dans la fiche detail d'une piece verrouillee ; vide = PickerTheme.lockedBody")]
    public string lockedReason;

    public bool TryGetStat(string statId, out PartStat stat)
    {
        if (stats != null)
        {
            foreach (var s in stats)
            {
                if (string.Equals(s.id, statId, StringComparison.OrdinalIgnoreCase))
                {
                    stat = s;
                    return true;
                }
            }
        }
        stat = default;
        return false;
    }
}

public interface IInventory
{
    bool IsOwned(string id);
    // Pieces possedees, dans l'ordre d'acquisition (la plus ancienne d'abord)
    IReadOnlyList<PartDefinition> Owned { get; }
    // Pieces du catalogue pas encore possedees (Store, apres recherche)
    int NotOwnedCount { get; }
    event Action Changed;
}

public interface IRobotBudget
{
    int Used(CpuPool pool);
    int Max(CpuPool pool);
    event Action Changed;
}
