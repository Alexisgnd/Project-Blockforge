using System.Collections.Generic;
using UnityEngine;

// =========================================================
// APPARITION DU ROBOT DANS LA SCENE DE TEST
// Au Start (synchrone, sans animation) : assemble le blueprint
// en memoire (RobotSession, rempli par le garage avant la
// touche P) ou, si la scene est lancee directement, un robot
// par defaut (12 cubes, 4 roues Scout, 2 lasers) ; le pose sur le
// SpawnPoint (un peu au-dessus : les suspensions se posent),
// lui ajoute la physique (RobotLocomotion), l'animation des
// blocs de mouvement (RobotPartAnimator), le pilotage
// (RobotTestDriver), les slots d'armes (RobotLoadoutState),
// l'energie (RobotEnergy), la visee (WeaponAimController), le tir
// (WeaponFireController), la poussiere au sol (RobotGroundFX), le
// redressement (RobotRecovery, touche R), le HUD de match
// (RobotTestHUD : vie, reticule + jauge d'energie, etat du robot,
// heatmap, minimap) et branche la camera. Sante : RobotHealth
// (detruit -> reapparait au spawn apres respawnSeconds).
// Au Start, la map recoit ses cibles destructibles
// (ShootableTarget) et, sur le terrain d'essai, la zone de combat
// du Combat Yard (CombatArena, robot ennemi).
// L'apparition est jouee par RobotSpawnSequence : assemblage
// holographique en levitation, puis chute et controles.
// Le robot passe sur le calque RobotLayer : la minimap le
// remplace par son marqueur au lieu de le photographier.
// F5 : passe au robot suivant (le robot du garage, puis des
// robots de demonstration : chenilles, pattes, survol,
// helico avec propulseur et gouverne).
// =========================================================

