using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static int LastScore = 0;

    [Serializable]
    public struct Lane
    {
        public Transform camStartTransform;
        public Transform camEndTransform;
        public Transform ballStartTransform;
        public GameObject pinsRoot;
    }

    [SerializeField] private List<Lane> lanes = new List<Lane>();
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private CameraMovement cameraMovement;

    [SerializeField] private TMPro.TextMeshProUGUI winText;
    [SerializeField] private TMPro.TextMeshProUGUI pinsKnockedText;

    [SerializeField] private float winZoomDuration = 0.25f;
    [SerializeField] private float winPunchOvershoot = 1.12f;

    [SerializeField] private float laneTimeLimitSeconds = 10f;
    [SerializeField] private float strikeToNextLaneDelay = 2.5f;
    [SerializeField] private float cameraFlySpeedToNextLane = 60f;

    [SerializeField] private bool restartWholeLevelOnNoStrike = true;

    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private float finalLaneReturnDelay = 2.5f;

    private readonly List<BowlingPinController> _activePins = new List<BowlingPinController>();

    private int _currentLaneIndex = 0;

    private bool _startWinCheck = false;

    private int _pinsKnockedThisLane = 0;
    private int _pinsTotalThisLane = 10;

    private int _pinsKnockedOverall = 0;
    private int _pinsTotalOverall = 0;

    private Coroutine _popRoutine;
    private Coroutine _laneTimerRoutine;
    private Coroutine _nextLaneRoutine;

    private GameObject _currentBallInstance;
    private Rigidbody _ballRb;

    private bool firstShot = true;
    [SerializeField] private GameObject handImage;

    [SerializeField] private AudioSource victorySource;
    [SerializeField] private AudioSource pointSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (winText != null)
        {
            winText.enabled = false;
            winText.rectTransform.localScale = Vector3.zero;
        }

        if (pinsKnockedText != null)
            pinsKnockedText.enabled = true;
    }

    void Start()
    {
        StampAllPinsLaneNumbers();
        RecalcOverallTotalPins();

        if (lanes != null && lanes.Count > 0)
            SetLane(0, true);

        UpdateScoreText();
    }

    void Update()
    {
        UpdateScoreText();
    }

    void StampAllPinsLaneNumbers()
    {
        if (lanes == null) return;

        for (int i = 0; i < lanes.Count; i++)
        {
            var root = lanes[i].pinsRoot;
            if (root == null) continue;

            var pins = root.GetComponentsInChildren<BowlingPinController>(true);
            if (pins == null) continue;

            for (int p = 0; p < pins.Length; p++)
            {
                if (pins[p] != null)
                    pins[p].SetLaneNumber(i);
            }
        }
    }

    void RecalcOverallTotalPins()
    {
        int total = 0;

        if (lanes != null)
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                var root = lanes[i].pinsRoot;
                if (root == null) continue;

                var pins = root.GetComponentsInChildren<BowlingPinController>(true);
                if (pins != null) total += pins.Length;
            }
        }

        _pinsTotalOverall = Mathf.Max(1, total);
        _pinsKnockedOverall = Mathf.Clamp(_pinsKnockedOverall, 0, _pinsTotalOverall);
    }

    void UpdateScoreText()
    {
        if (pinsKnockedText == null) return;

        if (!pinsKnockedText.enabled)
            pinsKnockedText.enabled = true;

        int laneCount = Mathf.Max(1, lanes.Count);
        int laneNum = Mathf.Clamp(_currentLaneIndex + 1, 1, laneCount);

        int overallTotal = Mathf.Max(1, _pinsTotalOverall);
        int overallKnocked = Mathf.Clamp(_pinsKnockedOverall, 0, overallTotal);

        pinsKnockedText.text = $"Lane {laneNum}/{laneCount}  Pins Knocked: {overallKnocked}/{overallTotal}";
    }

    public void StartWinChecking()
    {
        _startWinCheck = true;
        StartLaneTimer();
    }

    public void OnBallThrown(float initialXVelocity)
    {
        if (firstShot)
        {
            firstShot = false;
            HideTutorialAnim();
        }

        if (cameraMovement != null)
        {
            cameraMovement.SetTrackSpeedFromBall(initialXVelocity);
            var lane = lanes[_currentLaneIndex];
            if (lane.camEndTransform != null)
                cameraMovement.SetTarget(lane.camEndTransform);
        }

        if (!_startWinCheck)
            StartWinChecking();
    }

    private void SetLane(int laneIndex, bool snapCamToStart)
    {
        laneIndex = Mathf.Clamp(laneIndex, 0, lanes.Count - 1);
        _currentLaneIndex = laneIndex;

        if (_nextLaneRoutine != null) { StopCoroutine(_nextLaneRoutine); _nextLaneRoutine = null; }

        _activePins.Clear();
        _pinsKnockedThisLane = 0;
        _pinsTotalThisLane = 10;

        if (_currentBallInstance != null)
            Destroy(_currentBallInstance);

        _currentBallInstance = null;
        _ballRb = null;

        var lane = lanes[_currentLaneIndex];
        _startWinCheck = false;

        if (ballPrefab != null && lane.ballStartTransform != null)
        {
            _currentBallInstance = Instantiate(
                ballPrefab,
                lane.ballStartTransform.position,
                lane.ballStartTransform.rotation
            );

            _ballRb = _currentBallInstance.GetComponent<Rigidbody>();
        }

        var pinsRoot = lane.pinsRoot;
        if (pinsRoot != null)
        {
            _activePins.AddRange(pinsRoot.GetComponentsInChildren<BowlingPinController>(true));
            _pinsTotalThisLane = Mathf.Max(1, _activePins.Count);
        }

        if (_popRoutine != null) { StopCoroutine(_popRoutine); _popRoutine = null; }

        if (winText != null)
        {
            winText.enabled = false;
            winText.rectTransform.localScale = Vector3.zero;
        }

        if (cameraMovement != null)
        {
            if (snapCamToStart && lane.camStartTransform != null)
            {
                cameraMovement.transform.position = lane.camStartTransform.position;
                cameraMovement.transform.rotation = lane.camStartTransform.rotation;
            }

            cameraMovement.SetTarget(lane.camStartTransform);
            cameraMovement.SetMoveSpeed(cameraFlySpeedToNextLane);
            cameraMovement.ClearTrackSpeed();
        }

        if (pinsKnockedText != null)
            pinsKnockedText.enabled = true;

        UpdateScoreText();

        if (_startWinCheck)
            StartLaneTimer();
    }

    public void AddKnock(int laneNumber)
    {
        _pinsKnockedOverall = Mathf.Clamp(_pinsKnockedOverall + 1, 0, Mathf.Max(1, _pinsTotalOverall));
        UpdateScoreText();

        if (laneNumber != _currentLaneIndex)
            return;

        _pinsKnockedThisLane = Mathf.Clamp(_pinsKnockedThisLane + 1, 0, Mathf.Max(1, _pinsTotalThisLane));

        if (_pinsKnockedThisLane >= Mathf.Max(1, _pinsTotalThisLane))
        {
            ShowPop("STRIKE");
            OnStrike();
            return;
        }

        if (pointSource != null && pointSource.clip != null)
            pointSource.PlayOneShot(pointSource.clip);

        ShowPop($"{_pinsKnockedThisLane}/{Mathf.Max(1, _pinsTotalThisLane)}");
    }

    void ShowPop(string text)
    {
        if (winText == null) return;

        winText.text = text;
        winText.enabled = true;

        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(PopZoom());
    }

    private void OnStrike()
    {
        StopLaneTimer();

        if (victorySource != null && victorySource.clip != null)
            victorySource.PlayOneShot(victorySource.clip);

        if (_nextLaneRoutine != null) StopCoroutine(_nextLaneRoutine);
        _nextLaneRoutine = StartCoroutine(GoNextLaneAfterDelay(strikeToNextLaneDelay));
    }

    private IEnumerator GoNextLaneAfterDelay(float delay)
    {
        float d = Mathf.Max(0f, delay);
        if (d > 0f) yield return new WaitForSeconds(d);

        int next = _currentLaneIndex + 1;
        if (next >= lanes.Count)
        {
            LastScore = _pinsKnockedOverall;
            float r = Mathf.Max(0f, finalLaneReturnDelay);
            if (r > 0f) yield return new WaitForSeconds(r);
            SceneManager.LoadScene(mainSceneName);
            yield break;
        }

        if (cameraMovement != null)
        {
            Transform nextStart = lanes[next].camStartTransform;
            cameraMovement.SetMoveSpeed(cameraFlySpeedToNextLane);
            cameraMovement.ClearTrackSpeed();
            cameraMovement.SetTarget(nextStart);
        }

        yield return new WaitUntil(() =>
        {
            if (cameraMovement == null) return true;
            Transform target = lanes[next].camStartTransform;
            if (target == null) return true;
            return Vector3.Distance(cameraMovement.transform.position, target.position) <= 0.05f;
        });

        SetLane(next, false);
    }

    private void StartLaneTimer()
    {
        StopLaneTimer();
        _laneTimerRoutine = StartCoroutine(LaneTimerRoutine(laneTimeLimitSeconds));
    }

    private void StopLaneTimer()
    {
        if (_laneTimerRoutine != null)
        {
            StopCoroutine(_laneTimerRoutine);
            _laneTimerRoutine = null;
        }
    }

    private IEnumerator LaneTimerRoutine(float seconds)
    {
        float t0 = Time.time;
        float limit = Mathf.Max(0.01f, seconds);

        while (Time.time - t0 < limit)
            yield return null;

        if (restartWholeLevelOnNoStrike)
        {
            ReloadScene();
        }
        else
        {
            if (_currentLaneIndex == lanes.Count - 1)
            {
                LastScore = _pinsKnockedOverall;
                SceneManager.LoadScene(mainSceneName);
            }
            else
                SetLane(_currentLaneIndex + 1, true);
        }
    }

    void HideTutorialAnim()
    {
        if (handImage != null)
            handImage.SetActive(false);
    }

    private IEnumerator PopZoom()
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
        _popRoutine = null;
    }
    public void PlaySound(AudioClip clip)
    {
        if (pointSource != null && clip != null)
            pointSource.PlayOneShot(clip);
    }
    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
