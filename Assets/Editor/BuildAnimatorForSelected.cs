#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Put this file in Assets/Editor. It is a menu tool, NOT a component.
public static class BuildAnimatorForSelected
{
    [MenuItem("Tools/Build Animator For Selected")]
    public static void Build()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogError("Select the character's TOP object in the Hierarchy first.");
            return;
        }

        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        if (root == null) root = go;

        var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
        string fbxPath = source != null ? AssetDatabase.GetAssetPath(source) : "";
        if (string.IsNullOrEmpty(fbxPath) || !fbxPath.ToLower().EndsWith(".fbx"))
        {
            Debug.LogError("Selected object is not an FBX instance. Select the TOP object of the model that came from the animation FBX.");
            return;
        }

        // 1. Read clip names and pick the ones we need
        var firstLoad = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var names = firstLoad.OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .Select(c => c.name).ToArray();
        Debug.Log("Clips in " + fbxPath + ":\n" + string.Join("\n", names));

        string idleName = FindName(names, "idle");
        string walkName = FindName(names, "walking", "walk", "running", "run");
        string attackName = FindName(names, "attack", "combo", "punch", "slash", "swing", "kick");

        if (idleName == null || walkName == null)
        {
            Debug.LogError("Could not find an idle or walk clip. Check the clip list printed above.");
            return;
        }

        // 2. Loop Time on idle and walk
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            var anims = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            foreach (var a in anims)
                a.loopTime = (a.name == idleName || a.name == walkName);
            importer.clipAnimations = anims;
            importer.SaveAndReimport();
        }

        // 3. Reload clips and avatar after reimport
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var avatar = assets.OfType<Avatar>().FirstOrDefault();
        AnimationClip Get(string n)
        {
            return n == null ? null : assets.OfType<AnimationClip>().FirstOrDefault(c => c.name == n);
        }
        var idle = Get(idleName);
        var walk = Get(walkName);
        var attack = Get(attackName);

        // 4. Build the controller next to the FBX
        string folder = System.IO.Path.GetDirectoryName(fbxPath).Replace("\\", "/");
        string controllerPath = folder + "/" + root.name + "_Controller.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        var walkState = sm.AddState("Walk");
        walkState.motion = walk;
        sm.defaultState = idleState;

        var toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.15f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");

        var toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");

        if (attack != null)
        {
            var attackState = sm.AddState("Attack");
            attackState.motion = attack;

            var toAttack = sm.AddAnyStateTransition(attackState);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;
            toAttack.canTransitionToSelf = false;
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var back = attackState.AddTransition(idleState);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.1f;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // 5. Assign to the selected object
        var animator = root.GetComponent<Animator>();
        if (animator == null) animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        if (avatar != null) animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Debug.Log("Built " + controllerPath + " | Idle: " + idleName + " | Walk: " + walkName +
                  " | Attack: " + (attackName ?? "none found"));
    }

    static string FindName(string[] names, params string[] keys)
    {
        foreach (var k in keys)
        {
            var n = names.FirstOrDefault(x => x.ToLower().Contains(k));
            if (n != null) return n;
        }
        return null;
    }
}
#endif
