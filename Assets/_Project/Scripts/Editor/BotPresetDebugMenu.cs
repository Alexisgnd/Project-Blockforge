using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// =========================================================
// MENU DEBUG DES ROBOTS DES BOTS (editeur)
// Blockforge > Debug > Bots :
// - Pool : Mode default / Ground and air / Ground only / Air
//   only — force le pool des robots des bots pour les tests
//   (PlayerPrefs BotRobotCatalog.PoolOverrideKey, lu par
//   MatchIntroData.Bots et TdmMatch) ; « Mode default » rend la
//   main au champ botPool du GameModeDefinition.
// - Validate presets : assemble les 15 presets avec le catalogue
//   des blocs (Data/Blocks) et verifie pour chacun : blocs
//   inconnus, nombre de blocs construits par RobotAssembler,
//   chevauchements de modeles (boites LocalBox tournees), pieces
//   detachees (cases d'empreinte qui ne se touchent pas),
//   types d'armes / modules (RobotLoadout : 3 au plus), CPU,
//   masse, vitesse estimee (RobotSpeedModel), et une empreinte
//   (nombre, CPU, masse, somme de controle) a comparer avec la
//   conception. Regles de pose : support de chaque bloc accroche
//   (la case derriere sa face d'ancrage doit etre occupee), rien
//   pose ni colle sur un propulseur (BlockMountRules), disque des
//   pales de chaque rotor libre (rayon mesure sur le mesh), et
//   contact reel des modeles : la face d'ancrage du modele d'un bloc
//   accroche repose sur le modele d'un porteur (pas seulement sur
//   une case reservee vide). Rapport : console ([Bot]) et
//   Library/BotPresetReport.txt.
// =========================================================

public static class BotPresetDebugMenu
{
    private const string Root = "Blockforge/Debug/Bots/";
    private const string BlockDataDir = "Assets/_Project/Data/Blocks";
    private const string ReportPath = "Library/BotPresetReport.txt";

    // ---------- Pool force ----------

    [MenuItem(Root + "Pool : mode default", false, 1)]
    private static void PoolDefault() => SetPool(-1);
    [MenuItem(Root + "Pool : mode default", true)]
    private static bool PoolDefaultCheck() { Menu.SetChecked(Root + "Pool : mode default", BotRobotCatalog.PoolOverride < 0); return true; }

    [MenuItem(Root + "Pool : ground and air", false, 2)]
    private static void PoolMixed() => SetPool((int)BotRobotPool.Mixed);
    [MenuItem(Root + "Pool : ground and air", true)]
    private static bool PoolMixedCheck() { Menu.SetChecked(Root + "Pool : ground and air", BotRobotCatalog.PoolOverride == (int)BotRobotPool.Mixed); return true; }

    [MenuItem(Root + "Pool : ground only", false, 3)]
    private static void PoolGround() => SetPool((int)BotRobotPool.GroundOnly);
    [MenuItem(Root + "Pool : ground only", true)]
    private static bool PoolGroundCheck() { Menu.SetChecked(Root + "Pool : ground only", BotRobotCatalog.PoolOverride == (int)BotRobotPool.GroundOnly); return true; }

    [MenuItem(Root + "Pool : air only", false, 4)]
    private static void PoolAir() => SetPool((int)BotRobotPool.AirOnly);
    [MenuItem(Root + "Pool : air only", true)]
    private static bool PoolAirCheck() { Menu.SetChecked(Root + "Pool : air only", BotRobotCatalog.PoolOverride == (int)BotRobotPool.AirOnly); return true; }

    private static void SetPool(int value)
    {
        BotRobotCatalog.PoolOverride = value;
        Debug.Log("[Bot] Pool des robots des bots : " +
                  (value < 0 ? "celui du mode (GameModeDefinition.botPool)" : BotRobotCatalog.PoolLabel((BotRobotPool)value)) +
                  ". Pris en compte au prochain « Play with bots » (ou Play direct dans Map_MARS).");
    }

    // ---------- Validation ----------

