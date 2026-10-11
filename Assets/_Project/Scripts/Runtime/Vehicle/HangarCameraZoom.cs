using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// ZOOM MOLETTE SUR LE ROBOT DU HANGAR (menu d'accueil)
// Sur l'ecran Accueil, la molette rapproche ou eloigne la
// camera du robot de la vitrine (HangarRobotShowcase).
// - Zoom = position sur un trajet entre la pose d'origine de
//   la camera (zoom 0, cadrage du menu inchange) et un point
//   proche du robot (zoom 1). En se rapprochant, la camera
//   se tourne progressivement vers le centre du robot pour
//   le garder cadre.
// - La distance minimale suit la taille du robot affiche :
//   un gros robot ne rentre jamais dans l'objectif.
// - Mouvement amorti (SmoothDamp) : chaque cran de molette
//   glisse au lieu de sauter.
// - Ignore la molette au-dessus d'une liste deroulante de
//   l'UI. Hors Accueil (Jouer, Construire), la camera revient
//   en douceur a sa pose d'origine.
// Setup : Blockforge > Setup MainMenu Robot Showcase.
// =========================================================

[RequireComponent(typeof(Camera))]
public class HangarCameraZoom : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public HangarRobotShowcase showcase;
    [Tooltip("Ecran Accueil : le zoom n'est actif que lorsqu'il est affiche")]
    public GameObject homeScreen;

    [Header("Zoom")]
    [Tooltip("Part du trajet parcourue par cran de molette")]
    public float stepPerNotch = 0.15f;
    [Tooltip("Distance minimale camera -> centre du robot, en multiples de sa taille d'affichage")]
    public float minDistanceFactor = 1.9f;
    [Tooltip("Distance minimale absolue (m)")]
    public float minDistance = 6f;
    [Tooltip("Au plus pres, la camera vise sous le centre du robot (x taille) : le robot remonte au-dessus des cartes des baies")]
    public float lookBelowFactor = 0.22f;
    [Tooltip("Temps d'amortissement du zoom (s)")]
    public float smoothTime = 0.18f;
    public bool invertScroll;

    private Vector3 originPosition;
    private Quaternion originRotation;
    private float target;     // 0 = pose d'origine, 1 = au plus pres
    private float zoom;
    private float velocity;

    private readonly List<RaycastResult> uiHits = new();

    private void Awake()
    {
        originPosition = transform.position;
        originRotation = transform.rotation;
        if (showcase == null)
            showcase = FindAnyObjectByType<HangarRobotShowcase>();
    }

    private void LateUpdate()
    {
        bool home = homeScreen == null || homeScreen.activeInHierarchy;
        if (!home)
            target = 0f;
        else
            HandleScroll();

        float smooth = BlockforgeTheme.ReduceMotion ? 0f : smoothTime;
        zoom = smooth > 0f ? Mathf.SmoothDamp(zoom, target, ref velocity, smooth) : target;
        if (Mathf.Abs(zoom - target) < 0.0005f)
        {
            zoom = target;
            velocity = 0f;
        }
        Apply();
    }

    private void HandleScroll()
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) < 0.01f || PointerOverScrollableUI(mouse.position.ReadValue()))
            return;

        // Plusieurs crans peuvent arriver dans la meme image (molette lancee) :
        // selon la plateforme un cran vaut 120 (Windows) ou 1 (valeur normalisee)
        float magnitude = Mathf.Abs(scrollY);
        float notches = Mathf.Clamp(magnitude >= 30f ? magnitude / 120f : magnitude, 1f, 6f);
        float step = Mathf.Sign(scrollY) * stepPerNotch * notches;
        if (invertScroll)
            step = -step;
        target = Mathf.Clamp01(target + step);
    }

    private void Apply()
    {
        if (showcase == null || zoom <= 0f)
        {
            transform.SetPositionAndRotation(originPosition, originRotation);
            return;
        }

        Vector3 focus = showcase.FocusPoint;
        Vector3 away = originPosition - focus;
        float originDistance = away.magnitude;
        if (originDistance < 0.01f)
            return;

        float nearest = Mathf.Min(originDistance, Mathf.Max(minDistance, showcase.DisplaySize * minDistanceFactor));
        Vector3 nearPosition = focus + away / originDistance * nearest;
        Vector3 lookAt = focus + Vector3.down * showcase.DisplaySize * lookBelowFactor;
        Quaternion nearRotation = Quaternion.LookRotation(lookAt - nearPosition, Vector3.up);

        // Ease-in-out sur le trajet : le cadrage tourne surtout en fin de zoom
        float t = zoom * zoom * (3f - 2f * zoom);
        transform.SetPositionAndRotation(Vector3.Lerp(originPosition, nearPosition, zoom),
                                         Quaternion.Slerp(originRotation, nearRotation, t));
    }

    // La molette appartient a une liste deroulante survolee (ex. liste de robots)
    private bool PointerOverScrollableUI(Vector2 screenPosition)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        uiHits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, uiHits);
        foreach (var hit in uiHits)
        {
            if (hit.gameObject != null && hit.gameObject.GetComponentInParent<ScrollRect>() != null)
                return true;
        }
        return false;
    }
}
