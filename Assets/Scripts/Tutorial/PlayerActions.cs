using System;
using UnityEngine;

// Turns and jumps the player rig. One action at a time: input while an action runs is ignored.
// Sits on the rig root, so camera and avatar move together.
public class PlayerActions : MonoBehaviour
{
    [SerializeField] float turnSeconds = 0.4f;
    [Tooltip("Crouch before take-off, the rig stays on the ground.")]
    [SerializeField] float jumpWindupSeconds = 0.15f;
    [SerializeField] float jumpSeconds = 0.6f;
    [Tooltip("Landing, the rig stays on the ground while the avatar absorbs the impact.")]
    [SerializeField] float jumpLandSeconds = 0.2f;
    [SerializeField] float jumpHeight = 0.5f;

    // Fired when an action starts and when it has finished, with one of the TutorialStep action names.
    public event Action<string> Started;
    public event Action<string> Performed;

    // Whole jump including windup and landing, so the avatar's jump animation can match it.
    public float JumpTotalSeconds => jumpWindupSeconds + jumpSeconds + jumpLandSeconds;

    public bool IsBusy { get; private set; }
    public float TurnAngle { get; set; } = 90f;

    public void TurnLeft() => Run(TutorialStep.TurnLeft);
    public void TurnRight() => Run(TutorialStep.TurnRight);
    public void Jump() => Run(TutorialStep.Jump);

    async void Run(string action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Started?.Invoke(action);
        if (action == TutorialStep.Jump)
            await DoJump();
        else
            await DoTurn(action == TutorialStep.TurnLeft ? -TurnAngle : TurnAngle);
        IsBusy = false;
        Performed?.Invoke(action);
    }

    async Awaitable DoTurn(float angle)
    {
        Quaternion from = transform.rotation;
        Quaternion to = from * Quaternion.Euler(0f, angle, 0f);
        for (float t = 0f; t < turnSeconds; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / turnSeconds));
            await Awaitable.NextFrameAsync();
        }
        transform.rotation = to;
    }

    async Awaitable DoJump()
    {
        Vector3 ground = transform.position;
        await Awaitable.WaitForSecondsAsync(jumpWindupSeconds);
        for (float t = 0f; t < jumpSeconds; t += Time.deltaTime)
        {
            float x = t / jumpSeconds;
            transform.position = ground + Vector3.up * (4f * jumpHeight * x * (1f - x));
            await Awaitable.NextFrameAsync();
        }
        transform.position = ground;
        await Awaitable.WaitForSecondsAsync(jumpLandSeconds);
    }
}
