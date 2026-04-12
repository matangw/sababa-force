using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 5f;
    float _health;

    void Awake()
    {
        _health = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        _health -= Mathf.Max(0f, amount);
        if (_health <= 0f)
            Destroy(gameObject);
    }
}
