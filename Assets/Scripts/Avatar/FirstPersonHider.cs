using UnityEngine;

// Collapses bones of the player's own avatar (head, hat) so the first-person camera inside
// the head sees nothing of it. Runs after MirrorAvatarSync, which therefore copies the real
// scale to the mirror clones, and restores the scale before the Animator runs again.
[DefaultExecutionOrder(100)]
public class FirstPersonHider : MonoBehaviour
{
    Transform[] bones;
    Vector3[] scales;

    public void Init(Transform[] bones)
    {
        this.bones = bones;
        scales = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            scales[i] = bones[i].localScale;
    }

    void Update()
    {
        for (int i = 0; i < bones.Length; i++)
            bones[i].localScale = scales[i];
    }

    void LateUpdate()
    {
        for (int i = 0; i < bones.Length; i++)
        {
            scales[i] = bones[i].localScale;
            bones[i].localScale = Vector3.zero;
        }
    }
}
