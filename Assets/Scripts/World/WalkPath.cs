using UnityEngine;

// Scripted walk from Room 1 through the door to the NPC. The rig turns towards each waypoint,
// walks there at constant speed and finally turns to the last waypoint's facing.
public class WalkPath : MonoBehaviour
{
    [SerializeField] Transform[] waypoints;
    [SerializeField] float walkSpeed = 1.3f;
    [SerializeField] float turnSpeed = 180f;

    public async Awaitable Walk(Transform rig)
    {
        foreach (var point in waypoints)
        {
            Vector3 target = new Vector3(point.position.x, rig.position.y, point.position.z);
            Vector3 dir = target - rig.position;
            if (dir.sqrMagnitude > 0.0001f)
                await TurnTo(rig, Quaternion.LookRotation(dir));

            while (rig.position != target)
            {
                rig.position = Vector3.MoveTowards(rig.position, target, walkSpeed * Time.deltaTime);
                await Awaitable.NextFrameAsync();
            }
        }
        await TurnTo(rig, Quaternion.Euler(0f, waypoints[waypoints.Length - 1].eulerAngles.y, 0f));
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
