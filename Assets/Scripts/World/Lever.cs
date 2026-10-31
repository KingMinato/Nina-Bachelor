using UnityEngine;

// One lever of the puzzle in Room 3. Sits on the pivot (local x along the wall, z out of the wall),
// the handle is a child pointing along local +y.
public class Lever : MonoBehaviour
{
    [SerializeField] bool startUp;
    [Tooltip("Tilt of the handle out of the wall, from vertical.")]
    [SerializeField] float tilt = 35f;
    [SerializeField] float seconds = 0.25f;

    public bool Up { get; private set; }

    // Turn of the pivot towards the wall, without the tilt.
    Quaternion mount;

    void Awake()
    {
        Up = startUp;
        mount = transform.localRotation * Quaternion.Inverse(Tilt(Up));
    }

    Quaternion Tilt(bool up) => Quaternion.Euler(up ? tilt : 180f - tilt, 0f, 0f);

    Quaternion Pose(bool up) => mount * Tilt(up);

    public async Awaitable Toggle()
    {
        Up = !Up;
        UiSfx.Play(UiSfx.Click);
        Quaternion from = transform.localRotation, to = Pose(Up);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            transform.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            await Awaitable.NextFrameAsync();
        }
        transform.localRotation = to;
    }
}