    [MenuItem(Root + "Validate presets", false, 20)]
    public static void Validate()
    {
        var defs = LoadDefs();

        var report = new StringBuilder();
        report.AppendLine($"# Presets des bots — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} — {defs.Count} blocs au catalogue");
        report.AppendLine("nom | domaine | blocs | construits | CPU | masse kg | km/h | armes | somme | problemes");
        int failed = 0;
        var cells = new List<Vector3Int>();
        foreach (var name in BotRobotCatalog.Names)
        {
            var bp = BotRobotCatalog.Build(name);
            var problems = new List<string>();
            int cpu = 0, kg = 0;
            long checksum = 0;
            var items = new List<(PlacedBlock p, BlockDefinition def, Bounds box, HashSet<Vector3Int> cells)>();
            foreach (var p in bp.blocks)
            {
                checksum += p.x * 7 + p.y * 131 + p.z * 1031 + p.rotation * 17 + p.colorIndex * 3 + (p.blockId?.Length ?? 0) * 13;
                if (string.IsNullOrEmpty(p.blockId) || !defs.TryGetValue(p.blockId, out var def))
                {
                    problems.Add("bloc inconnu " + p.blockId);
                    continue;
                }
                cpu += def.costCpu;
                kg += def.weightKg;
                BlockFootprint.GetCells(def, new Vector3Int(p.x, p.y, p.z), p.rotation, cells);
                items.Add((p, def, WorldBox(def, p), new HashSet<Vector3Int>(cells)));
            }

            // Chevauchements des modeles
            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                {
                    float v = Overlap(items[i].box, items[j].box);
                    if (v > 0.05f)
                        problems.Add($"chevauchement {Short(items[i].p)} / {Short(items[j].p)} ({v:0.00} case)");
                }

            // Pieces detachees : graphe des cases qui se touchent par une face
            if (items.Count > 0)
            {
                var owner = new Dictionary<Vector3Int, List<int>>();
                for (int i = 0; i < items.Count; i++)
                    foreach (var c in items[i].cells)
                    {
                        if (!owner.TryGetValue(c, out var l)) owner[c] = l = new List<int>();
                        l.Add(i);
                    }
                CheckMounting(items, owner, problems);
                CheckModelContact(items, problems);
                CheckRods(items, owner, problems);
                var dirs = new[] { Vector3Int.zero, Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
                var seen = new HashSet<int> { 0 };
                var stack = new Stack<int>();
                stack.Push(0);
                while (stack.Count > 0)
                {
                    int u = stack.Pop();
                    foreach (var c in items[u].cells)
                        foreach (var d in dirs)
                            if (owner.TryGetValue(c + d, out var others))
                                foreach (int o in others)
                                    if (seen.Add(o)) stack.Push(o);
                }
                if (seen.Count != items.Count)
                {
                    var loose = Enumerable.Range(0, items.Count).Where(i => !seen.Contains(i)).Select(i => Short(items[i].p));
                    problems.Add("detache : " + string.Join(", ", loose));
                }
            }

            // Armes et modules (regle des 3 types)
            var types = items.Where(it => RobotLoadout.Counts(it.def)).Select(it => it.def).Distinct().ToList();
            if (types.Count > RobotLoadout.MaxTypes)
                problems.Add($"{types.Count} types d'armes / modules (max {RobotLoadout.MaxTypes})");
            if (!types.Any(t => RobotLoadout.Kind(t) == LoadoutKind.Weapon))
                problems.Add("aucune arme");
            // Pieces verrouillees dans cette version (modules, arme healer) : inertes en match
            var locked = items.Where(it => RobotLoadout.IsLocked(it.def)).Select(it => it.def.blockName).Distinct().ToList();
            if (locked.Count > 0)
                problems.Add("verrouille : " + string.Join(", ", locked));

            // Domaine coherent : un robot volant a un rotor, un robot au sol a des appuis
            bool rotor = items.Any(it => RobotSpeedModel.TryClassify(it.def, out var k) && k == LocomotionKind.Rotor);
            bool ground = items.Any(it => RobotSpeedModel.TryClassify(it.def, out var k) && RobotSpeedModel.IsGround(k));
            var domain = BotRobotCatalog.DomainOf(name);
            if (domain == BotDomain.Air && !rotor) problems.Add("robot volant sans rotor");
            if (domain == BotDomain.Ground && (!ground || rotor)) problems.Add("robot au sol sans appui (ou avec rotor)");

            RobotSpeedModel.TryEstimateKmh(bp, defs, out int kmh);

            // Assemblage reel (comme TdmMatch) : disque des rotors mesure sur le mesh, puis destruction
            var go = RobotAssembler.Build(bp, defs, 1f, null, out int placed, out int weight);
            CheckRotorDiscs(go, items, problems);
            CheckMeshContact(go, items, problems);
            // Hauteur : un robot au sol plus haut que la hauteur libre du NavMesh est
            // envoye sous des ponts ou il se coince (constate le 22/10)
            float height = RobotHeight(go);
            if (domain == BotDomain.Ground && height > MatchNavMesh.AgentHeight - 0.3f)
                problems.Add($"trop haut pour les ponts : {height:0.0} m (NavMesh : {MatchNavMesh.AgentHeight:0.0} m libres, marge 0,3 m)");
            Object.DestroyImmediate(go);
            if (placed != bp.blocks.Count)
                problems.Add($"{bp.blocks.Count - placed} bloc(s) non construits");

            var weapons = string.Join(" + ", types.Select(t => $"{t.blockName} x{items.Count(it => it.def == t)}"));
            string line = $"{name} | {domain} | {bp.blocks.Count} | {placed} | {cpu} | {kg} | {kmh} | {weapons} | {checksum} | " +
                          (problems.Count == 0 ? "OK" : string.Join(" ; ", problems));
            report.AppendLine(line);
            if (problems.Count > 0)
            {
                failed++;
                Debug.LogWarning("[Bot] Preset " + line);
            }
            else
            {
                Debug.Log("[Bot] Preset " + line);
            }
        }
        report.AppendLine(failed == 0 ? "# Tous les presets sont valides." : $"# {failed} preset(s) a corriger.");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log($"[Bot] Validation des presets : {(failed == 0 ? "tous valides" : failed + " a corriger")} — rapport {ReportPath}");
    }

    private static Dictionary<string, BlockDefinition> LoadDefs()
    {
        var defs = new Dictionary<string, BlockDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:BlockDefinition", new[] { BlockDataDir }))
        {
            var def = AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null)
                defs[def.name] = def;
        }
        return defs;
    }

