#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Put this file in Assets/Editor. It is a menu tool, NOT a component.
// Select your MAP pieces in the Hierarchy (not the hero or boss), then click
// Tools > Add Mesh Colliders To Selected. It gives every map mesh a solid collider.
public static class AddMeshColliders
{
    [MenuItem("Tools/Add Mesh Colliders To Selected")]
    public static void Run()
    {
        int added = 0;
        foreach (var go in Selection.gameObjects)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue;

                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                added++;
            }
        }
        Debug.Log("Added " + added + " mesh colliders.");
    }
}
#endif
