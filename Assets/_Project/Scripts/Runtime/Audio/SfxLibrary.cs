using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// BIBLIOTHEQUE DES EFFETS SONORES (Resources/SfxLibrary.asset)
// Un clip et ses regles de lecture par usage (SfxId).
// Creee et tenue a jour par Blockforge > Setup SFX ; volumes
// et regles se reglent dans l'Inspector.
// Chargee au demarrage par SfxManager.
// =========================================================

[CreateAssetMenu(menuName = "Blockforge/SFX Library", fileName = "SfxLibrary")]
public class SfxLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SfxId id;
        public AudioClip clip;
        [Tooltip("Volume propre au son (equilibre les sons entre eux)")]
        [Range(0f, 1f)] public float volume = 0.7f;
        [Tooltip("Ignore une nouvelle demande tant que ce son est deja en train de jouer")]
        public bool noOverlap;
        [Tooltip("Ecart minimal entre deux lectures, en secondes (0 = aucun)")]
        [Min(0f)] public float minInterval;
        [Tooltip("Variation aleatoire de hauteur (+/-), evite l'effet mitraillette d'un son repete")]
        [Range(0f, 0.2f)] public float pitchJitter;
        [Tooltip("Joue en boucle jusqu'a SfxManager.Stop")]
        public bool loop;
    }

    [Header("Volume general des effets")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Header("Sons")]
    public List<Entry> entries = new();

    public Entry Find(SfxId id)
    {
        foreach (var e in entries)
        {
            if (e != null && e.id == id)
                return e;
        }
        return null;
    }
}
