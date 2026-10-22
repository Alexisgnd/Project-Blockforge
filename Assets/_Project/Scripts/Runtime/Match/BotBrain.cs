using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// =========================================================
// IA D'UN BOT DU MATCH PAR EQUIPES (TdmMatch)
// Pilote les memes composants qu'un joueur : RobotLocomotion
// (gaz, direction, montee, boost), WeaponAimController (point
// vise manuel), WeaponFireController (FireHeld) et
// RobotLoadoutState (choix de l'arme selon la distance).
// Toutes les 0,25 s (decalees d'un bot a l'autre), le bot :
// 1. choisit sa cible parmi les ennemis vivants : proche, en
//    vue, qui le canarde, et surtout abimee pour un
//    opportuniste (BotPersonality.Opportunism) ;
// 2. choisit un mode :
//    - Retreat : sa vie passe sous RetreatAt -> il decroche
//      vers ses allies / sa base le temps de se regenerer
//      (RobotHealth repare apres 6 s sans degats), en tirant
//      encore s'il voit quelqu'un ;
//    - Hunt : une cible tres abimee a portee de chasse -> il
//      fonce l'achever ;
//    - Engage : cible vue (ou vue il y a moins de 3 s) -> il
//      tient la distance de son arme x RangeFactor : avance si
//      loin, recule si trop pres, tourne autour sinon ;
//    - Advance : personne en vue -> vers le dernier ennemi
//      repere ; un prudent avance avec ses allies ;
// 3. choisit l'arme du slot qui convient a la distance (rail
//    de loin, plasma a mi-distance, laser partout, Tesla au
//    contact).
// Chaque image : visee (anticipation de la vitesse de la cible,
// cloche du plasma, erreur qui depend du niveau), rafales apres
// un temps de reaction, conduite vers le prochain coin du
// chemin NavMesh (MatchNavMesh ; ligne droite tant qu'il n'est
// pas pret), antennes d'evitement (rayons a +/- 28 degres),
// deblocage (marche arriere, redressement RobotRecovery, point
// voisin). Les robots a rotors volent (pas de NavMesh) : ils
// lisent le relief sur leur route (sol echantillonne jusqu'a
// 12-45 m devant selon la vitesse, dans l'axe et a +/- 25 degres)
// et tiennent FlyHeight au-dessus du point le plus haut ; une
// paroi devant (sphere lancee dans l'axe) les fait monter sur
// place avant d'avancer ; colles a une colline et trop penches,
// ils se degagent (pleine portance, recul) puis se redressent
// (RobotRecovery) s'ils restent couches.
// Passages etroits (robots au sol : ponts, corniches, couloirs) :
// a chaque decision, le bot mesure la largeur praticable 5-14 m
// devant lui sur sa route (vide de plus de 1,5 m ou rambarde /
// paroi de chaque cote) ; si elle est etroite pour sa largeur, il
// vise le milieu, ralentit et coupe les antennes laterales (les
// rambardes le renvoyaient d'un bord a l'autre).
// Cible : il garde sa cible tant qu'elle est en vue (pas de
// zapping), prefere les robots abimes et ceux que ses allies
// visent deja (feu concentre : TdmParticipant.BotTarget).
// Mode : il chasse des que la cible est sous 45 % ; mais face a
// deux ennemis ou plus en vue a moins de 80 m, sans allie a
// moins de 35 m et avec moins de 70 % de vie, il trace vers ses
// allies (Retreat).
// Arme : la plus grosse (degats par tir) qui porte a la distance
// de la cible et que l'energie permet ; quand l'energie manque,
// la plus petite qui tire encore.
// Tir : seulement quand au moins une tourelle alignee a une
// ligne de tir degagee depuis sa bouche ; sinon, apres 0,6 s, le
// bot tourne son corps vers la cible (au sol) ou descend vers
// elle (en vol, tangage limite a -20 degres) et se deplace si
// ca ne suffit pas.
// Pattes : le bot saute (RobotLocomotion.RequestJump) devant un
// obstacle ou une marche (sol 0,6 m plus haut a 4 m), des qu'il
// patine (0,8 s immobile en voulant avancer), pour remonter une
// crevasse, et en combat pour esquiver quand il vient d'etre
// touche (d'autant plus qu'il est agressif).
// Repartition : chaque bot approche sa cible par son propre angle
// (secteur de 72 degres selon son numero, comme une meute), au lieu
// que tous visent le meme point ; les volants gardent entre eux la
// somme de leurs demi-envergures + 6 m.
// Separation : au sol, un robot a moins de (demi-largeur + 4 m)
// devant fait braquer de l'autre cote et ralentir ; en vol, les
// robots se repartissent sur des altitudes differentes (+0 / +4 /
// +8 m selon le bot) et s'ecartent d'un volant a moins de 12 m.
// Contact avec un autre robot (allie ou ennemi) : le bot ne reste
// jamais colle a lui. En chasse il s'arrete a sa distance de tir
// (il ne fonce plus dans la cible), un robot a moins de 5 m est
// un obstacle pour ses antennes (sauf lames Tesla, qui veulent le
// contact), et s'il pousse un robot sans avancer pendant 1,2 s
// (OnCollisionStay), il recule en degageant son arriere du cote
// oppose ; un volant monte en reculant.
// Blocage au sol : 1) marche arriere en braquant, 2) marche avant
// a fond braquee de l'autre cote (robot pose sur une arete), 3)
// redressement / soulevement RobotRecovery, puis un autre point du
// NavMesh.
// Crevasses (robots au sol) : chaque seconde le bot compare le
// sol sous lui au sol a 25 m dans huit directions ; s'il est
// au fond (5 directions sur 8 plus hautes de 7 m), il ne se bat
// plus d'en bas : il cherche une sortie (point du NavMesh 10 m
// plus haut, a 35-130 m, chemin COMPLET le plus court), y roule
// a plein gaz en continuant de tirer, et en change si elle le
// bloque ; sans NavMesh, il prend la pente la plus douce qui
// remonte. Il reprend son comportement normal une fois en haut.
// Repartition sur la carte (BotLanes) : chaque bot a un role et
// son couloir (gauche, centre, droite, flanc profond, tireur en
// hauteur pour les rails, maraudeur pour les volants), equilibre
// dans l'equipe, re-tire a chaque reapparition, toutes les 1 a 2
// min et apres un changement de style. Hors combat (Advance), il
// patrouille son couloir au lieu de foncer vers l'ennemi repere
// (il ne poursuit un ennemi perdu de vue que s'il etait a moins
// de ChaseLeash). Cible : un ennemi loin de lui et de son couloir
// compte moins ; l'attrait d'une cible abimee baisse avec la
// distance ; le feu concentre ne compte que les allies a moins
// de FocusRadius ; au-dela de MaxOnTarget allies sur une cible,
// elle est laissee aux autres.
// Regroupements motives :
// - renfort : en Retreat (vie basse, 1 contre 2, ou nettement en
//   inferiorite numerique), et hors combat sous WeakJoinLife, il
//   rejoint l'allie le plus proche (et non plus le centre de toute
//   l'equipe, qui etait l'amas lui-meme) ;
// - soutien : hors combat, un allie engage a portee (SupportRange,
//   plus loin s'il est en difficulte) recoit 1 allie, 2 s'il est
//   en difficulte (vie basse ou plus d'ennemis que d'amis autour),
//   allies deja sur place compris ; le bot prend la cible de
//   l'allie en tenaille (50 degres a cote de lui).
// Anti-amas : plus de ClumpMaxRobots robots dans ClumpRadius, ou
// deja MaxAlliesPerPoint allies sur place : un bot qui n'est pas
// au contact repart quelques secondes vers un point de son
// couloir loin de l'amas (et y boude les cibles).
// Rien ne bouge tant que MatchGate.Frozen (compte a rebours,
// fin de match). Journal : [Bot] dans la console.
// =========================================================

[RequireComponent(typeof(RobotLocomotion), typeof(WeaponAimController), typeof(WeaponFireController))]
public class BotBrain : MonoBehaviour
{
    private enum Mode { Advance, Engage, Hunt, Retreat }

    private TdmMatch match;
    private TdmParticipant self;
    private BotPersonality persona;
    private RobotLocomotion loco;
    private WeaponAimController aim;
    private WeaponFireController fire;
    private RobotLoadoutState loadout;
    private RobotHealth health;
    private RobotRecovery recovery;
    private bool flier;

    private Mode mode = Mode.Advance;
    private TdmParticipant target;
    private bool targetVisible;
    private float visibleSince = -1f;
    private Vector3 lastSeenPos;
    private float lastSeenTime = -100f;
    private TdmParticipant lastHitBy;
    private float lastHitTime = -100f;
    private float nextThink;
    private float retreatUntil;

    // Deplacement
    private Vector3 destination;
    private bool hasDestination;
    private readonly List<Vector3> corners = new();
    private int corner;
    private NavMeshPath navPath;
    private float nextRepath;
    private Vector3 pathGoal;
    private bool backingOff;
    private float circleSign = 1f, nextCircleFlip;
    private Vector3 laneOffset;

    // Deblocage
    private float stuckTime, unstickUntil, unstickSteer;
    private int unstickCount;
    private float unstickWindowStart;
    private Vector3 lastLiftAt;
    private float lastLiftTime = -999f;
    private float escapeUntil;   // sortie de blocage : la destination de secours est gardee
    private float tiltedSince = -1f;   // volant : couche contre le relief depuis
    private float unstickThrottle = -0.8f;
    private bool unstickBoost;
    private float contactSince = -1f, lastContactTime = -100f;   // colle a un autre robot
    private Vector3 contactDir;

    // Passage etroit devant (pont, corniche) : point du milieu a viser
    private bool narrow;
    private Vector3 laneAim;
    private float halfWidth = -1f;

    // Crevasse : au fond, on cherche a remonter
    private bool inPit;
    private float nextPitCheck, nextExitSearch, lastPitLog = -100f;
    private bool hasPitExit;
    private Vector3 pitExit;
    private readonly List<Vector3> badExits = new();
    private int pitFailures;   // sorties de crevasse ratees d'affilee (2 -> repose hors du trou)
    private float pitBestGround, pitProgressAt;   // hauteur atteinte dans le trou, et quand
    private const float PitNoProgressTime = 15f;  // s sans remonter de 2 m : sortie ratee

    // Tir
    private Vector3 aimOffset;
    private float nextAimJitter;
    private float burstTimer;
    private bool bursting;
    private float weaponRange = 120f, weaponSpeed = 150f, weaponGravity;
    private WeaponFireController.Kind weaponKind = WeaponFireController.Kind.Laser;

