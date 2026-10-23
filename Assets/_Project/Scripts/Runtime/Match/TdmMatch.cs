using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Blockforge.Meta;
using UnityEngine;

// =========================================================
// MATCH PAR EQUIPES CONTRE LES BOTS (Team Deathmatch 5 v 5)
// Pose dans la scene de la carte (Map_MARS) par
// Blockforge > Setup Map MARS Match. Au lancement :
// 1. Participants : ceux de l'ecran Versus (MatchIntro.LastData :
//    le joueur + 4 bots allies, 5 bots ennemis), sinon une
//    composition par defaut (scene lancee directement).
// 2. Bases : l'equipe du joueur prend la base A ou B au hasard
//    (le HUD la met toujours a gauche). Chaque base a 6
//    marqueurs (Marker_Spawn_A_01..06 de la carte) : 5 sont
//    tires au sort et distribues au hasard ; chacun garde le sien
//    pour toute la partie et y reapparait.
// 3. Compte a rebours (mode.countdownSeconds, 15 s) : les dix
//    robots apparaissent (assemblage holographique), MatchGate
//    gele pilotage et tir, la camera reste libre, l'ecran
//    MatchCountdownUI decompte ; le NavMesh des bots se construit.
// 4. Live : controles rendus, HUD de combat (TdmHud : rosters,
//    score, plaques ; Tab = TdmScoreTab), chrono (10:00).
//    Degats attribues (RobotHealth.DamagedBy) : un kill revient au
//    dernier ennemi qui a touche le robot detruit (15 s max), une
//    assist a tout autre ennemi qui l'a touche dans les 10 s. Pas
//    de tir allie (RobotHealth.CanBeDamagedBy).
//    Robot detruit : 10 s avant de pouvoir reapparaitre (bouton
//    Respawn / Espace pour le joueur, SpectateOverlay), puis
//    reapparition automatique 5 s plus tard ; les bots
//    reapparaissent seuls. Toujours a son point d'apparition.
// 5. Fin : une equipe atteint mode.scoreToWin (15) ou le chrono
//    tombe a zero (en tete = gagnant, egalite = Draw) -> tout est
//    gele, puis MatchEndFlow (Victory / Defeat / Draw, puis le
//    scoreboard complet de ResultsScreen, retour au hub).
// L'etat (participants, scores, phases) est separe de
// l'affichage (TdmHud, MatchCountdownUI, SpectateOverlay,
// TdmScoreTab lisent ce composant) : un futur serveur de match
// remplacera la logique sans toucher a l'interface.
// =========================================================

public enum TdmPhase { Setup, Countdown, Live, Ended }

public class TdmMatch : MonoBehaviour
{
    public static TdmMatch Instance { get; private set; }

    [Header("References (remplies par le setup)")]
    [Tooltip("Catalogue des blocs (Data/Blocks) : les blueprints referencent les blocs par nom d'asset")]
    public BlockDefinition[] blocks;
    [Tooltip("Mode joue si la scene est lancee directement (sinon celui de l'ecran Play)")]
    public GameModeDefinition defaultMode;
    public RobotFollowCamera followCamera;
    public float cellSize = 1f;
    [Tooltip("Scene de test : aucun robot pour le joueur, les 10 robots sont des bots et le joueur regarde " +
             "(BotSpectatorView : camera sur un bot au choix ou camera libre).")]
    public bool spectatorOnly;
    [Tooltip("Le joueur recoit a chaque lancement un robot tire au sort parmi les presets des bots " +
             "(meme pool que les bots), au lieu de son robot du garage.")]
    public bool randomLocalRobot = true;

    [Header("Attribution")]
    public float killCreditSeconds = 15f;
    public float assistSeconds = 10f;

