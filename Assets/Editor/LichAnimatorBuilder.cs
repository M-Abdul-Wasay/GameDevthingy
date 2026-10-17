#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Put this file in Assets/Editor (replace the old one). It is a menu tool, NOT a component.
// Select the boss's TOP object in the Hierarchy, then click Tools > Build Boss Animator.
// It scans EVERY fbx in the boss folder for clips, so animations split over several files work.
public static class LichAnimatorBuilder
{
    const float WalkThreshold = 0.1f;
    const float RunThreshold = 3.5f;

    class ClipInfo
    {
        public string name;
        public string path;
        public string Key { get { return path + "|" + name; } }
    }

    [MenuItem("Tools/Build Lich Animator")]
    public static void Build()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogError("Select the boss's TOP object in the Hierarchy first.");
            return;
        }

        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        if (root == null) root = go;

        var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
        string bossFbx = source != null ? AssetDatabase.GetAssetPath(source) : "";
        if (string.IsNullOrEmpty(bossFbx) || !bossFbx.ToLower().EndsWith(".fbx"))
        {
            Debug.LogError("Selected object is not an FBX instance. Select the boss's TOP object.");
            return;
        }

        string folder = System.IO.Path.GetDirectoryName(bossFbx).Replace("\\", "/");

        // 1. Collect clips from every FBX in the boss folder
        var infos = new List<ClipInfo>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.ToLower().EndsWith(".fbx")) continue;
            if (p != bossFbx) continue; // only use the selected boss's own FBX clips
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())
            {
                if (c.name.StartsWith("__preview__")) continue;
                infos.Add(new ClipInfo { name = c.name, path = p });
            }
        }

        // Clips from the selected boss's OWN fbx come first, so another boss's clips are only a fallback
        infos = infos.OrderBy(i => i.path == bossFbx ? 0 : 1).ToList();

        Debug.Log("ALL clips found in " + folder + ":\n" +
                  string.Join("\n", infos.Select(i => System.IO.Path.GetFileName(i.path) + "  ->  " + Short(i.name)).ToArray()));

        // "unsteady" is used as the idle when there is no real idle clip (swaying stand-in)
        var idle = Find(infos, "freetime", "idle", "stand", "unsteady");
        var walk = Find(infos, "walk");
        var run = Find(infos, "run");
        var skill1 = Find(infos, "skill1", "skill01");
        var skill3 = Find(infos, "skill3", "skill03", "punch", "combo");
        var die = Find(infos, "die", "dead", "death");

        if (walk == null) walk = run; // fall back to the run clip if there is no walk clip

        // Last resort: still no idle -> reuse the walk clip, frozen
        bool frozenIdle = false;
        if (idle == null && walk != null)
        {
            idle = walk;
            frozenIdle = true;
        }

        if (idle == null || walk == null)
        {
            Debug.LogError("Could not find an idle or walk clip in any FBX in " + folder +
                           ". Read the ALL clips list above and tell me the names.");
            return;
        }

        // 2. Loop Time only on idle / walk / run, and warn if a file is not Humanoid
        var loopKeys = new HashSet<string> { idle.Key, walk.Key };
        if (run != null) loopKeys.Add(run.Key);

        var picked = new List<ClipInfo> { idle, walk, run, skill1, skill3, die }
            .Where(c => c != null).ToList();

        foreach (string path in picked.Select(c => c.path).Distinct())
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            if (importer.animationType != ModelImporterAnimationType.Human)
                Debug.LogWarning(System.IO.Path.GetFileName(path) +
                    " is not Humanoid. Set Rig > Animation Type = Humanoid, Avatar Definition = Copy From Other Avatar, Source = the boss avatar.");

            var anims = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;

            foreach (var a in anims)
            {
                string key = path + "|" + a.name;
                if (loopKeys.Contains(key)) a.loopTime = true;
                else if (picked.Any(c => c.Key == key)) a.loopTime = false;
            }
            importer.clipAnimations = anims;
            importer.SaveAndReimport();
        }

        // 3. Reload everything after reimport
        var avatar = AssetDatabase.LoadAllAssetsAtPath(bossFbx).OfType<Avatar>().FirstOrDefault();

        // 4. Build the controller
        string controllerPath = folder + "/" + root.name + "_BossController.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Skill1", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Skill3", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState("Idle");
        idleState.motion = Load(idle);
        if (frozenIdle) idleState.speed = 0f; // freeze the walk pose as a standing idle
        var walkState = sm.AddState("Walk");
        walkState.motion = Load(walk);
        sm.defaultState = idleState;

        var iw = idleState.AddTransition(walkState);
        iw.hasExitTime = false;
        iw.duration = 0.15f;
        iw.AddCondition(AnimatorConditionMode.Greater, WalkThreshold, "Speed");

        var wi = walkState.AddTransition(idleState);
        wi.hasExitTime = false;
        wi.duration = 0.15f;
        wi.AddCondition(AnimatorConditionMode.Less, WalkThreshold, "Speed");

        if (run != null && run != walk)
        {
            var runState = sm.AddState("Run");
            runState.motion = Load(run);

            var wr = walkState.AddTransition(runState);
            wr.hasExitTime = false;
            wr.duration = 0.15f;
            wr.AddCondition(AnimatorConditionMode.Greater, RunThreshold, "Speed");

            var rw = runState.AddTransition(walkState);
            rw.hasExitTime = false;
            rw.duration = 0.15f;
            rw.AddCondition(AnimatorConditionMode.Less, RunThreshold, "Speed");
        }

        AddSkill(sm, idleState, "Skill1", Load(skill1));
        AddSkill(sm, idleState, "Skill3", Load(skill3));

        var dieClip = Load(die);
        if (dieClip != null)
        {
            var dieState = sm.AddState("Die");
            dieState.motion = dieClip;
            var toDie = sm.AddAnyStateTransition(dieState);
            toDie.hasExitTime = false;
            toDie.duration = 0.1f;
            toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        var animator = root.GetComponent<Animator>();
        if (animator == null) animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        if (avatar != null) animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Debug.Log("Boss controller built: " + controllerPath +
                  "\nIdle: " + Short(idle.name) + (frozenIdle ? " (frozen walk)" : "") +
                  " | Walk: " + Short(walk.name) +
                  " | Run: " + (run != null ? Short(run.name) : "none") +
                  "\nSkill1: " + (skill1 != null ? Short(skill1.name) : "none") +
                  " | Skill3: " + (skill3 != null ? Short(skill3.name) : "none") +
                  " | Die: " + (die != null ? Short(die.name) : "none"));
    }

    static void AddSkill(AnimatorStateMachine sm, AnimatorState idleState, string trigger, AnimationClip clip)
    {
        if (clip == null) return;

        var state = sm.AddState(trigger);
        state.motion = clip;

        var t = sm.AddAnyStateTransition(state);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);

        var back = state.AddTransition(idleState);
        back.hasExitTime = true;
        back.exitTime = 0.95f;
        back.duration = 0.15f;
    }

    static AnimationClip Load(ClipInfo info)
    {
        if (info == null) return null;
        return AssetDatabase.LoadAllAssetsAtPath(info.path)
            .OfType<AnimationClip>().FirstOrDefault(c => c.name == info.name);
    }

    static string Short(string n)
    {
        int i = n.LastIndexOf('|');
        return i >= 0 ? n.Substring(i + 1) : n;
    }

    static string Norm(string n)
    {
        return Short(n).ToLower().Replace("_", "").Replace(" ", "");
    }

    static ClipInfo Find(List<ClipInfo> infos, params string[] keys)
    {
        foreach (var k in keys)
        {
            var n = infos.FirstOrDefault(x => Norm(x.name).Contains(k));
            if (n != null) return n;
        }
        return null;
    }
}
#endif
