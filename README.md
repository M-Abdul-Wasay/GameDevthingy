# GamedevThingy - Unity Base Structure

## Folder Structure
```
/Assets
    /Scripts          - C# scripts (Player, Health, Boss, etc.)
    /Scenes           - Unity scenes (Tutorial, Graveyard, Castle, etc.)
    /Prefabs          - Reusable GameObjects (Player, Enemies, Bosses, Items)
    /Animations       - Animator controllers, animation clips
    /Audio            - Sound effects, music
    /Art              - Sprites, textures, tilemaps
    /Materials        - Materials for lighting/shaders
```

## Core Systems
- **PlayerController.cs** – basic movement & jumping (Rigidbody2D).
- **HealthSystem.cs** – health UI, death, and time‑travel respawn (fixed 3 s).
- **HealthBar.cs** – simple UI slider for health (player & bosses).
- **LifeDrop.cs** – collectible that heals the player.
- **EnemyAI.cs** – simple enemy that takes damage and drops life on death.
- **BossBase.cs** – abstract boss health & death handling (gives clue on defeat).
- **GameManager.cs** – singleton tracking bosses defeated, clues, and final reveal.

## How to Extend
1. **Scenes** – Create scenes for each area (Tutorial, Graveyard, Castle) and add tilemaps, backgrounds, and spawn points.
2. **Prefabs** – Build Player prefab (sprite + PlayerController + HealthSystem), Enemy prefab, Boss prefabs (inherit from BossBase and add fire attacks, patterns).
3. **Clues** – In each boss’s `OnBossDefeated()` override, call `GameManager.Instance.RegisterBossDefeated("your clue text");`.
4. **Final Boss** – Use a future‑self boss that inherits BossBase and triggers the ending when defeated.
5. **Time Travel Bar** – Optionally add a UI fill that shows respawn timer (bind to HealthSystem.respawnTimer).

## Next Steps
- Import placeholder art or use Unity’s built‑in sprites.
- Set up UI Canvas with HealthSlider and RespawnTimer.
- Implement boss attack patterns (fire projectiles, shadow attacks, time‑based mechanics).
- Add dialogue or cutscenes to reveal story.
- Polish with audio, particle effects, and polishing.

Enjoy building your omen‑shadow‑time game!