    // ---------- Etat ----------
    public TdmPhase Phase { get; private set; } = TdmPhase.Setup;
    public readonly List<TdmParticipant> Participants = new();
    public TdmParticipant Local { get; private set; }
    public int LocalSide { get; private set; }
    public int ScoreToWin { get; private set; } = 15;
    public float MatchSeconds { get; private set; } = 600f;
    public float RespawnSeconds { get; private set; } = 10f;
    public float AutoRespawnSeconds { get; private set; } = 5f;
    public float CountdownSeconds { get; private set; } = 15f;
    public float CountdownLeft => Phase == TdmPhase.Countdown ? Mathf.Max(0f, countdownEndsAt - Time.time) : 0f;
    public float TimeLeft { get; private set; }
    public float Elapsed => MatchSeconds - TimeLeft;
    public string ModeTitle { get; private set; } = "Team Deathmatch";
    public string MapName { get; private set; } = "Mars";
    public string AllyTeamName { get; private set; } = "Blue team";
    public string EnemyTeamName { get; private set; } = "Red team";
    public MatchResult Result { get; private set; }
    public RobotTestHUD LocalTestHud { get; private set; }
    public Vector3 Center { get; private set; }

    private readonly int[] sideScore = new int[2];
    private readonly Vector3[] baseCenter = new Vector3[2];
    private readonly Dictionary<string, BlockDefinition> defs = new();
    // Catalogue des blocs du match (vignettes des robots de la killfeed)
    public IReadOnlyDictionary<string, BlockDefinition> BlockDefs => defs;
    private float countdownEndsAt;
    private GameObject localHudHost;
    private System.Random rng;
    private BotRobotPool botPool = BotRobotPool.Mixed;
    public BotRobotPool BotPool => botPool;

    public event Action<TdmPhase> PhaseChanged;
    public event Action<TdmParticipant, TdmParticipant> Died;      // victime, tueur (null : sans tueur)
    public event Action<TdmParticipant> Spawned;
    public event Action ScoreChanged;

    public int SideScore(int side) => sideScore[Mathf.Clamp(side, 0, 1)];
    public int AllyScore => SideScore(LocalSide);
    public int EnemyScore => SideScore(1 - LocalSide);
    public int LeadOf(int side) => SideScore(side) - SideScore(1 - side);

    // Fin de match proche (lue par l'affichage et l'audio : chrono rouge, equipe
    // a un kill de la victoire, musique de derniere action)
    public const float LastMinuteSeconds = 60f;
    public bool LastMinute => Phase == TdmPhase.Live && TimeLeft <= LastMinuteSeconds;
    public bool OneKillFromWin(int side) => ScoreToWin > 1 && SideScore(side) == ScoreToWin - 1;
    public bool FinalStretch => LastMinute || OneKillFromWin(0) || OneKillFromWin(1);
    public bool IsAlly(TdmParticipant p) => p != null && p.Side == LocalSide;
    public Team TeamOf(TdmParticipant p) => p.Side == LocalSide ? Team.Ally : Team.Enemy;
    public Vector3 EnemyBase(int side) => baseCenter[1 - Mathf.Clamp(side, 0, 1)];
    public Vector3 OwnBase(int side) => baseCenter[Mathf.Clamp(side, 0, 1)];

    // Equipes dans l'ordre de l'ecran Versus (jamais retriees pendant le match : regle du roster)
    public List<TdmParticipant> Roster(bool allies)
    {
        var list = new List<TdmParticipant>();
        foreach (var p in Participants)
        {
            if ((p.Side == LocalSide) == allies)
                list.Add(p);
        }
        return list;
    }

    // =========================================================
    // DEMARRAGE
    // =========================================================

    private void Awake()
    {
        Instance = this;
        rng = new System.Random();
        MatchGate.Frozen = true;
    }

    private void OnDestroy()
    {
        // Un match plus recent (rechargement de la carte) a deja pris la main : on ne touche a rien
        if (Instance != this)
            return;
        Instance = null;
        MatchGate.Frozen = false;
        RobotHealth.CanBeDamagedBy = null;
        MatchNavMesh.Clear();
    }

