using UnityEngine;
using UnityEngine.InputSystem;

public class Shooter : MonoBehaviour
{
    [SerializeField] GameObject bulletPrefab;
    [Header("Weapon stats")]
    [SerializeField] float damage = 1f;
    [Tooltip("Shots per second. Set to 0 for single-shot (press only).")]
    [SerializeField] float rateOfFire = 6f;
    [Tooltip("How far the bullet can travel before it despawns.")]
    [SerializeField] float range = 12f;

    [Header("Bullet")]
    [SerializeField] float bulletSpeed = 24f;
    [SerializeField] Vector2 fireOffset = new Vector2(0.55f, 0f);
    [SerializeField] PlayerController playerController;

    float _nextShotTime;

    void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || bulletPrefab == null)
            return;

        bool wantsToShoot = rateOfFire > 0f ? kb.jKey.isPressed : kb.jKey.wasPressedThisFrame;
        if (!wantsToShoot)
            return;

        if (rateOfFire > 0f)
        {
            if (Time.time < _nextShotTime)
                return;
            _nextShotTime = Time.time + (1f / rateOfFire);
        }

        float facing = playerController != null ? playerController.FacingSign : Mathf.Sign(transform.localScale.x);
        if (Mathf.Abs(facing) < 0.01f)
            facing = 1f;

        Vector2 dir = Vector2.right * facing;
        Vector3 spawnPos = transform.position + new Vector3(fireOffset.x * facing, fireOffset.y, 0f);

        var bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        if (bullet.TryGetComponent<Collider2D>(out var bulletCol))
        {
            foreach (var c in GetComponentsInChildren<Collider2D>(true))
                Physics2D.IgnoreCollision(bulletCol, c, true);
        }

        if (bullet.TryGetComponent<Bullet>(out var b))
            b.Launch(dir, bulletSpeed, damage, range, BulletAlliance.Player, gameObject);
        else if (bullet.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = dir * bulletSpeed;
    }
}
