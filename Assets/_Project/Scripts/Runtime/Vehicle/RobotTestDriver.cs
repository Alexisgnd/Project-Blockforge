using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// =========================================================
// CONDUITE DU ROBOT DE TEST (type char)
// Z / S : avancer / reculer, Q / D : tourner sur place ou en
// roulant, Shift : boost (touches physiques W/S/A/D, donc
// Z/S/Q/D en AZERTY, comme PlayerGarageController ; les
// fleches marchent aussi).
// ECHAP libere le curseur ; un second ECHAP (curseur libre)
// revient au garage (RobotSession.ReturnSceneName) ; un clic
// gauche reverrouille le curseur.
// Corps rigide dynamique pilote en vitesse : il glisse le long
// des murs, monte les rampes et ne bascule pas (rotations X/Z
// gelees). Pas de Rigidbody cinematique : il traverserait les
// murs. Prototype : ni physique de roues, ni vitesse tiree des
// stats du robot.
// =========================================================

[RequireComponent(typeof(Rigidbody))]
public class RobotTestDriver : MonoBehaviour
{
    [Header("Conduite")]
    public float moveSpeed = 8f;
    public float boostMultiplier = 2f;
    public float turnSpeedDeg = 110f;
    [Tooltip("Variation de vitesse maximale par seconde (m/s^2)")]
    public float acceleration = 20f;

    [Header("Navigation")]
    [Tooltip("Scene garage rechargee si RobotSession.ReturnSceneName est vide (scene lancee directement)")]
    public string fallbackReturnScene = "Garage";
    public bool lockCursorOnStart = true;

    private Rigidbody rb;
    private float throttle;
    private float steer;
    private bool boost;
    private float currentSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Start()
    {
        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    private void Update()
    {
        throttle = 0f;
        steer = 0f;
        boost = false;

        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                SetCursorLocked(false);
            }
            else
            {
                ReturnToGarage();
                return;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            SetCursorLocked(true);

        // AZERTY physique : wKey = Z, aKey = Q
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
        boost = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
    }

    private void FixedUpdate()
    {
        float target = throttle * moveSpeed * (boost ? boostMultiplier : 1f);
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * Time.fixedDeltaTime);

        // Vitesse planaire imposee, composante verticale laissee a la gravite
        // et aux contacts (rampes) ; lacet impose en vitesse angulaire (pas de
        // MoveRotation : sur un corps dynamique ce serait une teleportation).
        var planar = transform.forward * currentSpeed;
        var velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
        rb.angularVelocity = new Vector3(0f, steer * turnSpeedDeg * Mathf.Deg2Rad, 0f);
    }

    public void ReturnToGarage()
    {
        string scene = string.IsNullOrEmpty(RobotSession.ReturnSceneName) ? fallbackReturnScene : RobotSession.ReturnSceneName;
        SetCursorLocked(false);
        SceneManager.LoadScene(scene);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SetCursorLocked(false);
    }
}
