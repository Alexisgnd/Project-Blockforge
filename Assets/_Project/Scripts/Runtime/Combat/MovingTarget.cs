using UnityEngine;

// =========================================================
// CIBLE MOBILE DU TERRAIN D'ESSAI (Map_TestGround)
// Pose sur la racine FIXE d'une cible mobile exportee de Blender
// (MoveLinear_*, MoveRotor_*, MoveOrbit_*, MovePopup_*). Reglage
// lu a l'import dans les proprietes FBX bf_* (voir
// MapTestGroundImporter), sinon deduit du nom par le setup.
// - Linear : <nom>_Carriage va de sa position a <nom>_End,
//   aller-retour si pingPong (sinon retour instantane), en m/s
// - Rotate : <nom>_Rotor tourne autour de la verticale, en deg/s
//   (le signe donne le sens) ; sert aux tourniquets et aux
//   « drones » en orbite
// - Popup  : <nom>_Hinge bascule de `angle` degres autour de
//   l'axe Hinge -> <nom>_HingeAxis (empties poses dans Blender :
//   aucune ambiguite de conversion d'axes) ; cycle leve/couche
//   automatique, et Knock() la couche puis la releve (a appeler
//   depuis le futur systeme de degats)
// La piece mobile recoit un Rigidbody kinematique deplace en
// FixedUpdate (MovePosition / MoveRotation) : un robot touche est
// pousse proprement. Ses MeshCollider passent en convexe (exige
// pour un collider qui bouge souvent). Chaque cible demarre avec
// un dephasage aleatoire pour qu'elles ne bougent pas en rythme.
// =========================================================

public class MovingTarget : MonoBehaviour
{
    public enum Mode { Linear, Rotate, Popup }

    [Header("Mouvement (rempli a l'import depuis bf_*)")]
    public Mode mode = Mode.Linear;
    [Tooltip("Linear : vitesse en m/s. Rotate : vitesse en deg/s (signe = sens de rotation).")]
    public float speed = 4f;
    [Tooltip("Linear : aller-retour entre le depart et _End (sinon retour instantane au depart)")]
    public bool pingPong = true;
    [Tooltip("Popup : angle de bascule en degres (negatif = bascule de l'autre cote)")]
    public float angle = 90f;

    [Header("Pop-up")]
    [Tooltip("Se couche et se releve toute seule en boucle ; sinon seulement apres Knock()")]
    public bool autoCycle = true;
    [Tooltip("Duree debout (min, max) en secondes")]
    public Vector2 upSeconds = new(3f, 6f);
    [Tooltip("Duree couchee (min, max) en secondes")]
    public Vector2 downSeconds = new(1.5f, 3f);
    [Tooltip("Duree de la bascule en secondes")]
    public float flipSeconds = 0.25f;

    public Transform Mover => mover;
    public bool IsDown => popupTarget > 0.5f;

    private Transform mover;
    private Rigidbody body;
    private Vector3 startPos;
    private Vector3 endPos;
    private Quaternion startRot;
    private Vector3 hingeAxis;
    private float phase;

    private float popupState;      // 0 = debout, 1 = couchee
    private float popupTarget;
    private float nextSwitch;

    private void Awake()
    {
        string moverName = mode switch
        {
            Mode.Linear => name + "_Carriage",
            Mode.Rotate => name + "_Rotor",
            _ => name + "_Hinge",
        };
        mover = FindDeep(transform, moverName);
        if (mover == null)
        {
            Debug.LogWarning($"[MovingTarget] {name} : piece mobile '{moverName}' introuvable, cible desactivee.");
            enabled = false;
            return;
        }

        startPos = mover.position;
        startRot = mover.rotation;

        switch (mode)
        {
            case Mode.Linear:
                var end = FindDeep(transform, name + "_End");
                endPos = end != null ? end.position : startPos;
                if (end == null)
                    Debug.LogWarning($"[MovingTarget] {name} : repere '{name}_End' introuvable, la cible ne bougera pas.");
                phase = Random.Range(0f, 2f * Vector3.Distance(startPos, endPos));
                break;
            case Mode.Rotate:
                phase = Random.Range(0f, 360f);
                break;
            case Mode.Popup:
                var axis = FindDeep(transform, name + "_HingeAxis");
                hingeAxis = axis != null ? (axis.position - mover.position).normalized : mover.right;
                if (hingeAxis.sqrMagnitude < 1e-6f)
                    hingeAxis = mover.right;
                nextSwitch = autoCycle ? Time.time + Random.Range(0.5f, upSeconds.y) : float.PositiveInfinity;
                break;
        }

        // Piece mobile = corps kinematique, colliders convexes
        if (!mover.TryGetComponent(out body))
            body = mover.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        foreach (var meshCollider in mover.GetComponentsInChildren<MeshCollider>(true))
            meshCollider.convex = true;
    }

