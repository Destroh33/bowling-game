using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class BowlingSwipe_ScreenAndWorldViz : MonoBehaviour
{
    [Header("Input (InputActionAsset)")]
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string pressActionName = "Press";
    [SerializeField] private string positionActionName = "Position";
    [SerializeField] private bool enableActionMapOnEnable = true;

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Camera cam;

    [Header("Lane (kept)")]
    [SerializeField] private LayerMask laneMask;

    [Header("Gesture sampling")]
    [Tooltip("How long we record samples after press starts. After this, we stop sampling until release.")]
    [SerializeField] private float maxRecordSeconds = 0.35f;

    [Tooltip("Clamp sample count so the polyline stays manageable.")]
    [SerializeField] private int maxSamples = 128;

    [Header("World mapping: 'faceplant' rotation")]
    [Tooltip("Pixels -> world units scaling for the rotated world path.")]
    [SerializeField] private float pixelsToWorld = 0.0025f;

    [Tooltip("If swipe feels backwards (up swipe goes behind), enable this.")]
    [SerializeField] private bool invertForward = false;

    [Tooltip("World path y value (lane is flat).")]
    [SerializeField] private float laneY = 0f;

    [Header("World Path Debug")]
    [SerializeField] private bool drawWorldPath = true;
    [SerializeField] private LineRenderer worldPathLine;
    [SerializeField] private float worldPathUpOffset = 0.02f;

    [Header("Logs")]
    [SerializeField] private bool debugLogs = true;

    // Input
    private InputActionMap map;
    private InputAction pressAction;
    private InputAction positionAction;

    private struct Sample
    {
        public Vector2 screen;  // Input System screen coords (0,0 bottom-left)
        public float t;
        public Sample(Vector2 s, float time) { screen = s; t = time; }
    }

    private readonly List<Sample> samples = new List<Sample>(256);
    private readonly List<Vector3> worldPath = new List<Vector3>(256);

    // Swipe state
    private bool dragging;
    private float swipeStartTime;
    private bool recordComplete;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
    }

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!cam) cam = Camera.main;
        ResolveActions();
        EnsureWorldLine();
    }

    private void OnEnable()
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

    private void OnDisable()
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

    private void ResolveActions()
    {
        map = null; pressAction = null; positionAction = null;

        if (actions == null)
        {
            Debug.LogError("[SwipeViz] InputActionAsset is null. Assign it.");
            return;
        }

        map = actions.FindActionMap(actionMapName, false);
        if (map == null)
        {
            Debug.LogError($"[SwipeViz] ActionMap '{actionMapName}' not found in asset '{actions.name}'.");
            return;
        }

        pressAction = map.FindAction(pressActionName, false);
        positionAction = map.FindAction(positionActionName, false);

        if (pressAction == null) Debug.LogError($"[SwipeViz] Press action '{pressActionName}' not found in map '{actionMapName}'.");
        if (positionAction == null) Debug.LogError($"[SwipeViz] Position action '{positionActionName}' not found in map '{actionMapName}'.");
    }

    private void EnsureWorldLine()
    {
        if (worldPathLine != null) return;

        var go = new GameObject("WorldRotatedSwipeLine");
        go.transform.SetParent(transform, false);

        worldPathLine = go.AddComponent<LineRenderer>();
        worldPathLine.useWorldSpace = true;
        worldPathLine.positionCount = 0;
        worldPathLine.widthMultiplier = 0.03f;
        worldPathLine.numCornerVertices = 4;
        worldPathLine.numCapVertices = 4;
        worldPathLine.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
        if (!dragging || recordComplete || positionAction == null) return;

        float now = Time.unscaledTime;

        // stop sampling after maxRecordSeconds
        if (now - swipeStartTime >= maxRecordSeconds)
        {
            recordComplete = true;
            if (debugLogs) Debug.Log($"[SwipeViz] Record complete (>{maxRecordSeconds:0.###}s), samples={samples.Count}");
            return;
        }

        Vector2 p = positionAction.ReadValue<Vector2>();
        samples.Add(new Sample(p, now));

        if (samples.Count > maxSamples)
            samples.RemoveRange(0, samples.Count - maxSamples);

        // Update world path in real-time while dragging
        if (drawWorldPath)
        {
            if (BuildWorldPathFromSamples_Faceplant(samples, worldPath))
                UpdateWorldLine(worldPath);
        }
    }

    private void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (positionAction == null) return;

        dragging = true;
        recordComplete = false;

        samples.Clear();
        worldPath.Clear();

        swipeStartTime = Time.unscaledTime;

        Vector2 start = positionAction.ReadValue<Vector2>();
        samples.Add(new Sample(start, swipeStartTime));

        if (debugLogs) Debug.Log($"[SwipeViz] START {start}");

        if (drawWorldPath)
        {
            if (BuildWorldPathFromSamples_Faceplant(samples, worldPath))
                UpdateWorldLine(worldPath);
            else
                worldPathLine.positionCount = 0;
        }
    }

    private void OnPressCanceled(InputAction.CallbackContext ctx)
    {
        if (!dragging) return;
        dragging = false;

        if (debugLogs)
        {
            Vector2 end = positionAction != null ? positionAction.ReadValue<Vector2>() : Vector2.zero;
            Debug.Log($"[SwipeViz] END {end} samples={samples.Count}");
        }

        // Keep the last stroke visible after release.
        // If you want to clear immediately on release, uncomment:
        // samples.Clear();
        // worldPath.Clear();
        // if (worldPathLine) worldPathLine.positionCount = 0;
    }

    // ---- World mapping: rotate screen curve about camera.right so it "faceplants" onto the ground plane ----
    // Screen X stays camera-right. Screen Y becomes camera-forward. Then we anchor to ball and force y=laneY.
    private bool BuildWorldPathFromSamples_Faceplant(List<Sample> s, List<Vector3> outPath)
    {
        outPath.Clear();
        if (s == null || s.Count < 2) return false;

        if (!cam) cam = Camera.main;
        if (!cam) return false;

        Vector2 p0 = s[0].screen;

        // Use camera axes, but flatten onto the ground so camera pitch doesn't affect mapping
        Vector3 right = cam.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        right.Normalize();

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        forward.Normalize();

        float sign = invertForward ? -1f : 1f;

        Vector3 ballPos = rb ? rb.position : transform.position;
        ballPos.y = laneY;

        for (int i = 0; i < s.Count; i++)
        {
            Vector2 d = s[i].screen - p0; // pixels relative to start
            float dx = d.x * pixelsToWorld;
            float dz = d.y * pixelsToWorld * sign;

            Vector3 w = ballPos + right * dx + forward * dz;
            w.y = laneY;
            outPath.Add(w);
        }

        RemoveNearDuplicates(outPath, 0.01f);
        return outPath.Count >= 2;
    }

    private void UpdateWorldLine(List<Vector3> path)
    {
        if (!worldPathLine) return;

        worldPathLine.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
            worldPathLine.SetPosition(i, path[i] + Vector3.up * worldPathUpOffset);
    }

    private static void RemoveNearDuplicates(List<Vector3> pts, float minDist)
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

#if UNITY_EDITOR
    // InputSystem screen coords: (0,0) bottom-left
    // OnGUI/Handles GUI coords: (0,0) top-left
    private static Vector2 ToGUI(Vector2 inputSystemScreenPos)
    {
        return new Vector2(inputSystemScreenPos.x, Screen.height - inputSystemScreenPos.y);
    }

    // Draw the original screen-space stroke as before
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (samples.Count < 2) return;

        Handles.BeginGUI();

        Handles.color = Color.white;
        for (int i = 1; i < samples.Count; i++)
        {
            Vector2 a = ToGUI(samples[i - 1].screen);
            Vector2 b = ToGUI(samples[i].screen);
            Handles.DrawAAPolyLine(3.0f, a, b);
        }

        Handles.EndGUI();
    }
#endif
}
