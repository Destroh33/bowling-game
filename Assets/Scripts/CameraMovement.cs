using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] Transform newLoc;

    [SerializeField] float speedScale = 0.8f;

    float moveSpeed = 0f;
    float trackSpeed = -1f;

    void Update()
    {
        if (newLoc == null)
            return;

        float s = trackSpeed >= 0f ? trackSpeed : moveSpeed;
        transform.position = Vector3.MoveTowards(transform.position, newLoc.position, s * speedScale * Time.deltaTime);
    }

    public void SetTarget(Transform target)
    {
        newLoc = target;
    }

    public void SetMoveSpeed(float s)
    {
        moveSpeed = Mathf.Max(0f, s);
        Debug.Log($"Camera move speed set to {moveSpeed}");
    }

    public void SetTrackSpeedFromBall(float initialXVelocity)
    {
        trackSpeed = Mathf.Abs(initialXVelocity);
    }

    public void ClearTrackSpeed()
    {
        trackSpeed = -1f;
    }
}
