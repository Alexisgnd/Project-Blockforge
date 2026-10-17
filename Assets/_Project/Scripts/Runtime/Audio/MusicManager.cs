using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// MUSIQUE DE FOND (persistante entre les scenes)
// - Cree tout seul au lancement du jeu (aucun objet a poser
//   dans les scenes), survit aux changements de scene.
// - A chaque scene chargee, joue la musique prevue par
//   MusicLibrary (scene non listee : la musique continue).
// - Changement de musique = fondu enchaine : l'ancienne baisse
//   pendant que la nouvelle monte (courbe a puissance
//   constante, pas de creux de volume au milieu).
// - Quand une musique sortante ne s'entend plus : arret,
//   remise au debut, clip retire. Si on la redemande avant
//   qu'elle soit muette, elle remonte la ou elle en est.
// - Meme musique redemandee (ex. Boot -> MainMenu) : rien ne
//   change, elle continue sans redemarrer.
// - Fondus en temps reel (insensibles a timeScale 0 du menu
//   pause), pas de saut apres le gel d'un chargement de scene.
// - Boot et MainMenu n'ont pas d'AudioListener : le manager
//   en porte un de secours, actif seulement quand la scene
//   n'en a aucun.
// Lancement explicite : MusicManager.PlayTrack(MusicTrack.X).
// =========================================================

public class MusicManager : MonoBehaviour
{
    public const string LibraryResourcePath = "MusicLibrary";

    // Pas de fondu maximal par image : un chargement de scene qui gele
    // le jeu 1 s ne doit pas faire sauter le fondu d'un coup
    private const float MaxFadeDeltaTime = 1f / 30f;
    private const float ListenerCheckInterval = 0.5f;

    public static MusicManager Instance { get; private set; }

    // Musique demandee en dernier (None = silence)
    public MusicTrack CurrentTrack { get; private set; } = MusicTrack.None;

    // Volume general (futur menu Options) ; part de MusicLibrary.masterVolume,
    // modifie a l'execution sans toucher l'asset
    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            foreach (var v in voices)
            {
                if (!v.Free)
                    ApplyVolume(v);
            }
        }
    }

    // Une "voix" = une AudioSource et son fondu en cours
    private class Voice
    {
        public AudioSource source;
        public MusicTrack track = MusicTrack.None;
        public float trackVolume;
        public float gain;      // 0..1, position dans le fondu
        public float target;    // 1 = monte, 0 = descend puis s'arrete
        public bool Free => track == MusicTrack.None;
    }

    private readonly List<Voice> voices = new();
    private MusicLibrary library;
    private float masterVolume = 1f;
    private GameObject fallbackListener;
    private float listenerCheckTimer;

    // ---------- Creation automatique ----------

    // Remise a zero si le rechargement de domaine est desactive en Play mode
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

        var lib = Resources.Load<MusicLibrary>(LibraryResourcePath);
        if (lib == null)
        {
            Debug.LogWarning("[Music] Resources/MusicLibrary introuvable : lancer Blockforge > Setup Music.");
            return;
        }

        var go = new GameObject("[MusicManager]");
        DontDestroyOnLoad(go);
        var manager = go.AddComponent<MusicManager>();
        manager.library = lib;
        manager.masterVolume = lib.masterVolume;
        Instance = manager;
    }

    // Raccourci pour le code du jeu (sans effet si la musique n'est pas en place)
    public static void PlayTrack(MusicTrack track)
    {
        if (Instance != null)
            Instance.Play(track);
    }

    // ---------- Cycle de vie ----------

    private void Awake()
    {
        fallbackListener = new GameObject("FallbackAudioListener");
        fallbackListener.transform.SetParent(transform, false);
        fallbackListener.AddComponent<AudioListener>();
        fallbackListener.SetActive(false);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // Filet de securite pour la toute premiere scene (deja fait par
        // OnSceneLoaded en temps normal : redemander la meme musique ne fait rien)
        RefreshListener();
        PlaySceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        RefreshListener();
        PlaySceneMusic(scene.name);
    }

    private void PlaySceneMusic(string sceneName)
    {
        if (library != null && library.TryGetSceneTrack(sceneName, out var track))
            Play(track);
    }

    // ---------- Lecture ----------

    public void Play(MusicTrack track)
    {
        if (library == null || track == CurrentTrack)
            return;

        CurrentTrack = track;

        // Tout ce qui n'est pas la nouvelle musique part en fondu de sortie
        foreach (var v in voices)
        {
            if (v.track != track)
                v.target = 0f;
        }

        if (track == MusicTrack.None)
            return;

        var entry = library.Find(track);
        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"[Music] Pas de clip pour '{track}' dans MusicLibrary.");
            return;
        }

        // Encore audible (fondu de sortie en cours) : elle remonte sans redemarrer
        var voice = voices.Find(v => v.track == track);
        if (voice == null)
        {
            voice = voices.Find(v => v.Free) ?? CreateVoice();
            voice.track = track;
            voice.gain = 0f;
            voice.source.clip = entry.clip;
            voice.source.Play();            // toujours depuis le debut
        }

        voice.trackVolume = entry.volume;
        voice.target = 1f;
        ApplyVolume(voice);
    }

    public void Stop()
    {
        Play(MusicTrack.None);
    }

    private Voice CreateVoice()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;           // 2D
        source.priority = 0;                // jamais coupee par manque de voix
        source.ignoreListenerPause = true;  // continue dans les menus pause
        source.bypassReverbZones = true;
        source.volume = 0f;

        var voice = new Voice { source = source };
        voices.Add(voice);
        return voice;
    }

    private void Update()
    {
        if (library == null)
            return;

        float step = Mathf.Min(Time.unscaledDeltaTime, MaxFadeDeltaTime) / library.crossfadeDuration;

        foreach (var v in voices)
        {
            if (v.Free)
                continue;

            v.gain = Mathf.MoveTowards(v.gain, v.target, step);
            ApplyVolume(v);

            // Ne s'entend plus : arret, retour au debut, plus jouee
            if (v.target <= 0f && v.gain <= 0f)
                Release(v);
        }

        listenerCheckTimer -= Time.unscaledDeltaTime;
        if (listenerCheckTimer <= 0f)
            RefreshListener();
    }

    private void ApplyVolume(Voice v)
    {
        // Puissance constante : sin/cos au lieu d'une rampe lineaire
        float curve = Mathf.Sin(v.gain * Mathf.PI * 0.5f);
        v.source.volume = v.trackVolume * masterVolume * curve;
    }

    private static void Release(Voice v)
    {
        v.source.Stop();                    // Stop (pas Pause) : retour au debut
        v.source.clip = null;
        v.source.volume = 0f;
        v.track = MusicTrack.None;
        v.gain = 0f;
        v.target = 0f;
    }

    // ---------- AudioListener de secours ----------

    // Actif uniquement si la scene n'a aucun AudioListener actif (Boot,
    // MainMenu) ; coupe des qu'une camera de la scene en apporte un.
    private void RefreshListener()
    {
        listenerCheckTimer = ListenerCheckInterval;

        // Le notre est mis de cote le temps de chercher celui de la scene
        if (fallbackListener.activeSelf)
            fallbackListener.SetActive(false);

        var sceneListener = FindAnyObjectByType<AudioListener>();
        bool sceneHasListener = sceneListener != null && sceneListener.enabled;
        if (!sceneHasListener)
            fallbackListener.SetActive(true);
    }
}
