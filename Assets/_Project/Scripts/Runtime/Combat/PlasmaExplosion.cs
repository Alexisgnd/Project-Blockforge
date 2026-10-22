using System.Collections.Generic;
using UnityEngine;

// =========================================================
// EXPLOSION DE PLASMA (effet visuel, sans degats)
// Creee par WeaponFireController a l'impact d'une boule de
// plasma. Tout est dimensionne sur la zone de degats ('radius',
// qui grandit avec le niveau de l'arme) et sur 'power' (0 = N1,
// 1 = N6) qui regle la densite et la duree des effets :
// Couleur : celle du tir ('color', couleur d'equipe relative au joueur
// local, voir TeamRelation) ; arcs et teinte au sol en derivent.
// - Eclair : coeur blanc + halo colore (bords de Fresnel) qui
//   gonflent et s'eteignent en ~0,15 s
// - Onde de choc : bulle hologramme qui gonfle jusqu'au rayon
//   exact de la zone puis s'efface
// - Anneau : disque lumineux plaque sur la surface touchee,
//   ouvert jusqu'au rayon de la zone
// - Onde electrique : cercle crepitant (ligne brisee regeneree
//   en continu) qui court au sol avec l'onde de choc
// - Arcs electriques : eclairs ramifies qui jaillissent du point
//   d'impact dans la zone (7 a N1, 17 a N6), renaissent et
//   crepitent pendant 0,3 a 0,55 s ; un arc file aussi vers
//   chaque robot / cible touche par la zone
// - Teinte au sol : disque violet semi-opaque (fondu alpha) sous
//   l'onde de choc. Sur un sol clair, l'additif seul sature en
//   blanc : ce fond donne le contraste qui rend les arcs lisibles
// - Brulure : tache sombre douce laissee sur un decor fixe (pas
//   sur un robot), qui s'efface en quelques secondes
// Les particules (gerbe, braises, etincelles, fumee) sont emises
// par WeaponFireController sur ses propres systemes.
// Materiaux propres a chaque explosion (leur intensite baisse),
// detruits avec elle.
// =========================================================

public class PlasmaExplosion : MonoBehaviour
{
    private const float FlashTime = 0.16f;
    private const float WaveTime = 0.45f;
    private const float RingTime = 0.6f;
    private const float ScorchTime = 4f;
    private const float ArcRefresh = 0.035f;     // regeneration des eclairs (crepitement)

    private static Mesh ringMesh, sphereMesh, discMesh;

    private class Arc
    {
        public LineRenderer line, fork;
        public Vector3 end;           // relatif au point d'impact
        public float born, dies;
        public bool toTarget;
    }

    private float radius, power, born, arcTime, life;
    private Vector3 normal;
    private Transform core, glow, wave, ring, scorch, tint;
    private LineRenderer electricRing;
    private Material coreMat, glowMat, waveMat, ringMat, arcMat, scorchMat, tintMat;
    private Color tintColor;   // teinte au sol : ton sombre et sature de la couleur du tir
    private readonly List<Arc> arcs = new();
    private float nextRefresh;

    public static PlasmaExplosion Spawn(Vector3 at, Vector3 normal, float radius, float power, Color color,
                                        Shader hologram, Shader dust, IReadOnlyList<Vector3> targets, bool scorchSurface)
    {
        if (hologram == null)
            return null;
        var go = new GameObject("PlasmaExplosion");
        go.transform.position = at;
        var fx = go.AddComponent<PlasmaExplosion>();
        fx.Build(normal, Mathf.Max(0.5f, radius), Mathf.Clamp01(power), color, hologram, scorchSurface ? dust : null, targets);
        return fx;
    }

    // ---------- Construction ----------

