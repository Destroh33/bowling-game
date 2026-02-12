using UnityEngine;

public class SplashPlayer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private GameObject splashVFX;
    [SerializeField] private AudioSource audioSource;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        Instantiate(splashVFX, other.transform.position, Quaternion.identity);
        audioSource.PlayOneShot(audioSource.clip);
    }
}
