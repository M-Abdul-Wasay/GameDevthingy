#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Put this in Assets/Editor. Drag the COLOSSUS ANIMATION FBX (Meshy_AI_Voidforge_Colossus_biped_Animation_all_frame_rate_60)
// into the scene, select its top object, then Tools > Build Boss 3 Animator.
//
// That FBX has 7 clips but only Running has real motion (the others are flat poses in the file).
// So: Running is used for movement (its playback speed follows the boss speed, smoothly),
// Combat_Stance is the standing pose. ColossusBoss does the jump, lava and body motion in code.
public static class BuildBoss3Animator
{
    [MenuItem("Tools/Build Boss 3 Animator")]
    public static void Build()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogError("Select the Boss 3 TOP object in the Hierarchy first.");
            return;
        }

        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        if (root == null) root = go;

        var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
        string fbxPath = source != null ? AssetDatabase.GetAssetPath(source) : "";
        if (string.IsNullOrEmpty(fbxPath) || !fbxPath.ToLower().EndsWith(".fbx"))
        {
            Debug.LogError("Selected object is not an FBX instance. Drag the Colossus ANIMATION FBX into the scene and select its top object.");
            return;
        }

        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        // 1. clip list = what is really inside the FBX, loop Running / Walking
        var anims = importer.defaultClipAnimations;
        foreach (var a in anims)
        {
            string n = a.name.ToLower();
            a.loopTime = n.Contains("running") || n.Contains("walking");
        }
        importer.clipAnimations = anims;
        importer.SaveAndReimport();

        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var avatar = assets.OfType<Avatar>().FirstOrDefault();

        Debug.Log("Clips inside " + System.IO.Path.GetFileName(fbxPath) + ":\n" +
                  string.Join("\n", clips.Select(c => Short(c.name) + (IsFlat(c) ? "   <-- FLAT (no motion in the file)" : "   (has motion)")).ToArray()));

        AnimationClip run = Pick(clips, "running", "run", "walking", "walk");
        AnimationClip pose = Pick(clips, "idle", "combat_stance", "stance", "walking");

        if (run == null)
        {
            Debug.LogError("No Running or Walking clip found. Read the clip list above.");
            return;
        }
        bool poseFrozen = false;
        if (pose == null) { pose = run; poseFrozen = true; }
        else if (IsFlat(pose)) poseFrozen = true;

        // 2. controller: Idle <-> Run driven by Speed, run clip speed driven by RunSpeed
        string folder = System.IO.Path.GetDirectoryName(fbxPath).Replace("\\", "/");
        string controllerPath = folder + "/Boss3Controller.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "RunSpeed",
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });

        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState("Idle");
        idleState.motion = pose;
        if (poseFrozen) idleState.speed = 0f;

        var runState = sm.AddState("Run");
        runState.motion = run;
        runState.speedParameterActive = true;
        runState.speedParameter = "RunSpeed";

        sm.defaultState = idleState;

        var toRun = idleState.AddTransition(runState);
        toRun.hasExitTime = false;
        toRun.duration = 0.25f;
        toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var toIdle = runState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.25f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // 3. assign
        var animator = root.GetComponent<Animator>();
        if (animator == null) animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        if (avatar != null) animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Debug.Log("Boss 3 controller built: " + controllerPath +
                  "\nStand pose: " + Short(pose.name) + (poseFrozen ? " (frozen)" : "") +
                  " | Move: " + Short(run.name));
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
