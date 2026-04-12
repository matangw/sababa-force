using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] ParticleSystem deathParticles;
    [Tooltip("URP-compatible particle material. If unset, a default URP Particles/Unlit material is created once at runtime.")]
    [SerializeField] Material deathParticleMaterial;
    [Tooltip("Logs when death VFX is scheduled; use with Window > Analysis > Particle Effect Stats to verify simulation vs rendering.")]
    [SerializeField] bool debugLogDeathVfx;

    public const int DeathBurstParticleCount = 36;

    [Header("Combat")]
    [SerializeField] float maxHealth = 3f;
    [SerializeField] float damage = 1f;
    [SerializeField] float rateOfFire = 2f;
    [SerializeField] float bulletSpeed = 18f;
    [SerializeField] float shootRange = 14f;
    [SerializeField] Vector2 fireOffset = new Vector2(0.45f, 0f);

    [Header("Movement")]
    [SerializeField] float moveSpeed = 3.5f;
    [SerializeField] float chaseRange = 14f;
    [SerializeField] [Range(0.5f, 1f)] float preferredDistanceFactor = 0.8f;
    [SerializeField] float distanceHysteresis = 0.25f;
    [SerializeField] bool lockMovementToHorizontal = true;

    [Header("Line of sight")]
    [SerializeField] float sightDistance = 12f;
    [SerializeField] float sightHalfAngleDegrees = 70f;
    [SerializeField] float verticalSightTolerance = 2.25f;
    [SerializeField] bool requireClearRaycast = true;
    [SerializeField] LayerMask obstacleMask;
    [SerializeField] Vector2 raycastOriginOffset = new Vector2(0f, 0f);
    [Tooltip("Ray starts slightly along aim so casts do not immediately hit geometry overlapping the enemy.")]
    [SerializeField] float losRayStartInset = 0.12f;
    [Tooltip("Treat obstacles farther than (range minus this) as not blocking (clears LOS near the target).")]
    [SerializeField] float losTargetClearance = 0.35f;
    [Tooltip("Raise LOS probe on Y so casts from the feet/root do not immediately hit the floor collider.")]
    [SerializeField] float losProbeHeightOffset = 0.45f;
    [Tooltip("Ignore obstacle hits closer than this along the ray (overlap / embedded start in ground).")]
    [SerializeField] float losIgnoreHitsCloserThan = 0.28f;

    [Header("Animation")]
    [SerializeField] Animator animator;
    [SerializeField] LayerMask groundLayers;
    [SerializeField] Vector2 groundCheckOffset = new Vector2(0f, -0.52f);
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.45f, 0.08f);
    [SerializeField] float moveAnimThreshold = 0.05f;

    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int MovingHash = Animator.StringToHash("Moving");

    Rigidbody2D _rb;
    float _health;
    float _nextShotTime;
    bool _dead;
    SpriteRenderer _sprite;
    Collider2D _col;
    Transform _playerTransform;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sprite = GetComponent<SpriteRenderer>();
        _col = GetComponent<Collider2D>();
        _health = maxHealth;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Ground");

        if (groundLayers.value == 0)
            groundLayers = LayerMask.GetMask("Ground");

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (deathParticles == null)
        {
            var fxGo = new GameObject("DeathParticles");
            fxGo.transform.SetParent(transform, false);
            // Inactive so AddComponent does not run OnEnable with default playOnAwake=true (burst on spawn).
            fxGo.SetActive(false);
            deathParticles = fxGo.AddComponent<ParticleSystem>();
        }

        ConfigureDeathBurst(deathParticles);
        ApplyDeathParticleMaterial(deathParticles);
        MatchDeathParticleSorting(deathParticles);

        if (!deathParticles.gameObject.activeSelf)
            deathParticles.gameObject.SetActive(true);

        ResetDeathParticlesQuiet(deathParticles);
    }

    void ApplyDeathParticleMaterial(ParticleSystem ps)
    {
        if (ps == null)
            return;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            return;
        if (deathParticleMaterial != null)
        {
            renderer.sharedMaterial = deathParticleMaterial;
            return;
        }
        Material fallback = DeathParticleMaterials.GetOrCreateDefaultUrp();
        if (fallback != null)
            renderer.sharedMaterial = fallback;
    }

    SpriteRenderer SortingReferenceSprite()
    {
        if (_sprite != null)
            return _sprite;
        var sprites = GetComponentsInChildren<SpriteRenderer>(false);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].enabled)
                return sprites[i];
        }
        return sprites.Length > 0 ? sprites[0] : null;
    }

    void MatchDeathParticleSorting(ParticleSystem ps)
    {
        var sr = SortingReferenceSprite();
        if (sr == null || ps == null)
            return;
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        if (pr == null)
            return;
        pr.sortingLayerID = sr.sortingLayerID;
        pr.sortingOrder = sr.sortingOrder + 20;
    }

    static void ResetDeathParticlesQuiet(ParticleSystem ps)
    {
        if (ps == null)
            return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    static void ConfigureDeathBurst(ParticleSystem ps)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.05f;
        main.startLifetime = 0.45f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = Color.red;
        main.maxParticles = 128;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)DeathBurstParticleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(new Color(0.6f, 0f, 0f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(g);
    }

    void FixedUpdate()
    {
        if (_dead)
            return;

        if (bulletPrefab != null)
        {
            if (_playerTransform == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                    _playerTransform = p.transform;
            }

            if (_playerTransform != null)
            {
                Vector2 toPlayer = (Vector2)(_playerTransform.position - transform.position);
                float dist = toPlayer.magnitude;
                if (dist >= 0.001f)
                {
                    bool inChaseRange = dist <= chaseRange;
                    if (inChaseRange)
                    {
                        FaceToward(toPlayer.x);

                        Vector2 moveDir = toPlayer.normalized;
                        if (lockMovementToHorizontal)
                            moveDir = new Vector2(Mathf.Sign(toPlayer.x), 0f);

                        float preferredDistance = shootRange * preferredDistanceFactor;
                        if (lockMovementToHorizontal)
                        {
                            // Hold zone is based on horizontal spacing; full 2D distance made
                            // preferredDistance ~0.8*shootRange so enemies almost never moved in 2D levels.
                            preferredDistance = Mathf.Min(preferredDistance, shootRange * 0.48f);
                        }

                        float spacingForMove = lockMovementToHorizontal ? Mathf.Abs(toPlayer.x) : dist;
                        float h = distanceHysteresis;

                        Vector2 v = _rb.linearVelocity;
                        if (moveDir.sqrMagnitude > 0.01f)
                        {
                            float dirX = moveDir.normalized.x;
                            if (spacingForMove > preferredDistance + h)
                                v.x = dirX * moveSpeed;
                            else
                                v.x = 0f;
                        }
                        else
                            v.x = 0f;
                        _rb.linearVelocity = v;
                    }
                    else
                    {
                        Vector2 v = _rb.linearVelocity;
                        v.x = 0f;
                        _rb.linearVelocity = v;
                    }
                }
            }
        }

        UpdateEnemyAnimator();
    }

    void Update()
    {
        if (_dead || bulletPrefab == null)
            return;

        if (_playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                _playerTransform = p.transform;
            else
                return;
        }

        Vector2 toPlayer = (Vector2)(_playerTransform.position - transform.position);
        float dist = toPlayer.magnitude;
        if (dist < 0.001f)
            return;

        if (dist <= shootRange && HasLineOfSight(dist, toPlayer))
            TryFire(AimWorldDirection(toPlayer));
    }

    /// <summary>
    /// World-space fire direction. Sidescroller enemies use pure horizontal aim so bullets
    /// do not arc toward a higher/lower player.
    /// </summary>
    Vector2 AimWorldDirection(Vector2 toPlayer)
    {
        if (lockMovementToHorizontal)
        {
            if (Mathf.Abs(toPlayer.x) > 0.001f)
                return new Vector2(Mathf.Sign(toPlayer.x), 0f);
            return new Vector2(Mathf.Sign(transform.localScale.x), 0f);
        }

        return toPlayer.normalized;
    }

    void UpdateEnemyAnimator()
    {
        if (animator == null)
            return;
        Vector2 origin = (Vector2)transform.position + groundCheckOffset;
        bool grounded = Physics2D.OverlapBox(origin, groundCheckSize, 0f, groundLayers);
        bool moving = Mathf.Abs(_rb.linearVelocity.x) > moveAnimThreshold;
        animator.SetBool(GroundedHash, grounded);
        animator.SetBool(MovingHash, moving);
    }

    void FaceToward(float deltaX)
    {
        if (Mathf.Abs(deltaX) < 0.01f)
            return;
        float sign = Mathf.Sign(deltaX);
        var scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * sign;
        transform.localScale = scale;
    }

    bool HasLineOfSight(float dist, Vector2 toPlayer)
    {
        float maxSightRange = Mathf.Max(sightDistance, shootRange);
        if (dist > maxSightRange)
            return false;

        float dy = Mathf.Abs(toPlayer.y);
        if (dy > verticalSightTolerance)
            return false;

        Vector2 forward = Vector2.right * Mathf.Sign(transform.localScale.x);
        if (Mathf.Abs(forward.x) < 0.01f)
            forward = Vector2.right;

        Vector2 aimDirForCone = lockMovementToHorizontal
            ? (Mathf.Abs(toPlayer.x) > 0.001f ? new Vector2(Mathf.Sign(toPlayer.x), 0f) : forward)
            : toPlayer.normalized;
        float angle = Vector2.Angle(forward, aimDirForCone);
        if (angle > sightHalfAngleDegrees)
            return false;

        if (requireClearRaycast && obstacleMask.value != 0)
        {
            if (!LosClearAlongAim(toPlayer, dist))
                return false;
        }

        return true;
    }

    bool ColliderBelongsToThisEnemy(Collider2D c)
    {
        return c != null && c.GetComponentInParent<Enemy>() == this;
    }

    /// <summary>
    /// True if no obstacle-mask collider blocks before the target. Skips hits on this enemy.
    /// When <see cref="lockMovementToHorizontal"/> is on, uses a horizontal probe so diagonal
    /// rays do not scrape floor/world geometry and kill sustained fire while walking.
    /// </summary>
    bool LosClearAlongAim(Vector2 toPlayer, float distToTarget)
    {
        Vector2 rawOrigin = (Vector2)transform.position + raycastOriginOffset + new Vector2(0f, losProbeHeightOffset);
        float slack = Mathf.Max(0.01f, losTargetClearance);
        float inset = Mathf.Max(0f, losRayStartInset);

        if (lockMovementToHorizontal)
        {
            float hx = Mathf.Abs(toPlayer.x);
            if (hx < 0.001f)
                return true;
            Vector2 dir = new Vector2(Mathf.Sign(toPlayer.x), 0f);
            float len = Mathf.Max(0f, hx - inset);
            if (len < 0.001f)
                return true;
            Vector2 origin = rawOrigin + dir * inset;
            return !LosRayBlocked(origin, dir, len, slack);
        }

        Vector2 aim = toPlayer.normalized;
        float castLen = Mathf.Max(0f, distToTarget - inset);
        if (castLen < 0.001f)
            return true;
        Vector2 o = rawOrigin + aim * inset;
        return !LosRayBlocked(o, aim, castLen, slack);
    }

    bool LosRayBlocked(Vector2 origin, Vector2 direction, float castLength, float endSlack)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, castLength + 0.02f, obstacleMask);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        float limit = Mathf.Max(0f, castLength - endSlack);
        float ignoreNear = Mathf.Max(0f, losIgnoreHitsCloserThan);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || ColliderBelongsToThisEnemy(c))
                continue;
            if (hits[i].distance < ignoreNear)
                continue;
            if (hits[i].distance < limit)
                return true;
        }

        return false;
    }

    void TryFire(Vector2 aimWorld)
    {
        if (rateOfFire <= 0f || Time.time < _nextShotTime)
            return;

        _nextShotTime = Time.time + 1f / rateOfFire;

        float facing = Mathf.Sign(transform.localScale.x);
        if (Mathf.Abs(facing) < 0.01f)
            facing = 1f;

        Vector3 spawnPos = transform.position + new Vector3(fireOffset.x * facing, fireOffset.y, 0f);
        Vector2 dir = aimWorld.sqrMagnitude > 0.0001f ? aimWorld.normalized : Vector2.right * facing;

        var bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        if (bullet.TryGetComponent<Collider2D>(out var bulletCol))
        {
            foreach (var c in GetComponentsInChildren<Collider2D>(true))
            {
                if (c != null && c.enabled)
                    Physics2D.IgnoreCollision(bulletCol, c, true);
            }
        }

        if (bullet.TryGetComponent<Bullet>(out var b))
            b.Launch(dir, bulletSpeed, damage, shootRange, BulletAlliance.Enemy);
        else if (bullet.TryGetComponent<Rigidbody2D>(out var orb))
            orb.linearVelocity = dir * bulletSpeed;
    }

    public void TakeDamage(float amount)
    {
        if (_dead)
            return;

        _health -= amount;
        if (_health <= 0f)
            Die();
    }

    void Die()
    {
        if (_dead)
            return;
        _dead = true;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null)
                sr.enabled = false;
        }
        if (_col != null)
            _col.enabled = false;

        if (deathParticles != null)
        {
            if (debugLogDeathVfx)
                Debug.Log($"{nameof(Enemy)} death VFX scheduled at {transform.position}", this);

            Transform psTr = deathParticles.transform;
            Vector3 burstWorldPos = psTr.position;
            psTr.SetParent(null, true);
            psTr.position = burstWorldPos;
            ApplyDeathParticleMaterial(deathParticles);
            MatchDeathParticleSorting(deathParticles);
            // Same-frame Stop/Clear + Play often drops bursts; play next frame on this GO (survives enemy destroy).
            DeathParticleBurstRunner.Schedule(deathParticles);
            float d = DeathParticleBurstRunner.DestroyAfterSeconds(deathParticles);
            Destroy(deathParticles.gameObject, d);
        }

        Destroy(gameObject, 0.05f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, sightDistance);

        Vector2 forward = Vector2.right * Mathf.Sign(transform.localScale.x);
        if (Mathf.Abs(forward.x) < 0.01f)
            forward = Vector2.right;
        Quaternion r = Quaternion.Euler(0f, 0f, sightHalfAngleDegrees);
        Vector2 a = r * forward * sightDistance * 0.5f;
        r = Quaternion.Euler(0f, 0f, -sightHalfAngleDegrees);
        Vector2 b = r * forward * sightDistance * 0.5f;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(forward * sightDistance * 0.5f));
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)a);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)b);

        if (animator != null)
        {
            Gizmos.color = Color.green;
            Vector2 origin = (Vector2)transform.position + groundCheckOffset;
            Gizmos.DrawWireCube(origin, groundCheckSize);
        }
    }
}

