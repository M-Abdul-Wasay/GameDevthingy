using UnityEngine;

public enum PlayerForm { Shadow, TimeWarp, Eternal }

public class ShadowHeroController : MonoBehaviour
{
    [Header("General Stats")]
    public PlayerForm currentForm = PlayerForm.Shadow;
    public float health = 100f;
    public float maxHealth = 100f;
    public float essence = 0f;
    public float maxEssence = 100f;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Walls")]
    public float wallRadius = 0.3f;   // how wide he is for wall checks
    public float wallHeight = 0.9f;   // height of the wall check above his feet

    [Header("Jump")]
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpForce = 6f;      // higher = jumps higher
    public float gravity = -20f;      // more negative = falls faster

    [Header("Visuals")]
    public Renderer characterRenderer;
    public Material shadowMat, timeMat, eternalMat;

    [Header("Combat")]
    public float attackRange = 2f;
    public float attackDamage = 15f;

    private PlayerAnimationController pAnim;
    private CameraFollow camFollow;

    private float verticalVelocity;
    private bool isGrounded;
    private float startY;

    void Awake()
    {
        pAnim = GetComponent<PlayerAnimationController>();
        if (Camera.main != null)
            camFollow = Camera.main.GetComponent<CameraFollow>();
    }

    void Start()
    {
        startY = transform.position.y;
        UpdateVisuals();
    }

    void Update()
    {
        HandleMovement();
        HandleJump();
        HandleAttack();
        HandleFormSwitching();
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        float yaw = camFollow != null ? camFollow.Yaw : 0f;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRot * Vector3.forward;
        Vector3 right = yawRot * Vector3.right;

        Vector3 moveDir = (forward * moveZ + right * moveX).normalized;
        bool moving = moveDir.magnitude > 0.1f;

        if (moving)
        {
            Vector3 step = moveDir * moveSpeed * Time.deltaTime;
            transform.position += WallCheck.Allowed(transform, step, wallRadius, wallHeight);
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        if (pAnim != null) pAnim.SetWalking(moving && isGrounded);
    }

    void HandleJump()
    {
        // Find the ground below. If the map has no colliders, use the starting height.
        float groundY;
        if (!TryGetGroundY(out groundY)) groundY = startY;

        isGrounded = transform.position.y <= groundY + 0.05f && verticalVelocity <= 0f;

        if (isGrounded)
        {
            verticalVelocity = 0f;
            if (Input.GetKeyDown(jumpKey))
            {
                verticalVelocity = jumpForce;
                isGrounded = false;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 p = transform.position;
        p.y += verticalVelocity * Time.deltaTime;

        // Land on the ground
        if (p.y <= groundY && verticalVelocity <= 0f)
        {
            p.y = groundY;
            verticalVelocity = 0f;
        }

        transform.position = p;
    }

    bool TryGetGroundY(out float y)
    {
        y = 0f;
        Vector3 origin = transform.position + Vector3.up * 1f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);

        bool found = false;
        float best = float.NegativeInfinity;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue; // ignore the hero itself
            if (h.point.y > best)
            {
                best = h.point.y;
                found = true;
            }
        }

        if (found) y = best;
        return found;
    }

    void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PerformAttack();
        }
    }

    void PerformAttack()
    {
        Debug.Log("Shadow Hero Attacks!");

        if (pAnim != null) pAnim.TriggerAttack();

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var hit in hits)
        {
            BossAI boss = hit.GetComponent<BossAI>();
            if (boss != null)
            {
                boss.TakeDamage(attackDamage);
                essence += 5f;
                if (essence > maxEssence) essence = maxEssence;
            }
        }
    }

    void HandleFormSwitching()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetForm(PlayerForm.Shadow);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetForm(PlayerForm.TimeWarp);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetForm(PlayerForm.Eternal);
    }

    public void SetForm(PlayerForm newForm)
    {
        currentForm = newForm;
        UpdateVisuals();
        Debug.Log("Hero Form switched to: " + currentForm);
    }

    public void EvolveForm(PlayerForm newForm)
    {
        SetForm(newForm);
        Debug.Log("Hero Evolved to: " + currentForm);
    }

    void UpdateVisuals()
    {
        if (characterRenderer == null) return;

        switch (currentForm)
        {
            case PlayerForm.Shadow:
                characterRenderer.material = shadowMat;
                moveSpeed = 8f;
                break;
            case PlayerForm.TimeWarp:
                characterRenderer.material = timeMat;
                moveSpeed = 5f;
                break;
            case PlayerForm.Eternal:
                characterRenderer.material = eternalMat;
                moveSpeed = 3f;
                break;
        }
    }

    public void TakeDamage(float amount, bool isFire)
    {
        if (currentForm == PlayerForm.Shadow && isFire) amount *= 2f;
        health -= amount;
        if (health <= 0) Debug.Log("Hero has fallen!");
    }
}
