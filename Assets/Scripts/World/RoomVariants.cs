using System.Collections.Generic;
using UnityEngine;

// Switches between the furnishing variants of the rooms. Applies the start variant in Awake, before the
// mirror worlds copy Room 1. Switching later rebuilds the mirror copies.
public class RoomVariants : MonoBehaviour
{
    [SerializeField] RoomVariant[] variants;
    [SerializeField] int startIndex;
    [Tooltip("The shell surfaces of both rooms (only the real rooms, not the mirror copies).")]
    [SerializeField] Transform[] rooms;
    [SerializeField] MirrorWorld[] mirrorWorlds;

    readonly List<RoomSurface> surfaces = new List<RoomSurface>();

    public RoomVariant[] Variants => variants;
    public RoomVariant Current { get; private set; }

    void Awake()
    {
        foreach (var room in rooms)
            surfaces.AddRange(room.GetComponentsInChildren<RoomSurface>(true));
        Apply(variants[startIndex]);
    }

    // Returns false if no variant has this name (case-insensitive).
    public bool Apply(string name)
    {
        foreach (var variant in variants)
        {
            if (string.Equals(variant.DisplayName, name, System.StringComparison.OrdinalIgnoreCase))
            {
                Apply(variant);
                return true;
            }
        }
        return false;
    }

    public void Apply(RoomVariant variant)
    {
        // Variants may share parts (e.g. the plain door leaf), so switch everything off first.
        foreach (var v in variants)
            v.SetPartsActive(false);
        variant.SetPartsActive(true);
        foreach (var surface in surfaces)
            surface.SetMaterial(variant.MaterialFor(surface.SurfaceKind));
        Current = variant;
        foreach (var world in mirrorWorlds)
            world.RebuildRoom();
    }
}
