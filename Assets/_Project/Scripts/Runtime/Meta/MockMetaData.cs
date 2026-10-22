using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// DONNEES DE DEMONSTRATION DES ECRANS META (ScriptableObject)
// Asset : Resources/Meta/MockMetaData.asset, cree par
// Blockforge > Meta > Create Mock Data (et retouchable dans
// l'Inspector). Sans asset, MockMetaData.Default() fournit le
// meme jeu de donnees en memoire : chaque ecran tourne dans une
// scene vide. Contenu repris des artboards du canvas
// « Blockforge — screens ».
// =========================================================

namespace Blockforge.Meta
{
    [CreateAssetMenu(menuName = "Blockforge/Meta/Mock Data", fileName = "MockMetaData")]
    public class MockMetaData : ScriptableObject
    {
        public const string ResourcePath = "Meta/MockMetaData";

        [Serializable]
        public class StatSeed
        {
            public string label;
            public string value;
            [Range(0f, 1f)] public float ratio;

            public StatSeed(string label, string value, float ratio) { this.label = label; this.value = value; this.ratio = ratio; }
        }

        [Serializable]
        public class PlayerSeed
        {
            public string name;
            public Team team;
            public bool isLocal;
            public bool isBot = true;
            public int kills, deaths, assists, damage, healing, score;
        }

        [Serializable]
        public class RobotSeed
        {
            public string id;
            public string name;
            public string author;
            public int cpu, mass, topSpeedMph, health, downloads, price;
            public Grade grade;
            public Category mainCategory;
            public int blocks, movement, weapons, electronics;
            public int ownedParts, totalParts;
            [TextArea] public string description;
            public string updatedLabel = "updated 2 days ago";
        }

        [Serializable]
        public class StoreItemSeed
        {
            public string id;
            public string name;
            public Category category;
            public Grade grade;
            public int cpu, price, owned;
            public bool requiresResearch;
            public List<StatSeed> stats = new();
            [TextArea] public string description;
        }

        [Serializable]
        public class BundleSeed
        {
            public string id = "bundle-plasma-strike";
            public string name = "Plasma strike pack";
            [TextArea] public string description = "Two plasma launchers, a shield module, 40 hardened armour blocks and the Ember paint set. Saves 1 300 credits over buying the parts alone.";
            public int price = 4900, wasPrice = 6200;
            public float endsInHours = 54f;
            public int weapons = 2, modules = 1, blocks = 40, paintSets = 1;
        }

        [Serializable]
        public class TechNodeSeed
        {
            public string id;
            public string name;
            public Category lane;
            [Range(1, 5)] public int tier = 1;
            public Grade grade;
            public TechState state;
            public int cost;
            public int storePrice;
            [TextArea] public string description;
            public List<StatSeed> stats = new();
        }

        [Header("Portefeuille")]
        public int credits = 12480;
        public int techPoints = 1240;
        public int level = 14;
        public int xpInLevel = 2450; // apres le gain du match
        public int xpForNextLevel = 3000;

        [Header("Match de demonstration")]
        public string modeName = "Team Deathmatch";
        public string mapName = "Hex valley";
        public float durationSeconds = 552f;
        public int allyScore = 25, enemyScore = 19;
        public int rewardCredits = 1240, rewardTechPoints = 85, rewardXp = 620;
        public List<PlayerSeed> players = new();

        [Header("Workshop")]
        public int workshopTotal = 1240;
        public List<RobotSeed> robots = new();

        [Header("Store")]
        public string currentRobotName = "Race car";
        public int currentRobotCpu = 1813;
        public int cpuBudget = 2000;
        public BundleSeed bundle = new();
        public List<StoreItemSeed> items = new();

        [Header("Tech Tree")]
        public List<TechNodeSeed> nodes = new();

        // ---------- Jeu de donnees par defaut (identique aux maquettes) ----------

        public static MockMetaData Load()
        {
            // Copie : les mocks modifient leurs donnees (achats, recherches) ; l'asset reste intact
            var asset = Resources.Load<MockMetaData>(ResourcePath);
            if (asset != null)
                return Instantiate(asset);
            var fallback = CreateInstance<MockMetaData>();
            fallback.name = "MockMetaData (in memory)";
            fallback.FillDefaults();
            return fallback;
        }

