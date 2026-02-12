using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class BowlingSwipeController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] InputActionAsset actions;
    [SerializeField] string actionMapName = "Player";
    [SerializeField] string pressActionName = "Press";
    [SerializeField] string positionActionName = "Position";
    [SerializeField] bool enableActionMapOnEnable = true;

    [Header("Refs")]
    [SerializeField] Rigidbody rb;
    [SerializeField] Camera cam;

    [Header("Lane")]
    [SerializeField] LayerMask laneMask;
    [SerializeField] float laneY = 0f;

    [Header("Sampling")]
    [SerializeField] float maxRecordSeconds = 0.35f;
    [SerializeField] float sampleInterval = 0.01f;
    [SerializeField] int maxSamples = 128;

    [Header("Reference Screen")]
    [SerializeField] float refWidth = 666f;
    [SerializeField] float refHeight = 441f;

    [Header("Mapping")]
    [SerializeField] float pixelsToWorld = 0.0025f;
    [SerializeField] bool invertForward = false;

    [Header("Launch")]
    [SerializeField] float speedScale = 0.02f;
    [SerializeField] float minSpeed = 4f;
    [SerializeField] float maxSpeed = 14f;

    [Header("Facing")]
    [SerializeField] Transform faceTarget;

    [Header("Spin")]
    [SerializeField] float maxSpinAroundVelocityRad = 30f;
    [SerializeField] float bendMaxDeg = 35f;
    [SerializeField] float bendDeadzone = 0.10f;
    [SerializeField] float bendExponent = 2.2f;
    [SerializeField] bool useSignedBend = true;

    [Header("Logs")]
    [SerializeField] bool debugLogs = true;

    InputActionMap map;
    InputAction pressAction;
    InputAction positionAction;

    struct Sample { public Vector2 p; public float t; public Sample(Vector2 p, float t) { this.p = p; this.t = t; } }

    readonly List<Sample> samples = new(256);

    bool dragging;
    float swipeStartTime;
    float lastSampleTime;
    bool recordComplete;

    Vector2 fitOrigin;
    Vector2 fitDir;
    Vector2 fitA, fitB;

    [SerializeField] CameraMovement camMove;

    [SerializeField] AudioSource source;

    [Header("Rolling Audio")]
    [SerializeField] float rollMinSpeed = 0.15f;
    [SerializeField] float rollMaxSpeed = 10f;
    [SerializeField] float rollMinVolume = 0.05f;
    [SerializeField] float rollMaxVolume = 0.6f;
    [SerializeField] float rollVolumeLerp = 12f;

    bool onLane;

    private bool firstShot = true;
    [SerializeField] GameObject handImage;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!cam) cam = Camera.main;

        camMove = cam ? cam.GetComponent<CameraMovement>() : null;

        ResolveActions();

        if (!faceTarget) faceTarget = rb ? rb.transform : transform;
        rb.maxAngularVelocity = 50000f;

        if (source != null)
            source.volume = 0f;
    }

    void OnEnable()
    {
        ResolveActions();

        if (map != null && enableActionMapOnEnable) map.Enable();
        else { pressAction?.Enable(); positionAction?.Enable(); }

        if (pressAction != null)
        {
            pressAction.started += OnPressStarted;
            pressAction.canceled += OnPressCanceled;
        }
    }

    void OnDisable()
    {
        if (pressAction != null)
        {
            pressAction.started -= OnPressStarted;
            pressAction.canceled -= OnPressCanceled;
        }

        map?.Disable();
        pressAction?.Disable();
        positionAction?.Disable();
    }

    void ResolveActions()
    {
        map = null; pressAction = null; positionAction = null;

        if (!actions)
        {
            Debug.LogError("[Swipe] InputActionAsset is null.");
            return;
        }

        map = actions.FindActionMap(actionMapName, false);
        if (map == null)
        {
            Debug.LogError($"[Swipe] ActionMap '{actionMapName}' not found in '{actions.name}'.");
            return;
        }

        pressAction = map.FindAction(pressActionName, false);
        positionAction = map.FindAction(positionActionName, false);

        if (pressAction == null) Debug.LogError($"[Swipe] Press action '{pressActionName}' not found in map '{actionMapName}'.");
        if (positionAction == null) Debug.LogError($"[Swipe] Position action '{positionActionName}' not found in map '{actionMapName}'.");
    }

    private void OnCollisionStay(Collision collision)
    {
        if (LayerMask.LayerToName(collision.gameObject.layer) == "Lane")
        {
            onLane = true;

            if (source != null && !source.isPlaying)
                source.Play();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (LayerMask.LayerToName(collision.gameObject.layer) == "Lane")
        {
            onLane = false;

            if (source != null && source.isPlaying)
                source.Stop();
        }
    }

    void LateUpdate()
    {
        if (source == null || rb == null) return;

        if (!onLane)
        {
            source.volume = Mathf.Lerp(source.volume, 0f, rollVolumeLerp * Time.deltaTime);
            return;
        }

        Vector2 planar = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        float spd = planar.magnitude;

        float t = Mathf.InverseLerp(rollMinSpeed, rollMaxSpeed, spd);
        float targetVol = Mathf.Lerp(rollMinVolume, rollMaxVolume, t);

        source.volume = Mathf.Lerp(source.volume, targetVol, rollVolumeLerp * Time.deltaTime);

        if (spd < rollMinSpeed * 0.5f)
            source.volume = Mathf.Lerp(source.volume, 0f, rollVolumeLerp * Time.deltaTime);
    }

    void Update()
    {
        if (!rb) return;
        if (!dragging || recordComplete || positionAction == null) return;

        float now = Time.unscaledTime;

        if (now - swipeStartTime >= maxRecordSeconds)
        {
            recordComplete = true;
            if (debugLogs) Debug.Log($"[Swipe] Record complete, samples={samples.Count}");
            return;
        }

        float dt = now - lastSampleTime;
        if (dt < Mathf.Max(0.0001f, sampleInterval))
            return;

        Vector2 p = ReadPosRef();
        samples.Add(new Sample(p, now));
        lastSampleTime = now;

        if (samples.Count > maxSamples)
            samples.RemoveRange(0, samples.Count - maxSamples);

        Refit();
    }

    void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (positionAction == null || rb.linearVelocity != Vector3.zero) return;

        dragging = true;
        recordComplete = false;

        samples.Clear();

        swipeStartTime = Time.unscaledTime;
        lastSampleTime = swipeStartTime - 999f;

        Vector2 start = ReadPosRef();
        samples.Add(new Sample(start, swipeStartTime));

        Refit();
    }

    void OnPressCanceled(InputAction.CallbackContext ctx)
    {
        if (!dragging) return;
        dragging = false;

        if (samples.Count < 2 || !rb) return;

        Refit();

        Vector3 worldDir = ScreenDirToWorld(fitDir);
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 1e-6f) return;
        worldDir.Normalize();

        float swipeLenPx = (fitB - fitA).magnitude;

        float dur = Time.unscaledTime - swipeStartTime;
        dur = Mathf.Clamp(dur, 0.03f, maxRecordSeconds);

        float speed = Mathf.Clamp(
            swipeLenPx * pixelsToWorld / dur * speedScale,
            minSpeed, maxSpeed
        );

        float bend01 = BendY(samples, fitOrigin, fitDir, bendMaxDeg, bendDeadzone, bendExponent);
        float signedB = useSignedBend ? bend01 : Mathf.Abs(bend01);
        float spin = signedB * maxSpinAroundVelocityRad;

        Vector3 spinAxis = faceTarget ? faceTarget.forward : transform.forward;
        spinAxis.y = 0f;
        if (spinAxis.sqrMagnitude < 1e-6f) spinAxis = transform.forward;
        spinAxis.Normalize();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.linearVelocity = worldDir * speed;
        rb.angularVelocity = spinAxis * spin;

        if (debugLogs)
            Debug.Log($"[Swipe] dur={dur:0.000} speed={speed:0.00} bend01={bend01:0.000} spin={spin:0.00}");

        GameManager.Instance.OnBallThrown(15f);
        GameManager.Instance.StartWinChecking();
    }

    Vector2 ReadPosRef()
    {
        Vector2 p = positionAction.ReadValue<Vector2>();
        float sw = Mathf.Max(1f, Screen.width);
        float sh = Mathf.Max(1f, Screen.height);
        float sx = refWidth / sw;
        float sy = refHeight / sh;
        return new Vector2(p.x * sx, p.y * sy);
    }

    void Refit()
    {
        FitY(samples, out fitOrigin, out fitDir, out fitA, out fitB);
    }

    static bool HalfY(List<Sample> s, out float midY, out bool up)
    {
        midY = 0f;
        up = true;
        if (s == null || s.Count < 2) return false;

        float y0 = s[0].p.y;
        float y1 = s[s.Count - 1].p.y;
        float dy = y1 - y0;

        if (Mathf.Abs(dy) < 1e-3f) return false;

        up = dy > 0f;
        midY = y0 + dy * 0.5f;
        return true;
    }

    static void FitY(List<Sample> s, out Vector2 origin, out Vector2 dir, out Vector2 a, out Vector2 b)
    {
        origin = Vector2.zero;
        dir = Vector2.up;
        a = b = Vector2.zero;

        if (s == null || s.Count < 2) return;

        if (!HalfY(s, out float midY, out bool up))
        {
            FitCount(s, out origin, out dir, out a, out b);
            return;
        }

        List<Vector2> pts = new List<Vector2>(s.Count);
        for (int i = 0; i < s.Count; i++)
        {
            float y = s[i].p.y;
            bool first = up ? (y <= midY) : (y >= midY);
            if (first) pts.Add(s[i].p);
        }

        if (pts.Count < 2)
        {
            pts.Clear();
            pts.Add(s[0].p);
            pts.Add(s[1].p);
        }

        Vector2 mean = Vector2.zero;
        for (int i = 0; i < pts.Count; i++) mean += pts[i];
        mean /= pts.Count;

        float sxx = 0f, syy = 0f, sxy = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 d = pts[i] - mean;
            sxx += d.x * d.x;
            syy += d.y * d.y;
            sxy += d.x * d.y;
        }

        float ang = 0.5f * Mathf.Atan2(2f * sxy, sxx - syy);
        Vector2 v = new(Mathf.Cos(ang), Mathf.Sin(ang));

        Vector2 overall = s[s.Count - 1].p - s[0].p;
        if (Vector2.Dot(v, overall) < 0f) v = -v;

        origin = mean;
        dir = v;

        float minT = float.PositiveInfinity, maxT = float.NegativeInfinity;
        for (int i = 0; i < pts.Count; i++)
        {
            float t = Vector2.Dot(pts[i] - mean, v);
            if (t < minT) minT = t;
            if (t > maxT) maxT = t;
        }

        a = mean + v * minT;
        b = mean + v * maxT;
    }

    static void FitCount(List<Sample> s, out Vector2 origin, out Vector2 dir, out Vector2 a, out Vector2 b)
    {
        origin = Vector2.zero;
        dir = Vector2.up;
        a = b = Vector2.zero;

        if (s == null || s.Count < 2) return;

        int n = Mathf.Max(2, s.Count / 2);

        Vector2 mean = Vector2.zero;
        for (int i = 0; i < n; i++) mean += s[i].p;
        mean /= n;

        float sxx = 0f, syy = 0f, sxy = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 d = s[i].p - mean;
            sxx += d.x * d.x;
            syy += d.y * d.y;
            sxy += d.x * d.y;
        }

        float ang = 0.5f * Mathf.Atan2(2f * sxy, sxx - syy);
        Vector2 v = new(Mathf.Cos(ang), Mathf.Sin(ang));
        if (Vector2.Dot(v, s[n - 1].p - s[0].p) < 0f) v = -v;

        origin = mean;
        dir = v;

        float minT = float.PositiveInfinity, maxT = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            float t = Vector2.Dot(s[i].p - mean, v);
            if (t < minT) minT = t;
            if (t > maxT) maxT = t;
        }

        a = mean + v * minT;
        b = mean + v * maxT;
    }

    static float BendY(List<Sample> s, Vector2 origin, Vector2 dir, float maxDeg, float dead01, float exponent)
    {
        if (s == null || s.Count < 6) return 0f;
        if (dir.sqrMagnitude < 1e-6f) return 0f;

        if (!HalfY(s, out float midY, out bool up))
            return BendCount(s, origin, dir, maxDeg, dead01, exponent);

        dir.Normalize();
        Vector2 perp = new(-dir.y, dir.x);

        List<Vector2> pts = new List<Vector2>(s.Count);
        for (int i = 0; i < s.Count; i++)
        {
            float y = s[i].p.y;
            bool second = up ? (y > midY) : (y < midY);
            if (second) pts.Add(s[i].p);
        }

        if (pts.Count < 3) return 0f;

        float meanU = 0f, meanV = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 d = pts[i] - origin;
            meanU += Vector2.Dot(d, dir);
            meanV += Vector2.Dot(d, perp);
        }
        meanU /= pts.Count;
        meanV /= pts.Count;

        float suu = 0f, suv = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 d = pts[i] - origin;
            float u = Vector2.Dot(d, dir) - meanU;
            float v = Vector2.Dot(d, perp) - meanV;
            suu += u * u;
            suv += u * v;
        }

        if (suu < 1e-4f) return 0f;

        float m = suv / suu;
        float bendRad = Mathf.Atan(m);

        float maxRad = Mathf.Max(0.001f, maxDeg * Mathf.Deg2Rad);
        float norm = Mathf.Clamp(bendRad / maxRad, -1f, 1f);

        float a = Mathf.Abs(norm);
        float dz = Mathf.Clamp01(dead01);
        if (a <= dz) return 0f;

        float x = Mathf.InverseLerp(dz, 1f, a);
        x = Mathf.SmoothStep(0f, 1f, x);
        x = Mathf.Pow(x, Mathf.Max(0.01f, exponent));

        return Mathf.Sign(norm) * x;
    }

    static float BendCount(List<Sample> s, Vector2 origin, Vector2 dir, float maxDeg, float dead01, float exponent)
    {
        if (s == null || s.Count < 6) return 0f;
        if (dir.sqrMagnitude < 1e-6f) return 0f;

        dir.Normalize();
        Vector2 perp = new(-dir.y, dir.x);

        int start = s.Count / 2;
        int n = s.Count - start;
        if (n < 3) return 0f;

        float meanU = 0f, meanV = 0f;
        for (int i = start; i < s.Count; i++)
        {
            Vector2 d = s[i].p - origin;
            meanU += Vector2.Dot(d, dir);
            meanV += Vector2.Dot(d, perp);
        }
        meanU /= n;
        meanV /= n;

        float suu = 0f, suv = 0f;
        for (int i = start; i < s.Count; i++)
        {
            Vector2 d = s[i].p - origin;
            float u = Vector2.Dot(d, dir) - meanU;
            float v = Vector2.Dot(d, perp) - meanV;
            suu += u * u;
            suv += u * v;
        }

        if (suu < 1e-4f) return 0f;

        float m = suv / suu;
        float bendRad = Mathf.Atan(m);

        float maxRad = Mathf.Max(0.001f, maxDeg * Mathf.Deg2Rad);
        float norm = Mathf.Clamp(bendRad / maxRad, -1f, 1f);

        float a = Mathf.Abs(norm);
        float dz = Mathf.Clamp01(dead01);
        if (a <= dz) return 0f;

        float x = Mathf.InverseLerp(dz, 1f, a);
        x = Mathf.SmoothStep(0f, 1f, x);
        x = Mathf.Pow(x, Mathf.Max(0.01f, exponent));

        return Mathf.Sign(norm) * x;
    }

    Vector3 ScreenDeltaToWorld(Vector2 d)
    {
        if (!cam) cam = Camera.main;
        if (!cam) return Vector3.zero;

        Vector3 right = cam.transform.right; right.y = 0f;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        right.Normalize();

        Vector3 forward = cam.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        forward.Normalize();

        float sign = invertForward ? -1f : 1f;

        float dx = d.x * pixelsToWorld;
        float dz = d.y * pixelsToWorld * sign;
        return right * dx + forward * dz;
    }

    Vector3 ScreenDirToWorld(Vector2 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) return Vector3.zero;
        return ScreenDeltaToWorld(dir.normalized);
    }
}
