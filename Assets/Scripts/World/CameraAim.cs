using UnityEngine;

// Tilts the first-person camera smoothly towards a target, relative to the rig: the NPC's face (kept above the
// dialog panel) or the object in front of the player's spot (door, key, levers), whatever the avatar's eye height is.
// Without a target, or while it is not in front, the camera looks slightly down, so the floor arrows are in view.
public class CameraAim : MonoBehaviour
{
    [SerializeField] float sharpness = 4f;
    [Tooltip("Degrees the camera looks below the face, so the face sits in the upper part of the screen above the dialog panel.")]
    [SerializeField] float pitchDown = 14f;
    [Tooltip("Degrees the camera looks down without a target, at full eye height.")]
    [SerializeField] float restPitch = 12f;
    [Tooltip("From this eye height on the camera looks down by restPitch, smaller avatars proportionally less.")]
    [SerializeField] float fullPitchEyeHeight = 1.6f;

    [Tooltip("The target is only looked at while it is within this angle of the rig's facing.")]
    [SerializeField] float frontAngle = 60f;

    Transform target;
    Vector3? point;
    float rest;

    void Awake() => rest = restPitch;

    // Called by the avatar spawner with the avatar's standing eye height.
    public void SetEyeHeight(float eyes) => rest = restPitch * Mathf.Clamp01(eyes / fullPitchEyeHeight);

    public void Aim(Transform face)
    {
        target = face;
        point = null;
    }

    // Looks at a point, but never further down than at rest (null = rest).
    public void Look(Vector3? at)
    {
        target = null;
        point = at;
    }

    void Start()
    {
        transform.localRotation = Quaternion.Euler(rest, 0, 0);
    }

    void LateUpdate()
    {
        Quaternion local = Quaternion.Euler(rest, 0, 0);
        Vector3? at = target != null ? target.position : point;
        if (at != null)
        {
            Vector3 to = at.Value - transform.position;
            // Turning away from it looks at the floor arrows again.
            if (Vector3.Angle(Vector3.ProjectOnPlane(to, Vector3.up), transform.parent.forward) < frontAngle)
            {
                Quaternion look = Quaternion.LookRotation(to);
                float pitch = target != null ? look.eulerAngles.x + pitchDown : Mathf.Min(Mathf.DeltaAngle(0f, look.eulerAngles.x), rest);
                local = Quaternion.Inverse(transform.parent.rotation) * Quaternion.Euler(pitch, look.eulerAngles.y, 0f);
            }
        }
        transform.localRotation = Quaternion.Slerp(transform.localRotation, local, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
    }
}