    private static float RobotHeight(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0)
            return 0f;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.size.y;
    }

    // Tiges droites (Rod_Court, Rod_Long) : ce sont des montants verticaux (pied et
    // chapeau), qui ne se tournent qu'en lacet. Posees en rangee horizontale, elles
    // flottent a cote de leurs voisins (constate le 22/10 sur Sky fortress et Iron
    // condor) : une tige droite doit tenir un bloc sous son pied ET un sous son chapeau.
    private static void CheckRods(List<(PlacedBlock p, BlockDefinition def, Bounds box, HashSet<Vector3Int> cells)> items,
                                  Dictionary<Vector3Int, List<int>> owner, List<string> problems)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var name = items[i].def.name;
            if (name != "Block_Rod_Court" && name != "Block_Rod_Long")
                continue;
            int bottom = items[i].cells.Min(c => c.y), top = items[i].cells.Max(c => c.y);
            var any = items[i].cells.First();
            bool below = owner.TryGetValue(new Vector3Int(any.x, bottom - 1, any.z), out var b) && b.Any(j => j != i);
            bool above = owner.TryGetValue(new Vector3Int(any.x, top + 1, any.z), out var a) && a.Any(j => j != i);
            if (!below || !above)
                problems.Add($"tige dans le vide : {Short(items[i].p)} (rien {(!below && !above ? "dessous ni dessus" : !below ? "dessous" : "dessus")} : une tige droite est un montant vertical)");
        }
    }

    // ---------- Rendus PNG ----------
    // Photographie chaque preset (RobotThumbnailRenderer, dans la scene ouverte :
    // son eclairage, un calque reserve) : Library/BotPresetRender_<nom>.png.
    // Vue 3/4 avant, pour verifier a l'oeil ce que la validation mesure.
    [MenuItem(Root + "Render presets", false, 22)]
    private static void RenderPresets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Bot] Render presets : quittez le Play mode d'abord.");
            return;
        }
        var defs = LoadDefs();
        var stage = new Vector3(0f, 400f, 0f);
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        int done = 0;
        foreach (var name in BotRobotCatalog.Names)
        {
            var bp = BotRobotCatalog.Build(name);
            var rt = RobotThumbnailRenderer.Render(bp, defs, stage, 1600, 900);
            if (rt == null)
                continue;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"Library/BotPresetRender_{name.Replace(' ', '_')}.png", tex.EncodeToPNG());
            rt.Release();
            done++;
        }
        Object.DestroyImmediate(tex);
        Debug.Log($"[Bot] Render presets : {done} rendu(s) dans Library/BotPresetRender_*.png");
    }

    // ---------- Hauteurs et passages sous les ponts ----------
    // Hauteur de chaque preset assemble (boite des renderers) et jour sous les
    // surplombs de la scene ouverte (ponts) : pour chaque point d'une grille de
    // 2 m, chaque surface touchee sous une autre donne la hauteur libre entre
    // elle et le dessous de celle du dessus. Rapport : Library/BotHeightReport.txt.
    [MenuItem(Root + "Measure heights and bridges", false, 23)]
    private static void MeasureHeights()
    {
        var defs = LoadDefs();
        var sb = new StringBuilder();
        sb.AppendLine($"# Hauteurs des presets et jour sous les ponts — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        var heights = new List<(string name, BotDomain domain, float h)>();
        foreach (var name in BotRobotCatalog.Names)
        {
            var go = RobotAssembler.Build(BotRobotCatalog.Build(name), defs, 1f, null, out _, out _);
            var rs = go.GetComponentsInChildren<Renderer>();
            float h = 0f;
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                h = b.size.y;
            }
            Object.DestroyImmediate(go);
            heights.Add((name, BotRobotCatalog.DomainOf(name), h));
        }
        foreach (var e in heights.OrderByDescending(e => e.h))
            sb.AppendLine($"{e.name,-14} {e.domain,-6} {e.h:0.0} m");

        int mask = ~((1 << RobotTestSpawner.RobotLayer) | (1 << 2));
        var cols = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude).Where(c => c.enabled && !c.isTrigger && ((1 << c.gameObject.layer) & mask) != 0).ToArray();
        if (cols.Length == 0)
        {
            sb.AppendLine("Aucun collider de decor dans la scene ouverte : ouvrez Map_MARS.");
        }
        else
        {
            var world = cols[0].bounds;
            foreach (var c in cols) world.Encapsulate(c.bounds);
            var gaps = new List<(Vector3 at, float gap, string under)>();
            for (float x = world.min.x; x <= world.max.x; x += 2f)
                for (float z = world.min.z; z <= world.max.z; z += 2f)
                {
                    var top = new Vector3(x, world.max.y + 5f, z);
                    var hits = Physics.RaycastAll(top, Vector3.down, world.size.y + 10f, mask, QueryTriggerInteraction.Ignore)
                                      .OrderBy(h => h.distance).ToArray();
                    for (int i = 1; i < hits.Length; i++)
                    {
                        if (Vector3.Angle(hits[i].normal, Vector3.up) > 40f)
                            continue;   // paroi, pas un sol
                        var floor = hits[i].point;
                        if (Physics.Raycast(floor + Vector3.up * 0.05f, Vector3.up, out var ceil, 60f, mask, QueryTriggerInteraction.Ignore))
                            gaps.Add((floor, ceil.distance + 0.05f, ceil.collider.name));
                    }
                }
            // Un passage = un point au sol avec un plafond ; on ne garde que ceux ou un robot
            // tiendrait en largeur (le plafond se prolonge sur 4 m) pour ecarter les fentes
            var passages = gaps.Where(g => g.gap > 1.5f).ToList();
            sb.AppendLine($"\n{passages.Count} point(s) de sol sous un surplomb (grille 2 m) ; scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            foreach (var bucket in new[] { 4f, 6f, 8f, 10f, 12f, 14f, 17f, 20f, 25f, 60f })
                sb.AppendLine($"  jour < {bucket,4:0} m : {passages.Count(g => g.gap < bucket)}");
            foreach (var g in passages.Where(g => g.gap < 25f).OrderBy(g => g.gap).Take(25))
                sb.AppendLine($"  ({g.at.x:0}, {g.at.y:0}, {g.at.z:0}) jour {g.gap:0.0} m sous {g.under}");
            float tallestGround = heights.Where(e => e.domain == BotDomain.Ground).Select(e => e.h).DefaultIfEmpty(0f).Max();
            sb.AppendLine($"\nPlus grand robot au sol : {tallestGround:0.0} m ; NavMesh agentHeight {MatchNavMesh.AgentHeight:0.0} m ; " +
                          $"points de passage plus bas que lui : {passages.Count(g => g.gap < tallestGround)}");
        }
        File.WriteAllText("Library/BotHeightReport.txt", sb.ToString());
        Debug.Log("[Bot] Hauteurs et ponts : Library/BotHeightReport.txt\n" + sb);
    }

    // ---------- Regles de pose ----------

    // Bloc accroche (face 1..6) : la case derriere sa face d'ancrage (case visee - normale)
    // est son support ; elle doit exister et ne pas etre un propulseur ni une tige. Rien d'autre que
    // son propre support ne touche un propulseur (pas de cube, d'aile ou d'arme colle dessus).
    private static void CheckMounting(List<(PlacedBlock p, BlockDefinition def, Bounds box, HashSet<Vector3Int> cells)> items,
                                      Dictionary<Vector3Int, List<int>> owner, List<string> problems)
    {
        var supportOf = new int[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            supportOf[i] = -1;
            int face = BlockFootprint.FaceOf(items[i].p.rotation);
            if (face == BlockFootprint.NaturalFace)
                continue;
            var anchor = new Vector3Int(items[i].p.x, items[i].p.y, items[i].p.z);
            var support = anchor - BlockFootprint.FaceNormals[face];
            if (!owner.TryGetValue(support, out var under) || under.Count == 0)
            {
                problems.Add($"sans support : {Short(items[i].p)}");
                continue;
            }
            supportOf[i] = under[0];
            if (BlockMountRules.IsThruster(items[under[0]].def))
                problems.Add($"pose sur un propulseur : {Short(items[i].p)} sur {Short(items[under[0]].p)}");
            else if (BlockMountRules.IsRod(items[under[0]].def))
                problems.Add($"pose sur une tige (seul le chassis va sur une tige) : {Short(items[i].p)} sur {Short(items[under[0]].p)}");
        }

        var dirs = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
        for (int t = 0; t < items.Count; t++)
        {
            if (!BlockMountRules.IsThruster(items[t].def))
                continue;
            // Le contact par la face d'ancrage (plan de fixation, cote support) est normal
            var mountSide = -BlockFootprint.FaceNormals[BlockFootprint.FaceOf(items[t].p.rotation)];
            var touching = new HashSet<int>();
            foreach (var c in items[t].cells)
                foreach (var d in dirs)
                    if (d != mountSide && owner.TryGetValue(c + d, out var others))
                        foreach (int o in others)
                            if (o != t && o != supportOf[t] && supportOf[o] != t)
                                touching.Add(o);
            foreach (int o in touching)
                problems.Add($"colle a un propulseur : {Short(items[o].p)} contre {Short(items[t].p)}");
        }
    }

    // Contact reel des modeles : la face d'ancrage du MODELE (boite LocalBox tournee, celle
    // qui donne l'echelle du mesh) doit reposer sur le modele d'un bloc porteur, avec une
    // vraie surface d'appui. Le test des cases (CheckMounting) ne suffit pas : les cases
    // reservees sont arrondies a un nombre impair (OccupancySize) et une plaque T4 reserve
    // 3 cases de haut ; une arme posee sur une de ces cases vides passe le test des cases
    // mais flotte a l'ecran (constate en match le 22/10).
    private const float ContactTolerance = 0.06f;   // jeu admis entre deux faces (case)
    private const float MinBearing = 0.2f;          // recouvrement minimal sur chaque axe de la face (case)

    private static void CheckModelContact(List<(PlacedBlock p, BlockDefinition def, Bounds box, HashSet<Vector3Int> cells)> items,
                                          List<string> problems)
    {
        for (int i = 0; i < items.Count; i++)
        {
            int face = BlockFootprint.FaceOf(items[i].p.rotation);
            if (face == BlockFootprint.NaturalFace)
                continue;   // chassis : relie par ses cases pleines (1 x 1 x 1)
            var n = BlockFootprint.FaceNormals[face];
            int axis = n.x != 0 ? 0 : n.y != 0 ? 1 : 2;
            float sign = n[axis];
            var bi = items[i].box;
            // Plan de la face d'ancrage du modele, cote support
            float plane = sign > 0f ? bi.min[axis] : bi.max[axis];
            bool resting = false;
            for (int j = 0; j < items.Count && !resting; j++)
            {
                if (j == i || !BlockMountRules.CanSupport(items[j].def, items[i].def))
                    continue;
                var bj = items[j].box;
                // Le porteur est du cote du support et atteint le plan (a la tolerance pres)
                bool reaches = sign > 0f
                    ? bj.min[axis] < plane - ContactTolerance && bj.max[axis] > plane - ContactTolerance
                    : bj.max[axis] > plane + ContactTolerance && bj.min[axis] < plane + ContactTolerance;
                if (!reaches)
                    continue;
                bool bearing = true;
                for (int k = 0; k < 3; k++)
                {
                    if (k == axis)
                        continue;
                    float overlap = Mathf.Min(bi.max[k], bj.max[k]) - Mathf.Max(bi.min[k], bj.min[k]);
                    if (overlap < MinBearing)
                        bearing = false;
                }
                resting = bearing;
            }
            if (!resting)
                problems.Add($"flotte : {Short(items[i].p)} (sa face d'ancrage ne touche aucun modele porteur)");
        }
    }

    // Contact des MAILLAGES (robot reellement assemble) : les modeles ne remplissent pas
    // leur boite (une pente n'a qu'une moitie de cube, une arme est un fut sur un socle,
    // une plaque inclinee ne touche son plan de fixation que par un bord). Pour chaque bloc
    // accroche, on echantillonne une grille sur chaque face partagee avec un porteur (case
    // derriere une case de son empreinte, du cote de son support). Le bloc tient si en au
    // moins un point de ces faces son mesh ET celui du porteur sont a moins de MeshGap :
    // c'est la definition visuelle de « pose sur ». Sinon on rapporte le plus petit jour
    // mesure entre le mesh du bloc et une face porteuse (ce que voit le joueur).
    private const float MeshGap = 0.06f;      // 6 cm (une case = 1 m)
    private const int FaceGrid = 9;           // 9 x 9 points par face partagee
    private const float PlaneBand = 0.25f;    // seuls les triangles a moins de 25 cm du plan comptent

    private sealed class MeshData
    {
        public readonly List<Vector3> points = new();
        public readonly List<Vector3> tris = new();   // 3 sommets par triangle (monde)
        public Bounds bounds;
        public bool any;
    }

    private static void CheckMeshContact(GameObject robot, List<(PlacedBlock p, BlockDefinition def, Bounds box, HashSet<Vector3Int> cells)> items,
                                         List<string> problems)
    {
        if (robot == null)
            return;
        var views = robot.GetComponentsInChildren<PlacedBlockView>(true);
        var data = new Dictionary<PlacedBlockView, MeshData>();
        foreach (var v in views)
            data[v] = Collect(v);
        // Case -> bloc qui la reserve (empreinte complete)
        var byCell = new Dictionary<Vector3Int, PlacedBlockView>();
        foreach (var v in views)
            if (v.cells != null)
                foreach (var c in v.cells)
                    byCell.TryAdd(c, v);

        for (int i = 0; i < items.Count; i++)
        {
            int face = BlockFootprint.FaceOf(items[i].p.rotation);
            if (face == BlockFootprint.NaturalFace)
                continue;
            var anchor = new Vector3Int(items[i].p.x, items[i].p.y, items[i].p.z);
            var view = views.FirstOrDefault(v => v.cell == anchor && v.definition == items[i].def);
            if (view == null || !data[view].any)
                continue;
            var n = BlockFootprint.FaceNormals[face];
            Vector3 toSupport = robot.transform.TransformDirection(-(Vector3)n).normalized;
            // Deux axes du plan de la face
            Vector3 u = Mathf.Abs(toSupport.x) > 0.5f ? Vector3.up : Vector3.right;
            Vector3 w = Vector3.Cross(toSupport, u).normalized;
            u = Vector3.Cross(w, toSupport).normalized;

            float bestGap = float.MaxValue;
            string bestOn = null;
            bool held = false;
            var ownCells = view.cells != null ? new HashSet<Vector3Int>(view.cells) : new HashSet<Vector3Int> { anchor };
            foreach (var c in ownCells)
            {
                var s = c - n;
                if (ownCells.Contains(s) || !byCell.TryGetValue(s, out var sv) || sv == view)
                    continue;
                if (!BlockMountRules.CanSupport(sv.definition, items[i].def) || !data[sv].any)
                    continue;
                // Centre monde de la case c (racine du bloc = centre de la case visee)
                Vector3 cellCenter = view.transform.position + robot.transform.TransformVector((Vector3)(c - anchor));
                Vector3 faceCenter = cellCenter + toSupport * 0.5f;
                float planeD = Vector3.Dot(faceCenter, toSupport);
                var mineNear = NearPlane(data[view].tris, toSupport, planeD);
                var supNear = NearPlane(data[sv].tris, toSupport, planeD);
                if (mineNear.Count == 0)
                    continue;
                for (int a = 0; a < FaceGrid && !held; a++)
                    for (int b = 0; b < FaceGrid && !held; b++)
                    {
                        float fu = -0.48f + 0.96f * a / (FaceGrid - 1);
                        float fw = -0.48f + 0.96f * b / (FaceGrid - 1);
                        Vector3 g = faceCenter + u * fu + w * fw;
                        float dMine = PointToTris(g, mineNear, float.MaxValue);
                        if (dMine < bestGap)
                        {
                            bestGap = dMine;
                            bestOn = $"{sv.definition.name.Replace("Block_", "")}@({s.x},{s.y},{s.z})";
                        }
                        if (dMine <= MeshGap && PointToTris(g, supNear, MeshGap * 2f) <= MeshGap)
                            held = true;
                    }
                if (held)
                    break;
            }
            if (held)
                continue;
            problems.Add(bestOn == null
                ? $"flotte (mesh) : {Short(items[i].p)} (aucun porteur ne touche son plan de fixation)"
                : $"flotte (mesh) : {Short(items[i].p)} (jour de {bestGap * 100f:0} cm avec {bestOn})");
        }
    }

    // Triangles dont au moins un sommet est a moins de PlaneBand du plan
    private static List<Vector3> NearPlane(List<Vector3> tris, Vector3 axis, float planeD)
    {
        var r = new List<Vector3>();
        for (int t = 0; t + 2 < tris.Count; t += 3)
        {
            float d0 = Vector3.Dot(tris[t], axis) - planeD, d1 = Vector3.Dot(tris[t + 1], axis) - planeD, d2 = Vector3.Dot(tris[t + 2], axis) - planeD;
            float lo = Mathf.Min(d0, Mathf.Min(d1, d2)), hi = Mathf.Max(d0, Mathf.Max(d1, d2));
            if (lo > PlaneBand || hi < -PlaneBand)
                continue;
            r.Add(tris[t]); r.Add(tris[t + 1]); r.Add(tris[t + 2]);
        }
        return r;
    }

    private static float PointToTris(Vector3 p, List<Vector3> tris, float bestSoFar)
    {
        return MinDistance(new List<Vector3>(1) { p }, tris, bestSoFar);
    }

    // Sommets et triangles (monde) des meshes d'un bloc
    private static MeshData Collect(PlacedBlockView view)
    {
        var d = new MeshData();
        void Add(Mesh mesh, Matrix4x4 m)
        {
            if (mesh == null)
                return;
            var verts = mesh.vertices;
            if (verts == null || verts.Length == 0)
                return;
            var world = new Vector3[verts.Length];
            for (int k = 0; k < verts.Length; k++)
            {
                world[k] = m.MultiplyPoint3x4(verts[k]);
                if (!d.any) { d.bounds = new Bounds(world[k], Vector3.zero); d.any = true; }
                else d.bounds.Encapsulate(world[k]);
            }
            d.points.AddRange(world);
            var tri = mesh.triangles;
            for (int k = 0; k + 2 < tri.Length; k += 3)
            {
                d.tris.Add(world[tri[k]]);
                d.tris.Add(world[tri[k + 1]]);
                d.tris.Add(world[tri[k + 2]]);
            }
        }
        foreach (var mf in view.GetComponentsInChildren<MeshFilter>(true))
            Add(mf.sharedMesh, mf.transform.localToWorldMatrix);
        foreach (var smr in view.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Add(smr.sharedMesh, smr.transform.localToWorldMatrix);
        return d;
    }

    // Plus petite distance entre des points et des triangles (arret des que < MeshGap)
    private static float MinDistance(List<Vector3> points, List<Vector3> tris, float bestSoFar)
    {
        float best = bestSoFar;
        for (int t = 0; t + 2 < tris.Count; t += 3)
        {
            Vector3 a = tris[t], b = tris[t + 1], c = tris[t + 2];
            Vector3 min = Vector3.Min(a, Vector3.Min(b, c)), max = Vector3.Max(a, Vector3.Max(b, c));
            foreach (var p in points)
            {
                // Rejet rapide : le point est loin de la boite du triangle
                float dx = Mathf.Max(0f, Mathf.Max(min.x - p.x, p.x - max.x));
                float dy = Mathf.Max(0f, Mathf.Max(min.y - p.y, p.y - max.y));
                float dz = Mathf.Max(0f, Mathf.Max(min.z - p.z, p.z - max.z));
                if (dx * dx + dy * dy + dz * dz >= best * best)
                    continue;
                float dist = Vector3.Distance(p, ClosestPointOnTriangle(p, a, b, c));
                if (dist < best)
                {
                    best = dist;
                    if (best <= MeshGap)
                        return best;
                }
            }
        }
        return best;
    }

    // Point le plus proche sur un triangle (Ericson, Real-Time Collision Detection)
    private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        float denom = 1f / (va + vb + vc);
        float v = vb * denom, w = vc * denom;
        return a + ab * v + ac * w;
    }

    // Disque des pales : rayon = plus grande distance a l'axe du moyeu des sommets du mesh du
    // rotor, tranche = hauteur des sommets de pale (au-dela de 60 % du rayon). Tout autre bloc
    // dont la boite entre dans ce cylindre gene l'helice.
    private static void CheckRotorDiscs(GameObject robot, List<(PlacedBlock p, BlockDefinition def, Bounds box, HashSet<Vector3Int> cells)> items,
                                        List<string> problems)
    {
        if (robot == null)
            return;
        var views = robot.GetComponentsInChildren<PlacedBlockView>(true);
        for (int r = 0; r < items.Count; r++)
        {
            if (!BlockMountRules.IsRotor(items[r].def))
                continue;
            var anchor = new Vector3Int(items[r].p.x, items[r].p.y, items[r].p.z);
            var view = views.FirstOrDefault(v => v.cell == anchor && v.definition == items[r].def);
            if (view == null)
                continue;

            // Axe du moyeu : axe le plus fin de la boite du rotor
            var ext = items[r].box.extents;
            int axis = ext.x <= ext.y && ext.x <= ext.z ? 0 : (ext.y <= ext.z ? 1 : 2);
            Vector3 hub = items[r].box.center;
            // Repere des cases : RobotAssembler recentre le robot, on repart donc du pivot du
            // bloc (centre de sa case visee)
            Vector3 pivot = robot.transform.InverseTransformPoint(view.transform.position);
            var points = new List<Vector3>();
            foreach (var mf in view.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                    continue;
                foreach (var v in mf.sharedMesh.vertices)
                    points.Add((Vector3)anchor + robot.transform.InverseTransformPoint(mf.transform.TransformPoint(v)) - pivot);
            }
            if (points.Count == 0)
            {
                problems.Add($"rotor sans mesh mesurable : {Short(items[r].p)}");
                continue;
            }
            // Le support du rotor (sous le moyeu) n'est pas dans le disque
            var support = anchor - BlockFootprint.FaceNormals[BlockFootprint.FaceOf(items[r].p.rotation)];
            float Radial(Vector3 q) { var d = q - hub; d[axis] = 0f; return d.magnitude; }
            float radius = points.Max(Radial);
            var blade = points.Where(q => Radial(q) > radius * 0.6f).ToList();
            float lo = blade.Min(q => q[axis]) - 0.05f, hi = blade.Max(q => q[axis]) + 0.05f;

            for (int i = 0; i < items.Count; i++)
            {
                if (i == r || items[i].cells.Contains(support))
                    continue;
                var b = items[i].box;
                if (b.max[axis] <= lo || b.min[axis] >= hi)
                    continue;
                // Point de la boite le plus proche de l'axe, dans le plan des pales
                var closest = new Vector3(Mathf.Clamp(hub.x, b.min.x, b.max.x), Mathf.Clamp(hub.y, b.min.y, b.max.y), Mathf.Clamp(hub.z, b.min.z, b.max.z));
                if (Radial(closest) < radius - 0.02f)
                    problems.Add($"dans le disque du rotor : {Short(items[i].p)} sous {Short(items[r].p)} (rayon {radius:0.0})");
            }
        }
    }

    // Boite du modele (LocalBox) tournee et posee sur sa case, en cases
    private static Bounds WorldBox(BlockDefinition def, PlacedBlock p)
    {
        var local = BlockFootprint.LocalBox(def);
        var rot = BlockFootprint.Rotation(def, p.rotation);
        var center = new Vector3(p.x, p.y, p.z);
        var b = new Bounds(center + rot * local.center, Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x,
                                     (i & 2) == 0 ? local.min.y : local.max.y,
                                     (i & 4) == 0 ? local.min.z : local.max.z);
            b.Encapsulate(center + rot * corner);
        }
        return b;
    }

    private static float Overlap(Bounds a, Bounds b)
    {
        float x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
        float y = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
        float z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
        return x > 0f && y > 0f && z > 0f ? x * y * z : 0f;
    }

    private static string Short(PlacedBlock p) => $"{p.blockId.Replace("Block_", "")}@({p.x},{p.y},{p.z})";
}
