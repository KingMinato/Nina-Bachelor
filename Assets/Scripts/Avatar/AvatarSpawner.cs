using System;
using UnityEngine;

// Spawns the player avatar into the player rig, puts the camera at its eyes,
// hides its head from the camera and hands it to the mirror worlds.
// Spawning again replaces the current avatar (debug avatar switcher).
public class AvatarSpawner : MonoBehaviour
{
    [SerializeField] AvatarDatabase database;
    [SerializeField] PlayerActions playerActions;
    [SerializeField] Camera playerCamera;
    [SerializeField] MirrorWorld[] mirrorWorlds;

    AvatarBody current;
    Action<string> onActionStarted;

    public string[] SpeciesIds => database.Ids;
    public string SpeciesId { get; private set; }
    public string Gender { get; private set; }

    public AvatarBody Spawn(string speciesId, string gender)
    {
        if (current != null)
        {
            playerActions.Started -= onActionStarted;
            Destroy(current.gameObject);
        }

        var body = Instantiate(database.Get(speciesId, gender), playerActions.transform, false);
        playerCamera.transform.localPosition = new Vector3(0f, body.EyeHeight, 0f);

        onActionStarted = action =>
        {
            if (action == TutorialStep.Jump)
                body.PlayJump(playerActions.JumpTotalSeconds);
        };
        playerActions.Started += onActionStarted;

        foreach (var world in mirrorWorlds)
            world.SetAvatar(body.transform);
        body.gameObject.AddComponent<FirstPersonHider>().Init(body.FirstPersonHidden);

        current = body;
        SpeciesId = speciesId;
        Gender = gender;
        return body;
    }
}