    private void Build(Vector3 n, float r, float p, Color color, Shader holo, Shader dust, IReadOnlyList<Vector3> targets)
    {
        radius = r;
        power = p;
        born = Time.time;
        normal = n.sqrMagnitude < 1e-4f ? Vector3.up : n.normalized;
        arcTime = Mathf.Lerp(0.45f, 0.8f, power);

        Color hot = Color.Lerp(color, Color.white, 0.6f);
        // Derivees de la couleur du tir (equipe) : arcs plus clairs, teinte au sol sombre
        // et saturee (meme rendu qu'avant pour le violet d'origine)
        Color arcColor = Color.Lerp(color, Color.white, 0.3f);
        Color.RGBToHSV(color, out float hue, out float sat, out _);
        tintColor = Color.HSVToRGB(hue, Mathf.Max(sat, 0.85f), 0.62f);
        coreMat = Holo(holo, "PlasmaCore", Color.white, 8f, 2f);
        glowMat = Holo(holo, "PlasmaGlow", hot, 3.5f, 0f);
        glowMat.SetFloat("_FillAlpha", 0.35f);
        glowMat.SetFloat("_RimPower", 1.2f);
        glowMat.SetFloat("_RimStrength", 2.2f);
        waveMat = Holo(holo, "PlasmaWave", color, 1.7f, 0f);
        waveMat.SetFloat("_FillAlpha", 0.08f);
        waveMat.SetFloat("_RimPower", 1.6f);
        waveMat.SetFloat("_RimStrength", 2f);
        waveMat.SetFloat("_LineDensity", 6f);
        waveMat.SetFloat("_LineStrength", 0.4f);
        ringMat = Holo(holo, "PlasmaRing", color, 2.4f, 2f);
        arcMat = Holo(holo, "PlasmaArc", arcColor, 6f, 2f);
        arcMat.SetFloat("_Flicker", 0.35f);

        core = Part("Core", SphereMesh(), coreMat, Quaternion.identity);
        glow = Part("Glow", SphereMesh(), glowMat, Quaternion.identity);
        wave = Part("Shockwave", SphereMesh(), waveMat, Quaternion.identity);
        ring = Part("Ring", RingMesh(), ringMat, Quaternion.FromToRotation(Vector3.up, normal));
        ring.position += normal * 0.04f;
        core.localScale = glow.localScale = wave.localScale = ring.localScale = Vector3.zero;

        // Epaisseurs proportionnelles a la zone : lisibles de loin comme de pres
        float w = 0.05f + 0.035f * radius;
        electricRing = NewLine("ElectricRing", w * 1.1f);
        electricRing.loop = true;
        electricRing.positionCount = 56;

        // Arcs libres dans la zone, puis un arc par robot / cible touche
        int count = Mathf.RoundToInt(Mathf.Lerp(7f, 17f, power));
        for (int i = 0; i < count; i++)
            arcs.Add(NewArc(false, RandomArcEnd(), born + Random.Range(0f, 0.08f)));
        if (targets != null)
        {
            for (int i = 0; i < targets.Count && i < 8; i++)
                arcs.Add(NewArc(true, targets[i] - transform.position, born + Random.Range(0.02f, 0.1f)));
        }

        life = Mathf.Max(RingTime, arcTime + 0.1f);
        if (dust != null)
        {
            // Dessines avant les effets additifs (file transparente plus tot) : ils passent dessous
            tintMat = AlphaDisc(dust, "PlasmaTint", 0.9f);
            tintMat.renderQueue = 2990;
            tint = Part("Tint", DiscMesh(), tintMat, Quaternion.FromToRotation(Vector3.up, normal));
            tint.position += normal * 0.03f;
            tint.localScale = Vector3.zero;

            scorchMat = new Material(dust) { name = "PlasmaScorch (instance)" };
            scorchMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            scorchMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            scorchMat.SetFloat("_Additive", 0f);
            scorchMat.SetFloat("_Softness", 0.95f);
            scorchMat.SetFloat("_FadeIn", 0.01f);
            scorchMat.SetFloat("_FadeOut", 0.99f);
            scorchMat.SetColor("_Tint", new Color(0.05f, 0.03f, 0.08f, 0f));
            scorchMat.renderQueue = 2989;
            scorch = Part("Scorch", DiscMesh(), scorchMat, Quaternion.FromToRotation(Vector3.up, normal));
            scorch.position += normal * 0.025f;
            scorch.localScale = Vector3.one * (radius * 1.5f);
            life = ScorchTime;
        }
    }

    private Arc NewArc(bool toTarget, Vector3 end, float start)
    {
        var a = new Arc
        {
            line = NewLine("Arc", (0.05f + 0.035f * radius) * (toTarget ? 1.2f : 1f)),
            fork = toTarget ? null : NewLine("Fork", (0.05f + 0.035f * radius) * 0.55f),
            end = end,
            born = start,
            dies = start + Random.Range(0.12f, 0.26f) * (1f + power * 0.5f),
            toTarget = toTarget,
        };
        a.line.enabled = false;
        if (a.fork != null)
            a.fork.enabled = false;
        return a;
    }

