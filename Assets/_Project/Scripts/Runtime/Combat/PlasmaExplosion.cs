using System.Collections.Generic;
using UnityEngine;

// =========================================================
// EXPLOSION DE PLASMA (effet visuel, sans degats)
// Creee par WeaponFireController a l'impact d'une boule de
// plasma. La zone de degats (rayon 'radius') est montree telle
// quelle :
// - Eclair : petite sphere blanc-violet qui gonfle et s'eteint
//   en ~0,12 s
// - Onde de choc : bulle hologramme (remplissage leger, bords
//   brillants) qui gonfle jusqu'au rayon exact de la zone puis
//   s'efface
// - Anneau : disque lumineux plaque sur la surface touchee, qui
//   s'ouvre jusqu'au rayon de la zone (on lit d'un coup d'oeil
//   jusqu'ou l'explosion porte)
// Les particules (gerbe, braises, fumee) sont emises par
// WeaponFireController sur ses propres systemes.
// Un materiau par explosion (intensite qui baisse) ; detruit
// avec l'objet.
// =========================================================

public class PlasmaExplosion : MonoBehaviour
{
    private const float FlashTime = 0.14f;
    private const float WaveTime = 0.42f;
    private const float RingTime = 0.55f;

    private static Mesh ringMesh;
    private static Mesh sphereMesh;

    private float radius, born;
    private Transform flash, wave, ring;
    private Material flashMat, waveMat, ringMat;
    private float flashIntensity, waveIntensity, ringIntensity;

    public static PlasmaExplosion Spawn(Vector3 at, Vector3 normal, float radius, Color color, Shader hologram)
    {
        if (hologram == null)
            return null;
        var go = new GameObject("PlasmaExplosion");
        go.transform.position = at;
        var fx = go.AddComponent<PlasmaExplosion>();
        fx.Build(normal, Mathf.Max(0.5f, radius), color, hologram);
        return fx;
    }

    private void Build(Vector3 normal, float r, Color color, Shader shader)
    {
        radius = r;
        born = Time.time;
        if (normal.sqrMagnitude < 1e-4f)
            normal = Vector3.up;

        Color hot = Color.Lerp(color, Color.white, 0.55f);
        flashIntensity = 6f;
        waveIntensity = 2.2f;
        ringIntensity = 3.2f;

        flashMat = Holo(shader, "PlasmaFlash", hot, flashIntensity, 2f);
        waveMat = Holo(shader, "PlasmaWave", color, waveIntensity, 0f);
        waveMat.SetFloat("_FillAlpha", 0.1f);
        waveMat.SetFloat("_RimPower", 1.6f);
        waveMat.SetFloat("_RimStrength", 1.8f);
        waveMat.SetFloat("_LineDensity", 6f);
        waveMat.SetFloat("_LineStrength", 0.35f);
        ringMat = Holo(shader, "PlasmaRing", Color.Lerp(color, Color.white, 0.25f), ringIntensity, 2f);

        flash = Part("Flash", SphereMesh(), flashMat, Quaternion.identity);
        wave = Part("Shockwave", SphereMesh(), waveMat, Quaternion.identity);
        // Anneau plaque sur la surface touchee, un peu decolle pour ne pas scintiller
        ring = Part("Ring", RingMesh(), ringMat, Quaternion.FromToRotation(Vector3.up, normal));
        ring.position += normal * 0.04f;

        flash.localScale = Vector3.one * (r * 0.15f);
        wave.localScale = Vector3.zero;
        ring.localScale = Vector3.zero;
    }

    private Transform Part(string name, Mesh mesh, Material mat, Quaternion rotation)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.transform.rotation = rotation;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }

    private static Material Holo(Shader shader, string name, Color color, float intensity, float mode)
    {
        var m = new Material(shader) { name = name + " (instance)" };
        m.SetFloat("_Beam", mode);
        m.SetColor("_Color", color);
        m.SetFloat("_Intensity", intensity);
        m.SetFloat("_Reveal", 1f);
        m.SetFloat("_Flicker", 0.05f);
        m.SetFloat("_LineDensity", 0.5f);
        m.SetFloat("_LineStrength", 0f);
        m.SetVector("_HoloCenter", Vector4.zero);
        m.SetVector("_HoloUp", new Vector4(0f, 1f, 0f, 0f));
        m.SetFloat("_HoloHalfHeight", 100000f);
        return m;
    }

    private void Update()
    {
        float t = Time.time - born;

        // Eclair : gonfle vite, s'eteint
        float f = Mathf.Clamp01(t / FlashTime);
        flash.gameObject.SetActive(f < 1f);
        flash.localScale = Vector3.one * (radius * Mathf.Lerp(0.15f, 0.55f, EaseOut(f)));
        flashMat.SetFloat("_Intensity", flashIntensity * (1f - f) * (1f - f));

        // Onde de choc : jusqu'au rayon exact de la zone (le diametre de la sphere = 2 r)
        float w = Mathf.Clamp01(t / WaveTime);
        wave.gameObject.SetActive(w < 1f);
        wave.localScale = Vector3.one * (2f * radius * EaseOut(Mathf.Clamp01(w * 1.6f)));
        waveMat.SetFloat("_Intensity", waveIntensity * (1f - w * w));

        // Anneau au sol : s'ouvre jusqu'au rayon, tient un instant, s'efface
        float g = Mathf.Clamp01(t / RingTime);
        ring.gameObject.SetActive(g < 1f);
        float open = EaseOut(Mathf.Clamp01(g * 2.2f));
        ring.localScale = new Vector3(radius * open, 1f, radius * open);
        ringMat.SetFloat("_Intensity", ringIntensity * (g < 0.45f ? 1f : 1f - (g - 0.45f) / 0.55f));

        if (t >= Mathf.Max(WaveTime, RingTime))
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (flashMat != null) Destroy(flashMat);
        if (waveMat != null) Destroy(waveMat);
        if (ringMat != null) Destroy(ringMat);
    }

    private static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

    // Sphere unite (rayon 0,5, comme la primitive Unity) sans collider
    private static Mesh SphereMesh()
    {
        if (sphereMesh != null)
            return sphereMesh;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
        return sphereMesh;
    }

    // Anneau plat de rayon 1 (bord interieur 0,86), normale +Y, deux faces (Cull Off)
    private static Mesh RingMesh()
    {
        if (ringMesh != null)
            return ringMesh;
        const int sides = 64;
        const float inner = 0.86f;
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts.Add(d * inner);
            verts.Add(d);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(i / (float)sides, 0f));
            uvs.Add(new Vector2(i / (float)sides, 1f));
        }
        for (int i = 0; i < sides; i++)
        {
            int b = i * 2;
            tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
        }
        ringMesh = new Mesh { name = "PlasmaRing" };
        ringMesh.SetVertices(verts);
        ringMesh.SetNormals(normals);
        ringMesh.SetUVs(0, uvs);
        ringMesh.SetTriangles(tris, 0);
        ringMesh.RecalculateBounds();
        return ringMesh;
    }
}
