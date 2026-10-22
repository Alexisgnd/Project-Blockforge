using System.Collections.Generic;
using UnityEngine;

// =========================================================
// ROBOTS DES BOTS (match contre les bots) : 15 presets
// Quinze robots assembles en code, chacun avec son domaine :
// - Sol (9) : roues (Dune runner, Rail sniper, Brawler, Mortar
//   buggy), chenilles (Bulwark), pattes d'insecte (Plasma walker,
//   Tesla stalker), lames de survol (Hover striker, Hover lancer) ;
// - Air (6) : rotors (Rotor gunship, Wasp drone, Iron condor,
//   Rail hawk, Plasma raven, Sky fortress).
// Le pool du match (GameModeDefinition.botPool : Mixed /
// GroundOnly / AirOnly, force par Blockforge > Debug > Bots)
// donne la liste NamesFor(pool) dans laquelle MatchIntroData.Bots
// tire un robot par bot : l'ecran Versus montre les robots qui
// apparaissent vraiment (TdmMatch construit Build(nom)).
// Regles de montage (celles du garage, BlockFootprint) :
// - un bloc se pose sur une piece deja posee, par sa face
//   d'ancrage ; les ailerons et gouvernes ne se posent que sur
//   des cubes (jamais sur un propulseur) ;
// - roues sur les flancs (faces 3 / 4, moyeu contre le chassis),
//   chenilles sous la dalle, pattes plaquees sur les flancs, lames
//   de survol sur les flancs (3 cases d'ecart), rotor sur le dessus
//   au-dessus du centre, propulseur a l'arriere (face 6) ;
// - electroplates = barricades : rangees de T4 (1 x 3) plaquees
//   contre la face avant (face 5), les flancs (faces 3 / 4) ou
//   l'arriere (face 6) d'une rangee de cubes, T2 (1 x 2) sur les
//   petits chassis ; chaque plaque couvre 3 cases de haut
//   centrees sur son cube : la rangee de cubes du dessous ou du
//   devant doit etre libre ;
// - une tige (Rod) ne porte que du chassis : rien ne s'y accroche
// - lames Tesla sur la face avant (face 5), a hauteur 3 pour ne
//   pas racler le sol ;
// - armes sur le dessus (face 1), derriere les barricades ; au
//   plus 3 types d'armes par robot (RobotLoadout).
// Chaque preset est verifie par Blockforge > Debug > Bots >
// Validate presets (blocs inconnus, chevauchements, pieces
// detachees, types d'armes, CPU, masse, vitesse) et exporte dans
// Blender par Export presets to Blender.
// Equilibrage : chaque preset echange force et sante (RoleOf) —
// canons de verre (Rail sniper, Mortar buggy, Wasp drone),
// tanks (Bulwark, Iron condor, Sky fortress), equilibres.
// Couleurs : index de la palette de peinture (0 = non peint,
// 1 rouge, 2 orange, 3 jaune, 4 vert clair, 5 vert, 6 turquoise,
// 7 cyan, 8 azur, 9 bleu, 10 violet, 11 magenta, 12 rose).
// Chaque appel a Build rend un blueprint neuf.
// =========================================================

public enum BotDomain { Ground, Air }

public enum BotRobotPool { Mixed, GroundOnly, AirOnly }

public static class BotRobotCatalog
{
    // ---------- Sol ----------
    public const string DuneRunner = "Dune runner";
    public const string RailSniper = "Rail sniper";
    public const string Brawler = "Brawler";
    public const string Bulwark = "Bulwark";
    public const string MortarBuggy = "Mortar buggy";
    public const string PlasmaWalker = "Plasma walker";
    public const string TeslaStalker = "Tesla stalker";
    public const string HoverStriker = "Hover striker";
    public const string HoverLancer = "Hover lancer";
    // ---------- Air ----------
    public const string RotorGunship = "Rotor gunship";
    public const string WaspDrone = "Wasp drone";
    public const string IronCondor = "Iron condor";
    public const string RailHawk = "Rail hawk";
    public const string PlasmaRaven = "Plasma raven";
    public const string SkyFortress = "Sky fortress";

    public static readonly string[] GroundNames =
    {
        DuneRunner, RailSniper, Brawler, Bulwark, MortarBuggy, PlasmaWalker, TeslaStalker, HoverStriker, HoverLancer,
    };

    public static readonly string[] AirNames =
    {
        RotorGunship, WaspDrone, IronCondor, RailHawk, PlasmaRaven, SkyFortress,
    };

    // Les 15 presets (pool Mixed)
    public static readonly string[] Names = Concat(GroundNames, AirNames);

    private static string[] Concat(string[] a, string[] b)
    {
        var all = new string[a.Length + b.Length];
        a.CopyTo(all, 0);
        b.CopyTo(all, a.Length);
        return all;
    }

    public static bool Contains(string name) => System.Array.IndexOf(Names, name) >= 0;

    // Role d'equilibrage : chaque preset echange de la force contre de la sante (ou de la vitesse)
    public static string RoleOf(string name) => name switch
    {
        DuneRunner => "Skirmisher : fast, average damage, average health",
        RailSniper => "Glass cannon : heavy rail at range, very fragile",
        Brawler => "Bruiser : short range, sturdy",
        Bulwark => "Tank : huge health, weak lasers, slow",
        MortarBuggy => "Artillery : heavy plasma lobs, fragile frame",
        PlasmaWalker => "Balanced : plasma on legs, climbs",
        TeslaStalker => "Melee : very high contact damage, armoured front",
        HoverStriker => "Raider : fast hover, strong lasers, light",
        HoverLancer => "Hover tank : slow hover, sturdy, steady lasers",
        RotorGunship => "Balanced flyer",
        WaspDrone => "Interceptor : strong lasers, paper frame",
        IronCondor => "Heavy flyer : armoured hull, light weapons",
        RailHawk => "Air sniper : rail and lasers, light",
        PlasmaRaven => "Air bomber : strong plasma, light",
        SkyFortress => "Flying fortress : very sturdy, weak plasma",
        _ => "",
    };

    public static BotDomain DomainOf(string name) => System.Array.IndexOf(AirNames, name) >= 0 ? BotDomain.Air : BotDomain.Ground;

    // Robot a rotors (BotBrain : vol a altitude tenue, pas de NavMesh)
    public static bool IsFlier(string name) => DomainOf(name) == BotDomain.Air;

    public static string[] NamesFor(BotRobotPool pool) => pool switch
    {
        BotRobotPool.GroundOnly => GroundNames,
        BotRobotPool.AirOnly => AirNames,
        _ => Names,
    };

    // ---------- Pool du match ----------

    // Forcage de test (Blockforge > Debug > Bots) : -1 = celui du mode
    public const string PoolOverrideKey = "Blockforge.BotPoolOverride";

    public static int PoolOverride
    {
        get => PlayerPrefs.GetInt(PoolOverrideKey, -1);
        set { PlayerPrefs.SetInt(PoolOverrideKey, value); PlayerPrefs.Save(); }
    }

    public static BotRobotPool EffectivePool(GameModeDefinition mode)
    {
        int forced = PoolOverride;
        if (forced >= 0 && forced <= (int)BotRobotPool.AirOnly)
            return (BotRobotPool)forced;
        return mode != null ? mode.botPool : BotRobotPool.Mixed;
    }

    public static string PoolLabel(BotRobotPool pool) => pool switch
    {
        BotRobotPool.GroundOnly => "Ground only",
        BotRobotPool.AirOnly => "Air only",
        _ => "Ground and air",
    };

    // ---------- Construction ----------

    public static RobotBlueprint Build(string name)
    {
        if (!Contains(name))
            name = DuneRunner;
        var bp = new RobotBlueprint { name = name };
        var b = new Builder(bp);
        switch (name)
        {
            case DuneRunner: BuildDuneRunner(b); break;
            case RailSniper: BuildRailSniper(b); break;
            case Brawler: BuildBrawler(b); break;
            case Bulwark: BuildBulwark(b); break;
            case MortarBuggy: BuildMortarBuggy(b); break;
            case PlasmaWalker: BuildPlasmaWalker(b); break;
            case TeslaStalker: BuildTeslaStalker(b); break;
            case HoverStriker: BuildHoverStriker(b); break;
            case HoverLancer: BuildHoverLancer(b); break;
            case RotorGunship: BuildRotorGunship(b); break;
            case WaspDrone: BuildWaspDrone(b); break;
            case IronCondor: BuildIronCondor(b); break;
            case RailHawk: BuildRailHawk(b); break;
            case PlasmaRaven: BuildPlasmaRaven(b); break;
            case SkyFortress: BuildSkyFortress(b); break;
        }
        return bp;
    }

    // Faces de montage (BlockFootprint.FaceNormals)
    private const int Top = 1, Under = 2, Right = 3, Left = 4, Front = 5, Back = 6;

    private const string Cube = "Block_01_Cube", Slope = "Block_02_Pente";
    private const string T2 = "Block_Electroplate_T2", T4 = "Block_Electroplate_T4";
    private const string Rod = "Block_Rod_Court";

    // Aide au montage : pas de doublon sur une case deja prise par un cube
    private sealed class Builder
    {
        private readonly RobotBlueprint bp;
        private readonly HashSet<Vector3Int> chassis = new();

        public Builder(RobotBlueprint bp) { this.bp = bp; }

        public void Add(string id, int x, int y, int z, int face = 0, int color = 0, int spin = 0)
        {
            bp.blocks.Add(new PlacedBlock
            {
                blockId = id, x = x, y = y, z = z,
                rotation = face == 0 && spin == 0 ? 0 : BlockFootprint.Compose(face, spin),
                colorIndex = color,
            });
            if (face == 0)
                chassis.Add(new Vector3Int(x, y, z));
        }

        // Dalle de cubes x0..x1 / z0..z1 a la hauteur y (cases deja prises sautees)
        public void Slab(int x0, int x1, int y, int z0, int z1, int color)
        {
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    if (!chassis.Contains(new Vector3Int(x, y, z)))
                        Add(Cube, x, y, z, 0, color);
                }
        }

        // Rangee de pentes sur la rangee z (spin : 0 vers l'avant, 2 vers l'arriere)
        public void Slopes(int x0, int x1, int y, int z, int color, int spin = 0)
        {
            for (int x = x0; x <= x1; x++)
                Add(Slope, x, y, z, 0, color, spin);
        }

        // Barricade avant : une plaque par colonne x, plaquee sur la face avant des cubes (x, y, z - 1)
        public void WallFront(string plate, int x0, int x1, int y, int z, int color = 0)
        {
            for (int x = x0; x <= x1; x++)
                Add(plate, x, y, z, Front, color);
        }

