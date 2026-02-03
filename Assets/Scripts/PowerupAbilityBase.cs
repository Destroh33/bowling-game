using UnityEngine;

public class PowerupAbilityBase : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public virtual void Activate(GameObject player)
    {
        Debug.Log("Powerup activated on " + player.name);
    }
}
