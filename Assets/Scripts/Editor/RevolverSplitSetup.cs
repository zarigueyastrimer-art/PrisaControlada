using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.EditorTools
{
    // Arma dividida en DOS modelos separados (Unity 6 no permite 2 Animators en el mismo GO):
    //  - RevolverGun: mesh + animaciones del arma (gun.fbx)
    //  - RevolverArm: mesh + animaciones del brazo (arms/*.fbx)
    // Los clips se extraen a .anim independientes para poder borrar los FBX redundantes despues.
    public static class RevolverSplitSetup
    {
        private const string GunFbxPath = "Assets/MyAssets/guns/revolver/gun.fbx";
        private const string ArmsFolder = "Assets/MyAssets/guns/revolver/arms";
        private const string ArmMeshFbxPath = "Assets/MyAssets/guns/revolver/arms/idelArm.fbx";
        private const string AnimFolder = "Assets/Animations/Revolver";
        private const string GunControllerPath = "Assets/Animators/RevolverGunAnimator.controller";
        private const string ArmControllerPath = "Assets/Animators/RevolverArmAnimator.controller";
        private const string ReportPath = "Assets/MyAssets/guns/revolver/DIAGNOSE_SPLIT.txt";

        // El gun va socketed a la mano; sus clips animan el nodo raiz (Armature / Armature/root) con
        // posicion/rotacion/scale horneados que despegan el arma del socket. Esas curvas se eliminan.
        private static readonly string[] GunRootPaths = { "", "Armature", "Armature/root" };

        [MenuItem("Tools/PrisaControlada/Weapons/Setup Revolver SPLIT (arm+gun)")]
        public static void SetupSplit()
        {
            var sb = new StringBuilder();
            try
            {
                ConfigureImporters(sb);

                sb.AppendLine("== CLIPS GUN ==");
                var gunClips = LoadGunClips(sb);
                sb.AppendLine("== CLIPS ARM ==");
                var armClips = LoadArmClips(sb);

                sb.AppendLine("== CONTROLLERS ==");
                var gunController = BuildGunController(gunClips, sb);
                float gunReloadLen = gunClips.TryGetValue("reload", out var rc) ? rc.length : 2.36f;
                var armController = BuildArmController(armClips, gunReloadLen, sb);

                // Los controllers se guardan YA (antes de tocar la escena) para que
                // NUNCA queden vacios aunque falle el armado de la escena.
                AssetDatabase.SaveAssets();

                BuildWeaponInScene(gunController, armController, gunClips, armClips, sb);
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                sb.AppendLine("\n== LISTO. Proba Play -> 1. Si el gun no queda en la mano, ajusta localPosition/localRotation de RevolverGun (hijo de hand.R). ==");
            }
            catch (Exception e)
            {
                sb.AppendLine("\n== EXCEPCION ==");
                sb.AppendLine(e.ToString());
            }

            string folder = Path.GetDirectoryName(ReportPath);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                int idx = folder.LastIndexOf('/');
                if (idx > 0) AssetDatabase.CreateFolder(folder.Substring(0, idx), folder.Substring(idx + 1));
            }
            File.WriteAllText(ReportPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log("[RevolverSplitSetup] Reporte: " + ReportPath + "\n" + sb.ToString());
        }

        // ---------- Importadores ----------

        private static void ConfigureImporters(StringBuilder sb)
        {
            ConfigureImporter(GunFbxPath, sb);
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ArmsFolder }))
                ConfigureImporter(AssetDatabase.GUIDToAssetPath(guid), sb);
        }

        private static void ConfigureImporter(string path, StringBuilder sb)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) { sb.AppendLine("  sin importer: " + path); return; }
            imp.importAnimation = true;
            imp.animationType = ModelImporterAnimationType.Generic;
            var defs = imp.defaultClipAnimations;
            foreach (var def in defs)
            {
                string low = def.name.ToLowerInvariant();
                if (low.Contains("idel") || low.Contains("idle") || low.Contains("walk"))
                    def.loopTime = true;
            }
            imp.clipAnimations = defs;
            imp.SaveAndReimport();
            sb.AppendLine("  importador OK: " + path);
        }

        // ---------- Clips ----------

        private static Dictionary<string, AnimationClip> LoadGunClips(StringBuilder sb)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(GunFbxPath))
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                string role = ResolveRole(clip.name);
                if (role == null || clips.ContainsKey(role)) continue;
                string name = "Gun_" + RoleToName(role);
                clips[role] = ExtractClip(clip, name, sb, GunRootPaths);
                sb.AppendLine($"  {role} <- {clip.name} ({clip.length:0.00}s) -> {name}.anim (raiz limpiada)");
            }
            return clips;
        }

        private static Dictionary<string, AnimationClip> LoadArmClips(StringBuilder sb)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ArmsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                    string role = ResolveRole(clip.name);
                    if (role == null || clips.ContainsKey(role)) continue;
                    string name = "Arm_" + RoleToName(role);
                    clips[role] = ExtractClip(clip, name, sb);
                    sb.AppendLine($"  {role} <- {clip.name} ({clip.length:0.00}s) -> {name}.anim");
                }
            }
            return clips;
        }

        private static AnimationClip ExtractClip(AnimationClip source, string name, StringBuilder sb, string[] stripRootPaths = null)
        {
            EnsureFolder(AnimFolder);
            string path = AnimFolder + "/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var clip = existing;
            if (existing == null)
            {
                clip = new AnimationClip { name = name, frameRate = source.frameRate };
                foreach (var b in AnimationUtility.GetCurveBindings(source))
                {
                    var curve = AnimationUtility.GetEditorCurve(source, b);
                    if (curve != null) AnimationUtility.SetEditorCurve(clip, b, curve);
                }
                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(source))
                    AnimationUtility.SetObjectReferenceCurve(clip, b, AnimationUtility.GetObjectReferenceCurve(source, b));

                var settings = AnimationUtility.GetAnimationClipSettings(source);
                string low = name.ToLowerInvariant();
                settings.loopTime = low.Contains("idel") || low.Contains("idle") || low.Contains("walk");
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, path);
            }

            if (stripRootPaths != null)
            {
                bool changed = false;
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (System.Array.IndexOf(stripRootPaths, b.path) >= 0)
                    {
                        AnimationUtility.SetEditorCurve(clip, b, null);
                        changed = true;
                    }
                }
                if (changed)
                {
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssets();
                }
            }
            return clip;
        }

        private static string RoleToName(string role)
        {
            switch (role)
            {
                case "idle": return "Idle";
                case "idleAim": return "IdleAim";
                case "walk": return "Walk";
                case "shoot": return "Shoot";
                case "shootAim": return "ShootAim";
                case "reload": return "Reload";
                default: return "Get";
            }
        }

        private static string ResolveRole(string clipName)
        {
            string low = clipName.ToLowerInvariant();
            bool isAim = low.Contains("aim");
            bool isShoot = low.Contains("shoot");
            bool isWalk = low.Contains("walk");
            bool isIdle = low.Contains("idel") || low.Contains("idle");
            bool isGet = low.Contains("get");
            bool isReload = low.Contains("reload");
            if (isGet) return "get";
            if (isReload) return "reload";
            if (isShoot) return isAim ? "shootAim" : "shoot";
            if (isWalk) return isAim ? "walkAim" : "walk";
            if (isIdle) return isAim ? "idleAim" : "idle";
            return null;
        }

        // ---------- Controllers ----------

        private static AnimatorController LoadOrCreateController(string path)
        {
            EnsureFolder("Assets/Animators");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            return controller;
        }

        private static AnimatorStateMachine AddBaseLayer(AnimatorController controller)
        {
            // Limpia sub-assets huerfanos de corridas previas (sin cambiar el guid del controller).
            string path = AssetDatabase.GetAssetPath(controller);
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub != controller) UnityEngine.Object.DestroyImmediate(sub, true);

            controller.parameters = new AnimatorControllerParameter[0];
            controller.layers = new AnimatorControllerLayer[0];

            // El state machine DEBE ser sub-asset del controller para persistir al guardar.
            var sm = new AnimatorStateMachine { name = "Base Layer" };
            AssetDatabase.AddObjectToAsset(sm, controller);

            var baseLayer = new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f };
            baseLayer.stateMachine = sm;
            controller.AddLayer(baseLayer);
            return sm;
        }

        private static AnimatorController BuildGunController(Dictionary<string, AnimationClip> clips, StringBuilder sb)
        {
            // Espejo exacto del controller del brazo: misma maquina de estados, clips gun/pistol.
            // reload.speed = 1.0 (el clip del gun ya dura lo que dura la recarga del arma).
            return BuildSimpleController(GunControllerPath, clips, 1f, "gun", sb);
        }

        private static AnimatorController BuildSimpleController(string path, Dictionary<string, AnimationClip> clips,
            float reloadSpeed, string label, StringBuilder sb)
        {
            var controller = LoadOrCreateController(path);
            if (controller == null) { sb.AppendLine("ERROR creando controller " + label); return null; }

            // Si el controller ya tiene estados (valido), se conserva tal cual para no
            // pisar el contenido (los controllers se escribieron a mano con referencias
            // correctas porque el API de Unity los guardaba vacios).
            if (controller.layers.Length > 0 && controller.layers[0].stateMachine != null &&
                controller.layers[0].stateMachine.states.Length > 0)
            {
                sb.AppendLine("  " + label + " controller ya tiene estados: se conserva (" + path + ")");
                return controller;
            }

            var sm = AddBaseLayer(controller);

            controller.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reloading", AnimatorControllerParameterType.Bool);

            var empty = sm.AddState("Empty");
            empty.writeDefaultValues = true;

            var idle = sm.AddState("Idle");
            idle.motion = GetOrNull(clips, "idle");

            var idleAim = sm.AddState("IdleAim");
            idleAim.motion = GetOrNull(clips, "idleAim");

            var walk = sm.AddState("Walk");
            walk.motion = GetOrNull(clips, "walk");
            // La velocidad de la animacion la controla el parametro Speed: 1 al caminar, 2 al correr.
            walk.speedParameter = "Speed";
            walk.speedParameterActive = true;

            var shoot = sm.AddState("Shoot");
            shoot.motion = GetOrNull(clips, "shoot");

            var shootAim = sm.AddState("ShootAim");
            shootAim.motion = GetOrNull(clips, "shootAim");

            var reload = sm.AddState("Reload");
            reload.motion = GetOrNull(clips, "reload");
            reload.speed = reloadSpeed;

            // Get (equip/unequip): solo si hay clip "get" (el gun lo tiene; el brazo no).
            var get = GetOrNull(clips, "get") != null ? sm.AddState("Get") : null;
            if (get != null) get.motion = GetOrNull(clips, "get");

            sm.defaultState = empty;

            // Equip/unequip NO usa transiciones: el WeaponController reproduce "Get" (hacia
            // adelante al equipar, invertido al desequipar) via Animator.Play(). Estos estados
            // existen solo para poder invocar Play() sobre ellos.

            // Idle / Walk
            Transition(idle, walk, 0.2f, floatCondition: ("Speed", 0.5f, true));
            Transition(walk, idle, 0.2f, floatCondition: ("Speed", 0.5f, false));

            // Aim
            Transition(idle, idleAim, 0.2f, condition: ("Aim", true));
            Transition(walk, idleAim, 0.2f, condition: ("Aim", true));
            Transition(idleAim, idle, 0.2f, condition: ("Aim", false));

            // Shoot (el controller elige shoot o shootAim segun el estado: idle -> shoot, idleAim -> shootAim)
            Transition(idle, shoot, 0.05f, trigger: "Shoot");
            Transition(walk, shoot, 0.05f, trigger: "Shoot");
            Exit(shoot, idle, exitTime: 0.9f, duration: 0.1f);
            Transition(idleAim, shootAim, 0.05f, trigger: "Shoot");
            Exit(shootAim, idleAim, exitTime: 0.9f, duration: 0.1f);

            // Reload: PISA TODO. Se puede entrar desde cualquier estado y solo sale cuando el
            // clip termino (exitTime 0.95) Y Reloading volvio a false.
            Transition(idle, reload, 0.1f, condition: ("Reloading", true));
            Transition(walk, reload, 0.1f, condition: ("Reloading", true));
            Transition(idleAim, reload, 0.1f, condition: ("Reloading", true));
            Transition(shoot, reload, 0.1f, condition: ("Reloading", true));
            Transition(shootAim, reload, 0.1f, condition: ("Reloading", true));
            ExitWithCondition(reload, idle, 0.95f, 0.1f, condition: ("Reloading", false));

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(sm);
            sb.AppendLine("  " + label + " controller OK: " + path + " (" + controller.layers.Length + " capa)");
            return controller;
        }

        private static AnimatorController BuildArmController(Dictionary<string, AnimationClip> clips, float gunReloadLen, StringBuilder sb)
        {
            // El brazo debe sincronizarse con la recarga del arma: se acelera el clip del brazo.
            var reloadClip = GetOrNull(clips, "reload");
            float reloadSpeed = 1f;
            if (reloadClip != null && gunReloadLen > 0.01f)
                reloadSpeed = reloadClip.length / gunReloadLen;
            return BuildSimpleController(ArmControllerPath, clips, reloadSpeed, "arm", sb);
        }

        // ---------- Escena ----------

        private static void BuildWeaponInScene(AnimatorController gunCtrl, AnimatorController armCtrl,
            Dictionary<string, AnimationClip> gunClips, Dictionary<string, AnimationClip> armClips, StringBuilder sb)
        {
            var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindObjectOfType<Camera>();
            if (cam == null) { sb.AppendLine("ERROR: no hay camara principal"); return; }

            var old = cam.transform.Find("RevolverWeapon");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var root = new GameObject("RevolverWeapon");
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // Arm (referencia para el socket del arma: hueso de la mano)
            var armPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmMeshFbxPath);
            var armGo = (GameObject)UnityEngine.Object.Instantiate(armPrefab);
            armGo.name = "RevolverArm";
            armGo.transform.SetParent(root.transform, false);
            armGo.transform.localPosition = Vector3.zero;
            armGo.transform.localRotation = Quaternion.identity;
            armGo.transform.localScale = Vector3.one;

            var armAnimator = armGo.GetComponent<Animator>();
            if (armAnimator == null) armAnimator = armGo.AddComponent<Animator>();
            armAnimator.runtimeAnimatorController = armCtrl;
            armAnimator.applyRootMotion = false;

            // Gun: HIJO del hueso de la mano del brazo (socket). Anima su mecanismo (hammer/cylinder/trigger...);
            // las curvas de raiz (Armature / Armature/root) se limpiaron al extraer los clips para que el
            // arma no se despegue del socket. Se conserva el transform tuneado.
            var handBone = FindBone(armGo.transform, "hand.R") ?? FindBone(armGo.transform, "hand");
            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunFbxPath);
            var gunGo = (GameObject)UnityEngine.Object.Instantiate(gunPrefab);
            gunGo.name = "RevolverGun";
            gunGo.transform.SetParent(handBone != null ? handBone : (Transform)root.transform, false);

            // Transform del socket del gun (tuneado para quedar en la mano): se aplica en cada setup.
            gunGo.transform.localPosition = new Vector3(-0.000472000014f, -0.00143199996f, -0.000722999976f);
            gunGo.transform.localRotation = Quaternion.Euler(270f, 0f, 0f);
            gunGo.transform.localScale = new Vector3(0.00999999978f, 0.00999999978f, 0.00999999978f);

            var gunAnimator = gunGo.GetComponent<Animator>();
            if (gunAnimator == null) gunAnimator = gunGo.AddComponent<Animator>();
            gunAnimator.runtimeAnimatorController = gunCtrl;
            gunAnimator.applyRootMotion = false;

            // Wiring WeaponController
            var wc = cam.GetComponent<WeaponController>();
            if (wc == null) wc = cam.gameObject.AddComponent<WeaponController>();
            var so = new SerializedObject(wc);
            so.FindProperty("weaponAnimator").objectReferenceValue = gunAnimator;
            so.FindProperty("weaponArmAnimator").objectReferenceValue = armAnimator;
            so.FindProperty("weaponRoot").objectReferenceValue = root.transform;
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("player").objectReferenceValue = cam.GetComponentInParent<PlayerController>();
            so.FindProperty("playerHealth").objectReferenceValue = cam.GetComponentInParent<PlayerHealth>();
            so.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (gunClips.TryGetValue("reload", out var reloadClip))
                so.FindProperty("reloadTime").floatValue = reloadClip.length;
            so.ApplyModifiedPropertiesWithoutUndo();

            sb.AppendLine("\n== SOCKET GUN ==");
            sb.AppendLine("  hueso mano: " + (handBone != null ? TransformPath(handBone) : "NO encontrado (gun bajo RevolverWeapon)"));
            sb.AppendLine("  RevolverGun.localPosition=" + gunGo.transform.localPosition +
                " localRotation=" + gunGo.transform.localRotation.eulerAngles +
                " localScale=" + gunGo.transform.localScale);

            sb.AppendLine("\n== CHEQUEO DE BINDINGS ==");
            CheckBindings(gunClips, gunGo.transform, "gun", sb);
            CheckBindings(armClips, armGo.transform, "arm", sb);

            sb.AppendLine("\nArma creada: RevolverWeapon (RevolverArm + RevolverGun socket en el hueso de la mano) bajo la camara.");
            sb.AppendLine("Si el arma no queda en la mano, ajusta localPosition/localRotation de RevolverGun (hijo de hand.R).");
        }

        private static void CheckBindings(Dictionary<string, AnimationClip> clips, Transform from, string label, StringBuilder sb)
        {
            foreach (var role in new[] { "idle", "reload" })
            {
                if (!clips.TryGetValue(role, out var clip)) continue;
                var bs = AnimationUtility.GetCurveBindings(clip);
                int resolved = 0;
                var unresolved = new List<string>();
                foreach (var b in bs)
                {
                    if (from.Find(b.path) != null) resolved++;
                    else if (unresolved.Count < 5) unresolved.Add(b.path);
                }
                sb.AppendLine($"  {label} [{role}] resuelven desde el root: {resolved}/{bs.Length}");
                foreach (var u in unresolved)
                    sb.AppendLine($"    NO resuelve: {u}");
            }
        }

        private static AnimationClip GetOrNull(Dictionary<string, AnimationClip> clips, string role)
        {
            return clips.TryGetValue(role, out var c) ? c : null;
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindBone(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static string TransformPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
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

        private static AnimatorStateTransition ExitWithCondition(AnimatorState from, AnimatorState to, float exitTime, float duration,
            (string param, bool value)? condition = null)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
            t.hasFixedDuration = true;
            if (condition.HasValue)
                t.AddCondition(condition.Value.value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, condition.Value.param);
            return t;
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