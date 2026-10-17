using UnityEngine;
// using UnityEngine.UI; // Commented out to prevent compile errors until UI package is installed

public class HealthSystem : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    // public Slider healthSlider; // Assign in inspector

    [Header("Respawn")]
    public float respawnTime = 3f; // seconds to travel back in time
    [Tooltip("ON = you come back exactly where you died. OFF = you come back at the checkpoint / start position")]
    public bool respawnWhereDied = true;
    [Tooltip("If you die falling this far below solid ground, you come back on the last safe ground instead")]
    public float fallDeathDepth = 8f;
    [Tooltip("Seconds you can't be hurt after coming back")]
    public float respawnInvulnerability = 2f;

    private float respawnTimer;
    private bool isRespawning;

    private Vector3 deathPosition;
    private Quaternion deathRotation;
    private Vector3 lastSafePosition;
    private Vector3 checkpointPosition;
    private bool hasCheckpoint;
    private float nextSafeCheck;
    private float invulnerableUntil;

    public bool IsDead { get { return isRespawning; } }

    void Start()
    {
        currentHealth = maxHealth;
        lastSafePosition = transform.position;
        UpdateHealthUI();
    }

    public void TakeDamage(int damage)
    {
        if (isRespawning) return;
        if (Time.time < invulnerableUntil) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        UpdateHealthUI();

        if (currentHealth <= 0 && !isRespawning)
        {
            Die();
        }
    }

    /// <summary>Call this from a checkpoint to make it the respawn point (when respawnWhereDied is OFF).</summary>
    public void SetCheckpoint(Vector3 position)
    {
        checkpointPosition = position;
        hasCheckpoint = true;
    }

    void Die()
    {
        Debug.Log("Player died. Initiating time travel respawn...");
        isRespawning = true;
        respawnTimer = respawnTime;

        // remember WHERE we died
        deathPosition = transform.position;
        deathRotation = transform.rotation;

        ShadowHeroController pc = GetComponent<ShadowHeroController>();
        if (pc != null) pc.enabled = false;
    }

    void Update()
    {
        if (isRespawning)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                Respawn();
            }
            return;
        }

        RememberSafeGround();
    }

    // remembers the last place you stood on solid ground (used if you die falling into a pit)
    void RememberSafeGround()
    {
        if (Time.time < nextSafeCheck) return;
        nextSafeCheck = Time.time + 0.5f;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            lastSafePosition = transform.position;
    }

    void Respawn()
    {
        Vector3 target;
        if (respawnWhereDied)
        {
            target = deathPosition;
            // fell into a pit? come back on the last safe ground instead
            if (deathPosition.y < lastSafePosition.y - fallDeathDepth) target = lastSafePosition;
        }
        else
        {
            target = hasCheckpoint ? checkpointPosition : Vector3.zero;
        }

        MoveTo(target + Vector3.up * 0.1f, respawnWhereDied ? deathRotation : transform.rotation);

        currentHealth = maxHealth;
        UpdateHealthUI();
        isRespawning = false;
        invulnerableUntil = Time.time + respawnInvulnerability;

        ShadowHeroController pc = GetComponent<ShadowHeroController>();
        if (pc != null) pc.enabled = true;
        Debug.Log("Player respawned via time travel!");
    }

    // a CharacterController (or Rigidbody) would undo a plain transform.position change
    void MoveTo(Vector3 pos, Quaternion rot)
    {
        CharacterController cc = GetComponent<CharacterController>();
        bool ccWasOn = cc != null && cc.enabled;
        if (ccWasOn) cc.enabled = false;

        transform.position = pos;
        transform.rotation = rot;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (ccWasOn) cc.enabled = true;
        Physics.SyncTransforms();
    }

    void UpdateHealthUI()
    {
        /*
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        */
    }
}
