using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// =========================================================
// SONDE DES ENDROITS OU LES BOTS SE BLOQUENT (editeur, hors Play)
// Blockforge > Debug > Bots > Probe spots : lit Library/BotProbe.request
// (une ligne par endroit : "x y z [etiquette]", ou "spawns" pour
// tous les marqueurs Marker_Spawn_*), ouvre Map_MARS si besoin,
// construit le NavMesh des bots comme en match (MatchNavMesh,
// synchrone) et, pour chaque endroit :
// - sol (collider touche, pente), relief a 5 / 15 / 30 m dans
//   8 directions, colliders dans un rayon de 8 m,
// - NavMesh : point le plus proche (et sa distance), chemin
//   complet ou non vers Marker_Center, pour l'agent normal et
//   l'agent large,
// - deux rendus PNG (vue 3/4 et vue de dessus) avec une sphere
//   rouge sur le point : Library/BotProbe_<n>_<vue>.png.
// Rapport : Library/BotProbeReport.txt et console [Bot].
// =========================================================

public static class BotSpotProbe
{
    private const string RequestPath = "Library/BotProbe.request";
    private const string ReportPath = "Library/BotProbeReport.txt";

    [MenuItem("Blockforge/Debug/Bots/Probe spots", false, 41)]
    public static void Probe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Bot] Probe spots : quittez le Play mode d'abord.");
            return;
        }
        if (!File.Exists(RequestPath))
        {
            Debug.LogError($"[Bot] Probe spots : {RequestPath} absent (une ligne par endroit : x y z [etiquette], ou spawns).");
            return;
        }
        var scene = SceneManager.GetActiveScene();
        if (scene.path != BlockforgeScenes.MapMars && scene.path != BlockforgeScenes.MapMarsSpectate)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            scene = EditorSceneManager.OpenScene(BlockforgeScenes.MapMars, OpenSceneMode.Single);
        }

        // ---------- Endroits ----------
        var spots = new List<(Vector3 pos, string label)>();
        Transform center = null;
        var markers = new List<Transform>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (t.name == "Marker_Center") center = t;
            else if (t.name.StartsWith("Marker_Spawn_") && t.name.Length > 14) markers.Add(t);
        }
        foreach (var raw in File.ReadAllLines(RequestPath))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;
            if (line == "spawns")
            {
                foreach (var m in markers.OrderBy(m => m.name))
                    spots.Add((m.position, m.name));
                continue;
            }
            var parts = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                spots.Add((new Vector3(x, y, z), parts.Length > 3 ? string.Join(" ", parts.Skip(3)) : line));
        }
        Vector3 goal = center != null ? center.position : Vector3.zero;

        // ---------- NavMesh comme en match ----------
        int mask = ~((1 << RobotTestSpawner.RobotLayer) | (1 << 2));
        var navs = new List<(string name, float radius, NavMeshData data, NavMeshDataInstance inst, int type)>();
        Bounds bounds = default;
        bool anyBounds = false;
        foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
        {
            if (col.isTrigger || ((1 << col.gameObject.layer) & mask) == 0 || col.attachedRigidbody != null) continue;
            if (col.bounds.size.x > 3000f || col.bounds.size.z > 3000f) continue;
            if (!anyBounds) { bounds = col.bounds; anyBounds = true; } else bounds.Encapsulate(col.bounds);
        }
        if (anyBounds)
        {
            bounds.Expand(new Vector3(4f, 10f, 4f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            foreach (var (name, radius) in new[] { ("normal", 1.6f), ("large", MatchNavMesh.WideRadius) })
            {
                var s = NavMesh.CreateSettings();
                s.agentRadius = radius; s.agentHeight = 2.2f; s.agentSlope = 34f; s.agentClimb = 0.9f; s.minRegionArea = 12f;
                s.overrideVoxelSize = true; s.voxelSize = 0.4f; s.overrideTileSize = true; s.tileSize = 128;
                var data = NavMeshBuilder.BuildNavMeshData(s, sources, bounds, Vector3.zero, Quaternion.identity);
                navs.Add((name, radius, data, NavMesh.AddNavMeshData(data), s.agentTypeID));
            }
        }

        // ---------- Sonde ----------
        var sb = new StringBuilder();
        sb.AppendLine($"# Sonde des endroits — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} — {spots.Count} endroit(s), centre {V(goal)}");
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "ProbeMarker";
        marker.transform.localScale = Vector3.one * 2.5f;
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        var mat = new Material(Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color"));
        mat.SetColor("_UnlitColor", Color.red); mat.color = Color.red;
        marker.GetComponent<Renderer>().sharedMaterial = mat;
        var camGo = new GameObject("ProbeCamera");
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.3f; cam.farClipPlane = 2000f;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);

        int index = 0;
        foreach (var (pos, label) in spots)
        {
            index++;
            sb.AppendLine($"\n## {index}. {label} {V(pos)}");
            float ground = pos.y;
            if (Physics.Raycast(pos + Vector3.up * 150f, Vector3.down, out var hit, 600f, mask, QueryTriggerInteraction.Ignore))
            {
                ground = hit.point.y;
                sb.AppendLine($"sol : y = {ground:0.0} ({hit.collider.name}), pente {Vector3.Angle(hit.normal, Vector3.up):0} deg, point {pos.y - ground:+0.0;-0.0} m au-dessus");
            }
            else
                sb.AppendLine("sol : AUCUN (rien sous le point)");

            foreach (float d in new[] { 5f, 15f, 30f })
            {
                var row = new StringBuilder($"relief a {d,2:0} m :");
                for (int i = 0; i < 8; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                    Vector3 q = pos + dir * d;
                    string h = Physics.Raycast(q + Vector3.up * 150f, Vector3.down, out var hh, 600f, mask, QueryTriggerInteraction.Ignore)
                        ? $"{hh.point.y - ground:+0;-0}" : "vide";
                    row.Append($" {Dir(i)} {h}");
                }
                sb.AppendLine(row.ToString());
            }

            var near = Physics.OverlapSphere(pos, 8f, mask, QueryTriggerInteraction.Ignore)
                .Select(c => c.name).Distinct().Take(8);
            sb.AppendLine("colliders a 8 m : " + (near.Any() ? string.Join(", ", near) : "aucun"));

            foreach (var nav in navs)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = nav.type, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(pos, out var nh, 12f, filter))
                {
                    var path = new NavMeshPath();
                    bool ok = NavMesh.SamplePosition(goal, out var gh, 30f, filter) && NavMesh.CalculatePath(nh.position, gh.position, filter, path);
                    sb.AppendLine($"navmesh {nav.name} (r {nav.radius}) : point a {Vector3.Distance(pos, nh.position):0.0} m {V(nh.position)}, " +
                                  $"chemin vers le centre : {(ok ? path.status.ToString() : "aucun")}" +
                                  (ok && path.status == NavMeshPathStatus.PathComplete ? $" ({Length(path):0} m)" : ""));
                }
                else
                    sb.AppendLine($"navmesh {nav.name} (r {nav.radius}) : AUCUN point a moins de 12 m");
            }

            // Rendus
            marker.transform.position = new Vector3(pos.x, ground + 1.2f, pos.z);
            Shoot(cam, rt, tex, pos + new Vector3(-22f, 16f + (pos.y - ground), -22f), marker.transform.position, $"Library/BotProbe_{index}_3q.png");
            Shoot(cam, rt, tex, marker.transform.position + Vector3.up * 70f + Vector3.back * 0.01f, marker.transform.position, $"Library/BotProbe_{index}_top.png");
            sb.AppendLine($"rendus : Library/BotProbe_{index}_3q.png, Library/BotProbe_{index}_top.png");
        }

        // ---------- Nettoyage ----------
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(marker);
        Object.DestroyImmediate(mat);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        foreach (var nav in navs)
        {
            NavMesh.RemoveNavMeshData(nav.inst);
            NavMesh.RemoveSettings(nav.type);
            Object.DestroyImmediate(nav.data);
        }
        File.WriteAllText(ReportPath, sb.ToString());
        Debug.Log($"[Bot] Probe spots : {spots.Count} endroit(s), rapport {ReportPath}\n{sb}");
    }

    private static void Shoot(Camera cam, RenderTexture rt, Texture2D tex, Vector3 from, Vector3 at, string path)
    {
        cam.transform.position = from;
        cam.transform.LookAt(at);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
    }

    private static float Length(NavMeshPath p)
    {
        float l = 0f;
        for (int i = 1; i < p.corners.Length; i++) l += Vector3.Distance(p.corners[i - 1], p.corners[i]);
        return l;
    }

    private static string V(Vector3 v) => $"({v.x:0}, {v.y:0}, {v.z:0})";
    private static string Dir(int i) => new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" }[i];
}
