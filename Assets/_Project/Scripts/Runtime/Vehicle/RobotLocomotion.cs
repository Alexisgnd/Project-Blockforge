using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// =========================================================
// LOCOMOTION PHYSIQUE D'UN ROBOT ASSEMBLE (scene de test, puis jeu)
// Corps rigide libre (plus de rotations gelees) : gravite,
// basculement, chocs. Les blocs de mouvement du robot (famille
// de la BlockDefinition) produisent les forces :
// - Roues, chenilles, pattes : suspension par rayon (ressort +
//   amortisseur) a chaque point de contact, adherence laterale
//   et traction au point de contact, limitees par le frottement
//   (μ x charge). Direction : la derive laterale est mesuree par
//   rapport au virage demande, donc le robot tourne sans etre
//   retenu par ses propres pneus (comme un char, mais en douceur)
// - Lames de survol : meme suspension, mais plus haute et plus
//   souple (le robot flotte), peu d'adherence laterale (il glisse)
// - Rotors : portance repartie sur chaque rotor (Espace monte,
//   Ctrl descend, sinon l'altitude est tenue), assiette pilotee
//   (Z/S inclinent le robot, qui avance en s'inclinant), lacet Q/D
// - Propulseurs : poussee dans l'axe de leur tuyere quand ils
//   regardent vers l'avant (Z) ou vers le haut (Espace)
// - Gouvernes : couple de lacet proportionnel a la vitesse et
//   amortissement de la derive (un robot rapide tourne mieux)
// Les colliders des blocs de mouvement au sol deviennent des
// triggers : le robot repose sur ses suspensions, pas sur les
// boites d'empreinte (le chassis, lui, heurte toujours le decor).
// Masse = poids reel / 100 ; centre de masse abaisse (stabilite).
// Suspension au repos = rayon + GroundSag x debattement : a
// l'equilibre (le ressort s'enfonce de GroundSag x debattement
// sous le poids) le pneu, la chenille ou le pied touche le sol.
// (Avant : rayon + debattement complet, le robot flottait de
// 0,65 x debattement, ~26 cm sur une roue Scout.) Les lames de
// survol gardent tout leur debattement : elles doivent flotter.
// Les animations des pieces sont faites par RobotPartAnimator a
// partir de l'etat expose ici (vitesse aux contacts, compression,
// portance, poussee) ; RobotGroundFX lit la derive laterale de
// chaque contact (slip) et la part d'adherence consommee par la
// traction (traction) pour la poussiere des roues. Un bloc de
// mouvement detruit (RobotHealth -> SetUnitDisabled) ne porte plus.
// =========================================================

public enum LocomotionKind { Wheel, Track, Leg, Hover, Rotor, Thruster, Rudder }

public class LocomotionContact
{
    public Vector3 localOrigin;      // repere de la racine du robot
    public float rest;               // longueur de suspension au repos (rayon + debattement)
    public float travel;
    public float stiffness, damping;
    public bool grounded;
    public float compression;        // m (0 = suspension detendue)
    public float lastCompression;
    public Vector3 point, normal;
    public float forwardSpeed;       // vitesse du sol sous le contact, le long de l'avant du robot (m/s)
    public float slip;               // derive laterale (m/s, signee) par rapport au virage demande : derapage
    public Vector3 lateral;          // direction laterale du sol sous le contact (monde)
}

public class LocomotionUnit
{
    public LocomotionKind kind;
    public PlacedBlockView view;
    public Transform visual;
    public int tier = 1;             // niveau du bloc (N1..N7, Recon/Invader/Assault) : vitesse et poussee
    public readonly List<LocomotionContact> contacts = new();
    public float maxSpeed;           // m/s
    public float driveAccel;         // m/s^2
    public float lateralGrip;        // 0..1
    public float friction;           // μ
    // Etat lu par RobotPartAnimator
    public float output;             // 0..1 : poussee, portance ou activite
    public float groundSpeed;        // m/s le long de l'avant (moyenne des contacts)
    public float traction;           // 0..1 : part de l'adherence consommee par la traction (1 = patine)
    public bool anyGrounded;
    public bool disabled;            // bloc detruit (RobotHealth) : ne porte plus, ne pousse plus
    // Geometrie mesuree au montage (repere de la racine)
    public Vector3 center;           // centre du visuel
    public Vector3 axis;             // axe propre : essieu, axe de rotor, axe de poussee
    public float radius;
}

