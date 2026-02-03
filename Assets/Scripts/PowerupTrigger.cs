using UnityEngine;

public class PowerupTrigger : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Powerup collected!");
            object powerupAbility = GetComponent<PowerupAbilityBase>();
            if(powerupAbility != null)
            {
                ((PowerupAbilityBase)powerupAbility).Activate(other.gameObject);
            }
            Destroy(gameObject); // Remove the powerup from the scene
        }
    }
}
