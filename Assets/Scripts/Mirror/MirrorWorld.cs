using System;
using System.Collections.Generic;
using UnityEngine;

// Fake mirror: a reflected copy of a room behind one of its mirror walls, plus a clone of the player avatar.
// Sits on the mirror plane with local z pointing into the wall and scale (1, 1, -1), so anything
// placed under it with plane-relative coordinates appears reflected. Unity fixes the triangle winding.
public class MirrorWorld : MonoBehaviour
{
    // Layer of the player's own avatar, hidden from the first-person camera. Mirror clones use Default.
    public const string FirstPersonHiddenLayer = "FirstPersonHidden";

    [SerializeField] Transform room;
    [SerializeField] MirrorWall ownMirror;
    [Tooltip("Objects outside the room seen in this mirror too (the doors to the neighbouring rooms). Copied, pose synced every frame.")]
    [SerializeField] Transform[] sharedParts;
    [Tooltip("Player avatar. Can also be set later with SetAvatar, e.g. by the avatar spawner.")]
    [SerializeField] Transform avatar;

    readonly List<(Transform source, Transform copy)> synced = new List<(Transform, Transform)>();
    GameObject avatarClone;
    GameObject roomCopy;

    void Start()
    {
        if (roomCopy == null)
            CopyRoom();
        if (avatar != null)
            SetAvatar(avatar);
    }

    // Copies the room again, e.g. after switching the room variant. Before Start this does nothing,
    // Start makes the first copy.
    public void RebuildRoom()
    {
        if (roomCopy == null)
            return;
        Destroy(roomCopy);
        CopyRoom();
    }

    void CopyRoom()
    {
        // Container with identity local pose, so its children are placed in this mirror world's frame.
        roomCopy = new GameObject(room.name + " (Mirror)");
        roomCopy.transform.SetParent(transform, false);
        AdaptMirrors(Copy(room));
        synced.Clear();
        foreach (var part in sharedParts)
            synced.Add((part, Copy(part)));
        RemoveInactive(roomCopy.transform);
    }

    Transform Copy(Transform source)
    {
        var copy = Instantiate(source.gameObject, roomCopy.transform).transform;
        Place(source, copy);
        return copy;
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
            Place(source, copy);
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

    // Inactive objects (the unused room variants) are invisible anyway, the copies don't need them.
    static void RemoveInactive(Transform copy)
    {
        foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            if (!t.gameObject.activeSelf)
                Destroy(t.gameObject);
    }
}
