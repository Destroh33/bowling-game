using UnityEngine;
using System.Collections;
public class ScoreSetter : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private TMPro.TextMeshProUGUI scoreText;
    private string scoreString;
    private AudioSource audioSource;
    void Start()
    {
        scoreString = "Last Score: " + GameManager.LastScore;
        scoreText = GetComponent<TMPro.TextMeshProUGUI>();
        audioSource = GetComponent<AudioSource>();
        if (scoreText != null)
        {
            Invoke("InitialCoroutineDelay", 1f);
        }
    }   
    // Update is called once per frame
    void Update()
    {
        
    }
    private void InitialCoroutineDelay()
    {
        StartCoroutine(TypeRoutine(scoreString));
    }

    private IEnumerator TypeRoutine(string message)
    {
        scoreText.text = "";

        for (int i = 0; i < message.Length; i++)
        {
            scoreText.text += message[i];
            if(audioSource != null)
            {
                audioSource.PlayOneShot(audioSource.clip);
            }
            yield return new WaitForSeconds(0.1f);
        }

    }
}
