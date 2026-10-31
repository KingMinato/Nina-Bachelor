using UnityEngine;

// Door between two rooms. Sits on the hinge, the door leaf is a child.
public class Door : MonoBehaviour
{
    [SerializeField] float openAngle = 100f;
    [SerializeField] float openSeconds = 1.2f;

    Quaternion closed;

    void Awake() => closed = transform.localRotation;

    public Awaitable Open()
    {
        UiSfx.Play(UiSfx.DoorOpen);
        return Swing(closed * Quaternion.Euler(0f, openAngle, 0f), false);
    }

    // Speeds up towards the frame and closes with a thud.
    public async Awaitable Close()
    {
        await Swing(closed, true);
        UiSfx.Play(UiSfx.DoorClose);
    }

    async Awaitable Swing(Quaternion to, bool easeIn)
    {
        Quaternion from = transform.localRotation;
        for (float t = 0f; t < openSeconds; t += Time.deltaTime)
        {
            float k = t / openSeconds;
            transform.localRotation = Quaternion.Slerp(from, to, easeIn ? k * k : Mathf.SmoothStep(0f, 1f, k));
            await Awaitable.NextFrameAsync();
        }
        transform.localRotation = to;
    }
}
