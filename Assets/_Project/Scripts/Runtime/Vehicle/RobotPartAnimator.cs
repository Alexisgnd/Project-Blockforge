using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// =========================================================
// ANIMATION DES BLOCS DE MOUVEMENT (lit RobotLocomotion)
// - Roues : le groupe "wheel" du modele tourne autour de
//   l'essieu a la vitesse du sol sous la roue (v / r) et suit
//   la suspension (monte quand elle se comprime)
// - Chenilles : Animator du modele (TankTrackBlockSetup),
//   parametres Roll (sens) et RollSpeed (vitesse du sol)
// - Pattes : demarche procedurale. Un pivot a la hanche porte
//   bras, genou et tibia : balancement avant/arriere et lever du
//   pied, en opposition de phase une patte sur deux, cadence
//   selon la vitesse
// - Lames de survol : les pales / turbines au centre du conduit
//   tournent (plus vite quand le robot accelere)
// - Rotors : les pales blade_N tournent selon la portance
// - Propulseurs : anneaux lumineux et jet (cone additif du
//   shader Blockforge/Hologram) selon la poussee
// - Gouvernes : les surfaces de controle braquent avec Q/D
// Un parent en echelle negative (bloc pose en miroir) inverse
// le sens des rotations locales : il est compense.
// =========================================================

[RequireComponent(typeof(RobotLocomotion))]
public class RobotPartAnimator : MonoBehaviour
{
    private static readonly Regex RudderSurfaceName =
        new(@"control_surface|aft_surface|control_vane|rear_flap|trailing_blade|aft_swept_blade", RegexOptions.IgnoreCase);
    private static readonly int RollId = Animator.StringToHash("Roll");
    private static readonly int RollSpeedId = Animator.StringToHash("RollSpeed");
    private static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");

    // Rotation d'une piece autour d'un axe passant par un pivot, dans le repere de son parent
    private class Spinner
    {
        public Transform t;
        public Vector3 basePos, pivot, axis;
        public Quaternion baseRot;
        public float sign = 1f;

        public void Set(float angleDeg, Vector3 offset = default)
        {
            var q = Quaternion.AngleAxis(angleDeg * sign, axis);
            t.localPosition = pivot + q * (basePos - pivot) + offset;
            t.localRotation = q * baseRot;
        }
    }

    private class Animated
    {
        public LocomotionUnit unit;
        public readonly List<Spinner> spinners = new();
        public float angle;               // degres cumules (roues, rotors, pales)
        public float phase;               // cycles (pattes)
        public float phaseOffset;
        public Vector3 suspensionAxis;    // repere du parent (roues)
        public Vector3 swingAxis, liftAxis; // repere du parent (pattes)
        public Animator animator;
        public Renderer[] glows;
        public Color[] glowBase;
        public Transform flame;
        public float flameLength;
    }

    private RobotLocomotion loco;
    private readonly List<Animated> animated = new();
    private MaterialPropertyBlock block;
    private Material flameMaterial;

    public void Build(RobotLocomotion locomotion)
    {
        loco = locomotion;
        block = new MaterialPropertyBlock();
        animated.Clear();
        foreach (var unit in loco.units)
        {
            var a = new Animated { unit = unit };
            switch (unit.kind)
            {
                case LocomotionKind.Wheel: BuildWheel(a); break;
                case LocomotionKind.Track: a.animator = unit.visual.GetComponentInChildren<Animator>(); break;
                case LocomotionKind.Leg: BuildLeg(a); break;
                case LocomotionKind.Hover: BuildHover(a); break;
                case LocomotionKind.Rotor: BuildRotor(a); break;
                case LocomotionKind.Thruster: BuildThruster(a); break;
                case LocomotionKind.Rudder: BuildRudder(a); break;
            }
            animated.Add(a);
        }
    }

    private void OnDestroy()
    {
        if (flameMaterial != null)
            Destroy(flameMaterial);
    }

    // ---------- Construction ----------