    private static int terrainMask = -1;
    private static int TerrainMask => terrainMask != -1 ? terrainMask : (terrainMask = ~((1 << RobotTestSpawner.RobotLayer) | (1 << 2)));
    private static readonly RaycastHit[] whiskerHits = new RaycastHit[8];

    public string ModeName => mode.ToString();
    public TdmParticipant Target => target;

    // ---------- Repartition (couloirs, renfort, soutien, anti-amas) : reglages ----------
    private const float ThinkPeriod = 0.25f;
    private const float RoleShuffleMin = 60f, RoleShuffleMax = 120f;   // re-tirage du role (s)
    private const float LaneReach = 25f;            // point de couloir atteint a moins de (m)
    private const float LanePointTimeout = 75f;     // point jamais atteint : on passe au suivant (s)
    private const float ChaseLeash = 150f;          // Advance : poursuite d'un ennemi perdu de vue s'il etait a moins de (m)
    private const float FocusRadius = 75f;          // feu concentre : seuls les allies a moins de (m) de lui comptent
    private const int MaxOnTarget = 3;              // allies au plus sur une meme cible (au-dela : penalite)
    private const float LaneTargetSlack = 90f;      // ennemi a plus de (m) de lui et de son couloir : penalite
    private const float DamagedTargetRange = 250f;  // l'attrait d'une cible abimee s'eteint vers 1,4 x cette distance
    private const float SupportRange = 160f;        // soutien d'un allie engage (m)
    private const float SupportRangeTrouble = 230f; // soutien d'un allie en difficulte (m)
    private const int SupportersMax = 1, SupportersMaxTrouble = 2;   // allies venus l'aider (sur place compris)
    private const float SupportMaxSeconds = 25f, SupportCooldown = 8f;
    private const float SupportMinLife = 0.5f;      // sous cette vie, il ne part pas en soutien
    private const float AllyTroubleLife = 0.55f;    // allie en difficulte sous cette vie
    private const float WeakJoinLife = 0.5f;        // hors combat, sous cette vie : il rejoint l'allie le plus proche
    private const float ReinforceRange = 300f;      // allie le plus proche cherche jusqu'a (m)
    private const float CautiousBuddyRange = 120f;  // le prudent se cale sur un allie a moins de (m)
    private const int OutnumberedBy = 4;            // ennemis en vue - amis proches : il va chercher du renfort (3 : trop de replis en pleine sante)
    private const float ClumpRadius = 40f;
    private const int ClumpMaxRobots = 4;           // plus de 4 robots (lui compris, allies + ennemis) : amas
    private const int MaxAlliesPerPoint = 3;        // allies au plus au meme endroit hors contact
    private const float DeclumpMin = 6f, DeclumpMax = 10f;   // duree du retour au couloir (s)
    private const float ContactRange = 55f;         // au contact : cible en vue a moins de max(ceci, distance voulue + 10 m)

    private BotRole role = BotRole.Center;
    private bool roleReady, styleShuffle;
    private float nextRoleShuffle;
    private int laneIndex, laneStep = 1;
    private float lanePointSince, laneArrivedAt = -1f, dwellUntil;
    private TdmParticipant supportAlly, supportFoe, reinforceAlly;
    private float supportUntil, supportCooldownUntil, nextSupportScan, reinforceStamp = -100f;
    private float declumpUntil = -100f, nextClumpCheck, lastClumpLog = -100f, lastGroupLog = -100f;
    private Vector3 clumpCenter;

    public BotRole Role => role;
    public TdmParticipant SupportAlly => supportAlly;
    public bool Retreating => mode == Mode.Retreat;
    // Se bat (cible en vue, en Engage ou Hunt)
    public bool IsFighting => (mode == Mode.Engage || mode == Mode.Hunt) && target != null && targetVisible;
    // Au contact : cible en vue et proche
    public bool InContact => target != null && targetVisible
                             && Vector3.Distance(transform.position, target.Position) < Mathf.Max(ContactRange, PreferredDistance() + 10f);

    public void Init(TdmMatch owner, TdmParticipant participant)
    {
        match = owner;
        self = participant;
        persona = participant.Personality ?? BotPersonality.Create(BotStyle.Balanced, new System.Random());
        persona.StyleChanged -= OnStyleChanged;
        persona.StyleChanged += OnStyleChanged;
        flier = BotRobotCatalog.IsFlier(participant.RobotName);
        nav = participant.Nav ??= new BotNavStats();
        BotLanes.Register(owner, participant, this);
    }

    private BotNavStats nav;

    private void Awake()
    {
        loco = GetComponent<RobotLocomotion>();
        aim = GetComponent<WeaponAimController>();
        fire = GetComponent<WeaponFireController>();
        loadout = GetComponent<RobotLoadoutState>();
        health = GetComponent<RobotHealth>();
        recovery = GetComponent<RobotRecovery>();
        energy = GetComponent<RobotEnergy>();
        navPath = new NavMeshPath();
        nextThink = Time.time + Random.Range(0f, 0.25f);
        var r = Random.insideUnitCircle * 12f;
        laneOffset = new Vector3(r.x, 0f, r.y);
        if (health != null)
            health.DamagedBy += OnDamaged;
    }

    private void OnDestroy()
    {
        if (persona != null)
            persona.StyleChanged -= OnStyleChanged;
        if (health != null)
            health.DamagedBy -= OnDamaged;
    }

    private void OnDisable()
    {
        if (loco != null)
            Stop();
    }

    private void OnDamaged(GameObject attacker, float amount)
    {
        var p = TdmCombatant.Of(attacker);
        if (p == null || self == null || p.Side == self.Side)
            return;
        lastHitBy = p;
        lastHitTime = Time.time;
    }

    private void OnStyleChanged(BotStyle previous, BotStyle next)
    {
        Debug.Log($"[Bot] {self.Name} ({self.RobotName}) change de style : {previous} -> {next}");
        // Nouveau style : son role est re-tire sous peu (un prudent quitte le flanc, etc.)
        if (roleReady)
        {
            styleShuffle = true;
            nextRoleShuffle = Mathf.Min(nextRoleShuffle, Time.time + Random.Range(2f, 6f));
        }
    }

    // ---------- Saut (pattes) ----------

    private float nextJumpWish;
    private RobotEnergy energy;
    private float aimBlockedSince = -1f;   // cible en vue mais aucune tourelle ne peut tirer depuis

    private void Jump(string why)
    {
        if (loco == null || !loco.CanJump || Time.time < nextJumpWish || Time.time - loco.LastJumpTime < 2f)
            return;
        nextJumpWish = Time.time + 1f;
        loco.RequestJump();
        nav.jumps++;
    }

