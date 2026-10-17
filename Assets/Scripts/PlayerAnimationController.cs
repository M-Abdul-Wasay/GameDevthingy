using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator anim;
    private bool wasWalking;
    private bool wasRunning;
    private bool hasRunningParam = false;

    static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int DieHash = Animator.StringToHash("Die");

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();
        if (anim == null)
            Debug.LogWarning("No Animator found on Player!");
        else
        {
            foreach (var p in anim.parameters)
            {
                if (p.nameHash == IsRunningHash)
                {
                    hasRunningParam = true;
                    break;
                }
            }
        }
    }

    public void SetWalking(bool isWalking)
    {
        if (anim == null || isWalking == wasWalking) return;
        wasWalking = isWalking;
        anim.SetBool(IsWalkingHash, isWalking);
    }

    public void SetRunning(bool isRunning)
    {
        if (anim == null || !hasRunningParam || isRunning == wasRunning) return;
        wasRunning = isRunning;
        anim.SetBool(IsRunningHash, isRunning);
    }

    public void TriggerAttack()
    {
        if (anim == null) return;
        anim.SetTrigger(AttackHash);
    }

    public void TriggerDeath()
    {
        if (anim == null) return;
        anim.SetTrigger(DieHash);
    }
}