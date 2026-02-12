using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // add this namespace

public class ScrollRawImage : MonoBehaviour
{
    //store our raw image's uv rect.
    Rect rect;

    //assign these in the inspector.
    public float xOffsetSpeed, yOffsetSpeed;

    void Start()
    {
        rect = GetComponent<RawImage>().uvRect;
    }

    void Update()
    {
        //update offset X
        float newX = rect.x + (xOffsetSpeed * Time.deltaTime);

        // keep range between -1:1, loop back if exceed range.
        switch (newX)
        {
            case > 1:
                newX--;
                break;
            case < -1:
                newX++;
                break;
            default:
                break;
        }

        //assign our rect's x value.
        rect.x = newX;


        //update offset Y
        float newY = rect.y + (yOffsetSpeed * Time.deltaTime);

        // keep range between -1:1, loop back if exceed range.
        switch (newY)
        {
            case > 1:
                newY--;
                break;
            case < -1:
                newY++;
                break;
            default:
                break;
        }

        //assign our rect's y value.
        rect.y = newY;

        //update our raw image's uv rect
        GetComponent<RawImage>().uvRect = rect;
    }
}
