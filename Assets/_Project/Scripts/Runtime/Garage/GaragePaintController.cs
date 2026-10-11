using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// PEINTURE DES BLOCS AU PISTOLET (outil 2 du garage)
// Ajoute au demarrage par GarageBuildController (qui garde la
// grille, le blueprint et le miroir) ; actif seulement quand le
// pistolet est en main, le curseur verrouille et l'inventaire
// ferme.
// - Viseur sur un bloc : apercu de la teinte de la roue
// - Clic gauche (maintenu = balayage) : peint le bloc
// - Clic droit (maintenu = balayage) : rend ses couleurs d'origine
// - Molette : change la teinte (PaintSprayColorWheel)
// - M : miroir, le jumeau du bloc est peint en meme temps
// La legende des commandes passe aux libelles du pistolet
// tant qu'il est en main.
// =========================================================

public class GaragePaintController : MonoBehaviour
{
    [HideInInspector] public GarageBuildController build;

    private const int SprayToolIndex = 1;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private PaintSprayColorWheel wheel;

    private PlacedBlockView hover, hoverTwin;
    private int previewColor;
    private PlacedBlockView suppressed; // bloc qu'on vient de peindre : pas d'apercu tant qu'on reste dessus

    // Legende des commandes (Ctrl_AJOUTER / SUPPRIMER / PIVOTER)
    private TMP_Text placeLabel, removeLabel, rotateLabel;
    private string placeText, removeText, rotateText;
    private bool labelsSearched, labelsPaint;

    private void OnDisable()
    {
        ClearPreview();
        SetPaintLabels(false);
    }

    private void Update()
    {
        if (!IsActive())
        {
            ClearPreview();
            SetPaintLabels(false);
            return;
        }

        SetPaintLabels(true);
        build.PollMirrorKey();

        if (wheel == null)
            wheel = Object.FindAnyObjectByType<PaintSprayColorWheel>(FindObjectsInactive.Include);
        int color = wheel != null ? wheel.SelectedColorIndex : 1;

        var view = Raycast();
        var twin = build.MirrorTwin(view);
        if (view != suppressed)
            suppressed = null;

        var mouse = Mouse.current;
        if (view != null && mouse != null)
        {
            int target = -1;
            if (mouse.leftButton.isPressed)
                target = color;
            else if (mouse.rightButton.isPressed)
                target = 0;

            if (target >= 0)
            {
                ClearPreview();
                bool painted = build.PaintBlock(view, target);
                painted |= twin != null && build.PaintBlock(twin, target);
                if (painted)
                {
                    suppressed = view;
                    if (target > 0)
                        SprayFX()?.Spray(build.viewCamera.transform.forward);
                }
                return;
            }
        }

        // Apercu : le bloc vise (et son jumeau) prend la teinte de la roue
        if (view == null || view == suppressed)
        {
            ClearPreview();
            return;
        }
        if (view != hover || twin != hoverTwin || color != previewColor)
        {
            ClearPreview();
            hover = view;
            hoverTwin = twin;
            previewColor = color;
            Preview(hover, color);
            Preview(hoverTwin, color);
        }
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
    // premiere ; a defaut, la boite la plus proche.
    private PlacedBlockView Raycast()
    {
        var ray = build.viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int count = Physics.RaycastNonAlloc(ray, hitBuffer, build.maxReach, ~0, QueryTriggerInteraction.Ignore);
        PlacedBlockView nearestBox = null, nearestVisual = null;
        float boxDist = float.MaxValue, visualDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var candidate = hitBuffer[i].collider.GetComponentInParent<PlacedBlockView>();
            if (candidate == null)
                continue;
            if (hitBuffer[i].distance < boxDist)
            {
                boxDist = hitBuffer[i].distance;
                nearestBox = candidate;
            }
            float d = VisualDistance(candidate, ray);
            if (d < visualDist)
            {
                visualDist = d;
                nearestVisual = candidate;
            }
        }
        return nearestVisual != null ? nearestVisual : nearestBox;
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
    // APERCU
    // =========================================================

    private static void Preview(PlacedBlockView view, int color)
    {
        if (view != null && view.colorIndex != color)
            BlockPaint.Apply(BlockColliders.Visual(view.transform), color);
    }

    private static void Restore(PlacedBlockView view)
    {
        if (view != null)
            BlockPaint.Apply(BlockColliders.Visual(view.transform), view.colorIndex);
    }

    private void ClearPreview()
    {
        Restore(hover);
        Restore(hoverTwin);
        hover = null;
        hoverTwin = null;
        previewColor = 0;
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
