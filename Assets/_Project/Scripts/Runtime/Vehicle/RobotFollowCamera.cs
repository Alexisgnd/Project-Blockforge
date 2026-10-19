using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// CAMERA DE MATCH (facon Robocraft)
// Camera "par-dessus l'epaule" du robot : l'oeil est juste
// au-dessus et derriere le robot, tres pres, et regarde dans
// la direction visee (le reticule au centre de l'ecran est le
// point vise). Le robot n'est pas cadre en entier : seul son
// dessus et son arriere occupent le bas de l'ecran, comme
// dans Robocraft.
// - Cadrage calcule sur la taille du robot (SetTarget) : un
//   gros robot recule et rehausse un peu la camera
// - Souris (curseur verrouille) : lacet / tangage autour d'un
//   pivot place au-dessus du robot. Souris vers le haut =
//   viser plus haut (invertY pour l'inverse)
// - Le pivot est lisse (pas la rotation : la visee reste
//   seche) ; une sphere lancee du pivot vers l'oeil empeche
//   la camera de traverser le sol et les murs
// - Apres un moment sans souris, se replace derriere le robot
//   (autoAlign)
// - Secousse (Shake) : une explosion proche (plasma) fait
//   trembler l'oeil quelques dixiemes de seconde, sans toucher a
//   la rotation (la visee reste exacte)
// =========================================================

public class RobotFollowCamera : MonoBehaviour
{
    public Transform target;

    // ---------- Secousse ----------
    private static readonly System.Collections.Generic.List<RobotFollowCamera> shakeable = new();
    private float trauma;

    private void OnEnable() => shakeable.Add(this);
    private void OnDisable() => shakeable.Remove(this);

