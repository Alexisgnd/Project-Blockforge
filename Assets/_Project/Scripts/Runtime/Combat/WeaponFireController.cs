using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// TIR DES ARMES (match)
// Pose sur le robot par RobotTestSpawner. Clic gauche (curseur
// verrouille) : les armes du slot selectionne (RobotLoadoutState)
// tirent, a tour de role, en puisant dans RobotEnergy. Un module
// selectionne (radar, bouclier, nano) ne tire pas.
// Familles (lues dans BlockDefinition.family / nom, palier N1..N6
// dans le nom) :
// - Laser  : bolt lumineux (WeaponProjectile) tire comme une
//            balle, tres rapide, droit (sans gravite), faible
//            cout, etincelles a l'impact
// - Plasma : boule lumineuse (WeaponProjectile) soumise a une
//            gravite reduite, halo et arcs crepitants en vol
//            (PlasmaBoltFX) ; explosion a l'impact (PlasmaExplosion :
//            eclair, onde de choc, anneau et onde electrique au rayon
//            exact de la zone, arcs electriques ramifies, brulure au
//            sol, gerbe, braises, etincelles, fumee, secousse camera) ;
//            degats de zone degressifs et souffle sur les corps rigides.
//            Selon le niveau (N1..N6) : cadence 3,3 -> 8,2 tirs/s, boule
//            plus grosse, zone de 2,2 -> 6 m, effets plus denses.
//            Cout par tir ajuste pour garder une consommation
//            d'energie par seconde fixe a chaque niveau
// - Rail   : tir instantane lourd et lent, gros cout, long trait
//            blanc, recul sur le robot
// - Tesla  : corps a corps : slot choisi et arc ouvert, la frappe
//            part toute seule (sans clic) sur tout ennemi au contact
//            (un coup par robot et par lame a chaque tick, 10 ticks/s
//            a cadence fixe ; la lame traverse : le surplus d'un bloc
//            detruit passe au suivant). Arme au plus fort DPS de son
//            niveau, mais au contact seulement (TeslaGap : portee IA).
//            Arc de la lame (bras terminaux + bandes d'energie du
//            modele) : ferme quand la Tesla n'est pas le slot choisi,
//            s'ouvre (TeslaOpenSeconds) quand elle l'est ; arcs
//            electriques animes seulement arc ouvert et energie pour
//            un coup ; aucune frappe tant que l'arc n'est pas ouvert.
//            Chaque tick qui touche coute TeslaEnergyPerTick au slot
//            (toutes lames confondues, autonomie ~6 s) et chaque lame
//            qui blesse lache une decharge (eclair lame -> point
//            touche, flash, gerbe d'etincelles)
// Chaque tir part de la bouche de la tourelle (WeaponAimController)
// vers le point vise si le canon y est presque aligne, sinon dans
// l'axe du canon. Une cible touchee (ShootableTarget) est
// detruite puis reapparait ; un robot touche (RobotHealth) perd
// des PV sur le bloc atteint (zone pour le plasma). Le robot
// tireur ignore ses propres colliders. manual = true : un robot
// IA tient FireHeld au lieu du clic.
// Match (TdmMatch) : chaque impact porte le tireur (attribution des
// kills, tir allie coupe par RobotHealth.CanBeDamagedBy) ; rien ne
// tire tant que MatchGate.Frozen (compte a rebours, fin de match).
// Effets : traits = LineRenderer (shader Blockforge/Hologram,
// mode aretes = couleur pleine additive), etincelles = particules
// additives (Blockforge/DustParticle), tout en espace monde.
// Couleur des tirs : relative au joueur local (TeamRelation), jamais
// fixee par l'equipe : ses tirs et ceux de ses allies en bleu, ceux
// des ennemis en rouge (teinte des jetons BlockforgeTheme.Hud
// .TeamAlly / TeamEnemy a pleine saturation : TeamRelation
// .ShotColor). RefreshTeamColors recolore, au tir, les materiaux et
// particules de ce robot (instances qui lui sont propres). Peu de
// blanc melange (15-30 %) : le bleu et le rouge doivent se lire de
// loin ; bolts laser plus epais pour la meme raison.
// =========================================================

[RequireComponent(typeof(RobotLoadoutState), typeof(RobotEnergy), typeof(WeaponAimController))]
public class WeaponFireController : MonoBehaviour
{
    public enum Kind { None, Laser, Plasma, Rail, Tesla }

    public struct Stats
    {
        public Kind kind;
        public float rate;          // tirs par seconde pour l'ensemble des armes du slot
        public float cost;          // energie par tir (Tesla : par tick de frappe, pour tout le slot)
        public float damage;
        public float speed;         // projectile (m/s)
        public float gravity;       // part de la gravite subie par le projectile
        public float splash;        // rayon de zone (m)
        public int tier;            // niveau de l'arme (N1..N6)
        public BlockDefinition source;  // bloc d'arme qui a tire : suit le tir jusqu'a l'impact (arme du kill)
    }

    [Header("Portee")]
    public float maxRange = 320f;

    [Header("Pilotage externe (IA)")]
    [Tooltip("Vrai : le tir vient de FireHeld (robot ennemi) au lieu du clic gauche")]
    public bool manual;
    public bool FireHeld { get; set; }

    private RobotLoadoutState loadout;
    private RobotEnergy energy;
    private WeaponAimController aim;
    private RobotHealth health;
    private Rigidbody body;
    private readonly RaycastHit[] hits = new RaycastHit[24];

    private float nextShot;
    private int roundRobin;
    private float nextTeslaTick;
    private float lastTeslaFrame = -10f;
    private float lastTeslaStrike = -10f;   // dernier coup porte (arcs plus nourris)
    private float nextTeslaShake;

    // Effets
    private Material beamMaterial, railMaterial, plasmaMaterial, teslaBoltMaterial;
    private readonly List<(LineRenderer line, float until)> beams = new();
    private readonly Stack<LineRenderer> beamPool = new();
    private Transform fxRoot;
    private ParticleSystem sparks, plasmaBurst, plasmaEmbers, plasmaSmoke, teslaArcs, muzzleFlash, muzzleSmoke;
    private Shader hologramShader, dustShader;
    private Material plasmaHaloMaterial, plasmaArcMaterial;
    private static readonly Color PlasmaArcColor = new(0.72f, 0.82f, 1f);
    private ParticleSystem.EmitParams emit;
    private Blockforge.Meta.Team? tintedFor;   // relation deja appliquee aux effets (null : pas encore)
    private Color shotColor = BlockforgeTheme.Hud.TeamAlly;   // couleur d'equipe du dernier tir (explosion plasma)

    private static readonly Regex TierPattern = new(@"_N(\d)", RegexOptions.IgnoreCase);
    private static readonly Color LaserColor = new(0.45f, 0.92f, 1f);
    private static readonly Color RailColor = new(0.85f, 0.95f, 1f);
    private static readonly Color PlasmaColor = new(0.78f, 0.4f, 1f);
    private static readonly Color TeslaColor = new(0.55f, 0.85f, 1f);

