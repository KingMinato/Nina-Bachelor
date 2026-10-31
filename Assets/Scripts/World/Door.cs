using UnityEngine;

// Door between two rooms. Sits on the hinge, the door leaf is a child.
public class Door : MonoBehaviour
{
    [SerializeField] float openAngle = 100f;
    [SerializeField] float openSeconds = 1.2f;

    Quaternion closed;

    void Awake() => closed = transform.localRotation;

    public Awaitable Open() => Swing(closed * Quaternion.Euler(0f, openAngle, 0f));

    public Awaitable Close() => Swing(closed);

    async Awaitable Swing(Quaternion to)
    {
        UiSfx.Play(UiSfx.Door);
        Quaternion from = transform.localRotation;
        for (float t = 0f; t < openSeconds; t += Time.deltaTime)
        {
            transform.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / openSeconds));
            await Awaitable.NextFrameAsync();
        }
        transform.localRotation = to;
    }
}