        // Barricade arriere : plaques sur la face arriere des cubes (x, y, z + 1)
        public void WallBack(string plate, int x0, int x1, int y, int z, int color = 0)
        {
            for (int x = x0; x <= x1; x++)
                Add(plate, x, y, z, Back, color);
        }

        // Barricade de flanc : une plaque par rangee z, sur le flanc droit (x = cubes + 1) ou gauche (cubes - 1)
        public void WallSide(string plate, bool right, int x, int y, int z0, int z1, int color = 0)
        {
            for (int z = z0; z <= z1; z++)
                Add(plate, x, y, z, right ? Right : Left, color);
        }

        // Paire de roues (ou de pattes) symetrique : droite en x = xr (face 3), gauche en x = xl (face 4)
        public void Pair(string id, int xr, int xl, int y, int z, int color = 0)
        {
            Add(id, xr, y, z, Right, color);
            Add(id, xl, y, z, Left, color);
        }
    }

    // =========================================================
    // SOL
    // =========================================================

    // Raider des dunes : buggy bas et ramasse sur huit roues Pathfinder, nez plongeant en deux marches,
    // garde-boue blindes T4/T2 au-dessus des roues, cabine basse a deux Blaster, aileron arriere arme
    // de trois Hornet sur ses montants, deux Hornet d'epaule, deux Wasp sous le nez, butoir et mur arriere T4
    private static void BuildDuneRunner(Builder b)
    {
        b.Slab(13, 17, 0, 10, 10, 8);
        b.Slab(13, 17, 0, 13, 13, 8);
        b.Slab(13, 17, 0, 16, 16, 8);
        b.Slab(13, 17, 0, 19, 19, 8);
        b.Slab(15, 15, 1, 9, 19, 8);
        b.Add(Cube, 14, 1, 10, 0, 8);
        b.Add(Cube, 16, 1, 10, 0, 8);
        b.Add(Cube, 14, 1, 13, 0, 8);
        b.Add(Cube, 16, 1, 13, 0, 8);
        b.Add(Cube, 14, 1, 16, 0, 8);
        b.Add(Cube, 16, 1, 16, 0, 8);
        b.Add(Cube, 14, 1, 19, 0, 8);
        b.Add(Cube, 16, 1, 19, 0, 8);
        b.Slab(14, 16, 1, 20, 21, 8);
        b.Add(Slope, 14, 1, 22);
        b.Add(Slope, 16, 1, 22);
        b.Slab(13, 17, 2, 9, 19, 8);
        b.Slopes(13, 17, 2, 20, 0);
        b.Slab(13, 17, 3, 9, 10, 8);
        b.Slab(13, 13, 3, 11, 18, 8);
        b.Slab(17, 17, 3, 11, 18, 8);
        b.Slab(14, 16, 3, 13, 16, 0);
        b.Slopes(14, 16, 3, 17, 0);
        b.Slopes(14, 16, 3, 12, 0, 2);
        b.Add(Cube, 13, 4, 9);
        b.Add(Cube, 17, 4, 9);
        b.Slab(13, 17, 5, 9, 9, 0);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 10);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 13);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 16);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 19);
        b.WallSide(T2, true, 18, 3, 9, 11);
        b.WallSide(T4, true, 18, 3, 12, 17);
        b.Add(T2, 18, 3, 18, Right);
        b.WallSide(T2, false, 12, 3, 9, 11);
        b.WallSide(T4, false, 12, 3, 12, 17);
        b.Add(T2, 12, 3, 18, Left);
        b.WallBack(T4, 13, 17, 2, 8);
        b.Add(T2, 13, 2, 21, Front);
        b.Add(T2, 17, 2, 21, Front);
        b.Add(T4, 15, 1, 22, Front);
        b.Add("Block_Laser_N3_Blaster", 14, 4, 14, Top);
        b.Add("Block_Laser_N3_Blaster", 16, 4, 14, Top);
        b.Add("Block_Laser_N2_Hornet", 13, 6, 9, Top);
        b.Add("Block_Laser_N2_Hornet", 15, 6, 9, Top);
        b.Add("Block_Laser_N2_Hornet", 17, 6, 9, Top);
        b.Add("Block_Laser_N2_Hornet", 13, 4, 16, Top);
        b.Add("Block_Laser_N2_Hornet", 17, 4, 16, Top);
        b.Add("Block_Laser_N1_Wasp", 14, 0, 21, Under);
        b.Add("Block_Laser_N1_Wasp", 16, 0, 21, Under);
    }

    // Scorpion de verre : corps compact 5 x 12 sur huit roues Pathfinder en guise de pattes, pinces Tesla
    // Ripper a l'avant, queue segmentee qui se dresse a l'arriere et se recourbe pour porter un canon rail
    // Decimator au-dessus du dos, quatre Hornet sur la carapace, deux Hornet sous le nez, murs T4 tout autour
    private static void BuildRailSniper(Builder b)
    {
        b.Add(Cube, 13, 0, 10, 0, 4);
        b.Add(Cube, 17, 0, 10, 0, 4);
        b.Add(Cube, 13, 0, 13, 0, 4);
        b.Add(Cube, 17, 0, 13, 0, 4);
        b.Add(Cube, 13, 0, 16, 0, 4);
        b.Add(Cube, 17, 0, 16, 0, 4);
        b.Add(Cube, 13, 0, 19, 0, 4);
        b.Add(Cube, 17, 0, 19, 0, 4);
        b.Slab(13, 17, 1, 9, 20, 4);
        b.Slab(13, 13, 2, 9, 20, 4);
        b.Slab(17, 17, 2, 9, 20, 4);
        b.Slab(14, 16, 2, 9, 11, 4);
        b.Slab(14, 16, 2, 19, 20, 4);
        b.Slab(13, 13, 3, 10, 20, 0);
        b.Slab(17, 17, 3, 10, 20, 0);
        b.Slab(15, 15, 3, 12, 18, 4);
        b.Add(Cube, 14, 3, 19);          // socle du Hornet (14,4,19) : une pente n'a pas de dessus plat
        b.Slopes(15, 15, 3, 19, 0);
        b.Add(Cube, 16, 3, 19);          // socle du Hornet (16,4,19)
        b.Slab(15, 15, 4, 15, 16, 0);
        b.Slab(14, 16, 3, 10, 11, 0);
        b.Add(Cube, 15, 4, 10);
        b.Add(Cube, 15, 4, 11);
        b.Add(Slope, 14, 4, 10, 0, 0, 1);
        b.Add(Slope, 16, 4, 10, 0, 0, 3);
        b.Add(Cube, 15, 5, 10);
        b.Add(Slope, 15, 5, 9, 0, 0, 2);
        b.Slab(15, 15, 5, 11, 13, 0);
        // Queue basse : 11,7 m de haut avant, coince sous les ponts (jour 9 a 17 m)
        b.Add("Block_Rail_N3_Decimator", 15, 6, 13, Top);
        b.Add("Block_Tesla_N2_Ripper", 13, 2, 21, Front);
        b.Add("Block_Tesla_N2_Ripper", 17, 2, 21, Front);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 10);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 13);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 16);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 19);
        b.WallSide(T4, true, 18, 3, 10, 20);
        b.WallSide(T4, false, 12, 3, 10, 20);
        b.WallFront(T4, 14, 16, 2, 21);
        b.WallBack(T4, 13, 17, 2, 8);
        b.Add("Block_Laser_N2_Hornet", 14, 4, 19, Top);
        b.Add("Block_Laser_N2_Hornet", 16, 4, 19, Top);
        b.Add("Block_Laser_N2_Hornet", 13, 4, 13, Top);
        b.Add("Block_Laser_N2_Hornet", 17, 4, 13, Top);
        b.Add("Block_Laser_N2_Hornet", 14, 0, 20, Under);
        b.Add("Block_Laser_N2_Hornet", 16, 0, 20, Under);
    }

    // Bouledogue : coque large 7 x 11 sur huit roues Pathfinder (quatre essieux), carrure basse,
    // machoire blindee T4 encadree de deux Tesla Ripper en bajoues, deux Hornet en crocs, deux Hornet sous le
    // menton, cabine 3 x 5 cuirassee sur ses quatre faces, Disruptor en tourelle, murs T4 sur les flancs et l'arriere
    private static void BuildBrawler(Builder b)
    {
        b.Slab(14, 16, 0, 11, 11, 1);
        b.Slab(14, 16, 0, 14, 14, 1);
        b.Slab(14, 16, 0, 17, 17, 1);
        b.Slab(14, 16, 0, 20, 20, 1);
        b.Slab(15, 15, 1, 11, 21, 1);
        b.Slab(14, 16, 1, 11, 11, 1);
        b.Slab(14, 16, 1, 14, 14, 1);
        b.Slab(14, 16, 1, 17, 17, 1);
        b.Slab(14, 16, 1, 20, 20, 1);
        b.Slab(14, 16, 1, 21, 21, 1);
        b.Slab(12, 18, 2, 11, 21, 1);
        b.Slab(14, 16, 3, 12, 16, 1);
        b.Slab(14, 16, 4, 12, 16, 0);
        b.Add(Cube, 15, 5, 14);
        b.Slopes(14, 16, 3, 20, 0);
        b.Slab(12, 13, 3, 11, 11, 1);
        b.Slab(17, 18, 3, 11, 11, 1);
        b.Pair("Block_Wheel_N3_Pathfinder", 17, 13, 0, 11);
        b.Pair("Block_Wheel_N3_Pathfinder", 17, 13, 0, 14);
        b.Pair("Block_Wheel_N3_Pathfinder", 17, 13, 0, 17);
        b.Pair("Block_Wheel_N3_Pathfinder", 17, 13, 0, 20);
        b.WallSide(T4, true, 19, 2, 11, 21);
        b.WallSide(T4, false, 11, 2, 11, 21);
        b.WallBack(T4, 14, 16, 2, 10);
        b.WallBack(T4, 12, 13, 3, 10);
        b.WallBack(T4, 17, 18, 3, 10);
        b.WallFront(T4, 14, 16, 2, 22);
        b.WallSide(T2, true, 17, 4, 12, 16);
        b.WallSide(T2, false, 13, 4, 12, 16);
        b.WallFront(T2, 14, 16, 4, 17);
        b.WallBack(T2, 14, 16, 4, 11);
        b.Add("Block_Tesla_N2_Ripper", 12, 2, 22, Front);
        b.Add("Block_Tesla_N2_Ripper", 18, 2, 22, Front);
        b.Add("Block_Laser_N2_Hornet", 13, 3, 21, Top);
        b.Add("Block_Laser_N2_Hornet", 17, 3, 21, Top);
        b.Add("Block_Laser_N2_Hornet", 14, 0, 21, Under);
        b.Add("Block_Laser_N2_Hornet", 16, 0, 21, Under);
        b.Add("Block_Plasma_N2_Disruptor", 15, 6, 14, Top);
    }

    // Mammouth : cadre creux 7 x 10 sur six chenilles Rhino en bogies (garde au sol 1,3 m), jupes T4 a l'avant et T2 a l'arriere,
    // bosse blindee T2 portant un Bombarder sur pylone, deux defenses rail Penetrator, trompe et ventre
    // a Hornet, glacis T4 avant a embrasure, mur T2 arriere
    private static void BuildBulwark(Builder b)
    {
        b.Slab(12, 12, 2, 11, 20, 6);
        b.Slab(18, 18, 2, 11, 20, 6);
        b.Slab(13, 17, 2, 11, 11, 6);
        b.Slab(13, 17, 2, 20, 20, 6);
        b.Add(Cube, 13, 2, 12, 0, 6);
        b.Add(Cube, 17, 2, 12, 0, 6);
        b.Add(Cube, 13, 2, 19, 0, 6);
        b.Add(Cube, 17, 2, 19, 0, 6);
        b.Add(Cube, 15, 2, 12, 0, 6);
        b.Add(Cube, 15, 2, 13, 0, 6);
        b.Add(Cube, 13, 2, 16, 0, 6);
        b.Add(Cube, 17, 2, 16, 0, 6);
        b.Add(Cube, 13, 1, 12, 0, 6);
        b.Add("Block_TankTrack_N2_Rhino", 13, 0, 12, Under);
        b.Add(Cube, 17, 1, 12, 0, 6);
        b.Add("Block_TankTrack_N2_Rhino", 17, 0, 12, Under);
        b.Add(Cube, 13, 1, 16, 0, 6);
        b.Add("Block_TankTrack_N2_Rhino", 13, 0, 16, Under);
        b.Add(Cube, 17, 1, 16, 0, 6);
        b.Add("Block_TankTrack_N2_Rhino", 17, 0, 16, Under);
        b.Add(Cube, 13, 1, 19, 0, 6);
        b.Add("Block_TankTrack_N2_Rhino", 13, 0, 19, Under);
        b.Add(Cube, 17, 1, 19, 0, 6);
        b.Add("Block_TankTrack_N2_Rhino", 17, 0, 19, Under);
        b.WallSide(T2, true, 19, 2, 11, 16);
        b.WallSide(T2, false, 11, 2, 11, 16);
        b.WallSide(T4, true, 19, 2, 17, 20);
        b.WallSide(T4, false, 11, 2, 17, 20);
        b.WallBack(T2, 13, 17, 2, 10);
        b.Add(T4, 12, 2, 21, Front);
        b.Add(T4, 13, 2, 21, Front);
        b.Add(T4, 14, 2, 21, Front);
        b.Add(T4, 16, 2, 21, Front);
        b.Add(T4, 17, 2, 21, Front);
        b.Add(T4, 18, 2, 21, Front);
        b.Slab(14, 16, 3, 13, 17, 0);
        b.WallSide(T2, true, 17, 3, 14, 15);
        b.WallSide(T2, false, 13, 3, 14, 15);
        b.Add(Cube, 15, 4, 15);
        b.Add(Cube, 15, 5, 15);
        b.Add("Block_Plasma_N3_Bombarder", 15, 6, 15, Top);
        b.Add(Cube, 13, 3, 19);
        b.Add(Cube, 17, 3, 19);
        b.Add("Block_Rail_N2_Penetrator", 13, 4, 19, Top);
        b.Add("Block_Rail_N2_Penetrator", 17, 4, 19, Top);
        b.Add("Block_Laser_N2_Hornet", 15, 3, 20, Top);
        b.Add("Block_Laser_N2_Hornet", 15, 1, 20, Under);
    }

    // Catapulte lourde : chassis 5 x 12 a deux etages sur huit roues Pathfinder, bras de catapulte
    // arme a l'arriere qui dresse un mortier Bombarder au-dessus du dos, second Bombarder en tourelle
    // sur le pont avant, deux mandibules Tesla Ripper basses, deux Hornet sous le nez et deux sur les
    // rails d'epaule, jupes T4 sur les flancs, murs T4 devant et derriere
    private static void BuildMortarBuggy(Builder b)
    {
        b.Add(Cube, 13, 0, 9, 0, 2);
        b.Add(Cube, 17, 0, 9, 0, 2);
        b.Add(Cube, 13, 0, 12, 0, 2);
        b.Add(Cube, 17, 0, 12, 0, 2);
        b.Add(Cube, 13, 0, 15, 0, 2);
        b.Add(Cube, 17, 0, 15, 0, 2);
        b.Add(Cube, 13, 0, 18, 0, 2);
        b.Add(Cube, 17, 0, 18, 0, 2);
        b.Slab(13, 13, 1, 9, 20, 2);
        b.Slab(17, 17, 1, 9, 20, 2);
        b.Slab(14, 16, 1, 9, 9, 2);
        b.Slab(14, 16, 1, 20, 20, 2);
        b.Slab(13, 17, 2, 10, 19, 2);
        b.Slopes(13, 17, 2, 20, 0);
        b.Slopes(13, 17, 2, 9, 0, 2);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 9);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 12);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 15);
        b.Pair("Block_Wheel_N3_Pathfinder", 18, 12, 0, 18);
        b.Slab(13, 13, 3, 10, 19, 0);
        b.Slab(17, 17, 3, 10, 19, 0);
        b.Slab(14, 16, 3, 14, 16, 0);
        b.Add("Block_Plasma_N3_Bombarder", 15, 4, 15, Top);
        b.Slab(14, 16, 3, 10, 12, 2);
        b.Slopes(14, 16, 3, 9, 0, 2);
        b.Slab(14, 16, 4, 10, 12, 2);
        b.Slab(14, 16, 5, 10, 12, 2);
        b.Slopes(14, 16, 4, 9, 0, 2);
        b.Slopes(14, 16, 5, 9, 0, 2);
        b.Add("Block_Plasma_N3_Bombarder", 15, 6, 11, Top);
        b.Add("Block_Tesla_N2_Ripper", 13, 2, 21, Front);
        b.Add("Block_Tesla_N2_Ripper", 17, 2, 21, Front);
        b.Add("Block_Laser_N2_Hornet", 14, 0, 20, Under);
        b.Add("Block_Laser_N2_Hornet", 16, 0, 20, Under);
        b.Add("Block_Laser_N2_Hornet", 13, 4, 15, Top);
        b.Add("Block_Laser_N2_Hornet", 17, 4, 15, Top);
        b.WallFront(T4, 14, 16, 2, 21);
        b.WallBack(T4, 13, 17, 2, 8);
        b.WallSide(T4, true, 18, 3, 10, 19);
        b.WallSide(T4, false, 12, 3, 10, 19);
    }

    // Rex blinde : dinosaure trapu a l'horizontale sur dix pattes Soldier, corps 5 x 9 a bosse dorsale,
    // tete massive a machoires Tesla Ripper et museliere T2, deux Bombarder plasma sur les epaules,
    // deux Hornet en yeux et deux en petits bras sur le poitrail, queue relevee courte,
    // flancs, poitrail et arriere en plaques T4 relevees au-dessus des pieds
    private static void BuildPlasmaWalker(Builder b)
    {
        b.Slab(13, 17, 2, 9, 17, 9);
        b.Slab(13, 17, 3, 9, 17, 9);
        b.Slab(14, 16, 4, 10, 17, 9);
        b.Slopes(14, 16, 4, 9, 0, 2);
        b.Add(Cube, 12, 4, 13, 0, 9);
        b.Add(Cube, 13, 4, 13, 0, 9);
        b.Add(Cube, 17, 4, 13, 0, 9);
        b.Add(Cube, 18, 4, 13, 0, 9);
        b.Slab(14, 16, 5, 15, 19, 0);
        b.Slab(14, 16, 4, 19, 19, 0);
        b.Slab(14, 16, 6, 17, 19, 0);
        b.Slopes(14, 16, 6, 16, 0, 2);
        b.Add(Cube, 15, 7, 17);
        b.Add(Slope, 15, 7, 18);
        b.Add(Cube, 15, 4, 8);
        b.Add(Cube, 15, 5, 8);
        b.Add(Slope, 15, 5, 7, 0, 0, 2);
        b.Add(Slope, 15, 4, 7, 0, 0, 2);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 9);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 11);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 13);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 15);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 17);
        b.Add(T4, 18, 3, 10, Right);
        b.Add(T4, 12, 3, 10, Left);
        b.Add(T4, 18, 3, 12, Right);
        b.Add(T4, 12, 3, 12, Left);
        b.Add(T4, 18, 3, 14, Right);
        b.Add(T4, 12, 3, 14, Left);
        b.Add(T4, 18, 3, 16, Right);
        b.Add(T4, 12, 3, 16, Left);
        b.Add(T4, 13, 3, 8, Back);
        b.Add(T4, 14, 3, 8, Back);
        b.Add(T4, 16, 3, 8, Back);
        b.Add(T4, 17, 3, 8, Back);
        b.WallFront(T4, 14, 16, 3, 18);
        b.Add("Block_Tesla_N2_Ripper", 14, 4, 20, Front);
        b.Add("Block_Tesla_N2_Ripper", 16, 4, 20, Front);
        b.Add(T2, 15, 4, 20, Front);
        b.Add("Block_Plasma_N3_Bombarder", 12, 5, 13, Top);
        b.Add("Block_Plasma_N3_Bombarder", 18, 5, 13, Top);
        b.Add("Block_Laser_N2_Hornet", 14, 7, 18, Top);
        b.Add("Block_Laser_N2_Hornet", 16, 7, 18, Top);
        b.Add("Block_Laser_N2_Hornet", 13, 3, 18, Front);
        b.Add("Block_Laser_N2_Hornet", 17, 3, 18, Front);
    }

    // Scorpion geant : huit pattes Soldier, corps 5 x 11 a trois etages, tete relevee armee de quatre
    // mandibules Tesla Ripper hautes, queue qui se recourbe au-dessus du dos et porte un rail Penetrator en dard,
    // barre dorsale a quatre Hornet, flancs, nez et croupe blindes de plaques
    private static void BuildTeslaStalker(Builder b)
    {
        b.Slab(13, 17, 2, 11, 20, 3);
        b.Slab(14, 16, 3, 11, 20, 3);
        b.Add(Cube, 13, 3, 12, 0, 3);
        b.Add(Cube, 17, 3, 12, 0, 3);
        b.Add(Cube, 13, 3, 13, 0, 3);
        b.Add(Cube, 17, 3, 13, 0, 3);
        b.Add(Cube, 13, 3, 15, 0, 3);
        b.Add(Cube, 17, 3, 15, 0, 3);
        b.Add(Cube, 13, 3, 16, 0, 3);
        b.Add(Cube, 17, 3, 16, 0, 3);
        b.Add(Cube, 13, 3, 18, 0, 3);
        b.Add(Cube, 17, 3, 18, 0, 3);
        b.Add(Cube, 13, 3, 19, 0, 3);
        b.Add(Cube, 17, 3, 19, 0, 3);
        b.Add(Cube, 13, 3, 20, 0, 3);
        b.Add(Cube, 17, 3, 20, 0, 3);
        b.Slopes(13, 17, 3, 10, 0, 2);
        b.Slab(14, 16, 4, 12, 17, 3);
        b.Slab(13, 17, 4, 18, 20, 3);
        b.Slab(13, 17, 5, 19, 20, 0);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 11);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 14);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 17);
        b.Pair("Block_InsectLeg_N2_Soldier", 18, 12, 2, 20);
        b.Add("Block_Tesla_N2_Ripper", 13, 5, 21, Front);
        b.Add("Block_Tesla_N2_Ripper", 14, 5, 21, Front);
        b.Add("Block_Tesla_N2_Ripper", 16, 5, 21, Front);
        b.Add("Block_Tesla_N2_Ripper", 17, 5, 21, Front);
        b.Add(T4, 15, 5, 21, Front);
        b.Slopes(13, 17, 6, 20, 0);
        b.WallSide(T2, true, 18, 3, 12, 13);
        b.WallSide(T2, false, 12, 3, 12, 13);
        b.WallSide(T2, true, 18, 3, 15, 16);
        b.WallSide(T2, false, 12, 3, 15, 16);
        b.WallSide(T2, true, 18, 3, 18, 19);
        b.WallSide(T2, false, 12, 3, 18, 19);
        b.WallBack(T2, 13, 17, 3, 9);
        b.Slab(15, 15, 5, 13, 16, 0);
        b.Add(Cube, 15, 6, 15);
        b.Slab(13, 17, 7, 15, 15, 0);
        b.Add("Block_Laser_N2_Hornet", 13, 8, 15, Top);
        b.Add("Block_Laser_N2_Hornet", 14, 8, 15, Top);
        b.Add("Block_Laser_N2_Hornet", 16, 8, 15, Top);
        b.Add("Block_Laser_N2_Hornet", 17, 8, 15, Top);
        b.Add(Cube, 15, 4, 10);
        b.Add(Cube, 15, 5, 10);
        b.Add(Cube, 15, 5, 9);
        b.Add(Cube, 15, 6, 9);
        b.Add(Cube, 15, 6, 8);
        b.Add(Cube, 15, 7, 8);
        b.Add(Cube, 15, 7, 9);
        b.Add(Cube, 15, 7, 10);
        b.Add(Cube, 15, 7, 11);
        b.Add(T2, 16, 7, 8, Right);
        b.Add(T2, 14, 7, 8, Left);
        // Dard bas : 11,5 m de haut avant, coince sous les ponts (jour 9 a 17 m)
        b.Add("Block_Rail_N2_Penetrator", 15, 8, 11, Top);
    }

    // Raie manta d'assaut : coque 5 x 13 a ventre creux sur dix lames Thunder, aile 7 de large effilee,
    // deux Ripper en cornes cephaliques, Disruptor sur la bosse dorsale, deux Hornet sur les plats-bords,
    // deux Hornet sous les cornes, flancs T4, mur avant T4, mur arriere T2
    private static void BuildHoverStriker(Builder b)
    {
        b.Slab(13, 17, 1, 10, 11, 7);
        b.Slab(13, 17, 1, 21, 22, 7);
        b.Add(Cube, 13, 1, 13, 0, 7);
        b.Add(Cube, 17, 1, 13, 0, 7);
        b.Add(Cube, 13, 1, 16, 0, 7);
        b.Add(Cube, 17, 1, 16, 0, 7);
        b.Add(Cube, 13, 1, 19, 0, 7);
        b.Add(Cube, 17, 1, 19, 0, 7);
        b.Pair("Block_HoverBlade_N2_Thunder", 18, 12, 1, 10);
        b.Pair("Block_HoverBlade_N2_Thunder", 18, 12, 1, 13);
        b.Pair("Block_HoverBlade_N2_Thunder", 18, 12, 1, 16);
        b.Pair("Block_HoverBlade_N2_Thunder", 18, 12, 1, 19);
        b.Pair("Block_HoverBlade_N2_Thunder", 18, 12, 1, 22);
        b.Slab(12, 18, 2, 13, 18, 7);
        b.Slab(13, 17, 2, 12, 12, 7);
        b.Slab(13, 17, 2, 19, 19, 7);
        b.Slab(13, 17, 2, 10, 11, 7);
        b.Slab(13, 17, 2, 20, 22, 7);
        b.Add(Cube, 13, 2, 23, 0, 7);
        b.Add(Cube, 17, 2, 23, 0, 7);
        b.Slab(14, 16, 3, 13, 17, 0);
        b.Slopes(14, 16, 3, 18, 0);
        b.Slopes(14, 16, 3, 12, 0, 2);
        b.Add(Cube, 12, 3, 13);
        b.Add(Cube, 18, 3, 13);
        b.Add(Cube, 12, 3, 14);
        b.Add(Cube, 18, 3, 14);
        b.Add(Cube, 12, 3, 15);
        b.Add(Cube, 18, 3, 15);
        b.Add(Cube, 12, 3, 16);
        b.Add(Cube, 18, 3, 16);
        b.Add(Cube, 12, 3, 17);
        b.Add(Cube, 18, 3, 17);
        b.Add(Cube, 12, 3, 18);
        b.Add(Cube, 18, 3, 18);
        b.WallSide(T2, true, 19, 3, 13, 18);
        b.WallSide(T2, false, 11, 3, 13, 18);
        b.WallFront(T4, 14, 16, 2, 23);
        b.WallBack(T2, 14, 16, 2, 9);
        b.Add(Rod, 12, 2, 21);
        b.Add(Rod, 12, 3, 21);
        b.Add(Cube, 12, 4, 21, 0, 7);
        b.Add(Rod, 18, 2, 21);
        b.Add(Rod, 18, 3, 21);
        b.Add(Cube, 18, 4, 21, 0, 7);
        b.Add("Block_Tesla_N2_Ripper", 12, 4, 22, Front);
        b.Add("Block_Tesla_N2_Ripper", 18, 4, 22, Front);
        b.Add("Block_Plasma_N2_Disruptor", 15, 4, 15, Top);
        b.Add("Block_Laser_N2_Hornet", 12, 4, 17, Top);
        b.Add("Block_Laser_N2_Hornet", 18, 4, 17, Top);
        b.Add("Block_Laser_N2_Hornet", 13, 1, 23, Under);
        b.Add("Block_Laser_N2_Hornet", 17, 1, 23, Under);
    }

    // Lancier lourd compact : pont 7 x 13 sur un tapis de quinze lames Thunder (trois colonnes),
    // poste de pilotage blinde, lance rail Decimator sur la dorsale flanquee de deux lances Tesla
    // Ripper, pinces de proue a deux Hornet etages de chaque cote, flancs T4 hauts sur rambardes,
    // murs avant et arriere
    private static void BuildHoverLancer(Builder b)
    {
        b.Add(Cube, 12, 1, 8, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 12, 1, 7, Back);
        b.Add("Block_HoverBlade_N2_Thunder", 12, 1, 9, Front);
        b.Add(Cube, 12, 1, 13, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 12, 1, 12, Back);
        b.Add("Block_HoverBlade_N2_Thunder", 12, 1, 14, Front);
        b.Add(Cube, 12, 1, 16, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 12, 1, 17, Front);
        b.Add(Cube, 15, 1, 8, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 15, 1, 7, Back);
        b.Add("Block_HoverBlade_N2_Thunder", 15, 1, 9, Front);
        b.Add(Cube, 15, 1, 13, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 15, 1, 12, Back);
        b.Add("Block_HoverBlade_N2_Thunder", 15, 1, 14, Front);
        b.Add(Cube, 15, 1, 16, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 15, 1, 17, Front);
        b.Add(Cube, 18, 1, 8, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 18, 1, 7, Back);
        b.Add("Block_HoverBlade_N2_Thunder", 18, 1, 9, Front);
        b.Add(Cube, 18, 1, 13, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 18, 1, 12, Back);
        b.Add("Block_HoverBlade_N2_Thunder", 18, 1, 14, Front);
        b.Add(Cube, 18, 1, 16, 0, 10);
        b.Add("Block_HoverBlade_N2_Thunder", 18, 1, 17, Front);
        b.Slab(12, 18, 2, 6, 18, 10);
        b.Add(Cube, 12, 3, 6, 0, 10);
        b.Add(Cube, 18, 3, 6, 0, 10);
        b.Add(Cube, 12, 3, 7, 0, 10);
        b.Add(Cube, 18, 3, 7, 0, 10);
        b.Add(Cube, 12, 3, 8, 0, 10);
        b.Add(Cube, 18, 3, 8, 0, 10);
        b.Add(Cube, 12, 3, 9, 0, 10);
        b.Add(Cube, 18, 3, 9, 0, 10);
        b.Add(Cube, 12, 3, 10, 0, 10);
        b.Add(Cube, 18, 3, 10, 0, 10);
        b.Add(Cube, 12, 3, 11, 0, 10);
        b.Add(Cube, 18, 3, 11, 0, 10);
        b.Add(Cube, 12, 3, 12, 0, 10);
        b.Add(Cube, 18, 3, 12, 0, 10);
        b.Add(Cube, 12, 3, 13, 0, 10);
        b.Add(Cube, 18, 3, 13, 0, 10);
        b.Add(Cube, 12, 3, 14, 0, 10);
        b.Add(Cube, 18, 3, 14, 0, 10);
        b.Add(Cube, 12, 3, 15, 0, 10);
        b.Add(Cube, 18, 3, 15, 0, 10);
        b.Add(Cube, 12, 3, 16, 0, 10);
        b.Add(Cube, 18, 3, 16, 0, 10);
        b.Add(Cube, 12, 3, 17, 0, 10);
        b.Add(Cube, 18, 3, 17, 0, 10);
        b.Add(Cube, 12, 3, 18, 0, 10);
        b.Add(Cube, 18, 3, 18, 0, 10);
        b.Slab(14, 16, 3, 8, 11, 0);
        b.Slab(14, 16, 4, 8, 10, 0);
        b.Slopes(14, 16, 4, 11, 0);
        b.Add(Cube, 15, 3, 15, 0, 10);
        b.Add(Cube, 15, 3, 16, 0, 10);
        b.Add(Cube, 15, 3, 17, 0, 10);
        b.Add(Cube, 15, 3, 18, 0, 10);
        b.Add("Block_Rail_N3_Decimator", 15, 4, 16, Top);
        b.Add(Cube, 13, 3, 17, 0, 10);
        b.Add(Cube, 13, 4, 17, 0, 10);
        b.Add(Cube, 13, 5, 17, 0, 10);
        b.Add(Cube, 17, 3, 17, 0, 10);
        b.Add(Cube, 17, 4, 17, 0, 10);
        b.Add(Cube, 17, 5, 17, 0, 10);
        b.Add("Block_Tesla_N2_Ripper", 13, 5, 18, Front);
        b.Add("Block_Tesla_N2_Ripper", 17, 5, 18, Front);
        b.Add(Cube, 12, 4, 17);
        b.Add(Cube, 18, 4, 17);
        b.Add("Block_Laser_N2_Hornet", 12, 4, 18, Top);
        b.Add("Block_Laser_N2_Hornet", 18, 4, 18, Top);
        b.Add("Block_Laser_N2_Hornet", 12, 5, 17, Top);
        b.Add("Block_Laser_N2_Hornet", 18, 5, 17, Top);
        b.WallSide(T4, true, 19, 3, 6, 18);
        b.WallSide(T4, false, 11, 3, 6, 18);
        b.WallFront(T2, 14, 16, 2, 19);
        b.Add(T2, 12, 2, 19, Front);
        b.Add(T2, 18, 2, 19, Front);
        b.WallBack(T2, 12, 18, 2, 5);
        b.WallBack(T2, 14, 16, 4, 7);
        b.WallSide(T2, true, 17, 4, 8, 10);
        b.WallSide(T2, false, 13, 4, 8, 10);
    }

    // =========================================================
    // AIR (rotors sur des mats, disque libre ; propulseur a l'arriere, rien de pose ni colle
    // dessus ; gouverne sur un cube ; armes sous les ailes et sous le nez : elles visent vers le bas)
    // =========================================================

    // Helicoptere de combat tandem lourd : fuselage blinde 3 x 16 sur deux etages, nez carre blinde
    // et verriere, bosse dorsale a l'arriere, deux tetes de rotor en tandem (Assault central sur son mat
    // et traverse a quatre Recon, la tete arriere plus haute), ailerons de sponson a deux rotors Invader,
    // derive Falcon et propulseur Panther sur la poutre de queue ; rail Penetrator sous le menton,
    // Disruptor et Blaster sous chaque sponson, deux Blaster sur la bosse ; murs T4 au nez et autour
    // du cockpit, T2 sur les flancs arriere et derriere la cabine
    private static void BuildRotorGunship(Builder b)
    {
        b.Slab(14, 16, 0, 9, 23, 4);
        b.Slab(14, 16, 1, 9, 23, 4);
        b.Slab(14, 16, 2, 17, 20, 0);
        b.Slopes(14, 16, 2, 21, 0);
        b.Slab(14, 16, 2, 9, 13, 4);
        b.Slab(14, 16, 3, 10, 12, 4);
        b.Slopes(14, 16, 2, 14, 0);
        b.Add(Cube, 15, 0, 4, 0, 4);
        b.Add(Cube, 15, 0, 5, 0, 4);
        b.Add(Cube, 15, 0, 6, 0, 4);
        b.Add(Cube, 15, 0, 7, 0, 4);
        b.Add(Cube, 15, 0, 8, 0, 4);
        b.Add("Block_Rudder_N2_Falcon", 15, 1, 5, Top);
        b.Add("Block_Thruster_N2_Panther", 15, 0, 3, Back);
        b.Slab(6, 13, 2, 19, 19, 0);
        b.Slab(17, 24, 2, 19, 19, 0);
        b.Add("Block_RotorBlade_Recon", 6, 3, 19, Top);
        b.Add("Block_RotorBlade_Recon", 10, 3, 19, Top);
        b.Add("Block_RotorBlade_Recon", 20, 3, 19, Top);
        b.Add("Block_RotorBlade_Recon", 24, 3, 19, Top);
        b.Add(Cube, 15, 3, 19);
        b.Add(Cube, 15, 4, 19);
        b.Add("Block_RotorBlade_Assault", 15, 5, 19, Top);
        b.Slab(6, 13, 3, 10, 10, 0);
        b.Slab(17, 24, 3, 10, 10, 0);
        b.Add("Block_RotorBlade_Recon", 6, 4, 10, Top);
        b.Add("Block_RotorBlade_Recon", 10, 4, 10, Top);
        b.Add("Block_RotorBlade_Recon", 20, 4, 10, Top);
        b.Add("Block_RotorBlade_Recon", 24, 4, 10, Top);
        b.Add(Cube, 15, 4, 10);
        b.Add(Cube, 15, 5, 10);
        b.Add("Block_RotorBlade_Assault", 15, 6, 10, Top);
        b.Slab(10, 13, 1, 13, 15, 4);
        b.Slab(17, 20, 1, 13, 15, 4);
        b.Slab(7, 9, 1, 14, 14, 4);
        b.Slab(21, 23, 1, 14, 14, 4);
        b.Add(Cube, 7, 2, 14);
        b.Add("Block_RotorBlade_Invader", 7, 3, 14, Top);
        b.Add(Cube, 11, 2, 14);
        b.Add("Block_RotorBlade_Invader", 11, 3, 14, Top);
        b.Add(Cube, 19, 2, 14);
        b.Add("Block_RotorBlade_Invader", 19, 3, 14, Top);
        b.Add(Cube, 23, 2, 14);
        b.Add("Block_RotorBlade_Invader", 23, 3, 14, Top);
        b.Add("Block_Plasma_N2_Disruptor", 11, 0, 14, Under);
        b.Add("Block_Plasma_N2_Disruptor", 19, 0, 14, Under);
        b.Add("Block_Laser_N3_Blaster", 8, 0, 14, Under);
        b.Add("Block_Laser_N3_Blaster", 22, 0, 14, Under);
        b.Add("Block_Laser_N3_Blaster", 14, 4, 12, Top);
        b.Add("Block_Laser_N3_Blaster", 16, 4, 12, Top);
        b.Add("Block_Rail_N2_Penetrator", 15, -1, 21, Under);
        b.WallFront(T4, 14, 16, 1, 24);
        b.WallSide(T4, true, 17, 1, 16, 18);
        b.WallSide(T4, false, 13, 1, 16, 18);
        b.WallSide(T4, true, 17, 1, 20, 23);
        b.WallSide(T4, false, 13, 1, 20, 23);
        b.WallSide(T2, true, 17, 1, 9, 11);
        b.WallSide(T2, false, 13, 1, 9, 11);
        b.Add(T2, 14, 1, 8, Back);
        b.Add(T2, 16, 1, 8, Back);
    }

    // Guepe de chasse : thorax blinde 5 x 5 sur deux etages, tete a mandibules et abdomen raye
    // jaune et noir, quatre ailes (deux paires) portant huit rotors Recon sur leurs mats plus deux
    // rotors Recon dorsaux, propulseur Panther au dard, deux Ripper en mandibules sous la tete, quatre
    // Hornet (yeux et sous l'abdomen), deux Blaster sous les ailes avant, murs T4 au museau, sur les
    // flancs et a l'arriere du thorax, plaques T2 sur l'abdomen et en bout d'aile
    private static void BuildWaspDrone(Builder b)
    {
        b.Slab(13, 17, 0, 13, 17, 11);
        b.Slab(13, 17, 1, 13, 17, 11);
        b.Slab(13, 17, 0, 18, 19, 0);
        b.Slab(14, 16, 1, 18, 19, 0);
        b.Slab(14, 16, 0, 20, 21, 0);
        b.Slopes(14, 16, 1, 20, 0);
        b.Slab(14, 16, 0, 7, 7, 11);
        b.Slab(14, 16, 0, 8, 8, 0);
        b.Slab(14, 16, 0, 9, 9, 11);
        b.Slab(14, 16, 0, 10, 10, 0);
        b.Slab(14, 16, 0, 11, 11, 11);
        b.Slab(14, 16, 0, 12, 12, 0);
        b.Slab(14, 16, 1, 9, 12, 11);
        b.Slopes(14, 16, 1, 8, 0, 2);
        b.Slab(5, 12, 0, 17, 17, 11);
        b.Slab(18, 25, 0, 17, 17, 11);
        b.Slab(5, 13, 0, 12, 12, 11);
        b.Slab(17, 25, 0, 12, 12, 11);
        b.Slab(7, 7, 0, 13, 16, 0);
        b.Slab(23, 23, 0, 13, 16, 0);
        b.Add(Cube, 5, 1, 12);
        b.Add("Block_RotorBlade_Recon", 5, 2, 12, Top);
        b.Add(Cube, 5, 1, 17);
        b.Add("Block_RotorBlade_Recon", 5, 2, 17, Top);
        b.Add(Cube, 9, 1, 12);
        b.Add("Block_RotorBlade_Recon", 9, 2, 12, Top);
        b.Add(Cube, 9, 1, 17);
        b.Add("Block_RotorBlade_Recon", 9, 2, 17, Top);
        b.Add(Cube, 21, 1, 12);
        b.Add("Block_RotorBlade_Recon", 21, 2, 12, Top);
        b.Add(Cube, 21, 1, 17);
        b.Add("Block_RotorBlade_Recon", 21, 2, 17, Top);
        b.Add(Cube, 25, 1, 12);
        b.Add("Block_RotorBlade_Recon", 25, 2, 12, Top);
        b.Add(Cube, 25, 1, 17);
        b.Add("Block_RotorBlade_Recon", 25, 2, 17, Top);
        b.Add(Cube, 15, 2, 15);
        b.Add("Block_RotorBlade_Recon", 15, 3, 15, Top);
        b.Add(Cube, 15, 2, 10);
        b.Add("Block_RotorBlade_Recon", 15, 3, 10, Top);
        b.Add("Block_Thruster_N2_Panther", 15, 0, 6, Back);
        b.Add("Block_Tesla_N2_Ripper", 13, 0, 20, Front);
        b.Add("Block_Tesla_N2_Ripper", 17, 0, 20, Front);
        b.Add("Block_Laser_N2_Hornet", 14, 2, 19, Top);
        b.Add("Block_Laser_N2_Hornet", 16, 2, 19, Top);
        b.Add("Block_Laser_N2_Hornet", 14, -1, 11, Under);
        b.Add("Block_Laser_N2_Hornet", 16, -1, 11, Under);
        b.Add("Block_Laser_N3_Blaster", 11, -1, 17, Under);
        b.Add("Block_Laser_N3_Blaster", 19, -1, 17, Under);
        b.WallFront(T4, 14, 16, 0, 22);
        b.WallSide(T4, true, 18, 1, 14, 16);
        b.WallSide(T4, false, 12, 1, 14, 16);
        b.WallBack(T4, 13, 13, 0, 11);
        b.WallBack(T4, 17, 17, 0, 11);
        b.WallSide(T2, true, 17, 1, 9, 10);
        b.WallSide(T2, false, 13, 1, 9, 10);
        b.Add(T2, 4, 0, 17, Left);
        b.Add(T2, 4, 0, 12, Left);
        b.Add(T2, 26, 0, 17, Right);
        b.Add(T2, 26, 0, 12, Right);
    }

    // Osprey lourd : fuselage 5 x 14 sur deux etages, cockpit et nez en pentes, aile haute de 25 cases
    // terminee par deux nacelles portant chacune trois rotors Assault, grand rotor Assault sur un mat
    // central, rotors Invader sur l'aile, le plan canard et l'empennage, derive Falcon, propulseur Leopard,
    // deux Disruptor sur le dos, deux rails Penetrator et un Hornet sous le ventre, quatre Hornet sous
    // les ailes, murs T4 sur les flancs, les nacelles et le nez, plaques T4 a l'arriere de l'empennage
    private static void BuildIronCondor(Builder b)
    {
        b.Slab(13, 17, 0, 8, 21, 6);
        b.Slab(13, 13, 1, 9, 21, 6);
        b.Slab(17, 17, 1, 9, 21, 6);
        b.Add(Cube, 15, 1, 11, 0, 6);
        b.Add(Cube, 15, 1, 17, 0, 6);
        b.Slopes(13, 17, 0, 22, 0);
        b.Slopes(13, 17, 1, 22, 0);
        b.Slab(14, 16, 2, 20, 21, 0);
        b.Slopes(14, 16, 2, 22, 0);
        b.Slab(5, 13, 2, 13, 14, 6);
        b.Slab(17, 25, 2, 13, 14, 6);
        b.Slab(14, 16, 2, 14, 14, 6);
        b.Slab(14, 16, 2, 8, 9, 6);
        b.Add(Cube, 9, 2, 8, 0, 6);
        b.Add(Cube, 9, 2, 21, 0, 6);
        b.Add(Cube, 10, 2, 8, 0, 6);
        b.Add(Cube, 10, 2, 21, 0, 6);
        b.Add(Cube, 11, 2, 8);
        b.Add(Cube, 11, 2, 21, 0, 6);
        b.Add(Cube, 12, 2, 8);
        b.Add(Cube, 12, 2, 21, 0, 6);
        b.Add(Cube, 13, 2, 8, 0, 6);
        b.Add(Cube, 13, 2, 21, 0, 6);
        b.Add(Cube, 17, 2, 8, 0, 6);
        b.Add(Cube, 17, 2, 21, 0, 6);
        b.Add(Cube, 18, 2, 8);
        b.Add(Cube, 18, 2, 21, 0, 6);
        b.Add(Cube, 19, 2, 8);
        b.Add(Cube, 19, 2, 21, 0, 6);
        b.Add(Cube, 20, 2, 8, 0, 6);
        b.Add(Cube, 20, 2, 21, 0, 6);
        b.Add(Cube, 21, 2, 8, 0, 6);
        b.Add(Cube, 21, 2, 21, 0, 6);
        b.Slab(4, 4, 2, 10, 18, 6);
        b.Add(Cube, 4, 3, 10);
        b.Add("Block_RotorBlade_Assault", 4, 4, 10, Top);
        b.Add(Cube, 4, 3, 14);
        b.Add("Block_RotorBlade_Assault", 4, 4, 14, Top);
        b.Add(Cube, 4, 3, 18);
        b.Add("Block_RotorBlade_Assault", 4, 4, 18, Top);
        b.Slab(26, 26, 2, 10, 18, 6);
        b.Add(Cube, 26, 3, 10);
        b.Add("Block_RotorBlade_Assault", 26, 4, 10, Top);
        b.Add(Cube, 26, 3, 14);
        b.Add("Block_RotorBlade_Assault", 26, 4, 14, Top);
        b.Add(Cube, 26, 3, 18);
        b.Add("Block_RotorBlade_Assault", 26, 4, 18, Top);
        b.Add(Cube, 9, 3, 8);
        b.Add("Block_RotorBlade_Invader", 9, 4, 8, Top);
        b.Add(Cube, 9, 3, 14);
        b.Add("Block_RotorBlade_Invader", 9, 4, 14, Top);
        b.Add(Cube, 9, 3, 21);
        b.Add("Block_RotorBlade_Invader", 9, 4, 21, Top);
        b.Add(Cube, 21, 3, 8);
        b.Add("Block_RotorBlade_Invader", 21, 4, 8, Top);
        b.Add(Cube, 21, 3, 14);
        b.Add("Block_RotorBlade_Invader", 21, 4, 14, Top);
        b.Add(Cube, 21, 3, 21);
        b.Add("Block_RotorBlade_Invader", 21, 4, 21, Top);
        b.Add(Rod, 15, 3, 14);
        b.Add(Rod, 15, 4, 14);
        b.Add(Cube, 15, 5, 14);
        b.Add("Block_RotorBlade_Assault", 15, 6, 14, Top);
        b.Add("Block_Rudder_N2_Falcon", 15, 3, 9, Top);
        b.Add("Block_Thruster_N3_Leopard", 15, 0, 7, Back);
        b.Add("Block_Plasma_N2_Disruptor", 15, 2, 17, Top);
        b.Add("Block_Plasma_N2_Disruptor", 15, 2, 11, Top);
        b.Add("Block_Rail_N2_Penetrator", 13, -1, 17, Under);
        b.Add("Block_Rail_N2_Penetrator", 17, -1, 17, Under);
        b.Add("Block_Laser_N2_Hornet", 15, -1, 17, Under);
        b.Add("Block_Laser_N2_Hornet", 7, 1, 14, Under);
        b.Add("Block_Laser_N2_Hornet", 11, 1, 14, Under);
        b.Add("Block_Laser_N2_Hornet", 19, 1, 14, Under);
        b.Add("Block_Laser_N2_Hornet", 23, 1, 14, Under);
        b.WallSide(T4, true, 18, 1, 17, 20);
        b.WallSide(T4, false, 12, 1, 17, 20);
        b.WallSide(T4, true, 18, 1, 10, 11);
        b.WallSide(T4, false, 12, 1, 10, 11);
        b.WallSide(T4, true, 27, 2, 13, 15);
        b.WallSide(T4, false, 3, 2, 13, 15);
        b.WallFront(T4, 14, 16, 1, 23);
        b.WallBack(T2, 11, 12, 2, 7);
        b.WallBack(T2, 18, 19, 2, 7);
    }

    // Chasseur d'interception : fuselage 3 x 18 et epine dorsale, grande aile delta en treillis de
    // 25 cases a bord d'attaque en pentes, trois rotors Invader sur l'epine et dix rotors Recon en
    // quinconce sur les longerons, deux derives Hawk, deux propulseurs Lynx, rail Decimator sous le
    // nez, quatre Hornet sous les ailes, deux Blaster sur les ailes, bec blinde T4, cockpit blinde T4,
    // winglets T2 et bord de fuite T2
    private static void BuildRailHawk(Builder b)
    {
        b.Slab(14, 16, 0, 6, 23, 0);
        b.Slab(15, 15, 1, 10, 18, 9);
        b.Slab(14, 16, 1, 19, 23, 0);
        b.Slab(15, 15, 2, 20, 22, 9);
        b.Add(Slope, 15, 2, 23, 0, 9);
        b.WallFront(T4, 14, 16, 1, 24);
        b.Add(Cube, 17, 0, 6);
        b.Add(Cube, 13, 0, 6);
        b.Add(Cube, 18, 0, 6);
        b.Add(Cube, 12, 0, 6);
        b.Add(Cube, 19, 0, 6);
        b.Add(Cube, 11, 0, 6);
        b.Add(Cube, 20, 0, 6);
        b.Add(Cube, 10, 0, 6);
        b.Add(Cube, 21, 0, 6);
        b.Add(Cube, 9, 0, 6);
        b.Add(Cube, 22, 0, 6);
        b.Add(Cube, 8, 0, 6);
        b.Add(Cube, 23, 0, 6);
        b.Add(Cube, 7, 0, 6);
        b.Add(Cube, 24, 0, 6);
        b.Add(Cube, 6, 0, 6);
        b.Add(Cube, 25, 0, 6);
        b.Add(Cube, 5, 0, 6);
        b.Add(Cube, 26, 0, 6);
        b.Add(Cube, 4, 0, 6);
        b.Add(Cube, 17, 0, 7);
        b.Add(Cube, 13, 0, 7);
        b.Add(Cube, 26, 0, 7);
        b.Add(Cube, 4, 0, 7);
        b.Add(Cube, 17, 0, 8);
        b.Add(Cube, 13, 0, 8);
        b.Add(Cube, 18, 0, 8);
        b.Add(Cube, 12, 0, 8);
        b.Add(Cube, 19, 0, 8);
        b.Add(Cube, 11, 0, 8);
        b.Add(Cube, 20, 0, 8);
        b.Add(Cube, 10, 0, 8);
        b.Add(Cube, 21, 0, 8);
        b.Add(Cube, 9, 0, 8);
        b.Add(Cube, 22, 0, 8);
        b.Add(Cube, 8, 0, 8);
        b.Add(Cube, 23, 0, 8);
        b.Add(Cube, 7, 0, 8);
        b.Add(Cube, 24, 0, 8);
        b.Add(Cube, 6, 0, 8);
        b.Add(Cube, 25, 0, 8);
        b.Add(Cube, 5, 0, 8);
        b.Add(Cube, 26, 0, 8);
        b.Add(Cube, 4, 0, 8);
        b.Slopes(26, 26, 0, 9, 9);
        b.Slopes(4, 4, 0, 9, 9);
        b.Add(Cube, 17, 0, 9);
        b.Add(Cube, 13, 0, 9);
        b.Add(Cube, 25, 0, 9);
        b.Add(Cube, 5, 0, 9);
        b.Add(Cube, 17, 0, 10);
        b.Add(Cube, 13, 0, 10);
        b.Add(Cube, 25, 0, 10);
        b.Add(Cube, 5, 0, 10);
        b.Slopes(25, 25, 0, 11, 9);
        b.Slopes(5, 5, 0, 11, 9);
        b.Add(Cube, 17, 0, 11);
        b.Add(Cube, 13, 0, 11);
        b.Add(Cube, 24, 0, 11);
        b.Add(Cube, 6, 0, 11);
        b.Add(Cube, 17, 0, 12);
        b.Add(Cube, 13, 0, 12);
        b.Add(Cube, 18, 0, 12);
        b.Add(Cube, 12, 0, 12);
        b.Add(Cube, 19, 0, 12);
        b.Add(Cube, 11, 0, 12);
        b.Add(Cube, 20, 0, 12);
        b.Add(Cube, 10, 0, 12);
        b.Add(Cube, 21, 0, 12);
        b.Add(Cube, 9, 0, 12);
        b.Add(Cube, 22, 0, 12);
        b.Add(Cube, 8, 0, 12);
        b.Add(Cube, 23, 0, 12);
        b.Add(Cube, 7, 0, 12);
        b.Add(Cube, 24, 0, 12);
        b.Add(Cube, 6, 0, 12);
        b.Slopes(24, 24, 0, 13, 9);
        b.Slopes(6, 6, 0, 13, 9);
        b.Add(Cube, 17, 0, 13);
        b.Add(Cube, 13, 0, 13);
        b.Add(Cube, 23, 0, 13);
        b.Add(Cube, 7, 0, 13);
        b.Slopes(23, 23, 0, 14, 9);
        b.Slopes(7, 7, 0, 14, 9);
        b.Add(Cube, 17, 0, 14);
        b.Add(Cube, 13, 0, 14);
        b.Add(Cube, 22, 0, 14);
        b.Add(Cube, 8, 0, 14);
        b.Slopes(22, 22, 0, 15, 9);
        b.Slopes(8, 8, 0, 15, 9);
        b.Add(Cube, 17, 0, 15);
        b.Add(Cube, 13, 0, 15);
        b.Add(Cube, 21, 0, 15);
        b.Add(Cube, 9, 0, 15);
        b.Slopes(21, 21, 0, 16, 9);
        b.Slopes(9, 9, 0, 16, 9);
        b.Add(Cube, 17, 0, 16);
        b.Add(Cube, 13, 0, 16);
        b.Add(Cube, 20, 0, 16);
        b.Add(Cube, 10, 0, 16);
        b.Slopes(20, 20, 0, 17, 9);
        b.Slopes(10, 10, 0, 17, 9);
        b.Add(Cube, 17, 0, 17);
        b.Add(Cube, 13, 0, 17);
        b.Add(Cube, 19, 0, 17);
        b.Add(Cube, 11, 0, 17);
        b.Slopes(19, 19, 0, 18, 9);
        b.Slopes(11, 11, 0, 18, 9);
        b.Add(Cube, 17, 0, 18);
        b.Add(Cube, 13, 0, 18);
        b.Add(Cube, 18, 0, 18);
        b.Add(Cube, 12, 0, 18);
        b.Slopes(18, 18, 0, 19, 9);
        b.Slopes(12, 12, 0, 19, 9);
        b.Add(Cube, 15, 2, 10, 0, 9);
        b.Add("Block_RotorBlade_Invader", 15, 3, 10, Top);
        b.Add(Cube, 15, 2, 14, 0, 9);
        b.Add("Block_RotorBlade_Invader", 15, 3, 14, Top);
        b.Add(Cube, 15, 2, 18, 0, 9);
        b.Add("Block_RotorBlade_Invader", 15, 3, 18, Top);
        b.Add(Cube, 19, 1, 8, 0, 9);
        b.Add("Block_RotorBlade_Recon", 19, 2, 8, Top);
        b.Add(Cube, 11, 1, 8, 0, 9);
        b.Add("Block_RotorBlade_Recon", 11, 2, 8, Top);
        b.Add(Cube, 22, 1, 8, 0, 9);
        b.Add(Cube, 22, 2, 8, 0, 9);
        b.Add("Block_RotorBlade_Recon", 22, 3, 8, Top);
        b.Add(Cube, 8, 1, 8, 0, 9);
        b.Add(Cube, 8, 2, 8, 0, 9);
        b.Add("Block_RotorBlade_Recon", 8, 3, 8, Top);
        b.Add(Cube, 25, 1, 8, 0, 9);
        b.Add("Block_RotorBlade_Recon", 25, 2, 8, Top);
        b.Add(Cube, 5, 1, 8, 0, 9);
        b.Add("Block_RotorBlade_Recon", 5, 2, 8, Top);
        b.Add(Cube, 19, 1, 12, 0, 9);
        b.Add("Block_RotorBlade_Recon", 19, 2, 12, Top);
        b.Add(Cube, 11, 1, 12, 0, 9);
        b.Add("Block_RotorBlade_Recon", 11, 2, 12, Top);
        b.Add(Cube, 22, 1, 12, 0, 9);
        b.Add(Cube, 22, 2, 12, 0, 9);
        b.Add("Block_RotorBlade_Recon", 22, 3, 12, Top);
        b.Add(Cube, 8, 1, 12, 0, 9);
        b.Add(Cube, 8, 2, 12, 0, 9);
        b.Add("Block_RotorBlade_Recon", 8, 3, 12, Top);
        b.Add("Block_Rudder_N1_Hawk", 14, 1, 7, Top);
        b.Add("Block_Rudder_N1_Hawk", 16, 1, 7, Top);
        b.Add("Block_Thruster_N1_Lynx", 13, 0, 5, Back);
        b.Add("Block_Thruster_N1_Lynx", 17, 0, 5, Back);
        b.Add("Block_Rail_N3_Decimator", 15, -1, 21, Under);
        b.Add("Block_Laser_N2_Hornet", 20, -1, 12, Under);
        b.Add("Block_Laser_N2_Hornet", 10, -1, 12, Under);
        b.Add("Block_Laser_N2_Hornet", 24, -1, 8, Under);
        b.Add("Block_Laser_N2_Hornet", 6, -1, 8, Under);
        b.Add("Block_Laser_N3_Blaster", 21, 1, 15, Top);
        b.Add("Block_Laser_N3_Blaster", 9, 1, 15, Top);
        b.WallSide(T4, true, 17, 1, 19, 23);
        b.WallSide(T4, false, 13, 1, 19, 23);
        b.WallSide(T2, true, 27, 0, 6, 8);
        b.WallSide(T2, false, 3, 0, 6, 8);
        b.WallBack(T2, 23, 26, 0, 5);
        b.WallBack(T2, 4, 7, 0, 5);
    }

    // Corbeau bombardier : fuselage 3 x 12 sur deux etages, tete de corbeau a bec sur la verriere,
    // grande aile de 25 cases et aile arriere en tandem, dix rotors Assault sur mats Rod, derives Hawk,
    // propulseur Leopard, Bombarder sous le ventre, quatre Disruptor sous l'aile, quatre Hornet sur
    // l'aile, nez blinde T4, flancs du cockpit T4, saumons d'aile T4, bord d'attaque et arriere en T2
    private static void BuildPlasmaRaven(Builder b)
    {
        b.Slab(14, 16, 0, 9, 20, 1);
        b.Slab(14, 16, 1, 13, 20, 1);
        b.Slab(15, 15, 2, 16, 19, 0);
        b.Add(Slope, 14, 2, 17, 0, 0, 1);
        b.Add(Slope, 16, 2, 17, 0, 0, 3);
        b.Add(Slope, 14, 2, 18, 0, 0, 1);
        b.Add(Slope, 16, 2, 18, 0, 0, 3);
        b.Add(Slope, 14, 2, 19, 0, 0, 1);
        b.Add(Slope, 16, 2, 19, 0, 0, 3);
        b.Add(Slope, 15, 2, 20);
        b.Add(Cube, 15, 3, 18, 0, 1);
        b.Add(Slope, 15, 3, 19);
        b.Slab(4, 13, 0, 15, 16, 1);
        b.Slab(17, 26, 0, 15, 16, 1);
        b.Slab(5, 13, 0, 11, 11, 1);
        b.Slab(17, 25, 0, 11, 11, 1);
        b.Add(Rod, 4, 1, 15);
        b.Add(Cube, 4, 2, 15);
        b.Add("Block_RotorBlade_Assault", 4, 3, 15, Top);
        b.Add(Rod, 8, 1, 15);
        b.Add(Cube, 8, 2, 15);
        b.Add("Block_RotorBlade_Assault", 8, 3, 15, Top);
        b.Add(Rod, 12, 1, 15);
        b.Add(Cube, 12, 2, 15);
        b.Add("Block_RotorBlade_Assault", 12, 3, 15, Top);
        b.Add(Rod, 18, 1, 15);
        b.Add(Cube, 18, 2, 15);
        b.Add("Block_RotorBlade_Assault", 18, 3, 15, Top);
        b.Add(Rod, 22, 1, 15);
        b.Add(Cube, 22, 2, 15);
        b.Add("Block_RotorBlade_Assault", 22, 3, 15, Top);
        b.Add(Rod, 26, 1, 15);
        b.Add(Cube, 26, 2, 15);
        b.Add("Block_RotorBlade_Assault", 26, 3, 15, Top);
        b.Add(Rod, 6, 1, 11);
        b.Add(Cube, 6, 2, 11);
        b.Add("Block_RotorBlade_Assault", 6, 3, 11, Top);
        b.Add(Rod, 10, 1, 11);
        b.Add(Cube, 10, 2, 11);
        b.Add("Block_RotorBlade_Assault", 10, 3, 11, Top);
        b.Add(Rod, 20, 1, 11);
        b.Add(Cube, 20, 2, 11);
        b.Add("Block_RotorBlade_Assault", 20, 3, 11, Top);
        b.Add(Rod, 24, 1, 11);
        b.Add(Cube, 24, 2, 11);
        b.Add("Block_RotorBlade_Assault", 24, 3, 11, Top);
        b.Add("Block_Thruster_N3_Leopard", 15, 0, 8, Back);
        b.Add("Block_Rudder_N1_Hawk", 14, 1, 12, Top);
        b.Add("Block_Rudder_N1_Hawk", 16, 1, 12, Top);
        b.Add("Block_Plasma_N3_Bombarder", 15, -1, 18, Under);
        b.Add("Block_Plasma_N2_Disruptor", 7, -1, 15, Under);
        b.Add("Block_Plasma_N2_Disruptor", 11, -1, 15, Under);
        b.Add("Block_Plasma_N2_Disruptor", 19, -1, 15, Under);
        b.Add("Block_Plasma_N2_Disruptor", 23, -1, 15, Under);
        b.Add("Block_Laser_N2_Hornet", 6, 1, 16, Top);
        b.Add("Block_Laser_N2_Hornet", 10, 1, 16, Top);
        b.Add("Block_Laser_N2_Hornet", 20, 1, 16, Top);
        b.Add("Block_Laser_N2_Hornet", 24, 1, 16, Top);
        b.WallFront(T4, 14, 16, 1, 21);
        b.WallSide(T4, true, 17, 1, 18, 20);
        b.WallSide(T4, false, 13, 1, 18, 20);
        b.WallSide(T4, true, 27, 0, 15, 16);
        b.WallSide(T4, false, 3, 0, 15, 16);
        b.Add(T2, 26, 0, 11, Right);
        b.Add(T2, 4, 0, 11, Left);
        b.Add(T2, 5, 0, 17, Front);
        b.Add(T2, 9, 0, 17, Front);
        b.Add(T2, 21, 0, 17, Front);
        b.Add(T2, 25, 0, 17, Front);
        b.WallBack(T2, 13, 13, 0, 10);
        b.WallBack(T2, 17, 17, 0, 10);
    }

    // Citadelle volante : coque 5 x 14, pont 3 x 13 et donjon crenele portant un rotor Invader au
    // sommet, trois balanciers de chaque cote avec deux rotors Invader chacun, Bombarder en tourelle
    // avant (sur socle) et arriere (tournee vers la poupe), deux Penetrator sous le balancier avant, Hornet
    // sous les balanciers et la proue, derives Hawk, propulseur Panther, murs T4 avant, arriere et flancs,
    // cockpit blinde T2
    private static void BuildSkyFortress(Builder b)
    {
        b.Slab(13, 17, 0, 9, 22, 9);
        b.Slab(14, 16, 1, 10, 22, 0);
        b.Add(Cube, 13, 1, 22);
        b.Add(Cube, 17, 1, 22);
        b.Slopes(14, 16, 1, 9, 0, 2);
        b.Slab(14, 16, 2, 14, 16, 0);
        b.Add(Cube, 14, 3, 14, 0, 9);
        b.Add(Cube, 16, 3, 14, 0, 9);
        b.Add(Cube, 14, 3, 16, 0, 9);
        b.Add(Cube, 16, 3, 16, 0, 9);
        b.Add(Cube, 15, 3, 15);
        b.Add(Cube, 15, 4, 15);
        b.Add("Block_RotorBlade_Invader", 15, 5, 15, Top);
        b.Add(Cube, 18, 0, 9, 0, 9);
        b.Add(Cube, 19, 0, 9, 0, 9);
        b.Add(Cube, 20, 0, 9, 0, 9);
        b.Add(Cube, 21, 0, 9, 0, 9);
        b.Add(Cube, 22, 0, 9);
        b.Add(Cube, 23, 0, 9, 0, 9);
        b.Add(Cube, 24, 0, 9);
        b.Add(Cube, 6, 0, 9);
        b.Add(Cube, 7, 0, 9, 0, 9);
        b.Add(Cube, 8, 0, 9);
        b.Add(Cube, 9, 0, 9, 0, 9);
        b.Add(Cube, 10, 0, 9, 0, 9);
        b.Add(Cube, 11, 0, 9, 0, 9);
        b.Add(Cube, 12, 0, 9, 0, 9);
        b.Add(Cube, 20, 1, 9, 0, 9);
        b.Add("Block_RotorBlade_Invader", 20, 2, 9, Top);
        b.Add(Cube, 24, 1, 9, 0, 9);
        b.Add("Block_RotorBlade_Invader", 24, 2, 9, Top);
        b.Add(Cube, 10, 1, 9, 0, 9);
        b.Add("Block_RotorBlade_Invader", 10, 2, 9, Top);
        b.Add(Cube, 6, 1, 9, 0, 9);
        b.Add("Block_RotorBlade_Invader", 6, 2, 9, Top);
        b.Add(T2, 25, 0, 9, Right);
        b.Add(T2, 5, 0, 9, Left);
        b.Add(Cube, 18, 0, 15, 0, 9);
        b.Add(Cube, 19, 0, 15, 0, 9);
        b.Add(Cube, 20, 0, 15, 0, 9);
        b.Add(Cube, 21, 0, 15, 0, 9);
        b.Add(Cube, 22, 0, 15);
        b.Add(Cube, 23, 0, 15, 0, 9);
        b.Add(Cube, 24, 0, 15);
        b.Add(Cube, 6, 0, 15);
        b.Add(Cube, 7, 0, 15, 0, 9);
        b.Add(Cube, 8, 0, 15);
        b.Add(Cube, 9, 0, 15, 0, 9);
        b.Add(Cube, 10, 0, 15, 0, 9);
        b.Add(Cube, 11, 0, 15, 0, 9);
        b.Add(Cube, 12, 0, 15, 0, 9);
        b.Add(Cube, 20, 1, 15, 0, 9);
        b.Add("Block_RotorBlade_Invader", 20, 2, 15, Top);
        b.Add(Cube, 24, 1, 15, 0, 9);
        b.Add("Block_RotorBlade_Invader", 24, 2, 15, Top);
        b.Add(Cube, 10, 1, 15, 0, 9);
        b.Add("Block_RotorBlade_Invader", 10, 2, 15, Top);
        b.Add(Cube, 6, 1, 15, 0, 9);
        b.Add("Block_RotorBlade_Invader", 6, 2, 15, Top);
        b.Add(T2, 25, 0, 15, Right);
        b.Add(T2, 5, 0, 15, Left);
        b.Add(Cube, 18, 0, 21, 0, 9);
        b.Add(Cube, 19, 0, 21, 0, 9);
        b.Add(Cube, 20, 0, 21);
        b.Add(Cube, 21, 0, 21, 0, 9);
        b.Add(Cube, 22, 0, 21, 0, 9);
        b.Add(Cube, 23, 0, 21, 0, 9);
        b.Add(Cube, 24, 0, 21);
        b.Add(Cube, 6, 0, 21);
        b.Add(Cube, 7, 0, 21, 0, 9);
        b.Add(Cube, 8, 0, 21, 0, 9);
        b.Add(Cube, 9, 0, 21, 0, 9);
        b.Add(Cube, 10, 0, 21);
        b.Add(Cube, 11, 0, 21, 0, 9);
        b.Add(Cube, 12, 0, 21, 0, 9);
        b.Add(Cube, 20, 1, 21, 0, 9);
        b.Add("Block_RotorBlade_Invader", 20, 2, 21, Top);
        b.Add(Cube, 24, 1, 21, 0, 9);
        b.Add("Block_RotorBlade_Invader", 24, 2, 21, Top);
        b.Add(Cube, 10, 1, 21, 0, 9);
        b.Add("Block_RotorBlade_Invader", 10, 2, 21, Top);
        b.Add(Cube, 6, 1, 21, 0, 9);
        b.Add("Block_RotorBlade_Invader", 6, 2, 21, Top);
        b.Add(T2, 25, 0, 21, Right);
        b.Add(T2, 5, 0, 21, Left);
        b.Add(Cube, 15, 0, 8);
        b.Add(Cube, 15, 0, 7);
        b.Add("Block_Thruster_N2_Panther", 15, 0, 6, Back);
        b.Add(Cube, 15, 2, 18);
        b.Add("Block_Plasma_N3_Bombarder", 15, 3, 18, Top);
        b.Add("Block_Plasma_N3_Bombarder", 15, 2, 12, Top, 0, 2);
        b.Add("Block_Rail_N2_Penetrator", 20, -1, 21, Under);
        b.Add("Block_Rail_N2_Penetrator", 10, -1, 21, Under);
        b.Add("Block_Laser_N2_Hornet", 22, -1, 9, Under);
        b.Add("Block_Laser_N2_Hornet", 8, -1, 9, Under);
        b.Add("Block_Laser_N2_Hornet", 22, -1, 15, Under);
        b.Add("Block_Laser_N2_Hornet", 8, -1, 15, Under);
        b.Add("Block_Laser_N2_Hornet", 14, -1, 21, Under);
        b.Add("Block_Laser_N2_Hornet", 16, -1, 21, Under);
        b.Add("Block_Laser_N2_Hornet", 14, -1, 11, Under);
        b.Add("Block_Laser_N2_Hornet", 16, -1, 11, Under);
        b.WallFront(T4, 13, 17, 1, 23);
        b.Add(T4, 13, 0, 8, Back);
        b.Add(T4, 14, 0, 8, Back);
        b.Add(T4, 16, 0, 8, Back);
        b.Add(T4, 17, 0, 8, Back);
        b.Add(Cube, 13, 1, 9, 0, 9);     // socles des gouvernails (ils etaient sur la tranche des plaques)
        b.Add(Cube, 17, 1, 9, 0, 9);
        b.Add("Block_Rudder_N1_Hawk", 13, 2, 9, Top);
        b.Add("Block_Rudder_N1_Hawk", 17, 2, 9, Top);
        b.Add(T4, 18, 0, 10, Right);
        b.Add(T4, 12, 0, 10, Left);
        b.Add(T4, 18, 0, 11, Right);
        b.Add(T4, 12, 0, 11, Left);
        b.Add(T4, 18, 0, 12, Right);
        b.Add(T4, 12, 0, 12, Left);
        b.Add(T4, 18, 0, 13, Right);
        b.Add(T4, 12, 0, 13, Left);
        b.Add(T4, 18, 0, 14, Right);
        b.Add(T4, 12, 0, 14, Left);
        b.Add(T4, 18, 0, 16, Right);
        b.Add(T4, 12, 0, 16, Left);
        b.Add(T4, 18, 0, 17, Right);
        b.Add(T4, 12, 0, 17, Left);
        b.Add(T4, 18, 0, 18, Right);
        b.Add(T4, 12, 0, 18, Left);
        b.Add(T4, 18, 0, 19, Right);
        b.Add(T4, 12, 0, 19, Left);
        b.Add(T4, 18, 0, 20, Right);
        b.Add(T4, 12, 0, 20, Left);
        b.Add(T4, 18, 0, 22, Right);
        b.Add(T4, 12, 0, 22, Left);
        b.Add(T2, 17, 2, 15, Right);
        b.Add(T2, 13, 2, 15, Left);
    }
}
