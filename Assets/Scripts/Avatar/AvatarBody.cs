using UnityEngine;

// Root of an avatar prefab. The root sits on the ground directly below the eyes,
// so the first-person camera is placed at (0, EyeHeight, 0) in the player rig.
public class AvatarBody : MonoBehaviour
{
    static readonly int JumpTrigger = Animator.StringToHash("Jump");
    static readonly int JumpSpeed = Animator.StringToHash("JumpSpeed");
    static readonly int CrouchParam = Animator.StringToHash("Crouch");
    static readonly int WalkParam = Animator.StringToHash("Walk");

    [SerializeField] Animator animator;
    [SerializeField] Transform eyes;
    [Tooltip("Length of the jump clip, used to stretch it to the duration of the player's jump.")]
    [SerializeField] float jumpClipSeconds = 1f;

    public float EyeHeight => eyes.localPosition.y;

    bool crouched;
    float squash = 1f;
    Vector3 lastPosition;

    public void PlayJump(float seconds)
    {
        animator.SetFloat(JumpSpeed, jumpClipSeconds / seconds);
        animator.SetTrigger(JumpTrigger);
    }

    public void SetCrouch(bool value)
    {
        crouched = value;
        animator.SetBool(CrouchParam, value);
    }

    void Update()
    {
        // Walks are scripted moves of the rig: walk while it moves sideways (jumps only move it up).
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        float speed = Time.deltaTime > 0f ? new Vector2(moved.x, moved.z).magnitude / Time.deltaTime : 0f;
        animator.SetBool(WalkParam, speed > 0.3f);

        // Creatures have no crouch clip, they squash instead (the mirror clones copy the root scale).
        if (animator.isHuman)
            return;
        squash = Mathf.MoveTowards(squash, crouched ? 0.7f : 1f, Time.deltaTime / 0.3f);
        float wide = 1f + (1f - squash) * 0.5f;
        transform.localScale = new Vector3(wide, squash, wide);
    }
}
