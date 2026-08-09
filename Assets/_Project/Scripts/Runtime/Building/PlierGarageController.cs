using UnityEngine;

public class PlierGarageController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerGarageController playerController;


    // =========================================================
    // INERTIE DE DEPLACEMENT
    // =========================================================

    [Header("Movement Inertia")]

    [Tooltip("Décalage inverse quand le joueur va gauche/droite")]
    [SerializeField] private float horizontalOffset = 0.06f;

    [Tooltip("Décalage inverse quand le joueur avance/recule")]
    [SerializeField] private float forwardOffset = 0.04f;

    [Tooltip("Décalage inverse quand le joueur monte/descend")]
    [SerializeField] private float verticalOffset = 0.05f;


    // =========================================================
    // INERTIE DE LA SOURIS
    // =========================================================

    [Header("Camera Look Inertia")]

    [Tooltip("Translation inverse lors d'un mouvement gauche/droite de caméra")]
    [SerializeField] private float lookHorizontalAmount = 0.0025f;

    [Tooltip("Translation inverse lors d'un mouvement haut/bas de caméra")]
    [SerializeField] private float lookVerticalAmount = 0.002f;

    [Tooltip("Limite maximale du décalage provoqué par la souris")]
    [SerializeField] private float maxLookOffset = 0.075f;

    [Tooltip("Réactivité de la pince lorsque la caméra tourne")]
    [SerializeField] private float lookFollowSpeed = 18f;

    [Tooltip("Vitesse de retour après arrêt de la souris")]
    [SerializeField] private float lookReturnSpeed = 10f;


    // =========================================================
    // MINI FLOAT
    // =========================================================

    [Header("Floating Movement")]

    [SerializeField] private float bobAmount = 0.01f;

    [SerializeField] private float bobSpeed = 7f;

    [SerializeField] private float swayAmount = 0.005f;


    // =========================================================
    // ROTATION
    // =========================================================

    [Header("Movement Rotation")]

    [SerializeField] private float rollAmount = 1.5f;

    [SerializeField] private float pitchAmount = 1f;

    [SerializeField] private float rotationSmooth = 8f;


    // =========================================================
    // SMOOTH
    // =========================================================

    [Header("Smoothing")]

    [SerializeField] private float positionSmoothTime = 0.12f;


    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;

    private Vector3 positionVelocity;

    private Vector3 currentLookOffset;

    private float animationTimer;


    private void Awake()
    {
        startLocalPosition =
            transform.localPosition;

        startLocalRotation =
            transform.localRotation;


        if (playerController == null)
        {
            playerController =
                GetComponentInParent<PlayerGarageController>();
        }
    }


    private void LateUpdate()
    {
        if (playerController == null)
            return;


        UpdateLookInertia();

        ApplyPosition(
            playerController.MoveInput
        );

        ApplyRotation(
            playerController.MoveInput
        );
    }


    // =========================================================
    // CAMERA INERTIA
    // =========================================================

    private void UpdateLookInertia()
    {
        Vector2 look =
            playerController.LookDelta;


        // =====================================================
        // INVERSE DE LA CAMERA
        //
        // caméra droite  -> pince gauche
        // caméra gauche  -> pince droite
        //
        // caméra haut    -> pince bas
        // caméra bas     -> pince haut
        // =====================================================

        Vector3 targetLookOffset =
            new Vector3(
                -look.x * lookHorizontalAmount,
                -look.y * lookVerticalAmount,
                0f
            );


        // Evite les gros coups lorsque la souris
        // produit un delta important.
        targetLookOffset =
            Vector3.ClampMagnitude(
                targetLookOffset,
                maxLookOffset
            );


        bool cameraIsMoving =
            look.sqrMagnitude > 0.01f;


        float speed =
            cameraIsMoving
                ? lookFollowSpeed
                : lookReturnSpeed;


        currentLookOffset =
            Vector3.Lerp(
                currentLookOffset,
                targetLookOffset,
                1f - Mathf.Exp(
                    -speed * Time.deltaTime
                )
            );
    }


    // =========================================================
    // POSITION
    // =========================================================

    private void ApplyPosition(Vector3 input)
    {
        float effectMultiplier =
            playerController.CurrentEffectMultiplier;


        // =====================================================
        // DEPLACEMENT DU PLAYER
        // =========================================================

        Vector3 movementOffset =
            new Vector3(
                -input.x * horizontalOffset,
                -input.y * verticalOffset,
                -input.z * forwardOffset
            )
            * effectMultiplier;


        // =====================================================
        // MINI BALANCEMENT
        // =========================================================

        Vector3 floatingOffset =
            Vector3.zero;


        if (playerController.IsMovingHorizontally)
        {
            animationTimer +=
                Time.deltaTime *
                bobSpeed *
                effectMultiplier;


            floatingOffset.y =
                Mathf.Sin(animationTimer) *
                bobAmount *
                effectMultiplier;


            floatingOffset.x =
                Mathf.Cos(animationTimer * 0.5f) *
                swayAmount *
                effectMultiplier;
        }


        // =====================================================
        // POSITION CIBLE
        // =====================================================

        Vector3 targetPosition =
            startLocalPosition
            + movementOffset
            + floatingOffset
            + currentLookOffset;


        transform.localPosition =
            Vector3.SmoothDamp(
                transform.localPosition,
                targetPosition,
                ref positionVelocity,
                positionSmoothTime
            );
    }


    // =========================================================
    // ROTATION DE MOUVEMENT
    // =========================================================

    private void ApplyRotation(Vector3 input)
    {
        float effectMultiplier =
            playerController.CurrentEffectMultiplier;


        float pitch =
            input.z *
            pitchAmount *
            effectMultiplier;


        float roll =
            -input.x *
            rollAmount *
            effectMultiplier;


        if (playerController.IsMovingHorizontally)
        {
            roll +=
                Mathf.Sin(animationTimer) *
                0.3f *
                effectMultiplier;
        }


        Quaternion movementRotation =
            Quaternion.Euler(
                pitch,
                0f,
                roll
            );


        Quaternion targetRotation =
            startLocalRotation *
            movementRotation;


        transform.localRotation =
            Quaternion.Slerp(
                transform.localRotation,
                targetRotation,
                rotationSmooth * Time.deltaTime
            );
    }
}