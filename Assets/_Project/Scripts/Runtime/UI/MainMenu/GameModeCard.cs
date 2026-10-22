using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// LIGNE D'UN MODE DE JEU (liste de l'ecran Play)
// Carte cliquable du design system (DSElement, role Card) :
// pictogramme du mode dans un puits a gauche, titre, ligne de
// chiffres, chevron a droite sur la ligne selectionnee.
// La selection (surface-selected + border-accent, pictogramme
// en accent) est portee par DSElement : la carte ne touche pas
// aux couleurs.
// Mode indisponible (GameModeDefinition.available = false) :
// regle du DS pour un controle desactive. Le bouton passe non
// interactif (DSElement applique opacity-disabled et coupe le
// survol), DisabledReason pose le cadenas et la raison au
// survol ; la carte ne peut pas etre selectionnee.
// =========================================================

public class GameModeCard : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Button button;
    public DSElement element;
    public Image iconWell;
    public Image iconImage;
    public TMP_Text titleText;
    public TMP_Text summaryText;
    public Image chevron;

    public GameModeDefinition Definition { get; private set; }

    // Faux : carte affichee mais ni selectionnable ni lancable
    public bool Available { get; private set; } = true;

    private Action<GameModeCard> onSelect;

    public void Bind(GameModeDefinition def, Action<GameModeCard> selectCallback)
    {
        Definition = def;
        onSelect = selectCallback;

        if (titleText != null) titleText.text = def.title;
        if (summaryText != null) summaryText.text = def.summary;
        if (iconImage != null)
        {
            var sprite = def.icon != null ? def.icon : DSAssets.Icon(def.iconName);
            if (sprite != null)
                iconImage.sprite = sprite;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(Select);
            button.onClick.AddListener(Select);
        }

        // Etat lu sur la donnee au runtime : pas besoin de relancer le setup
        SetAvailable(def.available, def.unavailableReason);
    }

    // Disponibilite du mode : opacite disabled + cadenas + raison au survol si indisponible
    public void SetAvailable(bool value, string reason)
    {
        Available = value;
        if (button != null)
            button.interactable = value;
        var why = GetComponent<DisabledReason>();
        if (!value)
        {
            if (why == null)
                why = gameObject.AddComponent<DisabledReason>();
            why.Set(string.IsNullOrEmpty(reason) ? "Coming in a future update." : reason);
            Selected = false;
        }
        else if (why != null)
        {
            why.Clear();
        }
    }

    public bool Selected
    {
        get => element != null && element.Selected;
        set
        {
            if (element != null)
                element.Selected = value;
            if (chevron != null)
                chevron.enabled = value;
            // Pictogramme : ink-secondary au repos, accent sur le mode actif
            if (iconImage != null)
                iconImage.color = value ? BlockforgeTheme.Accent : BlockforgeTheme.InkSecondary;
        }
    }

    private void Select()
    {
        if (Definition != null && Available)
            onSelect?.Invoke(this);
    }
}
