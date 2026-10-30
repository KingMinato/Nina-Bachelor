using UnityEngine;

// Turns the NPC's head towards the player's eyes with humanoid look-at IK (layer needs IK Pass),
// so it looks down at small avatars. Sits next to the NPC's Animator. Also provides the point
// between the NPC's eyes for the player's camera to look at.
public class NpcLookAt : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] Vector3 faceOffset = new Vector3(0f, 0.1f, 0.08f);
    [SerializeField] float blendSeconds = 0.6f;

    Transform target;
    float weight;

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

    void Update()
    {
        weight = Mathf.MoveTowards(weight, target != null ? 1f : 0f, Time.deltaTime / blendSeconds);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (target == null)
            return;
        animator.SetLookAtWeight(weight, 0.1f, 1f, 0f, 0.6f);
        animator.SetLookAtPosition(target.position);
    }
}
