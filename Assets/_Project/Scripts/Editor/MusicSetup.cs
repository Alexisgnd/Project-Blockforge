using System.IO;
using UnityEditor;
using UnityEngine;

// =========================================================
// SETUP DES MUSIQUES
// Menu : Blockforge > Setup Music
// 1. Reglages d'import des MUS_* de Audio/Music :
//    - musiques : Streaming (pas de musique entiere en
//      memoire), Vorbis qualite 70, stereo, frequence d'origine
//    - jingles (quelques secondes) : decompresses au chargement
//      (PCM), aucune latence au declenchement
// 2. Cree / complete Resources/MusicLibrary.asset : un clip
//    par usage (volumes egalises a l'oreille : mesures EBU
//    R128, Servo Horizon -15,2 LUFS sert de reference ; jingles
//    ~3 dB au-dessus de la musique), position de depart, et la
//    musique de chaque scene.
// Idempotent : un clip deja relie, un volume ou une scene
// deja reglee dans l'Inspector ne sont jamais ecrases.
// S'execute aussi une seule fois tout seul apres compilation
// (marqueur versionne dans Library/ : a changer quand la liste
// s'allonge).
// =========================================================

[InitializeOnLoad]
public static class MusicSetup
{
    private const string MusicFolder = "Assets/_Project/Audio/Music";
    private const string LibraryPath = "Assets/_Project/Resources/MusicLibrary.asset";
    private const string AutoRunMarker = "Library/BlockforgeMusicSetup.v2.done";

    private static readonly (MusicTrack track, string file, float volume, float startSeconds, bool stinger)[] Tracks =
    {
        (MusicTrack.Menu,            "MUS_MainMenu_ServoHorizon.mp3",      0.50f, 0f,  false), // -15,2 LUFS
        (MusicTrack.Garage,          "MUS_Garage_WorkshopPulse.mp3",       0.43f, 0f,  false), // -13,9 LUFS
        (MusicTrack.Matchmaking,     "MUS_Matchmaking_LaunchSequence.mp3", 0.43f, 0f,  false), // -13,9 LUFS
        (MusicTrack.MapTest,         "MUS_MapTest_TestingPadAlpha.mp3",    0.42f, 0f,  false), // -13,7 LUFS
        (MusicTrack.MarsCombat,      "MUS_Mars_CombatLoop.mp3",            0.44f, 0f,  false), // -14,0 LUFS
        (MusicTrack.MarsProtocol,    "MUS_Mars_Protocol.mp3",              0.48f, 85f, false), // -14,8 LUFS, depart a 1:25
        (MusicTrack.RobotSpawn,      "MUS_Match_RobotSpawn.wav",           0.56f, 0f,  true),  // -13,0 LUFS
        (MusicTrack.FinalTransition, "MUS_Mars_FinalTransition.wav",       0.36f, 0f,  true),  // -9,1 LUFS
    };

    // Carte Mars : Mars Combat Loop des l'arrivee ; la derniere action
    // (Mars Protocol) est pilotee par MatchMusic
    private static readonly (string scene, MusicTrack track)[] Scenes =
    {
        ("Boot",              MusicTrack.Menu),
        ("MainMenu",          MusicTrack.Menu),
        ("Garage",            MusicTrack.Garage),
        ("Map_Test",          MusicTrack.MapTest),
        ("Map_MARS",          MusicTrack.MarsCombat),
        ("Map_MARS_Spectate", MusicTrack.MarsCombat),
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
            ok &= t.stinger ? ConfigureStingerImport($"{MusicFolder}/{t.file}") : ConfigureImport($"{MusicFolder}/{t.file}");

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
                entry = new MusicLibrary.TrackEntry { track = t.track, volume = t.volume, startSeconds = t.startSeconds };
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

    // Jingle de quelques secondes : en memoire, decompresse, pret a partir
    private static bool ConfigureStingerImport(string path)
    {
        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
        {
            Debug.LogWarning($"[MusicSetup] Introuvable : {path}");
            return false;
        }

        var s = importer.defaultSampleSettings;
        bool changed = s.loadType != AudioClipLoadType.DecompressOnLoad
                       || s.compressionFormat != AudioCompressionFormat.PCM
                       || s.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate
                       || !s.preloadAudioData
                       || importer.forceToMono
                       || importer.loadInBackground;
        if (!changed)
            return true;

        s.loadType = AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.PCM;
        s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        s.preloadAudioData = true;
        importer.defaultSampleSettings = s;
        importer.forceToMono = false;
        importer.loadInBackground = false;
        importer.SaveAndReimport();
        return true;
    }
}
