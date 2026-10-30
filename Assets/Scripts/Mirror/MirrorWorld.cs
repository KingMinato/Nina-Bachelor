using System;
using System.Collections.Generic;
using UnityEngine;

// Fake mirror: a reflected copy of Room 1 behind one mirror wall, plus a clone of the player avatar.
// Sits on the mirror plane with local z pointing into the wall and scale (1, 1, -1), so anything
// placed under it with plane-relative coordinates appears reflected. Unity fixes the triangle winding.
public class MirrorWorld : MonoBehaviour
{
    // Layer of the player's own avatar, hidden from the first-person camera. Mirror clones use Default.
    public const string FirstPersonHiddenLayer = "FirstPersonHidden";

    [SerializeField] Transform room;
    [SerializeField] MirrorWall ownMirror;
    [Tooltip("Moving parts of the room (e.g. the door hinge) whose local pose is copied every frame.")]
    [SerializeField] Transform[] movingParts;
    [Tooltip("Player avatar. Can also be set later with SetAvatar, e.g. by the avatar spawner.")]
    [SerializeField] Transform avatar;

    readonly List<(Transform source, Transform copy)> synced = new List<(Transform, Transform)>();
    GameObject avatarClone;

    void Start()
    {
        var copy = Instantiate(room.gameObject, transform).transform;
        copy.name = room.name + " (Mirror)";
        Place(room, copy);
        AdaptMirrors(copy);

        foreach (var part in movingParts)
            synced.Add((part, copy.Find(PathFrom(room, part))));

        if (avatar != null)
            SetAvatar(avatar);
    }

    // Replaces the previous clone, so the avatar can be switched at runtime (debug UI).
    public void SetAvatar(Transform source)
    {
        if (avatarClone != null)
            Destroy(avatarClone);
        var clone = Instantiate(source.gameObject, transform);
        avatarClone = clone;
        clone.name = source.name + " (Mirror)";
        int hidden = LayerMask.NameToLayer(FirstPersonHiddenLayer);
        foreach (var t in clone.GetComponentsInChildren<Transform>(true))
            if (t.gameObject.layer == hidden)
                t.gameObject.layer = 0;
        // Bones are copied from the player avatar, so the clone must not animate itself.
        foreach (var animator in clone.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;
        clone.AddComponent<MirrorAvatarSync>().Init(source, this);
    }

    // Puts target (a child of this mirror world) at the reflection of source.
    public void Place(Transform source, Transform target)
    {
        Quaternion inverse = Quaternion.Inverse(transform.rotation);
        target.localPosition = inverse * (source.position - transform.position);
        target.localRotation = inverse * source.rotation;
    }

    void LateUpdate()
    {
        foreach (var (source, copy) in synced)
        {
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
        }
    }

    // The own mirror wall lies on the mirror plane and is removed from the copy; the others turn dark.
    void AdaptMirrors(Transform copy)
    {
        var sourceWalls = room.GetComponentsInChildren<MirrorWall>(true);
        var copyWalls = copy.GetComponentsInChildren<MirrorWall>(true);
        int own = Array.IndexOf(sourceWalls, ownMirror);
        for (int i = 0; i < copyWalls.Length; i++)
        {
            if (i == own)
                Destroy(copyWalls[i].gameObject);
            else
                copyWalls[i].ShowAsDark();
        }
    }

    static string PathFrom(Transform root, Transform part)
    {
        string path = part.name;
        for (var t = part.parent; t != root; t = t.parent)
            path = t.name + "/" + path;
        return path;
    }
}
