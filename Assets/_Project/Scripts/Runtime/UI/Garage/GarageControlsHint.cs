using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// AIDE DES COMMANDES DU GARAGE (design system : Garage)
// "command help collapsible and at opacity-ghost when idle" :
// - au repos le panneau est a opacity-ghost (0.7) ;
// - H le replie (seul l'en-tete "Controls  H" reste) ou le
//   deplie ; il repasse a 100 % pendant 1,5 s apres un appui.
// L'etat replie est memorise (PlayerPrefs).
// =========================================================

[RequireComponent(typeof(CanvasGroup))]
public class GarageControlsHint : MonoBehaviour
{
    private const string CollapsedKey = "Blockforge.Garage.ControlsCollapsed";

    [Tooltip("En-tete toujours visible (libelle Controls + touche H)")]
    public RectTransform header;
    [Tooltip("Lignes masquees quand le panneau est replie")]
    public GameObject[] rows = new GameObject[0];
    public float expandedHeight = 480f;
    public float collapsedHeight = 54f;

    private CanvasGroup group;
    private RectTransform rect;
    private bool collapsed;
    private float wakeUntil;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rect = (RectTransform)transform;
        group.interactable = false;
        group.blocksRaycasts = false;
        collapsed = PlayerPrefs.GetInt(CollapsedKey, 0) == 1;
        ApplyCollapsed();
        group.alpha = BlockforgeTheme.OpacityGhost;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.hKey.wasPressedThisFrame)
        {
            collapsed = !collapsed;
            PlayerPrefs.SetInt(CollapsedKey, collapsed ? 1 : 0);
            ApplyCollapsed();
            wakeUntil = Time.unscaledTime + 1.5f;
        }

        float target = Time.unscaledTime < wakeUntil ? 1f : BlockforgeTheme.OpacityGhost;
        float duration = BlockforgeTheme.Duration(BlockforgeTheme.MotionPanel);
        group.alpha = duration <= 0f ? target
            : Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / duration);
    }

    private void ApplyCollapsed()
    {
        foreach (var row in rows)
            if (row != null) row.SetActive(!collapsed);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, collapsed ? collapsedHeight : expandedHeight);
    }
}
