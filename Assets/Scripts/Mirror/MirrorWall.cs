using UnityEngine;

// One mirror wall of Room 1: wall segments around the opening, frame and glass.
// The mirror worlds use it to adapt their copies of the room.
public class MirrorWall : MonoBehaviour
{
    [SerializeField] Renderer glass;
    [SerializeField] Material darkMaterial;

    // Inside a mirror world the other mirrors show no further reflection, only a dark surface.
    public void ShowAsDark()
    {
        glass.sharedMaterial = darkMaterial;
    }
}
