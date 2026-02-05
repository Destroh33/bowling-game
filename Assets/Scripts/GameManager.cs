using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

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

    private readonly List<BowlingPinController> _activePins = new List<BowlingPinController>();

    private int _currentLaneIndex = 0;

    private bool _startWinCheck = false;
    private bool _strike = false;
    private bool _cachedStrike = false;

    private int _pinsKnockedThisLane = 0;

    private Coroutine _winZoomRoutine;
    private Coroutine _laneTimerRoutine;
    private Coroutine _nextLaneRoutine;

    private GameObject _currentBallInstance;
    private Rigidbody _ballRb;
    private bool firstShot = true; 
    [SerializeField] private GameObject handImage;
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
    }

    void Start()
    {
        if (lanes != null && lanes.Count > 0)
            SetLane(0, true);
    }

    void Update()
    {
        if (!_startWinCheck)
            return;

        _strike = KnockedCheck();

        if (pinsKnockedText != null)
            pinsKnockedText.text = $"Lane {_currentLaneIndex + 1}/{Mathf.Max(1, lanes.Count)}  Pins Knocked: {_pinsKnockedThisLane}/10";

        if (_strike != _cachedStrike)
        {
            _cachedStrike = _strike;
            if (_strike)
                OnStrike();
        }
    }

    public void StartWinChecking()
    {
        _startWinCheck = true;
        _strike = false;
        _cachedStrike = false;
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
        _strike = false;
        _cachedStrike = false;

        if (_currentBallInstance != null)
            Destroy(_currentBallInstance);

        _currentBallInstance = null;
        _ballRb = null;

        var lane = lanes[_currentLaneIndex];

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
            _activePins.AddRange(pinsRoot.GetComponentsInChildren<BowlingPinController>(true));

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

        if (_startWinCheck)
            StartLaneTimer();
    }

    private void OnStrike()
    {
        StopLaneTimer();

        if (winText != null)
        {
            winText.enabled = true;
            if (_winZoomRoutine != null) StopCoroutine(_winZoomRoutine);
            _winZoomRoutine = StartCoroutine(WinZoomIn());
        }

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
            ReloadScene();
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
            RestartCurrentLane();
        }
    }

    private void RestartCurrentLane()
    {
        StopLaneTimer();

        _strike = false;
        _cachedStrike = false;

        if (_nextLaneRoutine != null) { StopCoroutine(_nextLaneRoutine); _nextLaneRoutine = null; }

        SetLane(_currentLaneIndex, true);

        if (_startWinCheck)
            StartLaneTimer();
    }
    void HideTutorialAnim()
    {
        handImage.SetActive(false);
    }

    public bool KnockedCheck()
    {
        if (_activePins == null || _activePins.Count == 0)
            return false;

        return _activePins.TrueForAll(pin => pin != null && pin.isKnocked);
    }

    public void AddKnock()
    {
        _pinsKnockedThisLane++;
    }

    private IEnumerator WinZoomIn()
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
        _winZoomRoutine = null;
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
