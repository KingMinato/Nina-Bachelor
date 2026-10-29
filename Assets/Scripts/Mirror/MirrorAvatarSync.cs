using UnityEngine;

// Lets an avatar clone inside a mirror world follow the player avatar: the root pose is reflected
// through the mirror plane, every bone below it is copied 1:1 in local space.
// Added by MirrorWorld. Runs in LateUpdate, after movement and the player's Animator.
public class MirrorAvatarSync : MonoBehaviour
{
    MirrorWorld world;
    Transform source;
    Transform[] sourceBones;
    Transform[] cloneBones;

    public void Init(Transform source, MirrorWorld world)
    {
        this.source = source;
        this.world = world;
        // Same hierarchy, so both lists are in the same order. Index 0 is the root itself.
        sourceBones = source.GetComponentsInChildren<Transform>(true);
        cloneBones = GetComponentsInChildren<Transform>(true);
        LateUpdate();
    }

    void LateUpdate()
    {
        if (source == null)
            return;

        world.Place(source, transform);
        for (int i = 1; i < sourceBones.Length; i++)
        {
            cloneBones[i].localPosition = sourceBones[i].localPosition;
            cloneBones[i].localRotation = sourceBones[i].localRotation;
            cloneBones[i].localScale = sourceBones[i].localScale;
        }
    }
}
