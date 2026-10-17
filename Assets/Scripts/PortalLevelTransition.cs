using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalLevelTransition : MonoBehaviour
{
    [Header("Transition Settings")]
    public string targetSceneName = "level2";
    public float triggerDistance = 3.5f;

    private bool isTransitioning = false;
    private Transform playerTransform;

    void Start()
    {
        FindPlayer();
    }

    void Update()
    {
        if (isTransitioning) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= triggerDistance)
        {
            TriggerTransition();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isTransitioning) return;

        if (other.CompareTag("Player") || other.GetComponent<ShadowHeroController>() != null || other.name.Contains("Hero"))
        {
            TriggerTransition();
        }
    }

    void TriggerTransition()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        Debug.Log("[Portal] Entering Portal! Loading level: " + targetSceneName);
        SceneManager.LoadScene(targetSceneName);
    }

    private void FindPlayer()
    {
        var hero = FindObjectOfType<ShadowHeroController>();
        if (hero != null)
        {
            playerTransform = hero.transform;
            return;
        }
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            return;
        }
        var heroObj = GameObject.Find("Hero");
        if (heroObj != null)
        {
            playerTransform = heroObj.transform;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
    }
}
