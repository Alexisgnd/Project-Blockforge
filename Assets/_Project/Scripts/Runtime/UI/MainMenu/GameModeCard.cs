using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// CARTE D'UN MODE DE JEU (widget purement visuel)
// Sans apercu : pictogramme du mode dans un puits, titre,
// accroche, puis deux lignes de chiffres (equipes, duree)
// alignees d'une carte a l'autre. Mode personnalise :
// bouton Configure a la place des chiffres.
// =========================================================

public class GameModeCard : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Button button;
    public Image iconImage;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text teamsText;
    public TMP_Text durationText;
    [Tooltip("Groupe des lignes de chiffres, masque pour le mode personnalise")]
    public GameObject statsGroup;
    public Button configureButton;

    // Pictogramme du mode (lu par la fiche detail)
    public Sprite IconSprite => iconImage != null ? iconImage.sprite : null;

    private GameModeDefinition definition;
    private Action<GameModeDefinition, GameModeCard> onOpen;

    public void Bind(GameModeDefinition def, Action<GameModeDefinition, GameModeCard> openCallback)
    {
        definition = def;
        onOpen = openCallback;

        if (titleText != null) titleText.text = def.title;
        if (descriptionText != null) descriptionText.text = def.tagline;
        if (teamsText != null) teamsText.text = def.teamsLabel;
        if (durationText != null) durationText.text = def.durationLabel;

        // Icone fournie par les donnees ; sinon celle posee par le design system
        if (def.icon != null && iconImage != null)
            iconImage.sprite = def.icon;

        if (statsGroup != null)
            statsGroup.SetActive(!def.isCustom);
        if (configureButton != null)
        {
            configureButton.gameObject.SetActive(def.isCustom);
            configureButton.onClick.RemoveListener(Open);
            configureButton.onClick.AddListener(Open);
        }

        if (button != null)
        {
            button.onClick.RemoveListener(Open);
            button.onClick.AddListener(Open);
        }
    }

    private void Open()
    {
        if (definition != null)
            onOpen?.Invoke(definition, this);
    }
}
