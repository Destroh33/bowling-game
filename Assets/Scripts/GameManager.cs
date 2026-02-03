using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public List<BowlingPinController> pins;
    private bool win = false;
    private int pinsKnocked = 0;

    [SerializeField] private TMPro.TextMeshProUGUI winText;
    [SerializeField] private TMPro.TextMeshProUGUI pinsKnockedText;

    private bool cachedWin = false;
    private bool startWinCheck = false;

    [SerializeField] private float winZoomDuration = 0.25f;
    [SerializeField] private float winPunchOvershoot = 1.12f;

    Coroutine winZoomRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        winText.enabled = false;
        winText.rectTransform.localScale = Vector3.zero;
    }

    void Update()
    {
        if (!startWinCheck)
            return;

        WinCheck();
        pinsKnockedText.text = "Pins Knocked: " + pinsKnocked + "/10";

        if (win != cachedWin)
        {
            cachedWin = win;
            if (win)
            {
                winText.enabled = true;
                if (winZoomRoutine != null) StopCoroutine(winZoomRoutine);
                winZoomRoutine = StartCoroutine(WinZoomIn());
            }
        }
    }

    IEnumerator WinZoomIn()
    {
        var rt = winText.rectTransform;
        float d = Mathf.Max(0.0001f, winZoomDuration);
        float o = Mathf.Max(1f, winPunchOvershoot);

        float split = 0.7f;
        float t0 = Time.unscaledTime;

        while (true)
        {
            float t = (Time.unscaledTime - t0) / d;
            if (t >= 1f) break;

            float s;
            if (t < split)
            {
                float a = t / split;
                float e = 1f - Mathf.Pow(1f - a, 3f);
                s = Mathf.LerpUnclamped(0f, o, e);
            }
            else
            {
                float b = (t - split) / (1f - split);
                float e = 1f - Mathf.Pow(1f - b, 3f);
                s = Mathf.LerpUnclamped(o, 1f, e);
            }

            rt.localScale = Vector3.one * s;
            yield return null;
        }

        rt.localScale = Vector3.one;
        winZoomRoutine = null;
    }

    public bool KnockedCheck()
    {
        return pins.TrueForAll(pin => pin.isKnocked);
    }

    public void StartWinChecking()
    {
        startWinCheck = true;
        Invoke(nameof(ReloadScene), 10f);
    }

    public void WinCheck()
    {
        win = KnockedCheck();
        Debug.Log("Win status: " + win);
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void AddKnock()
    {
        pinsKnocked++;
    }
}