    private Spinner MakeSpinner(Transform t, Vector3 pivotRoot, Vector3 axisRoot)
    {
        var parent = t.parent;
        var s = new Spinner
        {
            t = t,
            basePos = t.localPosition,
            baseRot = t.localRotation,
            pivot = parent.InverseTransformPoint(transform.TransformPoint(pivotRoot)),
            axis = parent.InverseTransformVector(transform.TransformDirection(axisRoot)).normalized,
        };
        var ls = parent.lossyScale;
        if (ls.x * ls.y * ls.z < 0f)
            s.sign = -1f;
        return s;
    }

    private Vector3 ParentDirection(Transform parent, Vector3 rootDir) =>
        parent.InverseTransformVector(transform.TransformDirection(rootDir));

    private void BuildWheel(Animated a)
    {
        var group = RobotLocomotion.FindDeep(a.unit.visual, "wheel");
        if (group == null)
            return;
        // Essieu oriente comme la droite du robot : rouler vers l'avant = angle positif
        Vector3 axle = a.unit.axis;
        if (Vector3.Dot(axle, Vector3.right) < 0f)
            axle = -axle;
        a.spinners.Add(MakeSpinner(group, a.unit.center, axle));
        a.suspensionAxis = ParentDirection(group.parent, Vector3.up);
    }

    private void BuildLeg(Animated a)
    {
        var visual = a.unit.visual;
        var hip = RobotLocomotion.FindDeep(visual, "pitch_pin") ?? RobotLocomotion.FindDeep(visual, "hip_mount");
        var parts = new List<Transform>();
        foreach (var name in new[] { "upper_arm", "knee_joint", "lower_leg" })
        {
            var p = RobotLocomotion.FindDeep(visual, name);
            if (p != null)
                parts.Add(p);
        }
        if (hip == null || parts.Count == 0)
            return;

        // Pivot de hanche : tout ce qui bouge passe dessous
        var parent = parts[0].parent;
        var pivot = new GameObject("LegPivot").transform;
        pivot.SetParent(parent, false);
        Vector3 hipWorld = hip.GetComponent<Renderer>() != null ? hip.GetComponent<Renderer>().bounds.center : hip.position;
        pivot.position = hipWorld;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        foreach (var p in parts)
            p.SetParent(pivot, true);

        var s = new Spinner { t = pivot, basePos = pivot.localPosition, baseRot = pivot.localRotation, pivot = pivot.localPosition };
        a.spinners.Add(s);
        Vector3 outward = Vector3.ProjectOnPlane(a.unit.contacts.Count > 0 ? a.unit.contacts[0].localOrigin - loco.LocalPoint(hip) : Vector3.right, Vector3.up);
        if (outward.sqrMagnitude < 1e-4f)
            outward = Vector3.right;
        outward.Normalize();
        a.swingAxis = ParentDirection(parent, Vector3.up).normalized;
        // Lever le pied = tourner autour de (outward x haut) dans le sens qui monte le pied
        a.liftAxis = ParentDirection(parent, Vector3.Cross(outward, Vector3.up)).normalized;
        var ls = parent.lossyScale;
        if (ls.x * ls.y * ls.z < 0f)
            s.sign = -1f;

        // Une patte sur deux en opposition de phase (diagonales ensemble)
        Vector3 c = a.unit.center;
        bool left = c.x < 0f, front = c.z > 0f;
        a.phaseOffset = left ^ front ? 0.5f : 0f;
    }

    private void BuildHover(Animated a)
    {
        var visual = a.unit.visual;
        float limit = a.unit.radius * 0.92f;
        var fans = new List<Transform>();
        foreach (var r in visual.GetComponentsInChildren<Renderer>())
        {
            if (!RobotLocomotion.IsHoverFan(r.name))
                continue;
            if (!loco.TryGetLocalBounds(r.transform, out var b))
                continue;
            // Seulement ce qui tient dans le conduit (pas les fixations exterieures)
            Vector3 d = Vector3.ProjectOnPlane(b.center - a.unit.center, a.unit.axis);
            if (d.magnitude + Mathf.Max(b.extents.x, b.extents.z) * 0.5f <= limit)
                fans.Add(r.transform);
        }
        foreach (var f in fans)
            a.spinners.Add(MakeSpinner(f, a.unit.center, a.unit.axis));
    }

