using UnityEngine;

// Marks a renderer of the room shell (floor, wall, ceiling, door leaf) whose material a room variant replaces.
[RequireComponent(typeof(Renderer))]
public class RoomSurface : MonoBehaviour
{
    public enum Kind { Floor, Wall, Ceiling, Door }

    [SerializeField] Kind kind;

    Material original;

    public Kind SurfaceKind => kind;

    // material null restores the material the surface had in the scene.
    public void SetMaterial(Material material)
    {
        var r = GetComponent<Renderer>();
        if (original == null)
            original = r.sharedMaterial;
        r.sharedMaterial = material != null ? material : original;
    }
}