[RequireComponent(typeof(Rigidbody))]
public class RobotLocomotion : MonoBehaviour
{
    // ---------- Reglages par famille ----------
    [Header("Vitesses maximales (m/s)")]
    public float wheelSpeed = 16f;
    public float trackSpeed = 10f;
    public float legSpeed = 6f;
    public float hoverSpeed = 14f;
    public float rotorSpeed = 14f;
    public float boostMultiplier = 1.5f;

    [Header("Conduite")]
    public float turnRateDeg = 95f;
    [Tooltip("Abaissement du centre de masse, en part de la hauteur du robot")]
    public float centerOfMassDrop = 0.4f;
    [Tooltip("Hauteur de vol des lames de survol (m)")]
    public float hoverHeight = 1.4f;
    public float rotorClimbSpeed = 6f;

    // ---------- Entrees (RobotTestDriver) ----------
    [HideInInspector] public float throttle;   // -1..1 (Z / S)
    [HideInInspector] public float steer;      // -1..1 (Q / D)
    [HideInInspector] public float lift;       // -1..1 (Espace / Ctrl)
    [HideInInspector] public bool boost;

    public readonly List<LocomotionUnit> units = new();
    public Rigidbody Body { get; private set; }
    public float Speed => Body != null ? Body.linearVelocity.magnitude : 0f;
    public int GroundedContacts { get; private set; }
    public int TotalContacts { get; private set; }
    public bool HasRotors { get; private set; }
    public float RobotHeight { get; private set; } = 1f;

    private readonly RaycastHit[] hits = new RaycastHit[16];

    // Part du debattement enfoncee par le poids a l'equilibre (roues, chenilles,
    // pattes) ; RobotPartAnimator utilise la meme valeur pour poser les visuels
    public const float GroundSag = 0.35f;
    private float cellSize = 1f;

    private static readonly Regex HoverFanName = new(@"blade|rotor_\d|impeller|turbine|tri_blade", RegexOptions.IgnoreCase);

    // =========================================================
    // MONTAGE
    // =========================================================

    public void Setup(float cell, int weightKg)
    {
        cellSize = Mathf.Max(0.01f, cell);
        Body = GetComponent<Rigidbody>();
        Body.mass = Mathf.Max(2f, weightKg / 100f);
        Body.constraints = RigidbodyConstraints.None;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Body.linearDamping = 0.05f;
        Body.angularDamping = 1.5f;
        Body.maxAngularVelocity = 8f;

        units.Clear();
        foreach (var view in GetComponentsInChildren<PlacedBlockView>())
        {
            var unit = BuildUnit(view);
            if (unit != null)
                units.Add(unit);
        }

        // Centre de masse automatique (colliders), abaisse : le robot se couche moins
        if (TryGetLocalBounds(transform, out var robotBounds))
            RobotHeight = Mathf.Max(0.5f, robotBounds.size.y);
        Body.ResetCenterOfMass();
        Body.centerOfMass -= Vector3.up * (RobotHeight * centerOfMassDrop);

        // Repartition de la charge entre les contacts porteurs
        int supporting = 0;
        foreach (var u in units)
            supporting += u.contacts.Count;
        TotalContacts = supporting;
        float share = Body.mass / Mathf.Max(1, supporting);
        float g = -Physics.gravity.y;
        foreach (var u in units)
        {
            foreach (var c in u.contacts)
            {
                float sag = u.kind == LocomotionKind.Hover ? 0.5f : GroundSag;
                c.stiffness = share * g / Mathf.Max(0.02f, sag * c.travel);
                float ratio = u.kind == LocomotionKind.Hover ? 0.35f : 0.55f;
                c.damping = 2f * Mathf.Sqrt(c.stiffness * share) * ratio;
            }
        }

        string summary = "";
        foreach (LocomotionKind k in System.Enum.GetValues(typeof(LocomotionKind)))
        {
            int n = units.FindAll(u => u.kind == k).Count;
            if (n > 0)
                summary += $"{k} x{n} ";
        }
        Debug.Log($"[RobotLocomotion] {units.Count} bloc(s) de mouvement : {summary}; masse {Body.mass:0.#}, {supporting} point(s) d'appui.");
    }