    public static Stats Resolve(BlockDefinition def)
    {
        var s = new Stats { kind = Kind.None };
        if (def == null)
            return s;
        string key = ((def.family ?? "") + " " + def.name).ToLowerInvariant();
        var m = TierPattern.Match(def.name ?? "");
        int tier = m.Success ? int.Parse(m.Groups[1].Value) : 1;
        // Reglage "parties nerveuses" : laser 9,5 tirs/s (cout x0,7), rail 1,15 tir/s
        // (cout 24 + 2,5 x niveau), Tesla 10 coups/s a TeslaEnergyPerTick par tick pour le slot.
        // Tesla : 24 x niveau par coup, soit 240/s par niveau et par lame au contact
        // (laser 95/s par niveau pour tout le slot, rail ~207/s) : ~2,5x le laser, la
        // recompense du risque d'aller au contact (avant : 28 x niveau, puis -15 %).
        // splash = portee des arcs au-dela de la boite de la lame (TeslaArcReach).
        if (key.Contains("laser"))
            s = new Stats { kind = Kind.Laser, rate = 9.5f, cost = 2.1f + 0.42f * tier, damage = 10f * tier, speed = 150f, gravity = 0f };
        else if (key.Contains("plasma"))
            s = PlasmaStats(tier);
        else if (key.Contains("rail"))
            s = new Stats { kind = Kind.Rail, rate = 1.15f, cost = 24f + 2.5f * tier, damage = 180f * tier };   // tir de sniper : gros degats
        else if (key.Contains("tesla"))
            s = new Stats { kind = Kind.Tesla, rate = 10f, cost = TeslaEnergyPerTick, damage = 24f * tier, splash = TeslaArcReach, tier = tier };
        s.source = def;
        return s;
    }

    // Plasma par rarete (N1..N6) : cadence 3,3 tirs/s au N1, +20 % par niveau (8,2 au N6).
    // L'energie depensee par seconde est fixee par niveau ((13 + 2 x niveau) x 1,7, soit
    // 25,5/s au N1 -> 42,5/s au N6) : le cout par tir baisse quand la cadence monte.
    // Les degats d'un tir sont le total encaisse par le robot touche, reparti entre les
    // blocs de la zone (RobotHealth.Damage) ; ils montent moins vite que le niveau, car la
    // zone grandit avec lui et touche plus souvent plusieurs robots : N1 60 x 3,3/s ~200/s,
    // N3 ~750/s, N6 252 x 8,2/s ~2070/s (laser : 95/s par niveau, sur un seul bloc ;
    // rail ~207/s par niveau ; Tesla 240/s par niveau et par lame au contact).
    // Vitesse et gravite de la boule inchangees : meme portee, meme anticipation des bots.
    // Buff "tir reussi" : la boule n'est pas evidente a placer (cloche, vol lent), donc
    // chaque impact frappe plus fort (x1,25 sur le coup direct : N1 60 -> 75, N6 252 -> 315),
    // la zone un peu moins (PlasmaSplashShare 0,45 -> 0,41, soit ~+14 %). Cadence, cout
    // et rayon inchanges : on recompense la precision, pas le DPS garanti.
    public const float PlasmaHitBonus = 1.25f;
    public const float PlasmaSplashShare = 0.41f;   // part des degats du tir prise par la zone (au centre)

    public static Stats PlasmaStats(int tier)
    {
        tier = Mathf.Clamp(tier, 1, 6);
        float rate = 3.3f * Mathf.Pow(1.2f, tier - 1);
        float energyPerSecond = (13f + 2f * tier) * 1.7f;
        float damage = 60f * tier * Mathf.Lerp(1f, 0.7f, (tier - 1) / 5f) * PlasmaHitBonus;
        return new Stats
        {
            kind = Kind.Plasma,
            rate = rate,
            cost = energyPerSecond / rate,
            damage = damage,
            speed = 58f + 4f * tier,
            gravity = 0.55f,
            splash = 2.2f + 0.76f * (tier - 1),   // N1 2,2 m -> N6 6 m
            tier = tier,
        };
    }

    private void Awake()
    {
        loadout = GetComponent<RobotLoadoutState>();
        energy = GetComponent<RobotEnergy>();
        aim = GetComponent<WeaponAimController>();
        body = GetComponent<Rigidbody>();
        health = GetComponent<RobotHealth>();
        BuildFX();
    }

    private void OnDestroy()
    {
        if (beamMaterial != null) Destroy(beamMaterial);
        if (teslaBoltMaterial != null) Destroy(teslaBoltMaterial);
        if (railMaterial != null) Destroy(railMaterial);
        if (plasmaMaterial != null) Destroy(plasmaMaterial);
        if (plasmaHaloMaterial != null) Destroy(plasmaHaloMaterial);
        if (plasmaArcMaterial != null) Destroy(plasmaArcMaterial);
        if (fxRoot != null) Destroy(fxRoot.gameObject);
    }

    public Material PlasmaMaterial => plasmaMaterial;

    // =========================================================
    // ENTREES
    // =========================================================

    private void Update()
    {
        UpdateBeams();
        UpdateTeslaBolts();
        if (health == null)
            health = GetComponent<RobotHealth>();
        if (health != null && health.IsDead)
            return;
        UpdateTeslaBlades();   // ouverture / fermeture de l'arc, arcs electriques
        // Compte a rebours ou fin de match (TdmMatch) : aucune arme ne tire
        if (MatchGate.Frozen)
            return;
        // Tesla choisie : frappe automatique au contact (arc ouvert), sans bouton de tir,
        // pour le joueur comme pour les bots
        var selectedSlot = loadout.Selected;
        if (selectedSlot != null && teslaSlotStats.TryGetValue(selectedSlot, out var teslaStats))
        {
            FireTesla(selectedSlot, teslaStats);
            return;
        }
        bool firing;
        if (manual)
        {
            firing = FireHeld;
        }
        else
        {
            var mouse = Mouse.current;
            firing = mouse != null && mouse.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
        }
        if (!firing)
            return;

        var slot = loadout.Selected;
        if (slot == null || slot.kind != LoadoutKind.Weapon)
            return;
        var stats = Resolve(slot.definition);
        if (stats.kind == Kind.None)
            return;
        // Couleur d'equipe (allie bleu / ennemi rouge) avant de creer le moindre effet
        RefreshTeamColors();

        if (stats.kind == Kind.Tesla)
        {
            FireTesla(slot, stats);
            return;
        }

        if (Time.time < nextShot)
            return;
        // Tourelles du slot (armes visees) a tour de role
        var turrets = new List<WeaponAimController.Turret>();
        foreach (var t in aim.Turrets)
        {
            if (slot.blocks.Contains(t.view) && !t.view.destroyed)
                turrets.Add(t);
        }
        if (turrets.Count == 0)
            return;
        if (!energy.TrySpend(stats.cost))
        {
            nextShot = Time.time + 0.1f;   // pas de rafale de refus
            return;
        }
        nextShot = Time.time + 1f / Mathf.Max(0.1f, stats.rate);
        var turret = turrets[roundRobin++ % turrets.Count];

        Vector3 muzzle = turret.Muzzle;
        Vector3 toAim = (aim.AimPoint - muzzle).normalized;
        Vector3 dir = Vector3.Angle(turret.Forward, toAim) < 12f ? toAim : turret.Forward;

        switch (stats.kind)
        {
            case Kind.Laser: FireLaser(muzzle, dir, stats); break;
            case Kind.Rail: FireHitscan(muzzle, dir, stats, railMaterial, RailColor, 0.18f, 0.14f, 28); Recoil(dir, muzzle, 1.1f); break;
            case Kind.Plasma: FirePlasma(muzzle, dir, stats); break;
        }
    }

