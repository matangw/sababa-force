using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float acceleration = 80f;
    [SerializeField] float friction = 70f;

    [Header("Jump")]
    [SerializeField] float jumpForce = 14f;
    [SerializeField] float doubleJumpForce = 12f;
    [SerializeField] int extraJumps = 1;

    [Header("Ground check")]
    [SerializeField] LayerMask groundLayers;
    [SerializeField] Vector2 groundCheckOffset = new Vector2(0f, -0.52f);
    [SerializeField] Vector2 groundCheckSize = new Vector2(0.45f, 0.08f);

    [Header("Animation")]
    [SerializeField] Animator animator;
    [SerializeField] float moveAnimThreshold = 0.05f;

    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int MovingHash = Animator.StringToHash("Moving");

    Rigidbody2D _rb;
    int _jumpsRemaining;

    public float FacingSign { get; private set; } = 1f;
    public bool IsGrounded { get; private set; }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    void Start()
    {
        if (animator != null)
            animator.Rebind();
    }

    void Update()
    {
        IsGrounded = CheckGrounded();
        if (IsGrounded)
            _jumpsRemaining = extraJumps;

        var kb = Keyboard.current;
        if (kb != null)
        {
            float input = 0f;
            if (kb.aKey.isPressed) input -= 1f;
            if (kb.dKey.isPressed) input += 1f;

            if (Mathf.Abs(input) > 0.01f)
                FacingSign = Mathf.Sign(input);

            if (kb.spaceKey.wasPressedThisFrame)
            {
                if (IsGrounded)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
                    IsGrounded = false;
                }
                else if (_jumpsRemaining > 0)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, doubleJumpForce);
                    _jumpsRemaining--;
                }
            }

            float targetVx = input * moveSpeed;
            float rate = Mathf.Abs(input) > 0.01f ? acceleration : friction;
            float newVx = Mathf.MoveTowards(_rb.linearVelocity.x, targetVx, rate * Time.deltaTime);
            _rb.linearVelocity = new Vector2(newVx, _rb.linearVelocity.y);

            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * FacingSign;
            transform.localScale = scale;
        }

        if (animator != null)
        {
            animator.SetBool(GroundedHash, IsGrounded);
            animator.SetBool(MovingHash, Mathf.Abs(_rb.linearVelocity.x) > moveAnimThreshold);
        }
    }

    bool CheckGrounded()
    {
        Vector2 origin = (Vector2)transform.position + groundCheckOffset;
        return Physics2D.OverlapBox(origin, groundCheckSize, 0f, groundLayers);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 origin = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireCube(origin, groundCheckSize);
    }
}
