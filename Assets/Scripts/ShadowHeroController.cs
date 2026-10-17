using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public enum PlayerForm { Shadow, TimeWarp, Eternal }

public class ShadowHeroController : MonoBehaviour
{
    [Header("General Stats")]
    public PlayerForm currentForm = PlayerForm.TimeWarp;
    public float health = 20f;
    public float maxHealth = 20f;
    public float essence = 0f;
    public float maxEssence = 100f;
    public bool isDead = false;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Walls")]
    public float wallRadius = 0.3f;
    public float wallHeight = 0.9f;

    [Header("Jump")]
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpForce = 6f;
    public float gravity = -20f;

    [Header("Visuals")]
    public Renderer characterRenderer;
    public Material shadowMat, timeMat, eternalMat;

    [Header("Combat Settings")]
    public float attackRange = 2.5f;
    public float swordDamage = 1f;       // 1 HP damage per sword hit
    public float fireDamage = 3f;        // 3 HP damage per fire charge
    public float fireCooldown = 5.0f;    // 5 seconds reload time
    public float swordCooldown = 0.22f;  // Continuous sword hitting rate

    public KeyCode swordAttackKey = KeyCode.R;
    public KeyCode fireAttackKey = KeyCode.T;

    private float swordTimer = 0f;
    private float fireTimer = 0f;

    private PlayerAnimationController pAnim;
    private CameraFollow camFollow;

    private float verticalVelocity;
    private bool isGrounded;
    private float startY;

    // Procedural Floating Health Bar (Above Head - just like enemies, NO numbers)
    private Transform hpBarRoot;
    private Transform hpBarForeground;

    // Textures for OnGUI HUD
    private Texture2D texBg;
    private Texture2D texHealth;
    private Texture2D texFire;
    private Texture2D texFireReady;

    void Awake()
    {
        pAnim = GetComponent<PlayerAnimationController>();
        if (Camera.main != null)
            camFollow = Camera.main.GetComponent<CameraFollow>();

        InitGuiTextures();
    }

    void Start()
    {
        startY = transform.position.y;
        health = maxHealth;
        UpdateVisuals();
        CreateHealthBar();
    }