    // =========================================================
    // TIRS
    // =========================================================

    private void FireHitscan(Vector3 muzzle, Vector3 dir, Stats stats, Material material, Color color, float width, float seconds, int sparkCount)
    {
        Vector3 end = muzzle + dir * maxRange;
        if (WeaponAimController.RaycastIgnoring(body, hits, muzzle, dir, maxRange, out var hit))
        {
            end = hit.point;
            ApplyHit(hit.collider, hit.point, hit.normal, stats, color, sparkCount);
        }
        ShowBeam(muzzle, end, material, width, seconds);
        MuzzleFX(muzzle, dir, color, 1.8f);
    }

    // Bolt laser : capsule etiree dans l'axe du tir, courte trainee, tout droit
    // Depart du coup : flash en bout de canon, etincelles, petite bouffee
    private void MuzzleFX(Vector3 muzzle, Vector3 dir, Color color, float scale)
    {
        Burst(muzzleFlash, muzzle + dir * 0.2f, dir, 2, color, 0.28f * scale, 0.45f * scale, 0f, 0.5f, 0.07f);
        Burst(sparks, muzzle + dir * 0.1f, dir, Mathf.RoundToInt(6 * scale), color, 0.06f, 0.13f, 4f, 9f, 0.14f);
        Burst(muzzleSmoke, muzzle + dir * 0.25f, dir, Mathf.RoundToInt(2 * scale), color, 0.15f * scale, 0.3f * scale, 0.6f, 1.4f, 0.5f);
    }

