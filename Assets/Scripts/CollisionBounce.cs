using UnityEngine;

public class CollisionBounce : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float reflectMult = 3f;
    public Vector3 forceDir = Vector3.left;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnCollisionEnter(Collision collision)
    {
        if(!collision.gameObject.CompareTag("Player")) return;
        Rigidbody rb = collision.collider.GetComponent<Rigidbody>();
        Debug.Log("collided");
        if (rb != null)
        {
            rb.AddForce(forceDir.normalized * reflectMult, ForceMode.Impulse);
        }   
    }
}
