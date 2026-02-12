using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScrollTiledSprite : MonoBehaviour
{
    SpriteRenderer sprRend;

    float startingX, // get our starting width
        offset; // stores our offset over time, which we'll add to our width.

    public float xOffsetSpeed; // adjust this in the inspector.

    void Start()
    {
        sprRend = GetComponent<SpriteRenderer>();
        startingX = sprRend.size.x;
    }

    void Update()
    {
        // update offset
        offset += (xOffsetSpeed * Time.deltaTime); 

        // keep "offset" range between -2 and 2.
        //   when sprite's width has increased by TWICE its original size, 
        //   it loops back to its starting "position".
        switch (offset)
        {
            case > 2:
                offset-=2;
                break;
            case < -2:
                offset+=2;
                break;
            default:
                break;
        }


        // assign new sprite size as a vector 2.
        sprRend.size = new Vector2(startingX * (offset+1) , sprRend.size.y);

    }
}