    private LocomotionUnit BuildUnit(PlacedBlockView view)
    {
        var def = view.definition;
        if (def == null || def.category != BlockCategory.Mouvement)
            return null;
        var visual = BlockColliders.Visual(view.transform);
        if (visual == null)
            return null;

        string family = (def.family ?? "").ToLowerInvariant();
        var unit = new LocomotionUnit { view = view, visual = visual, tier = TierOf(def.name) };
        if (!TryGetLocalBounds(visual, out var vb))
            return null;
        unit.center = vb.center;

        if (family.Contains("wheel"))
        {
            unit.kind = LocomotionKind.Wheel;
            var group = FindDeep(visual, "wheel");
            if (group == null || !TryGetLocalBounds(group, out var wb))
                wb = vb;
            int axle = SmallestAxis(wb.extents);
            unit.axis = Axis(axle);
            unit.center = wb.center;
            unit.radius = LargestOther(wb.extents, axle);
            AddContact(unit, wb.center, unit.radius, unit.radius * 0.4f);
            SetFamily(unit, wheelSpeed, 14f, 0.85f, 1.15f);
        }
        else if (family.Contains("track"))
        {
            unit.kind = LocomotionKind.Track;
            int along = Mathf.Abs(vb.extents.x) > Mathf.Abs(vb.extents.z) ? 0 : 2;
            unit.axis = Axis(along);
            unit.radius = vb.extents.y * 0.85f;
            float half = Mathf.Max(0f, vb.extents[along] - unit.radius);
            foreach (float f in new[] { -0.8f, 0f, 0.8f })
                AddContact(unit, vb.center + Axis(along) * (half * f), unit.radius, unit.radius * 0.35f);
            SetFamily(unit, trackSpeed, 10f, 0.95f, 1.3f);
        }
        else if (family.Contains("leg"))
        {
            unit.kind = LocomotionKind.Leg;
            var foot = FindDeep(visual, "foot_tip");
            var hip = FindDeep(visual, "pitch_pin") ?? FindDeep(visual, "hip_mount");
            Vector3 footPos = foot != null ? LocalPoint(foot) : vb.center;
            float hipY = hip != null && TryGetLocalBounds(hip, out var hb) ? hb.center.y : vb.max.y;
            if (foot != null && TryGetLocalBounds(foot, out var fb))
                footPos.y = fb.min.y;
            else
                footPos.y = vb.min.y;
            float reach = Mathf.Max(0.2f, hipY - footPos.y);
            AddContact(unit, new Vector3(footPos.x, hipY, footPos.z), reach, reach * 0.25f);
            unit.center = new Vector3(footPos.x, hipY, footPos.z);
            SetFamily(unit, legSpeed, 8f, 0.9f, 1.2f);
        }
        else if (family.Contains("hover"))
        {
            unit.kind = LocomotionKind.Hover;
            int duct = SmallestAxis(vb.extents);
            unit.axis = Axis(duct);
            unit.radius = LargestOther(vb.extents, duct);
            float height = hoverHeight * cellSize + vb.extents.y;
            AddContact(unit, vb.center, height, height * 0.6f, onGround: false);
            SetFamily(unit, hoverSpeed, 9f, 0.18f, 0.6f);
        }
        else if (family.Contains("rotor"))
        {
            unit.kind = LocomotionKind.Rotor;
            int hub = SmallestAxis(vb.extents);
            unit.axis = Axis(hub);
            unit.radius = LargestOther(vb.extents, hub);
            HasRotors = true;
        }
        else if (family.Contains("thruster"))
        {
            unit.kind = LocomotionKind.Thruster;
            // Axe de poussee : de la tuyere vers l'arriere du propulseur (la poussee s'oppose au jet)
            var nozzle = FindDeep(visual, "nozzle_lip");
            var core = FindDeep(visual, "core_housing");
            if (nozzle != null && core != null && TryGetLocalBounds(nozzle, out var nb) && TryGetLocalBounds(core, out var cb))
            {
                unit.axis = (cb.center - nb.center).normalized;
                unit.center = nb.center;
                unit.radius = Mathf.Max(nb.extents.x, nb.extents.y, nb.extents.z);
            }
            else
            {
                unit.axis = Vector3.forward;
                unit.radius = 0.3f;
            }
        }
        else if (family.Contains("rudder"))
        {
            unit.kind = LocomotionKind.Rudder;
            unit.axis = Axis(LargestAxis(vb.extents));
        }
        else
        {
            return null;
        }

        // Au sol : le robot repose sur ses suspensions, pas sur ces boites
        if (unit.contacts.Count > 0)
        {
            foreach (var col in view.GetComponentsInChildren<Collider>())
                col.isTrigger = true;
        }
        return unit;
    }

