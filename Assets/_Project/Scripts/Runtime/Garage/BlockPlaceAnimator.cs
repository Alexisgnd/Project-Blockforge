using UnityEngine;

// =========================================================
// ANIMATION DE POSE / RETRAIT D'UN BLOC DANS LE GARAGE
// N'anime que le VISUEL (enfant de la racine Block_x_y_z) :
// la racine, son collider et l'occupation de la grille sont
// a leur place definitive des la pose, la logique ne change pas.
// - Pose : le bloc tombe d'une demi-case en grossissant
//   (60 % -> 106 % -> 100 %), puis un petit tassement a
//   l'impact (ecrase en Y, elargi en XZ) : ~0,28 s.
// - Retrait : le visuel est detache puis retreci vers son
//   centre en 0,14 s avant destruction.
// Temps non scale ; "reduire les animations" (BlockforgeTheme)
// rend la pose et le retrait instantanes.
// =========================================================

public class BlockPlaceAnimator : MonoBehaviour
{
    private const float PlaceDuration = 0.28f;
    private const float RemoveDuration = 0.14f;
    private const float DropCells = 0.5f;

    private enum Mode { Place, Remove }

    private Mode mode;
    private float t;
    private float duration;
    private float drop;
    private Vector3 baseLocalPos;
    private Vector3 baseLocalScale;

    // Pose : cellSize = taille d'une case en metres (hauteur de chute)
    public static void Place(Transform visual, float cellSize)
    {
        if (visual == null || BlockforgeTheme.ReduceMotion)
            return;
        var anim = visual.gameObject.AddComponent<BlockPlaceAnimator>();
        anim.mode = Mode.Place;
        anim.duration = PlaceDuration;
        anim.drop = DropCells * cellSize;
        anim.baseLocalPos = visual.localPosition;
        anim.baseLocalScale = visual.localScale;
        anim.Apply(0f);
    }

    // Retrait : detache le visuel de sa racine (detruite tout de suite par l'appelant)
    public static void Remove(Transform visual, Transform keepUnder)
    {
        if (visual == null)
            return;
        if (BlockforgeTheme.ReduceMotion)
        {
            Destroy(visual.gameObject);
            return;
        }
        visual.SetParent(keepUnder, true);
        var existing = visual.GetComponent<BlockPlaceAnimator>();
        if (existing != null)
            Destroy(existing);
        var anim = visual.gameObject.AddComponent<BlockPlaceAnimator>();
        anim.mode = Mode.Remove;
        anim.duration = RemoveDuration;
        anim.baseLocalPos = visual.localPosition;
        anim.baseLocalScale = visual.localScale;
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime / duration;
        if (t >= 1f)
        {
            if (mode == Mode.Remove)
            {
                Destroy(gameObject);
                return;
            }
            transform.localPosition = baseLocalPos;
            transform.localScale = baseLocalScale;
            Destroy(this);
            return;
        }
        Apply(t);
    }

    private void Apply(float k)
    {
        if (mode == Mode.Remove)
        {
            // Retrecit vers le centre, ease-in
            float s = 1f - k * k;
            transform.localScale = baseLocalScale * s;
            transform.localPosition = baseLocalPos;
            return;
        }

        // 0 -> 0,7 : chute + grossissement avec leger depassement (ease-out-back)
        // 0,7 -> 1 : tassement a l'impact puis retour
        const float split = 0.7f;
        Vector3 scale;
        float height;
        if (k < split)
        {
            float u = k / split;
            float back = EaseOutBack(u);
            height = (1f - BlockforgeTheme.EaseOut(u)) * drop;
            scale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, back);
        }
        else
        {
            float u = (k - split) / (1f - split);
            float squash = Mathf.Sin(u * Mathf.PI) * 0.06f;
            height = 0f;
            scale = new Vector3(1f + squash, 1f - squash, 1f + squash);
        }

        transform.localScale = Vector3.Scale(baseLocalScale, scale);
        // La chute se fait dans le repere de la racine (tournee) : on la passe en monde
        transform.localPosition = baseLocalPos;
        if (transform.parent != null)
            transform.position += Vector3.up * height;
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
