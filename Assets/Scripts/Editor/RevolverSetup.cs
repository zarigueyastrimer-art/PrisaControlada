using System.Collections.Generic;
using System.Text;
using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.EditorTools
{
    public static class RevolverSetup
    {
        private const string RevolverPath = "Assets/MyAssets/guns/revolver/revolver.fbx";
        private const string ControllerPath = "Assets/Animators/RevolverAnimator.controller";
        private const string ArmClipPath = "Assets/Animations/Revolver/RevolverArmReload.anim";

        [MenuItem("Tools/PrisaControlada/Weapons/Setup Revolver")]
        public static void SetupRevolver()
        {
            var controller = RebuildControllerAsset(out var clips, out var missing);

            BuildWeaponInScene(controller, clips);

            StringBuilder sb = new StringBuilder("[RevolverSetup] Clips usados:\n");
            foreach (var kv in clips)
                sb.AppendLine($"  {kv.Key} -> {kv.Value.name} ({kv.Value.length:0.00}s)");

            sb.AppendLine("\n[RevolverSetup] ANIMACIONES FALTANTES:");
            if (missing.Count == 0) sb.AppendLine("  (ninguna)");
            else foreach (var m in missing) sb.AppendLine("  - " + m);

            Debug.Log(sb.ToString());

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        public static AnimatorController RebuildControllerAsset(out Dictionary<string, AnimationClip> clips, out List<string> missing)
        {
            ConfigureImporterClips();
            clips = LoadClips();
            var controller = BuildController(clips, out missing);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void ConfigureImporterClips()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(RevolverPath);
            if (importer == null) return;

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;

            var defs = importer.defaultClipAnimations;
            foreach (var def in defs)
            {
                string low = def.name.ToLowerInvariant();
                if (low.Contains("idel") || low.Contains("idle") || low.Contains("walk"))
                    def.loopTime = true;
            }
            importer.clipAnimations = defs;
            importer.SaveAndReimport();
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RevolverPath))
            {
                if (asset is AnimationClip clip)
                {
                    if (clip.name.StartsWith("__preview__")) continue;
                    string role = ResolveRole(clip.name);
                    if (role != null && !clips.ContainsKey(role))
                        clips[role] = clip;
                }
            }
            return clips;
        }

        private static string ResolveRole(string clipName)
        {
            string low = clipName.ToLowerInvariant();
            bool isAim = low.Contains("aim");
            bool isShoot = low.Contains("shoot");
            bool isWalk = low.Contains("walk");
            bool isRun = low.Contains("run");
            bool isIdle = low.Contains("idel") || low.Contains("idle");
            bool isGet = low.Contains("get");
            bool isReload = low.Contains("reload");
            bool isAimTrans = low.Contains("noaim") || low.Contains("toaim");

            if (isGet) return "get";
            if (isReload && (low.Contains("gun") || !low.Contains("arm"))) return "reload";
            if (isAimTrans) return "aimTransition";
            if (isShoot) return isAim ? "shootAim" : "shoot";
            if (isWalk) return isAim ? "walkAim" : "walk";
            if (isRun) return isAim ? "runAim" : "run";
            if (isIdle) return isAim ? "idleAim" : "idle";
            return null;
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, out List<string> missing)
        {
            string[] needed = { "idle", "idleAim", "aimTransition", "walk", "walkAim", "run", "runAim", "shoot", "shootAim", "reload", "get" };
            missing = new List<string>();
            foreach (string role in needed)
                if (!clips.ContainsKey(role))
                    missing.Add(role);

            EnsureFolder("Assets/Animators");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            if (controller == null)
            {
                missing.Add("controller");
                return null;
            }

            // Reconstruir EN EL MISMO asset (mismo guid) para no romper la referencia del Animator en la escena.
            controller.parameters = new AnimatorControllerParameter[0];
            controller.layers = new AnimatorControllerLayer[0];

            controller.AddParameter("Equipped", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reloading", AnimatorControllerParameterType.Bool);

            var baseLayer = new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f };
            var sm = new AnimatorStateMachine { name = "Base Layer" };
            baseLayer.stateMachine = sm;
            controller.AddLayer(baseLayer);

            var empty = sm.AddState("Empty");
            empty.writeDefaultValues = true;

            var get = sm.AddState("Get");
            get.motion = GetOrNull(clips, "get");

            var unequip = sm.AddState("Unequip");
            unequip.motion = GetOrNull(clips, "get");
            unequip.speed = -1f;

            var raised = CreateBlendTree(controller, sm, "Raised", "Speed",
                (GetOrNull(clips, "idleAim"), 0f),
                (GetOrNull(clips, "walkAim"), 1f),
                (GetOrNull(clips, "runAim"), 2f)).state;

            var lowerIdle = sm.AddState("LoweredIdle");
            lowerIdle.motion = GetOrNull(clips, "idle");

            var lowerWalk = sm.AddState("LoweredWalk");
            lowerWalk.motion = GetOrNull(clips, "walk");

            var lowerRun = sm.AddState("LoweredRun");
            lowerRun.motion = GetOrNull(clips, "walk");
            lowerRun.speed = 1.6f;

            var raiseToAim = sm.AddState("RaiseToAim");
            raiseToAim.motion = GetOrNull(clips, "aimTransition");

            var lowerFromAim = sm.AddState("LowerFromAim");
            lowerFromAim.motion = GetOrNull(clips, "aimTransition");
            lowerFromAim.speed = -1f;

            var shoot = sm.AddState("Shoot");
            shoot.motion = GetOrNull(clips, "shoot");

            var shootAim = sm.AddState("ShootAim");
            shootAim.motion = GetOrNull(clips, "shootAim");

            var reload = sm.AddState("Reload");
            reload.motion = GetOrNull(clips, "reload");

            sm.defaultState = empty;

            // Equip / Unequip
            Transition(empty, get, duration: 0.15f, condition: ("Equipped", true));
            Exit(get, lowerIdle, exitTime: 0.95f, duration: 0.2f);

            // Idle / Walk / Run por Speed (run reusa walk acelerado)
            Transition(lowerIdle, lowerWalk, duration: 0.2f, floatCondition: ("Speed", 0.5f, true));
            Transition(lowerWalk, lowerIdle, duration: 0.2f, floatCondition: ("Speed", 0.5f, false));
            Transition(lowerWalk, lowerRun, duration: 0.2f, floatCondition: ("Speed", 1.5f, true));
            Transition(lowerRun, lowerWalk, duration: 0.2f, floatCondition: ("Speed", 1.5f, false));
            Transition(lowerIdle, lowerRun, duration: 0.2f, floatCondition: ("Speed", 1.5f, true));
            Transition(lowerRun, lowerIdle, duration: 0.2f, floatCondition: ("Speed", 0.5f, false));

            // Aim
            Transition(lowerIdle, raiseToAim, duration: 0.1f, condition: ("Aim", true));
            Transition(lowerWalk, raiseToAim, duration: 0.1f, condition: ("Aim", true));
            Transition(lowerRun, raiseToAim, duration: 0.1f, condition: ("Aim", true));
            Exit(raiseToAim, raised, exitTime: 0.95f, duration: 0.2f);
            Transition(raised, lowerFromAim, duration: 0.1f, condition: ("Aim", false));
            Exit(lowerFromAim, lowerIdle, exitTime: 0.95f, duration: 0.2f);

            // Shoot
            Transition(lowerIdle, shoot, duration: 0.05f, trigger: "Shoot");
            Transition(lowerWalk, shoot, duration: 0.05f, trigger: "Shoot");
            Transition(lowerRun, shoot, duration: 0.05f, trigger: "Shoot");
            Exit(shoot, lowerIdle, exitTime: 0.9f, duration: 0.1f);

            Transition(raised, shootAim, duration: 0.05f, trigger: "Shoot");
            Exit(shootAim, raised, exitTime: 0.9f, duration: 0.1f);

            // Reload (bool sincronizado: lo setea el WeaponController)
            Transition(lowerIdle, reload, duration: 0.1f, condition: ("Reloading", true));
            Transition(lowerWalk, reload, duration: 0.1f, condition: ("Reloading", true));
            Transition(lowerRun, reload, duration: 0.1f, condition: ("Reloading", true));
            Transition(raised, reload, duration: 0.1f, condition: ("Reloading", true));
            Transition(reload, lowerIdle, duration: 0.15f, condition: ("Reloading", false));

            var unequipFrom = new[] { lowerIdle, lowerWalk, lowerRun, raised, raiseToAim, lowerFromAim, shoot, shootAim, reload };
            foreach (var from in unequipFrom)
                Transition(from, unequip, duration: 0.1f, condition: ("Equipped", false));
            Exit(unequip, empty, exitTime: 0.95f, duration: 0.2f);

            AddArmLayer(controller, clips);

            EditorUtility.SetDirty(controller);

            return controller;
        }

        // Unity 6 no permite dos Animator en el mismo GameObject, asi que el brazo
        // se anima en una CAPA del mismo controller (el Animator va en el root del arma).
        private static void AddArmLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            var armReloadClip = GetOrCreateArmReloadClip();
            if (armReloadClip == null) return;

            var armLayer = new AnimatorControllerLayer { name = "Arm", defaultWeight = 1f };
            var armSm = new AnimatorStateMachine { name = "Arm" };
            armLayer.stateMachine = armSm;
            controller.AddLayer(armLayer);

            var armHeld = armSm.AddState("Held");
            armHeld.writeDefaultValues = true;

            var armReload = armSm.AddState("Reload");
            armReload.motion = armReloadClip;

            float gunReloadLen = GetOrNull(clips, "reload") != null ? GetOrNull(clips, "reload").length : 2.36f;
            if (gunReloadLen > 0.01f)
                armReload.speed = armReloadClip.length / gunReloadLen;

            armSm.defaultState = armHeld;

            var toReload = armHeld.AddTransition(armReload);
            toReload.duration = 0.1f;
            toReload.hasExitTime = false;
            toReload.hasFixedDuration = true;
            toReload.AddCondition(AnimatorConditionMode.If, 0f, "Reloading");

            var toHeld = armReload.AddTransition(armHeld);
            toHeld.hasExitTime = false;
            toHeld.duration = 0.15f;
            toHeld.hasFixedDuration = true;
            toHeld.AddCondition(AnimatorConditionMode.IfNot, 0f, "Reloading");

            Debug.Log($"[RevolverSetup] Capa 'Arm' agregada: '{armReloadClip.name}' ({armReloadClip.length:0.00}s, speed {armReload.speed:0.00}x para sincronizar con {gunReloadLen:0.00}s).");
        }

        private static AnimationClip GetOrCreateArmReloadClip()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArmClipPath);
            if (existing != null) return existing;

            AnimationClip fullReload = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RevolverPath))
                if (asset is AnimationClip clip && clip.name == "root|reloadArm1")
                    fullReload = clip;
            if (fullReload == null)
            {
                Debug.LogWarning("[RevolverSetup] No se encontro el clip 'root|reloadArm1'; no se creo el clip del brazo.");
                return null;
            }

            EnsureFolder("Assets/Animations/Revolver");
            var armClip = new AnimationClip { name = "RevolverArmReload", frameRate = fullReload.frameRate };
            foreach (var b in AnimationUtility.GetCurveBindings(fullReload))
            {
                if (b.path == "root/Armature" || b.path.StartsWith("root/Armature/")) continue;
                var curve = AnimationUtility.GetEditorCurve(fullReload, b);
                if (curve != null) AnimationUtility.SetEditorCurve(armClip, b, curve);
            }
            AssetDatabase.CreateAsset(armClip, ArmClipPath);
            Debug.Log($"[RevolverSetup] Clip del brazo creado: {armClip.name} ({armClip.length:0.00}s).");
            return armClip;
        }

        private static AnimationClip GetOrNull(Dictionary<string, AnimationClip> clips, string role)
        {
            return clips.TryGetValue(role, out var c) ? c : null;
        }

        private static (BlendTree tree, AnimatorState state) CreateBlendTree(AnimatorController controller, AnimatorStateMachine sm, string name, string parameter, params (AnimationClip clip, float position)[] children)
        {
            controller.CreateBlendTreeInController(name, out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = parameter;
            foreach (var (clip, pos) in children)
                if (clip != null)
                    tree.AddChild(clip, pos);

            AnimatorState state = null;
            foreach (var s in sm.states)
                if (s.state.motion == tree) { state = s.state; break; }
            if (state == null)
            {
                state = sm.AddState(name);
                state.motion = tree;
            }
            state.name = name;
            return (tree, state);
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration,
            (string param, bool value)? condition = null,
            string trigger = null,
            (string param, float value, bool greater)? floatCondition = null)
        {
            var t = from.AddTransition(to);
            t.duration = duration;
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            if (condition.HasValue)
                t.AddCondition(condition.Value.value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, condition.Value.param);
            if (trigger != null)
                t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            if (floatCondition.HasValue)
                t.AddCondition(floatCondition.Value.greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, floatCondition.Value.value, floatCondition.Value.param);
            return t;
        }

        private static AnimatorStateTransition Exit(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
            t.hasFixedDuration = true;
            return t;
        }

        private static void BuildWeaponInScene(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[RevolverSetup] No hay camara principal en la escena.");
                return;
            }

            Transform existing = cam.transform.Find("RevolverWeapon");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var wc = cam.gameObject.GetComponent<WeaponController>();
            if (wc != null)
                Object.DestroyImmediate(wc);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RevolverPath);
            if (prefab == null)
            {
                Debug.LogError("[RevolverSetup] No se pudo cargar el prefab del revolver.");
                return;
            }

            var go = (GameObject)Object.Instantiate(prefab);
            go.name = "RevolverWeapon";
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, -0.1f, 0.55f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
            go.transform.localScale = Vector3.one;

            // Los clips bindean rutas relativas al root del modelo (p.ej. "root/Armature/root/crane"),
            // asi que el Animator va en el root del modelo (RevolverWeapon), NO en un hijo.
            Transform animRoot = go.transform;

            var animator = animRoot.GetComponent<Animator>();
            if (animator == null) animator = animRoot.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var player = cam.GetComponentInParent<PlayerController>();
            var health = cam.GetComponentInParent<PlayerHealth>();
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");

            var weapon = cam.gameObject.AddComponent<WeaponController>();
            var so = new SerializedObject(weapon);
            so.FindProperty("weaponAnimator").objectReferenceValue = animator;
            so.FindProperty("weaponRoot").objectReferenceValue = go.transform;
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("playerHealth").objectReferenceValue = health;
            so.FindProperty("inputActions").objectReferenceValue = inputAsset;
            if (clips.TryGetValue("get", out var getClip))
                so.FindProperty("equipAnimTime").floatValue = getClip.length;
            if (clips.TryGetValue("reload", out var reloadClip))
                so.FindProperty("reloadTime").floatValue = reloadClip.length;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[RevolverSetup] Arma creada como 'RevolverWeapon' bajo la camara. Ajusta localPosition/localRotation/localScale si la ves mal ubicada.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int idx = path.LastIndexOf('/');
            if (idx <= 0) return;
            string parent = path.Substring(0, idx);
            string name = path.Substring(idx + 1);
            EnsureFolder(parent);
            if (AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}