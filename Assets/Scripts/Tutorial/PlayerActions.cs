using System;
using UnityEngine;

// Turns, jumps and crouches the player rig. One action at a time: input while an action runs is ignored.
// Sits on the rig root, so camera and avatar move together.
public class PlayerActions : MonoBehaviour
{
    [SerializeField] float turnSeconds = 0.8f;
    [Tooltip("Crouch before take-off, the rig stays on the ground.")]
    [SerializeField] float jumpWindupSeconds = 0.15f;
    [SerializeField] float jumpSeconds = 0.6f;
    [Tooltip("Landing, the rig stays on the ground while the avatar absorbs the impact.")]
    [SerializeField] float jumpLandSeconds = 0.2f;
    [SerializeField] float jumpHeight = 0.5f;
    [Tooltip("First-person camera, lowered while crouching.")]
    [SerializeField] Transform eyes;
    [SerializeField] float crouchSeconds = 0.3f;
    [Tooltip("Eye height while crouching, relative to standing.")]
    [SerializeField] float crouchEyeFactor = 0.65f;

    // Fired when an action starts and when it has finished, with one of the TutorialStep action names.
    public event Action<string> Started;
    public event Action<string> Performed;

    // Whole jump including windup and landing, so the avatar's jump animation can match it.
    public float JumpTotalSeconds => jumpWindupSeconds + jumpSeconds + jumpLandSeconds;

    public bool IsBusy { get; private set; }
    public bool IsCrouched { get; private set; }
    public float TurnAngle { get; set; } = 90f;

    public void TurnLeft() => Run(TutorialStep.TurnLeft);
    public void TurnRight() => Run(TutorialStep.TurnRight);
    public void Jump() => Run(IsCrouched ? TutorialStep.CrouchJump : TutorialStep.Jump);
    // Toggles between crouching and standing.
    public void Crouch() => Run(IsCrouched ? TutorialStep.StandUp : TutorialStep.Crouch);

    // Stands up if crouched, e.g. before a scripted walk.
    public async Awaitable StandUp()
    {
        while (IsBusy)
            await Awaitable.NextFrameAsync();
        if (IsCrouched)
            Run(TutorialStep.StandUp);
        while (IsBusy)
            await Awaitable.NextFrameAsync();
    }

    async void Run(string action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Started?.Invoke(action);
        switch (action)
        {
            case TutorialStep.Jump:
                UiSfx.Play(UiSfx.Jump);
                await DoJump();
                break;
            case TutorialStep.CrouchJump:
                UiSfx.Play(UiSfx.Jump);
                IsCrouched = false;
                _ = MoveEyes(standingEyes, jumpWindupSeconds);
                await DoJump();
                break;
            case TutorialStep.Crouch:
                UiSfx.Play(UiSfx.Turn);
                IsCrouched = true;
                standingEyes = eyes.localPosition.y;
                await MoveEyes(standingEyes * crouchEyeFactor, crouchSeconds);
                break;
            case TutorialStep.StandUp:
                UiSfx.Play(UiSfx.Turn);
                IsCrouched = false;
                await MoveEyes(standingEyes, crouchSeconds);
                break;
            default:
                UiSfx.Play(UiSfx.Turn);
                await DoTurn(action == TutorialStep.TurnLeft ? -TurnAngle : TurnAngle);
                break;
        }
        IsBusy = false;
        Performed?.Invoke(action);
    }

    float standingEyes;

    async Awaitable MoveEyes(float y, float seconds)
    {
        float from = eyes.localPosition.y;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            SetEyes(Mathf.Lerp(from, y, Mathf.SmoothStep(0f, 1f, t / seconds)));
            await Awaitable.NextFrameAsync();
        }
        SetEyes(y);
    }

    void SetEyes(float y) => eyes.localPosition = new Vector3(eyes.localPosition.x, y, eyes.localPosition.z);

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
        UiSfx.Play(UiSfx.Land);
        await Awaitable.WaitForSecondsAsync(jumpLandSeconds);
    }
}