    private void Start()
    {
        foreach (var def in blocks ?? Array.Empty<BlockDefinition>())
        {
            if (def != null)
                defs[def.name] = def;
        }
        if (followCamera == null)
            followCamera = FindAnyObjectByType<RobotFollowCamera>();
        if (followCamera == null && Camera.main != null)
            followCamera = Camera.main.gameObject.AddComponent<RobotFollowCamera>();

        var intro = MatchIntro.TakeLastData();   // consommee : un lancement direct plus tard ne la reprend pas
        var mode = intro != null && intro.mode != null ? intro.mode : defaultMode;
        ReadRules(mode, intro);
        botPool = BotRobotCatalog.EffectivePool(mode);
        BuildParticipants(intro);
        AssignSpawns();
        var lineup = new System.Text.StringBuilder();
        foreach (var bot in Participants)
        {
            if (bot.IsBot)
                lineup.Append(lineup.Length > 0 ? ", " : "").Append(bot.Name).Append(" : ").Append(bot.RobotName);
        }
        Debug.Log($"[Bot] Pool {BotRobotCatalog.PoolLabel(botPool)} — {lineup}");
        // Robot du joueur (debrief des parties : il n'apparaissait nulle part dans les logs)
        if (Local != null && !spectatorOnly)
        {
            int blocks = Local.Blueprint != null && Local.Blueprint.blocks != null ? Local.Blueprint.blocks.Count : 0;
            Debug.Log($"[Tdm] Robot du joueur ({Local.Name}) : {Local.RobotName}, {blocks} blocs, " +
                      (randomLocalRobot ? "preset tire au sort (randomLocalRobot)" : "robot du garage"));
        }

        RobotHealth.CanBeDamagedBy = CanDamage;

        // Interface : HUD de combat (cache jusqu'au depart), compte a rebours, spectateur, Tab
        var hud = TdmHud.Create(this);
        TdmMiniMap.Create(this);
        MatchCountdownUI.Create(this);
        if (spectatorOnly)
            BotSpectatorView.Create(this);
        else
            SpectateOverlay.Create(this);
        TdmScoreTab.Create(this);
        MatchEndFlow.Install(hud.gameObject, "MainMenu");

        foreach (var p in Participants)
            SpawnRobot(p);

        StartCoroutine(MatchNavMesh.Build(RobotTestSpawner.RobotLayer));

        countdownEndsAt = Time.time + CountdownSeconds;
        TimeLeft = MatchSeconds;
        SetPhase(TdmPhase.Countdown);
        Debug.Log($"[Tdm] {ModeTitle} sur {MapName} : {Participants.Count} robots, base {(LocalSide == 0 ? "A" : "B")} pour le joueur" +
                  (spectatorOnly ? " (spectateur)" : "") + ", " +
                  $"premier a {ScoreToWin}, {MatchSeconds:0} s, depart dans {CountdownSeconds:0} s.");
        if (CountdownSeconds <= 0f)
            GoLive();
    }

    private void ReadRules(GameModeDefinition mode, MatchIntroData intro)
    {
        if (mode != null)
        {
            ScoreToWin = Mathf.Max(1, mode.scoreToWin);
            MatchSeconds = Mathf.Max(30f, mode.matchSeconds);
            CountdownSeconds = Mathf.Max(0f, mode.countdownSeconds);
            RespawnSeconds = Mathf.Max(1f, mode.respawnSeconds);
            AutoRespawnSeconds = Mathf.Max(0f, mode.autoRespawnSeconds);
            ModeTitle = mode.title;
            MapName = mode.mapName;
        }
        if (intro != null)
        {
            ModeTitle = intro.modeTitle;
            MapName = intro.mapName;
            AllyTeamName = intro.allyTeamName;
            EnemyTeamName = intro.enemyTeamName;
        }
    }

