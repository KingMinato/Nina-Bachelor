using UnityEngine;

// Turns the NPC's head towards the player's eyes with humanoid look-at IK (layer needs IK Pass),
// so it looks down at small avatars, and moves its hands while it thinks (hand at the chin, hands on the head).
// Sits next to the NPC's Animator. Also provides the point between the NPC's eyes for the player's camera to look at.
public class NpcLookAt : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] Vector3 faceOffset = new Vector3(0f, 0.1f, 0.08f);
    [SerializeField] float blendSeconds = 0.6f;

    public enum Gesture { None, Chin, Head }

    Transform target;
    float weight;
    Gesture gesture, shown;
    float gestureWeight;

    public Transform Face { get; private set; }

    void Awake()
    {
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        Face = new GameObject("Face").transform;
        Face.SetParent(head, false);
        Face.localPosition = head.InverseTransformVector(transform.TransformVector(faceOffset));
    }

    public void LookAt(Transform target)
    {
        this.target = target;
    }

    public void Think(Gesture value)
    {
        gesture = value;
    }

    void Update()
    {
        weight = Mathf.MoveTowards(weight, target != null ? 1f : 0f, Time.deltaTime / blendSeconds);
        // Lower the hands fully before switching to another gesture.
        if (gesture != shown && gestureWeight <= 0f)
            shown = gesture;
        gestureWeight = Mathf.MoveTowards(gestureWeight, gesture == shown && shown != Gesture.None ? 1f : 0f, Time.deltaTime / blendSeconds);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (target != null)
        {
            animator.SetLookAtWeight(weight, 0.1f, 1f, 0f, 0.6f);
            animator.SetLookAtPosition(target.position);
        }
        if (shown == Gesture.None || gestureWeight <= 0f)
            return;

        // Targets in the NPC's frame, around the face; a slow wobble keeps the hands alive.
        Vector3 face = Face.position, up = transform.up, right = transform.right, forward = transform.forward;
        float wobble = Mathf.Sin(Time.time * 2.5f) * 0.015f;
        if (shown == Gesture.Chin)
        {
            Vector3 chin = face - up * (0.21f + wobble) + forward * 0.1f;
            Reach(AvatarIKGoal.RightHand, chin, AvatarIKHint.RightElbow, chin - up * 0.3f + right * 0.15f + forward * 0.1f);
            Vector3 elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
            Reach(AvatarIKGoal.LeftHand, elbow - right * 0.05f, AvatarIKHint.LeftElbow, elbow - right * 0.35f - up * 0.1f);
        }
        else
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 hand = face + up * (0.12f + wobble * side) + right * side * 0.11f - forward * 0.02f;
                Reach(side > 0 ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand, hand,
                    side > 0 ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow, hand + right * side * 0.35f + forward * 0.1f - up * 0.1f);
            }
        }
    }

    void Reach(AvatarIKGoal goal, Vector3 position, AvatarIKHint hint, Vector3 hintPosition)
    {
        animator.SetIKPositionWeight(goal, gestureWeight);
        animator.SetIKPosition(goal, position);
        animator.SetIKHintPositionWeight(hint, gestureWeight);
        animator.SetIKHintPosition(hint, hintPosition);
    }
}