    void Update()
    {
        if (isDead)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R))
            {
                RestartLevel();
            }
            return;
        }

        // Update Timers
        if (swordTimer > 0f) swordTimer -= Time.deltaTime;
        if (fireTimer > 0f) fireTimer -= Time.deltaTime;

        HandleMovement();
        HandleJump();
        HandleAttack();
        HandleFormSwitching();
        UpdateHealthBar();
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
        bool isSprinting = moving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        float currentSpeed = isSprinting ? (moveSpeed * 1.6f) : moveSpeed;

        if (moving)
        {
            Vector3 step = moveDir * currentSpeed * Time.deltaTime;
            transform.position += WallCheck.Allowed(transform, step, wallRadius, wallHeight);
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        if (pAnim != null)
        {
            pAnim.SetWalking(moving && isGrounded && !isSprinting);
            pAnim.SetRunning(moving && isGrounded && isSprinting);
        }
    }

    void HandleJump()
    {
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
            if (h.transform.IsChildOf(transform)) continue;
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
        // 1. Sword Attack via R button (or Left Mouse Button) - continuous hitting
        if ((Input.GetKey(swordAttackKey) || Input.GetMouseButton(0)) && swordTimer <= 0f)
        {
            PerformSwordAttack();
        }

        // 2. Fire Throw via T button (5 second reload time)
        if (Input.GetKeyDown(fireAttackKey))
        {
            if (fireTimer <= 0f)
            {
                PerformFireAttack();
            }
            else
            {
                Debug.Log($"Fire charge cooling down: {fireTimer:F1}s");
            }
        }
    }

    void PerformSwordAttack()
    {
        swordTimer = swordCooldown;
        Debug.Log("Hero Sword Strike!");

        if (pAnim != null) pAnim.TriggerAttack();

        Vector3 strikeCenter = transform.position + transform.forward * 1.2f + Vector3.up * 0.9f;
        Collider[] hits = Physics.OverlapSphere(strikeCenter, attackRange);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") || hit.transform.IsChildOf(transform)) continue;

            ArcherAI archer = hit.GetComponentInParent<ArcherAI>();
            if (archer != null)
            {
                archer.TakeDamage(swordDamage); // 1 HP reduction
                SpawnSwordHitSpark(hit.ClosestPoint(strikeCenter));
                essence += 5f;
                if (essence > maxEssence) essence = maxEssence;
            }

            BossAI boss = hit.GetComponentInParent<BossAI>();
            if (boss != null)
            {
                boss.TakeDamage(swordDamage); // 1 HP reduction
                SpawnSwordHitSpark(hit.ClosestPoint(strikeCenter));
                essence += 5f;
                if (essence > maxEssence) essence = maxEssence;
            }
        }
    }

    void PerformFireAttack()
    {
        fireTimer = fireCooldown; // 5 seconds
        Debug.Log("Hero Throws Fire Charge!");

        if (pAnim != null) pAnim.TriggerAttack();

        Vector3 origin = transform.position + Vector3.up * 1.2f + transform.forward * 0.8f;
        Vector3 dir = transform.forward;

        PlayerFireCharge.SpawnFireball(origin, dir, 22f, fireDamage); // 3 HP reduction
    }

    void SpawnSwordHitSpark(Vector3 pos)
    {
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "SwordSpark";
        Destroy(spark.GetComponent<Collider>());
        spark.transform.position = pos;
        spark.transform.localScale = Vector3.one * 0.35f;

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");

        var rend = spark.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(sh) { color = new Color(0.3f, 0.9f, 1f, 0.9f) };
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Destroy(spark, 0.15f);
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

    public void TakeDamage(float amount, bool isFire = false)
    {
        if (isDead) return;

        health -= amount;
        Debug.Log($"Hero hit! Damage: {amount}. Health remaining: {health}/{maxHealth}");

        if (health <= 0f)
        {
            health = 0f;
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("Hero has fallen!");

        if (pAnim != null)
        {
            pAnim.SetWalking(false);
            pAnim.SetRunning(false);
            pAnim.TriggerDeath();
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        StartCoroutine(AutoRestartRoutine());
    }

    IEnumerator AutoRestartRoutine()
    {
        yield return new WaitForSeconds(3.5f);
        RestartLevel();
    }

    void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Procedural Floating Health Bar (Above Head - just like ArcherAI, NO numbers)
    void CreateHealthBar()
    {
        hpBarRoot = new GameObject("PlayerHPBar").transform;
        hpBarRoot.SetParent(transform, false);
        hpBarRoot.localPosition = Vector3.up * 2.3f;

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(bg.GetComponent<Collider>());
        bg.transform.SetParent(hpBarRoot, false);
        bg.transform.localScale = new Vector3(1.6f, 0.2f, 1f);

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");

        var bgRend = bg.GetComponent<Renderer>();
        if (bgRend != null) bgRend.material = new Material(sh) { color = new Color(0.1f, 0.1f, 0.1f, 0.85f) };

        GameObject fg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(fg.GetComponent<Collider>());
        fg.transform.SetParent(hpBarRoot, false);
        fg.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        fg.transform.localScale = new Vector3(1.56f, 0.16f, 1f);

        var fgRend = fg.GetComponent<Renderer>();
        if (fgRend != null) fgRend.material = new Material(sh) { color = new Color(0.15f, 0.85f, 0.4f) };

        hpBarForeground = fg.transform;
    }

    void UpdateHealthBar()
    {
        if (hpBarRoot == null) return;

        if (Camera.main != null)
        {
            hpBarRoot.rotation = Quaternion.LookRotation(hpBarRoot.position - Camera.main.transform.position);
        }

        if (hpBarForeground != null)
        {
            float pct = Mathf.Clamp01(health / maxHealth);
            float fullWidth = 1.56f;
            hpBarForeground.localScale = new Vector3(fullWidth * pct, hpBarForeground.localScale.y, 1f);
            hpBarForeground.localPosition = new Vector3(-fullWidth * (1f - pct) * 0.5f, 0f, -0.01f);
        }
    }

    void InitGuiTextures()
    {
        texBg = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.08f, 0.85f));
        texHealth = MakeTex(2, 2, new Color(0.15f, 0.85f, 0.35f, 0.95f));
        texFire = MakeTex(2, 2, new Color(0.4f, 0.2f, 0.05f, 0.8f));
        texFireReady = MakeTex(2, 2, new Color(1f, 0.45f, 0.05f, 0.95f));
    }

    Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    void OnGUI()
    {
        // HUD Health Bar at Top-Left (NO numbers, purely a clean bar!)
        float pct = Mathf.Clamp01(health / maxHealth);
        float barWidth = 220f;
        float barHeight = 18f;
        float x = 25f;
        float y = 25f;

        // Health Bar Outer border / Background
        GUI.DrawTexture(new Rect(x - 2, y - 2, barWidth + 4, barHeight + 4), texBg);
        // Health Bar Fill
        if (pct > 0f)
        {
            GUI.DrawTexture(new Rect(x, y, barWidth * pct, barHeight), texHealth);
        }

        // Fire Charge Reload Bar (5 second cooldown - NO numbers, purely a progress bar!)
        float firePct = 1f - Mathf.Clamp01(fireTimer / fireCooldown);
        float fireWidth = 130f;
        float fireHeight = 7f;
        float fireY = y + barHeight + 8f;

        GUI.DrawTexture(new Rect(x - 1, fireY - 1, fireWidth + 2, fireHeight + 2), texBg);
        Texture2D fireTex = firePct >= 1f ? texFireReady : texFire;
        if (firePct > 0f)
        {
            GUI.DrawTexture(new Rect(x, fireY, fireWidth * firePct, fireHeight), fireTex);
        }

        // If player is dead, show defeat banner
        if (isDead)
        {
            GUIStyle deadStyle = new GUIStyle(GUI.skin.label);
            deadStyle.fontSize = 32;
            deadStyle.fontStyle = FontStyle.Bold;
            deadStyle.alignment = TextAnchor.MiddleCenter;
            deadStyle.normal.textColor = new Color(0.95f, 0.2f, 0.2f, 1f);

            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 50), "YOU DIED", deadStyle);

            GUIStyle restartStyle = new GUIStyle(GUI.skin.label);
            restartStyle.fontSize = 18;
            restartStyle.alignment = TextAnchor.MiddleCenter;
            restartStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.4f + 50, Screen.width, 30), "Press SPACE or R to Restart", restartStyle);
        }
    }
}