    // Niveau d'un bloc d'apres le nom de son asset : _N3 -> 3 ; rotors Recon /
    // Invader / Assault -> 1 / 2 / 3. Chaque niveau au-dessus du premier ajoute
    // TierStep (30 %) de vitesse de pointe, de montee et de poussee.
    public const float TierStep = 0.3f;
    public static int TierOf(string assetName)
    {
        if (string.IsNullOrEmpty(assetName))
            return 1;
        var m = Regex.Match(assetName, @"_N(\d)");
        if (m.Success)
            return Mathf.Clamp(int.Parse(m.Groups[1].Value), 1, 9);
        string n = assetName.ToLowerInvariant();
        if (n.Contains("assault")) return 3;
        if (n.Contains("invader")) return 2;
        return 1;
    }
    public static float TierFactor(int tier) => 1f + TierStep * Mathf.Max(0, tier - 1);

    private static void SetFamily(LocomotionUnit unit, float maxSpeed, float accel, float grip, float friction)
    {
        unit.maxSpeed = maxSpeed;
        unit.driveAccel = accel;
        unit.lateralGrip = grip;
        unit.friction = friction;
    }

    // onGround : roue, chenille, patte -> au repos, le contact touche le sol
    // (seule la marge d'enfoncement est ajoutee au rayon) ; survol -> flotte
    private static void AddContact(LocomotionUnit unit, Vector3 origin, float radius, float travel, bool onGround = true)
    {
        float rest = radius + (onGround ? travel * GroundSag : travel);
        unit.contacts.Add(new LocomotionContact { localOrigin = origin, rest = rest, travel = travel });
    }

    // =========================================================
    // FORCES
    // =========================================================

