using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScrollSprite : MonoBehaviour
{
    public float scrollSpeed = 2.0f; 
    Vector3 startPosition;
    float tileSize; // get the horizontal size of our sprite

    void Start()
    {
        // could be "transform.position" if sprites are not parented to anything. 
        startPosition = transform.localPosition;

        // could be "bounds" instead of localBounds if sprites are not parented to anything.
        tileSize = GetComponent<Renderer>().localBounds.size.x;
    }

    void Update()
    {
        // animate the position horizontally at a displacement based on "scrollSpeed" 
        //   and loop it back to 0 once we've travelled the same distance equal to our sprite's horizontal size.
        float newPosition = Mathf.Repeat(Time.time * -scrollSpeed, tileSize);

        // could be "transform.position" and "transform.right" if sprites are not parented to anything. 
        transform.localPosition = startPosition + Vector3.right * newPosition;
    }
}
