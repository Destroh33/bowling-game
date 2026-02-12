using UnityEngine;

public class ScrollMaterialTexture : MonoBehaviour
{
    Material mat; //access our material component

    // declare a unique offset variable for each texture map you want to scroll through.
    Vector2 main_offset,detail_offset,height_offset;

    // declare the scroll speed in x- and y- direction for each texture map.
    [Header("Main Texture")]
    public float main_xOffsetSpeed;
    public float main_yOffsetSpeed;

    [Header("Detail Texture")]
    public float detail_xOffsetSpeed;
    public float detail_yOffsetSpeed;

    [Header("HeightMap")]
    public float height_xOffsetSpeed;
    public float height_yOffsetSpeed;
    
    // you can also customize other material properties, like the height map's displacement value.
    public float heightScale;

    void Start()
    {
        mat = GetComponent<Renderer>().material;

        // set the height map at the start of our scene.
        mat.SetFloat("_Parallax", heightScale);
    }

    void Update()
    {
        // === SCROLL THE MAIN TEXTURE ===

        // update offset X 
        float main_newX = main_offset.x + (main_xOffsetSpeed * Time.deltaTime);

        // keep range between -1:1, loop back if exceed range.
        switch (main_newX)
        {
            case > 1:
                main_newX--;
                break;
            case < -1:
                main_newX++;
                break;
            default:
                break;
        }
        //assign the new x-offset value
        main_offset.x = main_newX;

        //update offset Y
        float main_newY = main_offset.y + (main_yOffsetSpeed * Time.deltaTime);

        // keep range between -1:1, loop back if exceed range.
        switch (main_newY)
        {
            case > 1:
                main_newY--;
                break;
            case < -1:
                main_newY++;
                break;
            default:
                break;
        }

        //assign the new y-offset value
        main_offset.y = main_newY;

        //assign the final texture offset vector to our texture property.
        //  try scrolling through other textures by switching out
        //  the property ID string in the first argument.
        mat.SetTextureOffset("_BaseMap", main_offset);

        // === END OF MAIN TEXTURE SCROLL ===

        //update offset X

        float detail_newX = detail_offset.x + (detail_xOffsetSpeed * Time.deltaTime);
        switch (detail_newX)
        {
            case > 1:
                detail_newX--;
                break;
            case < -1:
                detail_newX++;
                break;
            default:
                break;
        }

        detail_offset.x = detail_newX;


        //update offset Y

        float detail_newY = detail_offset.y + (detail_yOffsetSpeed * Time.deltaTime);
        switch (detail_newY)
        {
            case > 1:
                detail_newY--;
                break;
            case < -1:
                detail_newY++;
                break;
            default:
                break;
        }

        detail_offset.y = detail_newY;

        mat.SetTextureOffset("_DetailAlbedoMap", detail_offset);
        mat.SetTextureOffset("_DetailMask", detail_offset);

        //update offset X

        float height_newX = height_offset.x + (height_xOffsetSpeed * Time.deltaTime);
        switch (height_newX)
        {
            case > 1:
                height_newX--;
                break;
            case < -1:
                height_newX++;
                break;
            default:
                break;
        }

        height_offset.x = height_newX;


        //update offset Y

        float height_newY = height_offset.y + (height_yOffsetSpeed * Time.deltaTime);
        switch (height_newY)
        {
            case > 1:
                height_newY--;
                break;
            case < -1:
                height_newY++;
                break;
            default:
                break;
        }

        height_offset.y = height_newY;

        mat.SetTextureOffset("_ParallaxMap", height_offset);
        

    }
}
