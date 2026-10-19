using UnityEngine;

// =========================================================
// BOULE DE PLASMA EN VOL (habillage du WeaponProjectile)
// Pose par WeaponFireController.FirePlasma sur la sphere du tir :
// - Halo : sphere hologramme plus grande, bords de Fresnel
//   brillants (l'energie deborde du coeur)
// - Arcs : petits eclairs qui crepitent autour de la boule (2 a
//   N1, 5 a N6), regeneres en continu
// Materiaux partages fournis par le tireur (pas detruits ici).
// =========================================================

public class PlasmaBoltFX : MonoBehaviour
{
    private const float Refresh = 0.03f;

    private LineRenderer[] arcs;
    private float nextRefresh;

    public void Init(Material haloMaterial, Material arcMaterial, float power)
    {
        if (haloMaterial != null)
        {
            var halo = new GameObject("Halo", typeof(MeshFilter), typeof(MeshRenderer));
            halo.transform.SetParent(transform, false);
            halo.transform.localScale = Vector3.one * 2.1f;
            halo.GetComponent<MeshFilter>().sharedMesh = PlasmaExplosion.SphereMesh();
            var mr = halo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = haloMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        if (arcMaterial == null)
            return;
        int count = Mathf.RoundToInt(Mathf.Lerp(2f, 5f, power));
        arcs = new LineRenderer[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("BoltArc");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = arcMaterial;
            lr.startWidth = 0.035f + 0.02f * power;
            lr.endWidth = 0.012f;
            lr.positionCount = 6;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            arcs[i] = lr;
        }
    }

    private void LateUpdate()
    {
        if (arcs == null || Time.time < nextRefresh)
            return;
        nextRefresh = Time.time + Refresh;
        float r = transform.lossyScale.x * 0.5f;
        Vector3 c = transform.position;
        foreach (var lr in arcs)
        {
            // Saute d'un point de la surface a un autre en passant au large
            Vector3 a = Random.onUnitSphere;
            Vector3 b = (a + Random.insideUnitSphere * 1.3f).normalized;
            lr.enabled = Random.value < 0.85f;
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float s = i / (float)(n - 1);
                Vector3 dir = Vector3.Slerp(a, b, s).normalized;
                float lift = 1f + Mathf.Sin(s * Mathf.PI) * 0.7f + Random.Range(-0.15f, 0.15f);
                lr.SetPosition(i, c + dir * (r * lift));
            }
        }
    }
}