    private void FixedUpdate()
    {
        if (Body == null)
            return;
        float dt = Time.fixedDeltaTime;
        Vector3 up = transform.up;
        Vector3 forward = transform.forward;
        Vector3 v = Body.linearVelocity;
        float yawRate = Vector3.Dot(Body.angularVelocity, up);
        float boostK = boost ? boostMultiplier : 1f;
        float targetYaw = steer * turnRateDeg * Mathf.Deg2Rad;

        int grounded = 0;
        Vector3 groundNormal = Vector3.zero;

        // ---------- Suspensions, adherence, traction ----------
        foreach (var unit in units)
        {
            if (unit.contacts.Count == 0 || unit.disabled)
                continue;
            float unitSpeed = 0f;
            float unitTraction = 0f;
            int unitGrounded = 0;
            float share = Body.mass / Mathf.Max(1, TotalContacts);

            foreach (var c in unit.contacts)
            {
                Vector3 origin = transform.TransformPoint(c.localOrigin);
                c.grounded = Cast(origin, -up, c.rest + 0.2f, out var hit) && hit.distance <= c.rest;
                if (!c.grounded)
                {
                    c.compression = 0f;
                    c.lastCompression = 0f;
                    c.slip = 0f;
                    continue;
                }

                c.point = hit.point;
                c.normal = hit.normal;
                c.compression = Mathf.Min(c.rest - hit.distance, c.travel * 1.5f);
                float compressionSpeed = (c.compression - c.lastCompression) / dt;
                c.lastCompression = c.compression;
                float load = Mathf.Max(0f, c.stiffness * c.compression + c.damping * compressionSpeed);
                // Butee : au-dela du debattement, la suspension devient tres dure
                if (c.compression > c.travel)
                    load += c.stiffness * 4f * (c.compression - c.travel);
                Body.AddForceAtPosition(up * load, origin);

                grounded++;
                unitGrounded++;
                groundNormal += hit.normal;

                // Repere du sol sous le contact
                Vector3 fwd = Vector3.ProjectOnPlane(forward, hit.normal).normalized;
                Vector3 lat = Vector3.Cross(hit.normal, fwd);
                Vector3 pv = Body.GetPointVelocity(hit.point);
                float vf = Vector3.Dot(pv, fwd);
                c.forwardSpeed = vf;
                unitSpeed += vf;

                // Derive laterale par rapport au virage demande
                Vector3 r = hit.point - Body.worldCenterOfMass;
                Vector3 turnVelocity = Vector3.Cross(up * targetYaw, r);
                float slip = Vector3.Dot(pv - turnVelocity, lat);
                c.slip = slip;
                c.lateral = lat;
                float maxForce = unit.friction * Mathf.Max(load, share * 2f);
                float lateral = Mathf.Clamp(-slip * share * unit.lateralGrip / dt, -maxForce, maxForce);

                // Traction vers la vitesse demandee (frein moteur sans gaz)
                float target = throttle * unit.maxSpeed * boostK;
                float response = Mathf.Abs(throttle) > 0.05f ? 1f : 0.35f;
                float longi = Mathf.Clamp((target - vf) * response, -unit.driveAccel, unit.driveAccel) * share;
                // Part de l'adherence consommee (RobotGroundFX : poussiere d'acceleration)
                unitTraction += Mathf.Clamp01(Mathf.Abs(longi) / Mathf.Max(maxForce, 1e-3f));
                longi = Mathf.Clamp(longi, -maxForce, maxForce);

                // Appliquees a la hauteur du centre de masse : ni roulis dans les
                // virages, ni cabrage a l'acceleration (conduite arcade stable)
                Vector3 at = hit.point + up * Vector3.Dot(Body.worldCenterOfMass - hit.point, up);
                Body.AddForceAtPosition(lat * lateral + fwd * longi, at);
            }

            unit.anyGrounded = unitGrounded > 0;
            unit.traction = unitGrounded > 0 ? unitTraction / unitGrounded : 0f;
            unit.groundSpeed = unitGrounded > 0 ? unitSpeed / unitGrounded : Mathf.Lerp(unit.groundSpeed, throttle * unit.maxSpeed * boostK, 2f * dt);
            unit.output = unit.kind == LocomotionKind.Hover ? (unit.anyGrounded ? 0.6f : 0.3f) + 0.4f * Mathf.Abs(throttle) : Mathf.Abs(throttle);
        }
        GroundedContacts = grounded;
        float groundedFraction = TotalContacts > 0 ? (float)grounded / TotalContacts : 0f;

        // ---------- Virage (au sol) ----------
        if (grounded > 0)
        {
            float yawAccel = (targetYaw - yawRate) * 6f * Mathf.Clamp01(groundedFraction * 1.5f);
            Body.AddTorque(up * yawAccel, ForceMode.Acceleration);

            // Tient l'assiette sur le sol (sans empecher de basculer sur un gros choc)
            Vector3 n = groundNormal.normalized;
            Vector3 align = Vector3.Cross(up, n);
            Vector3 tiltRate = Vector3.ProjectOnPlane(Body.angularVelocity, up);
            Body.AddTorque((align * 12f - tiltRate * 3f) * groundedFraction, ForceMode.Acceleration);
        }
        else if (!HasRotors)
        {
            // En l'air : se remet doucement a plat pour retomber sur ses appuis
            Vector3 align = Vector3.Cross(up, Vector3.up);
            Body.AddTorque(align * 2.5f - Vector3.ProjectOnPlane(Body.angularVelocity, up) * 0.8f, ForceMode.Acceleration);
        }

        ApplyRotors(dt, up, forward, yawRate);
        ApplyThrusters(up, forward);
        ApplyRudders(v, up, forward);
    }

