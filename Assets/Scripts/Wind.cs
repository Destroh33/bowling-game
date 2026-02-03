using UnityEngine;

public class Wind : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Vector3 worldDir;
    [SerializeField] private float forceStrength = 200f;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerStay(Collider other)
    {
        if(other.gameObject.GetComponent<Rigidbody>())
        {
            Debug.Log(other.gameObject.name + " in wind zone");
            Rigidbody rb = other.gameObject.GetComponent<Rigidbody>();
            rb.AddForce(worldDir*forceStrength, ForceMode.VelocityChange);
        }
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + worldDir*5);
    }
}
