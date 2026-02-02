
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
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        winText.enabled = false;
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
            }
        }
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