public class RobotTestSpawner : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Transform spawnPoint;
    public RobotFollowCamera followCamera;
    [Tooltip("Catalogue des blocs (Data/Blocks) : le blueprint reference les blocs par nom d'asset")]
    public BlockDefinition[] blocks;

    [Header("Reglages")]
    [Tooltip("Taille monde d'une case, independante du garage (qui mesure la sienne sur les lignes du vaisseau, " +
             "voir le log [GarageBuild] Grille mesuree). 1 m = l'echelle des blocs Blender et du Mothership.")]
    public float cellSize = 1f;

    public const int RobotLayer = 29;

    public GameObject Robot { get; private set; }

    [Tooltip("Delai de reapparition du robot joueur detruit (s)")]
    public float respawnSeconds = 4f;

    private readonly Dictionary<string, BlockDefinition> defs = new();
    private GameObject hudHost;
    private int demoIndex;   // 0 = robot du garage (ou robot par defaut), 1.. = robots de demonstration
    private CombatArena arena;
    private Coroutine respawnRoutine;

    private void Start()
    {
        if (blocks != null)
        {
            foreach (var def in blocks)
            {
                if (def != null)
                    defs[def.name] = def;
            }
        }

        var blueprint = RobotSession.Blueprint;
        bool isDefault = blueprint == null || blueprint.blocks == null || blueprint.blocks.Count == 0;
        Spawn(isDefault ? CreateDefaultTestBlueprint() : blueprint, isDefault);
        PrepareMap();
    }

    // Cibles destructibles et zone de combat (terrain d'essai uniquement)
    private void PrepareMap()
    {
        GameObject map = null;
        foreach (var name in new[] { "Map_TestGround", "Map_Canyon", "Map_Arena" })
        {
            map = GameObject.Find(name);
            if (map != null)
                break;
        }
        if (map == null)
            return;
        int targets = ShootableTarget.AttachAll(map.transform);
        if (map.name == "Map_TestGround")
        {
            arena = new GameObject("CombatArena").AddComponent<CombatArena>();
            arena.Player = Robot;
            arena.Setup(defs, cellSize, map.transform);
        }
        Debug.Log($"[RobotTest] {targets} cible(s) destructible(s)" + (arena != null ? ", zone de combat prete (Combat Yard)." : "."));
    }

    // Robot detruit : reapparait au spawn apres respawnSeconds
    private void OnPlayerDestroyed()
    {
        if (respawnRoutine != null)
            return;
        respawnRoutine = StartCoroutine(RespawnPlayer());
    }

    private System.Collections.IEnumerator RespawnPlayer()
    {
        yield return new WaitForSeconds(respawnSeconds);
        respawnRoutine = null;
        if (demoIndex == 0)
        {
            var bp = RobotSession.Blueprint;
            bool isDefault = bp == null || bp.blocks == null || bp.blocks.Count == 0;
            Spawn(isDefault ? CreateDefaultTestBlueprint() : bp, isDefault);
        }
        else
        {
            Spawn(DemoBlueprints()[demoIndex - 1], false);
        }
    }

    // F5 : robot suivant (robot du garage, puis les demonstrations de chaque famille de mouvement)
    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.f5Key.wasPressedThisFrame)
            return;
        var demos = DemoBlueprints();
        demoIndex = (demoIndex + 1) % (demos.Count + 1);
        if (demoIndex == 0)
        {
            var bp = RobotSession.Blueprint;
            bool isDefault = bp == null || bp.blocks == null || bp.blocks.Count == 0;
            Spawn(isDefault ? CreateDefaultTestBlueprint() : bp, isDefault);
        }
        else
        {
            Spawn(demos[demoIndex - 1], false);
        }
    }

    private void Spawn(RobotBlueprint blueprint, bool isDefault)
    {
        if (Robot != null)
            Destroy(Robot);
        if (hudHost != null)
            Destroy(hudHost);

        Robot = RobotAssembler.Build(blueprint, defs, cellSize, null, out int placed, out int weightKg);

        Vector3 position = (spawnPoint != null ? spawnPoint.position : Vector3.zero) + Vector3.up * 0.4f;
        Quaternion rotation = spawnPoint != null ? Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f) : Quaternion.identity;
        Robot.transform.SetPositionAndRotation(position, rotation);
        SetLayer(Robot.transform, RobotLayer);
        CheckGround(position);

        // Physique (masse = poids / 100), animations, pilotage
        Robot.AddComponent<Rigidbody>();
        var locomotion = Robot.AddComponent<RobotLocomotion>();
        locomotion.Setup(cellSize, weightKg);
        Robot.AddComponent<RobotPartAnimator>().Build(locomotion);
        var driver = Robot.AddComponent<RobotTestDriver>();
        Robot.AddComponent<RobotLoadoutState>();   // armes et modules : slots 1 / 2 / 3
        Robot.AddComponent<RobotEnergy>();         // energie des armes (jauge du reticule)
        var health = Robot.AddComponent<RobotHealth>();   // degats bloc par bloc, heatmap du HUD
        Robot.AddComponent<RobotDamageFX>();             // arcs, fumee, explosions, morceaux
        health.Destroyed += OnPlayerDestroyed;
        var aim = Robot.AddComponent<WeaponAimController>();   // les armes du slot actif visent le reticule
        var fire = Robot.AddComponent<WeaponFireController>(); // clic gauche : tir (laser, plasma, rail, Tesla)
        var dust = Robot.AddComponent<RobotGroundFX>();        // poussiere des roues / chenilles / pattes
        Robot.AddComponent<RobotRecovery>();       // R : redressement facon Robocraft

        // HUD de match (theme hud du design system), recree avec le robot
        int maxHealth = 0;
        foreach (var view in Robot.GetComponentsInChildren<PlacedBlockView>())
            maxHealth += view.definition != null ? view.definition.resistanceHp : 0;
        hudHost = new GameObject("MatchHUD_" + blueprint.name);
        hudHost.AddComponent<RobotTestHUD>().Bind(locomotion, blueprint.name, maxHealth, weightKg);

        if (followCamera == null)
            followCamera = FindAnyObjectByType<RobotFollowCamera>();
        if (followCamera != null)
        {
            followCamera.SetTarget(Robot.transform);
            MatchPauseMenu.ApplyToCamera();
        }
        if (arena != null)
            arena.Player = Robot;

        // Apparition : assemblage holographique en levitation, puis chute et
        // controles rendus au joueur (pilotage, physique, visee, tir, poussiere)
        Robot.AddComponent<RobotSpawnSequence>().Begin(new Behaviour[] { driver, locomotion, aim, fire, dust });

        Debug.Log($"[RobotTest] Robot '{blueprint.name}' assemble : {placed} bloc(s), {weightKg} kg, case {cellSize} m" +
                  (isDefault ? " (robot par defaut : aucun blueprint en memoire)" : "") +
                  $" ; F5 : robot suivant ; ECHAP x2 -> '{RobotSession.ReturnSceneName}'.");
    }

    // ---------- Robots de demonstration (une famille de mouvement chacun) ----------

    private static PlacedBlock B(string id, int x, int y, int z, int face = 0, int spin = 0) =>
        new() { blockId = id, x = x, y = y, z = z, rotation = face == 0 && spin == 0 ? 0 : BlockFootprint.Compose(face, spin) };

    private static void Slab(RobotBlueprint bp, int x0, int x1, int y, int z0, int z1)
    {
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                bp.blocks.Add(B("Block_01_Cube", x, y, z));
    }

    public static List<RobotBlueprint> DemoBlueprints()
    {
        var list = new List<RobotBlueprint>();

        var tracks = new RobotBlueprint { name = "DEMO TRACKS" };
        Slab(tracks, 13, 17, 1, 13, 16);
        tracks.blocks.Add(B("Block_TankTrack_N1_Bison", 13, 0, 14, 1));
        tracks.blocks.Add(B("Block_TankTrack_N1_Bison", 17, 0, 14, 1));
        tracks.blocks.Add(B("Block_TankTrack_N1_Bison", 13, 0, 16, 1));
        tracks.blocks.Add(B("Block_TankTrack_N1_Bison", 17, 0, 16, 1));
        list.Add(tracks);

        var legs = new RobotBlueprint { name = "DEMO LEGS" };
        Slab(legs, 14, 16, 2, 14, 16);
        foreach (int z in new[] { 14, 16 })
        {
            legs.blocks.Add(B("Block_InsectLeg_N1_Walker", 13, 1, z, 4));
            legs.blocks.Add(B("Block_InsectLeg_N1_Walker", 17, 1, z, 3));
        }
        list.Add(legs);

        var hover = new RobotBlueprint { name = "DEMO HOVER" };
        Slab(hover, 14, 17, 1, 14, 17);
        // Lames plaquees sur les flancs par leur charniere : le disque reste a plat
        foreach (int z in new[] { 14, 16 })
        {
            hover.blocks.Add(B("Block_HoverBlade_N1_Squall", 13, 1, z, 4));
            hover.blocks.Add(B("Block_HoverBlade_N1_Squall", 18, 1, z, 3));
        }
        list.Add(hover);

        var heli = new RobotBlueprint { name = "DEMO HELI" };
        Slab(heli, 14, 16, 0, 13, 16);
        heli.blocks.Add(B("Block_RotorBlade_Recon", 15, 1, 15, 1));
        heli.blocks.Add(B("Block_Rudder_N1_Hawk", 15, 1, 13, 1));
        heli.blocks.Add(B("Block_Thruster_N1_Lynx", 15, 0, 12, 6));
        list.Add(heli);

        list.Add(Libellule());
        return list;
    }

    private static PlacedBlock P(string id, int x, int y, int z, int color, int face = 0, int spin = 0)
    {
        var b = B(id, x, y, z, face, spin);
        b.colorIndex = color;
        return b;
    }

    // LIBELLULE : helicoptere d'attaque a rotors en tandem (facon Chinook).
    // Avant = +Z. Fuselage bleu azur de 3 x 7 elargi a 5 au milieu (y = 1) sur
    // deux patins de tiges, cabine 3 x 4 (y = 2), nez en pentes orange. Rotor
    // Assault avant sur un pylone au-dessus du nez, rotor Assault arriere sur un
    // pylone de deux cubes au-dessus de la queue : deux disques de 3 m qui se
    // chevauchent pas. Grandes ailes Eagle sur les flancs, poutre de queue avec
    // double derive et empennage, deux propulseurs Panther encastres dans les
    // flancs arriere. Armes : 2 lasers Hornet sur la cabine, plasma Pulser au
    // centre (2 types sur 3).
    private static RobotBlueprint Libellule()
    {
        const int azure = 8, orange = 2, white = 0;
        var bp = new RobotBlueprint { name = "LIBELLULE" };
        int top = 1, back = 6, right = 3, left = 4;

        // Patins d'atterrissage
        for (int z = 12; z <= 16; z++)
        {
            bp.blocks.Add(P("Block_Rod_Court", 14, 0, z, white));
            bp.blocks.Add(P("Block_Rod_Court", 16, 0, z, white));
        }
        // Fuselage y = 1 : 3 x 7 (z 11-17), flancs elargis (x 13 et 17, z 11-15), nez en pentes
        for (int x = 14; x <= 16; x++)
        {
            for (int z = 11; z <= 17; z++)
                bp.blocks.Add(P("Block_01_Cube", x, 1, z, azure));
            bp.blocks.Add(P("Block_02_Pente", x, 1, 18, orange));
        }
        foreach (int x in new[] { 13, 17 })
        {
            for (int z = 11; z <= 15; z++)
                bp.blocks.Add(P("Block_01_Cube", x, 1, z, azure));
        }
        // Cabine y = 2 : 3 x 4 (z 12-15)
        for (int x = 14; x <= 16; x++)
        {
            for (int z = 12; z <= 15; z++)
                bp.blocks.Add(P("Block_01_Cube", x, 2, z, azure));
        }
        // Rotor avant : pylone sur le nez, disque au-dessus de la cabine (z 16-18)
        bp.blocks.Add(P("Block_01_Cube", 15, 2, 17, orange));
        bp.blocks.Add(P("Block_RotorBlade_Assault", 15, 3, 17, white, top));
        // Queue : poutre y = 2 (z 7-11), pylone arriere de deux cubes, rotor arriere (z 10-12)
        for (int z = 7; z <= 11; z++)
            bp.blocks.Add(P("Block_01_Cube", 15, 2, z, azure));
        bp.blocks.Add(P("Block_01_Cube", 15, 3, 11, orange));
        bp.blocks.Add(P("Block_01_Cube", 15, 4, 11, orange));
        bp.blocks.Add(P("Block_RotorBlade_Assault", 15, 5, 11, white, top));
        // Empennage : poutre elargie au bout, double derive, stabilisateurs
        bp.blocks.Add(P("Block_01_Cube", 14, 2, 7, orange));
        bp.blocks.Add(P("Block_01_Cube", 16, 2, 7, orange));
        bp.blocks.Add(P("Block_Rudder_N1_Hawk", 14, 3, 7, orange, top));
        bp.blocks.Add(P("Block_Rudder_N1_Hawk", 16, 3, 7, orange, top));
        bp.blocks.Add(P("Block_Rudder_N1_Hawk", 17, 2, 7, orange, right, 1));
        bp.blocks.Add(P("Block_Rudder_N1_Hawk", 13, 2, 7, orange, left, 1));
        // Ailes : grandes gouvernes Eagle a plat sur les flancs
        bp.blocks.Add(P("Block_Rudder_N4_Eagle", 18, 1, 13, orange, right, 1));
        bp.blocks.Add(P("Block_Rudder_N4_Eagle", 12, 1, 13, orange, left, 1));
        // Propulseurs Panther plaques a l'arriere des flancs (x 13-14 et 16-17, y 1-2)
        bp.blocks.Add(P("Block_Thruster_N2_Panther", 13, 1, 10, white, back));
        bp.blocks.Add(P("Block_Thruster_N2_Panther", 16, 1, 10, white, back));
        // Armement : lasers sur la cabine, plasma au centre entre les rotors
        bp.blocks.Add(P("Block_Laser_N2_Hornet", 14, 3, 13, white, top));
        bp.blocks.Add(P("Block_Laser_N2_Hornet", 16, 3, 13, white, top));
        bp.blocks.Add(P("Block_Plasma_N1_Pulser", 15, 3, 13, white, top));
        return bp;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayer(child, layer);
    }

    // Le spawn a-t-il un sol sous lui ? Une map sans collider (marquage pris pour
    // une surface, decor exclu par erreur) laisserait le robot tomber sans fin.
    private static void CheckGround(Vector3 position)
    {
        var origin = position + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 50f, ~(1 << RobotLayer), QueryTriggerInteraction.Ignore))
        {
            float drop = position.y - hit.point.y;
            string surface = hit.collider != null ? hit.collider.gameObject.name : "?";
            if (drop < -0.5f)
                Debug.LogWarning($"[RobotTest] Le point d'apparition est {-drop:0.##} m SOUS le sol ('{surface}') : le robot peut rester coince.");
            else
                Debug.Log($"[RobotTest] Sol sous le point d'apparition : '{surface}' a {drop:0.##} m.");
            return;
        }
        Debug.LogWarning("[RobotTest] Aucun sol (collider) sous le point d'apparition : le robot va tomber. " +
                         "Relance Blockforge > Setup Map Test Scene, ou verifie le filtre des maillages sans collision.");
    }

    // Robot par defaut (scene lancee sans robot du garage), au centre de la
    // grille 31 x 31 : chassis 2 x 5 cubes, cabine de 2 cubes a l'arriere,
    // deux lasers a l'avant sur le dessus (slot 1 du HUD) et
    // quatre roues Scout (1x2x2, moyeu cote -X) plaquees sur les flancs, face 3
    // (+X) a droite et face 4 (-X) a gauche, comme les poserait le garage.
    // La roue est centree sur sa case en Y et en Z quelle que soit la face
    // (BlockFootprint.LocalBox) : gauche et droite prennent donc les MEMES z.
    // (L'ancienne version decalait les roues gauches d'une case vers l'avant :
    // robot de travers.) Symetrique : centre XZ des roues = centre du chassis.
    public static RobotBlueprint CreateDefaultTestBlueprint()
    {
        var bp = new RobotBlueprint { name = "ROBOT TEST" };
        for (int x = 15; x <= 16; x++)
        {
            for (int z = 13; z <= 17; z++)
                bp.blocks.Add(new PlacedBlock { blockId = "Block_01_Cube", x = x, y = 0, z = z });
            bp.blocks.Add(new PlacedBlock { blockId = "Block_01_Cube", x = x, y = 1, z = 13 });   // cabine (arriere)
        }

        int right = BlockFootprint.Compose(3, 0);
        int left = BlockFootprint.Compose(4, 0);
        foreach (int z in new[] { 13, 17 })
        {
            bp.blocks.Add(new PlacedBlock { blockId = "Block_Wheel_N1_Scout", x = 17, y = 0, z = z, rotation = right });
            bp.blocks.Add(new PlacedBlock { blockId = "Block_Wheel_N1_Scout", x = 14, y = 0, z = z, rotation = left });
        }

        // Deux lasers a l'avant sur le dessus (poses face 1 comme au garage) :
        // slot 1 du HUD (Laser Wasp x2)
        int top = BlockFootprint.Compose(1, 0);
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Laser_N1_Wasp", x = 15, y = 1, z = 17, rotation = top });
        bp.blocks.Add(new PlacedBlock { blockId = "Block_Laser_N1_Wasp", x = 16, y = 1, z = 17, rotation = top });
        return bp;
    }
}
