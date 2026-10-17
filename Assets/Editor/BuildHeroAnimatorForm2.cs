#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Put this in Assets/Editor. It does NOT replace any of your other tools or controllers.
// Select the Form 2 hero's TOP object (the one dragged in from the Direwolf animation FBX), then
// Tools > Build Hero Animator Form 2.
//
// What it fixes:
//  - resets the FBX clip list to the takes that are REALLY inside the file (a copied .meta had old clip names)
//  - builds ONE controller using clips from ONE FBX only (no mixing Humanoid and Generic clips)
//  - logs which clips are FLAT (no motion) so you know what really animates
public static class BuildHeroAnimatorForm2
{
    [MenuItem("Tools/Build Hero Animator Form 2")]
    public static void Build()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogError("Select the Form 2 hero's TOP object in the Hierarchy first.");
            return;
        }

        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        if (root == null) root = go;

        var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
        string fbxPath = source != null ? AssetDatabase.GetAssetPath(source) : "";
        if (string.IsNullOrEmpty(fbxPath) || !fbxPath.ToLower().EndsWith(".fbx"))
        {
            Debug.LogError("Selected object is not an FBX instance. Drag the Direwolf ANIMATION FBX into the scene and select its top object.");
            return;
        }

        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        // 1. Replace the clip list with the takes that really exist in this FBX
        var anims = importer.defaultClipAnimations;
        foreach (var a in anims)
        {
            string n = a.name.ToLower();
            a.loopTime = n.Contains("idle") || n.Contains("running") || n.Contains("walking");
        }
        importer.clipAnimations = anims;
        importer.SaveAndReimport();

        // 2. Load clips and avatar
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var avatar = assets.OfType<Avatar>().FirstOrDefault();

        Debug.Log("Clips inside " + System.IO.Path.GetFileName(fbxPath) + ":\n" +
                  string.Join("\n", clips.Select(c => Short(c.name) + (IsFlat(c) ? "   <-- FLAT (no motion in the file)" : "   (has motion)")).ToArray()));

        if (clips.Length <= 1)
        {
            Debug.LogError("The FBX " + fbxPath + " has only " + clips.Length + " animation clip(s). " +
                           "This is probably the CHARACTER FBX (no animations). Delete this hero from the scene, drag the " +
                           "Direwolf ANIMATION FBX (the one ending in Animation_all_frame_rate_60) into the scene, select it and run this again.");
            return;
        }

        AnimationClip idle = Pick(clips, "idle", "stand", "freetime", "unsteady");
        AnimationClip run = Pick(clips, "running", "run");
        AnimationClip walk = Pick(clips, "walking", "walk");
        AnimationClip attack = Pick(clips, "double_combo", "attack", "combo", "punch");
        AnimationClip dead = Pick(clips, "dead", "die", "death");

        // Never stop just because one clip is missing: fall back and say so.
        bool frozenIdle = false;
        if (idle == null)
        {
            idle = run != null ? run : (walk != null ? walk : clips[0]);
            frozenIdle = true;
            Debug.LogWarning("No idle clip in " + System.IO.Path.GetFileName(fbxPath) + ". Using '" + Short(idle.name) + "' frozen as the idle pose.");
        }
        if (run == null && walk == null)
        {
            walk = idle;
            Debug.LogWarning("No walk or run clip in " + System.IO.Path.GetFileName(fbxPath) + ". The moving states will reuse the idle clip.");
        }

        // The Walking clip is often flat, so the Walking state borrows the Running clip when there is one.
        AnimationClip walkMotion = run != null ? run : walk;
        AnimationClip runMotion = run != null ? run : walk;

        // 3. Build the controller (new file, your old controllers are not touched)
        string folder = System.IO.Path.GetDirectoryName(fbxPath).Replace("\\", "/");
        string controllerPath = folder + "/HeroController_Form2_Fixed.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        if (frozenIdle) idleState.speed = 0f;
        var walkState = sm.AddState("Walking");
        walkState.motion = walkMotion;
        if (run != null) walkState.speed = 0.7f;
        var runState = sm.AddState("Running");
        runState.motion = runMotion;
        sm.defaultState = idleState;

        var t = Link(idleState, walkState);
        t.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "isRunning");

        t = Link(idleState, runState);
        t.AddCondition(AnimatorConditionMode.If, 0, "isRunning");

        t = Link(walkState, idleState);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "isRunning");

        t = Link(walkState, runState);
        t.AddCondition(AnimatorConditionMode.If, 0, "isRunning");

        t = Link(runState, walkState);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "isRunning");
        t.AddCondition(AnimatorConditionMode.If, 0, "isWalking");

        t = Link(runState, idleState);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "isRunning");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");

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

        if (dead != null)
        {
            var deadState = sm.AddState("Dead");
            deadState.motion = dead;

            var toDead = sm.AddAnyStateTransition(deadState);
            toDead.hasExitTime = false;
            toDead.duration = 0.1f;
            toDead.canTransitionToSelf = false;
            toDead.AddCondition(AnimatorConditionMode.If, 0, "Die");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // 4. Assign to the selected object
        var animator = root.GetComponent<Animator>();
        if (animator == null) animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        if (avatar != null) animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Debug.Log("Form 2 controller built: " + controllerPath +
                  "\nIdle: " + Short(idle.name) + (frozenIdle ? " (frozen)" : "") +
                  " | Walk/Run: " + (run != null ? Short(run.name) : Short(walk.name)) +
                  " | Attack: " + (attack != null ? Short(attack.name) : "none") +
                  " | Dead: " + (dead != null ? Short(dead.name) : "none"));
    }

    static AnimatorStateTransition Link(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.12f;
        return t;
    }

    static AnimationClip Pick(AnimationClip[] clips, params string[] keys)
    {
        foreach (var k in keys)
        {
            var c = clips.FirstOrDefault(x => x.name.ToLower().Contains(k));
            if (c != null) return c;
        }
        return null;
    }

    static string Short(string n)
    {
        int i = n.LastIndexOf('|');
        return i >= 0 ? n.Substring(i + 1) : n;
    }

    // true when no curve in the clip ever changes value (the clip would look frozen)
    static bool IsFlat(AnimationClip clip)
    {
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            var c = AnimationUtility.GetEditorCurve(clip, b);
            if (c == null || c.length == 0) continue;

            float min = c[0].value, max = c[0].value;
            for (int i = 1; i < c.length; i++)
            {
                min = Mathf.Min(min, c[i].value);
                max = Mathf.Max(max, c[i].value);
            }
            if (max - min > 0.001f) return false;
        }
        return true;
    }
}
#endif
