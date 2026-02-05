using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [SerializeField] int maxSamples = 128;

    [Header("Mapping")]
    [SerializeField] float pixelsToWorld = 0.0025f;
    [SerializeField] bool invertForward = false;

    [Header("Viz")]
    [SerializeField] bool drawWorldPath = true;
    [SerializeField] LineRenderer worldPathLine;
    [SerializeField] LineRenderer worldFitLine;
    [SerializeField] float worldLineUpOffset = 0.02f;

    [Header("Launch")]
    [SerializeField] float speedScale = 0.02f;
    [SerializeField] float minSpeed = 4f;
    [SerializeField] float maxSpeed = 14f;

    [Header("Facing")]
    [SerializeField] Transform faceTarget;

    [Header("Spin from second-half CURVE (bend)")]
    [Tooltip("Max angular velocity magnitude applied from curve (radians/sec).")]
    [SerializeField] float maxSpinAroundVelocityRad = 30f;

    [Tooltip("How much bend angle (in degrees) maps to curve=1. Bigger = less sensitive.")]
    [SerializeField] float curveMaxBendAngleDeg = 35f;

    [Tooltip("Ignore tiny bend (0..1 after normalization). Higher = less sensitive.")]
    [SerializeField] float curveDeadzone = 0.10f;

    [Tooltip("Nonlinear response. >1 makes subtle curves weaker and big curves stronger.")]
    [SerializeField] float curveExponent = 2.2f;

    [Tooltip("If false, curve is absolute (always curves same direction).")]
    [SerializeField] bool useSignedCurve = true;

    [Header("Logs")]
    [SerializeField] bool debugLogs = true;

    InputActionMap map;
    InputAction pressAction;
    InputAction positionAction;

    struct Sample { public Vector2 p; public float t; public Sample(Vector2 p, float t) { this.p = p; this.t = t; } }

    readonly List<Sample> samples = new(256);
    readonly List<Vector3> worldPath = new(256);

    bool dragging;
    float swipeStartTime;
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
        Debug.Log(Camera.main);
        camMove = cam.GetComponent<CameraMovement>();
        ResolveActions();
        EnsureLines();
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

        if (debugLogs)
            Debug.Log($"[SwipeViz] Enabled. Map='{actionMapName}', Press='{pressActionName}', Position='{positionActionName}'.");
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
            Debug.LogError("[SwipeViz] InputActionAsset is null.");
            return;
        }

        map = actions.FindActionMap(actionMapName, false);
        if (map == null)
        {
            Debug.LogError($"[SwipeViz] ActionMap '{actionMapName}' not found in '{actions.name}'.");
            return;
        }

        pressAction = map.FindAction(pressActionName, false);
        positionAction = map.FindAction(positionActionName, false);

        if (pressAction == null) Debug.LogError($"[SwipeViz] Press action '{pressActionName}' not found in map '{actionMapName}'.");
        if (positionAction == null) Debug.LogError($"[SwipeViz] Position action '{positionActionName}' not found in map '{actionMapName}'.");
    }

    void EnsureLines()
    {
        if (!worldPathLine)
        {
            var go = new GameObject("WorldSwipeCurve");
            go.transform.SetParent(transform, false);
            worldPathLine = go.AddComponent<LineRenderer>();
            InitLine(worldPathLine, 0.03f);
        }

        if (!worldFitLine)
        {
            var go = new GameObject("WorldSwipeFit");
            go.transform.SetParent(transform, false);
            worldFitLine = go.AddComponent<LineRenderer>();
            InitLine(worldFitLine, 0.05f);
        }
    }

    void InitLine(LineRenderer lr, float width)
    {
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.widthMultiplier = width;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.material = new Material(Shader.Find("Sprites/Default"));
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
            if (debugLogs) Debug.Log($"[SwipeViz] Record complete, samples={samples.Count}");
            return;
        }

        Vector2 p = positionAction.ReadValue<Vector2>();
        samples.Add(new Sample(p, now));

        if (samples.Count > maxSamples)
            samples.RemoveRange(0, samples.Count - maxSamples);

        RecomputeViz();
    }

    void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (positionAction == null || rb.linearVelocity != Vector3.zero) return;

        dragging = true;
        recordComplete = false;

        samples.Clear();
        worldPath.Clear();

        swipeStartTime = Time.unscaledTime;
        Vector2 start = positionAction.ReadValue<Vector2>();

        RecomputeViz();

        if (debugLogs) Debug.Log($"[SwipeViz] START {start}");
    }

    void OnPressCanceled(InputAction.CallbackContext ctx)
    {
        if (!dragging) return;
        dragging = false;

        if (debugLogs)
            Debug.Log($"[SwipeViz] END samples={samples.Count}");

        if (samples.Count < 2 || !rb) return;

        RecomputeViz();

        Vector3 worldDir = ScreenDirToWorld(fitDir);
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 1e-6f) return;
        worldDir.Normalize();

        float swipeLenPx = (fitB - fitA).magnitude;
        float speed = Mathf.Clamp(
            swipeLenPx * pixelsToWorld / Mathf.Max(0.001f, maxRecordSeconds) * speedScale,
            minSpeed, maxSpeed
        );

        // SECOND HALF (by SCREEN Y distance): compute "bend" not "offset"
        float bend01 = ComputeSecondHalfBend01_ByScreenYHalf(
            samples, fitOrigin, fitDir,
            curveMaxBendAngleDeg, curveDeadzone, curveExponent
        );

        float signedBend = useSignedCurve ? bend01 : Mathf.Abs(bend01);
        float spin = signedBend * maxSpinAroundVelocityRad;

        Vector3 spinAxis = faceTarget ? faceTarget.forward : transform.forward;
        spinAxis.y = 0f;
        if (spinAxis.sqrMagnitude < 1e-6f) spinAxis = transform.forward;
        spinAxis.Normalize();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.linearVelocity = worldDir * speed;
        rb.angularVelocity = spinAxis * spin;

        if (debugLogs)
            Debug.Log($"[SwipeViz] Launch velDir={worldDir} speed={speed:0.00} bend01={bend01:0.000} spinAxis={spinAxis} spin={spin:0.00}");

        GameManager.Instance.OnBallThrown(15f);
        CallWinCheck();
    }

    void CallWinCheck()
    {
        GameManager.Instance.StartWinChecking();
    }

    void RecomputeViz()
    {
        // FIRST HALF (by SCREEN Y distance): fit direction
        ComputeFitFromFirstHalf_ByScreenYHalf(samples, out fitOrigin, out fitDir, out fitA, out fitB);

        if (!drawWorldPath) return;

        if (BuildWorldPathFromSamples(samples, worldPath))
            UpdateLine(worldPathLine, worldPath);
        else
            worldPathLine.positionCount = 0;

        if (BuildWorldLineFromFit(fitA, fitB, out var a, out var b))
            UpdateLine2(worldFitLine, a, b);
        else
            worldFitLine.positionCount = 0;
    }

    // ---------- NEW: split by SCREEN Y distance ----------

    // Returns the screen Y midpoint for "half distance" and the swipe direction sign (up or down).
    static bool TryGetScreenYHalfMid(List<Sample> s, out float midY, out bool goingUp)
    {
        midY = 0f;
        goingUp = true;
        if (s == null || s.Count < 2) return false;

        float y0 = s[0].p.y;
        float y1 = s[s.Count - 1].p.y;
        float dy = y1 - y0;

        if (Mathf.Abs(dy) < 1e-3f) return false;

        goingUp = dy > 0f;
        midY = y0 + dy * 0.5f; // halfway by Y distance
        return true;
    }

    static void ComputeFitFromFirstHalf_ByScreenYHalf(List<Sample> s, out Vector2 origin, out Vector2 dir, out Vector2 a, out Vector2 b)
    {
        origin = Vector2.zero;
        dir = Vector2.up;
        a = b = Vector2.zero;

        if (s == null || s.Count < 2) return;

        // If we can't split by Y distance, fall back to old count-based behavior.
        if (!TryGetScreenYHalfMid(s, out float midY, out bool goingUp))
        {
            ComputeFitFromFirstHalf_Count(s, out origin, out dir, out a, out b);
            return;
        }

        // Collect points in the first half by Y distance.
        // If swiping up: y <= midY is "first half".
        // If swiping down: y >= midY is "first half".
        List<Vector2> pts = new List<Vector2>(s.Count);
        for (int i = 0; i < s.Count; i++)
        {
            float y = s[i].p.y;
            bool inFirstHalf = goingUp ? (y <= midY) : (y >= midY);
            if (inFirstHalf) pts.Add(s[i].p);
        }

        if (pts.Count < 2)
        {
            // fallback: at least 2 points
            pts.Clear();
            pts.Add(s[0].p);
            pts.Add(s[1].p);
        }

        // PCA line fit on pts
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

        // Point the direction from start toward end (overall swipe direction)
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

    // SECOND HALF bend metric by Y distance
    static float ComputeSecondHalfBend01_ByScreenYHalf(
        List<Sample> s, Vector2 origin, Vector2 dir,
        float maxBendAngleDeg, float deadzone01, float exponent)
    {
        if (s == null || s.Count < 6) return 0f;
        if (dir.sqrMagnitude < 1e-6f) return 0f;

        // If we can't split by Y distance, fall back to old "second half by count".
        if (!TryGetScreenYHalfMid(s, out float midY, out bool goingUp))
            return ComputeSecondHalfBend01_ByCountHalf(s, origin, dir, maxBendAngleDeg, deadzone01, exponent);

        dir.Normalize();
        Vector2 perp = new(-dir.y, dir.x);

        // Gather second half points (by Y distance).
        // If swiping up: y > midY is "second half".
        // If swiping down: y < midY is "second half".
        List<Vector2> pts = new List<Vector2>(s.Count);
        for (int i = 0; i < s.Count; i++)
        {
            float y = s[i].p.y;
            bool inSecondHalf = goingUp ? (y > midY) : (y < midY);
            if (inSecondHalf) pts.Add(s[i].p);
        }

        if (pts.Count < 3) return 0f;

        // Build u (along) and v (lateral) for regression.
        float meanU = 0f, meanV = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 d = pts[i] - origin;
            float u = Vector2.Dot(d, dir);
            float v = Vector2.Dot(d, perp);
            meanU += u;
            meanV += v;
        }
        meanU /= pts.Count;
        meanV /= pts.Count;

        float suu = 0f;
        float suv = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 d = pts[i] - origin;
            float u = Vector2.Dot(d, dir) - meanU;
            float v = Vector2.Dot(d, perp) - meanV;
            suu += u * u;
            suv += u * v;
        }

        if (suu < 1e-4f) return 0f;

        // slope m = dv/du, then bend angle = atan(m)
        float m = suv / suu;
        float bendRad = Mathf.Atan(m);

        float maxRad = Mathf.Max(0.001f, maxBendAngleDeg * Mathf.Deg2Rad);
        float norm = Mathf.Clamp(bendRad / maxRad, -1f, 1f);

        // Deadzone + nonlinear ramp
        float a = Mathf.Abs(norm);
        float dz = Mathf.Clamp01(deadzone01);
        if (a <= dz) return 0f;

        float x = Mathf.InverseLerp(dz, 1f, a);
        x = Mathf.SmoothStep(0f, 1f, x);
        x = Mathf.Pow(x, Mathf.Max(0.01f, exponent));

        return Mathf.Sign(norm) * x;
    }

    // ---------- FALLBACKS (count based) ----------
    static void ComputeFitFromFirstHalf_Count(List<Sample> s, out Vector2 origin, out Vector2 dir, out Vector2 a, out Vector2 b)
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

    static float ComputeSecondHalfBend01_ByCountHalf(
        List<Sample> s, Vector2 origin, Vector2 dir,
        float maxBendAngleDeg, float deadzone01, float exponent)
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
            float u = Vector2.Dot(d, dir);
            float v = Vector2.Dot(d, perp);
            meanU += u;
            meanV += v;
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

        float maxRad = Mathf.Max(0.001f, maxBendAngleDeg * Mathf.Deg2Rad);
        float norm = Mathf.Clamp(bendRad / maxRad, -1f, 1f);

        float a = Mathf.Abs(norm);
        float dz = Mathf.Clamp01(deadzone01);
        if (a <= dz) return 0f;

        float x = Mathf.InverseLerp(dz, 1f, a);
        x = Mathf.SmoothStep(0f, 1f, x);
        x = Mathf.Pow(x, Mathf.Max(0.01f, exponent));

        return Mathf.Sign(norm) * x;
    }

    // ---------- existing world mapping / viz ----------

    bool BuildWorldPathFromSamples(List<Sample> s, List<Vector3> outPath)
    {
        outPath.Clear();
        if (s == null || s.Count < 2) return false;
        if (!cam) cam = Camera.main;
        if (!cam) return false;

        Vector2 p0 = s[0].p;

        Vector3 right = cam.transform.right; right.y = 0f;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        right.Normalize();

        Vector3 forward = cam.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        forward.Normalize();

        float sign = invertForward ? -1f : 1f;

        Vector3 ballPos = rb ? rb.position : transform.position;
        ballPos.y = laneY;

        for (int i = 0; i < s.Count; i++)
        {
            Vector2 d = s[i].p - p0;
            float dx = d.x * pixelsToWorld;
            float dz = d.y * pixelsToWorld * sign;
            Vector3 w = ballPos + right * dx + forward * dz;
            w.y = laneY;
            outPath.Add(w);
        }

        RemoveNearDuplicates(outPath, 0.01f);
        return outPath.Count >= 2;
    }

    bool BuildWorldLineFromFit(Vector2 fitScreenA, Vector2 fitScreenB, out Vector3 a, out Vector3 b)
    {
        a = b = Vector3.zero;
        if (!rb) return false;

        Vector2 p0 = samples.Count > 0 ? samples[0].p : fitScreenA;
        Vector3 basePos = rb.position; basePos.y = laneY;

        a = basePos + ScreenDeltaToWorld(fitScreenA - p0);
        b = basePos + ScreenDeltaToWorld(fitScreenB - p0);
        a.y = b.y = laneY;
        return true;
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

    void UpdateLine(LineRenderer lr, List<Vector3> path)
    {
        if (!lr) return;
        lr.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
            lr.SetPosition(i, path[i] + Vector3.up * worldLineUpOffset);
    }

    void UpdateLine2(LineRenderer lr, Vector3 a, Vector3 b)
    {
        if (!lr) return;
        lr.positionCount = 2;
        lr.SetPosition(0, a + Vector3.up * worldLineUpOffset);
        lr.SetPosition(1, b + Vector3.up * worldLineUpOffset);
    }

    static void RemoveNearDuplicates(List<Vector3> pts, float minDist)
    {
        if (pts.Count < 2) return;
        float minSqr = minDist * minDist;
        int write = 1;
        Vector3 last = pts[0];

        for (int i = 1; i < pts.Count; i++)
        {
            if ((pts[i] - last).sqrMagnitude >= minSqr)
            {
                pts[write++] = pts[i];
                last = pts[i];
            }
        }

        if (write < pts.Count)
            pts.RemoveRange(write, pts.Count - write);
    }

    void HideTutorialAnim()
    {
        if (handImage != null)
            handImage.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Debug.DrawRay(transform.position, transform.forward, Color.red);
    }

#if UNITY_EDITOR
    static Vector2 ToGUI(Vector2 inputScreen) => new(inputScreen.x, Screen.height - inputScreen.y);

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (samples.Count < 2) return;

        Handles.BeginGUI();

        Handles.color = Color.white;
        for (int i = 1; i < samples.Count; i++)
            Handles.DrawAAPolyLine(3f, ToGUI(samples[i - 1].p), ToGUI(samples[i].p));

        Handles.color = Color.yellow;
        Handles.DrawAAPolyLine(4f, ToGUI(fitA), ToGUI(fitB));

        Handles.EndGUI();
    }
#endif
}