static class DeathParticleMaterials
{
    static Material s_DefaultUrp;

    public static Material GetOrCreateDefaultUrp()
    {
        if (s_DefaultUrp != null)
            return s_DefaultUrp;

        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit")
            ?? Shader.Find("Particles/Unlit");
        if (sh == null)
            return null;

        s_DefaultUrp = new Material(sh) { name = "DefaultEnemyDeathParticles (URP)" };
        return s_DefaultUrp;
    }
}

/// <summary>
/// Runs on the detached particle GameObject so coroutines survive <see cref="Enemy"/> being destroyed.
/// </summary>
sealed class DeathParticleBurstRunner : MonoBehaviour
{
    ParticleSystem _ps;

    public static void Schedule(ParticleSystem ps)
    {
        if (ps == null)
            return;
        var runner = ps.gameObject.GetComponent<DeathParticleBurstRunner>();
        if (runner == null)
            runner = ps.gameObject.AddComponent<DeathParticleBurstRunner>();
        runner._ps = ps;
        runner.StartCoroutine(runner.CoPlayBurst());
    }

    public static float DestroyAfterSeconds(ParticleSystem ps)
    {
        if (ps == null)
            return 0.5f;
        var main = ps.main;
        float lifeMax = main.startLifetime.constantMax;
        if (lifeMax < 0.01f)
            lifeMax = main.startLifetime.constant;
        return Mathf.Max(0.5f, main.duration + lifeMax + 0.2f);
    }

    IEnumerator CoPlayBurst()
    {
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _ps.Clear(true);
        yield return null;
        _ps.Play(true);
        _ps.Emit(Enemy.DeathBurstParticleCount);
        Destroy(this);
    }
}
