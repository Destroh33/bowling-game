using UnityEngine;

public class Spinner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private float spinSpeed = 100f;
    [SerializeField] private Vector3 spinAxis = Vector3.up;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       transform.Rotate(spinAxis, spinSpeed * Time.deltaTime);
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + spinAxis.normalized * 2);
    }
}