    // Separation au sol : le robot le plus proche devant, a moins de (demi-largeur + 4 m)
    private bool Crowded(out float side, out float distance)
    {
        side = 0f;
        distance = float.MaxValue;
        Vector3 me = transform.position;
        Vector3 fwd = Flat(transform.forward);
        float reach = HalfWidth + 4f;
        foreach (var p in match.Participants)
        {
            if (p == self || !p.Alive || p.Robot == null)
                continue;
            Vector3 d = p.Position - me;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > reach || dist >= distance)
                continue;
            if (Vector3.Dot(d, fwd) < -0.2f * dist)
                continue;   // derriere
            distance = dist;
            side = Vector3.SignedAngle(fwd, d, Vector3.up);
        }
        return distance < float.MaxValue;
    }

    // =========================================================
    // BOUCLE
    // =========================================================

    // Contact avec un autre robot (le Rigidbody est sur la racine, comme ce script)
    private bool whiskerLeft, whiskerRight, whiskerAhead;   // diagnostic
    private string hullContact;        // diagnostic : dernier bloc de coque en contact avec le decor
    private float hullContactTime = -10f, hullContactDepth;

    private void OnCollisionStay(Collision collision)
    {
        var other = TdmCombatant.Of(collision.collider);
        if (other == null)
        {
            // Decor contre la coque (les appuis sont des triggers : seul un bloc porteur touche ici)
            if (collision.contactCount > 0 && collision.collider.attachedRigidbody == null)
            {
                var c = collision.GetContact(0);
                hullContact = c.thisCollider != null ? (c.thisCollider.transform.parent != null ? c.thisCollider.transform.parent.name : c.thisCollider.name) : "?";
                hullContactDepth = c.separation;
                hullContactTime = Time.time;
            }
            return;
        }
        if (other == self || other.Robot == null)
            return;
        if (contactSince < 0f)
            contactSince = Time.time;
        lastContactTime = Time.time;
        contactDir = Flat(other.Robot.transform.position - transform.position);
    }

    private void Update()
    {
        if (match == null || self == null || loco == null)
            return;
        if (Time.time - lastContactTime > 0.3f)
            contactSince = -1f;
        if (MatchGate.Frozen || match.Phase != TdmPhase.Live || (health != null && health.IsDead))
        {
            Stop();
            aim.ManualAimPoint = transform.position + transform.forward * 30f + Vector3.up;
            return;
        }

        persona.Tick(Time.time, Time.deltaTime, match.LeadOf(self.Side));

        if (Time.time >= nextThink)
        {
            nextThink = Time.time + 0.25f;
            Think();
        }

        Aim();
        Shoot();
        Drive();
    }

    // =========================================================
    // DECISIONS
    // =========================================================

    private void Think()
    {
        TrackGroup();
        UpdateRole();
        PickTarget();
        PickMode();
        PickWeapon();
        UpdateSupport();
        UpdateClump();
        if (!flier)
            UpdatePit();
        if (!flier)
            UpdateLane();
        if (inPit && hasPitExit)
        {
            // Au fond d'une crevasse : remonter passe avant tout (le tir continue)
            backingOff = false;
            SetDestination(pitExit);
            return;
        }
        if (Time.time >= escapeUntil)
            PickDestination();
    }

    // =========================================================
    // CREVASSES
    // =========================================================

    private void UpdatePit()
    {
        if (Time.time < nextPitCheck)
            return;
        nextPitCheck = Time.time + 1f;
        Vector3 me = transform.position;
        float ground = GroundHeight(me);
        int higher = 0;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            if (GroundHeight(me + dir * 25f) > ground + 7f)
                higher++;
        }
        bool wasInPit = inPit;
        inPit = inPit ? higher >= 3 : higher >= 5;   // hysteresis : on sort vraiment du trou
        if (inPit && !wasInPit) nav.pits++;
        if (inPit) nav.inPit += 1f;   // verifie chaque seconde
        if (!inPit)
        {
            if (wasInPit)
                Debug.Log($"[Bot] {self.Name} est sorti de la crevasse.");
            hasPitExit = false;
            badExits.Clear();
            pitFailures = 0;
            return;
        }
        // Avance reelle : un bot qui roule sans remonter (il tourne au fond, pousse
        // contre un rebord) ne declenche pas le deblocage ; 15 s sans gagner 2 m
        // comptent comme une sortie ratee
        if (!wasInPit || ground > pitBestGround + 2f)
        {
            pitBestGround = ground;
            pitProgressAt = Time.time;
        }
        else if (Time.time - pitProgressAt > PitNoProgressTime)
        {
            pitProgressAt = Time.time;
            if (PitExitFailed())
                return;
        }
        if (!hasPitExit || Time.time >= nextExitSearch)
            FindPitExit(me, ground);
    }

    private void FindPitExit(Vector3 me, float ground)
    {
        nextExitSearch = Time.time + 6f;
        var candidates = new List<Vector3>();
        foreach (float r in new[] { 35f, 60f, 90f, 130f })
        {
            for (int i = 0; i < 16; i++)
            {
                Vector3 p = me + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * r;
                float g = GroundHeight(p);
                if (g < ground + 10f)
                    continue;
                p.y = g;
                bool bad = false;
                foreach (var b in badExits)
                {
                    if ((b - p).sqrMagnitude < 400f) { bad = true; break; }
                }
                if (!bad)
                    candidates.Add(p);
            }
        }

        bool found = false;
        float best = float.MaxValue;
        Vector3 bestPoint = me;
        if (MatchNavMesh.Ready)
        {
            int complete = 0;
            foreach (var c in candidates)   // du plus proche au plus loin (rayons croissants)
            {
                if (!MatchNavMesh.TryCompletePath(me, c, navPath, out float len, Wide))
                    continue;
                if (len < best)
                {
                    best = len;
                    bestPoint = navPath.corners[navPath.corners.Length - 1];
                    found = true;
                }
                if (++complete >= 4)
                    break;
            }
        }
        if (!found)
        {
            // Sans chemin : la direction qui remonte avec la pente la plus douce
            float bestSlope = float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                float worst = 0f, prev = ground;
                bool climbs = false;
                for (int k = 1; k <= 6; k++)
                {
                    float g = GroundHeight(me + dir * (k * 10f));
                    worst = Mathf.Max(worst, (g - prev) / 10f);
                    prev = g;
                    if (g > ground + 10f) { climbs = true; break; }
                }
                bool known = false;
                foreach (var b in badExits)
                {
                    if ((Flat(b - (me + dir * 60f))).sqrMagnitude < 400f) { known = true; break; }
                }
                if (climbs && !known && worst < bestSlope)
                {
                    bestSlope = worst;
                    bestPoint = me + dir * 60f;
                    found = true;
                }
            }
        }
        hasPitExit = found;
        if (found)
        {
            pitExit = bestPoint;
            destination = pitExit;
            hasDestination = true;
            Repath();
            if (Time.time - lastPitLog > 10f)
            {
                lastPitLog = Time.time;
                Debug.Log($"[Bot] {self.Name} au fond d'une crevasse : sortie a {Vector3.Distance(me, pitExit):0} m" +
                          (best < float.MaxValue ? $" (chemin {best:0} m)" : " (pente la plus douce, sans NavMesh)") + ".");
            }
        }
    }

    private void PickTarget()
    {
        Vector3 me = transform.position;
        TdmParticipant best = null;
        float bestScore = float.MinValue;
        bool bestVisible = false;

        foreach (var p in match.Participants)
        {
            if (p.Side == self.Side || !p.Targetable)
                continue;
            float dist = Vector3.Distance(me, p.Position);
            if (dist > 420f)
                continue;
            float score = -dist / 45f;
            // Une cible abimee attire, surtout un opportuniste, mais de moins en moins avec la
            // distance (avant : toute l'equipe traversait la carte pour la meme cible)
            score += (1f + persona.Opportunism) * (1f - p.HealthFraction) * 2.2f * Mathf.Clamp01(1.4f - dist / DamagedTargetRange);
            if (p == lastHitBy && Time.time - lastHitTime < 4f)
                score += 1.6f;
            // Feu concentre : la cible d'un allie vaut plus, mais seulement d'un allie proche de
            // lui (FocusRadius) ; deja MaxOnTarget allies dessus : on la leur laisse
            int focus = 0, onIt = 0;
            foreach (var ally in match.Participants)
            {
                if (ally == self || ally.Side != self.Side || !ally.Alive || ally.BotTarget != p)
                    continue;
                if (Vector3.Distance(ally.Position, p.Position) < 150f) onIt++;
                if (Vector3.Distance(ally.Position, me) < FocusRadius) focus++;
            }
            score += Mathf.Min(2, focus) * 1.2f;
            if (onIt >= MaxOnTarget && p != target)
                score -= 3f;
            // Son couloir : un ennemi loin de lui ET loin de son couloir compte moins
            if (roleReady)
            {
                float off = Mathf.Min(dist, BotLanes.DistanceToLane(match, self.Side, role, p.Position));
                score -= Mathf.Max(0f, off - LaneTargetSlack) / 70f;
            }
            // Soutien : la cible de l'allie qu'il vient aider
            if (supportFoe != null && p == supportFoe)
                score += 1.5f;
            // Il vient de quitter un amas : il n'y retourne pas pour une cible de l'amas
            if (Time.time < declumpUntil + 6f && p != lastHitBy && FlatDist(p.Position, clumpCenter) < ClumpRadius * 1.5f)
                score -= 2f;
            if (p == target)
                score += targetVisible ? 2.5f : 0.8f;   // pas de zapping tant qu'on la voit
            // Cinq ennemis au plus : chacun a portee a son test de ligne de vue
            bool visible = dist < 300f && CanSee(p);
            if (visible)
                score += 2f;
            if (score > bestScore)
            {
                bestScore = score;
                best = p;
                bestVisible = visible;
            }
        }

        if (best != target)
        {
            visibleSince = bestVisible ? Time.time : -1f;
            // Nouvelle cible : rien de ce qu'on savait de l'ancienne ne vaut pour elle
            lastSeenTime = -100f;
            lastSeenPos = best != null ? best.Position : transform.position;
        }
        else if (bestVisible && !targetVisible)
            visibleSince = Time.time;
        target = best;
        targetVisible = bestVisible;
        self.BotTarget = target;
        if (target != null && targetVisible)
        {
            lastSeenPos = target.Position;
            lastSeenTime = Time.time;
        }
    }

    private void PickMode()
    {
        float life = self.HealthFraction;
        float retreatAt = persona.RetreatAt;

        if (mode == Mode.Retreat)
        {
            // Reste en retrait jusqu'a s'etre regenere ou jusqu'au delai
            if (life < retreatAt + 0.25f && Time.time < retreatUntil)
                return;
        }
        else if (life < retreatAt)
        {
            bool finishIt = target != null && targetVisible && target.HealthFraction < 0.2f && persona.Style != BotStyle.Cautious;
            if (!finishIt)
            {
                mode = Mode.Retreat;
                retreatUntil = Time.time + Random.Range(5f, 9f);
                return;
            }
        }

        if (target == null)
        {
            mode = Mode.Advance;
            return;
        }
        float dist = Vector3.Distance(transform.position, target.Position);

        // Deux ennemis ou plus en vue de pres, aucun allie a portee, vie entamee : on trace
        if (life < 0.7f && mode != Mode.Retreat)
        {
            int enemiesNear = 0, alliesNear = 0;
            foreach (var p in match.Participants)
            {
                if (p == self || !p.Alive) continue;
                float d = Vector3.Distance(transform.position, p.Position);
                if (p.Side != self.Side && d < 80f && CanSee(p)) enemiesNear++;
                else if (p.Side == self.Side && d < 35f) alliesNear++;
            }
            if (enemiesNear >= 2 && alliesNear == 0)
            {
                mode = Mode.Retreat;
                retreatUntil = Time.time + Random.Range(6f, 10f);
                return;
            }
        }

        // Nettement en inferiorite numerique (OutnumberedBy ennemis en vue de plus que d'amis
        // a moins de 50 m), meme en bonne sante : renfort aupres de l'allie le plus proche
        if (life >= 0.7f)
        {
            int enemiesSeen = 0, friends = 0;
            foreach (var p in match.Participants)
            {
                if (p == self || !p.Alive) continue;
                float d = Vector3.Distance(transform.position, p.Position);
                if (p.Side != self.Side && d < 80f && CanSee(p)) enemiesSeen++;
                else if (p.Side == self.Side && d < 50f) friends++;
            }
            int need = OutnumberedBy + (persona.Style == BotStyle.Aggressive ? 1 : 0);
            if (enemiesSeen - friends >= need && NearestAlly(ReinforceRange, false) != null)
            {
                mode = Mode.Retreat;
                retreatUntil = Mathf.Max(retreatUntil, Time.time + Random.Range(4f, 7f));
                return;
            }
        }

        float huntRange = persona.Style == BotStyle.Opportunist ? 220f : 110f;
        // Chasse : seulement une cible vue recemment (ou toute proche), pas a travers la carte
        bool huntKnown = targetVisible || Time.time - lastSeenTime < 6f || dist < 60f;
        if (target.HealthFraction < 0.45f && dist < huntRange && persona.Aggression > 0.25f && huntKnown)
            mode = Mode.Hunt;
        else if (targetVisible || Time.time - lastSeenTime < 3f)
            mode = Mode.Engage;
        else
            mode = Mode.Advance;
    }

    // Arme du slot adaptee a la distance
    private void PickWeapon()
    {
        if (loadout == null || loadout.slots.Count == 0)
            return;
        float dist = target != null ? Vector3.Distance(transform.position, target.Position) : 80f;
        int bestIndex = loadout.SelectedIndex;
        float bestScore = float.MinValue;
        for (int i = 0; i < loadout.slots.Count; i++)
        {
            var slot = loadout.slots[i];
            if (slot.kind != LoadoutKind.Weapon)
                continue;
            bool alive = false;
            foreach (var v in slot.blocks)
            {
                if (!v.destroyed) { alive = true; break; }
            }
            if (!alive)
                continue;
            var s = WeaponFireController.Resolve(slot.definition);
            // Porte-t-elle ? (lames : contact seulement)
            bool inRange = s.kind switch
            {
                WeaponFireController.Kind.Rail => dist <= 260f,
                WeaponFireController.Kind.Plasma => dist <= 120f,
                WeaponFireController.Kind.Tesla => dist <= 10f,
                WeaponFireController.Kind.Laser => dist <= 170f,
                _ => false,
            };
            if (s.kind == WeaponFireController.Kind.None)
                continue;
            // La plus grosse d'abord : degats par tir (x nombre de canons) ; sans l'energie
            // pour un tir, elle passe derriere les petites
            float score = (inRange ? 10f : 0f) + s.damage * Mathf.Max(1, slot.Count) / 100f;
            if (energy != null && !energy.CanSpend(s.cost))
                score -= 6f;
            if (s.kind == WeaponFireController.Kind.Tesla && dist < 10f)
                score += 5f;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        if (bestIndex >= 0 && bestIndex != loadout.SelectedIndex)
            loadout.Select(bestIndex);

        var sel = loadout.Selected;
        var stats = sel != null ? WeaponFireController.Resolve(sel.definition) : default;
        weaponKind = stats.kind;
        switch (stats.kind)
        {
            case WeaponFireController.Kind.Rail: weaponRange = 260f; weaponSpeed = 0f; weaponGravity = 0f; break;
            case WeaponFireController.Kind.Plasma: weaponRange = 120f; weaponSpeed = stats.speed; weaponGravity = stats.gravity; break;
            case WeaponFireController.Kind.Tesla: weaponRange = 6f; weaponSpeed = 0f; weaponGravity = 0f; break;
            default: weaponRange = 170f; weaponSpeed = stats.speed > 0f ? stats.speed : 150f; weaponGravity = 0f; break;
        }
    }

    // Distance de combat voulue selon l'arme et le caractere
    private float PreferredDistance()
    {
        float d = weaponKind switch
        {
            WeaponFireController.Kind.Rail => 90f,
            WeaponFireController.Kind.Plasma => 45f,
            WeaponFireController.Kind.Tesla => 2f,
            _ => 35f,
        };
        return weaponKind == WeaponFireController.Kind.Tesla ? d : d * persona.RangeFactor;
    }

    private void PickDestination()
    {
        Vector3 me = transform.position;
        backingOff = false;
        // Soutien en cours (hors combat) : vers l'allie engage, en tenaille sur sa cible
        if (mode != Mode.Retreat && supportAlly != null && !IsFighting)
        {
            SetDestination(SupportPoint());
            return;
        }
        // Sorti d'un amas sans etre au contact : retour vers un point de son couloir
        if (mode != Mode.Retreat && Time.time < declumpUntil && !InContact)
        {
            SetDestination(LaneGoal() + laneOffset);
            return;
        }
        switch (mode)
        {
            case Mode.Retreat:
            {
                // Renfort : vers l'allie le plus proche (pas le centre de toute l'equipe, qui
                // etait justement l'amas), sinon sa base, a l'oppose de la menace
                var buddy = NearestAlly(ReinforceRange, true) ?? NearestAlly(ReinforceRange, false);
                Vector3 safe = buddy != null ? buddy.Position : self.SpawnPosition;
                NoteReinforce(buddy, "decroche");
                if (target != null)
                {
                    Vector3 away = me - target.Position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 1f)
                        safe += away.normalized * 15f;
                }
                SetDestination(safe);
                break;
            }
            case Mode.Hunt:
            {
                // Jusqu'a portee de tir, pas jusque dans la cible
                Vector3 toTarget = target.Position - me;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                float stop = weaponKind == WeaponFireController.Kind.Tesla ? 0f : Mathf.Min(PreferredDistance() * 0.7f, dist * 0.5f);
                SetDestination(dist > 0.01f ? target.Position - toTarget / dist * stop : target.Position);
                break;
            }
            case Mode.Engage:
            {
                Vector3 toTarget = (targetVisible ? target.Position : lastSeenPos) - me;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                float want = PreferredDistance();
                Vector3 dir = dist > 0.01f ? toTarget / dist : transform.forward;
                if (dist > want + 8f || !targetVisible)
                {
                    // Chacun son angle d'approche autour de la cible (meute) : pas de grappe
                    float spread = ((self.Id % 5) - 2) * 36f;
                    Vector3 approach = Quaternion.AngleAxis(spread, Vector3.up) * -dir;
                    SetDestination(targetVisible ? target.Position + approach * want * 0.8f : lastSeenPos + approach * 12f);
                }
                else if (dist < want - 8f && weaponKind != WeaponFireController.Kind.Tesla)
                {
                    backingOff = true;
                    SetDestination(me - dir * 12f);
                }
                else
                {
                    if (Time.time >= nextCircleFlip)
                    {
                        circleSign = Random.value < 0.5f ? -1f : 1f;
                        nextCircleFlip = Time.time + Random.Range(2.5f, 5f);
                    }
                    Vector3 tangent = Vector3.Cross(Vector3.up, dir) * circleSign;
                    SetDestination(me + tangent * 10f + dir * (dist - want) * 0.5f);
                }
                break;
            }
            default:
            {
                // Avant : ennemi vu il y a moins de 20 s, sinon la position de la cible (meme
                // invisible, a 400 m), sinon la base adverse -> tout le monde au meme endroit.
                // Maintenant : un ennemi perdu de vue tout pres, sinon son couloir.
                Vector3 goal;
                TdmParticipant buddy = null;
                if (self.HealthFraction < WeakJoinLife && (buddy = NearestAlly(ReinforceRange, true)) != null)
                {
                    // Abime, hors combat : il rejoint l'allie le plus proche (renfort)
                    goal = buddy.Position;
                    NoteReinforce(buddy, "abime");
                }
                else if (target != null && Time.time - lastSeenTime < 20f && FlatDist(me, lastSeenPos) < ChaseLeash)
                    goal = lastSeenPos;
                else
                    goal = LaneGoal();
                if (persona.Style == BotStyle.Cautious && buddy == null)
                {
                    // Le prudent avance avec l'allie le plus proche s'il y en a un pres de lui
                    var near = NearestAlly(CautiousBuddyRange, false);
                    if (near != null)
                        goal = Vector3.Lerp(near.Position, goal, 0.5f);
                }
                SetDestination(goal + laneOffset);
                break;
            }
        }
    }

    private void SetDestination(Vector3 point)
    {
        destination = point;
        hasDestination = true;
        if (flier)
            return;
        bool far = (point - pathGoal).sqrMagnitude > 36f;
        if (far || Time.time >= nextRepath)
            Repath();
    }

    private void Repath()
    {
        nextRepath = Time.time + 1.5f;
        pathGoal = destination;
        corners.Clear();
        corner = 0;
        if (MatchNavMesh.TryPath(transform.position, destination, navPath, Wide))
        {
            corners.AddRange(navPath.corners);
            corner = corners.Count > 1 ? 1 : 0;
        }
    }

    // =========================================================
    // REPARTITION : ROLE ET COULOIR, RENFORT, SOUTIEN, ANTI-AMAS
    // =========================================================

    // Temps passe en renfort / en soutien (bilan BotNavStats), compte a chaque decision
    private void TrackGroup()
    {
        if (reinforceAlly != null && Time.time - reinforceStamp < ThinkPeriod * 2f + 0.05f)
            nav.reinforce += ThinkPeriod;
        if (supportAlly != null)
            nav.support += ThinkPeriod;
    }

    // Role : tire a la premiere decision de chaque robot (apparition ou reapparition), puis
    // toutes les RoleShuffleMin-Max s (plus tot apres un changement de style), hors combat
    private void UpdateRole()
    {
        if (!roleReady)
        {
            bool had = BotLanes.HasRole(match, self, out _);
            AssignRole(had ? "reapparition" : "debut", had);
            return;
        }
        if (Time.time >= nextRoleShuffle && mode != Mode.Retreat && supportAlly == null && !IsFighting)
            AssignRole(styleShuffle ? "nouveau style" : "rotation", true);
    }

    private void AssignRole(string why, bool reroll)
    {
        BotLanes.Register(match, self, this);
        bool had = BotLanes.HasRole(match, self, out var previous);
        role = BotLanes.Assign(match, self, flier, HasRail(), persona.Style, reroll);
        roleReady = true;
        styleShuffle = false;
        nextRoleShuffle = Time.time + Random.Range(RoleShuffleMin, RoleShuffleMax);
        var pts = BotLanes.Points(match, self.Side, role);
        laneIndex = BotLanes.NearestIndex(pts, transform.position);
        laneStep = 1;
        lanePointSince = Time.time;
        laneArrivedAt = -1f;
        if (!had || previous != role)
        {
            if (had)
                nav.roleChanges++;
            Debug.Log($"[Bot] {self.Name} ({self.RobotName}) role : {(had ? BotLanes.RoleName(previous) + " -> " : "")}{BotLanes.RoleName(role)} ({why}, {persona.Style}).");
        }
    }

    // Une arme a rail dans ses slots (candidat au role de tireur en hauteur)
    private bool HasRail()
    {
        if (loadout == null)
            return false;
        foreach (var slot in loadout.slots)
        {
            if (slot.kind == LoadoutKind.Weapon && WeaponFireController.Resolve(slot.definition).kind == WeaponFireController.Kind.Rail)
                return true;
        }
        return false;
    }

    // Point de patrouille courant de son couloir ; passe au suivant une fois atteint (apres
    // une courte pause, longue pour le tireur en hauteur) ou au bout de LanePointTimeout
    private Vector3 LaneGoal()
    {
        var pts = BotLanes.Points(match, self.Side, role);
        if (pts == null || pts.Count == 0)
            return match.EnemyBase(self.Side);
        laneIndex = Mathf.Clamp(laneIndex, 0, pts.Count - 1);
        Vector3 goal = pts[laneIndex];
        float now = Time.time;
        bool arrived = FlatDist(transform.position, goal) < (flier ? LaneReach * 1.4f : LaneReach);
        if (arrived && laneArrivedAt < 0f)
        {
            laneArrivedAt = now;
            // Pause : longue pour le tireur, courte aux bouts de la patrouille, aucune en chemin
            bool patrolEnd = laneIndex >= LaneLimit(pts.Count) || laneStep < 0;
            dwellUntil = now + (role == BotRole.Sniper ? Random.Range(8f, 16f)
                                : role != BotRole.Marauder && patrolEnd ? Random.Range(1f, 4f) : 0f);
        }
        if ((arrived && now >= dwellUntil) || now - lanePointSince > LanePointTimeout)
        {
            laneIndex = NextLaneIndex(pts.Count);
            laneArrivedAt = -1f;
            lanePointSince = now;
            goal = pts[laneIndex];
        }
        return goal;
    }

    // Couloir : avance jusqu'a sa limite puis va-et-vient entre les deux derniers points ;
    // tireur : va-et-vient entre ses points hauts ; maraudeur : boucle
    private int NextLaneIndex(int n)
    {
        if (n <= 1)
            return 0;
        if (role == BotRole.Marauder)
            return (laneIndex + 1) % n;
        int hi = LaneLimit(n);
        int lo = role == BotRole.Sniper ? 0 : Mathf.Max(0, hi - 1);
        if (laneIndex > hi)
        {
            // Au-dela de sa limite (devenu prudent) : il revient
            laneStep = -1;
            return laneIndex - 1;
        }
        int next = laneIndex + laneStep;
        if (next > hi)
        {
            laneStep = -1;
            next = Mathf.Max(lo, laneIndex - 1);
        }
        else if (next < lo && laneIndex <= lo)
        {
            laneStep = 1;
            next = Mathf.Min(hi, laneIndex + 1);
        }
        return Mathf.Clamp(next, 0, n - 1);
    }

    // Point le plus avance de son couloir : un prudent s'arrete avant, un equilibre sur deux aussi
    private int LaneLimit(int n)
    {
        if (role == BotRole.Sniper || role == BotRole.Marauder)
            return n - 1;
        int hold = persona.Style switch
        {
            BotStyle.Cautious => 1,
            BotStyle.Balanced => self.Id % 2,
            _ => 0,
        };
        return Mathf.Clamp(n - 1 - hold, 0, n - 1);
    }

    // Allie vivant le plus proche a moins de range (sans ceux qui decrochent eux-memes si demande)
    private TdmParticipant NearestAlly(float range, bool skipRetreating)
    {
        Vector3 me = transform.position;
        TdmParticipant best = null;
        float bestD = range;
        foreach (var p in match.Participants)
        {
            if (p == self || p.Side != self.Side || !p.Alive)
                continue;
            if (skipRetreating)
            {
                var b = BotLanes.BrainOf(match, p);
                if (b != null && b.Retreating)
                    continue;
            }
            float d = Vector3.Distance(me, p.Position);
            if (d < bestD)
            {
                bestD = d;
                best = p;
            }
        }
        return best;
    }

    // Renfort : note l'allie rejoint (bilan) et le journalise une fois par episode
    private void NoteReinforce(TdmParticipant buddy, string why)
    {
        if (buddy == null)
            return;
        bool fresh = buddy != reinforceAlly || Time.time - reinforceStamp > 3f;
        reinforceAlly = buddy;
        reinforceStamp = Time.time;
        if (fresh && Time.time - lastGroupLog > 8f)
        {
            lastGroupLog = Time.time;
            Debug.Log($"[Bot] {self.Name} ({self.RobotName}) se replie sur {buddy.Name} ({Vector3.Distance(transform.position, buddy.Position):0} m) : {why}, vie {self.HealthFraction:P0}.");
        }
    }

    // Soutien : un bot hors combat va aider un allie engage a portee, 1 au plus par allie
    // (2 s'il est en difficulte), allies deja sur place compris
    private void UpdateSupport()
    {
        float now = Time.time;
        if (supportAlly != null)
        {
            TdmParticipant current = null;
            bool end = !supportAlly.Alive || mode == Mode.Retreat || now > supportUntil || IsFighting;
            if (!end)
                end = !AllyEngaged(supportAlly, out current);
            if (end)
            {
                supportAlly = null;
                supportFoe = null;
                supportCooldownUntil = now + SupportCooldown;
            }
            else if (current != null)
                supportFoe = current;
            return;
        }
        if (now < supportCooldownUntil || now < nextSupportScan || mode != Mode.Advance
            || self.HealthFraction < SupportMinLife || (inPit && hasPitExit))
            return;
        nextSupportScan = now + 0.5f;

        float rangeK = persona.Style switch
        {
            BotStyle.Cautious => 0.7f,
            BotStyle.Aggressive => 1.2f,
            BotStyle.Opportunist => 1.1f,
            _ => 1f,
        };
        if (role == BotRole.Sniper) rangeK *= 0.6f;
        if (role == BotRole.Marauder) rangeK *= 1.5f;

        Vector3 me = transform.position;
        TdmParticipant best = null, bestFoe = null;
        float bestScore = float.MinValue;
        bool bestTrouble = false;
        foreach (var a in match.Participants)
        {
            if (a == self || a.Side != self.Side || !a.Alive)
                continue;
            float d = Vector3.Distance(me, a.Position);
            if (d < 30f || d > SupportRangeTrouble * rangeK)
                continue;   // deja a cote, ou trop loin
            if (!AllyEngaged(a, out var foe))
                continue;
            bool trouble = AllyInTrouble(a);
            if (d > (trouble ? SupportRangeTrouble : SupportRange) * rangeK)
                continue;
            int cap = trouble ? SupportersMaxTrouble : SupportersMax;
            int helpers = BotLanes.SupportersOf(match, a, self) + FriendsNear(a.Position, 45f, a);
            if (helpers >= cap)
                continue;
            float score = (trouble ? 200f : 0f) - d;
            if (score > bestScore)
            {
                bestScore = score;
                best = a;
                bestFoe = foe;
                bestTrouble = trouble;
            }
        }
        if (best == null)
            return;
        supportAlly = best;
        supportFoe = bestFoe;
        supportUntil = now + SupportMaxSeconds;
        nav.supports++;
        Debug.Log($"[Bot] {self.Name} ({self.RobotName}) part soutenir {best.Name} ({Vector3.Distance(me, best.Position):0} m, " +
                  $"{(bestTrouble ? "en difficulte" : "au combat")}){(bestFoe != null ? $" contre {bestFoe.Name}" : "")}.");
    }

    // Allie qui se bat : un bot avec une cible en vue ; le joueur, un ennemi a moins de 90 m
    private bool AllyEngaged(TdmParticipant a, out TdmParticipant foe)
    {
        foe = null;
        var b = BotLanes.BrainOf(match, a);
        if (b != null)
        {
            foe = b.Target;
            return b.IsFighting && foe != null && foe.Targetable;
        }
        if (a.IsBot)
            return false;
        float best = 90f;
        foreach (var e in match.Participants)
        {
            if (e.Side == a.Side || !e.Targetable)
                continue;
            float d = Vector3.Distance(e.Position, a.Position);
            if (d < best)
            {
                best = d;
                foe = e;
            }
        }
        return foe != null;
    }

    // En difficulte : vie basse, ou plus d'ennemis a 80 m que d'amis a 50 m (lui compris)
    private bool AllyInTrouble(TdmParticipant a)
    {
        if (a.HealthFraction < AllyTroubleLife)
            return true;
        int enemies = 0, friends = 1;
        foreach (var p in match.Participants)
        {
            if (p == a || !p.Alive)
                continue;
            float d = Vector3.Distance(p.Position, a.Position);
            if (p.Side != a.Side && d < 80f && p.Targetable) enemies++;
            else if (p.Side == a.Side && d < 50f) friends++;
        }
        return enemies > friends;
    }

    // Allies vivants (hors lui et hors "except") a moins de radius d'un point
    private int FriendsNear(Vector3 at, float radius, TdmParticipant except)
    {
        int n = 0;
        foreach (var p in match.Participants)
        {
            if (p == self || p == except || p.Side != self.Side || !p.Alive)
                continue;
            if (FlatDist(p.Position, at) < radius)
                n++;
        }
        return n;
    }

    // Point de soutien : a sa distance de tir de la cible de l'allie, 50 degres a cote de
    // l'allie (tenaille), pas dans son dos
    private Vector3 SupportPoint()
    {
        Vector3 allyPos = supportAlly.Position;
        if (supportFoe == null || !supportFoe.Targetable)
            return allyPos;
        Vector3 foe = supportFoe.Position;
        Vector3 fromFoe = allyPos - foe;
        fromFoe.y = 0f;
        float d = fromFoe.magnitude;
        if (d < 1f)
            return allyPos;
        float angle = self.Id % 2 == 0 ? 50f : -50f;
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * (fromFoe / d);
        return foe + dir * Mathf.Clamp(PreferredDistance(), 15f, Mathf.Max(15f, d));
    }

    // Anti-amas (chaque seconde) : plus de ClumpMaxRobots robots dans ClumpRadius, ou deja
    // MaxAlliesPerPoint allies sur place (au contact, ou de numero plus petit que lui) ; s'il
    // n'est pas lui-meme au contact, il repart vers un point de son couloir loin de l'amas
    private void UpdateClump()
    {
        float now = Time.time;
        if (now < nextClumpCheck)
            return;
        nextClumpCheck = now + 1f;
        if (mode == Mode.Retreat || now < declumpUntil || (inPit && hasPitExit) || supportAlly != null)
            return;
        Vector3 me = transform.position;
        // A sa base (apparition, toute l'equipe groupee) : chacun part deja vers son couloir
        if (FlatDist(me, self.SpawnPosition) < 60f)
            return;
        int total = 1, alliesAhead = 0;
        Vector3 sum = me;
        foreach (var p in match.Participants)
        {
            if (p == self || !p.Alive || FlatDist(p.Position, me) > ClumpRadius)
                continue;
            total++;
            sum += p.Position;
            if (p.Side == self.Side)
            {
                var b = BotLanes.BrainOf(match, p);
                bool engaged = b == null || b.InContact;   // le joueur compte comme engage
                if (engaged || p.Id < self.Id)
                    alliesAhead++;
            }
        }
        if (total <= ClumpMaxRobots && alliesAhead < MaxAlliesPerPoint)
            return;
        if (InContact)
            return;
        declumpUntil = now + Random.Range(DeclumpMin, DeclumpMax);
        clumpCenter = sum / total;
        nav.declumps++;
        // Point de son couloir le plus proche de lui hors de l'amas
        var pts = BotLanes.Points(match, self.Side, role);
        if (pts != null && pts.Count > 0)
        {
            laneIndex = Mathf.Clamp(laneIndex, 0, pts.Count - 1);
            if (FlatDist(pts[laneIndex], clumpCenter) < ClumpRadius * 2f)
            {
                float bestD = float.MaxValue;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (FlatDist(pts[i], clumpCenter) < ClumpRadius * 2f)
                        continue;
                    float dm = FlatDist(pts[i], me);
                    if (dm < bestD)
                    {
                        bestD = dm;
                        laneIndex = i;
                    }
                }
                lanePointSince = now;
                laneArrivedAt = -1f;
            }
        }
        if (now - lastClumpLog > 20f)
        {
            lastClumpLog = now;
            Debug.Log($"[Bot] {self.Name} ({self.RobotName}) quitte un amas ({total} robots a {ClumpRadius:0} m) : retour couloir {BotLanes.RoleName(role)}.");
        }
    }

    private static float FlatDist(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // =========================================================
    // VISEE ET TIR
    // =========================================================

    private bool CanSee(TdmParticipant p)
    {
        Vector3 eye = transform.position + Vector3.up * (loco.RobotHeight + 0.6f);
        Vector3 at = p.AimPoint;
        return !Physics.Linecast(eye, at, TerrainMask, QueryTriggerInteraction.Ignore);
    }

    private void Aim()
    {
        if (target == null || !target.Targetable)
        {
            aim.ManualAimPoint = transform.position + transform.forward * 30f + Vector3.up * loco.RobotHeight;
            return;
        }
        Vector3 at = target.AimPoint;
        float dist = Vector3.Distance(transform.position, at);

        // Anticipation : la cible aura bouge pendant le vol du projectile
        if (weaponSpeed > 0f && target.Loco != null && target.Loco.Body != null)
        {
            float t = dist / weaponSpeed;
            at += target.Loco.Body.linearVelocity * (t * persona.LeadAccuracy);
            // Cloche du plasma : on vise plus haut d'autant que la boule tombera
            if (weaponGravity > 0f)
                at += Vector3.up * (0.5f * -Physics.gravity.y * weaponGravity * t * t);
        }

        if (Time.time >= nextAimJitter)
        {
            float err = persona.AimError * Mathf.Clamp(dist / 50f, 0.4f, 2.5f);
            // Une cible qui vient d'apparaitre est plus dure a cadrer
            if (visibleSince > 0f && Time.time - visibleSince < 1f)
                err *= 1.6f;
            aimOffset = Random.insideUnitSphere * err;
            nextAimJitter = Time.time + Random.Range(0.25f, 0.55f);
        }
        aim.ManualAimPoint = at + aimOffset;
    }

    private void Shoot()
    {
        bool can = target != null && target.Targetable && targetVisible && visibleSince > 0f
                   && Time.time - visibleSince >= persona.ReactionTime;
        if (can)
        {
            float dist = Vector3.Distance(transform.position, target.Position);
            can = dist <= weaponRange;
        }
        // Ligne de tir : au moins une tourelle alignee dont la bouche voit le point vise
        if (can && weaponKind != WeaponFireController.Kind.Tesla)
        {
            bool clear = false;
            Vector3 at = aim.ManualAimPoint;
            foreach (var t in aim.Turrets)
            {
                if (t.view != null && t.view.destroyed) continue;
                if (!t.Aligned) continue;
                Vector3 muzzle = t.Muzzle;
                if (!Physics.Linecast(muzzle, at, TerrainMask, QueryTriggerInteraction.Ignore)) { clear = true; break; }
            }
            if (!clear)
            {
                if (aimBlockedSince < 0f) aimBlockedSince = Time.time;
                can = false;
            }
            else
                aimBlockedSince = -1f;
        }
        else
            aimBlockedSince = -1f;
        if (!can)
        {
            fire.FireHeld = false;
            bursting = false;
            return;
        }
        if (weaponKind == WeaponFireController.Kind.Tesla)
        {
            fire.FireHeld = true;   // lames : tant qu'on est au contact
            return;
        }
        burstTimer -= Time.deltaTime;
        if (burstTimer <= 0f)
        {
            bursting = !bursting;
            burstTimer = bursting ? persona.BurstSeconds * Random.Range(0.8f, 1.2f) : persona.PauseSeconds * Random.Range(0.8f, 1.3f);
        }
        fire.FireHeld = bursting;
    }

    // =========================================================
    // PASSAGES ETROITS (pont, corniche, couloir)
    // =========================================================

    private void UpdateLane()
    {
        narrow = false;
        if (!hasDestination || (inPit && hasPitExit))
            return;

        Vector3 me = transform.position;
        Vector3 next = NextWaypoint(me);
        Vector3 dir = next - me;
        dir.y = 0f;
        if (dir.magnitude < 3f)
            return;
        dir.Normalize();
        float ahead = Mathf.Clamp(5f + loco.Speed * 0.8f, 5f, 22f);   // vitesses x1,55 : anticipation allongee
        Vector3 p = me + dir * ahead;
        float g = GroundHeight(p);
        if (g < me.y - 20f)
            return;
        Vector3 rightDir = Vector3.Cross(Vector3.up, dir);
        float right = LaneEdge(p, g, rightDir);
        float left = LaneEdge(p, g, -rightDir);
        // Etroit = bornes des deux cotes et moins de 8 m de marge en tout pour ce robot
        if (right >= LaneScan || left >= LaneScan || left + right > 2f * HalfWidth + 8f)
            return;
        narrow = true;
        laneAim = p + rightDir * ((right - left) * 0.5f) + dir * 4f;
    }

    private const float LaneScan = 12f;

    // Distance au bord du passage, de p vers side : vide de plus de 1,5 m, ou rambarde /
    // paroi a hauteur de caisse
    private float LaneEdge(Vector3 p, float ground, Vector3 side)
    {
        float limit = LaneScan;
        var origin = new Vector3(p.x, ground + 0.8f, p.z);
        if (Physics.Raycast(origin, side, out var hit, limit, TerrainMask, QueryTriggerInteraction.Ignore) && hit.normal.y < 0.65f)
            limit = hit.distance;
        for (float k = 1f; k < limit; k += 1f)
        {
            if (GroundHeight(new Vector3(p.x, ground, p.z) + side * k) < ground - 1.5f)
                return k - 0.5f;
        }
        return limit;
    }

    // Demi-largeur du robot (pattes, roues, armes comprises), mesuree une fois
    public float HalfWidth => halfWidth >= 0f ? halfWidth : (halfWidth = MeasureHalfWidth());

    // Robot large (pattes, chenilles, caisse de 5) : chemins du NavMesh large, loin des rochers
    private bool Wide => !flier && HalfWidth > 2.4f;

    private float MeasureHalfWidth()
    {
        float half = 1.5f;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var local = transform.InverseTransformPoint(r.bounds.center);
            half = Mathf.Max(half, Mathf.Abs(local.x) + 0.5f * Mathf.Min(r.bounds.size.x, r.bounds.size.z));
        }
        return Mathf.Min(half, flier ? 14f : 6f);   // un volant peut faire 25 cases d'envergure
    }

    // =========================================================
    // CONDUITE
    // =========================================================

    private void Drive()
    {
        if (!hasDestination)
        {
            Stop();
            return;
        }

        // Colle a un autre robot sans avancer : on se degage tout de suite, l'arriere du
        // cote oppose a lui (en marche arriere, l'arriere part du cote du braquage)
        if (contactSince >= 0f && Time.time - contactSince > 0.8f && loco.Speed < 3f && Time.time >= unstickUntil
            && !(weaponKind == WeaponFireController.Kind.Tesla && mode == Mode.Engage && target != null && targetVisible))
        {
            float side = Vector3.SignedAngle(Flat(transform.forward), contactDir, Vector3.up);
            unstickUntil = Time.time + Random.Range(1f, 1.4f);
            unstickSteer = side > 0f ? -1f : 1f;
            unstickThrottle = -0.8f;
            unstickBoost = false;
            contactSince = -1f;
            stuckTime = 0f;
            nav.contacts++;
        }

        // Deblocage en cours : marche arriere en braquant
        if (Time.time < unstickUntil)
        {
            loco.throttle = unstickThrottle;
            loco.steer = unstickSteer;
            loco.boost = unstickBoost;
            loco.lift = flier ? 1f : 0f;
            return;
        }

        Vector3 me = transform.position;
        Vector3 next = NextWaypoint(me);
        Vector3 to = next - me;
        to.y = 0f;
        float dist = to.magnitude;

        if (flier)
        {
            DriveFlier(to, dist);
            return;
        }

        if (dist < 2.5f && corner >= corners.Count - 1)
        {
            // Arrive : en combat on garde le nez vers la cible
            loco.throttle = 0f;
            loco.steer = target != null ? SteerTowards(target.Position - me) * 0.6f : 0f;
            loco.boost = false;
            loco.lift = 0f;
            stuckTime = 0f;
            return;
        }

        // Pont ou corniche devant : on vise le milieu du passage plutot que le coin du chemin
        bool centering = narrow && !inPit && !backingOff && !(dist < 6f && corner >= corners.Count - 1);
        if (centering)
            nav.narrow += Time.deltaTime;
        if (centering)
        {
            Vector3 lane = laneAim - me;
            lane.y = 0f;
            if (lane.sqrMagnitude > 1f)
                to = lane.normalized * dist;
        }

        float steer, throttle;
        bool aimBlocked = aimBlockedSince >= 0f && Time.time - aimBlockedSince > 0.6f && target != null && targetVisible;
        if (aimBlocked && Time.time - aimBlockedSince < 3f)
        {
            // Tourelles masquees par le corps ou le relief : on tourne le nez vers la cible sur place
            steer = SteerTowards(target.Position - me);
            throttle = Mathf.Abs(steer) > 0.3f ? 0.2f : 0.35f;
        }
        else if (aimBlocked)
        {
            // Toujours rien : on change de position (de cote par rapport a la cible)
            Vector3 dirT = Flat(target.Position - me);
            SetDestination(me + Vector3.Cross(Vector3.up, dirT) * (12f * circleSign) + dirT * 4f);
            aimBlockedSince = Time.time;
            steer = SteerTowards(to);
            throttle = 0.6f;
        }
        else if (backingOff && target != null)
        {
            // Recule en gardant le nez vers la cible
            steer = SteerTowards(target.Position - me);
            throttle = -0.75f;
        }
        else
        {
            steer = SteerTowards(to);
            float angle = Mathf.Abs(Vector3.SignedAngle(Flat(transform.forward), to, Vector3.up));
            throttle = angle < 100f ? Mathf.Lerp(1f, 0.45f, angle / 100f) : 0.25f;
            if (mode == Mode.Engage && dist < 12f)
                throttle *= 0.6f;
            throttle = Mathf.Clamp(throttle * (0.75f + 0.35f * persona.Aggression), -1f, 1f);
            // Remontee d'une crevasse : plein gaz quand le nez est dans l'axe
            if (inPit && angle < 35f)
                throttle = 1f;
            // Passage etroit : on y entre droit et sans se presser
            if (centering)
                throttle = Mathf.Min(throttle, angle < 12f ? 0.7f : 0.4f);
        }

        // Un autre robot devant : on s'ecarte et on ralentit (lames Tesla sur leur cible exceptees)
        if (Crowded(out float crowdSide, out float crowdDist)
            && !(weaponKind == WeaponFireController.Kind.Tesla && target != null && targetVisible && crowdDist < 8f))
        {
            float k = 1f - Mathf.Clamp01(crowdDist / (HalfWidth + 4f));
            steer = Mathf.Clamp(steer + (crowdSide > 0f ? -0.8f : 0.8f) * k, -1f, 1f);
            if (throttle > 0f)
                throttle *= Mathf.Lerp(1f, 0.3f, k);
        }

        // Pattes : saut devant une marche ou un obstacle, en remontee de crevasse, ou pour
        // esquiver quand on vient d'etre touche
        if (loco.CanJump && throttle > 0.3f)
        {
            float here = GroundHeight(me), ahead = GroundHeight(me + Flat(transform.forward) * 4f);
            if (ahead > here + 0.6f && ahead < here + 4f)
                Jump("marche");
            else if (inPit && Mathf.Abs(Vector3.SignedAngle(Flat(transform.forward), to, Vector3.up)) < 35f && Time.time - loco.LastJumpTime > 1.5f)
                Jump("crevasse");
            else if (mode == Mode.Engage && Time.time - lastHitTime < 1f && Random.value < persona.Aggression * 0.04f)
                Jump("esquive");
        }

        // Antennes d'evitement : un obstacle devant fait braquer de l'autre cote. Dans un
        // passage etroit, les rambardes ne sont pas des obstacles : seul le rayon central compte.
        if (throttle > 0f)
        {
            float avoid = Whiskers(out bool blockedAhead);
            if (!centering)
                steer = Mathf.Clamp(steer + avoid, -1f, 1f);
            if (blockedAhead)
            {
                throttle = Mathf.Min(throttle, 0.3f);
                if (loco.CanJump)
                    Jump("obstacle");
            }
            if (loco.CanJump && stuckTime > 0.8f)
                Jump("patine");
        }

        loco.throttle = throttle;
        loco.steer = steer;
        loco.lift = 0f;
        loco.boost = !centering && (inPit || (mode != Mode.Engage && dist > 40f && persona.Aggression > 0.55f)) && Mathf.Abs(steer) < 0.3f;
        CheckStuck(Mathf.Abs(throttle) > 0.3f);
    }

    private void DriveFlier(Vector3 to, float dist)
    {
        Vector3 me = transform.position;
        Vector3 fwd = Flat(transform.forward);
        Vector3 travel = dist > 0.5f ? to / dist : fwd;

        // Relief sur la route : point le plus haut du sol devant (dans l'axe du trajet et
        // a +/- 25 degres), plus loin quand on va vite ; la cible d'altitude le survole
        float look = Mathf.Clamp(12f + loco.Speed * 1.5f, 12f, 65f);
        // Sol mesure depuis sa propre altitude (+2 m) : sous un pont, c'est le sol qu'on voit,
        // pas le tablier (qui le faisait monter dans le dessous du pont)
        float terrain = GroundBelow(me);
        float nearRise = terrain;   // relief des 15 premiers metres
        var left = Quaternion.AngleAxis(-25f, Vector3.up) * travel;
        var right = Quaternion.AngleAxis(25f, Vector3.up) * travel;
        for (int i = 1; i <= 6; i++)
        {
            float d = look * i / 6f;
            float h = Mathf.Max(GroundBelow(me + travel * d),
                                Mathf.Max(GroundBelow(me + left * (d * 0.8f)), GroundBelow(me + right * (d * 0.8f))));
            terrain = Mathf.Max(terrain, h);
            if (d <= 15f)
                nearRise = Mathf.Max(nearRise, h);
        }
        float wanted = terrain + persona.FlyHeight + (self.Id % 3) * 4f;   // altitudes etagees : pas deux volants a la meme hauteur

        // Cible trop a la verticale pour les tourelles (tangage -20 degres) : on descend vers elle
        if (mode == Mode.Engage && target != null && targetVisible)
        {
            Vector3 dt = target.Position - me;
            float horizontal = new Vector2(dt.x, dt.z).magnitude;
            float maxAltitude = target.Position.y + horizontal * Mathf.Tan(15f * Mathf.Deg2Rad);
            wanted = Mathf.Max(terrain + 4f, Mathf.Min(wanted, maxAltitude));
        }

        // Plafond (tablier de pont, arche) : on reste 3 m dessous
        if (Physics.Raycast(me, Vector3.up, out var ceiling, 40f, TerrainMask, QueryTriggerInteraction.Ignore))
            wanted = Mathf.Min(wanted, ceiling.point.y - 3f);

        // Paroi devant (flanc de colline, falaise, pont) : on monte au-dessus de son sommet
        float clearance = Mathf.Max(1.2f, loco.RobotHeight * 0.6f);
        bool wall = Physics.SphereCast(me, clearance, travel, out var wallHit, Mathf.Min(look, 20f), TerrainMask, QueryTriggerInteraction.Ignore);
        if (wall)
            wanted = Mathf.Max(wanted, GroundHeight(wallHit.point + travel * 2f) + persona.FlyHeight * 0.6f, me.y + 6f);

        float climb = wanted - me.y;
        loco.lift = Mathf.Clamp(climb / 3f, -0.6f, 1f);

        // Couche contre le relief (pente, flanc) : pleine portance, on recule, et si ca dure
        // au sol, redressement
        bool tilted = Vector3.Dot(transform.up, Vector3.up) < 0.55f;
        if (tilted)
        {
            if (tiltedSince < 0f)
                tiltedSince = Time.time;
            loco.lift = 1f;
            loco.throttle = -0.4f;
            loco.steer = 0f;
            loco.boost = false;
            if (Time.time - tiltedSince > 1.8f && recovery != null && NearGround() && recovery.Begin())
            {
                tiltedSince = -1f;
                nav.flips++;
            }
            return;
        }
        tiltedSince = -1f;

        Vector3 lookDir = to;
        if (mode == Mode.Engage && target != null && targetVisible && (dist < 20f || aimBlockedSince >= 0f))
            lookDir = target.Position - me;   // sur place ou tourelles masquees : nez vers la cible
        float steerFly = SteerTowards(lookDir);
        // Un autre volant a moins de 12 m : on s'en ecarte (et on monte un peu s'il est plus bas)
        foreach (var p in match.Participants)
        {
            if (p == self || !p.Alive || p.Robot == null || !BotRobotCatalog.IsFlier(p.RobotName))
                continue;
            Vector3 d = p.Position - me;
            float dd = d.magnitude;
            var otherBrain = p.Robot.GetComponent<BotBrain>();
            float keep = HalfWidth + (otherBrain != null ? otherBrain.HalfWidth : 4f) + 6f;   // envergures + 6 m
            if (dd > keep)
                continue;
            float side2 = Vector3.SignedAngle(fwd, Flat(d), Vector3.up);
            steerFly = Mathf.Clamp(steerFly + (side2 > 0f ? -0.8f : 0.8f) * (1f - dd / keep), -1f, 1f);
            if (d.y < 0f)
                loco.lift = Mathf.Max(loco.lift, 0.6f);
        }
        loco.steer = steerFly;
        float angle = Mathf.Abs(Vector3.SignedAngle(fwd, travel, Vector3.up));
        float throttle = dist < 6f ? 0f : (angle < 60f ? Mathf.Clamp01(dist / 30f) : 0.15f);
        // Trop bas pour passer ce qui est juste devant : on monte d'abord, on avance ensuite
        bool blocked = (wall && wallHit.distance < 12f) || nearRise + clearance > me.y;
        if (blocked && climb > 2f)
            throttle = Mathf.Min(throttle, 0.1f);
        else if (climb > 6f)
            throttle *= 0.5f;
        loco.throttle = throttle;
        loco.boost = false;
        // Le bot veut avancer : s'il n'y arrive pas (meme en montant), deblocage
        CheckStuck(dist > 6f && !blocked && throttle > 0.3f);
    }

    private Vector3 NextWaypoint(Vector3 me)
    {
        if (flier || corners.Count == 0)
            return destination;
        while (corner < corners.Count - 1)
        {
            Vector3 d = corners[corner] - me;
            d.y = 0f;
            if (d.magnitude > (Wide ? 3.5f : 2.5f))
                break;
            corner++;
        }
        return corners[Mathf.Clamp(corner, 0, corners.Count - 1)];
    }

    private float SteerTowards(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            return 0f;
        float angle = Vector3.SignedAngle(Flat(transform.forward), dir.normalized, Vector3.up);
        return Mathf.Clamp(angle / 35f, -1f, 1f);
    }

    // Deux rayons a +/- 28 degres devant : renvoie une correction de direction
    private float Whiskers(out bool blockedAhead)
    {
        blockedAhead = false;
        Vector3 origin = transform.position + Vector3.up * 0.45f;
        Vector3 fwd = Flat(transform.forward);
        Vector3 side = Vector3.Cross(Vector3.up, fwd) * (HalfWidth * 0.85f);
        float reach = Mathf.Clamp(6f + loco.Speed * 0.6f, 6f, 24f);
        // Rayons lateraux partis des flancs du robot (un rocher bas passait entre le rayon
        // central et les pattes d'un robot large), plus un rayon droit devant chaque flanc
        bool left = Probe(origin - side, Quaternion.AngleAxis(-28f, Vector3.up) * fwd, reach)
                    || Probe(origin - side, fwd, reach * 0.6f);
        bool right = Probe(origin + side, Quaternion.AngleAxis(28f, Vector3.up) * fwd, reach)
                     || Probe(origin + side, fwd, reach * 0.6f);
        // Rayon central, plus un rayon bas (petits rochers que les roues et les pattes accrochent)
        blockedAhead = Probe(origin, fwd, reach * 0.8f) || Probe(transform.position + Vector3.up * 0.15f, fwd, Mathf.Min(reach * 0.6f, 5f));
        whiskerLeft = left; whiskerRight = right; whiskerAhead = blockedAhead;
        if (left && !right) return 0.7f;
        if (right && !left) return -0.7f;
        if (left && right) return blockedAhead ? 0.9f * circleSign : 0f;
        return 0f;
    }

    private bool Probe(Vector3 origin, Vector3 dir, float reach)
    {
        int n = Physics.RaycastNonAlloc(origin, dir, whiskerHits, reach, ~(1 << 2), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = whiskerHits[i].collider;
            if (col == null || col.transform.IsChildOf(transform))
                continue;
            // La cible n'est un obstacle que de pres (on ne fonce pas dedans) ; les lames
            // Tesla, elles, cherchent le contact
            if (target != null && target.Robot != null && col.transform.IsChildOf(target.Robot.transform)
                && (weaponKind == WeaponFireController.Kind.Tesla || whiskerHits[i].distance > 5f))
                continue;
            // Une pente douce n'est pas un mur : jusqu'a 30 degres (ce que roues, pattes et
            // chenilles montent sans patiner) ; au fond d'une crevasse on accepte jusqu'a 49
            // degres pour tenter la remontee
            if (whiskerHits[i].normal.y > (inPit ? 0.65f : 0.87f))
                continue;
            return true;
        }
        return false;
    }

    private void CheckStuck(bool wantsToMove)
    {
        if (wantsToMove)
            nav.wantedToMove += Time.deltaTime;
        if (!wantsToMove || loco.Speed > 1.2f)
        {
            stuckTime = Mathf.Max(0f, stuckTime - Time.deltaTime * 2f);
            return;
        }
        float slope = Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out var under, 8f, TerrainMask, QueryTriggerInteraction.Ignore)
            ? Vector3.Angle(under.normal, Vector3.up) : 0f;
        nav.Stall(transform.position, Time.deltaTime, slope, Vector3.Angle(transform.up, Vector3.up));
        stuckTime += Time.deltaTime;
        if (stuckTime < 2.2f)
            return;
        stuckTime = 0f;

        if (Time.time - unstickWindowStart > 20f)
        {
            unstickWindowStart = Time.time;
            unstickCount = 0;
        }
        unstickCount++;

        // Sur le dos ou sur le flanc, au sol : redressement (en l'air, RobotRecovery
        // afficherait son avertissement au joueur)
        if (Vector3.Dot(transform.up, Vector3.up) < 0.5f && recovery != null && NearGround() && recovery.Begin())
        {
            nav.flips++;
            return;
        }

        // 1er essai : marche arriere en braquant ; 2e : marche avant a fond braquee de l'autre
        // cote (pose sur une arete, roues dans le vide) ; 3e : soulevement / redressement
        // Deuxieme soulevement au meme endroit en moins d'une minute : le robot est pose
        // sur son chassis (crete, rebord) et retomberait au meme point ; on le repose
        // plus loin sur son chemin (ou sur un point libre du NavMesh)
        Vector3? moveTo = null;
        if (!flier && unstickCount >= 3 && Time.time - lastLiftTime < 60f
            && (transform.position - lastLiftAt).sqrMagnitude < 10f * 10f && TryRelocationPoint(out var spot))
            moveTo = spot;
        if (!flier && unstickCount >= 3 && recovery != null && NearGround() && recovery.BeginAt(moveTo))
        {
            var at = transform.position;
            lastLiftAt = at;
            lastLiftTime = moveTo.HasValue ? -999f : Time.time;
            if (moveTo.HasValue)
            {
                nav.escapes++;
                Debug.Log($"[Bot] {self.Name} ({self.RobotName}) rebloque au meme endroit : repose a ({moveTo.Value.x:0}, {moveTo.Value.y:0}, {moveTo.Value.z:0}).");
            }
            nav.Lift(at);
            float gs = Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var gh, 8f, TerrainMask, QueryTriggerInteraction.Ignore) ? Vector3.Angle(gh.normal, Vector3.up) : -1f;
            Debug.Log($"[Bot] {self.Name} ({self.RobotName}) bloque : soulevement a ({at.x:0}, {at.y:0}, {at.z:0}), sol {gs:0} deg ({(gh.collider != null ? gh.collider.name : "?")}), " +
                      $"robot incline {Vector3.Angle(transform.up, Vector3.up):0} deg, cap {Flat(transform.forward).x:0.00};{Flat(transform.forward).z:0.00}, " +
                      $"vers {(hasDestination ? $"({destination.x:0}, {destination.z:0})" : "-")}{(narrow ? ", passage etroit" : "")}{(inPit ? ", crevasse" : "")} | {Diagnostic()}");
            unstickCount = 0;
            if (MatchNavMesh.RandomPointNear(transform.position, 25f, out var free, Wide))
            {
                destination = free;
                hasDestination = true;
                Repath();
                escapeUntil = Time.time + 4f;
            }
            return;
        }
        unstickUntil = Time.time + Random.Range(0.9f, 1.5f);
        bool forward = !flier && unstickCount == 2;
        if (forward) nav.forwards++; else nav.reverses++;
        unstickSteer = forward ? -unstickSteer : (Random.value < 0.5f ? -1f : 1f);
        unstickThrottle = forward ? 1f : -0.8f;
        unstickBoost = forward;
        if (inPit && hasPitExit && unstickCount >= 2)
        {
            // Cette sortie ne passe pas (mur, rebord) : on en cherche une autre
            unstickCount = 0;
            if (!PitExitFailed())
                FindPitExit(transform.position, GroundHeight(transform.position));
            return;
        }
        if (unstickCount >= 3)
        {
            // Bloque pour de bon : un autre point du NavMesh a proximite
            nav.escapes++;
            if (MatchNavMesh.RandomPointNear(transform.position, 25f, out var p, Wide))
            {
                destination = p;
                hasDestination = true;
                Repath();
                escapeUntil = Time.time + 4f;
            }
            unstickCount = 0;
        }
    }

    // Etat detaille au moment d'un blocage (journal)
    private string Diagnostic()
    {
        string hull = Time.time - hullContactTime < 1f ? $"coque au sol : {hullContact} ({hullContactDepth:+0.00;-0.00} m)" : "coque libre";
        string mesh = MatchNavMesh.Snap(transform.position, 6f, out var s, Wide) ? $"navmesh{(Wide ? " large" : "")} a {Vector3.Distance(transform.position, s):0.0} m" : "HORS navmesh (6 m)";
        string path = corners.Count == 0 ? "sans chemin" : $"chemin {corners.Count} coins, coin {corner} a {Flat(corners[Mathf.Clamp(corner, 0, corners.Count - 1)] - transform.position).magnitude:0} m";
        return $"appuis {loco.GroundedContacts}/{loco.TotalContacts}, gaz {loco.throttle:0.0} dir {loco.steer:0.0}, v {loco.Speed:0.0} m/s, {hull}, {mesh}, {path}, " +
               $"antennes G{(whiskerLeft ? "x" : "-")} C{(whiskerAhead ? "x" : "-")} D{(whiskerRight ? "x" : "-")}";
    }

    // Point ou reposer un robot rebloque : 8 a 16 m plus loin sur son chemin, sinon un
    // point du NavMesh a 8-20 m ; sol a moins de 18 degres et place libre (aucun robot)
    // Sortie de crevasse ratee (bloque contre elle, ou pas de remontee) : on l'ecarte.
    // A la deuxieme d'affilee, le trou ne se remonte pas en roulant (avant, le
    // compteur de deblocage etait remis a zero ici, aucun soulevement ne partait et
    // le bot tournait au fond indefiniment, constate le 22/10 sur un Rail sniper) :
    // on le repose sur le bord, sur le NavMesh. True si repose.
    private bool PitExitFailed()
    {
        if (hasPitExit)
            badExits.Add(pitExit);
        hasPitExit = false;
        pitFailures++;
        if (pitFailures < 2 || recovery == null || !NearGround() || !TryPitRelocation(out var rim) || !recovery.BeginAt(rim))
            return false;
        nav.escapes++;
        pitFailures = 0;
        pitProgressAt = Time.time;
        Debug.Log($"[Bot] {self.Name} ({self.RobotName}) ne sort pas de la crevasse : repose sur le bord a ({rim.x:0}, {rim.y:0}, {rim.z:0}).");
        return true;
    }

    // Point du NavMesh sur le bord d'une crevasse (10 m plus haut que le fond, a
    // 35-130 m), a plat et libre : le plus proche
    private bool TryPitRelocation(out Vector3 point)
    {
        Vector3 me = transform.position;
        float ground = GroundHeight(me);
        float clear = Mathf.Max(3f, HalfWidth);
        foreach (float r in new[] { 35f, 60f, 90f, 130f })
        {
            for (int i = 0; i < 16; i++)
            {
                Vector3 p = me + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * r;
                float g = GroundHeight(p);
                if (g < ground + 10f)
                    continue;
                p.y = g;
                if (!MatchNavMesh.Snap(p, 6f, out var s, Wide))
                    continue;
                if (Physics.Raycast(s + Vector3.up * 4f, Vector3.down, out var h, 10f, TerrainMask, QueryTriggerInteraction.Ignore)
                    && Vector3.Angle(h.normal, Vector3.up) > 18f)
                    continue;
                bool busy = false;
                foreach (var col in Physics.OverlapSphere(s + Vector3.up * (clear + 0.5f), clear, 1 << RobotTestSpawner.RobotLayer, QueryTriggerInteraction.Ignore))
                    if (!col.transform.IsChildOf(transform)) { busy = true; break; }
                if (busy)
                    continue;
                point = s;
                return true;
            }
        }
        point = me;
        return false;
    }

    private bool TryRelocationPoint(out Vector3 point)
    {
        Vector3 me = transform.position;
        var candidates = new List<Vector3>();
        for (int i = Mathf.Max(0, corner - 1); i + 1 < corners.Count; i++)
        {
            Vector3 a = corners[i], b = corners[i + 1];
            for (float u = 0f; u <= 1f; u += 0.1f)
            {
                Vector3 q = Vector3.Lerp(a, b, u);
                float d = Flat(q - me).magnitude;
                if (d >= 8f && d <= 16f) candidates.Add(q);
            }
            if (Flat(b - me).magnitude > 16f) break;
        }
        for (int i = 0; i < 10; i++)
            if (MatchNavMesh.RandomPointNear(me, 20f, out var r, Wide) && Flat(r - me).magnitude >= 8f)
                candidates.Add(r);
        float clear = Mathf.Max(3f, HalfWidth);
        foreach (var q in candidates)
        {
            if (!MatchNavMesh.Snap(q, 4f, out var s, Wide))
                continue;
            if (Physics.Raycast(s + Vector3.up * 4f, Vector3.down, out var h, 10f, TerrainMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(h.normal, Vector3.up) > 18f)
                continue;
            bool busy = false;
            foreach (var col in Physics.OverlapSphere(s + Vector3.up * (clear + 0.5f), clear, 1 << RobotTestSpawner.RobotLayer, QueryTriggerInteraction.Ignore))
                if (!col.transform.IsChildOf(transform)) { busy = true; break; }
            if (busy)
                continue;
            point = s;
            return true;
        }
        point = me;
        return false;
    }

    // Meme test que RobotRecovery.OnGround : un appui porte, ou le decor a portee
    private bool NearGround()
    {
        if (loco.GroundedContacts > 0)
            return true;
        Vector3 origin = loco.Body != null ? loco.Body.worldCenterOfMass : transform.position;
        float reach = loco.RobotHeight * 0.8f + 0.6f;
        int mask = ~(1 << RobotTestSpawner.RobotLayer);
        foreach (var dir in new[] { Vector3.down, -transform.up, transform.up, transform.right, -transform.right, transform.forward, -transform.forward })
        {
            if (Physics.Raycast(origin, dir, reach, mask, QueryTriggerInteraction.Ignore))
                return true;
        }
        return false;
    }

    // Sol sous un point, collines comprises : le rayon part de 150 m plus haut (depuis 60 m,
    // un volant au pied d'une grande colline partait de l'interieur et ne la voyait pas)
    // Sol sous un point en partant de 2 m au-dessus (ignore ce qui est plus haut : tabliers)
    private static float GroundBelow(Vector3 at)
    {
        return Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var hit, 400f, TerrainMask, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : GroundHeight(at);
    }

    private static float GroundHeight(Vector3 at)
    {
        return Physics.Raycast(at + Vector3.up * 150f, Vector3.down, out var hit, 600f, TerrainMask, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : at.y - 10f;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private void Stop()
    {
        loco.throttle = 0f;
        loco.steer = 0f;
        loco.lift = 0f;
        loco.boost = false;
        if (fire != null)
            fire.FireHeld = false;
    }
}
