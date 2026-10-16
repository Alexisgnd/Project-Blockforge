using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// ZONE DE COMBAT DU COMBAT YARD (terrain d'essai)
// Creee par RobotTestSpawner quand la map est Map_TestGround :
// la zone est d'abord degagee de ses obstacles (ClearObstacles),
// puis un carre delimite en rouge (traits lumineux au sol et quatre
// bornes) au debut du Combat Yard, avec un robot ennemi
// (EnemyRobotAI) qui attaque le joueur tant qu'il est dans la
// zone. Le robot ennemi est assemble comme le joueur
// (RobotAssembler + locomotion, visee, tir, sante) a partir d'un
// blueprint fixe peint en rouge ; detruit, il reapparait apres
// enemyRespawnSeconds. Le spawner lui fournit le catalogue des
// blocs et la reference du robot joueur (Player).
// =========================================================

public class CombatArena : MonoBehaviour
{
    [Header("Zone (monde)")]
    public Vector3 center = new(0f, 0f, 100f);
    public float halfSize = 18f;
    public Vector3 enemySpawn = new(0f, 0.5f, 112f);
    public float enemyRespawnSeconds = 6f;
    public Color zoneColor = new(1f, 0.22f, 0.18f, 1f);

    public GameObject Player { get; set; }
    public GameObject Enemy { get; private set; }

    private IReadOnlyDictionary<string, BlockDefinition> defs;
    private float cellSize = 1f;
    private Material zoneMat;
    private bool respawnPending;

    public void Setup(IReadOnlyDictionary<string, BlockDefinition> catalogue, float cell, Transform map)
    {
        defs = catalogue;
        cellSize = cell;
        if (map != null)
            Debug.Log($"[CombatArena] {ClearObstacles(map)} obstacle(s) retire(s) de la zone de combat.");
        BuildBoundary();
        SpawnEnemy();
    }

    // Degage la zone : tout objet de la map dont le volume touche le carre (cibles,
    // conteneurs, couverts, cibles mobiles entieres), avec 10 m de marge autour
    // pour degager les abords, est desactive. Le sol et les
    // grands elements (> 50 m), les marquages au sol et les textes restent.
    private int ClearObstacles(Transform map)
    {
        int count = 0;
        var removed = new HashSet<GameObject>();
        foreach (var r in map.GetComponentsInChildren<Renderer>(true))
        {
            var b = r.bounds;
            if (b.size.x > 50f || b.size.z > 50f || b.size.y < 0.05f)
                continue;
            string n = r.name.ToLowerInvariant();
            if (n.Contains("label") || n.Contains("_text") || n.Contains("marking") || n.Contains("line") || n.Contains("border"))
                continue;
            bool overlap = b.max.x >= center.x - halfSize - 10f && b.min.x <= center.x + halfSize + 10f
                           && b.max.z >= center.z - halfSize - 10f && b.min.z <= center.z + halfSize + 10f
                           && b.min.y < center.y + 20f;
            if (!overlap)
                continue;
            // Cible mobile : on retire toute la cible (racine MovingTarget)
            var mover = r.GetComponentInParent<MovingTarget>();
            var go = mover != null ? mover.gameObject : r.gameObject;
            if (removed.Add(go))
            {
                go.SetActive(false);
                count++;
            }
        }
        return count;
    }

    public bool Contains(Vector3 p)
    {
        return Mathf.Abs(p.x - center.x) <= halfSize && Mathf.Abs(p.z - center.z) <= halfSize && Mathf.Abs(p.y - center.y) < 40f;
    }

    private void OnDestroy()
    {
        if (zoneMat != null)
            Destroy(zoneMat);
    }

    // ---------- Delimitation ----------

