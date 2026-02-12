using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

public class HoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float growSpeed = 10f;
    [SerializeField] private string sceneToLoad;

    private Vector3 _baseScale;
    private bool _hovered;

    void Awake()
    {
        _baseScale = transform.localScale;
    }

    void Update()
    {
        Vector3 target = _hovered ? _baseScale * hoverScale : _baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * growSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
    }

    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
            SceneManager.LoadScene(sceneToLoad);
    }
}
