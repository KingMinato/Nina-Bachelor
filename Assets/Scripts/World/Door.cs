using UnityEngine;

// Door between Room 1 and Room 2. Sits on the hinge, the door leaf is a child.
public class Door : MonoBehaviour
{
    [SerializeField] float openAngle = 100f;
    [SerializeField] float openSeconds = 1.2f;

    public async Awaitable Open()
    {
        UiSfx.Play(UiSfx.Door);
        Quaternion from = transform.localRotation;
        Quaternion to = from * Quaternion.Euler(0f, openAngle, 0f);
        for (float t = 0f; t < openSeconds; t += Time.deltaTime)
        {
            transform.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / openSeconds));
            await Awaitable.NextFrameAsync();
        }
        transform.localRotation = to;
    }
}