    private void FireLaser(Vector3 muzzle, Vector3 dir, Stats stats)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "LaserBolt";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = muzzle + dir * 0.4f;
        go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f); // la capsule est en Y
        go.transform.localScale = new Vector3(0.15f, 0.7f, 0.15f);   // epais : la couleur d'equipe se lit de loin
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = beamMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var bolt = go.AddComponent<WeaponProjectile>();
        bolt.Launch(this, body, dir * stats.speed, stats, beamMaterial, trailTime: 0.08f, trailWidth: 0.2f, pulse: false, alignToVelocity: true);
        MuzzleFX(muzzle, dir, LaserColor, 0.8f);
    }

    private void FirePlasma(Vector3 muzzle, Vector3 dir, Stats stats)
    {
        float power = Mathf.Clamp01((stats.tier - 1) / 5f);
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PlasmaBolt";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = muzzle + dir * 0.3f;
        go.transform.localScale = Vector3.one * Mathf.Lerp(0.32f, 0.62f, power);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = plasmaMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.AddComponent<PlasmaBoltFX>().Init(plasmaHaloMaterial, plasmaArcMaterial, power);
        var bolt = go.AddComponent<WeaponProjectile>();
        bolt.Launch(this, body, dir * stats.speed, stats, plasmaMaterial, trailTime: Mathf.Lerp(0.22f, 0.32f, power),
                    trailWidth: Mathf.Lerp(0.3f, 0.6f, power), pulse: true, alignToVelocity: false);
        MuzzleFX(muzzle, dir, PlasmaColor, Mathf.Lerp(1.3f, 2f, power));
    }

    // Frappe des lames, appelee chaque frame tant que la Tesla est le slot choisi (pas de
    // clic) : un tick ne frappe que les ennemis dans la zone d'une lame a l'arc ouvert.
    // Rien sur un allie ni sur le decor, rien dans le vide. Le tick qui touche coute
    // TeslaEnergyPerTick au slot, une fois, quel que soit le nombre de lames.
    // Les arcs electriques sont animes par UpdateTeslaBlades.
    private void FireTesla(RobotLoadoutState.Slot slot, Stats stats)
    {
        // Cadence fixe, independante du framerate. Reprise apres une pause (autre slot,
        // match fige) : premier coup tout de suite, sans rattrapage.
        float interval = 1f / Mathf.Max(0.1f, stats.rate);
        if (Time.time - lastTeslaFrame > 0.25f)
            nextTeslaTick = Mathf.Max(nextTeslaTick, Time.time);
        lastTeslaFrame = Time.time;
        if (Time.time < nextTeslaTick)
            return;
        // Coups dus depuis la frame precedente (frame lente : jusqu'a 3, cumules en un seul)
        int ticks = 0;
        while (Time.time >= nextTeslaTick && ticks < 3)
        {
            nextTeslaTick += interval;
            ticks++;
        }
        if (Time.time >= nextTeslaTick)
            nextTeslaTick = Time.time + interval;
        var strike = stats;
        strike.damage = stats.damage * ticks;
        teslaHits.Clear();
        foreach (var view in slot.blocks)
        {
            if (view.destroyed)
                continue;
            // Arc ferme ou en cours d'ouverture : pas de frappe
            if (!teslaBlades.TryGetValue(view, out var blade) || !blade.IsOpen)
                continue;
            // Volume de frappe : sphere centree sur la boite d'empreinte de la lame, de rayon
            // sa demi-longueur + TeslaArcReach. Avant : 2,2 m fixes autour du centre, soit
            // 0,2 m seulement au-dela de la pointe d'une lame de 4 cases (0 pour la Nova) :
            // il fallait s'encastrer dans la cible pour toucher.
            BladeSphere(view, out Vector3 center, out float radius);
            // Un seul coup par robot (et par cible d'essai) et par lame : le collider le
            // plus proche de la lame. Avant, chaque collider du robot dans la sphere
            // prenait un coup entier : les degats etaient multiplies par le nombre de
            // blocs proches. Le decor (sol, murs) n'est pas frappe.
            teslaBest.Clear();
            int n = Physics.OverlapSphereNonAlloc(center, radius, teslaOverlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var col = teslaOverlap[i];
                if (col == null || col.attachedRigidbody == body)
                    continue;
                Vector3 point = col.bounds.ClosestPoint(center);
                Component owner = col.GetComponentInParent<RobotHealth>();
                if (owner == null)
                    owner = col.GetComponentInParent<ShootableTarget>();
                if (owner == null)
                    continue;   // decor : pas de frappe
                float dist = (point - center).sqrMagnitude;
                if (!teslaBest.TryGetValue(owner, out var best) || dist < best.dist)
                    teslaBest[owner] = (col, point, dist);
            }
            // Seulement ce qui peut etre blesse : robot ennemi vivant, cible d'essai
            foreach (var pair in teslaBest)
            {
                if (pair.Key is RobotHealth r && (r.IsDead || (RobotHealth.CanBeDamagedBy != null && !RobotHealth.CanBeDamagedBy(gameObject, r))))
                    continue;
                var hit = pair.Value;
                teslaHits.Add((view, center, pair.Key, hit.col, hit.point));
            }
        }
        teslaBest.Clear();
        if (teslaHits.Count == 0)
            return;
        // Un seul paiement pour le tick du slot ; pas assez d'energie : aucun coup, les arcs
        // s'eteignent (Refused fait clignoter la jauge du HUD)
        if (!energy.TrySpend(TeslaEnergyPerTick * ticks))
        {
            teslaHits.Clear();
            return;
        }
        lastTeslaStrike = Time.time;
        RefreshTeamColors();   // decharges a la couleur d'equipe
        foreach (var h in teslaHits)
        {
            Vector3 normal = (h.center - h.point).normalized;
            if (h.owner is RobotHealth robot)
            {
                if (TeslaStrike(robot, h.point, normal, strike) > 0f)
                    TeslaDischarge(h.view, h.point, normal);
                else
                    Burst(sparks, h.point, normal, 8, TeslaColor, 0.06f, 0.14f, 3f, 8f, 0.2f);
            }
            else
            {
                ApplyHit(h.col, h.point, normal, strike, TeslaColor, 8);
                TeslaDischarge(h.view, h.point, normal);
            }
        }
        teslaHits.Clear();
    }

    private readonly List<(PlacedBlockView view, Vector3 center, Component owner, Collider col, Vector3 point)> teslaHits = new();

    // Coup de lame sur un robot : la lame traverse. Un bloc detruit avant d'avoir tout
    // encaisse laisse le reste au bloc vivant le plus proche du point d'impact (2 blocs de
    // plus au plus). Sans ce report, le surplus etait perdu sur les petits blocs (cube :
    // 56 PV) et les degats reels de la Tesla plafonnaient bien sous sa valeur affichee.
    private float TeslaStrike(RobotHealth robot, Vector3 point, Vector3 normal, Stats stats)
    {
        if (robot.gameObject == gameObject)
            return 0f;
        float total = 0f;
        float left = stats.damage;
        for (int i = 0; i < 3 && left >= robot.minDamage && !robot.IsDead; i++)
        {
            float before = robot.Hp;
            robot.Damage(point, left, 0f, transform.position, gameObject, stats.source);
            float dealt = before - robot.Hp;
            if (dealt <= 0f)
                break;   // tir allie, aucun bloc a portee
            total += dealt;
            left -= dealt;
        }
        return total;
    }

    // Energie par tick de frappe du slot Tesla, toutes lames confondues (reglage) :
    // AUTONOMIE ~6 s de contact continu depuis la jauge pleine, quel que soit le nombre de
    // lames. Reserve 650, regen 80/s seulement apres 0,35 s sans depense (RobotEnergy) :
    // au contact, un tick paye toutes les 0,1 s, donc aucune regen. 10 ticks/s x 10,8
    // = 108/s -> 650 / 108 = 6,0 s. Ensuite un coup a chaque remontee (~1 toutes les 0,5 s).
    // (Avant : 9,5 par coup et par lame, 2 lames ~3,4 s, 4 lames ~1,7 s.)
    // A comparer : laser 25 a 44/s, plasma 25 a 42/s, rail ~31 a 34/s en tir continu.
    public const float TeslaEnergyPerTick = 10.8f;

    // =========================================================
    // ARC DES LAMES TESLA (ouverture / fermeture, arcs electriques)
    // Le modele (Tesla_N1_Slicer / N2_Ripper / N3_Nova) n'a ni os ni
    // animation : l'arc est anime par le code. Bras terminaux
    // (upper/lower_terminal_bar, avec colliers, embouts et vis) qui
    // pivotent a leur pied ; bandes d'energie (energy_blade_N et
    // plasma_highlight_N) qui se replient dans le dos de la lame.
    // Ferme : bras rabattus vers le centre jusqu'a ce que leurs
    // pointes se touchent presque au-dessus de la lame (angle calcule
    // par modele, borne a TeslaMaxFoldDeg), bandes repliees.
    // Ouvert : pose d'origine du modele (garage, vignettes inchanges :
    // seuls les robots de match portent ce composant).
    // =========================================================

    public const float TeslaOpenSeconds = 0.4f;    // ouverture de l'arc
    public const float TeslaCloseSeconds = 0.3f;   // fermeture
    private const float TeslaMaxFoldDeg = 100f;    // bras rabattus au plus de (arc ferme)
    private const float TeslaClosedTipGap = 0.3f;  // arc ferme : pointe des bras a 0,3 m du milieu de la lame

    // Piece animee : pivot et axe dans le repere de son parent
    private sealed class TeslaPart
    {
        public Transform t;
        public Vector3 basePos, baseScale, pivot, axis;
        public Quaternion baseRot;
        public float sign = 1f;
        public float fold;                 // bras : angle de fermeture (degres)

        public void Fold(float deg)
        {
            var q = Quaternion.AngleAxis(deg * sign, axis);
            t.localPosition = pivot + q * (basePos - pivot);
            t.localRotation = q * baseRot;
        }

        public void Scale(float k)
        {
            t.localPosition = pivot + (basePos - pivot) * k;
            t.localScale = baseScale * k;
        }
    }

    private sealed class TeslaBlade
    {
        public PlacedBlockView view;
        public RobotLoadoutState.Slot slot;
        public readonly List<TeslaPart> arms = new();
        public readonly List<TeslaPart> bands = new();
        public float open;                 // 0 ferme -> 1 ouvert
        public float applied = -1f;        // dernier etat pose sur les pieces
        public bool IsOpen => open >= 1f;
    }

    private readonly Dictionary<PlacedBlockView, TeslaBlade> teslaBlades = new();
    private readonly Dictionary<RobotLoadoutState.Slot, Stats> teslaSlotStats = new();
    private bool teslaBladesBuilt;

    private void BuildTeslaBlades()
    {
        teslaBladesBuilt = true;
        foreach (var slot in loadout.slots)
        {
            if (slot.kind != LoadoutKind.Weapon)
                continue;
            var stats = Resolve(slot.definition);
            if (stats.kind != Kind.Tesla)
                continue;
            teslaSlotStats[slot] = stats;
            foreach (var view in slot.blocks)
            {
                if (view == null || teslaBlades.ContainsKey(view))
                    continue;
                var blade = new TeslaBlade { view = view, slot = slot };
                RigTeslaBlade(blade);
                teslaBlades[view] = blade;
            }
        }
    }

    // Repere les pieces du modele (noms du FBX) dans le repere de l'instance du prefab
    private static void RigTeslaBlade(TeslaBlade blade)
    {
        var wrapper = BlockColliders.Visual(blade.view.transform);
        if (wrapper == null || wrapper.childCount == 0)
            return;   // visuel de remplacement (primitive) : l'arc s'ouvre sans animation
        var inst = wrapper.GetChild(0);
        var parts = new List<(Transform t, Bounds b)>();
        Bounds spine = default;
        bool hasSpine = false;
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            if (!LocalBounds(r, inst, out var b))
                continue;
            parts.Add((r.transform, b));
            if (r.name.Contains("curved_spine"))
            {
                spine = b;
                hasSpine = true;
            }
        }
        // Bandes d'energie : repliees vers le dessus du dos de la lame
        Vector3 bandPivot = hasSpine ? new Vector3(spine.center.x, spine.max.y, spine.center.z) : Vector3.zero;
        foreach (var (t, b) in parts)
        {
            if (t.name.Contains("energy_blade") || t.name.Contains("plasma_highlight"))
                blade.bands.Add(MakeTeslaPart(t, inst, bandPivot, Vector3.right));
        }
        // Bras terminaux : un de chaque cote (z > 0 / z < 0), pivot au pied du bras
        for (int side = -1; side <= 1; side += 2)
        {
            Bounds bar = default, cap = default;
            bool hasBar = false, hasCap = false;
            foreach (var (t, b) in parts)
            {
                if (Mathf.Sign(b.center.z) != side)
                    continue;
                if (t.name.Contains("terminal_bar")) { bar = b; hasBar = true; }
                else if (t.name.StartsWith("terminal_end_cap")) { cap = b; hasCap = true; }
            }
            if (!hasBar)
                continue;
            float innerZ = side > 0 ? bar.min.z : bar.max.z;
            var pivot = new Vector3(bar.center.x, bar.min.y + 0.15f, innerZ + side * 0.15f);
            Vector3 tip = hasCap ? cap.center : bar.center;
            // Sens : la pointe doit se rapprocher du centre de la lame (|z| baisse)
            Vector3 probe = pivot + Quaternion.AngleAxis(10f, Vector3.right) * (tip - pivot);
            float sign = Mathf.Abs(probe.z) < Mathf.Abs(tip.z) ? 1f : -1f;
            // Angle : la pointe (dans le plan Y-Z du bras) finit a TeslaClosedTipGap du milieu
            Vector3 arm = tip - pivot;
            float reach = Mathf.Max(0.01f, new Vector2(arm.y, arm.z).magnitude);
            float openDeg = Mathf.Atan2(arm.y, arm.z * side) * Mathf.Rad2Deg;   // depuis l'exterieur
            float closedDeg = Mathf.Acos(Mathf.Clamp((TeslaClosedTipGap - Mathf.Abs(pivot.z)) / reach, -1f, 1f)) * Mathf.Rad2Deg;
            float fold = Mathf.Clamp(closedDeg - openDeg, 20f, TeslaMaxFoldDeg);
            foreach (var (t, b) in parts)
            {
                if (Mathf.Sign(b.center.z) != side)
                    continue;
                string n = t.name;
                if (n.Contains("terminal_bar") || n.StartsWith("terminal_end_cap") || n.StartsWith("terminal_screw")
                    || n.StartsWith("arc_terminal_collar"))
                {
                    var part = MakeTeslaPart(t, inst, pivot, Vector3.right);
                    part.sign = sign;
                    part.fold = fold;
                    blade.arms.Add(part);
                }
            }
        }
    }

    // Pivot et axe donnes dans le repere de l'instance, ramenes dans celui du parent de t
    private static TeslaPart MakeTeslaPart(Transform t, Transform inst, Vector3 pivotInst, Vector3 axisInst)
    {
        var parent = t.parent;
        return new TeslaPart
        {
            t = t,
            basePos = t.localPosition,
            baseRot = t.localRotation,
            baseScale = t.localScale,
            pivot = parent.InverseTransformPoint(inst.TransformPoint(pivotInst)),
            axis = parent.InverseTransformVector(inst.TransformVector(axisInst)).normalized,
        };
    }

    // Boite du maillage d'un renderer dans le repere 'frame' (8 coins)
    private static bool LocalBounds(Renderer r, Transform frame, out Bounds bounds)
    {
        bounds = default;
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return false;
        var mb = mf.sharedMesh.bounds;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x,
                                     (i & 2) == 0 ? mb.min.y : mb.max.y,
                                     (i & 4) == 0 ? mb.min.z : mb.max.z);
            var p = frame.InverseTransformPoint(r.transform.TransformPoint(corner));
            if (i == 0) bounds = new Bounds(p, Vector3.zero);
            else bounds.Encapsulate(p);
        }
        return true;
    }

    // Chaque frame (robot vivant) : l'arc suit le slot choisi, arcs electriques si ouvert
    // et assez d'energie pour un coup (sinon eteints, l'arc reste ouvert)
    private void UpdateTeslaBlades()
    {
        if (!teslaBladesBuilt)
            BuildTeslaBlades();
        if (teslaBlades.Count == 0)
            return;
        var selected = loadout.Selected;
        bool charged = energy.CanSpend(TeslaEnergyPerTick);
        bool firing = Time.time - lastTeslaStrike < 0.2f;
        bool anyOpen = false;
        float dt = Time.deltaTime;
        foreach (var blade in teslaBlades.Values)
        {
            if (blade.view == null || blade.view.destroyed)
                continue;
            bool want = blade.slot == selected;
            blade.open = Mathf.MoveTowards(blade.open, want ? 1f : 0f, dt / (want ? TeslaOpenSeconds : TeslaCloseSeconds));
            if (blade.open != blade.applied)
            {
                blade.applied = blade.open;
                // Bras d'abord (0 -> 60 %), puis les bandes d'energie se deploient (45 -> 100 %)
                float arms = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blade.open / 0.6f));
                float bands = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((blade.open - 0.45f) / 0.55f));
                foreach (var a in blade.arms)
                    a.Fold(a.fold * (1f - arms));
                foreach (var b in blade.bands)
                    b.Scale(Mathf.Lerp(0.001f, 1f, bands));
            }
            if (!blade.IsOpen || !charged)
                continue;
            anyOpen = true;
            // Arcs electriques animes, plus nourris quand on frappe
            if (Random.value < (firing ? 0.7f : 0.35f))
            {
                BladeSphere(blade.view, out Vector3 center, out float radius);
                float spread = Mathf.Max(0.3f, radius - TeslaArcReach) * 0.5f;
                Burst(teslaArcs, center + Random.insideUnitSphere * spread, Random.onUnitSphere, firing ? 3 : 2,
                      TeslaColor, 0.06f, 0.16f, 1.5f, 4f, 0.12f);
            }
        }
        if (anyOpen)
            RefreshTeamColors();   // arcs a la couleur d'equipe meme sans tirer
    }

    // Decharge d'un coup qui blesse : eclair brise de la lame au point touche, flash,
    // gerbe d'etincelles et d'arcs, legere secousse (au plus 5 par seconde). Eclairs
    // pris dans un pool de LineRenderer : rien n'est instancie par coup.
    private void TeslaDischarge(PlacedBlockView view, Vector3 point, Vector3 normal)
    {
        var box = view.GetComponentInChildren<BoxCollider>(true);
        Vector3 from = view.transform.position;
        if (box != null && box.enabled)
        {
            from = box.ClosestPoint(point);
            if ((from - point).sqrMagnitude < 0.04f)
                from = box.bounds.center;   // pointe deja dans la cible : l'eclair part du coeur de la lame
        }
        ShowTeslaBolt(from, point);
        Burst(muzzleFlash, point + normal * 0.1f, normal, 2, TeslaColor, 0.45f, 0.8f, 0f, 0.4f, 0.08f);
        Burst(sparks, point, normal, 14, TeslaColor, 0.05f, 0.13f, 6f, 15f, 0.18f);
        Burst(teslaArcs, point, normal, 4, TeslaColor, 0.08f, 0.2f, 2f, 6f, 0.12f);
        if (Time.time >= nextTeslaShake)
        {
            nextTeslaShake = Time.time + 0.2f;
            RobotFollowCamera.Shake(point, 0.07f, 14f);
        }
    }

    private const int TeslaBoltPoints = 8;
    private readonly List<(LineRenderer line, Vector3 from, Vector3 to, float until)> teslaBolts = new();
    private readonly Stack<LineRenderer> teslaBoltPool = new();

    private void ShowTeslaBolt(Vector3 from, Vector3 to)
    {
        if (teslaBoltMaterial == null)
            return;
        LineRenderer line;
        if (teslaBoltPool.Count > 0)
            line = teslaBoltPool.Pop();
        else
        {
            line = NewLine();
            line.name = "TeslaBolt";
            line.positionCount = TeslaBoltPoints;
            line.numCapVertices = 0;
        }
        line.gameObject.SetActive(true);
        line.sharedMaterial = teslaBoltMaterial;
        line.startWidth = 0.11f;
        line.endWidth = 0.05f;
        JitterBolt(line, from, to);
        teslaBolts.Add((line, from, to, Time.time + Random.Range(0.07f, 0.11f)));
    }

    // Trace brise : points intermediaires decales au hasard, plus au milieu
    private static void JitterBolt(LineRenderer line, Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        Vector3 dir = len > 1e-4f ? d / len : Vector3.forward;
        Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 up = Vector3.Cross(side, dir);
        float amp = Mathf.Clamp(len * 0.18f, 0.05f, 0.4f);
        for (int i = 0; i < TeslaBoltPoints; i++)
        {
            float k = i / (float)(TeslaBoltPoints - 1);
            float env = Mathf.Sin(k * Mathf.PI);
            Vector2 o = Random.insideUnitCircle * (amp * env);
            line.SetPosition(i, from + d * k + side * o.x + up * o.y);
        }
    }

    // Eclairs vivants : crepitent (nouveau trace chaque frame), puis retournent au pool
    private void UpdateTeslaBolts()
    {
        for (int i = teslaBolts.Count - 1; i >= 0; i--)
        {
            var b = teslaBolts[i];
            if (Time.time < b.until)
            {
                JitterBolt(b.line, b.from, b.to);
                continue;
            }
            b.line.gameObject.SetActive(false);
            teslaBoltPool.Push(b.line);
            teslaBolts.RemoveAt(i);
        }
    }

    // Portee des arcs au-dela de la boite d'empreinte d'une lame (m)
    public const float TeslaArcReach = 0.8f;

    // Sphere de frappe d'une lame : centre de sa boite d'empreinte (BoxCollider pose par
    // RobotAssembler), rayon = demi-plus-grande dimension + TeslaArcReach
    private static void BladeSphere(PlacedBlockView view, out Vector3 center, out float radius)
    {
        var box = view.GetComponentInChildren<BoxCollider>(true);
        if (box == null)
        {
            center = view.transform.position;
            radius = 2.2f;
            return;
        }
        var t = box.transform;
        center = t.TransformPoint(box.center);
        Vector3 s = t.lossyScale;
        Vector3 size = new(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y), Mathf.Abs(box.size.z * s.z));
        radius = 0.5f * Mathf.Max(size.x, Mathf.Max(size.y, size.z)) + TeslaArcReach;
    }

    // IA (BotBrain) : ecart (m) entre la sphere de frappe des lames vivantes du slot et le
    // bloc vivant le plus proche de la cible (centre de case, demi-bloc retire) ; <= 0 :
    // une lame touche. Infini sans lame ni cible. La distance entre les racines des robots
    // ne convient pas : deux longs robots lames au contact ont leurs centres a plus de 10 m.
    public static float TeslaGap(RobotLoadoutState.Slot slot, RobotHealth target)
    {
        float gap = float.PositiveInfinity;
        if (slot == null || target == null || target.IsDead)
            return gap;
        var tt = target.transform;
        foreach (var view in slot.blocks)
        {
            if (view == null || view.destroyed)
                continue;
            BladeSphere(view, out Vector3 center, out float radius);
            float best = float.PositiveInfinity;
            foreach (var b in target.blocks)
            {
                if (b.Destroyed)
                    continue;
                float d = (tt.TransformPoint(b.localCenter) - center).sqrMagnitude;
                if (d < best)
                    best = d;
            }
            if (!float.IsPositiveInfinity(best))
                gap = Mathf.Min(gap, Mathf.Sqrt(best) - 0.5f - radius);
        }
        return gap;
    }

    // Tampons de la Tesla (sans allocation a chaque coup)
    private static readonly Collider[] teslaOverlap = new Collider[256];
    private readonly Dictionary<Component, (Collider col, Vector3 point, float dist)> teslaBest = new();

    private void Recoil(Vector3 dir, Vector3 at, float strength)
    {
        if (body != null && !body.isKinematic)
            body.AddForceAtPosition(-dir * (body.mass * strength), at, ForceMode.Impulse);
    }

    // Impact : cible touchee (ShootableTarget : detruite puis reapparait), robot
    // touche (RobotHealth : degats au bloc), zone eventuelle, etincelles
    public void ApplyHit(Collider collider, Vector3 point, Vector3 normal, Stats stats, Color color, int sparkCount)
    {
        ShootableTarget directTarget = null;
        RobotHealth directRobot = null;
        if (collider != null)
        {
            directTarget = collider.GetComponentInParent<ShootableTarget>();
            if (directTarget != null)
                directTarget.Hit(stats.damage);
            directRobot = collider.GetComponentInParent<RobotHealth>();
            if (directRobot != null && directRobot.gameObject != gameObject)
                directRobot.Damage(point, stats.damage, stats.kind == Kind.Plasma ? stats.splash : 0f, transform.position, gameObject, stats.source);
        }
        if (stats.splash > 0f && stats.kind == Kind.Plasma)
            PlasmaBlast(collider, point, normal, stats, directTarget, directRobot);
        else
            Burst(sparks, point, normal, sparkCount, color, 0.06f, 0.14f, 3f, 8f, 0.2f);
    }

    // Zone du plasma : degats degressifs (41 % au centre -> ~10 % au bord) a ce qui n'a
    // pas pris le coup direct, souffle sur les corps rigides, effets d'explosion
    private void PlasmaBlast(Collider hitCollider, Vector3 point, Vector3 normal, Stats stats, ShootableTarget directTarget, RobotHealth directRobot)
    {
        float radius = stats.splash;
        float power = Mathf.Clamp01((stats.tier - 1) / 5f);
        var hitRobots = new HashSet<RobotHealth>();
        var hitTargets = new HashSet<ShootableTarget>();
        var pushed = new HashSet<Rigidbody>();
        var arcTargets = new List<Vector3>();
        foreach (var col in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Collide))
        {
            Vector3 closest = col.bounds.ClosestPoint(point);   // (ClosestPoint refuse les MeshCollider concaves)
            float falloff = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(Vector3.Distance(closest, point) / radius));

            var target = col.GetComponentInParent<ShootableTarget>();
            if (target != null && hitTargets.Add(target))
            {
                if (target != directTarget)
                    target.Hit(stats.damage * PlasmaSplashShare * falloff);
                arcTargets.Add(closest);
            }

            var robot = col.GetComponentInParent<RobotHealth>();
            if (robot != null && robot.gameObject != gameObject && hitRobots.Add(robot))
            {
                if (robot != directRobot)
                    robot.Damage(point, stats.damage * PlasmaSplashShare * falloff, radius, transform.position, gameObject, stats.source);
                arcTargets.Add(closest);
            }

            // Souffle : robots ennemis, debris detaches, caisses (jamais le tireur)
            var rb = col.attachedRigidbody;
            if (rb != null && rb != body && !rb.isKinematic && pushed.Add(rb))
            {
                // Un robot encaisse une petite poussee (sinon il serait jongle a 8 tirs/s),
                // un debris ou une caisse est projete. Poussee par tir reduite avec la
                // hausse de cadence et de degats : la poussee par seconde reste proche de
                // l'ancien reglage (~1,2/s au N1, ~4,5/s au N6)
                var pushedHealth = rb.GetComponent<RobotHealth>();
                bool isRobot = pushedHealth != null;
                // Pas de tir allie en match : un allie n'est pas souffle non plus
                if (isRobot && RobotHealth.CanBeDamagedBy != null && !RobotHealth.CanBeDamagedBy(gameObject, pushedHealth))
                    continue;
                // (degats ramenes au reglage d'avant le buff : le souffle ne change pas)
                float kick = isRobot ? 0.32f + 0.04f * (stats.damage / PlasmaHitBonus) / 45f : 3f;
                rb.AddExplosionForce(rb.mass * kick, point, radius * 1.3f, 0.3f, ForceMode.Impulse);
            }
        }

        // Effets : la zone est montree au rayon exact ; brulure seulement sur un decor fixe
        bool scorch = hitCollider != null && hitCollider.attachedRigidbody == null;
        PlasmaExplosion.Spawn(point, normal, radius, power, shotColor, hologramShader, dustShader, arcTargets, scorch);
        float k = radius / 3f;
        float dense = Mathf.Lerp(1f, 2.2f, power);
        Burst(plasmaBurst, point + normal * 0.2f, normal, Mathf.RoundToInt(22 * dense), PlasmaColor, 0.3f * k, 0.8f * k, 2f * k, 7f * k, 0.4f);
        Burst(plasmaEmbers, point + normal * 0.1f, normal, Mathf.RoundToInt(30 * dense), Color.white, 0.05f, 0.12f, 6f * k, 14f * k, 0.6f);
        // Etincelles electriques : tres vives, tres courtes
        Burst(sparks, point + normal * 0.05f, normal, Mathf.RoundToInt(18 * dense), Color.white, 0.06f, 0.14f, 9f * k, 22f * k, 0.16f);
        Burst(plasmaSmoke, point + normal * 0.4f, normal, Mathf.RoundToInt(8 * dense), PlasmaColor, 0.8f * k, 1.6f * k, 0.6f, 1.8f, 1.4f);
        RobotFollowCamera.Shake(point, Mathf.Lerp(0.18f, 0.45f, power), 20f + 6f * radius);
    }

    private static bool TryBounds(Transform t, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    // =========================================================
    // EFFETS
    // =========================================================

    private void BuildFX()
    {
        fxRoot = new GameObject("WeaponFX").transform;
        var holo = Shader.Find("Blockforge/Hologram");
        hologramShader = holo;
        if (holo != null)
        {
            beamMaterial = BeamMaterial(holo, "LaserBeam", LaserColor, 3.2f);   // 2,6 avant : bleu et rouge satures sont moins lumineux que le cyan d'origine
            teslaBoltMaterial = BeamMaterial(holo, "TeslaBolt", TeslaColor, 4.5f);
            teslaBoltMaterial.SetFloat("_Flicker", 0.45f);
            railMaterial = BeamMaterial(holo, "RailBeam", RailColor, 3.2f);
            plasmaMaterial = BeamMaterial(holo, "PlasmaBolt", Color.Lerp(PlasmaColor, Color.white, 0.35f), 3.6f);
            plasmaHaloMaterial = PlasmaExplosion.Holo(holo, "PlasmaHalo", PlasmaColor, 2.6f, 0f);
            plasmaHaloMaterial.SetFloat("_FillAlpha", 0.18f);
            plasmaHaloMaterial.SetFloat("_RimPower", 1.4f);
            plasmaHaloMaterial.SetFloat("_RimStrength", 1.8f);
            plasmaArcMaterial = PlasmaExplosion.Holo(holo, "PlasmaBoltArc", PlasmaArcColor, 5f, 2f);
            plasmaArcMaterial.SetFloat("_Flicker", 0.35f);
        }
        var dust = Shader.Find("Blockforge/DustParticle");
        dustShader = dust;
        if (dust != null)
        {
            // Plafonds releves avec la cadence (plasma N6 ~8 explosions/s, Tesla 10 coups/s) :
            // au-dela, Emit ignore les particules en trop (braises et fumee coupees)
            sparks = BuildParticles(dust, "Sparks", 520, stretch: true, new Color(0.8f, 0.95f, 1f));
            plasmaBurst = BuildParticles(dust, "PlasmaBurst", 300, stretch: false, PlasmaColor);
            plasmaEmbers = BuildParticles(dust, "PlasmaEmbers", 560, stretch: true, new Color(1f, 0.75f, 1f));
            plasmaSmoke = BuildParticles(dust, "PlasmaSmoke", 260, stretch: false, new Color(0.28f, 0.22f, 0.34f, 0.45f), additive: false);
            teslaArcs = BuildParticles(dust, "TeslaArcs", 300, stretch: true, TeslaColor);
            muzzleFlash = BuildParticles(dust, "MuzzleFlash", 80, stretch: false, new Color(1f, 0.93f, 0.8f));
            muzzleSmoke = BuildParticles(dust, "MuzzleSmoke", 120, stretch: false, new Color(0.5f, 0.5f, 0.52f, 0.3f), additive: false);
        }
    }

    // Couleur des tirs selon la relation au joueur local (TeamRelation) : allie (lui
    // compris) -> bleu, ennemi -> rouge. Appele a chaque tir : le participant du match
    // (TdmCombatant) et le pilotage (manual) sont poses apres Awake, la relation n'est
    // donc connue qu'au premier tir. Ne recolore que si la relation change. Materiaux et
    // systemes de particules sont des instances propres a ce robot : les autres tireurs
    // gardent leur couleur. Intensites inchangees (le shader hologramme multiplie _Color
    // par _Intensity, HDR) ; rail, coeur du plasma, etincelles et flash ne prennent qu'un
    // peu de blanc (15-30 %, avant 35-60 % : les tirs viraient au blanc) : la couleur
    // d'equipe domine partout.
    private void RefreshTeamColors()
    {
        var relation = TeamRelation.Of(gameObject);
        if (tintedFor == relation)
            return;
        tintedFor = relation;
        Color c = TeamRelation.ShotColor(relation);
        shotColor = c;
        SetColor(beamMaterial, c);
        SetColor(railMaterial, Color.Lerp(c, Color.white, 0.2f));
        SetColor(plasmaMaterial, Color.Lerp(c, Color.white, 0.15f));
        SetColor(plasmaHaloMaterial, c);
        SetColor(plasmaArcMaterial, Color.Lerp(c, Color.white, 0.2f));
        SetTint(sparks, Color.Lerp(c, Color.white, 0.25f));
        SetTint(plasmaBurst, c);
        SetTint(plasmaEmbers, Color.Lerp(c, Color.white, 0.2f));
        SetTint(teslaArcs, c);
        SetColor(teslaBoltMaterial, Color.Lerp(c, Color.white, 0.15f));
        SetTint(muzzleFlash, Color.Lerp(c, Color.white, 0.3f));
        var smoke = Color.Lerp(new Color(0.26f, 0.26f, 0.3f), c, 0.3f);
        smoke.a = 0.45f;   // fumee alpha (non additive) : seulement une nuance d'equipe
        SetTint(plasmaSmoke, smoke);
    }

    private static void SetColor(Material material, Color color)
    {
        if (material != null)
            material.SetColor("_Color", color);
    }

    // Teinte d'un systeme de particules (DustParticle : _Tint du materiau, propre au systeme)
    private static void SetTint(ParticleSystem ps, Color tint)
    {
        if (ps == null)
            return;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r != null && r.sharedMaterial != null)
            r.sharedMaterial.SetColor("_Tint", tint);
    }

    // Couleur pleine additive (mode aretes du shader hologramme, devoilee en entier)
    private static Material BeamMaterial(Shader shader, string name, Color color, float intensity)
    {
        var m = new Material(shader) { name = name + " (instance)" };
        m.SetFloat("_Beam", 2f);
        m.SetColor("_Color", color);
        m.SetFloat("_Intensity", intensity);
        m.SetFloat("_Reveal", 1f);
        m.SetFloat("_Flicker", 0.08f);
        m.SetFloat("_LineDensity", 0.5f);
        m.SetFloat("_LineStrength", 0f);
        m.SetVector("_HoloCenter", Vector4.zero);
        m.SetVector("_HoloUp", new Vector4(0f, 1f, 0f, 0f));
        m.SetFloat("_HoloHalfHeight", 100000f);
        return m;
    }

    private ParticleSystem BuildParticles(Shader shader, string name, int max, bool stretch, Color tint, bool additive = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(fxRoot, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 0.1f;
        main.gravityModifier = stretch ? 0.6f : 0.1f;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = !stretch;
        limit.dampen = 0.5f;
        limit.limit = 1f;

        var mat = new Material(shader) { name = name + " (instance)" };
        mat.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
        mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        mat.SetFloat("_Additive", additive ? 1f : 0f);
        mat.SetFloat("_Softness", stretch ? 0.9f : 0.8f);
        mat.SetFloat("_FadeIn", 0.05f);
        mat.SetFloat("_FadeOut", 0.3f);
        mat.SetColor("_Tint", tint);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = stretch ? 0.05f : 0f;
        r.lengthScale = stretch ? 1.5f : 1f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent,
        });
        ps.Play();
        return ps;
    }

    // Bouffee de particules autour d'une direction (la teinte est celle du systeme ;
    // 'color' est gardee pour les appels, sans effet par particule : voir le shader)
    public void Burst(ParticleSystem ps, Vector3 at, Vector3 dir, int count, Color color, float sizeMin, float sizeMax,
                      float speedMin, float speedMax, float life)
    {
        if (ps == null)
            return;
        for (int i = 0; i < count; i++)
        {
            Vector3 v = (dir + Random.insideUnitSphere * 0.9f).normalized * Random.Range(speedMin, speedMax);
            emit.position = at;
            emit.velocity = v;
            emit.startSize = Random.Range(sizeMin, sizeMax);
            emit.startLifetime = life * Random.Range(0.7f, 1.3f);
            emit.rotation = Random.Range(0f, 360f);
            ps.Emit(emit, 1);
        }
    }

    public void ShowBeam(Vector3 from, Vector3 to, Material material, float width, float seconds)
    {
        if (material == null)
            return;
        LineRenderer line = beamPool.Count > 0 ? beamPool.Pop() : NewLine();
        line.gameObject.SetActive(true);
        line.sharedMaterial = material;
        line.startWidth = width;
        line.endWidth = width * 0.6f;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        beams.Add((line, Time.time + seconds));
    }

    private LineRenderer NewLine()
    {
        var go = new GameObject("Beam");
        go.transform.SetParent(fxRoot, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void UpdateBeams()
    {
        for (int i = beams.Count - 1; i >= 0; i--)
        {
            if (Time.time < beams[i].until)
                continue;
            beams[i].line.gameObject.SetActive(false);
            beamPool.Push(beams[i].line);
            beams.RemoveAt(i);
        }
    }
}
