using UnityEngine;

public enum BulletAlliance
{
    Player,
    Enemy
}

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    const int CastBufferLen = 8;
    const float CastSkin = 0.05f;

    [SerializeField] float lifetimeSeconds = 2f;

    Rigidbody2D _rb;
    Collider2D _col;
    Vector2 _startPos;
    float _damage;
    float _maxTravelDistance;
    BulletAlliance _alliance;
    GameObject _ownerRoot;
    bool _resolvedHit;
    readonly RaycastHit2D[] _castHits = new RaycastHit2D[CastBufferLen];

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void OnEnable()
    {
        Destroy(gameObject, lifetimeSeconds);
    }

    public void Launch(Vector2 direction, float speed)
    {
        Launch(direction, speed, damage: 0f, range: 0f, BulletAlliance.Player, ownerRoot: null);
    }

    public void Launch(Vector2 direction, float speed, float damage, float range)
    {
        Launch(direction, speed, damage, range, BulletAlliance.Player, ownerRoot: null);
    }

    public void Launch(Vector2 direction, float speed, float damage, float range, BulletAlliance alliance)
    {
        Launch(direction, speed, damage, range, alliance, ownerRoot: null);
    }

    public void Launch(Vector2 direction, float speed, float damage, float range, BulletAlliance alliance, GameObject ownerRoot)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;
        direction.Normalize();
        _rb.linearVelocity = direction * speed;
        _damage = damage;
        _maxTravelDistance = range;
        _startPos = transform.position;
        _alliance = alliance;
        _ownerRoot = ownerRoot;
        _resolvedHit = false;
    }

    void FixedUpdate()
    {
        if (_resolvedHit)
            return;

        Vector2 v = _rb.linearVelocity;
        float speed = v.magnitude;
        if (speed < 1e-4f)
            return;

        var filter = new ContactFilter2D
        {
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false,
            useTriggers = false
        };

        int n = _rb.Cast(v.normalized, filter, _castHits, speed * Time.fixedDeltaTime + CastSkin);
        for (int i = 0; i < n; i++)
        {
            Collider2D hitCol = _castHits[i].collider;
            if (hitCol == null || hitCol.attachedRigidbody == _rb)
                continue;
            ResolveHit(hitCol);
            if (_resolvedHit)
                break;
        }
    }

    void Update()
    {
        if (_maxTravelDistance > 0f)
        {
            float travelled = Vector2.Distance(_startPos, transform.position);
            if (travelled >= _maxTravelDistance)
                Destroy(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        ResolveHit(collision.collider);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        ResolveHit(other);
    }

    void ResolveHit(Collider2D other)
    {
        if (_resolvedHit)
            return;

        if (_alliance == BulletAlliance.Enemy
            && !IsPlayerCollider(other)
            && IsEnemyFriendlyFireTarget(other))
        {
            if (_col != null)
                Physics2D.IgnoreCollision(_col, other, true);
            return;
        }

        if (_alliance == BulletAlliance.Player
            && _ownerRoot != null
            && IsColliderFromOwnerPlayer(other, _ownerRoot))
        {
            if (_col != null)
                Physics2D.IgnoreCollision(_col, other, true);
            return;
        }

        _resolvedHit = true;

        if (_damage > 0f)
        {
            var ph = other.GetComponentInParent<PlayerHealth>();
            if (ph != null)
                ph.TakeDamage(_damage);
            else
            {
                other.SendMessageUpwards(
                    "TakeDamage",
                    _damage,
                    SendMessageOptions.DontRequireReceiver
                );
            }
        }
        Destroy(gameObject);
    }

    static bool IsPlayerCollider(Collider2D c)
    {
        if (c.GetComponentInParent<PlayerHealth>() != null)
            return true;

        for (Transform t = c.transform; t != null; t = t.parent)
        {
            if (t.CompareTag("Player"))
                return true;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0)
            return false;
        for (Transform t = c.transform; t != null; t = t.parent)
        {
            if (t.gameObject.layer == playerLayer)
                return true;
        }
        return false;
    }

    static bool IsEnemyFriendlyFireTarget(Collider2D c)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0 && c.gameObject.layer == enemyLayer)
            return true;
        return c.GetComponentInParent<Enemy>() != null;
    }

    /// <summary>
    /// True when <paramref name="other"/> belongs to the shooting player instance (same root as <paramref name="ownerRoot"/>, typically Player-tagged).
    /// </summary>
    static bool IsColliderFromOwnerPlayer(Collider2D other, GameObject ownerRoot)
    {
        if (ownerRoot == null)
            return false;

        Transform t = other.transform;
        if (t == ownerRoot.transform || t.IsChildOf(ownerRoot.transform))
            return true;

        Transform hitPlayerRoot = FindTaggedAncestor(t, "Player");
        return hitPlayerRoot != null && hitPlayerRoot.gameObject == ownerRoot;
    }

    static Transform FindTaggedAncestor(Transform t, string tag)
    {
        for (Transform x = t; x != null; x = x.parent)
        {
            if (x.CompareTag(tag))
                return x;
        }

        return null;
    }
}
