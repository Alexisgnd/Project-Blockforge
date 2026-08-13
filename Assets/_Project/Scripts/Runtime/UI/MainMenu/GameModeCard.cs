using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// CARTE D'UN MODE DE JEU (widget purement visuel)
// =========================================================

public class GameModeCard : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Button button;
    public Image coverImage;
    public Image iconImage;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text footerText;
    public Button configureButton;

    private GameModeDefinition definition;
    private Action<GameModeDefinition> onOpen;

    public void Bind(GameModeDefinition def, Action<GameModeDefinition> openCallback)
    {
        definition = def;
        onOpen = openCallback;

        titleText.text = def.title;
        descriptionText.text = def.tagline;
        footerText.text = $"{def.teamsLabel}   |   {def.durationLabel}";

        if (def.coverImage != null)
            coverImage.sprite = def.coverImage;
        if (def.icon != null)
            iconImage.sprite = def.icon;

        // Mode personnalise : bouton CONFIGURER a la place du pied de carte
        footerText.gameObject.SetActive(!def.isCustom);
        configureButton.gameObject.SetActive(def.isCustom);

        button.onClick.RemoveListener(Open);
        button.onClick.AddListener(Open);
        configureButton.onClick.RemoveListener(Open);
        configureButton.onClick.AddListener(Open);
    }

    private void Open()
    {
        if (definition != null)
            onOpen?.Invoke(definition);
    }
}
