using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    Rigidbody rb;

    //INSTRUCTIONS:
    //player must press [Q] [W] [E] [R] keys in FORWARD or BACKWARD sequential order
    //in order to move RIGHT or LEFT.

    //int? prevKey;
    // an "int?" is a nullable integer, meaning we can assign a null value to it.
        // which is useful for checking whether our integer has been initialised yet.
        // you can make other variable types nullable by adding a "?" at the end,
        // e.g. "public float? nullableFloat"

    public int currentKey;
    KeyCode[] QWER = {KeyCode.Q,KeyCode.W,KeyCode.E,KeyCode.R};

    [SerializeField] float speed;
    [SerializeField] SpriteSwitcher sprSwitcher;

    [SerializeField] Vector3 direction;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentKey = 0;
        //prevKey = currentKey;
    }

    // Update is called once per frame
    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        direction = new Vector3(h, 0, v);

        if (!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.D) )
        {
            currentKey = 0; // idle
        } else
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                currentKey = 1;
            }
            if (Input.GetKeyDown(KeyCode.A))
            {
                currentKey = 3; // left
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                currentKey = 2; // back
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                currentKey = 4; // right
            }
        }


        if (!sprSwitcher.CheckIfMatchKey(currentKey))
        {
            sprSwitcher.UpdateSprite(currentKey);
        }
    }

    private void FixedUpdate()
    {

        rb.MovePosition(transform.position + new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"))*speed*Time.deltaTime);

        /*
        if ((currentKey==prevKey+1)||(currentKey == 0 && prevKey == QWER.Length - 1))
        {
            //move right
            rb.MovePosition(transform.position + Vector3.right*speed);
            sprSwitcher.UpdateSprite(currentKey);
            prevKey = currentKey;
        } else if ((currentKey == QWER.Length-1 && prevKey == 0) || (currentKey == prevKey - 1))
        {
            //move left
            rb.MovePosition(transform.position + Vector3.left * speed);
            sprSwitcher.UpdateSprite(currentKey);
            prevKey = currentKey;
        } 
        */
    }
}
