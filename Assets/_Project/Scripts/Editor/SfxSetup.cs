using System.IO;
using UnityEditor;
using UnityEngine;

// =========================================================
// SETUP DES EFFETS SONORES
// Menu : Blockforge > Setup SFX
// 1. Reglages d'import des SFX_*.wav listes ci-dessous :
//    sons courts (< 2 s) decompresses au chargement (PCM,
//    aucune latence), sons longs compresses en memoire
//    (Vorbis 70). Stereo conserve : ce sont des sons 2D.
// 2. Cree / complete Resources/SfxLibrary.asset : un clip et
//    ses regles de lecture par usage (SfxId).
// Idempotent : un clip deja relie, un volume ou une regle deja
// reglee dans l'Inspector ne sont jamais ecrases.
// S'execute aussi une seule fois tout seul apres compilation
// (marqueur dans Library/).
// Ajouter un son : une valeur dans SfxId, une ligne ici, relancer le menu.
// =========================================================

[InitializeOnLoad]
public static class SfxSetup
{
    private const string SfxFolder = "Assets/_Project/Audio/SFX";
    private const string LibraryPath = "Assets/_Project/Resources/SfxLibrary.asset";
    // Marqueur versionne : a changer quand la liste s'allonge, pour que les
    // nouveaux sons soient ajoutes tout seuls a la bibliotheque existante
    private const string AutoRunMarker = "Library/BlockforgeSfxSetup.v3.done";

    // Volumes de depart egalises a l'oreille sur le niveau RMS des fichiers.
    // Les fichiers ne doivent jamais depasser 0 dBFS (crete visee -1 dBFS) :
    // un WAV flottant au-dessus de 1,0 est ecrete a l'import (PCM 16 bits) et gresille.
    // Regen, NoEnergy et RegenInterrupted ont ete ramenes de +1,1 a -1 dBFS (-2 dB) :
    // leurs volumes sont remontes d'autant.
    private static readonly (SfxId id, string file, float volume, bool noOverlap, float minInterval, float pitchJitter, bool shortClip, bool loop)[] Sounds =
    {
        (SfxId.MatchAlert,        "Match/Alerts/SFX_Match_Alert.wav",                   0.70f, false, 0.5f,  0f,    false, false),
        (SfxId.RobotRegen,        "Damage/Regen/SFX_Robot_Regen.wav",                   0.76f, true,  0f,    0f,    false, false),
        (SfxId.WeaponNoEnergy,    "Weapons/Common/SFX_Weapon_NoEnergy.wav",             0.51f, true,  0f,    0f,    true,  false),
        (SfxId.GarageBlockPlace,  "Garage/Build/SFX_Garage_BlockPlace.wav",             1.00f, false, 0.03f, 0.05f, true,  false),
        (SfxId.GarageBlockRemove, "Garage/Build/SFX_Garage_BlockRemove.wav",            1.00f, false, 0.03f, 0.05f, true,  false),
        (SfxId.RegenCountdown,    "Damage/Regen/SFX_Robot_RegenCountdown_Loop.wav",     0.80f, true,  0f,    0f,    true,  true),
        (SfxId.RegenInterrupted,  "Damage/Regen/SFX_Robot_RegenInterrupted.wav",        0.77f, false, 0.2f,  0f,    true,  false),
        (SfxId.RespawnTick,       "Match/Respawn/SFX_Match_RespawnTick.wav",            0.90f, false, 0.5f,  0f,    true,  false),
    };

    static SfxSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Setup SFX")]
    public static void SetupFromMenu()
    {
        Setup();
    }

    private static bool Setup()
    {
        bool ok = true;
        foreach (var s in Sounds)
            ok &= ConfigureImport($"{SfxFolder}/{s.file}", s.shortClip);

        var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        if (library == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            library = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        foreach (var s in Sounds)
        {
            var entry = library.Find(s.id);
            if (entry == null)
            {
                entry = new SfxLibrary.Entry
                {
                    id = s.id,
                    volume = s.volume,
                    noOverlap = s.noOverlap,
                    minInterval = s.minInterval,
                    pitchJitter = s.pitchJitter,
                    loop = s.loop,
                };
                library.entries.Add(entry);
            }
            if (entry.clip == null)
                entry.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{s.file}");
            if (entry.clip == null)
                ok = false;
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssetIfDirty(library);

        if (ok)
            Debug.Log($"[SfxSetup] {Sounds.Length} sons configures, bibliotheque : {LibraryPath}");
        else
            Debug.LogWarning($"[SfxSetup] Sons manquants dans {SfxFolder} : bibliotheque incomplete.");
        return ok;
    }

    private static bool ConfigureImport(string path, bool shortClip)
    {
        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
        {
            Debug.LogWarning($"[SfxSetup] Introuvable : {path}");
            return false;
        }

        var loadType = shortClip ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
        var format = shortClip ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;

        var s = importer.defaultSampleSettings;
        bool changed = s.loadType != loadType
                       || s.compressionFormat != format
                       || (!shortClip && !Mathf.Approximately(s.quality, 0.7f))
                       || s.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate
                       || !s.preloadAudioData
                       || importer.forceToMono
                       || importer.loadInBackground;
        if (!changed)
            return true;

        s.loadType = loadType;
        s.compressionFormat = format;
        if (!shortClip)
            s.quality = 0.7f;
        s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        s.preloadAudioData = true;
        importer.defaultSampleSettings = s;
        importer.forceToMono = false;
        importer.loadInBackground = false;
        importer.SaveAndReimport();
        return true;
    }
}
