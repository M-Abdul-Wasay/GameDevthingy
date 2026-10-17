#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupLevel2
{

    [MenuItem("Tools/Setup Level 2 & Portal Transition")]
    public static void ExecuteAllSetup()
    {
        Debug.Log("[SetupLevel2] Starting Level 2 and Portal transition setup...");

        // 1. Build Form 2 Animator Controller
        var controller = BuildForm2AnimatorController();

        // 2. Setup Portal Transition in Level 1
        SetupLevel1Portal();

        // 3. Setup Level 2 Scene
        SetupLevel2Scene(controller);

        Debug.Log("[SetupLevel2] Level 2 and Portal transition setup completed successfully!");
    }

    public static AnimatorController BuildForm2AnimatorController()
    {
        string fbxPath = "Assets/Main Character form 2/Meshy_AI_Toxic_Direwolf_biped_Animation_all_frame_rate_60.fbx";
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            var anims = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            foreach (var a in anims)
            {
                bool loop = a.name.EndsWith("Idle_3") || a.name.EndsWith("Running") || a.name.EndsWith("Walking")
                            || a.name.ToLower().Contains("idle") || a.name.ToLower().Contains("run") || a.name.ToLower().Contains("walk");
                a.loopTime = loop;
            }
            importer.clipAnimations = anims;
            importer.SaveAndReimport();
        }

        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();

        AnimationClip idle = clips.FirstOrDefault(c => c.name.EndsWith("Idle_3")) 
                             ?? clips.FirstOrDefault(c => c.name.ToLower().Contains("idle")) 
                             ?? clips.FirstOrDefault();
        AnimationClip run = clips.FirstOrDefault(c => c.name.EndsWith("Running")) 
                            ?? clips.FirstOrDefault(c => c.name.ToLower().Contains("run")) 
                            ?? clips.FirstOrDefault(c => c.name.ToLower().Contains("walk")) 
                            ?? idle;
        AnimationClip attack = clips.FirstOrDefault(c => c.name.Contains("Double_Combo")) 
                               ?? clips.FirstOrDefault(c => c.name.ToLower().Contains("attack")) 
                               ?? clips.FirstOrDefault(c => c.name.ToLower().Contains("combo")) 
                               ?? idle;

        string controllerPath = "Assets/Main Character form 2/HeroController_Form2.controller";
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

        Debug.Log("[SetupLevel2] Created Form 2 Animator Controller at " + controllerPath);
        return controller;
    }

    private static void SetupLevel1Portal()
    {
        string level1Path = "Assets/Level 1.unity";
        var scene = EditorSceneManager.OpenScene(level1Path, OpenSceneMode.Single);

        // Find the portal object
        GameObject portalObj = null;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name.Contains("Portal") && !go.name.Contains("Moonlit"))
            {
                portalObj = go;
                break;
            }
        }

        if (portalObj != null)
        {
            var transition = portalObj.GetComponent<PortalLevelTransition>();
            if (transition == null)
            {
                transition = portalObj.AddComponent<PortalLevelTransition>();
            }
            transition.targetSceneName = "level2";
            transition.triggerDistance = 4f;

            var col = portalObj.GetComponent<Collider>();
            if (col == null)
            {
                var sc = portalObj.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 3.5f;
            }

            Debug.Log("[SetupLevel2] Configured Portal in Level 1 with PortalLevelTransition pointing to 'level2'.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        else
        {
            Debug.LogWarning("[SetupLevel2] Could not find Portal object in Level 1!");
        }
    }

    private static void SetupLevel2Scene(AnimatorController controller)
    {
        string level2Path = "Assets/level2.unity";
        var scene = EditorSceneManager.OpenScene(level2Path, OpenSceneMode.Single);

        // 1. Delete all existing enemies/knights in level2
        var allObjects = Object.FindObjectsOfType<GameObject>();
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            string lowerName = go.name.ToLower();
            if (lowerName.Contains("knight"))
            {
                // Delete top root
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                if (root != null) Object.DestroyImmediate(root);
                else Object.DestroyImmediate(go);
            }
        }

        // 2. Setup or verify the Red Castle map
        GameObject castleObj = null;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go == null) continue;
            if (go.name.ToLower().Contains("castle") || go.name.Contains("make_ma_a_3d_modelled"))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                castleObj = root != null ? root : go;
                break;
            }
        }

        if (castleObj == null)
        {
            string castlePath = "Assets/things/castle_lvl2.fbx";
            var castlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(castlePath);
            if (castlePrefab != null)
            {
                castleObj = (GameObject)PrefabUtility.InstantiatePrefab(castlePrefab);
                castleObj.transform.position = new Vector3(3.77f, 0.52f, -5.01f);
                castleObj.transform.localScale = new Vector3(3055.7f, 3055.7f, 3055.7f);
                castleObj.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            }
        }

        if (castleObj != null)
        {
            castleObj.name = "Castle_Map";

            // Add MeshColliders to all meshes in Castle
            int collidersAdded = 0;
            foreach (var mf in castleObj.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = mf.gameObject.AddComponent<MeshCollider>();
                    collidersAdded++;
                }
                mc.sharedMesh = mf.sharedMesh;
            }
            Debug.Log("[SetupLevel2] Added " + collidersAdded + " MeshColliders to Castle_Map.");

            // Make the castle beautifully tinted with a rich red tone
            string castleMatPath = "Assets/things/Materials/Meshy_AI_make_ma_a_3d_modelled_1016212547_texture.mat";
            var castleMat = AssetDatabase.LoadAssetAtPath<Material>(castleMatPath);
            if (castleMat != null)
            {
                // Beautiful warm crimson/red castle tint
                castleMat.color = new Color(1.0f, 0.45f, 0.45f, 1f);
                EditorUtility.SetDirty(castleMat);
            }
        }

        // 3. Setup Directional Light with atmospheric warm red lighting
        var light = Object.FindObjectOfType<Light>();
        if (light == null)
        {
            var lightGo = new GameObject("Directional Light");
            light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
        }
        light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        light.color = new Color(1f, 0.75f, 0.65f); // warm fantasy sunlight
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;

        // Ambient lighting
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.22f, 0.22f); // warm red ambient bounce

        // 4. Setup Main Character Form 2
        // Remove old Hero objects in level2 if any
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go == null) continue;
            if (go.name.ToLower().Contains("hero") || go.name.ToLower().Contains("direwolf"))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                if (root != null) Object.DestroyImmediate(root);
                else Object.DestroyImmediate(go);
            }
        }

        string form2FbxPath = "Assets/Main Character form 2/Meshy_AI_Toxic_Direwolf_biped_Animation_all_frame_rate_60.fbx";
        var form2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(form2FbxPath);
        GameObject heroObj = null;
        if (form2Prefab != null)
        {
            heroObj = (GameObject)PrefabUtility.InstantiatePrefab(form2Prefab);
        }
        else
        {
            heroObj = new GameObject("Hero_Form2");
        }

        heroObj.name = "Hero_Form2";
        heroObj.tag = "Player";

        // Assign Form 2 Material
        string form2MatPath = "Assets/Main Character form 2/Materials/Meshy_AI_Toxic_Direwolf_biped_texture_0.mat";
        var form2Mat = AssetDatabase.LoadAssetAtPath<Material>(form2MatPath);
        if (form2Mat != null)
        {
            foreach (var r in heroObj.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = form2Mat;
            }
        }

        // Animator setup
        var animator = heroObj.GetComponent<Animator>();
        if (animator == null) animator = heroObj.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        var fbxAssets = AssetDatabase.LoadAllAssetsAtPath(form2FbxPath);
        var avatar = fbxAssets.OfType<Avatar>().FirstOrDefault();
        if (avatar != null) animator.avatar = avatar;
        animator.applyRootMotion = false;

        // PlayerAnimationController
        var pac = heroObj.GetComponent<PlayerAnimationController>();
        if (pac == null) pac = heroObj.AddComponent<PlayerAnimationController>();

        // ShadowHeroController
        var shc = heroObj.GetComponent<ShadowHeroController>();
        if (shc == null) shc = heroObj.AddComponent<ShadowHeroController>();
        shc.currentForm = PlayerForm.TimeWarp; // Form 2
        shc.moveSpeed = 5f;
        shc.jumpForce = 6f;
        shc.gravity = -20f;
        shc.wallRadius = 0.3f;
        shc.wallHeight = 0.9f;

        // ShadowTeleport
        var st = heroObj.GetComponent<ShadowTeleport>();
        if (st == null) st = heroObj.AddComponent<ShadowTeleport>();

        // CapsuleCollider
        var cc = heroObj.GetComponent<CapsuleCollider>();
        if (cc == null) cc = heroObj.AddComponent<CapsuleCollider>();
        cc.center = new Vector3(0f, 0.8f, 0f);
        cc.radius = 0.45f;
        cc.height = 1.6f;

        // Position player on castle floor (spawn position)
        heroObj.transform.position = new Vector3(3.77f, 1.2f, -5.0f);
        heroObj.transform.rotation = Quaternion.identity;

        // 5. Setup Main Camera with CameraFollow
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            var camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
        var camFollow = mainCam.GetComponent<CameraFollow>();
        if (camFollow == null) camFollow = mainCam.gameObject.AddComponent<CameraFollow>();
        camFollow.target = heroObj.transform;
        camFollow.distance = 7f;
        camFollow.height = 1.8f;
        mainCam.transform.position = heroObj.transform.position - Vector3.forward * 7f + Vector3.up * 1.8f;
        mainCam.transform.LookAt(heroObj.transform.position + Vector3.up * 1.8f);

        Debug.Log("[SetupLevel2] Level 2 setup complete with Castle map, Form 2 Hero, Animator, and CameraFollow.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Finally reopen Level 1 so the game starts from Level 1
        EditorSceneManager.OpenScene("Assets/Level 1.unity");
    }
}
#endif