    // Bloc de mouvement detruit ou repare (RobotHealth) : la charge se repartit
    // sur les appuis restants
    public void SetUnitDisabled(PlacedBlockView view, bool disabled)
    {
        bool changed = false;
        foreach (var u in units)
        {
            if (u.view == view && u.disabled != disabled)
            {
                u.disabled = disabled;
                u.anyGrounded = false;
                foreach (var c in u.contacts)
                {
                    c.grounded = false;
                    c.compression = c.lastCompression = 0f;
                }
                changed = true;
            }
        }
        if (!changed || Body == null)
            return;
        int supporting = 0;
        foreach (var u in units)
        {
            if (!u.disabled)
                supporting += u.contacts.Count;
        }
        TotalContacts = supporting;
        HasRotors = units.Exists(u => u.kind == LocomotionKind.Rotor && !u.disabled);
        float share = Body.mass / Mathf.Max(1, supporting);
        float g = -Physics.gravity.y;
        foreach (var u in units)
        {
            foreach (var c in u.contacts)
            {
                float sag = u.kind == LocomotionKind.Hover ? 0.5f : GroundSag;
                c.stiffness = share * g / Mathf.Max(0.02f, sag * c.travel);
                float ratio = u.kind == LocomotionKind.Hover ? 0.35f : 0.55f;
                c.damping = 2f * Mathf.Sqrt(c.stiffness * share) * ratio;
            }
        }
    }

    private void ApplyRotors(float dt, Vector3 up, Vector3 forward, float yawRate)
    {
        var rotors = units.FindAll(u => u.kind == LocomotionKind.Rotor && !u.disabled);
        if (rotors.Count == 0)
            return;

        float g = -Physics.gravity.y;
        // Niveau des rotors (le meilleur) : montee, inclinaison et vitesse de pointe
        int tier = 1;
        foreach (var rotor in rotors)
            tier = Mathf.Max(tier, rotor.tier);
        float k = TierFactor(tier);
        // Vitesse verticale demandee : Espace monte, Ctrl descend, sinon altitude tenue
        float vy = Body.linearVelocity.y;
        float wanted = lift * rotorClimbSpeed * k;
        float accel = g + (wanted - vy) * 2.5f;
        float verticality = Mathf.Max(0.35f, Vector3.Dot(up, Vector3.up));
        float total = Mathf.Clamp(Body.mass * accel / verticality, 0f, Body.mass * g * 2.5f);
        foreach (var rotor in rotors)
        {
            Vector3 pos = transform.TransformPoint(rotor.center);
            Body.AddForceAtPosition(up * (total / rotors.Count), pos);
            rotor.output = Mathf.Clamp01(total / (Body.mass * g * 1.5f));
        }

        // Assiette : Z/S inclinent vers l'avant/l'arriere, le reste reste a plat
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward);
        Vector3 wantedUp = Quaternion.AngleAxis(throttle * (14f + 6f * tier) * (boost ? 1.3f : 1f), flatRight) * Vector3.up;
        Vector3 error = Vector3.Cross(up, wantedUp);
        Vector3 ang = Body.angularVelocity;
        Vector3 tilt = error * 10f - Vector3.ProjectOnPlane(ang, up) * 3f;
        Body.AddTorque(tilt, ForceMode.Acceleration);

