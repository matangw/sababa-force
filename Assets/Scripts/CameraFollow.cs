using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smoothTime = 0.08f;

    Vector3 _velocity;

    void LateUpdate()
    {
        if (target == null)
            target = ResolveTarget();

        if (target == null)
            return;

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        if (smoothTime <= 0f)
            transform.position = desired;
        else
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
    }

    Transform ResolveTarget()
    {
        var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
            return taggedPlayer.transform;

        var playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
            return playerController.transform;

        return null;
    }
}
