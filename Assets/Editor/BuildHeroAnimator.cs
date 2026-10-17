#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BuildHeroAnimator
{
    [MenuItem("Tools/Build Hero Animator")]
    public static void Build()
    {
        // 1. Find the animation FBX
        string[] guids = AssetDatabase.FindAssets("Animation_all_frame_rate_60 t:Model");
        if (guids.Length == 0)
        {
            Debug.LogError("Could not find the Animation_all_frame_rate_60 FBX in the project.");
            return;
        }
        string fbxPath = AssetDatabase.GUIDToAssetPath(guids[0]);

        // 2. Set Loop Time on Idle / Walking / Running only
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            var anims = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            foreach (var a in anims)
            {
                bool loop = a.name.EndsWith("Idle_3") || a.name.EndsWith("Running") || a.name.EndsWith("Walking");
                a.loopTime = loop;
            }
            importer.clipAnimations = anims;
            importer.SaveAndReimport();
        }

        // 3. Load clips and avatar
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var avatar = assets.OfType<Avatar>().FirstOrDefault();

        AnimationClip idle = clips.FirstOrDefault(c => c.name.EndsWith("Idle_3"));
        AnimationClip run = clips.FirstOrDefault(c => c.name.EndsWith("Running"));
        AnimationClip attack = clips.FirstOrDefault(c => c.name.Contains("Double_Combo"));

        if (idle == null || run == null || attack == null)
        {
            Debug.LogError("Missing clip. Idle_3: " + (idle != null) + "  Running: " + (run != null) + "  Double_Combo: " + (attack != null));
            return;
        }

        // 4. Build the controller
        string controllerPath = "Assets/Main Character form 1/HeroController.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        var runState = sm.AddState("Running");
        runState.motion = run;
        var attackState = sm.AddState("Attack");
        attackState.motion = attack;
        sm.defaultState = idleState;

        var toRun = idleState.AddTransition(runState);
        toRun.hasExitTime = false;
        toRun.duration = 0.1f;
        toRun.AddCondition(AnimatorConditionMode.If, 0, "isWalking");

        var toIdle = runState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.1f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");

        var toAttack = sm.AddAnyStateTransition(attackState);
        toAttack.hasExitTime = false;
        toAttack.duration = 0.05f;
        toAttack.canTransitionToSelf = false;
        toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        var attackBack = attackState.AddTransition(idleState);
        attackBack.hasExitTime = true;
        attackBack.exitTime = 0.9f;
        attackBack.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // 5. Assign to the selected object
        var go = Selection.activeGameObject;
        if (go != null)
        {
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = false;
            Debug.Log("Hero Animator built and assigned to " + go.name);
        }
        else
        {
            Debug.Log("Controller built at " + controllerPath + ". Select your Hero and assign it to the Animator.");
        }
    }
}
#endif
