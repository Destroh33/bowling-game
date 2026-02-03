using UnityEngine;

public class FrictionPowerupAbility : PowerupAbilityBase
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private GameObject lane;
    [SerializeField] private PhysicsMaterial stickyMat;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public override void Activate(GameObject player)
    {
        lane.GetComponent<Collider>().material = stickyMat;
    }
}
