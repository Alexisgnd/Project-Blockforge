using System.Collections.Generic;
using UnityEngine;

// =========================================================
// VISEE DES ARMES (match) : les armes du slot selectionne
// pointent vers le reticule
// Pose sur le robot par RobotTestSpawner, lit RobotLoadoutState.
// - Point vise : rayon de la camera (centre de l'ecran, le
//   reticule), premier obstacle hors robot, sinon 300 m devant
// - Chaque arme (categorie Armes, sauf les lames de Tesla, au
//   corps a corps) devient une tourelle : ses pieces mobiles
//   passent sous un pivot cree au montage, qui tourne en lacet
//   autour du haut de l'arme et en tangage autour de sa droite,
//   dans le repere de l'arme (une arme posee sur un flanc vise
//   donc autour de la normale du flanc, comme dans Robocraft)
// - Pieces fixes : noms commencant par base, pedestal, mount,
//   azimuth, support, foot (socle, colonne, roulement) ; tout
//   le reste tourne (tete, berceau, canons...). Pivot : le noeud
//   "head" des lasers, sinon le centre de l'axe d'elevation
//   (elevation_axle), sinon le centre des pieces mobiles
// - Canon = +Z du modele ; tangage limite (-20 / +80 degres),
//   lacet libre ; rotation lissee (vitesse max en degres / s)
// - Les armes des autres slots reviennent au repos (lacet 0,
//   tangage 0)
// Les blocs miroir (echelle negative) marchent sans cas
// particulier : la cible est ramenee dans le repere local du
// pivot (InverseTransformPoint tient compte du miroir) et la
// rotation est calculee dans ce repere.
// =========================================================

[RequireComponent(typeof(RobotLoadoutState))]
public class WeaponAimController : MonoBehaviour
{
    [Header("Visee")]
    public float maxRange = 300f;
    public float yawSpeed = 420f;      // degres / s
    public float pitchSpeed = 320f;
    public float minPitch = -20f;      // vers le bas
    public float maxPitch = 80f;       // vers le haut

    private static readonly string[] FixedPrefixes = { "base", "pedestal", "mount", "azimuth", "support", "foot" };
    private const string MeleeFamily = "tesla";

    private class Turret
    {
        public PlacedBlockView view;
        public Transform pivot;
        public float yaw, pitch;       // degres, repere de l'arme
    }

    private readonly List<Turret> turrets = new();
    private RobotLoadoutState loadout;
    private Camera viewCamera;
    private int robotLayerMask;

    public Vector3 AimPoint { get; private set; }

    private void Awake()
    {
        loadout = GetComponent<RobotLoadoutState>();
        robotLayerMask = ~(1 << RobotTestSpawner.RobotLayer);
        foreach (var view in GetComponentsInChildren<PlacedBlockView>())
        {
            var turret = BuildTurret(view);
            if (turret != null)
                turrets.Add(turret);
        }
    }

    // ---------- Montage ----------

    private static bool IsAimed(BlockDefinition def)
    {
        if (def == null || def.category != BlockCategory.Armes)
            return false;
        string family = (def.family ?? "").ToLowerInvariant();
        return !family.Contains(MeleeFamily) && !def.name.ToLowerInvariant().Contains(MeleeFamily);
    }

    private static bool IsFixedName(string name)
    {
        string n = name.ToLowerInvariant();
        foreach (var prefix in FixedPrefixes)
        {
            if (n.StartsWith(prefix))
                return true;
        }
        return false;
    }

