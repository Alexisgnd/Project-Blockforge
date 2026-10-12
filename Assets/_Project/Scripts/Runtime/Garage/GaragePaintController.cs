using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// PEINTURE DES BLOCS AU PISTOLET (outil 2 du garage)
// Ajoute au demarrage par GarageBuildController (qui garde la
// grille, le blueprint et le miroir) ; actif seulement quand le
// pistolet est en main, le curseur verrouille et l'inventaire
// ferme.
// - Clic gauche maintenu : un jet de peinture part de la buse vers
//   le point vise ; le bloc touche change de couleur quand le jet
//   l'atteint (temps de vol PaintSprayFX.TravelTime), avec une
//   eclaboussure. Balayer en tenant le clic peint au passage.
// - Clic droit maintenu : jet de diluant, le bloc touche reprend ses
//   couleurs d'origine a l'arrivee du jet
// - Molette : change la teinte (PaintSprayColorWheel)
// - M : miroir, le jumeau du bloc est peint en meme temps
// La legende des commandes passe aux libelles du pistolet
// tant qu'il est en main.
// =========================================================

public class GaragePaintController : MonoBehaviour
{
    [HideInInspector] public GarageBuildController build;

    private const int SprayToolIndex = 1;
    private const float FreeJetDistance = 8f; // jet dans le vide : portee affichee

    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private PaintSprayColorWheel wheel;

    // Blocs vises par un jet en vol : peints a l'arrivee
    private struct Pending
    {
        public PlacedBlockView twin;
        public float arrival;
        public int color;
        public Vector3 point;
    }
    private readonly Dictionary<PlacedBlockView, Pending> pending = new();
    private readonly List<PlacedBlockView> arrived = new();

    // Historique : un trait reste ouvert tant que des jets sont en vol
    public int PendingCount => pending.Count;

    // Annuler / retablir pendant un trait : les jets en vol sont perdus
    public void CancelPending() => pending.Clear();

    // Legende des commandes (Ctrl_AJOUTER / SUPPRIMER / PIVOTER)
    private TMP_Text placeLabel, removeLabel, rotateLabel;
    private string placeText, removeText, rotateText;
    private bool labelsSearched, labelsPaint;

    private void OnDisable()
    {
        SetPaintLabels(false);
    }

    private void Update()
    {
        // Les jets deja partis arrivent meme si l'outil est range
        ResolvePending();

        if (!IsActive())
        {
            SetPaintLabels(false);
            return;
        }

        SetPaintLabels(true);
        build.PollMirrorKey();

        if (wheel == null)
            wheel = Object.FindAnyObjectByType<PaintSprayColorWheel>(FindObjectsInactive.Include);
        int color = wheel != null ? wheel.SelectedColorIndex : 1;

        var mouse = Mouse.current;
        if (mouse == null)
            return;
        int target = mouse.leftButton.isPressed ? color : mouse.rightButton.isPressed ? 0 : -1;
        if (target < 0)
            return;

        var ray = build.viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var view = Raycast(ray, out Vector3 point);
        var fx = SprayFX();
        if (view == null)
            point = AnyHit(ray, out var p) ? p : ray.GetPoint(FreeJetDistance);

        fx?.Fire(point, Time.deltaTime, target == 0);

        // Bloc touche : peint quand le jet l'atteint
        if (view != null && view.colorIndex != target && !pending.ContainsKey(view))
        {
            float travel = fx != null ? fx.TravelTime(point) : 0f;
            pending[view] = new Pending
            {
                twin = build.MirrorTwin(view),
                arrival = Time.time + travel,
                color = target,
                point = point,
            };
        }
    }

    private void ResolvePending()
    {
        if (pending.Count == 0)
            return;
        arrived.Clear();
        foreach (var kv in pending)
            if (kv.Key == null || Time.time >= kv.Value.arrival)
                arrived.Add(kv.Key);
        foreach (var view in arrived)
        {
            var p = pending[view];
            pending.Remove(view);
            if (view == null)
                continue;
            bool painted = build.PaintBlock(view, p.color);
            painted |= p.twin != null && build.PaintBlock(p.twin, p.color);
            if (painted)
            {
                var cam = build.viewCamera != null ? build.viewCamera.transform.position : p.point + Vector3.up;
                SprayFX()?.Splash(p.point, cam - p.point);
            }
        }
    }

