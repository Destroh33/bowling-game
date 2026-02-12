using UnityEngine;

public class BallVisualSpinScaler : MonoBehaviour
{
    [SerializeField] Rigidbody sourceRb;
    [SerializeField] float spinScale = 0.25f;

    Quaternion lastParentRot;
    Quaternion visualWorldRot;

    void Awake()
    {
        if (!sourceRb)
            sourceRb = GetComponentInParent<Rigidbody>();

        if (transform.parent != null)
            lastParentRot = transform.parent.rotation;

        visualWorldRot = transform.rotation;
    }

    void LateUpdate()
    {
        if (!sourceRb || transform.parent == null)
            return;

        Transform parent = transform.parent;

        Quaternion parentRot = parent.rotation;
        Quaternion parentDelta = parentRot * Quaternion.Inverse(lastParentRot);

        Quaternion scaledDelta = Quaternion.Slerp(Quaternion.identity, parentDelta, spinScale);

        visualWorldRot = scaledDelta * visualWorldRot;

        transform.localRotation = Quaternion.Inverse(parentRot) * visualWorldRot;

        lastParentRot = parentRot;
    }
}