    private Turret BuildTurret(PlacedBlockView view)
    {
        if (!IsAimed(view.definition))
            return null;
        var visual = BlockColliders.Visual(view.transform);
        if (visual == null)
            return null;

        // Racine du modele : on descend la chaine des enfants uniques
        // (Visual_X -> instance du prefab -> noeud racine du FBX) jusqu'au
        // premier noeud qui porte plusieurs pieces
        var model = visual;
        while (model.childCount == 1 && model.GetComponent<Renderer>() == null)
            model = model.GetChild(0);
        if (model.childCount == 0)
            return null;

        var moving = new List<Transform>();
        var fixedParts = new List<Transform>();
        Transform head = null, axle = null;
        for (int i = 0; i < model.childCount; i++)
        {
            var child = model.GetChild(i);
            if (IsFixedName(child.name))
                fixedParts.Add(child);
            else
                moving.Add(child);
            string n = child.name.ToLowerInvariant();
            if (head == null && n == "head")
                head = child;
            if (axle == null && n.Contains("elevation_axle"))
                axle = child;
        }
        if (moving.Count == 0)
            return null;

        // Pivot (repere de la racine du modele) : tete, axe d'elevation, ou
        // centre des pieces mobiles ; axe de lacet au centre XZ du socle
        Vector3 pivotLocal;
        if (head != null)
        {
            pivotLocal = head.localPosition;
        }
        else
        {
            Vector3 baseCenter = Vector3.zero;
            bool hasBase = TryGetLocalBounds(model, fixedParts, out var baseBounds);
            if (hasBase)
                baseCenter = baseBounds.center;
            if (axle != null && TryGetLocalBounds(model, new[] { axle }, out var axleBounds))
                pivotLocal = new Vector3(hasBase ? baseCenter.x : axleBounds.center.x, axleBounds.center.y, hasBase ? baseCenter.z : axleBounds.center.z);
            else if (TryGetLocalBounds(model, moving, out var movingBounds))
                pivotLocal = new Vector3(hasBase ? baseCenter.x : movingBounds.center.x, movingBounds.center.y, hasBase ? baseCenter.z : movingBounds.center.z);
            else
                pivotLocal = Vector3.zero;
        }

        var pivot = new GameObject("Turret").transform;
        pivot.SetParent(model, false);
        pivot.localPosition = pivotLocal;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        pivot.gameObject.layer = model.gameObject.layer;
        foreach (var part in moving)
            part.SetParent(pivot, true);

        return new Turret { view = view, pivot = pivot };
    }

    // Bornes de pieces dans le repere de 'space' (sommets des renderers)
    private static bool TryGetLocalBounds(Transform space, IEnumerable<Transform> parts, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var part in parts)
        {
            foreach (var r in part.GetComponentsInChildren<Renderer>())
            {
                Bounds lb = r.localBounds;
                Vector3 min = lb.min, max = lb.max;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                    var p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
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
        }
        return any;
    }

    // ---------- Visee ----------

    private void LateUpdate()
    {
        if (turrets.Count == 0)
            return;
        if (viewCamera == null)
            viewCamera = Camera.main;

        // Point vise : le reticule (centre de l'ecran)
        if (viewCamera != null)
        {
            var origin = viewCamera.transform.position;
            var dir = viewCamera.transform.forward;
            AimPoint = Physics.Raycast(origin, dir, out var hit, maxRange, robotLayerMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : origin + dir * maxRange;
        }

        var selected = loadout != null ? loadout.Selected : null;
        float dt = Time.deltaTime;
        foreach (var t in turrets)
        {
            if (t.pivot == null)
                continue;
            bool active = selected != null && selected.blocks.Contains(t.view);
            float wantedYaw = 0f, wantedPitch = 0f;
            if (active)
            {
                // Cible dans le repere du parent du pivot (miroir compris)
                var parent = t.pivot.parent;
                Vector3 local = parent.InverseTransformPoint(AimPoint) - t.pivot.localPosition;
                if (local.sqrMagnitude > 1e-6f)
                {
                    wantedYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                    wantedPitch = Mathf.Clamp(Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg, minPitch, maxPitch);
                }
            }
            t.yaw = Mathf.MoveTowardsAngle(t.yaw, wantedYaw, yawSpeed * dt);
            t.pitch = Mathf.MoveTowards(t.pitch, wantedPitch, pitchSpeed * dt);
            // +Z = canon, +Y = haut de l'arme : lacet autour de Y, tangage (vers le haut) autour de -X
            t.pivot.localRotation = Quaternion.Euler(-t.pitch, t.yaw, 0f);
        }
    }
}
