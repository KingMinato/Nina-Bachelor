using UnityEngine;

// Inventory slot (top left) for the key in Room 2. Taking the key flies the 3D key in front of the camera,
// spins it once and drops it into the slot; using it flies it from the slot in front of the camera,
// where it turns like in a lock and vanishes. Sits on the slot, shown only in the key room.
public class KeyInventory : MonoBehaviour
{
    [SerializeField] Camera cam;
    [Tooltip("Key icon in the slot, shown while the player holds the key.")]
    [SerializeField] GameObject icon;
    [Tooltip("Distance of the flying key from the camera (beyond the near clip plane).")]
    [SerializeField] float holdDistance = 0.8f;

    public async Awaitable Take(Transform key)
    {
        Vector3 from = key.position, front = Front();
        Quaternion rotation = key.rotation;
        Vector3 size = key.localScale;
        UiSfx.Play(UiSfx.KeyPick);
        await Tween(0.6f, t => key.position = Vector3.Lerp(from, front, t));
        await Tween(0.8f, t => key.rotation = Quaternion.AngleAxis(360f * t, cam.transform.up) * rotation);
        Vector3 slot = SlotPoint();
        await Tween(0.4f, t =>
        {
            key.position = Vector3.Lerp(front, slot, t);
            key.localScale = size * (1f - 0.8f * t);
        });
        key.gameObject.SetActive(false);
        key.localScale = size;
        icon.SetActive(true);
    }

    public async Awaitable Use(Transform key)
    {
        Vector3 slot = SlotPoint(), front = Front();
        Quaternion rotation = key.rotation;
        Vector3 size = key.localScale;
        icon.SetActive(false);
        key.gameObject.SetActive(true);
        await Tween(0.5f, t =>
        {
            key.position = Vector3.Lerp(slot, front, t);
            key.localScale = size * (0.2f + 0.8f * t);
        });
        // A quarter turn as in a lock, then it shrinks away.
        UiSfx.Play(UiSfx.Unlock);
        await Tween(0.5f, t => key.rotation = Quaternion.AngleAxis(90f * t, cam.transform.forward) * rotation);
        await Tween(0.3f, t => key.localScale = size * (1f - t));
        key.gameObject.SetActive(false);
        key.localScale = size;
    }

    Vector3 Front() => cam.transform.position + cam.transform.forward * holdDistance;

    // The point in front of the camera behind the slot on screen.
    Vector3 SlotPoint() => cam.ScreenPointToRay(transform.position).GetPoint(holdDistance);

    static async Awaitable Tween(float seconds, System.Action<float> apply)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            apply(Mathf.SmoothStep(0f, 1f, t / seconds));
            await Awaitable.NextFrameAsync();
        }
        apply(1f);
    }
}
