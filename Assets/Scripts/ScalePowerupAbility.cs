using UnityEngine;

public class ScalePowerupAbility : PowerupAbilityBase
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
        player.transform.localScale *= 1.5f;
        player.GetComponent<Rigidbody>().mass *= 5f;  
        base.Activate(player);
        Debug.Log("Scale Powerup Activated: Player size increased!");
    }
}
