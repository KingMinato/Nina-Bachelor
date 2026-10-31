using UnityEngine;

// Scripted walk, e.g. through a door into the next room. The rig turns towards each waypoint,
// walks there at constant speed and finally turns to the last waypoint's facing.
public class WalkPath : MonoBehaviour
{
    [SerializeField] Transform[] waypoints;
    [SerializeField] float walkSpeed = 1.3f;
    [SerializeField] float turnSpeed = 180f;
    [Tooltip("Distance between two step sounds in metres.")]
    [SerializeField] float stepLength = 0.65f;

    float sinceStep;

    public Awaitable Walk(Transform rig) => WalkTo(rig, true, waypoints);

    public Transform First => waypoints[0];

    // Walks along points; turnAtEnd false keeps facing the way the rig walked.
    public async Awaitable WalkTo(Transform rig, bool turnAtEnd, params Transform[] points)
    {
        foreach (var point in points)
        {
            Vector3 target = new Vector3(point.position.x, rig.position.y, point.position.z);
            Vector3 dir = target - rig.position;
            if (dir.sqrMagnitude > 0.0001f)
                await TurnTo(rig, Quaternion.LookRotation(dir));

            while (rig.position != target)
            {
                rig.position = Vector3.MoveTowards(rig.position, target, walkSpeed * Time.deltaTime);
                sinceStep += walkSpeed * Time.deltaTime;
                if (sinceStep >= stepLength)
                {
                    sinceStep = 0;
                    UiSfx.Play(UiSfx.Step);
                }
                await Awaitable.NextFrameAsync();
            }
        }
        if (turnAtEnd)
            await TurnTo(rig, Quaternion.Euler(0f, points[points.Length - 1].eulerAngles.y, 0f));
    }

    async Awaitable TurnTo(Transform rig, Quaternion target)
    {
        while (Quaternion.Angle(rig.rotation, target) > 0.1f)
        {
            rig.rotation = Quaternion.RotateTowards(rig.rotation, target, turnSpeed * Time.deltaTime);
            await Awaitable.NextFrameAsync();
        }
        rig.rotation = target;
    }
}
