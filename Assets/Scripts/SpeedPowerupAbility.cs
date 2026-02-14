using UnityEngine;

public class SpeedPowerupAbility : PowerupAbilityBase
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public override void Activate(GameObject player)
    {
        player.GetComponent<Rigidbody>().linearVelocity *= 1.5f;
        base.Activate(player);
    }
}