    // Extremite d'un arc libre : dans l'hemisphere de la surface, souvent rasante
    private Vector3 RandomArcEnd()
    {
        Vector3 d = Random.onUnitSphere;
        if (Vector3.Dot(d, normal) < 0f)
            d = Vector3.Reflect(d, normal);
        d = Vector3.Lerp(d, Vector3.ProjectOnPlane(d, normal), Random.Range(0.2f, 0.85f)).normalized;
        return d * (radius * Random.Range(0.55f, 1.05f));
    }

    private LineRenderer NewLine(string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sharedMaterial = arcMat;
        lr.startWidth = width;
        lr.endWidth = width * 0.35f;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 1;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
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

    private static Material AlphaDisc(Shader dust, string name, float softness)
    {
        var m = new Material(dust) { name = name + " (instance)" };
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_Additive", 0f);
        m.SetFloat("_Softness", softness);
        m.SetFloat("_FadeIn", 0.01f);
        m.SetFloat("_FadeOut", 0.99f);
        m.SetColor("_Tint", new Color(0f, 0f, 0f, 0f));
        return m;
    }

    public static Material Holo(Shader shader, string name, Color color, float intensity, float mode)
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

    // ---------- Animation ----------

    private void Update()
    {
        float t = Time.time - born;
        Vector3 center = transform.position;

        // Eclair : coeur blanc et halo violet, gonflent vite puis s'eteignent
        float f = Mathf.Clamp01(t / FlashTime);
        bool flashOn = f < 1f;
        core.gameObject.SetActive(flashOn);
        glow.gameObject.SetActive(flashOn);
        if (flashOn)
        {
            core.localScale = Vector3.one * (radius * Mathf.Lerp(0.12f, 0.4f, EaseOut(f)));
            glow.localScale = Vector3.one * (radius * Mathf.Lerp(0.25f, 0.95f, EaseOut(f)));
            coreMat.SetFloat("_Intensity", Mathf.Lerp(6f, 10f, power) * (1f - f) * (1f - f));
            glowMat.SetFloat("_Intensity", 3.5f * (1f - f));
        }

        // Onde de choc : jusqu'au rayon exact de la zone (diametre de la sphere = 2 r)
        float w = Mathf.Clamp01(t / WaveTime);
        float waveRadius = radius * EaseOut(Mathf.Clamp01(w * 1.6f));
        wave.gameObject.SetActive(w < 1f);
        wave.localScale = Vector3.one * (2f * waveRadius);
        waveMat.SetFloat("_Intensity", 1.7f * (1f - w * w));

        // Anneau au sol : s'ouvre jusqu'au rayon, tient un instant, s'efface
        float g = Mathf.Clamp01(t / RingTime);
        ring.gameObject.SetActive(g < 1f);
        float open = EaseOut(Mathf.Clamp01(g * 2.2f));
        ring.localScale = new Vector3(radius * open, 1f, radius * open);
        ringMat.SetFloat("_Intensity", 2.4f * (g < 0.45f ? 1f : 1f - (g - 0.45f) / 0.55f));

        // Teinte au sol : suit l'onde, tient pendant les arcs, s'efface
        if (tintMat != null)
        {
            float span = Mathf.Max(WaveTime, arcTime);
            float tintAlpha = Mathf.Clamp01(t / 0.05f) * (1f - Mathf.Clamp01((t - span * 0.6f) / (span * 0.4f)));
            tint.gameObject.SetActive(tintAlpha > 0.001f);
            tint.localScale = Vector3.one * (2.8f * Mathf.Max(waveRadius, radius * 0.35f));
            tintMat.SetColor("_Tint", new Color(tintColor.r, tintColor.g, tintColor.b, 0.7f * tintAlpha));
        }

        // Eclairs : intensite commune qui retombe a la fin
        float arcFade = 1f - Mathf.Clamp01((t - arcTime * 0.6f) / (arcTime * 0.4f));
        arcMat.SetFloat("_Intensity", Mathf.Lerp(7f, 10f, power) * arcFade);

        bool refresh = Time.time >= nextRefresh;
        if (refresh)
            nextRefresh = Time.time + ArcRefresh;

        // Onde electrique au sol, avec l'onde de choc
        bool ringOn = t < WaveTime * 0.95f && waveRadius > 0.05f;
        electricRing.enabled = ringOn;
        if (ringOn && refresh)
            CrackleRing(electricRing, center + normal * 0.08f, waveRadius);

        foreach (var a in arcs)
        {
            float now = Time.time;
            if (now >= a.dies && !a.toTarget && t < arcTime)
            {
                // Renait ailleurs : la zone crepite tant que dure la decharge
                a.end = RandomArcEnd();
                a.born = now + Random.Range(0f, 0.03f);
                a.dies = a.born + Random.Range(0.08f, 0.2f) * (1f + power * 0.5f);
            }
            bool on = now >= a.born && now < a.dies && t < arcTime;
            a.line.enabled = on;
            if (a.fork != null)
                a.fork.enabled = on && a.fork.positionCount > 1;
            if (!on || !refresh)
                continue;
            Vector3 end = center + a.end + Random.insideUnitSphere * (radius * 0.06f);
            Bolt(a.line, center, end, a.toTarget ? 0.1f : 0.16f, a.toTarget ? 14 : 10);
            if (a.fork != null)
            {
                // Branche : part d'un point de l'arc vers le cote
                int i = Random.Range(2, a.line.positionCount - 2);
                Vector3 from = a.line.GetPosition(i);
                Vector3 side = Vector3.ProjectOnPlane(Random.onUnitSphere, end - center).normalized;
                Vector3 to = from + (side + (end - center).normalized * 0.6f).normalized * (radius * Random.Range(0.2f, 0.4f));
                Bolt(a.fork, from, to, 0.2f, 5);
                a.fork.enabled = true;
            }
        }

        // Brulure : apparait vite, reste, s'efface
        if (scorchMat != null)
        {
            float alpha = Mathf.Clamp01(t / 0.15f) * (1f - Mathf.Clamp01((t - ScorchTime * 0.5f) / (ScorchTime * 0.5f)));
            scorchMat.SetColor("_Tint", new Color(0.05f, 0.03f, 0.08f, 0.85f * alpha));
        }

        if (t >= life)
            Destroy(gameObject);
    }

    // Eclair : ligne brisee de a vers b, ecart maximal au milieu (enveloppe en sinus)
    private static void Bolt(LineRenderer lr, Vector3 a, Vector3 b, float jag, int segments)
    {
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 1e-3f)
        {
            lr.positionCount = 0;
            return;
        }
        Vector3 fwd = dir / len;
        Vector3 side = Vector3.Cross(fwd, Mathf.Abs(fwd.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 up = Vector3.Cross(fwd, side);
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float s = i / (float)segments;
            Vector3 p = a + dir * s;
            if (i > 0 && i < segments)
            {
                float env = Mathf.Sin(s * Mathf.PI);
                p += (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * (jag * len * env);
            }
            lr.SetPosition(i, p);
        }
    }

    // Cercle crepitant dans le plan de la surface touchee
    private void CrackleRing(LineRenderer lr, Vector3 center, float r)
    {
        Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 v = Vector3.Cross(normal, u);
        int n = lr.positionCount;
        float phase = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n + phase;
            float rr = r * (1f + Random.Range(-0.06f, 0.06f));
            lr.SetPosition(i, center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rr + normal * Random.Range(-0.05f, 0.15f) * r * 0.25f);
        }
    }

    private void OnDestroy()
    {
        foreach (var m in new[] { coreMat, glowMat, waveMat, ringMat, arcMat, scorchMat, tintMat })
        {
            if (m != null)
                Destroy(m);
        }
    }

    private static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

    // ---------- Maillages partages ----------

    // Sphere unite (rayon 0,5, comme la primitive Unity), sans collider
    public static Mesh SphereMesh()
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

    // Carre de 1 m a plat (+Y) pour la brulure : le shader DustParticle y dessine un
    // disque doux ; uv.z = 0,5 tient le fondu d'age du shader a 1 (pas de particule ici)
    private static Mesh DiscMesh()
    {
        if (discMesh != null)
            return discMesh;
        discMesh = new Mesh { name = "PlasmaScorch" };
        discMesh.SetVertices(new List<Vector3> { new(-0.5f, 0f, -0.5f), new(0.5f, 0f, -0.5f), new(0.5f, 0f, 0.5f), new(-0.5f, 0f, 0.5f) });
        discMesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
        discMesh.SetUVs(0, new List<Vector4> { new(0f, 0f, 0.5f, 0f), new(1f, 0f, 0.5f, 0f), new(1f, 1f, 0.5f, 0f), new(0f, 1f, 0.5f, 0f) });
        discMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        discMesh.RecalculateBounds();
        return discMesh;
    }
}