    // Couche la cible (pop-up uniquement) ; elle se releve apres downSeconds
    public void Knock()
    {
        if (mode != Mode.Popup || !enabled || IsDown)
            return;
        popupTarget = 1f;
        nextSwitch = Time.time + flipSeconds + Random.Range(downSeconds.x, downSeconds.y);
    }

    private void FixedUpdate()
    {
        float t = Time.fixedTime;
        switch (mode)
        {
            case Mode.Linear:
            {
                float length = Vector3.Distance(startPos, endPos);
                if (length < 0.01f || Mathf.Approximately(speed, 0f))
                    return;
                float d = phase + t * Mathf.Abs(speed);
                float s = pingPong ? Mathf.PingPong(d, length) : Mathf.Repeat(d, length);
                MoveTo(Vector3.Lerp(startPos, endPos, s / length), startRot);
                break;
            }
            case Mode.Rotate:
                MoveTo(startPos, Quaternion.AngleAxis(phase + t * speed, Vector3.up) * startRot);
                break;
            case Mode.Popup:
                UpdatePopup();
                break;
        }
    }

    private void UpdatePopup()
    {
        if (Mathf.Approximately(popupState, popupTarget) && Time.time >= nextSwitch)
        {
            if (popupTarget > 0.5f)
            {
                // Couchee -> se releve, puis reste debout jusqu'au prochain cycle
                popupTarget = 0f;
                nextSwitch = autoCycle ? Time.time + flipSeconds + Random.Range(upSeconds.x, upSeconds.y) : float.PositiveInfinity;
            }
            else if (autoCycle)
            {
                popupTarget = 1f;
                nextSwitch = Time.time + flipSeconds + Random.Range(downSeconds.x, downSeconds.y);
            }
        }

        float step = flipSeconds > 0f ? Time.fixedDeltaTime / flipSeconds : 1f;
        popupState = Mathf.MoveTowards(popupState, popupTarget, step);
        float a = Mathf.SmoothStep(0f, angle, popupState);
        MoveTo(startPos, Quaternion.AngleAxis(a, hingeAxis) * startRot);
    }

    private void MoveTo(Vector3 position, Quaternion rotation)
    {
        if (body != null)
        {
            body.MovePosition(position);
            body.MoveRotation(rotation);
        }
        else
        {
            mover.SetPositionAndRotation(position, rotation);
        }
    }

    private static Transform FindDeep(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != root && t.name == childName)
                return t;
        }
        return null;
    }

    // Trajectoire visible a la selection dans l'editeur
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        switch (mode)
        {
            case Mode.Linear:
            {
                var a = FindDeep(transform, name + "_Carriage");
                var b = FindDeep(transform, name + "_End");
                if (a != null && b != null)
                    Gizmos.DrawLine(Application.isPlaying ? startPos : a.position, Application.isPlaying ? endPos : b.position);
                break;
            }
            case Mode.Rotate:
            {
                var rotor = FindDeep(transform, name + "_Rotor");
                if (rotor == null)
                    break;
                float radius = 0f;
                foreach (var r in rotor.GetComponentsInChildren<Renderer>())
                {
                    var c = r.bounds.center - rotor.position;
                    c.y = 0f;
                    radius = Mathf.Max(radius, c.magnitude);
                }
                Vector3 prev = rotor.position + Vector3.forward * radius;
                for (int i = 1; i <= 32; i++)
                {
                    float rad = i * Mathf.PI * 2f / 32f;
                    var p = rotor.position + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * radius;
                    Gizmos.DrawLine(prev, p);
                    prev = p;
                }
                break;
            }
            case Mode.Popup:
            {
                var hinge = FindDeep(transform, name + "_Hinge");
                var axis = FindDeep(transform, name + "_HingeAxis");
                if (hinge != null && axis != null)
                    Gizmos.DrawLine(hinge.position, axis.position);
                break;
            }
        }
    }
}