    private void BuildParticipants(MatchIntroData intro)
    {
        if (intro == null || intro.allies.Count == 0)
        {
            // Scene lancee directement : meme composition que l'ecran Versus
            string name = SessionService.DisplayName;
            intro = MatchIntroData.Bots(defaultMode, string.IsNullOrEmpty(name) ? "Player" : name, null);
        }
        LocalSide = rng.Next(2);

        // Styles des bots : un melange par equipe, puis le hasard
        var styles = new List<BotStyle> { BotStyle.Cautious, BotStyle.Balanced, BotStyle.Aggressive, BotStyle.Opportunist, BotStyle.Balanced };
        int id = 0;
        void Add(MatchParticipant mp, int side, List<BotStyle> pool)
        {
            var p = new TdmParticipant
            {
                Id = id++,
                Name = mp.name,
                RobotName = mp.robot,
                Side = side,
                IsBot = mp.isBot,
                IsLocal = mp.isSelf,
            };
            if (p.IsLocal)
            {
                if (randomLocalRobot)
                {
                    // Comme un bot : un preset tire au sort a chaque lancement
                    var presets = BotRobotCatalog.NamesFor(botPool);
                    p.RobotName = presets[rng.Next(presets.Length)];
                    p.Blueprint = BotRobotCatalog.Build(p.RobotName);
                }
                else
                {
                    var bp = RobotSession.Blueprint;
                    bool empty = bp == null || bp.blocks == null || bp.blocks.Count == 0;
                    p.Blueprint = empty ? RobotTestSpawner.CreateDefaultTestBlueprint() : bp;
                    if (string.IsNullOrEmpty(p.RobotName))
                        p.RobotName = p.Blueprint.name;
                }
                Local = p;
            }
            else
            {
                if (!BotRobotCatalog.Contains(p.RobotName))
                {
                    var candidates = BotRobotCatalog.NamesFor(botPool);
                    p.RobotName = candidates[rng.Next(candidates.Length)];
                }
                string forced = ForcedPreset(Participants.Count);
                if (forced != null)
                    p.RobotName = forced;
                p.Blueprint = BotRobotCatalog.Build(p.RobotName);
                var style = pool.Count > 0 ? pool[0] : (BotStyle)rng.Next(4);
                if (pool.Count > 0)
                    pool.RemoveAt(0);
                p.Personality = BotPersonality.Create(style, rng);
            }
            Participants.Add(p);
        }

        var allyStyles = Shuffled(styles);
        var enemyStyles = Shuffled(styles);
        foreach (var mp in intro.allies)
            Add(mp, LocalSide, allyStyles);
        foreach (var mp in intro.enemies)
            Add(mp, 1 - LocalSide, enemyStyles);

        if (spectatorOnly && Local != null)
        {
            // Scene de test spectateur : le joueur de la composition devient un bot de plus
            var p = Local;
            p.IsLocal = false;
            p.IsBot = true;
            p.Name = "Bot Echo";
            var candidates = BotRobotCatalog.NamesFor(botPool);
            p.RobotName = ForcedPreset(Participants.Count - 1) ?? candidates[rng.Next(candidates.Length)];
            p.Blueprint = BotRobotCatalog.Build(p.RobotName);
            p.Personality = BotPersonality.Create(allyStyles.Count > 0 ? allyStyles[0] : (BotStyle)rng.Next(4), rng);
            Local = null;
        }
        else if (Local == null)
        {
            // Pas de joueur dans la composition : le premier allie devient le joueur
            var first = Participants.Find(x => x.Side == LocalSide);
            if (first != null)
            {
                first.IsLocal = true;
                first.IsBot = false;
                first.Personality = null;
                first.Blueprint = RobotTestSpawner.CreateDefaultTestBlueprint();
                Local = first;
            }
        }
    }