    private void BuildBoundary()
    {
        var shader = Shader.Find("Blockforge/Hologram");
        if (shader == null)
            return;
        zoneMat = new Material(shader) { name = "CombatZone (instance)" };
        zoneMat.SetFloat("_Beam", 2f);
        zoneMat.SetColor("_Color", zoneColor);
        zoneMat.SetFloat("_Intensity", 1.6f);
        zoneMat.SetFloat("_Reveal", 1f);
        zoneMat.SetFloat("_Flicker", 0.05f);
        zoneMat.SetFloat("_LineDensity", 0.5f);
        zoneMat.SetFloat("_LineStrength", 0f);
        zoneMat.SetVector("_HoloUp", new Vector4(0f, 1f, 0f, 0f));
        zoneMat.SetFloat("_HoloHalfHeight", 100000f);

        // Trait au sol (suit la hauteur du sol a chaque coin)
        var go = new GameObject("CombatZone_Line");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.startWidth = line.endWidth = 0.3f;
        line.alignment = LineAlignment.View;
        line.sharedMaterial = zoneMat;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        var corners = new[]
        {
            new Vector3(center.x - halfSize, 0f, center.z - halfSize), new Vector3(center.x + halfSize, 0f, center.z - halfSize),
            new Vector3(center.x + halfSize, 0f, center.z + halfSize), new Vector3(center.x - halfSize, 0f, center.z + halfSize),
        };
        for (int i = 0; i < 4; i++)
        {
            corners[i].y = GroundY(corners[i]) + 0.35f;
            line.SetPosition(i, corners[i]);
            // Borne lumineuse
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "CombatZone_Post" + i;
            Destroy(post.GetComponent<Collider>());
            post.transform.SetParent(transform, false);
            post.transform.position = corners[i] + Vector3.up * 1.6f;
            post.transform.localScale = new Vector3(0.35f, 3.6f, 0.35f);
            var r = post.GetComponent<MeshRenderer>();
            r.sharedMaterial = zoneMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    private static float GroundY(Vector3 at)
    {
        return Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~(1 << RobotTestSpawner.RobotLayer), QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : at.y;
    }

    // ---------- Robot ennemi ----------

    private static PlacedBlock B(string id, int x, int y, int z, int face = 0) =>
        new() { blockId = id, x = x, y = y, z = z, rotation = face == 0 ? 0 : BlockFootprint.Compose(face, 0), colorIndex = 1 };

    // Chassis 3 x 5 rouge, cabine, quatre roues Scout, deux lasers Hornet a l'avant
    private static RobotBlueprint EnemyBlueprint()
    {
        var bp = new RobotBlueprint { name = "SENTINEL" };
        for (int x = 14; x <= 16; x++)
        {
            for (int z = 13; z <= 17; z++)
                bp.blocks.Add(B("Block_01_Cube", x, 0, z));
            bp.blocks.Add(B("Block_01_Cube", x, 1, 13));
        }
        bp.blocks.Add(B("Block_01_Cube", 15, 1, 14));
        foreach (int z in new[] { 13, 17 })
        {
            bp.blocks.Add(B("Block_Wheel_N1_Scout", 17, 0, z, 3));
            bp.blocks.Add(B("Block_Wheel_N1_Scout", 13, 0, z, 4));
        }
        bp.blocks.Add(B("Block_Laser_N2_Hornet", 14, 1, 17, 1));
        bp.blocks.Add(B("Block_Laser_N2_Hornet", 16, 1, 17, 1));
        return bp;
    }

    public void SpawnEnemy()
    {
        if (defs == null)
            return;
        if (Enemy != null)
            Destroy(Enemy);
        var bp = EnemyBlueprint();
        Enemy = RobotAssembler.Build(bp, defs, cellSize, null, out int placed, out int weightKg);
        if (placed == 0)
        {
            Debug.LogWarning("[CombatArena] Robot ennemi vide : catalogue sans les blocs attendus.");
            return;
        }
        Vector3 spawn = enemySpawn;
        spawn.y = GroundY(enemySpawn) + 0.5f;
        Vector3 facing = Vector3.ProjectOnPlane(center - spawn, Vector3.up);
        if (facing.sqrMagnitude < 1e-4f)
            facing = Vector3.back;
        Enemy.transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(facing.normalized, Vector3.up));
        SetLayer(Enemy.transform, RobotTestSpawner.RobotLayer);

        Enemy.AddComponent<Rigidbody>();
        var loco = Enemy.AddComponent<RobotLocomotion>();
        loco.Setup(cellSize, weightKg);
        Enemy.AddComponent<RobotPartAnimator>().Build(loco);
        var loadout = Enemy.AddComponent<RobotLoadoutState>();
        loadout.acceptInput = false;
        Enemy.AddComponent<RobotEnergy>();
        var aim = Enemy.AddComponent<WeaponAimController>();
        aim.useCamera = false;
        var fire = Enemy.AddComponent<WeaponFireController>();
        fire.manual = true;
        Enemy.AddComponent<RobotGroundFX>();
        var health = Enemy.AddComponent<RobotHealth>();
        Enemy.AddComponent<RobotDamageFX>();
        health.Destroyed += OnEnemyDestroyed;
        var ai = Enemy.AddComponent<EnemyRobotAI>();
        ai.arena = this;
        Enemy.AddComponent<RobotSpawnSequence>().Begin(new Behaviour[] { loco, aim, fire, ai });
        respawnPending = false;
    }

    private void OnEnemyDestroyed()
    {
        if (respawnPending)
            return;
        respawnPending = true;
        StartCoroutine(RespawnEnemy());
    }

    private IEnumerator RespawnEnemy()
    {
        yield return new WaitForSeconds(enemyRespawnSeconds);
        SpawnEnemy();
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayer(child, layer);
    }
}
