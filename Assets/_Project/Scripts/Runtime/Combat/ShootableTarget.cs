using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// =========================================================
// CIBLE DESTRUCTIBLE (disques du terrain d'essai)
// Posee au lancement sur chaque disque de cible de la map
// (AttachAll, appele par RobotTestSpawner) : disques fixes
// (<nom>_Target, Target_050m_A...) et disques des cibles mobiles
// (<nom>_Disk, _Disk1..., chariots _Target).
// Hit(degats) : la cible encaisse ; a 0 elle eclate (debris),
// disparait (rendu et collision coupes) et reapparait apres
// respawnSeconds avec un petit "pop". Les cibles mobiles
// continuent de bouger pendant ce temps.
// =========================================================

public class ShootableTarget : MonoBehaviour
{
    public float maxHp = 40f;
    public float respawnSeconds = 8f;

    public bool IsDown { get; private set; }
    public static int Destroyed { get; private set; }   // compteur de session (HUD futur)

    private float hp;
    private float respawnAt;
    private float popStart = -1f;
    private Renderer[] renderers;
    private Collider[] colliders;
    private Vector3 baseScale;

    private static readonly Regex DiskName = new(@"(_Disk\d*$)|(Target(_\d+|\d+|_[A-Za-z]|)$)|(Target$)");
    private static readonly string[] Excluded = { "_Post", "_Base", "_Ramp", "_Deck", "_Leg", "_Arm", "_Rail", "_Label", "_Text" };
    private static ParticleSystem shards;
    private static Material shardMat;
    private static ParticleSystem.EmitParams emit;

    // Pose le composant sur tous les disques de cible sous 'root' ; retourne le nombre
    public static int AttachAll(Transform root)
    {
        int count = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.name;
            if (!n.Contains("Target") && !DiskName.IsMatch(n))
                continue;
            bool excluded = false;
            foreach (var suffix in Excluded)
            {
                if (n.EndsWith(suffix))
                {
                    excluded = true;
                    break;
                }
            }
            if (excluded || n.StartsWith("Sign_") || r.GetComponent<ShootableTarget>() != null)
                continue;
            r.gameObject.AddComponent<ShootableTarget>();
            count++;
        }
        return count;
    }

    private void Awake()
    {
        hp = maxHp;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        baseScale = transform.localScale;
    }

    public void Hit(float damage)
    {
        if (IsDown)
            return;
        hp -= damage;
        if (hp > 0f)
            return;
        IsDown = true;
        Destroyed++;
        respawnAt = Time.time + respawnSeconds;
        Vector3 center = renderers.Length > 0 ? renderers[0].bounds.center : transform.position;
        float size = renderers.Length > 0 ? renderers[0].bounds.extents.magnitude : 1f;
        Burst(center, Mathf.Clamp(Mathf.RoundToInt(size * 14f), 10, 40), size);
        SetVisible(false);
    }

    private void Update()
    {
        if (IsDown && Time.time >= respawnAt)
        {
            IsDown = false;
            hp = maxHp;
            SetVisible(true);
            popStart = Time.time;
            transform.localScale = baseScale * 0.2f;
        }
        if (popStart >= 0f)
        {
            float k = (Time.time - popStart) / 0.25f;
            if (k >= 1f)
            {
                transform.localScale = baseScale;
                popStart = -1f;
            }
            else
            {
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float u = k - 1f;
                float back = 1f + c3 * u * u * u + c1 * u * u;
                transform.localScale = baseScale * Mathf.LerpUnclamped(0.2f, 1f, back);
            }
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in renderers)
        {
            if (r != null)
                r.enabled = visible;
        }
        foreach (var c in colliders)
        {
            if (c != null)
                c.enabled = visible;
        }
    }

    // ---------- Debris (un systeme partage) ----------

    private static void Burst(Vector3 at, int count, float size)
    {
        if (shards == null && !BuildShards())
            return;
        for (int i = 0; i < count; i++)
        {
            emit.position = at + Random.insideUnitSphere * size * 0.6f;
            emit.velocity = (Random.onUnitSphere + Vector3.up * 0.5f).normalized * Random.Range(2f, 7f);
            emit.startSize = Random.Range(0.12f, 0.3f);
            emit.startLifetime = Random.Range(0.5f, 1.1f);
            emit.rotation = Random.Range(0f, 360f);
            shards.Emit(emit, 1);
        }
    }

    private static bool BuildShards()
    {
        var shader = Shader.Find("Blockforge/DustParticle");
        if (shader == null)
            return false;
        var go = new GameObject("TargetShards");
        shards = go.AddComponent<ParticleSystem>();
        shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = shards.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.startSpeed = 0f;
        main.gravityModifier = 1f;
        var emission = shards.emission;
        emission.enabled = false;
        var shape = shards.shape;
        shape.enabled = false;
        var rot = shards.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
        shardMat = new Material(shader) { name = "TargetShards (instance)" };
        shardMat.SetFloat("_Softness", 0.3f);
        shardMat.SetFloat("_FadeIn", 0.02f);
        shardMat.SetFloat("_FadeOut", 0.6f);
        shardMat.SetColor("_Tint", new Color(0.95f, 0.85f, 0.7f, 0.95f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = shardMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent,
        });
        shards.Play();
        return true;
    }
}