        // Lacet et freinage horizontal leger (le robot ne glisse pas indefiniment)
        float targetYaw = steer * turnRateDeg * Mathf.Deg2Rad;
        Body.AddTorque(up * (targetYaw - yawRate) * 4f, ForceMode.Acceleration);
        // Vitesse de pointe : celle des rotors, ou celle des propulseurs s'il y en a
        Vector3 horizontal = Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up);
        float limit = Mathf.Max(rotorSpeed * k, ThrusterLimit()) * (boost ? boostMultiplier : 1f);
        if (horizontal.magnitude > limit)
            Body.AddForce(-horizontal.normalized * (horizontal.magnitude - limit) * 2f, ForceMode.Acceleration);
        Body.AddForce(-horizontal * (0.25f / k), ForceMode.Acceleration);
    }

    // Vitesse de pointe permise par les propulseurs (0 sans propulseur) : 30 m/s
    // au niveau 1, + 30 % par niveau
    private float ThrusterLimit()
    {
        float limit = 0f;
        foreach (var u in units)
        {
            if (u.kind == LocomotionKind.Thruster && !u.disabled)
                limit = Mathf.Max(limit, 30f * TierFactor(u.tier));
        }
        return limit;
    }

    private void ApplyThrusters(Vector3 up, Vector3 forward)
    {
        var thrusters = units.FindAll(u => u.kind == LocomotionKind.Thruster && !u.disabled);
        if (thrusters.Count == 0)
            return;
        float speed = Body.linearVelocity.magnitude;
        float limit = ThrusterLimit() * (boost ? boostMultiplier : 1f);
        foreach (var t in thrusters)
        {
            Vector3 dir = transform.TransformDirection(t.axis);
            float fire = Mathf.Clamp01(Vector3.Dot(dir, forward) * throttle + Vector3.Dot(dir, up) * Mathf.Max(0f, lift));
            if (speed > limit)
                fire *= 0.2f;
            t.output = Mathf.MoveTowards(t.output, fire, Time.fixedDeltaTime * 4f);
            float accel = 9f * TierFactor(t.tier) * (boost ? boostMultiplier : 1f);
            Body.AddForceAtPosition(dir * (t.output * accel * Body.mass / thrusters.Count), transform.TransformPoint(t.center));
        }
    }

    private void ApplyRudders(Vector3 v, Vector3 up, Vector3 forward)
    {
        var rudders = units.FindAll(u => u.kind == LocomotionKind.Rudder && !u.disabled);
        if (rudders.Count == 0)
            return;
        float vf = Vector3.Dot(v, forward);
        float authority = Mathf.Clamp01(Mathf.Abs(vf) / 12f) * Mathf.Min(1f, rudders.Count * 0.5f);
        Body.AddTorque(up * (steer * 2.5f * authority * Mathf.Sign(vf == 0f ? 1f : vf)), ForceMode.Acceleration);
        // Les gouvernes freinent la derive laterale (le robot suit son nez)
        Vector3 lateral = Vector3.Project(v, transform.right);
        Body.AddForce(-lateral * 0.6f * authority, ForceMode.Acceleration);
        foreach (var r in rudders)
            r.output = steer;
    }

    // Retour sur les roues (touche R du pilote) : un metre plus haut, a plat
    public void Recover()
    {
        if (Body == null)
            return;
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 1e-4f)
            flatForward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
        Body.linearVelocity = Vector3.zero;
        Body.angularVelocity = Vector3.zero;
        Body.position += Vector3.up * (1f + RobotHeight * 0.5f);
        Body.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }

    // Rayon vers le sol en ignorant le robot lui-meme et les triggers
    private bool Cast(Vector3 origin, Vector3 dir, float length, out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(origin, dir, hits, length, ~0, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.collider == null || h.collider.attachedRigidbody == Body)
                continue;
            if (h.distance < bestDistance)
            {
                bestDistance = h.distance;
                best = h;
            }
        }
        return bestDistance < float.MaxValue;
    }

    // =========================================================
    // OUTILS DE GEOMETRIE
    // =========================================================

    // Bornes des renderers d'une branche, dans le repere de la racine du robot
    public bool TryGetLocalBounds(Transform branch, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var r in branch.GetComponentsInChildren<Renderer>())
        {
            Bounds lb = r.localBounds;
            Vector3 min = lb.min, max = lb.max;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                var p = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }
        return any;
    }

    public Vector3 LocalPoint(Transform t) => transform.InverseTransformPoint(t.position);

    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    public static bool IsHoverFan(string name) => HoverFanName.IsMatch(name);

    private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

    private static int SmallestAxis(Vector3 e) => e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;

    private static int LargestAxis(Vector3 e) => e.x >= e.y && e.x >= e.z ? 0 : e.y >= e.z ? 1 : 2;

    private static float LargestOther(Vector3 e, int axis)
    {
        float a = axis == 0 ? e.y : e.x;
        float b = axis == 2 ? e.y : e.z;
        return Mathf.Max(a, b);
    }
}