    private void BuildRotor(Animated a)
    {
        var visual = a.unit.visual;
        for (int i = 1; i <= 8; i++)
        {
            var blade = RobotLocomotion.FindDeep(visual, $"blade_{i}");
            if (blade == null)
                break;
            Vector3 axis = a.unit.axis;
            if (Vector3.Dot(axis, Vector3.up) < 0f)
                axis = -axis;
            a.spinners.Add(MakeSpinner(blade, a.unit.center, axis));
        }
    }

    private void BuildThruster(Animated a)
    {
        var glows = new List<Renderer>();
        var bases = new List<Color>();
        foreach (var r in a.unit.visual.GetComponentsInChildren<Renderer>())
        {
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty(EmissiveId))
                continue;
            Color e = m.GetColor(EmissiveId);
            if (e.maxColorComponent > 0.01f)
            {
                glows.Add(r);
                bases.Add(e);
            }
        }
        a.glows = glows.ToArray();
        a.glowBase = bases.ToArray();

        // Jet : cone additif sortant de la tuyere (l'oppose de la poussee)
        var shader = Shader.Find("Blockforge/Hologram");
        if (shader == null)
            return;
        if (flameMaterial == null)
        {
            flameMaterial = new Material(shader) { name = "ThrusterFlame (instance)" };
            flameMaterial.SetFloat("_Beam", 1f);
            flameMaterial.SetColor("_Color", new Color(0.35f, 0.8f, 1f, 1f));
            flameMaterial.SetFloat("_Intensity", 3f);
            flameMaterial.SetFloat("_Flicker", 0.25f);
            flameMaterial.SetFloat("_LineDensity", 40f);
        }
        var go = new GameObject("ThrusterFlame", typeof(MeshFilter), typeof(MeshRenderer));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = a.unit.center;
        go.transform.localRotation = Quaternion.LookRotation(-a.unit.axis);
        float r0 = a.unit.radius * 0.75f;
        go.GetComponent<MeshFilter>().sharedMesh = BuildCone(r0, r0 * 0.15f, 1f, 20);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = flameMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        a.flame = go.transform;
        a.flameLength = a.unit.radius * 4f;
        go.SetActive(false);
    }

    private void BuildRudder(Animated a)
    {
        foreach (var r in a.unit.visual.GetComponentsInChildren<Renderer>())
        {
            if (!RudderSurfaceName.IsMatch(r.name) || !loco.TryGetLocalBounds(r.transform, out var b))
                continue;
            Vector3 hinge = a.unit.axis;
            // Corde : l'axe restant le plus long ; bord d'attaque = cote du centre de la gouverne
            int h = Mathf.Abs(hinge.x) > 0.5f ? 0 : Mathf.Abs(hinge.y) > 0.5f ? 1 : 2;
            int chord = -1;
            float best = -1f;
            for (int i = 0; i < 3; i++)
            {
                if (i != h && b.extents[i] > best)
                {
                    best = b.extents[i];
                    chord = i;
                }
            }
            Vector3 chordAxis = chord == 0 ? Vector3.right : chord == 1 ? Vector3.up : Vector3.forward;
            float side = Mathf.Sign(a.unit.center[chord] - b.center[chord]);
            Vector3 pivot = b.center + chordAxis * (side * b.extents[chord]);
            a.spinners.Add(MakeSpinner(r.transform, pivot, hinge));
        }
    }

    private static Mesh BuildCone(float r0, float r1, float length, int sides)
    {
        var mesh = new Mesh { name = "ThrusterFlame" };
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            var n = new Vector3(dir.x, dir.y, (r0 - r1) / length).normalized;
            verts.Add(dir * r0);
            verts.Add(dir * r1 + Vector3.forward * length);
            normals.Add(n);
            normals.Add(n);
            uvs.Add(new Vector2(i / (float)sides, 0f));
            uvs.Add(new Vector2(i / (float)sides, 1f));
        }
        for (int i = 0; i < sides; i++)
        {
            int b = i * 2;
            tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        }
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- Animation ----------

    private void LateUpdate()
    {
        if (loco == null)
            return;
        float dt = Time.deltaTime;
        foreach (var a in animated)
        {
            var u = a.unit;
            if (u.disabled)
                continue;
            switch (u.kind)
            {
                case LocomotionKind.Wheel:
                {
                    if (a.spinners.Count == 0)
                        break;
                    float r = Mathf.Max(0.05f, u.radius);
                    a.angle = Mathf.Repeat(a.angle + u.groundSpeed / r * Mathf.Rad2Deg * dt, 360f);
                    float bump = 0f;
                    if (u.contacts.Count > 0)
                    {
                        var c = u.contacts[0];
                        float sag = c.travel * 0.35f;
                        bump = Mathf.Clamp(c.grounded ? c.compression - sag : -c.travel * 0.3f, -c.travel * 0.5f, c.travel * 0.6f);
                    }
                    a.spinners[0].Set(a.angle, a.suspensionAxis * bump);
                    break;
                }
                case LocomotionKind.Track:
                    if (a.animator != null && a.animator.isActiveAndEnabled)
                    {
                        float speed = u.groundSpeed;
                        a.animator.SetFloat(RollId, Mathf.Abs(speed) < 0.15f ? 0f : Mathf.Sign(speed));
                        a.animator.SetFloat(RollSpeedId, Mathf.Clamp(Mathf.Abs(speed) / 1.9f, 0.2f, 8f));
                    }
                    break;
                case LocomotionKind.Leg:
                {
                    if (a.spinners.Count == 0)
                        break;
                    float speed = Mathf.Abs(u.groundSpeed);
                    float amount = Mathf.Clamp01(speed / 1.5f);
                    a.phase = Mathf.Repeat(a.phase + (speed / 1.1f + (amount > 0f ? 0.2f : 0f)) * dt, 1f);
                    float t = (a.phase + a.phaseOffset) * Mathf.PI * 2f;
                    float dir = Mathf.Sign(u.groundSpeed == 0f ? 1f : u.groundSpeed);
                    float swing = Mathf.Sin(t) * 24f * amount * dir;
                    float lift = Mathf.Max(0f, Mathf.Cos(t)) * 14f * amount;
                    var s = a.spinners[0];
                    s.t.localRotation = Quaternion.AngleAxis(swing * s.sign, a.swingAxis)
                                        * Quaternion.AngleAxis(-lift * s.sign, a.liftAxis) * s.baseRot;
                    break;
                }
                case LocomotionKind.Hover:
                    a.angle = Mathf.Repeat(a.angle + (420f + 900f * u.output) * dt, 360f);
                    foreach (var s in a.spinners)
                        s.Set(a.angle);
                    break;
                case LocomotionKind.Rotor:
                    a.angle = Mathf.Repeat(a.angle + (240f + 1500f * u.output) * dt, 360f);
                    foreach (var s in a.spinners)
                        s.Set(a.angle);
                    break;
                case LocomotionKind.Thruster:
                {
                    float fire = u.output;
                    if (a.glows != null)
                    {
                        float k = 0.35f + 2.4f * fire * (0.9f + 0.1f * Mathf.Sin(Time.time * 40f));
                        for (int i = 0; i < a.glows.Length; i++)
                        {
                            a.glows[i].GetPropertyBlock(block);
                            block.SetColor(EmissiveId, a.glowBase[i] * k);
                            a.glows[i].SetPropertyBlock(block);
                        }
                    }
                    if (a.flame != null)
                    {
                        bool on = fire > 0.03f;
                        if (a.flame.gameObject.activeSelf != on)
                            a.flame.gameObject.SetActive(on);
                        if (on)
                        {
                            float len = a.flameLength * (0.35f + 0.65f * fire) * (0.92f + 0.08f * Mathf.Sin(Time.time * 55f));
                            a.flame.localScale = new Vector3(1f, 1f, len);
                        }
                    }
                    break;
                }
                case LocomotionKind.Rudder:
                {
                    float target = -u.output * 25f;
                    a.angle = Mathf.MoveTowards(a.angle, target, 160f * dt);
                    foreach (var s in a.spinners)
                        s.Set(a.angle);
                    break;
                }
            }
        }
    }
}