    // Secousse attenuee par la distance (rien au-dela de 'range' metres)
    public static void Shake(Vector3 at, float strength, float range)
    {
        foreach (var cam in shakeable)
        {
            float k = Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, at) / Mathf.Max(0.1f, range));
            cam.trauma = Mathf.Min(1f, cam.trauma + strength * k);
        }
    }

    private Vector3 ShakeOffset()
    {
        if (trauma <= 0.001f)
            return Vector3.zero;
        float t = Time.time * 28f;
        float amp = trauma * trauma * 0.35f;
        return new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, Mathf.PerlinNoise(t, 3.3f) - 0.5f) * (2f * amp);
    }

    [Header("Cadrage")]
    [Tooltip("Hauteur de l'oeil au-dessus du haut du robot (m)")]
    public float eyeHeight = 1.1f;
    [Tooltip("Recul de l'oeil derriere l'arriere du robot (m)")]
    public float eyeBack = 3.2f;
    [Tooltip("Part de la taille du robot ajoutee a la hauteur et au recul : un gros robot reste lisible")]
    public float sizeScale = 0.12f;

    [Header("Visee")]
    public float mouseSensitivity = 0.12f;
    [Tooltip("Inverse l'axe vertical (souris vers le haut = viser plus bas)")]
    public bool invertY = false;
    [Tooltip("Angle max vers le haut (degres)")]
    public float maxLookUp = 35f;
    [Tooltip("Angle max vers le bas (degres)")]
    public float maxLookDown = 55f;
    [Tooltip("Tangage de repos (degres, positif = vers le bas) : l'horizon un peu au-dessus du centre")]
    public float restPitch = 6f;

    [Header("Suivi")]
    [Tooltip("Vitesse du lissage exponentiel du pivot")]
    public float followSmoothing = 18f;
    [Tooltip("Se replace derriere le robot apres autoAlignDelay secondes sans souris")]
    public bool autoAlign = true;
    public float autoAlignDelay = 1.5f;
    public float autoAlignSpeed = 3f;

    [Header("Collisions")]
    public float collisionRadius = 0.3f;

    [Header("Zoom (clic droit maintenu)")]
    [Tooltip("Champ de vision en zoom (le champ normal est celui de la camera)")]
    public float zoomFov = 30f;
    public float zoomSpeed = 10f;
    [Tooltip("Sensibilite souris en zoom, en part de la sensibilite normale")]
    public float zoomSensitivity = 0.5f;
    public bool Zooming { get; private set; }
    private Camera cam;
    private float baseFov = 60f;

    private float yaw;
    private float pitch;
    private float idleTime;

    // Cadrage du robot, en repere local de la cible
    private Vector3 localCenter;
    private float halfHeight = 1f;
    private float radius = 2f;
    private float size = 4f;
    private Vector3 smoothedPivot;

    private int collisionMask;

    private void Awake()
    {
        collisionMask = ~(1 << RobotTestSpawner.RobotLayer);
    }

    // Initialise l'orbite derriere la cible et y teleporte la camera
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        pitch = restPitch;
        idleTime = autoAlignDelay;
        if (target == null)
            return;

        MeasureTarget();
        yaw = target.eulerAngles.y;
        smoothedPivot = Pivot();
        Place(smoothedPivot);
    }

    // Boite du robot (rendus) exprimee dans son repere local
    private void MeasureTarget()
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        bool any = false;
        var local = new Bounds();
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var p = target.InverseTransformPoint(corner);
                if (!any)
                {
                    local = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    local.Encapsulate(p);
                }
            }
        }

        if (!any)
            local = new Bounds(Vector3.up, new Vector3(3f, 2f, 4f));

        localCenter = local.center;
        halfHeight = local.extents.y;
        radius = Mathf.Max(local.extents.x, local.extents.z);
        size = local.size.magnitude;
    }

    // Point au-dessus du robot autour duquel tourne la camera
    private Vector3 Pivot()
    {
        return target.TransformPoint(localCenter) + Vector3.up * (halfHeight + eyeHeight + size * sizeScale * 0.5f);
    }

    private void Place(Vector3 pivot)
    {
        var rotation = Quaternion.Euler(pitch, yaw, 0f);
        float distance = radius + eyeBack + size * sizeScale;
        var back = rotation * Vector3.back;

        // Le sol ou un mur entre le pivot et l'oeil : on rapproche l'oeil
        if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0.2f, hit.distance - 0.05f);

        transform.SetPositionAndRotation(pivot + back * distance + ShakeOffset(), rotation);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        var mouse = Mouse.current;
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        Vector2 delta = mouse != null && locked ? mouse.delta.ReadValue() : Vector2.zero;

        // Zoom : clic droit maintenu, champ de vision resserre et souris adoucie
        Zooming = mouse != null && locked && mouse.rightButton.isPressed && Time.timeScale > 0f;
        if (cam == null)
        {
            cam = GetComponent<Camera>();
            if (cam != null)
                baseFov = cam.fieldOfView;
        }
        if (cam != null)
        {
            float wantedFov = Zooming ? zoomFov : baseFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, wantedFov, 1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime));
        }
        float sensitivity = mouseSensitivity * (Zooming ? zoomSensitivity : 1f);

        if (delta.sqrMagnitude > 0.01f)
        {
            yaw += delta.x * sensitivity;
            // Tangage positif = vers le bas : souris vers le haut -> viser plus haut
            float dy = invertY ? delta.y : -delta.y;
            pitch = Mathf.Clamp(pitch + dy * sensitivity, -maxLookUp, maxLookDown);
            idleTime = 0f;
        }
        else if (autoAlign)
        {
            idleTime += Time.deltaTime;
            if (idleTime >= autoAlignDelay)
            {
                float k = 1f - Mathf.Exp(-autoAlignSpeed * Time.deltaTime);
                yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, k);
                pitch = Mathf.Lerp(pitch, restPitch, k * 0.5f);
            }
        }

        trauma = Mathf.Max(0f, trauma - 1.8f * Time.deltaTime);
        float follow = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        smoothedPivot = Vector3.Lerp(smoothedPivot, Pivot(), follow);
        Place(smoothedPivot);
    }
}
