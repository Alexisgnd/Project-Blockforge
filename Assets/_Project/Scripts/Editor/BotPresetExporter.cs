using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GLTFast;
using GLTFast.Export;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// EXPORT DES PRESETS DES BOTS VERS BLENDER (apercu)
// Blockforge > Debug > Bots > Export presets to Blender
// - Assemble les 15 presets (BotRobotCatalog) exactement comme en
//   match (RobotAssembler + catalogue Data/Blocks, peinture
//   comprise), dans une scene de previsualisation (la scene
//   ouverte n'est pas touchee), en grille : ligne 1 et 2 = sol,
//   ligne 3 = air, 16 m d'ecart
// - Exporte la grille en GLB (glTFast) :
//   Tools/Blender/BotPresets/BotPresets.glb
// - Ecrit les chiffres d'equilibrage de chaque preset dans
//   Tools/Blender/BotPresets/BotPresets.json et la console [Bot] :
//   CPU, masse, vitesse estimee (RobotSpeedModel), PV
//   (resistanceHp x RobotHealth.hpScale), PV effectifs (le robot
//   meurt a destroyFraction), meilleure arme et son DPS soutenu
//   (degats x cadence, limite par la recharge d'energie de
//   RobotEnergy), indice force x sante = DPS x PV effectifs / 1000
// Le GLB sert d'apercu dans Blender : les noeuds racines portent le
// nom du preset.
// =========================================================

public static class BotPresetExporter
{
    private const string BlockDataDir = "Assets/_Project/Data/Blocks";
    private const string OutDir = "Tools/Blender/BotPresets";
    private const float Spacing = 16f;

    [System.Serializable]
    private class PresetStats
    {
        public string name, domain, role, weapon, weaponKind;
        public int row, column, blocks, cpu, massKg, kmh, hp, effectiveHp, dps, power;
        public float x, z;
    }

    [System.Serializable]
    private class Report
    {
        public string exportedAt;
        public List<PresetStats> presets = new();
    }

    [MenuItem("Blockforge/Debug/Bots/Export presets to Blender", false, 21)]
    public static void Export()
    {
        var defs = new Dictionary<string, BlockDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:BlockDefinition", new[] { BlockDataDir }))
        {
            var def = AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null)
                defs[def.name] = def;
        }
        Directory.CreateDirectory(OutDir);

        var probe = new GameObject("probe").AddComponent<RobotHealth>();
        float hpScale = probe.hpScale, destroyFraction = probe.destroyFraction;
        Object.DestroyImmediate(probe.gameObject);
        var energyProbe = new GameObject("probe").AddComponent<RobotEnergy>();
        float regen = energyProbe.regenPerSecond;
        Object.DestroyImmediate(energyProbe.gameObject);

        var preview = EditorSceneManager.NewPreviewScene();
        var roots = new List<GameObject>();
        var report = new Report { exportedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
        var log = new StringBuilder();

        int groundIndex = 0, airIndex = 0;
        foreach (var name in BotRobotCatalog.Names)
        {
            var bp = BotRobotCatalog.Build(name);
            var domain = BotRobotCatalog.DomainOf(name);
            int row, column;
            if (domain == BotDomain.Ground) { row = groundIndex / 5; column = groundIndex % 5; groundIndex++; }
            else { row = 2; column = airIndex; airIndex++; }

            var go = RobotAssembler.Build(bp, defs, 1f, null, out int placed, out int weightKg);
            go.name = name;
            var pos = new Vector3(column * Spacing, 0f, -row * Spacing);
            go.transform.position = pos;
            SceneManager.MoveGameObjectToScene(go, preview);
            roots.Add(go);

            // ---------- Chiffres ----------
            int cpu = 0;
            float hp = 0f;
            var weapons = new Dictionary<BlockDefinition, int>();
            foreach (var p in bp.blocks)
            {
                if (string.IsNullOrEmpty(p.blockId) || !defs.TryGetValue(p.blockId, out var def))
                    continue;
                cpu += def.costCpu;
                hp += Mathf.Max(1f, def.resistanceHp * hpScale);
                if (RobotLoadout.Counts(def) && RobotLoadout.Kind(def) == LoadoutKind.Weapon)
                    weapons[def] = weapons.TryGetValue(def, out int n) ? n + 1 : 1;
            }
            string bestWeapon = "-", bestKind = "-";
            float bestDps = 0f;
            foreach (var kv in weapons)
            {
                var s = WeaponFireController.Resolve(kv.Key);
                if (s.kind == WeaponFireController.Kind.None)
                    continue;
                float raw = s.damage * s.rate;
                float energyPerSecond = s.kind == WeaponFireController.Kind.Tesla ? s.cost * kv.Value : s.cost * s.rate;
                float sustained = raw * Mathf.Min(1f, regen / Mathf.Max(0.01f, energyPerSecond));
                if (sustained > bestDps)
                {
                    bestDps = sustained;
                    bestWeapon = $"{kv.Key.blockName} x{kv.Value}";
                    bestKind = s.kind.ToString();
                }
            }
            RobotSpeedModel.TryEstimateKmh(bp, defs, out int kmh);
            float ehp = hp * (1f - destroyFraction);
            var stats = new PresetStats
            {
                name = name,
                domain = domain.ToString(),
                role = BotRobotCatalog.RoleOf(name),
                weapon = bestWeapon,
                weaponKind = bestKind,
                row = row,
                column = column,
                blocks = placed,
                cpu = cpu,
                massKg = weightKg,
                kmh = kmh,
                hp = Mathf.RoundToInt(hp),
                effectiveHp = Mathf.RoundToInt(ehp),
                dps = Mathf.RoundToInt(bestDps),
                power = Mathf.RoundToInt(bestDps * ehp / 1000f),
                x = pos.x,
                z = pos.z,
            };
            report.presets.Add(stats);
            log.AppendLine($"[Bot] {name,-15} {stats.domain,-6} CPU {cpu,4}  {weightKg,6} kg  {kmh,3} km/h  PV {stats.hp,5} (eff. {stats.effectiveHp,5})  " +
                           $"DPS {stats.dps,4} ({bestWeapon})  force x sante {stats.power,4}");
        }

        File.WriteAllText(Path.Combine(OutDir, "BotPresets.json"), JsonUtility.ToJson(report, true));
        Debug.Log("[Bot] Presets exportes :\n" + log);

        // ---------- GLB ----------
        string glb = Path.GetFullPath(Path.Combine(OutDir, "BotPresets.glb"));
        var export = new GameObjectExport(
            new ExportSettings
            {
                Format = GltfFormat.Binary,
                FileConflictResolution = FileConflictResolution.Overwrite,
                ComponentMask = ComponentType.Mesh,
            },
            new GameObjectExportSettings { OnlyActiveInHierarchy = true });
        export.AddScene(roots.ToArray(), "BotPresets");
        var task = export.SaveToFileAndDispose(glb);

        void Wait()
        {
            if (!task.IsCompleted)
                return;
            EditorApplication.update -= Wait;
            bool ok = !task.IsFaulted && task.Result;
            foreach (var r in roots)
                if (r != null) Object.DestroyImmediate(r);
            EditorSceneManager.ClosePreviewScene(preview);
            if (ok)
                Debug.Log($"[Bot] GLB des presets : {glb}");
            else
                Debug.LogError($"[Bot] Export GLB des presets echoue : {(task.Exception != null ? task.Exception.GetBaseException().Message : "glTFast a refuse l'export")}");
        }
        EditorApplication.update += Wait;
    }
}
