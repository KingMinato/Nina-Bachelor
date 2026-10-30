using UnityEngine;

// Spawns the player avatar into the player rig, puts the camera at its eyes,
// hides its head from the camera and hands it to the mirror worlds.
public class AvatarSpawner : MonoBehaviour
{
    [SerializeField] AvatarDatabase database;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] Camera playerCamera;
    [SerializeField] MirrorWorld[] mirrorWorlds;

    public AvatarBody Spawn(string speciesId, string gender)
    {
        var body = Instantiate(database.Get(speciesId, gender), playerActions.transform, false);
        playerCamera.transform.localPosition = new Vector3(0f, body.EyeHeight, 0f);

        playerActions.Started += action =>
        {
            if (action == TutorialStep.Jump)
                body.PlayJump(playerActions.JumpTotalSeconds);
        };

        foreach (var world in mirrorWorlds)
            world.SetAvatar(body.transform);
        body.gameObject.AddComponent<FirstPersonHider>().Init(body.FirstPersonHidden);
        return body;
    }
}
