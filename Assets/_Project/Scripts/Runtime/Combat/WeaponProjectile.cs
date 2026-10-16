using UnityEngine;

// =========================================================
// PROJECTILE D'ARME (bolt laser, boule de plasma)
// Cree par WeaponFireController sur un visuel lumineux (shader
// hologramme, couleur pleine) avec une trainee :
// - Laser  : capsule etiree alignee sur la vitesse, trainee
//            tres courte, trajectoire droite (gravite nulle)
// - Plasma : sphere pulsante, trainee plus longue, gravite
//            reduite (trajectoire en cloche, comme Robocraft)
// A chaque pas, un rayon entre l'ancienne et la nouvelle
// position detecte l'impact (le robot tireur est ignore) ;
// l'impact est remis au tireur
// (ApplyHit : degats, zone eventuelle, cibles couchees, eclat).
// Detruit a l'impact ou apres maxSeconds.
// =========================================================

public class WeaponProjectile : MonoBehaviour
{
    public float maxSeconds = 5f;

    private WeaponFireController owner;
    private Rigidbody ownerBody;
    private WeaponFireController.Stats stats;
    private Vector3 velocity;
    private float dieAt;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private bool pulse, alignToVelocity;
    private Vector3 baseScale;

    public void Launch(WeaponFireController shooter, Rigidbody shooterBody, Vector3 initialVelocity, WeaponFireController.Stats weaponStats,
                       Material material, float trailTime, float trailWidth, bool pulse, bool alignToVelocity)
    {
        owner = shooter;
        ownerBody = shooterBody;
        velocity = initialVelocity;
        stats = weaponStats;
        this.pulse = pulse;
        this.alignToVelocity = alignToVelocity;
        baseScale = transform.localScale;
        dieAt = Time.time + maxSeconds;

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.time = trailTime;
        trail.startWidth = trailWidth;
        trail.endWidth = 0.02f;
        trail.minVertexDistance = 0.08f;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (stats.gravity > 0f)
            velocity += Physics.gravity * (stats.gravity * dt);
        Vector3 from = transform.position;
        Vector3 to = from + velocity * dt;
        Vector3 step = to - from;
        float length = step.magnitude;
        if (length > 1e-5f && WeaponAimController.RaycastIgnoring(ownerBody, hits, from, step / length, length + 0.3f, out var hit))
        {
            if (owner != null)
                owner.ApplyHit(hit.collider, hit.point, hit.normal, stats, Color.white, 10);
            Destroy(gameObject);
            return;
        }
        transform.position = to;
        if (alignToVelocity && velocity.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(90f, 0f, 0f);
        if (pulse)
            transform.localScale = baseScale * (1f + 0.13f * Mathf.Sin(Time.time * 30f));
        if (Time.time >= dieAt || to.y < -200f)
            Destroy(gameObject);
    }
}