    private bool AnyHit(Ray ray, out Vector3 point)
    {
        point = default;
        if (!Physics.Raycast(ray, out var hit, build.maxReach, ~0, QueryTriggerInteraction.Ignore))
            return false;
        // pas sur le pistolet lui-meme (colliders eventuels de la main)
        point = hit.point;
        return hit.distance > 0.5f;
    }

    // Effets du pistolet (gouttelettes, gerbe de bulles) ; null sans le modele v3
    private PaintSprayFX fx;

    private PaintSprayFX SprayFX()
    {
        if (fx == null)
            fx = Object.FindAnyObjectByType<PaintSprayFX>();
        return fx;
    }

    private bool IsActive()
    {
        if (build == null || build.viewCamera == null)
            return false;
        if (Cursor.lockState != CursorLockMode.Locked)
            return false;
        if (build.inventory != null && build.inventory.IsOpen)
            return false;
        if (build.toolbar != null)
            return build.toolbar.SelectedIndex == SprayToolIndex;
        return !build.enabled; // sans barre d'outils : le switch coupe la pose
    }

    // Bloc vise : les boites d'empreinte (colliders) sont des cases pleines,
    // plus grosses que le modele (une roue de 2 cases deborde devant le
    // chassis). Parmi les blocs dont la boite est traversee, on garde celui
    // dont une piece visible (bounds de ses renderers) est touchee la
    // premiere ; a defaut, la boite la plus proche. 'point' = impact.
    private PlacedBlockView Raycast(Ray ray, out Vector3 point)
    {
        point = default;
        int count = Physics.RaycastNonAlloc(ray, hitBuffer, build.maxReach, ~0, QueryTriggerInteraction.Ignore);
        PlacedBlockView nearestBox = null, nearestVisual = null;
        float boxDist = float.MaxValue, visualDist = float.MaxValue;
        Vector3 boxPoint = default;
        for (int i = 0; i < count; i++)
        {
            var candidate = hitBuffer[i].collider.GetComponentInParent<PlacedBlockView>();
            if (candidate == null)
                continue;
            if (hitBuffer[i].distance < boxDist)
            {
                boxDist = hitBuffer[i].distance;
                nearestBox = candidate;
                boxPoint = hitBuffer[i].point;
            }
            float d = VisualDistance(candidate, ray);
            if (d < visualDist)
            {
                visualDist = d;
                nearestVisual = candidate;
            }
        }
        if (nearestVisual != null)
        {
            point = ray.GetPoint(visualDist);
            return nearestVisual;
        }
        point = boxPoint;
        return nearestBox;
    }

    private static float VisualDistance(PlacedBlockView view, Ray ray)
    {
        var visual = BlockColliders.Visual(view.transform);
        if (visual == null)
            return float.MaxValue;
        float best = float.MaxValue;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
        {
            if (renderer.bounds.IntersectRay(ray, out float d) && d < best)
                best = d;
        }
        return best;
    }

    // =========================================================
    // LEGENDE DES COMMANDES
    // =========================================================

    private void SetPaintLabels(bool paint)
    {
        if (labelsPaint == paint)
            return;
        labelsPaint = paint;

        if (!labelsSearched)
        {
            labelsSearched = true;
            var panel = GameObject.Find("ControlsPanel");
            if (panel != null)
            {
                placeLabel = panel.transform.Find("Ctrl_AJOUTER")?.GetComponent<TMP_Text>();
                removeLabel = panel.transform.Find("Ctrl_SUPPRIMER")?.GetComponent<TMP_Text>();
                rotateLabel = panel.transform.Find("Ctrl_PIVOTER")?.GetComponent<TMP_Text>();
                placeText = placeLabel != null ? placeLabel.text : null;
                removeText = removeLabel != null ? removeLabel.text : null;
                rotateText = rotateLabel != null ? rotateLabel.text : null;
            }
        }

        if (placeLabel != null) placeLabel.text = paint ? "Paint part" : placeText;
        if (removeLabel != null) removeLabel.text = paint ? "Clear paint" : removeText;
        if (rotateLabel != null) rotateLabel.text = paint ? "Color" : rotateText;
    }
}
