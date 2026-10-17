using UnityEngine;

// Small helper so characters can't walk through walls.
// Used by the hero, the boss and the minions.
public static class WallCheck
{
    // Returns the part of a horizontal move that does NOT run into a wall.
    // If you hit a wall at an angle, you slide along it.
    public static Vector3 Allowed(Transform self, Vector3 delta, float radius, float height)
    {
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0000001f) return Vector3.zero;

        if (!Blocked(self, delta, radius, height)) return delta;

        Vector3 onlyX = new Vector3(delta.x, 0f, 0f);
        if (onlyX.sqrMagnitude > 0.0000001f && !Blocked(self, onlyX, radius, height)) return onlyX;

        Vector3 onlyZ = new Vector3(0f, 0f, delta.z);
        if (onlyZ.sqrMagnitude > 0.0000001f && !Blocked(self, onlyZ, radius, height)) return onlyZ;

        return Vector3.zero;
    }

    static bool Blocked(Transform self, Vector3 delta, float radius, float height)
    {
        Vector3 origin = self.position + Vector3.up * height;
        float dist = delta.magnitude + 0.05f;

        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, delta.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.distance <= 0f) continue;                 // already overlapping: don't get stuck
            if (h.normal.y > 0.6f) continue;                // floors and gentle slopes are not walls
            if (h.transform == self || h.transform.IsChildOf(self) || self.IsChildOf(h.transform)) continue;
            if (h.collider.GetComponentInParent<ShadowHeroController>() != null) continue;
            if (h.collider.GetComponentInParent<MinionSkeleton>() != null) continue;
            if (h.collider.GetComponentInParent<BossSkills>() != null) continue;
            return true;
        }
        return false;
    }
}
