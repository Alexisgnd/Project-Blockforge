using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// EFFETS SONORES 2D (persistant entre les scenes)
// - Cree tout seul au lancement du jeu (aucun objet a poser
//   dans les scenes), survit aux changements de scene, comme
//   MusicManager.
// - Lecture : SfxManager.Play(SfxId.X). Regles par son dans
//   SfxLibrary (volume, pas de superposition, ecart minimal,
//   variation de hauteur).
// - Reserve de voix 2D : une voix libre, sinon la plus
//   ancienne est reprise.
// - Branchements (aucun son n'est appele depuis la logique) :
//   - GarageBuildSfx : pose / retrait de bloc, raccorde au
//     GarageBuildController a chaque scene chargee
//   - MatchAlertSfx : alertes du match par equipes (TdmMatch)
//   - LocalRobotSfx : robot regarde par la camera de suivi
//     (le sien, ou le bot / coequipier en spectateur) : energie
//     a sec, attente et debut de regeneration
// - Sons en boucle (SfxLibrary.loop) : joues jusqu'a Stop.
// Sons 2D uniquement pour l'instant : les sons
// positionnes en 3D (tirs, impacts) viendront avec leur
// propre lecteur.
// =========================================================

public class SfxManager : MonoBehaviour
{
    public const string LibraryResourcePath = "SfxLibrary";
    private const int VoiceCount = 12;

    public static SfxManager Instance { get; private set; }

    // Volume general (futur menu Options) ; part de SfxLibrary.masterVolume,
    // applique aux sons lances ensuite
    public float MasterVolume
    {
        get => masterVolume;
        set => masterVolume = Mathf.Clamp01(value);
    }

    private SfxLibrary library;
    private float masterVolume = 1f;
    private readonly List<AudioSource> voices = new();
    private readonly List<SfxId> voiceIds = new();
    private readonly List<float> voiceStarts = new();
    private readonly Dictionary<SfxId, float> lastPlayed = new();
    private readonly HashSet<SfxId> warned = new();
    private GarageBuildSfx garage;
    private LocalRobotSfx robot;

    // ---------- Creation automatique ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var lib = Resources.Load<SfxLibrary>(LibraryResourcePath);
        if (lib == null)
        {
            Debug.LogWarning("[Sfx] Resources/SfxLibrary introuvable : lancer Blockforge > Setup SFX.");
            return;
        }

        var go = new GameObject("[SfxManager]");
        DontDestroyOnLoad(go);
        var manager = go.AddComponent<SfxManager>();
        manager.library = lib;
        manager.masterVolume = lib.masterVolume;
        Instance = manager;
    }

    // Raccourci pour le code du jeu (sans effet si les sons ne sont pas en place).
    // Rend la voix qui joue (pour l'arreter plus tard), null si rien n'est joue.
    public static AudioSource Play(SfxId id)
    {
        return Instance != null ? Instance.PlayInternal(id) : null;
    }

    // Arrete une voix rendue par Play, si elle joue encore ce son
    public static void Stop(AudioSource voice, SfxId id)
    {
        if (Instance != null)
            Instance.StopInternal(voice, id);
    }

    // ---------- Cycle de vie ----------

    private void Awake()
    {
        for (int i = 0; i < VoiceCount; i++)
            CreateVoice();
        garage = gameObject.AddComponent<GarageBuildSfx>();
        gameObject.AddComponent<MatchAlertSfx>();
        robot = gameObject.AddComponent<LocalRobotSfx>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    // Filet de securite pour la toute premiere scene
    private void Start()
    {
        HookScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookScene();
    }

    private void HookScene()
    {
        garage.Hook(FindAnyObjectByType<GarageBuildController>(FindObjectsInactive.Include));
        robot.FindCamera();
    }

    // ---------- Lecture ----------

    private AudioSource PlayInternal(SfxId id)
    {
        if (library == null || id == SfxId.None)
            return null;

        var entry = library.Find(id);
        if (entry == null || entry.clip == null)
        {
            if (warned.Add(id))
                Debug.LogWarning($"[Sfx] Pas de clip pour '{id}' dans SfxLibrary.");
            return null;
        }

        float now = Time.unscaledTime;
        if (entry.minInterval > 0f && lastPlayed.TryGetValue(id, out float last) && now - last < entry.minInterval)
            return null;

        if (entry.noOverlap)
        {
            for (int i = 0; i < voices.Count; i++)
            {
                if (voiceIds[i] == id && voices[i].isPlaying)
                    return null;
            }
        }

        int index = PickVoice();
        var source = voices[index];
        source.Stop();
        source.clip = entry.clip;
        source.loop = entry.loop;
        source.volume = entry.volume * masterVolume;
        source.pitch = 1f + (entry.pitchJitter > 0f ? Random.Range(-entry.pitchJitter, entry.pitchJitter) : 0f);
        source.Play();

        voiceIds[index] = id;
        voiceStarts[index] = now;
        lastPlayed[id] = now;
        return source;
    }

    private void StopInternal(AudioSource voice, SfxId id)
    {
        if (voice == null)
            return;
        int index = voices.IndexOf(voice);
        if (index >= 0 && voiceIds[index] == id && voice.isPlaying)
            voice.Stop();
    }

    // Une voix libre, sinon la plus ancienne (jamais une boucle en cours)
    private int PickVoice()
    {
        int oldest = -1;
        for (int i = 0; i < voices.Count; i++)
        {
            if (!voices[i].isPlaying)
                return i;
            if (voices[i].loop)
                continue;
            if (oldest < 0 || voiceStarts[i] < voiceStarts[oldest])
                oldest = i;
        }
        return Mathf.Max(0, oldest);
    }

    private void CreateVoice()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;   // 2D
        source.priority = 64;       // au-dessus des sons 3D a venir, sous la musique
        source.bypassReverbZones = true;
        voices.Add(source);
        voiceIds.Add(SfxId.None);
        voiceStarts.Add(-1f);
    }
}
