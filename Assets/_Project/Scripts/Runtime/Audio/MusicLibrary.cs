using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// BIBLIOTHEQUE DES MUSIQUES (Resources/MusicLibrary.asset)
// - tracks : clip + volume de chaque usage (MusicTrack)
// - scenes : musique jouee a l'arrivee dans une scene
//   (scene absente de la liste = la musique en cours continue)
// Creee et tenue a jour par Blockforge > Setup Music ; les
// volumes et la liste des scenes se reglent dans l'Inspector.
// Chargee au demarrage par MusicManager.
// =========================================================

[CreateAssetMenu(menuName = "Blockforge/Music Library", fileName = "MusicLibrary")]
public class MusicLibrary : ScriptableObject
{
    [Serializable]
    public class TrackEntry
    {
        public MusicTrack track;
        public AudioClip clip;
        [Tooltip("Volume propre au morceau (equilibre les morceaux entre eux)")]
        [Range(0f, 1f)] public float volume = 0.5f;
    }

    [Serializable]
    public class SceneEntry
    {
        [Tooltip("Nom de la scene (sans .unity), ex. MainMenu")]
        public string sceneName;
        public MusicTrack track;
    }

    [Header("Fondu enchaine")]
    [Tooltip("Duree du fondu entre deux musiques, en secondes (temps reel : insensible a la pause)")]
    [Min(0.05f)] public float crossfadeDuration = 2.5f;

    [Header("Volume general de la musique")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Header("Morceaux")]
    public List<TrackEntry> tracks = new();

    [Header("Musique par scene")]
    public List<SceneEntry> scenes = new();

    public TrackEntry Find(MusicTrack track)
    {
        foreach (var entry in tracks)
        {
            if (entry != null && entry.track == track)
                return entry;
        }
        return null;
    }

    public bool TryGetSceneTrack(string sceneName, out MusicTrack track)
    {
        foreach (var entry in scenes)
        {
            if (entry != null && entry.sceneName == sceneName)
            {
                track = entry.track;
                return true;
            }
        }
        track = MusicTrack.None;
        return false;
    }
}
