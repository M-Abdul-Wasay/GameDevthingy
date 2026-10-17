using UnityEngine;

public class BossAnimationController : MonoBehaviour
{
    private Animator anim;

    [Header("Animation State Names")]
    public string idleState = "Idle";
    public string walkState = "Running";
    public string attackState = "Attack";
    public string dieState = "Die";

    void Awake()
    {
        // Null check for Animator to prevent NullReferenceException
        anim = GetComponent<Animator>();
        if (anim == null) {
            Debug.LogWarning($"No Animator found on {gameObject.name}. Animations will not play.");
        }
    }

    // GUARD 1: Safe Parameter Setting (Fixes 'Parameter does not exist' errors)
    private void SafeSetBool(string name, bool value)
    {
        if (anim == null) return;
        try {
            anim.SetBool(name, value);
        } catch (System.Exception) {
            // Parameter missing in Animator window - silently ignore to prevent spam
        }
    }

    private void SafeSetTrigger(string name)
    {
        if (anim == null) return;
        try {
            anim.SetTrigger(name);
        } catch (System.Exception) {
            // Trigger missing - silently ignore
        }
    }

    // GUARD 2: Safe State Playing (Fixes 'State could not be found' errors)
    private void SafePlay(string stateName)
    {
        if (anim == null || string.IsNullOrEmpty(stateName)) return;
        try {
            anim.Play(stateName);
        } catch (System.Exception) {
            // State missing in Animator - ignore
        }
    }

    public void SetWalking(bool isWalking)
    {
        // Try the parameter first, then force the state as fallback
        SafeSetBool("isWalking", isWalking);
        if (isWalking) SafePlay(walkState);
        else SafePlay(idleState);
    }

    public void TriggerAttack()
    {
        SafeSetTrigger("Attack");
        SafePlay(attackState);
    }

    public void TriggerDeath()
    {
        SafeSetTrigger("Die");
        SafePlay(dieState);
    }
}
