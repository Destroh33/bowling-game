using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SwipeVisual : MonoBehaviour
{
    [SerializeField] BowlingSwipeController bsc;

    [SerializeField] float refWidth = 666f;
    [SerializeField] float refHeight = 441f;

    [SerializeField] Image segmentPrefab;
    [SerializeField] float thickness = 6f;
    [SerializeField] int maxSegments = 256;

    [SerializeField] float fadeDelay = 0.05f;
    [SerializeField] float fadeDuration = 0.35f;

    RectTransform _canvasRect;
    Canvas _canvas;

    readonly List<Image> _pool = new();
    int _activeSegments;

    int _lastSampleCount;
    float _lastSampleChangeTime;
    bool _fading;
    float _fadeStartTime;

    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _canvasRect = transform as RectTransform;
        if (_canvasRect == null) _canvasRect = GetComponent<RectTransform>();
    }

    void Start()
    {
        if (bsc == null)
            bsc = FindFirstObjectByType<BowlingSwipeController>();

        _lastSampleChangeTime = Time.unscaledTime;
    }

    void Update()
    {
        if (bsc == null)
        {
            bsc = FindFirstObjectByType<BowlingSwipeController>();
            if (bsc == null) return;
        }

        var samples = bsc.samples;
        int count = samples != null ? samples.Count : 0;

        if (count <= 1)
        {
            _fading = false;
            _activeSegments = 0;
            ApplyAlphaToActive(0f);
            _lastSampleCount = count;
            return;
        }
        if (count != _lastSampleCount)
        {
            _lastSampleCount = count;
            _lastSampleChangeTime = Time.unscaledTime;

            _fading = false;
        }
        else
        {
            if (!_fading && (Time.unscaledTime - _lastSampleChangeTime) >= fadeDelay)
            {
                _fading = true;
                _fadeStartTime = Time.unscaledTime;
            }
        }

        float alpha = 1f;
        if (_fading)
        {
            float t = (Time.unscaledTime - _fadeStartTime) / Mathf.Max(0.0001f, fadeDuration);
            alpha = Mathf.Clamp01(1f - t);
        }

        DrawPolyline(samples, alpha);

        // Once fully faded, hide
        if (_fading && alpha <= 0.0001f)
        {
            _activeSegments = 0;
            ApplyAlphaToActive(0f);
        }
    }

    void DrawPolyline(List<BowlingSwipeController.Sample> samples, float alpha)
    {
        int count = samples.Count;
        int needed = Mathf.Min(maxSegments, count - 1);
        EnsurePool(needed);
        Vector2 prevLocal = RefToCanvasLocal(samples[0].p);

        _activeSegments = needed;

        for (int i = 1; i <= needed; i++)
        {
            Vector2 curLocal = RefToCanvasLocal(samples[i].p);

            Image img = _pool[i - 1];
            if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);

            RectTransform rt = (RectTransform)img.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Vector2 d = curLocal - prevLocal;
            float len = d.magnitude;
            if (len < 0.001f) len = 0.001f;

            Vector2 mid = (prevLocal + curLocal) * 0.5f;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(len, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, ang);

            Color c = img.color;
            c.a = alpha;
            img.color = c;

            prevLocal = curLocal;
        }
        for (int i = needed; i < _pool.Count; i++)
        {
            if (_pool[i].gameObject.activeSelf)
                _pool[i].gameObject.SetActive(false);
        }
    }

    Vector2 RefToCanvasLocal(Vector2 refPos)
    {
        float sx = Screen.width / Mathf.Max(1f, refWidth);
        float sy = Screen.height / Mathf.Max(1f, refHeight);

        Vector2 screenPx = new Vector2(refPos.x * sx, refPos.y * sy);

        Camera uiCam = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCam = _canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPx, uiCam, out Vector2 local);
        return local;
    }

    void EnsurePool(int needed)
    {
        if (segmentPrefab == null) return;

        while (_pool.Count < needed)
        {
            Image img = Instantiate(segmentPrefab, transform);
            img.raycastTarget = false;
            img.gameObject.SetActive(false);

            Color c = img.color;
            c.a = 0f;
            img.color = c;

            _pool.Add(img);
        }
    }

    void ApplyAlphaToActive(float alpha)
    {
        int n = Mathf.Min(_activeSegments, _pool.Count);
        for (int i = 0; i < n; i++)
        {
            var img = _pool[i];
            if (img == null) continue;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
            if (alpha <= 0f && img.gameObject.activeSelf) img.gameObject.SetActive(false);
        }
    }
}
