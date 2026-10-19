using UnityEngine;

// =========================================================
// MASQUAGE D'UNE LIGNE DE STATS (panneaux places a la main)
// Les panneaux de stats generes par les setups (garage, accueil,
// cartes de l'ecran Build) placent leurs lignes en absolu, sans
// layout : enfants « <prefixe><i>_Label / _Value / _BarBg ».
// Hide() desactive la ligne i, remonte les suivantes d'un pas
// (ecart mesure entre deux lignes) et, si demande, raccourcit le
// panneau d'autant. Sert a cacher l'energie tant qu'elle n'est
// pas calculee.
// =========================================================

public static class StatRowHider
{
    private static readonly string[] Suffixes = { "_Label", "_Value", "_BarBg" };

    // Ligne reperee par son texte de valeur (« Row_3_Value », « Stat_4_Value »...)
    public static void HideRowOf(Component value, bool shrinkPanel)
    {
        if (value == null)
            return;
        var m = System.Text.RegularExpressions.Regex.Match(value.name, @"^(.*?)(\d+)_Value$");
        if (m.Success)
            Hide(value.transform.parent, m.Groups[1].Value, int.Parse(m.Groups[2].Value), shrinkPanel);
        else
            value.gameObject.SetActive(false);
    }

    public static void Hide(Transform panel, string prefix, int index, bool shrinkPanel)
    {
        if (panel == null)
            return;
        var label = panel.Find($"{prefix}{index}_Label") as RectTransform;
        if (label == null || !label.gameObject.activeSelf)
            return;   // deja masquee (ou panneau d'une autre forme)

        // Pas entre deux lignes, mesure sur les libelles
        float step = 0f;
        var next = panel.Find($"{prefix}{index + 1}_Label") as RectTransform;
        var prev = panel.Find($"{prefix}{index - 1}_Label") as RectTransform;
        if (next != null)
            step = label.anchoredPosition.y - next.anchoredPosition.y;
        else if (prev != null)
            step = prev.anchoredPosition.y - label.anchoredPosition.y;

        foreach (var suffix in Suffixes)
        {
            var part = panel.Find($"{prefix}{index}{suffix}");
            if (part != null)
                part.gameObject.SetActive(false);
        }

        if (Mathf.Abs(step) < 0.01f)
            return;
        for (int i = index + 1; ; i++)
        {
            bool any = false;
            foreach (var suffix in Suffixes)
            {
                if (panel.Find($"{prefix}{i}{suffix}") is RectTransform rt)
                {
                    rt.anchoredPosition += new Vector2(0f, step);
                    any = true;
                }
            }
            if (!any)
                break;
        }

        if (shrinkPanel && panel is RectTransform panelRect && Mathf.Approximately(panelRect.anchorMin.y, panelRect.anchorMax.y))
            panelRect.sizeDelta -= new Vector2(0f, Mathf.Abs(step));
    }
}
