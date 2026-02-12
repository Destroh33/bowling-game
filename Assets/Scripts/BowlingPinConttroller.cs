using UnityEngine;
public class BowlingPinController : MonoBehaviour
{
    [SerializeField] private int laneNumber = 0;

    [SerializeField] private AudioClip hitClip;
    [SerializeField] private float minImpact = 1.5f;
    [SerializeField] private float maxImpact = 12f;
    [Range(0f, 1f)]
    [SerializeField] private float minVolume = 0.08f;
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.35f;
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.25f;
    [SerializeField] private float cooldown = 0.04f;
    [SerializeField] private bool preferImpulse = true;

    private Vector3 originalRotation;
    private AudioSource audioSource;
    private float lastPlayTime;

    public bool isKnocked = false;

    public int LaneNumber => laneNumber;

    public void SetLaneNumber(int value)
    {
        laneNumber = Mathf.Max(0, value);
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.dopplerLevel = 0f;
    }

    private void Start()
    {
        originalRotation = transform.eulerAngles;
    }

    private void Update()
    {
        if (isKnocked)
            return;

        CheckKnocked();
    }

    private void CheckKnocked()
    {
        if (Mathf.Abs(transform.eulerAngles.x - originalRotation.x) > 10f)
        {
            isKnocked = true;
            GameManager.Instance.AddKnock(laneNumber);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Ground")
            return;

        if (hitClip == null || audioSource == null)
            return;

        if (Time.time - lastPlayTime < cooldown)
            return;

        float impact = 0f;

        if (preferImpulse && collision.impulse.sqrMagnitude > 0.0001f)
            impact = collision.impulse.magnitude;
        else
            impact = collision.relativeVelocity.magnitude;

        if (impact < minImpact)
            return;

        float t = Mathf.InverseLerp(minImpact, maxImpact, impact);
        t = Mathf.Pow(t, 0.65f);

        float volume = Mathf.Lerp(minVolume, maxVolume, t);
        float pitch = Mathf.Lerp(minPitch, maxPitch, t);

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(hitClip, volume);

        lastPlayTime = Time.time;
    }
}
