using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator anim;
    private bool wasWalking;

    static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    static readonly int AttackHash = Animator.StringToHash("Attack");

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();
        if (anim == null)
            Debug.LogWarning("No Animator found on Player!");
    }

    public void SetWalking(bool isWalking)
    {
        if (anim == null || isWalking == wasWalking) return;
        wasWalking = isWalking;
        anim.SetBool(IsWalkingHash, isWalking);
    }

    public void TriggerAttack()
    {
        if (anim == null) return;
        anim.SetTrigger(AttackHash);
    }
}