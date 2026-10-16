using UnityEngine;

// =========================================================
// IA DU ROBOT ENNEMI DE LA ZONE DE COMBAT (CombatArena)
// Pilote RobotLocomotion (gaz / direction), WeaponAimController
// (point vise manuel) et WeaponFireController (tir manuel) :
// - Joueur hors de la zone (ou mort) : retourne au centre de la
//   zone et attend, armes au repos
// - Joueur dans la zone : vise son centre (avec une petite
//   erreur renouvelee regulierement), tire par rafales, garde
//   une distance de combat (avance s'il est loin, recule s'il
//   est trop pres, sinon tourne autour)
// - Ne sort jamais de la zone : des qu'il s'en approche, il
//   revient vers le centre
// Pas de navigation : le terrain de la zone est degage.
// =========================================================

[RequireComponent(typeof(RobotLocomotion), typeof(WeaponAimController), typeof(WeaponFireController))]
public class EnemyRobotAI : MonoBehaviour
{
    public CombatArena arena;
    [Header("Combat")]
    public float preferredDistance = 14f;
    public float burstSeconds = 0.9f;
    public float pauseSeconds = 0.7f;
    public float aimError = 0.7f;
    [Header("Conduite")]
    public float steerAngle = 35f;

    private RobotLocomotion loco;
    private WeaponAimController aim;
    private WeaponFireController fire;
    private RobotHealth health;
    private float burstTimer;
    private bool bursting;
    private Vector3 aimOffset;
    private float nextAimError;
    private float circleSign = 1f;
    private float nextCircleFlip;

    private void Awake()
    {
        loco = GetComponent<RobotLocomotion>();
        aim = GetComponent<WeaponAimController>();
        fire = GetComponent<WeaponFireController>();
        health = GetComponent<RobotHealth>();
    }

    private void Update()
    {
        if (arena == null || loco == null)
            return;
        if (health != null && health.IsDead)
        {
            Stop();
            return;
        }

        var player = arena.Player;
        var playerHealth = player != null ? player.GetComponent<RobotHealth>() : null;
        bool engaged = player != null && (playerHealth == null || !playerHealth.IsDead) && arena.Contains(player.transform.position);
        Vector3 me = transform.position;

        if (!engaged)
        {
            fire.FireHeld = false;
            bursting = false;
            aim.ManualAimPoint = me + transform.forward * 30f;
            Vector3 toCenter = arena.center - me;
            toCenter.y = 0f;
            if (toCenter.magnitude > 3f)
                DriveTowards(arena.center, 0.7f);
            else
                Stop();
            return;
        }

        // Visee : centre du joueur, erreur renouvelee
        Vector3 target = playerHealth != null ? player.transform.TransformPoint(playerHealth.Center()) : player.transform.position + Vector3.up * 0.8f;
        if (Time.time >= nextAimError)
        {
            aimOffset = Random.insideUnitSphere * aimError;
            nextAimError = Time.time + Random.Range(0.25f, 0.5f);
        }
        aim.ManualAimPoint = target + aimOffset;

        // Rafales
        burstTimer -= Time.deltaTime;
        if (burstTimer <= 0f)
        {
            bursting = !bursting;
            burstTimer = bursting ? burstSeconds : pauseSeconds;
        }
        fire.FireHeld = bursting;

        // Deplacement : distance de combat, sans sortir de la zone
        Vector3 toPlayer = player.transform.position - me;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        Vector3 ahead = me + transform.forward * 5f;
        if (!arena.Contains(ahead))
        {
            DriveTowards(arena.center, 0.8f);
            return;
        }
        if (dist > preferredDistance + 5f)
        {
            DriveTowards(player.transform.position, 1f);
        }
        else if (dist < preferredDistance - 5f)
        {
            // Recule en gardant le nez vers le joueur
            float angle = Vector3.SignedAngle(transform.forward, toPlayer.normalized, Vector3.up);
            loco.throttle = -0.7f;
            loco.steer = Mathf.Clamp(angle / steerAngle, -1f, 1f);
            loco.boost = false;
        }
        else
        {
            // Tourne autour du joueur
            if (Time.time >= nextCircleFlip)
            {
                circleSign = Random.value < 0.5f ? -1f : 1f;
                nextCircleFlip = Time.time + Random.Range(2f, 4f);
            }
            Vector3 tangent = Vector3.Cross(Vector3.up, toPlayer.normalized) * circleSign;
            DriveTowards(me + tangent * 8f + toPlayer.normalized * 2f, 0.6f);
        }
    }

    private void DriveTowards(Vector3 point, float speed)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            Stop();
            return;
        }
        float angle = Vector3.SignedAngle(transform.forward, dir.normalized, Vector3.up);
        loco.steer = Mathf.Clamp(angle / steerAngle, -1f, 1f);
        loco.throttle = Mathf.Abs(angle) < 100f ? speed : 0.25f;
        loco.lift = 0f;
        loco.boost = false;
    }

    private void Stop()
    {
        loco.throttle = 0f;
        loco.steer = 0f;
        loco.lift = 0f;
        loco.boost = false;
        if (fire != null)
            fire.FireHeld = false;
    }

    private void OnDisable()
    {
        if (loco != null)
            Stop();
    }
}
