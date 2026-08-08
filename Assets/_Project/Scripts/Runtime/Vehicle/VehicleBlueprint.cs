using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blockforge.Vehicle
{
    /// <summary>
    /// Design d'un véhicule tel que sauvegardé : une liste de blocs placés
    /// sur la grille. C'est ce qui est sérialisé en JSON par le système de
    /// sauvegarde et rejoué pour reconstruire le véhicule en jeu.
    /// </summary>
    [Serializable]
    public class VehicleBlueprint
    {
        public string Name = "Nouveau véhicule";
        public List<PlacedBlock> Blocks = new();
    }

    /// <summary>Un bloc placé : quel bloc, où, et dans quelle orientation.</summary>
    [Serializable]
    public struct PlacedBlock
    {
        [Tooltip("Correspond à BlockDefinition.Id")]
        public string BlockId;
        public Vector3Int GridPosition;
        [Tooltip("Rotation en pas de 90° : 0-3 autour de chaque axe.")]
        public Vector3Int Rotation;
    }
}
