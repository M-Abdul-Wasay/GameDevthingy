using UnityEngine;

// Add this to the boss. It turns the walk animation on whenever the boss
// is actually moving, and back to idle when it stands still.
// Works with any movement script (BossAI, NavMeshAgent, etc.).
public class AutoWalkAnimator : MonoBehaviour
{
    public float moveThreshold = 0.1f; // units per second

    private Animator anim;
    private Vector3 lastPos;
    private bool wasWalking;

    static readonly int IsWalkingHash = Animator.StringToHash("isWalking");

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        lastPos = transform.position;
    }

    void Update()
    {
        if (anim == null) return;

        float speed = (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = transform.position;

        bool walking = speed > moveThreshold;
        if (walking != wasWalking)
        {
            wasWalking = walking;
            anim.SetBool(IsWalkingHash, walking);
        }
    }

    // Call this from your boss attack code if you want the attack animation
    public void PlayAttack()
    {
        if (anim != null) anim.SetTrigger("Attack");
    }
}
