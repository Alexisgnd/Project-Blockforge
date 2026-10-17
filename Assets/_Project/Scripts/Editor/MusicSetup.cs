using System.IO;
using UnityEditor;
using UnityEngine;

// =========================================================
// SETUP DES MUSIQUES
// Menu : Blockforge > Setup Music
// 1. Reglages d'import des MUS_*.mp3 de Audio/Music :
//    Streaming (pas de musique entiere en memoire), Vorbis
//    qualite 70, stereo, frequence d'origine (48 kHz).
// 2. Cree / complete Resources/MusicLibrary.asset : un clip
//    par usage (volumes egalises a l'oreille : mesures EBU
//    R128, Servo Horizon -15,2 LUFS sert de reference) et la
//    musique de chaque scene.
// Idempotent : un clip deja relie, un volume ou une scene
// deja reglee dans l'Inspector ne sont jamais ecrases.
// S'execute aussi une seule fois tout seul apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MusicSetup
{
    private const string MusicFolder = "Assets/_Project/Audio/Music";
    private const string LibraryPath = "Assets/_Project/Resources/MusicLibrary.asset";
    private const string AutoRunMarker = "Library/BlockforgeMusicSetup.done";

    private static readonly (MusicTrack track, string file, float volume)[] Tracks =
    {
        (MusicTrack.Menu,        "MUS_MainMenu_ServoHorizon.mp3",      0.50f), // -15,2 LUFS
        (MusicTrack.Garage,      "MUS_Garage_WorkshopPulse.mp3",       0.43f), // -13,9 LUFS
        (MusicTrack.Matchmaking, "MUS_Matchmaking_LaunchSequence.mp3", 0.43f), // -13,9 LUFS
        (MusicTrack.MapTest,     "MUS_MapTest_TestingPadAlpha.mp3",    0.42f), // -13,7 LUFS
    };

    // Map_MARS : pas encore de musique de combat, Launch Sequence (lancee
    // au clic sur Play with bots) continue sur la carte
    private static readonly (string scene, MusicTrack track)[] Scenes =
    {
        ("Boot",     MusicTrack.Menu),
        ("MainMenu", MusicTrack.Menu),
        ("Garage",   MusicTrack.Garage),
        ("Map_Test", MusicTrack.MapTest),
        ("Map_MARS", MusicTrack.Matchmaking),
    };

    static MusicSetup()
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

    [MenuItem("Blockforge/Setup Music")]
    public static void SetupFromMenu()
    {
        Setup();
    }

    private static bool Setup()
    {
        bool ok = true;
        foreach (var t in Tracks)
            ok &= ConfigureImport($"{MusicFolder}/{t.file}");

        var library = AssetDatabase.LoadAssetAtPath<MusicLibrary>(LibraryPath);
        if (library == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            library = ScriptableObject.CreateInstance<MusicLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        foreach (var t in Tracks)
        {
            var entry = library.Find(t.track);
            if (entry == null)
            {
                entry = new MusicLibrary.TrackEntry { track = t.track, volume = t.volume };
                library.tracks.Add(entry);
            }
            if (entry.clip == null)
                entry.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicFolder}/{t.file}");
            if (entry.clip == null)
                ok = false;
        }

        foreach (var s in Scenes)
        {
            if (!library.TryGetSceneTrack(s.scene, out _))
                library.scenes.Add(new MusicLibrary.SceneEntry { sceneName = s.scene, track = s.track });
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssetIfDirty(library);

        if (ok)
            Debug.Log($"[MusicSetup] {Tracks.Length} musiques configurees, bibliotheque : {LibraryPath}");
        else
            Debug.LogWarning($"[MusicSetup] Musiques manquantes dans {MusicFolder} : bibliotheque incomplete.");
        return ok;
    }

    private static bool ConfigureImport(string path)
    {
        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
        {
            Debug.LogWarning($"[MusicSetup] Introuvable : {path}");
            return false;
        }

        var s = importer.defaultSampleSettings;
        bool changed = s.loadType != AudioClipLoadType.Streaming
                       || s.compressionFormat != AudioCompressionFormat.Vorbis
                       || !Mathf.Approximately(s.quality, 0.7f)
                       || s.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate
                       || importer.forceToMono
                       || !importer.loadInBackground;
        if (!changed)
            return true;

        s.loadType = AudioClipLoadType.Streaming;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.7f;
        s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = s;
        importer.forceToMono = false;
        importer.loadInBackground = true;
        importer.SaveAndReimport();
        return true;
    }
}
