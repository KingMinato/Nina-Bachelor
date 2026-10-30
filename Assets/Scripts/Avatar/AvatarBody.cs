using UnityEngine;

// Root of an avatar prefab. The root sits on the ground directly below the eyes,
// so the first-person camera is placed at (0, EyeHeight, 0) in the player rig.
public class AvatarBody : MonoBehaviour
{
    static readonly int JumpTrigger = Animator.StringToHash("Jump");
    static readonly int JumpSpeed = Animator.StringToHash("JumpSpeed");

    [SerializeField] Animator animator;
    [SerializeField] Transform eyes;
    [Tooltip("Bones collapsed for the first-person camera (head, hat, ...). Mirror clones show them.")]
    [SerializeField] Transform[] firstPersonHidden;
    [Tooltip("Length of the jump clip, used to stretch it to the duration of the player's jump.")]
    [SerializeField] float jumpClipSeconds = 1f;

    public float EyeHeight => eyes.localPosition.y;
    public Transform[] FirstPersonHidden => firstPersonHidden;

    public void PlayJump(float seconds)
    {
        animator.SetFloat(JumpSpeed, jumpClipSeconds / seconds);
        animator.SetTrigger(JumpTrigger);
    }
}
