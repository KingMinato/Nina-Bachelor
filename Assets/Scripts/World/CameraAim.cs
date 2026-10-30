using UnityEngine;

// Tilts the first-person camera smoothly towards a target (the NPC's face), relative to the rig,
// so the player looks the NPC in the face whatever the avatar's eye height is.
public class CameraAim : MonoBehaviour
{
    [SerializeField] float sharpness = 4f;

    Transform target;

    public void Aim(Transform target)
    {
        this.target = target;
    }

    void LateUpdate()
    {
        if (target == null)
            return;
        Quaternion look = Quaternion.LookRotation(target.position - transform.position);
        Quaternion local = Quaternion.Inverse(transform.parent.rotation) * look;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, local, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
    }
}
