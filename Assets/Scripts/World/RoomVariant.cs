using UnityEngine;

// One furnishing variant of both rooms: materials for the shell surfaces and the objects that belong to it
// (furniture under Room1/Room2, door model under the door hinge). Only one variant is active at a time.
public class RoomVariant : MonoBehaviour
{
    [Tooltip("Short name, shown in the debug room switcher and used by the URL parameter 'room'.")]
    [SerializeField] string displayName;
    [Tooltip("Empty = keep the scene's original material.")]
    [SerializeField] Material floor, wall, ceiling, door;
    [SerializeField] GameObject[] parts;

    public string DisplayName => displayName;

    public Material MaterialFor(RoomSurface.Kind kind)
    {
        switch (kind)
        {
            case RoomSurface.Kind.Floor: return floor;
            case RoomSurface.Kind.Wall: return wall;
            case RoomSurface.Kind.Ceiling: return ceiling;
            default: return door;
        }
    }

    public void SetPartsActive(bool active)
    {
        foreach (var part in parts)
            part.SetActive(active);
    }
}
