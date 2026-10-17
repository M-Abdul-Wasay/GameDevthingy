using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ArcherAI : MonoBehaviour
{
    [Header("Archer Identity")]
    public bool isBoss = false;
    public string characterName = "Shadow Archer";

    [Header("Health")]
    public float maxHealth = 20f;
    public float currentHealth = 20f;

    [Header("Combat Settings")]
    public float shootRange = 35f;
    public float shootInterval = 3f;
    public float arrowDamage = 2f;
    public float arrowSpeed = 22f;

    [Header("Reposition / Walk Settings")]
    public float walkIntervalMin = 5f;
    public float walkIntervalMax = 8f;
    public float walkDuration = 1.6f;
    public float moveSpeed = 3f;

    [Header("Boss Specifics")]
    public GameObject minionPrefab;
    private int spawnPhase = 0;
    private static List<ArcherAI> activeMinions = new List<ArcherAI>();

    private Animator anim;
    private Transform playerTransform;
    private bool isDead = false;
    private bool isWalking = false;
    private bool isAttacking = false;

    private float shootTimer = 0f;
    private float nextWalkTime = 0f;
    private Vector3 walkTargetPos;
    private float walkTimer = 0f;

    // Procedural HP bar
    private Transform hpBarForeground;
    private Transform hpBarRoot;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        if (isBoss)
        {
            characterName = "Shadowthorn Huntress";
            maxHealth = 20f;
            arrowDamage = 2f;
            shootInterval = 2.2f;
            moveSpeed = 3.5f;
        }
        currentHealth = maxHealth;
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        nextWalkTime = Time.time + Random.Range(walkIntervalMin, walkIntervalMax);
        shootTimer = Random.Range(0.5f, 1.5f);

        CreateHealthBar();

        if (!isBoss)
        {
            activeMinions.Add(this);
        }
    }

    void OnDestroy()
    {
        if (!isBoss)
        {
            activeMinions.Remove(this);
        }
    }

    void Update()
    {
        if (isDead) return;

        UpdateHealthBar();

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (isWalking)
        {
            HandleWalking();
        }
        else
        {
            HandleStationary(distToPlayer);
        }

        if (isBoss)
        {
            CheckBossSpawns();
        }
    }

    void HandleStationary(float distToPlayer)
    {
        // Smoothly face player
        Vector3 lookDir = (playerTransform.position - transform.position);
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6f);
        }

        // Check if it's time to take a short reposition walk
        if (Time.time >= nextWalkTime && !isAttacking)
        {
            StartWalk();
            return;
        }

        // Shooting logic while stationary
        if (distToPlayer <= shootRange && !isAttacking)
        {
            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f)
            {
                shootTimer = shootInterval;
                StartCoroutine(ShootArrowRoutine());
            }
        }
    }

    void StartWalk()
    {
        isWalking = true;
        walkTimer = walkDuration;
        if (anim != null) anim.SetBool("isWalking", true);

        // Pick a small reposition offset (strafing left or right, or stepping slightly back)
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        float strafe = Random.Range(-4f, 4f);
        float step = Random.Range(-2f, 3f);
        walkTargetPos = transform.position + right * strafe + forward * step;

        // Keep target within reasonable castle ground
        walkTargetPos.y = transform.position.y;
    }

    void HandleWalking()
    {
        walkTimer -= Time.deltaTime;

        Vector3 moveDir = (walkTargetPos - transform.position);
        moveDir.y = 0f;

        if (moveDir.magnitude > 0.3f && walkTimer > 0f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            transform.position += moveDir.normalized * moveSpeed * Time.deltaTime;
        }
        else
        {
            StopWalk();
        }
    }

    void StopWalk()
    {
        isWalking = false;
        if (anim != null) anim.SetBool("isWalking", false);
        nextWalkTime = Time.time + Random.Range(walkIntervalMin, walkIntervalMax);
    }

    IEnumerator ShootArrowRoutine()
    {
        isAttacking = true;
        if (anim != null) anim.SetTrigger("Attack");

        // Wait for release frame
        yield return new WaitForSeconds(0.4f);

        if (!isDead && playerTransform != null)
        {
            Vector3 origin = transform.position + Vector3.up * (isBoss ? 2.2f : 1.3f) + transform.forward * 0.8f;
            Vector3 target = playerTransform.position;
            ArrowProjectile.CreateDefaultArrow(origin, target, arrowSpeed, arrowDamage, isBoss);
        }

        yield return new WaitForSeconds(0.6f);
        isAttacking = false;
    }

    void CheckBossSpawns()
    {
        float hpPercent = currentHealth / maxHealth;
        if (spawnPhase == 0 && hpPercent <= 0.75f)
        {
            spawnPhase = 1;
            SpawnMinions(2);
        }
        else if (spawnPhase == 1 && hpPercent <= 0.40f)
        {
            spawnPhase = 2;
            SpawnMinions(2);
        }
    }

    public void SpawnMinions(int count)
    {
        Debug.Log($"Boss summoning {count} archer minions!");
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = (i == 0 ? transform.right * 4.5f : -transform.right * 4.5f) + transform.forward * 2f;
            Vector3 spawnPos = transform.position + offset;
            spawnPos.y = transform.position.y;

            GameObject minion = Instantiate(gameObject, spawnPos, transform.rotation);
            minion.name = $"ArcherMinion_{i + 1}";
            minion.transform.localScale = Vector3.one * 1.0f; // Player sized minion

            ArcherAI minionAI = minion.GetComponent<ArcherAI>();
            if (minionAI != null)
            {
                minionAI.isBoss = false;
                minionAI.maxHealth = 20f;
                minionAI.currentHealth = 20f;
                minionAI.characterName = "Archer Minion";
                minionAI.arrowDamage = 2f;
                minionAI.shootInterval = 3.2f;
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"{characterName} took {amount} dmg! Remaining HP: {currentHealth}");

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            anim.SetTrigger("Die");
        }

        // Disable colliders
        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;

        if (hpBarRoot != null) Destroy(hpBarRoot.gameObject);

        if (isBoss)
        {
            Debug.Log("Archer Boss Defeated! Level 2 Complete!");
            GameManager.Instance?.DefeatBoss();
        }

        Destroy(gameObject, 6f);
    }

    void CreateHealthBar()
    {
        hpBarRoot = new GameObject("ArcherHPBar").transform;
        hpBarRoot.SetParent(transform, false);
        hpBarRoot.localPosition = Vector3.up * (isBoss ? 4.2f : 2.4f);

        // Background
        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(bg.GetComponent<Collider>());
        bg.transform.SetParent(hpBarRoot, false);
        bg.transform.localScale = isBoss ? new Vector3(2.5f, 0.28f, 1f) : new Vector3(1.4f, 0.16f, 1f);

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");

        var bgRend = bg.GetComponent<Renderer>();
        if (bgRend != null) bgRend.material = new Material(sh) { color = new Color(0.1f, 0.1f, 0.1f, 0.85f) };

        // Foreground
        GameObject fg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(fg.GetComponent<Collider>());
        fg.transform.SetParent(hpBarRoot, false);
        fg.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        fg.transform.localScale = isBoss ? new Vector3(2.46f, 0.24f, 1f) : new Vector3(1.36f, 0.13f, 1f);

        var fgRend = fg.GetComponent<Renderer>();
        Color barColor = isBoss ? new Color(0.9f, 0.2f, 0.25f) : new Color(0.95f, 0.75f, 0.15f);
        if (fgRend != null) fgRend.material = new Material(sh) { color = barColor };

        hpBarForeground = fg.transform;
    }

    void UpdateHealthBar()
    {
        if (hpBarRoot == null) return;

        // Billboarding towards main camera
        if (Camera.main != null)
        {
            hpBarRoot.rotation = Quaternion.LookRotation(hpBarRoot.position - Camera.main.transform.position);
        }

        if (hpBarForeground != null)
        {
            float pct = Mathf.Clamp01(currentHealth / maxHealth);
            float fullWidth = isBoss ? 2.46f : 1.36f;
            hpBarForeground.localScale = new Vector3(fullWidth * pct, hpBarForeground.localScale.y, 1f);
            hpBarForeground.localPosition = new Vector3(-fullWidth * (1f - pct) * 0.5f, 0f, -0.01f);
        }
    }
}
