using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGarageController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform capsule;

    [Header("Movement")]
    [SerializeField] private float horizontalSpeed = 5f;
    [SerializeField] private float verticalSpeed = 4f;

    [Header("Movement Smoothing")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 10f;

    [Header("Boost")]
    [SerializeField] private float boostMultiplier = 3f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float maxLookUp = 80f;
    [SerializeField] private float maxLookDown = 80f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Limites du vaisseau")]
    [Tooltip("Empeche le joueur de traverser les parois et de sortir du Mothership")]
    [SerializeField] private bool confineToShip = true;

    [Tooltip("Volume interieur du hangar (repere monde, vaisseau a l'origine) : " +
             "entre les nervures des parois (x +-18.85), du pont (y 0) " +
             "aux traverses du plafond (y 33.7), de la porte arriere (z -20.4) " +
             "a l'entree du portail avant (z 20.5)")]
    [SerializeField] private Bounds shipInterior = new Bounds(
        new Vector3(0f, 16.85f, 0.05f),
        new Vector3(37.7f, 33.7f, 40.9f));

    [Tooltip("Distance gardee entre la camera et les parois (m)")]
    [SerializeField] private float wallPadding = 0.8f;


    // =========================================================
    // DONNEES ACCESSIBLES PAR LA PINCE
    // =========================================================

    public Vector3 MoveInput { get; private set; }

    public Vector3 CurrentVelocity { get; private set; }

    public Vector2 LookDelta { get; private set; }

    public bool IsBoosting { get; private set; }


    public float CurrentMovementMultiplier =>
        IsBoosting ? boostMultiplier : 1f;


    public float CurrentEffectMultiplier =>
        IsBoosting ? boostMultiplier : 1f;


    public bool IsMoving =>
        CurrentVelocity.sqrMagnitude > 0.001f;


    public bool IsMovingHorizontally =>
        Mathf.Abs(MoveInput.x) > 0.01f ||
        Mathf.Abs(MoveInput.z) > 0.01f;


    private float cameraPitch;


    private void Start()
    {
        if (capsule == null)
        {
            capsule = transform.Find("Capsule");
        }

        if (lockCursorOnStart)
        {
            LockCursor();
        }
    }


    private void Update()
    {
        ReadInput();
        HandleMouseLook();
        HandleCursor();
        MovePlayer();
    }


    // =========================================================
    // INPUT
    // =========================================================

    private void ReadInput()
    {
        if (Keyboard.current == null)
        {
            MoveInput = Vector3.zero;
            IsBoosting = false;
            return;
        }


        float horizontal = 0f;
        float vertical = 0f;
        float forward = 0f;


        // =====================================================
        // AZERTY PHYSIQUE FORCE
        //
        // wKey = touche physique Z
        // aKey = touche physique Q
        // =====================================================

        // Z = avancer
        if (Keyboard.current.wKey.isPressed)
            forward += 1f;

        // S = reculer
        if (Keyboard.current.sKey.isPressed)
            forward -= 1f;

        // Q = gauche
        if (Keyboard.current.aKey.isPressed)
            horizontal -= 1f;

        // D = droite
        if (Keyboard.current.dKey.isPressed)
            horizontal += 1f;


        // Espace = monter
        if (Keyboard.current.spaceKey.isPressed)
            vertical += 1f;


        // Ctrl = descendre
        if (
            Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.rightCtrlKey.isPressed
        )
        {
            vertical -= 1f;
        }


        // Shift = boost x3
        IsBoosting =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;


        MoveInput = new Vector3(
            horizontal,
            vertical,
            forward
        );


        if (MoveInput.sqrMagnitude > 1f)
        {
            MoveInput = MoveInput.normalized;
        }
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void MovePlayer()
    {
        float multiplier =
            CurrentMovementMultiplier;


        // Tes axes visuels sont inversés,
        // donc on conserve les inversions établies précédemment.

        Vector3 forwardDirection =
            -transform.forward * MoveInput.z;


        Vector3 horizontalDirection =
            -transform.right * MoveInput.x;


        Vector3 verticalDirection =
            Vector3.up * MoveInput.y;


        Vector3 targetVelocity =
            (
                forwardDirection * horizontalSpeed +
                horizontalDirection * horizontalSpeed +
                verticalDirection * verticalSpeed
            )
            * multiplier;


        float smoothingSpeed =
            MoveInput.sqrMagnitude > 0.01f
                ? acceleration
                : deceleration;


        CurrentVelocity = Vector3.Lerp(
            CurrentVelocity,
            targetVelocity,
            smoothingSpeed * Time.deltaTime
        );


        transform.position +=
            CurrentVelocity * Time.deltaTime;

        ConfineToShip();
    }


    // =========================================================
    // LIMITES DU VAISSEAU
    //
    // Deplacement libre par transform (pas de physique) : des
    // colliders ne l'arreteraient pas. On ramene donc la position
    // dans le volume interieur apres chaque deplacement, et on
    // annule la vitesse qui pousse contre la paroi (pas d'effet
    // "colle au mur", la pince ne voit plus de mouvement).
    // =========================================================

    private void ConfineToShip()
    {
        if (!confineToShip)
            return;

        Vector3 pad = Vector3.one * wallPadding;
        Vector3 min = shipInterior.min + pad;
        Vector3 max = shipInterior.max - pad;

        Vector3 position = transform.position;
        Vector3 clamped = new Vector3(
            Mathf.Clamp(position.x, min.x, max.x),
            Mathf.Clamp(position.y, min.y, max.y),
            Mathf.Clamp(position.z, min.z, max.z)
        );

        if (clamped == position)
            return;

        Vector3 velocity = CurrentVelocity;
        if (clamped.x != position.x) velocity.x = 0f;
        if (clamped.y != position.y) velocity.y = 0f;
        if (clamped.z != position.z) velocity.z = 0f;
        CurrentVelocity = velocity;

        transform.position = clamped;
    }


    private void OnDrawGizmosSelected()
    {
        if (!confineToShip)
            return;

        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(shipInterior.center, shipInterior.size);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Vector3 inner = shipInterior.size - Vector3.one * (wallPadding * 2f);
        Gizmos.DrawWireCube(shipInterior.center, Vector3.Max(inner, Vector3.zero));
    }


    // =========================================================
    // MOUSE LOOK
    // =========================================================

    private void HandleMouseLook()
    {
        LookDelta = Vector2.zero;


        if (Mouse.current == null)
            return;


        if (Cursor.lockState != CursorLockMode.Locked)
            return;


        LookDelta =
            Mouse.current.delta.ReadValue();


        float mouseX =
            LookDelta.x * mouseSensitivity;


        float mouseY =
            LookDelta.y * mouseSensitivity;


        // =====================================================
        // GAUCHE / DROITE
        // =====================================================

        transform.Rotate(
            0f,
            mouseX,
            0f,
            Space.World
        );


        // =====================================================
        // HAUT / BAS
        //
        // IMPORTANT :
        // souris vers le haut = pitch négatif
        // =====================================================

        cameraPitch += mouseY;


        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookUp,
            maxLookDown
        );


        if (capsule != null)
        {
            capsule.localRotation =
                Quaternion.Euler(
                    cameraPitch,
                    0f,
                    0f
                );
        }
    }


    // =========================================================
    // CURSOR
    // =========================================================

    private void HandleCursor()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }


        if (
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked
        )
        {
            LockCursor();
        }
    }


    private void LockCursor()
    {
        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }


    private void UnlockCursor()
    {
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        LookDelta = Vector2.zero;
    }


    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            UnlockCursor();
        }
    }
}