    // Editeur : Library/BotPreset.force (un nom de preset par ligne) impose ses robots aux
    // bots, dans l'ordre et en boucle (test d'un preset precis : "Bulwark"). Vide/absent : tirage.
    private static string[] forcedPresets;
    private static string ForcedPreset(int index)
    {
#if UNITY_EDITOR
        if (forcedPresets == null)
        {
            var list = new List<string>();
            const string path = "Library/BotPreset.force";
            if (System.IO.File.Exists(path))
                foreach (var raw in System.IO.File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length > 0 && !line.StartsWith("#") && BotRobotCatalog.Contains(line))
                        list.Add(line);
                }
            forcedPresets = list.ToArray();
            if (forcedPresets.Length > 0)
                Debug.Log($"[Bot] Presets imposes ({path}) : {string.Join(", ", forcedPresets)}");
        }
        return forcedPresets.Length > 0 ? forcedPresets[((index % forcedPresets.Length) + forcedPresets.Length) % forcedPresets.Length] : null;
#else
        return null;
#endif
    }

    private List<T> Shuffled<T>(List<T> source)
    {
        var list = new List<T>(source);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    // ---------- Points d'apparition ----------

    private static readonly Regex SpawnMarker = new(@"^Marker_Spawn_([AB])_(\d+)$", RegexOptions.CultureInvariant);

    private void AssignSpawns()
    {
        var markers = new[] { new List<Transform>(), new List<Transform>() };
        Transform centerMarker = null;
        var baseMarkers = new Transform[2];
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            string n = t.name;
            if (n == "Marker_Center") { centerMarker = t; continue; }
            if (n == "Marker_Spawn_A") { baseMarkers[0] = t; continue; }
            if (n == "Marker_Spawn_B") { baseMarkers[1] = t; continue; }
            var m = SpawnMarker.Match(n);
            if (m.Success)
                markers[m.Groups[1].Value == "A" ? 0 : 1].Add(t);
        }

        for (int side = 0; side < 2; side++)
        {
            Vector3 c = baseMarkers[side] != null ? baseMarkers[side].position : Vector3.zero;
            if (markers[side].Count > 0)
            {
                c = Vector3.zero;
                foreach (var t in markers[side])
                    c += t.position;
                c /= markers[side].Count;
            }
            else if (baseMarkers[side] == null)
            {
                c = new Vector3(0f, 0f, side == 0 ? -80f : 80f);
                Debug.LogWarning($"[Tdm] Aucun marqueur de spawn pour la base {(side == 0 ? "A" : "B")} : positions de secours.");
            }
            baseCenter[side] = c;
        }
        Center = centerMarker != null ? centerMarker.position : (baseCenter[0] + baseCenter[1]) * 0.5f;

        for (int side = 0; side < 2; side++)
        {
            var team = Participants.FindAll(p => p.Side == side);
            var pool = Shuffled(markers[side]);   // 6 marqueurs : 5 tires au sort
            for (int i = 0; i < team.Count; i++)
            {
                var p = team[i];
                Vector3 pos;
                if (i < pool.Count)
                {
                    p.Spawn = pool[i];
                    pos = pool[i].position;
                }
                else
                {
                    // Pas assez de marqueurs : en ligne autour du centre de la base
                    Vector3 across = Vector3.Cross(Vector3.up, (Center - baseCenter[side]).normalized);
                    pos = baseCenter[side] + across * ((i - team.Count * 0.5f) * 7f);
                }
                Vector3 facing = Center - pos;
                facing.y = 0f;
                if (facing.sqrMagnitude < 1f)
                    facing = side == 0 ? Vector3.forward : Vector3.back;
                p.SpawnPosition = pos;
                p.SpawnRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }
        }
    }

    // =========================================================
    // ROBOTS
    // =========================================================

    private void SpawnRobot(TdmParticipant p)
    {
        if (p.Robot != null)
            Destroy(p.Robot);
        if (p.IsLocal && localHudHost != null)
            Destroy(localHudHost);

        var robot = RobotAssembler.Build(p.Blueprint, defs, cellSize, null, out int placed, out int weightKg);
        if (placed == 0)
        {
            Destroy(robot);
            Debug.LogWarning($"[Tdm] Robot '{p.RobotName}' de {p.Name} vide (blocs inconnus) : robot par defaut.");
            p.Blueprint = RobotTestSpawner.CreateDefaultTestBlueprint();
            robot = RobotAssembler.Build(p.Blueprint, defs, cellSize, null, out placed, out weightKg);
        }
        robot.name = $"{p.Name} ({p.RobotName})";

        Vector3 position = FreeSpot(p) + Vector3.up * 0.4f;
        robot.transform.SetPositionAndRotation(position, p.SpawnRotation);
        SetLayer(robot.transform, RobotTestSpawner.RobotLayer);

        robot.AddComponent<Rigidbody>();
        var loco = robot.AddComponent<RobotLocomotion>();
        loco.Setup(cellSize, weightKg);
        robot.AddComponent<RobotPartAnimator>().Build(loco);
        var loadout = robot.AddComponent<RobotLoadoutState>();
        loadout.acceptInput = p.IsLocal;
        robot.AddComponent<RobotEnergy>();
        var health = robot.AddComponent<RobotHealth>();
        robot.AddComponent<RobotDamageFX>();
        var aim = robot.AddComponent<WeaponAimController>();
        aim.useCamera = p.IsLocal;
        var fire = robot.AddComponent<WeaponFireController>();
        fire.manual = !p.IsLocal;
        var dust = robot.AddComponent<RobotGroundFX>();
        robot.AddComponent<RobotRecovery>();
        robot.AddComponent<TdmCombatant>().Participant = p;

        p.Robot = robot;
        p.Health = health;
        p.Loco = loco;
        p.DiedAt = -1f;
        p.RecentAttackers.Clear();
        p.LastAttacker = null;
        health.DamagedBy += (attacker, amount) => OnDamaged(p, attacker, amount, health.LastHitWeapon);
        health.Destroyed += () => OnDestroyed(p);

        var controls = new List<Behaviour> { loco, aim, fire, dust };
        if (p.IsLocal)
        {
            var driver = robot.AddComponent<RobotTestDriver>();
            controls.Add(driver);
            int maxHealth = 0;
            foreach (var view in robot.GetComponentsInChildren<PlacedBlockView>())
                maxHealth += view.definition != null ? view.definition.resistanceHp : 0;
            localHudHost = new GameObject("MatchHUD_" + p.Name);
            LocalTestHud = localHudHost.AddComponent<RobotTestHUD>();
            LocalTestHud.Bind(loco, p.RobotName, maxHealth, weightKg, showMiniMap: false);
            localHudHost.SetActive(Phase == TdmPhase.Live);
            if (followCamera != null)
            {
                followCamera.SetTarget(robot.transform);
                MatchPauseMenu.ApplyToCamera();
            }
        }
        else
        {
            var brain = robot.AddComponent<BotBrain>();
            brain.Init(this, p);
            controls.Add(brain);
        }

        p.SpawnSequence = robot.AddComponent<RobotSpawnSequence>();
        p.SpawnSequence.Begin(controls.ToArray());
        Spawned?.Invoke(p);
    }

    // Point d'apparition du participant, decale si un robot vivant l'occupe
    private Vector3 FreeSpot(TdmParticipant p)
    {
        Vector3 basePos = Ground(p.SpawnPosition);
        Vector3 right = p.SpawnRotation * Vector3.right;
        for (int i = 0; i < 6; i++)
        {
            Vector3 candidate = i == 0 ? basePos : Ground(basePos + right * (i % 2 == 1 ? 1f : -1f) * 4.5f * ((i + 1) / 2));
            bool taken = false;
            foreach (var other in Participants)
            {
                if (other == p || !other.Alive)
                    continue;
                Vector3 d = other.Position - candidate;
                d.y = 0f;
                if (d.magnitude < 4f) { taken = true; break; }
            }
            if (!taken)
                return candidate;
        }
        return basePos;
    }

    private static Vector3 Ground(Vector3 at)
    {
        int mask = ~((1 << RobotTestSpawner.RobotLayer) | (1 << 2));
        return Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var hit, 120f, mask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : at;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayer(child, layer);
    }

    // =========================================================
    // DEGATS, KILLS, ASSISTS
    // =========================================================

    // Pas de tir allie : un robot de la meme equipe ne peut pas en blesser un autre.
    // Protection d'apparition : un robot en cours d'assemblage est intouchable (sinon
    // la fin de la sequence rendrait visible et dynamique un robot deja detruit).
    private bool CanDamage(GameObject attacker, RobotHealth victim)
    {
        var v = TdmCombatant.Of(victim);
        if (v != null && v.Spawning)
            return false;
        var a = TdmCombatant.Of(attacker);
        return a == null || v == null || a.Side != v.Side;
    }

    // weapon : bloc d'arme qui a reellement frappe (RobotHealth.LastHitWeapon) ;
    // null -> repli sur l'arme selectionnee par le tireur
    private void OnDamaged(TdmParticipant victim, GameObject attackerGo, float amount, BlockDefinition weapon)
    {
        var attacker = TdmCombatant.Of(attackerGo);
        if (attacker == null || attacker == victim || attacker.Side == victim.Side)
            return;
        if (Phase == TdmPhase.Live)
            attacker.Damage += amount;
        victim.RecentAttackers[attacker] = Time.time;
        victim.LastAttacker = attacker;
        victim.LastAttackerTime = Time.time;
        victim.LastAttackerWeaponDef = weapon != null ? weapon : SelectedWeapon(attacker);
        victim.LastAttackerWeapon = WeaponLabel(victim.LastAttackerWeaponDef);
        victim.LastAttackerDistance = Vector3.Distance(attacker.Position, victim.Position);
    }

    // Arme selectionnee par le tireur (repli quand l'impact ne dit pas quelle arme a frappe)
    private static BlockDefinition SelectedWeapon(TdmParticipant p)
    {
        if (p.Robot == null)
            return null;
        var loadout = p.Robot.GetComponent<RobotLoadoutState>();
        return loadout != null && loadout.Selected != null ? loadout.Selected.definition : null;
    }

    private static string WeaponLabel(BlockDefinition def)
    {
        if (def == null)
            return null;
        return !string.IsNullOrEmpty(def.family) ? def.family : def.blockName;
    }

    private void OnDestroyed(TdmParticipant victim)
    {
        if (Phase == TdmPhase.Ended)
            return;
        float now = Time.time;
        victim.Deaths++;
        victim.DiedAt = now;
        victim.RespawnReadyAt = now + RespawnSeconds;
        victim.AutoRespawnAt = victim.IsLocal
            ? victim.RespawnReadyAt + AutoRespawnSeconds
            : victim.RespawnReadyAt + (float)rng.NextDouble() * 1.5f;
        victim.Personality?.OnDeath();

        TdmParticipant killer = null;
        if (victim.LastAttacker != null && now - victim.LastAttackerTime <= killCreditSeconds)
            killer = victim.LastAttacker;
        victim.LastKiller = killer;
        victim.LastKillerWeapon = killer != null ? victim.LastAttackerWeapon : null;
        victim.LastKillerWeaponDef = killer != null ? victim.LastAttackerWeaponDef : null;
        victim.LastKillerDistance = killer != null ? victim.LastAttackerDistance : 0f;

        if (killer != null)
        {
            killer.Kills++;
            killer.Personality?.OnKill();
            sideScore[killer.Side]++;
        }
        foreach (var kv in victim.RecentAttackers)
        {
            if (kv.Key != killer && now - kv.Value <= assistSeconds)
                kv.Key.Assists++;
        }
        victim.RecentAttackers.Clear();
        victim.LastAttacker = null;

        if (victim.IsLocal && localHudHost != null)
            localHudHost.SetActive(false);

        Debug.Log($"[Tdm] {victim.Name} detruit" + (killer != null ? $" par {killer.Name} ({victim.LastKillerWeapon})" : "") +
                  $" — {AllyScore} : {EnemyScore}");
        // Killfeed (TdmKillfeed) : sans tireur dans le delai du kill, la mort revient a l'arene
        CombatEvents.RaiseKill(new KillEvent
        {
            killer = killer,
            victim = victim,
            cause = killer != null ? KillCause.Weapon : KillCause.Environment,
            weapon = victim.LastKillerWeaponDef,
        });
        Died?.Invoke(victim, killer);
        ScoreChanged?.Invoke();

        if (Phase == TdmPhase.Live && killer != null && sideScore[killer.Side] >= ScoreToWin)
            End();
    }

    // =========================================================
    // PHASES
    // =========================================================

    private void Update()
    {
        switch (Phase)
        {
            case TdmPhase.Countdown:
                if (Time.time >= countdownEndsAt)
                    GoLive();
                break;
            case TdmPhase.Live:
                TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
                UpdateRespawns();
                if (TimeLeft <= 0f)
                    End();
                break;
        }
    }

    private void GoLive()
    {
        MatchGate.Frozen = false;
        if (localHudHost != null && Local != null && Local.Alive)
            localHudHost.SetActive(true);
        SetPhase(TdmPhase.Live);
        Debug.Log("[Tdm] Depart !");
    }

    private void SetPhase(TdmPhase phase)
    {
        Phase = phase;
        PhaseChanged?.Invoke(phase);
    }

    private void UpdateRespawns()
    {
        float now = Time.time;
        foreach (var p in Participants)
        {
            if (p.Alive || p.DiedAt < 0f)
                continue;
            if (now >= p.AutoRespawnAt)
                Respawn(p);
        }
    }

    public bool CanRespawn(TdmParticipant p) => p != null && Phase == TdmPhase.Live && !p.Alive && p.DiedAt >= 0f && Time.time >= p.RespawnReadyAt;

    // Bouton Respawn / Espace du joueur (SpectateOverlay)
    public void RequestRespawn()
    {
        if (CanRespawn(Local))
            Respawn(Local);
    }

    private void Respawn(TdmParticipant p)
    {
        if (Phase != TdmPhase.Live)
            return;
        SpawnRobot(p);
        if (p.IsLocal && localHudHost != null)
            localHudHost.SetActive(true);
    }

    private void End()
    {
        if (Phase == TdmPhase.Ended)
            return;
        LogNavReport();
        MatchGate.Frozen = true;
        int ally = AllyScore, enemy = EnemyScore;
        var outcome = ally > enemy ? MatchOutcome.Victory : ally < enemy ? MatchOutcome.Defeat : MatchOutcome.Draw;
        Result = BuildResult(outcome);
        SetPhase(TdmPhase.Ended);
        Debug.Log($"[Tdm] Fin du match : {outcome} {ally} : {enemy} apres {Elapsed:0} s.");
        StartCoroutine(EndRoutine());
    }

    private IEnumerator EndRoutine()
    {
        // Le dernier robot detruit finit d'exploser avant le verdict
        yield return new WaitForSecondsRealtime(1.4f);
        if (MetaServices.MatchResults is MockMatchResultSource)
            MatchEndFlow.FireLocal(Result);
        else
            MatchEndFlow.Show(Result);   // source de resultats reelle : le deroule de fin s'ouvre directement
    }

    // =========================================================
    // RESULTATS
    // =========================================================

    // Resultat courant (Tab) ou final (fin de match)
    // Bilan de navigation des bots (BotNavStats) : console [Bot] et, dans l'editeur,
    // Library/BotNavReport.txt
    private void LogNavReport()
    {
        string report = BotNavStats.Report(Participants, Elapsed);
        Debug.Log(report);
#if UNITY_EDITOR
        try
        {
            System.IO.File.WriteAllText("Library/BotNavReport.txt",
                $"# {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {ModeTitle} sur {MapName}, {AllyScore} : {EnemyScore}\n{report}\n");
        }
        catch (Exception) { }
#endif
    }

    public MatchResult BuildResult(MatchOutcome outcome)
    {
        var players = new List<PlayerMatchStats>();
        foreach (var p in Participants)
        {
            players.Add(new PlayerMatchStats("p" + p.Id, p.Name, TeamOf(p), p.IsLocal, p.IsBot,
                                             p.Kills, p.Deaths, p.Assists, Mathf.RoundToInt(p.Damage), 0, p.Score));
        }
        MatchRewards rewards;
        if (MetaServices.MatchResults is MockMatchResultSource mock)
        {
            // Recompenses du profil (niveau, XP) : celles du mock, ajustees au match joue
            var r = mock.Build(outcome).Rewards;
            int kills = Local != null ? Local.Kills : 0;
            int credits = (outcome == MatchOutcome.Victory ? 400 : outcome == MatchOutcome.Draw ? 300 : 200) + 20 * kills;
            int tech = (outcome == MatchOutcome.Victory ? 60 : outcome == MatchOutcome.Draw ? 45 : 30) + 3 * kills;
            rewards = new MatchRewards(credits, tech, r.Xp, r.Level, r.XpInLevel, r.XpForNextLevel);
        }
        else
        {
            rewards = new MatchRewards(200, 30, 0, 1, 0, 1000);
        }
        return new MatchResult(ModeTitle, MapName, TimeSpan.FromSeconds(Mathf.Max(0f, Elapsed)), outcome,
                               AllyScore, EnemyScore, players, MatchLogic.ComputeAwards(players), rewards);
    }

    // ---------- Aides pour les bots ----------

    // Centre des allies vivants d'un participant (lui exclu)
    public Vector3 AllyCentroid(TdmParticipant self, out bool any)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var p in Participants)
        {
            if (p == self || p.Side != self.Side || !p.Alive)
                continue;
            sum += p.Position;
            n++;
        }
        any = n > 0;
        return n > 0 ? sum / n : OwnBase(self.Side);
    }
}
