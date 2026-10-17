using UnityEngine;
using UnityEngine.EventSystems;

// =========================================================
// RAISON AU SURVOL D'UNE ACTION INDISPONIBLE
// Regle du DS : un controle desactive porte opacity-disabled,
// un cadenas et une raison au survol. Le bouton garde ses
// evenements de pointeur meme non interactif : on affiche la
// raison dans le texte de l'ecran a l'entree, on l'efface a la
// sortie.
// =========================================================

public class HoverReason : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PlayMenuController owner;
    private string reason;

    public static void Attach(GameObject target, PlayMenuController owner, string reason)
    {
        if (target == null)
            return;
        var hover = target.GetComponent<HoverReason>();
        if (hover == null)
            hover = target.AddComponent<HoverReason>();
        hover.owner = owner;
        hover.reason = reason;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(reason))
            owner?.ShowReason(reason);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.ShowReason(null);
    }
}
