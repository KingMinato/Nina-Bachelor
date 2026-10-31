using System.Collections.Generic;
using UnityEngine;

// Walking inside a room: arrows on the floor lead from the first spot (home, where the player enters)
// to every other spot (in front of a mirror, door, key, ...) and back. Objects can only be used from their spot.
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
    [Tooltip("Distance of an arrow from the player, far enough to be in view on the floor.")]
    [SerializeField] float arrowReach = 2.4f;
    [Tooltip("An arrow lies at most this far behind its spot.")]
    [SerializeField] float arrowBeyond = 1f;

    int at;

    // The player has just walked in and stands at home.
    public void Enter() => at = 0;

    // Shows the arrows and the targets that can be used from the current spot (spot null = from anywhere).
    // Tapped arrows walk the player; returns the first tapped target.
    public async Awaitable<WorldButton> Next(Texts texts, params (WorldButton button, string caption, Transform spot)[] targets)
    {
        while (true)
        {
            var options = new List<(WorldButton, string)>();
            foreach (var (button, caption, spot) in targets)
                if (spot == null || spot == spots[at])
                    options.Add((button, caption));
            for (int i = 0; i < spots.Length; i++)
            {
                if (i == at || at != 0 && i != 0)
                    continue;
                PlaceArrow(arrows[i], spots[at].position, spots[i].position);
                options.Add((arrows[i], ""));
            }

            var clicked = await WorldButton.FirstClick(options.ToArray());
            int to = System.Array.IndexOf(arrows, clicked);
            if (to < 0)
                return clicked;

            actionButtons.Hide();
            await player.StandUp();
            await walker.WalkTo(player.transform, to != 0, spots[to]);
            at = to;
            actionButtons.Show(texts);
        }
    }

    // On the floor along the way, pointing along it.
    void PlaceArrow(WorldButton arrow, Vector3 from, Vector3 to)
    {
        Vector3 way = to - from;
        way.y = 0f;
        float distance = Mathf.Min(arrowReach, way.magnitude + arrowBeyond);
        arrow.transform.SetPositionAndRotation(from + way.normalized * distance + Vector3.up * 0.01f,
            Quaternion.LookRotation(Vector3.down, way));
    }
}