        public void FillDefaults()
        {
            players.Clear();
            void P(string n, Team t, int k, int d, int a, int dmg, int heal, int s, bool local = false) =>
                players.Add(new PlayerSeed { name = n, team = t, isLocal = local, isBot = !local, kills = k, deaths = d, assists = a, damage = dmg, healing = heal, score = s });
            P("Pilot", Team.Ally, 7, 2, 5, 18420, 0, 2180, true); // remplace par SessionService.DisplayName
            P("Necro_ciclone", Team.Ally, 6, 3, 4, 15300, 1200, 1640);
            P("Refticus", Team.Ally, 5, 4, 6, 12900, 0, 1410);
            P("serz219", Team.Ally, 4, 5, 3, 9870, 2100, 1020);
            P("Starshock21", Team.Ally, 3, 5, 7, 7400, 9350, 980);
            P("EoS_Keegardhard", Team.Enemy, 6, 4, 2, 14080, 0, 1520);
            P("Dima112006", Team.Enemy, 4, 5, 3, 11200, 0, 1150);
            P("Taker195", Team.Enemy, 4, 5, 1, 10050, 3100, 1040);
            P("xRyus", Team.Enemy, 3, 5, 4, 8600, 0, 900);
            P("Zdziefan", Team.Enemy, 2, 6, 2, 6100, 400, 640);

            robots.Clear();
            void R(string id, string n, string by, int cpu, int dl, int price, Category c, Grade g, int mass, int speed, int hp,
                   int blocks, int mov, int weap, int elec, int owned, int total, string desc)
                => robots.Add(new RobotSeed
                {
                    id = id, name = n, author = by, cpu = cpu, downloads = dl, price = price, mainCategory = c, grade = g,
                    mass = mass, topSpeedMph = speed, health = hp, blocks = blocks, movement = mov, weapons = weap, electronics = elec,
                    ownedParts = owned, totalParts = total, description = desc,
                });
            R("hover-tank-mk4", "Hover tank Mk IV", "Refticus", 1640, 12480, 2400, Category.Mobility, Grade.Hardened, 9820, 148, 31400, 212, 6, 2, 1, 198, 221,
              "Low-slung hover chassis with twin plasma launchers and a rear shield plate. Fast on open ground, fragile from above.");
            R("plasma-walker", "Plasma walker", "Necro_ciclone", 1720, 9115, 2900, Category.Weapon, Grade.Hardened, 11200, 92, 36800, 240, 6, 3, 0, 180, 249,
              "Six-legged platform that keeps three plasma launchers level on any slope. Slow, steady, relentless.");
            R("rail-sniper", "Rail sniper", "serz219", 1590, 7302, 2150, Category.Weapon, Grade.Tempered, 8600, 131, 24900, 168, 4, 1, 1, 160, 174,
              "A single rail cannon on a wheeled frame built to shoot first and leave. Keep your distance.");
            R("medic-buggy", "Medic buggy", "Starshock21", 1455, 6940, 1800, Category.Electronics, Grade.Tempered, 6200, 156, 19800, 140, 4, 0, 3, 139, 147,
              "Light buggy carrying two nano dispensers. Heals faster than it fights.");
            R("siege-platform", "Siege platform", "EoS_Keegardhard", 1850, 5612, 3300, Category.Structure, Grade.Masterwork, 15400, 64, 48200, 310, 8, 2, 1, 240, 321,
              "Tracked block of armour with a rail cannon on top. Takes the hits so the team does not have to.");
            R("flak-car", "Flak car", "Dima112006", 1702, 4870, 2650, Category.Weapon, Grade.Forged, 9100, 139, 27500, 190, 4, 2, 0, 170, 196,
              "Fast wheeled car with two flak cannons. Built to swat rotors out of the sky.");
            R("drone-carrier", "Drone carrier", "Taker195", 1668, 4101, 2500, Category.Electronics, Grade.Hardened, 10400, 88, 30100, 220, 6, 1, 2, 201, 229,
              "Hover platform with a radar and a shield module. Sees everything, hides nothing.");
            R("laser-hover", "Laser hover", "Zdziefan", 1610, 3955, 2200, Category.Mobility, Grade.Forged, 7800, 162, 22600, 176, 6, 2, 0, 170, 184,
              "Four hovers and two laser blasters. The lightest fighter on this page.");

            items.Clear();
            void I(string id, string n, Category c, Grade g, int cpu, int price, int owned, bool research, string desc, params StatSeed[] stats)
            {
                var item = new StoreItemSeed { id = id, name = n, category = c, grade = g, cpu = cpu, price = price, owned = owned, requiresResearch = research, description = desc };
                item.stats.AddRange(stats);
                items.Add(item);
            }
            I("laser-blaster", "Laser blaster", Category.Weapon, Grade.Raw, 60, 320, 8, false,
              "A rapid laser with a short reach. Cheap, light and always on target.",
              new StatSeed("Damage", "1 100", 0.22f), new StatSeed("Fire rate", "4", 0.9f), new StatSeed("Range", "140", 0.4f), new StatSeed("CPU", "60", 0.15f), new StatSeed("Mass", "320", 0.14f));
            I("rail-cannon", "Rail cannon", Category.Weapon, Grade.Tempered, 240, 2100, 1, false,
              "A long-barrel cannon that charges for a second and punches through armour in a straight line. Slow, loud, decisive.",
              new StatSeed("Damage", "7 800", 0.92f), new StatSeed("Fire rate", "0.4", 0.15f), new StatSeed("Range", "400", 0.95f), new StatSeed("CPU", "240", 0.6f), new StatSeed("Mass", "1 980", 0.7f));
            I("plasma-launcher", "Plasma launcher", Category.Weapon, Grade.Hardened, 165, 1450, 2, false,
              "Lobs a slow plasma bolt that splashes on impact. Arcs over cover; weak against fast hovers.",
              new StatSeed("Damage", "4 200", 0.78f), new StatSeed("Fire rate", "0.8", 0.3f), new StatSeed("Range", "220", 0.66f), new StatSeed("CPU", "165", 0.45f), new StatSeed("Mass", "1 240", 0.52f));
            I("flak-cannon", "Flak cannon", Category.Weapon, Grade.Forged, 140, 980, 0, false,
              "Bursts in the air near flying robots. Does little to anything on the ground.",
              new StatSeed("Damage", "2 600", 0.5f), new StatSeed("Fire rate", "1.5", 0.45f), new StatSeed("Range", "260", 0.7f), new StatSeed("CPU", "140", 0.38f), new StatSeed("Mass", "1 020", 0.42f));
            I("tesla-blade", "Tesla blade", Category.Weapon, Grade.Hardened, 120, 1600, 0, false,
              "A spinning blade that arcs lightning into anything it touches. Get close.",
              new StatSeed("Damage", "3 400", 0.64f), new StatSeed("Fire rate", "2", 0.55f), new StatSeed("Range", "12", 0.05f), new StatSeed("CPU", "120", 0.32f), new StatSeed("Mass", "860", 0.36f));
            I("ion-distorter", "Ion distorter", Category.Weapon, Grade.Masterwork, 310, 4200, 0, true,
              "Bends an enemy's energy field for a moment. Research it before it reaches the Store.",
              new StatSeed("Damage", "5 200", 0.84f), new StatSeed("Fire rate", "0.6", 0.2f), new StatSeed("Range", "300", 0.8f), new StatSeed("CPU", "310", 0.8f), new StatSeed("Mass", "2 400", 0.84f));
            I("cube", "Cube", Category.Structure, Grade.Raw, 1, 10, 480, false, "The block everything else is built on.",
              new StatSeed("Health", "600", 0.2f), new StatSeed("CPU", "1", 0.01f), new StatSeed("Mass", "10", 0.02f));
            I("hardened-plate", "Hardened plate", Category.Structure, Grade.Hardened, 4, 60, 40, false, "Thick armour plate for the side that faces the enemy.",
              new StatSeed("Health", "2 400", 0.7f), new StatSeed("CPU", "4", 0.04f), new StatSeed("Mass", "48", 0.1f));
            I("wheel", "Wheel", Category.Mobility, Grade.Raw, 20, 120, 12, false, "A steering wheel for flat ground.",
              new StatSeed("Speed", "118", 0.6f), new StatSeed("CPU", "20", 0.08f), new StatSeed("Mass", "240", 0.12f));
            I("hover-blade", "Hover blade", Category.Mobility, Grade.Forged, 35, 340, 6, false, "Floats a metre above anything. Slides in corners.",
              new StatSeed("Speed", "148", 0.74f), new StatSeed("CPU", "35", 0.12f), new StatSeed("Mass", "180", 0.1f));
            I("radar", "Radar", Category.Electronics, Grade.Raw, 40, 400, 1, false, "Shows enemies on the minimap for the whole team.",
              new StatSeed("Range", "300", 0.8f), new StatSeed("CPU", "40", 0.14f), new StatSeed("Mass", "120", 0.08f));
            I("shield-module", "Shield module", Category.Electronics, Grade.Tempered, 90, 1100, 0, true, "A bubble that absorbs one volley. Research first.",
              new StatSeed("Absorb", "6 000", 0.7f), new StatSeed("CPU", "90", 0.26f), new StatSeed("Mass", "420", 0.2f));
            I("ember-paint", "Ember paint set", Category.Cosmetic, Grade.Forged, 0, 600, 0, false, "Twelve warm tones for the spray gun.",
              new StatSeed("Colours", "12", 0.5f));

            nodes.Clear();
            void N(string id, string n, Category lane, int tier, Grade g, TechState st, int cost, int price, string desc, params StatSeed[] stats)
            {
                var node = new TechNodeSeed { id = id, name = n, lane = lane, tier = tier, grade = g, state = st, cost = cost, storePrice = price, description = desc };
                node.stats.AddRange(stats);
                nodes.Add(node);
            }
            N("t-light-armour", "Light armour", Category.Structure, 1, Grade.Raw, TechState.Researched, 0, 10, "Basic cubes and prisms.", new StatSeed("Health", "600", 0.2f), new StatSeed("CPU", "1", 0.01f));
            N("t-heavy-armour", "Heavy armour", Category.Structure, 2, Grade.Forged, TechState.Researched, 120, 40, "Denser blocks for the front line.", new StatSeed("Health", "1 400", 0.4f), new StatSeed("CPU", "2", 0.02f));
            N("hardened-plate", "Hardened plate", Category.Structure, 3, Grade.Hardened, TechState.Available, 220, 60, "Thick armour plate for the side that faces the enemy.", new StatSeed("Health", "2 400", 0.7f), new StatSeed("CPU", "4", 0.04f));
            N("t-tesla-plating", "Tesla plating", Category.Structure, 4, Grade.Hardened, TechState.Locked, 480, 140, "Armour that shocks whatever touches it.", new StatSeed("Health", "2 000", 0.6f), new StatSeed("Shock", "800", 0.4f));
            N("t-masterwork-alloy", "Masterwork alloy", Category.Structure, 5, Grade.Masterwork, TechState.Locked, 900, 320, "The lightest armour there is.", new StatSeed("Health", "3 600", 0.95f), new StatSeed("CPU", "6", 0.06f));
            N("wheel", "Wheels", Category.Mobility, 1, Grade.Raw, TechState.Researched, 0, 120, "A steering wheel for flat ground.", new StatSeed("Speed", "118", 0.6f), new StatSeed("CPU", "20", 0.08f));
            N("hover-blade", "Hovers", Category.Mobility, 2, Grade.Forged, TechState.Researched, 180, 340, "Floats a metre above anything. Slides in corners.", new StatSeed("Speed", "148", 0.74f), new StatSeed("CPU", "35", 0.12f));
            N("t-legs", "Legs", Category.Mobility, 3, Grade.Tempered, TechState.Available, 300, 520, "Insect legs that climb anything.", new StatSeed("Speed", "72", 0.3f), new StatSeed("CPU", "48", 0.16f));
            N("t-thrusters", "Thrusters", Category.Mobility, 4, Grade.Hardened, TechState.Locked, 520, 900, "Short bursts of speed in any direction.", new StatSeed("Thrust", "4 200", 0.7f), new StatSeed("CPU", "60", 0.2f));
            N("t-rotors", "Rotors", Category.Mobility, 5, Grade.Masterwork, TechState.Locked, 760, 1400, "Full flight. Fragile.", new StatSeed("Lift", "6 000", 0.85f), new StatSeed("CPU", "110", 0.3f));
            N("laser-blaster", "Laser blaster", Category.Weapon, 1, Grade.Raw, TechState.Researched, 0, 320, "A rapid laser with a short reach.", new StatSeed("Damage", "1 100", 0.22f), new StatSeed("Fire rate", "4", 0.9f), new StatSeed("Range", "140", 0.4f), new StatSeed("CPU", "60", 0.15f));
            N("plasma-launcher", "Plasma launcher", Category.Weapon, 2, Grade.Hardened, TechState.Researched, 260, 1450, "Lobs a slow plasma bolt that splashes on impact.", new StatSeed("Damage", "4 200", 0.78f), new StatSeed("Fire rate", "0.8", 0.3f), new StatSeed("Range", "220", 0.66f), new StatSeed("CPU", "165", 0.45f));
            N("rail-cannon", "Rail cannon", Category.Weapon, 3, Grade.Tempered, TechState.Available, 350, 2100, "A long-barrel cannon that charges for a second and punches through armour in a straight line. Slow, loud, decisive.", new StatSeed("Damage", "7 800", 0.92f), new StatSeed("Fire rate", "0.4", 0.15f), new StatSeed("Range", "400", 0.95f), new StatSeed("CPU", "240", 0.6f));
            N("flak-cannon", "Flak cannon", Category.Weapon, 4, Grade.Forged, TechState.Locked, 600, 980, "Bursts in the air near flying robots.", new StatSeed("Damage", "2 600", 0.5f), new StatSeed("Fire rate", "1.5", 0.45f), new StatSeed("Range", "260", 0.7f), new StatSeed("CPU", "140", 0.38f));
            N("ion-distorter", "Ion distorter", Category.Weapon, 5, Grade.Masterwork, TechState.Locked, 1100, 4200, "Bends an enemy's energy field for a moment.", new StatSeed("Damage", "5 200", 0.84f), new StatSeed("Fire rate", "0.6", 0.2f), new StatSeed("Range", "300", 0.8f), new StatSeed("CPU", "310", 0.8f));
            N("radar", "Radar", Category.Electronics, 1, Grade.Raw, TechState.Researched, 0, 400, "Shows enemies on the minimap for the whole team.", new StatSeed("Range", "300", 0.8f), new StatSeed("CPU", "40", 0.14f));
            N("shield-module", "Shield module", Category.Electronics, 2, Grade.Tempered, TechState.Available, 260, 1100, "A bubble that absorbs one volley.", new StatSeed("Absorb", "6 000", 0.7f), new StatSeed("CPU", "90", 0.26f));
            N("t-repair-module", "Repair module", Category.Electronics, 3, Grade.Tempered, TechState.Locked, 540, 1600, "Rebuilds nearby blocks over time.", new StatSeed("Heal", "900", 0.5f), new StatSeed("CPU", "110", 0.3f));
            N("t-cloak", "Cloak", Category.Electronics, 4, Grade.Hardened, TechState.Locked, 800, 2600, "Three seconds of invisibility.", new StatSeed("Duration", "3", 0.3f), new StatSeed("CPU", "150", 0.4f));
            N("t-emp-burst", "EMP burst", Category.Electronics, 5, Grade.Masterwork, TechState.Locked, 1200, 4800, "Shuts down every module in range.", new StatSeed("Range", "60", 0.45f), new StatSeed("CPU", "220", 0.6f));
        }
    }
}
