using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int bossesDefeated = 0;

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }
    }

    public void DefeatBoss() {
        bossesDefeated++;
        Debug.Log("Boss Defeated! Total: " + bossesDefeated);

        // Find the player and evolve them
        ShadowHeroController player = GameObject.FindGameObjectWithTag("Player")?.GetComponent<ShadowHeroController>();
        if (player != null) {
            if (bossesDefeated == 1) {
                player.EvolveForm(PlayerForm.TimeWarp);
                Debug.Log("PLAYER EVOLVED TO TIMEWARP FORM!");
            } else if (bossesDefeated == 2) {
                player.EvolveForm(PlayerForm.Eternal);
                Debug.Log("PLAYER EVOLVED TO ETERNAL FORM!");
            } else {
                Debug.Log("ALL BOSSES DEFEATED! YOU ARE THE ULTIMATE SHADOW!");
            }
        }

        // Optional: Load next scene after a delay
        // Invoke("LoadNextScene", 3f);
    }

    void LoadNextScene() {
        // Logic to load next level
    }
}
