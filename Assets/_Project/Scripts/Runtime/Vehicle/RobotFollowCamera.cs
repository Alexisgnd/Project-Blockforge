using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// CAMERA DE POURSUITE ORBITALE (scene de test)
// Suit une cible (le robot) a distance fixe ; la souris
// orbite (lacet / tangage) tant que le curseur est verrouille ;
// apres un moment sans mouvement de souris, la camera se
// replace toute seule derriere le robot.
// Pas d'evitement des murs pour l'instant.
// =========================================================

public class RobotFollowCamera : MonoBehaviour
{
    public Transform target;

    [Header("Orbite")]
    public float distance = 9f;
    [Tooltip("Hauteur du point vise au-dessus du pivot du robot")]
    public float height = 1.5f;
    public float mouseSensitivity = 0.12f;
    public float minPitch = -10f;
    public float maxPitch = 70f;
    public float startPitch = 20f;

    [Header("Suivi")]
    [Tooltip("Vitesse du lissage exponentiel de la position")]
    public float followSmoothing = 12f;
    [Tooltip("Se replace derriere le robot apres autoAlignDelay secondes sans souris")]
    public bool autoAlign = true;
    public float autoAlignDelay = 1.5f;
    public float autoAlignSpeed = 3f;

    private float yaw;
    private float pitch;
    private float idleTime;

    // Initialise l'orbite derriere la cible et y teleporte la camera
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        pitch = startPitch;
        idleTime = autoAlignDelay;
        if (target == null)
            return;

        yaw = target.eulerAngles.y;
        var focus = Focus();
        transform.position = DesiredPosition(focus);
        transform.LookAt(focus);
    }

    private Vector3 Focus() => target.position + Vector3.up * height;

    private Vector3 DesiredPosition(Vector3 focus)
    {
        return focus + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        var mouse = Mouse.current;
        Vector2 delta = mouse != null && Cursor.lockState == CursorLockMode.Locked
            ? mouse.delta.ReadValue()
            : Vector2.zero;

        if (delta.sqrMagnitude > 0.01f)
        {
            yaw += delta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch + delta.y * mouseSensitivity, minPitch, maxPitch);
            idleTime = 0f;
        }
        else if (autoAlign)
        {
            idleTime += Time.deltaTime;
            if (idleTime >= autoAlignDelay)
            {
                float k = 1f - Mathf.Exp(-autoAlignSpeed * Time.deltaTime);
                yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, k);
                pitch = Mathf.Lerp(pitch, startPitch, k * 0.5f);
            }
        }

        var focus = Focus();
        float follow = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, DesiredPosition(focus), follow);
        transform.LookAt(focus);
    }
}
