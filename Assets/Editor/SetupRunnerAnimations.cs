using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PrisaControlada.Editor
{
    // Configura las 3 animaciones del runer como loop, crea un Animator Controller
    // con Idle / Run / Attack y lo asigna al prefab RunnerEnemy.
    //
    // Uso: Tools > PrisaControlada > Setup Runner Animations & Controller
    public static class SetupRunnerAnimations
    {
        private const string ControllerPath = "Assets/Resources/RunnerAnimator.controller";

        private const string IdleFbx = "Assets/MyAssets/runer/Anim/Runner40k@Unarmed Idle.fbx";
        private const string RunFbx = "Assets/MyAssets/runer/Anim/Runner40k@Running Crawl.fbx";
        private const string AttackFbx = "Assets/MyAssets/runer/Anim/Runner40k@Zombie Neck Bite.fbx";
        private const string ModelFbx = "Assets/MyAssets/runer/Runner40k_Rigged.fbx";

        [MenuItem("Tools/PrisaControlada/Setup Runner Animations & Controller", false, 203)]
        public static void Setup()
        {
            // 0. Forzar reimport de los FBX para que Unity genere los clips y el Avatar
            foreach (var path in new[] { IdleFbx, RunFbx, AttackFbx, ModelFbx })
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                {
                    Debug.LogWarning($"[SetupRunnerAnimations] Aún no importado por Unity: {path}");
                    continue;
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            // 1. Configurar las 3 animaciones como loop
            SetLoop(IdleFbx, true);
            SetLoop(RunFbx, true);
            SetLoop(AttackFbx, false); // El ataque NO hace loop: se reproduce una vez y vuelve a Idle
            AssetDatabase.SaveAssets();

            // 2. Crear el Animator Controller
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idleClip = LoadClip(IdleFbx, "Idle");
            var runClip = LoadClip(RunFbx, "Run");
            var attackClip = LoadClip(AttackFbx, "Attack");

            if (idleClip == null || runClip == null || attackClip == null)
            {
                Debug.LogError("[SetupRunnerAnimations] Faltan clips de animación. Esperá a que Unity termine de importar los FBX y volvé a correr el menú.");
                return;
            }

            var idleState = sm.AddState("Idle", new Vector3(0, 0, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            var runState = sm.AddState("Run", new Vector3(200, 0, 0));
            runState.motion = runClip;

            var attackState = sm.AddState("Attack", new Vector3(400, 0, 0));
            attackState.motion = attackClip;

            // Idle <-> Run según Speed
            var toRun = idleState.AddTransition(runState);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            toRun.hasExitTime = false;
            toRun.duration = 0.1f;

            var toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;

            // Cualquier estado -> Attack por trigger
            var toAttack = sm.AddAnyStateTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0f;

            // Attack -> Idle cuando termina
            var toIdleAfterAttack = attackState.AddTransition(idleState);
            toIdleAfterAttack.hasExitTime = true;
            toIdleAfterAttack.exitTime = 0.95f;
            toIdleAfterAttack.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SetupRunnerAnimations] Controller creado: {ControllerPath}");

            // 3. Asignar al prefab RunnerEnemy
            ApplyToPrefab(controller, ModelFbx);
        }

        private static void SetLoop(string fbxPath, bool loop)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[SetupRunnerAnimations] No es ModelImporter: {fbxPath}");
                return;
            }

            var clipAnimations = importer.clipAnimations;
            for (int i = 0; i < clipAnimations.Length; i++)
            {
                clipAnimations[i].loopTime = loop;
            }
            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
            Debug.Log($"[SetupRunnerAnimations] Loop={loop} en {fbxPath}");
        }

        private static AnimationClip LoadClip(string fbxPath, string fallbackName)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                    return clip;
            }

            Debug.LogWarning($"[SetupRunnerAnimations] No se encontró clip en {fbxPath}");
            return null;
        }

        private static void ApplyToPrefab(RuntimeAnimatorController controller, string modelFbxPath)
        {
            var prefabPath = "Assets/Prefabs/Enemies/RunnerEnemy.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SetupRunnerAnimations] No se encontró el prefab: {prefabPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            var body = instance.transform.Find("Body");
            if (body == null)
            {
                Debug.LogWarning("[SetupRunnerAnimations] No se encontró el child 'Body' en el prefab.");
                Object.DestroyImmediate(instance);
                return;
            }

            var animator = body.GetComponent<Animator>();
            if (animator == null) animator = body.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // Asignar el Avatar del modelo importado (necesario para animaciones Generic)
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelFbxPath);
            if (avatar != null)
                animator.avatar = avatar;

            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SetupRunnerAnimations] Controller asignado al prefab RunnerEnemy (Avatar={(avatar != null ? avatar.name : "null")})");
        }
    }
}
