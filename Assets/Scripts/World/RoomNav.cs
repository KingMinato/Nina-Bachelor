using System.Collections.Generic;
using UnityEngine;

// Walking inside a room: arrows lead from the first spot (home, where the player enters) to every other spot
// (in front of a mirror, door, key, ...) and back. Objects can only be used from their spot.
// The arrows are screen UI buttons (styled like the other buttons, never hidden by furniture), kept over a point
// on the floor along the way. They always point up: every spot lies straight ahead after a 90 degree turn.
public class RoomNav : MonoBehaviour
{
    [SerializeField] PlayerActions player;
    [SerializeField] ActionButtonsUI actionButtons;
    [Tooltip("Walks between the spots, its own waypoints are not used.")]
    [SerializeField] WalkPath walker;
    [Tooltip("Spot 0 is home, facing of the others = facing after arriving.")]
    [SerializeField] Transform[] spots;
    [Tooltip("arrows[i] leads to spots[i]; arrow 0 (home) is shown at every other spot.")]
    [SerializeField] WorldButton[] arrows;
    [Tooltip("Distance of an arrow's floor point from the player, far enough to be in view.")]
    [SerializeField] float arrowReach = 2.4f;
    [Tooltip("An arrow lies at most this far behind its spot.")]
    [SerializeField] float arrowBeyond = 1f;

    [System.NonSerialized] public System.Action onWalk;

    int at;
    Vector3[] points;

    // The player has just walked in and stands at home.
    public void Enter() => at = 0;

    // Shows the arrows and the targets that can be used from the current spot (spot null = from anywhere).
    // Tapped arrows walk the player; returns the first tapped target.
    public async Awaitable<WorldButton> Next(Texts texts, params (WorldButton button, string caption, Transform spot)[] targets)
    {
        while (true)
        {
            var options = new List<(WorldButton, string)>();
            Vector3 middle = Vector3.zero;
            int here = 0;
            foreach (var (button, caption, spot) in targets)
                if (spot == null || spot == spots[at])
                {
                    options.Add((button, caption));
                    if (spot != null)
                    {
                        middle += button.Center;
                        here++;
                    }
                }
            // Standing in front of a door, key, levers or the NPC: look at it.
            Aim(here > 0 ? middle / here : null);
            for (int i = 0; i < spots.Length; i++)
            {
                if (i == at || at != 0 && i != 0)
                    continue;
                PlaceArrow(i, spots[at].position, spots[i].position);
                options.Add((arrows[i], ""));
            }

            var clicked = await WorldButton.FirstClick(options.ToArray());
            int to = System.Array.IndexOf(arrows, clicked);
            if (to < 0)
                return clicked;

            onWalk?.Invoke();
            Aim(null);
            actionButtons.Hide();
            await player.StandUp();
            await walker.WalkTo(player.transform, to != 0, spots[to]);
            at = to;
            actionButtons.Show(texts);
        }
    }

    static void Aim(Vector3? at) => Camera.main.GetComponent<CameraAim>().Look(at);

    // Floor point along the way, followed on screen in LateUpdate.
    void PlaceArrow(int i, Vector3 from, Vector3 to)
    {
        points ??= new Vector3[arrows.Length];
        Vector3 way = to - from;
        way.y = 0f;
        points[i] = from + way.normalized * Mathf.Min(arrowReach, way.magnitude + arrowBeyond);
        points[i].y = from.y;
        Follow(i);
    }

    void LateUpdate()
    {
        if (points == null)
            return;
        for (int i = 0; i < arrows.Length; i++)
            if (arrows[i].gameObject.activeSelf)
                Follow(i);
    }

    void Follow(int i)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(points[i]);
        var t = arrows[i].transform;
        t.position = new Vector3(screen.x, screen.y, 0f);
        // Behind the camera: hidden until the player turns towards it.
        t.localScale = screen.z > 0f ? Vector3.one : Vector3.zero;
    }
}
