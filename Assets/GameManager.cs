using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public List<BowlingPinController> pins;
    private bool win = false;
    private int pinsKnocked = 0;

    [SerializeField] private TMPro.TextMeshProUGUI winText;
    [SerializeField] private TMPro.TextMeshProUGUI pinsKnockedText;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
    }

    public bool KnockedCheck()
    {
        return pins.TrueForAll(pin => pin.isKnocked);
    }
    public void WinCheck()
    {
        win = KnockedCheck();
        Debug.Log("Win status: " + win);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void AddKnock()
    {
        pinsKnocked++;
    }
}
