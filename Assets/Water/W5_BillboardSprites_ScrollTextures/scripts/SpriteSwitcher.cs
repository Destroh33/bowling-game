using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSwitcher : MonoBehaviour
{
    Animator anim;
    public string spriteKeyName;

    private void Start()
    {
        anim = GetComponent<Animator>();
    }

    public bool CheckIfMatchKey(int thisInt)
    {
        return anim.GetInteger(spriteKeyName) == thisInt;
    }

    public void UpdateSprite(int toThisSprite)
    {
        anim.SetInteger(spriteKeyName, toThisSprite);
    }
}
