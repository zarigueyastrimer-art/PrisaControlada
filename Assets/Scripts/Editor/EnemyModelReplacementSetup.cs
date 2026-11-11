using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PrisaControlada.Editor
{
    public static class EnemyModelReplacementSetup
    {
        private const string BlindPrefabPath = "Assets/Prefabs/Enemies/BlindEnemy.prefab";
        private const string RunnerPrefabPath = "Assets/Prefabs/Enemies/RunnerEnemy.prefab";
        private const string BlindIdleFbx = "Assets/MyAssets/blindPrototipo/Idle.fbx";
        private const string BlindRunFbx = "Assets/MyAssets/blindPrototipo/Run.fbx";
        private const string RunnerIdleFbx = "Assets/MyAssets/runerPrototipo/Anim/Runner40k@Unarmed Idle.fbx";
        private const string RunnerRunFbx = "Assets/MyAssets/runerPrototipo/Anim/Runner40k@Running Crawl.fbx";
        private const string BlindMatPath = "Assets/Materials/Enemies/BlindBody.mat";
        private const string RunnerMatPath = "Assets/Materials/Enemies/RunnerBody.mat";
        private const string ResourcesFolder = "Assets/Resources";
        private const string BlindControllerPath = "Assets/Resources/BlindAnimator.controller";
        private const string RunnerControllerPath = "Assets/Resources/RunnerAnimator.controller";
        private const float TargetHeight = 2f;

        [MenuItem("Tools/PrisaControlada/Replace Blind Runner Models")]
        public static void ReplaceModels()
        {
            Debug.Log("[EnemyModelSetup] Iniciando...");

            EnsureResourcesFolder();
            RuntimeAnimatorController blindCtrl = CreateSpeedController(
                BlindControllerPath, BlindIdleFbx, BlindRunFbx, "Blind");
            RuntimeAnimatorController runnerCtrl = CreateSpeedController(
                RunnerControllerPath, RunnerIdleFbx, RunnerRunFbx, "Runner");

            ReplaceOne(BlindPrefabPath, BlindIdleFbx, BlindMatPath, "Blind", blindCtrl);
            ReplaceOne(RunnerPrefabPath, RunnerIdleFbx, RunnerMatPath, "Runner", runnerCtrl);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyModelSetup] Listo. Mesh + Animator (Idle/Run) en Blind y Runner.");
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // Controller con parametro Speed: Idle <-> Run (mismo patron que Zombie).
        private static RuntimeAnimatorController CreateSpeedController(
            string controllerPath, string idleFbx, string runFbx, string label)
        {
            AnimationClip idleClip = LoadClip(idleFbx, true);
            AnimationClip runClip = LoadClip(runFbx, true);

            if (idleClip == null || runClip == null)
            {
                Debug.LogError($"[EnemyModelSetup] {label}: faltan clips idle/run.");
                return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idleState = sm.AddState("Idle", new Vector3(0, 0, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState runState = sm.AddState("Run", new Vector3(250, 0, 0));
            runState.motion = runClip;

            AnimatorStateTransition toRun = idleState.AddTransition(runState);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            toRun.hasExitTime = false;
            toRun.duration = 0.15f;

            AnimatorStateTransition toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[EnemyModelSetup] {label} controller creado: Idle + Run (Speed).");
            return controller;
        }

        private static AnimationClip LoadClip(string fbxPath, bool loop)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (Object obj in assets)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                {
                    SerializedObject so = new SerializedObject(clip);
                    SerializedProperty loopProp = so.FindProperty("m_AnimationClipSettings.m_LoopTime");
                    if (loopProp != null)
                    {
                        loopProp.boolValue = loop;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    Debug.Log($"[EnemyModelSetup] Clip: {clip.name} en {fbxPath}");
                    return clip;
                }
            }

            Debug.LogError($"[EnemyModelSetup] Sin clip en {fbxPath}");
            return null;
        }

        private static void ReplaceOne(
            string prefabPath, string fbxPath, string matPath, string label,
            RuntimeAnimatorController controller)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[EnemyModelSetup] Prefab no encontrado: {prefabPath}");
                return;
            }

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[EnemyModelSetup] FBX no encontrado: {fbxPath}");
                return;
            }

            Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            for (int i = instance.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = instance.transform.GetChild(i);
                string n = child.name;
                if (n == "Lenses" || n == "Body" || n == "Eyes" || n == "Ojos" || n == "Anteojos")
                {
                    Object.DestroyImmediate(child.gameObject);
                    Debug.Log($"[EnemyModelSetup] {label}: borrado '{n}'");
                }
            }

            GameObject newBody = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            newBody.name = "Body";
            newBody.transform.SetParent(instance.transform, false);
            newBody.transform.localPosition = Vector3.zero;
            newBody.transform.localRotation = Quaternion.identity;
            newBody.transform.localScale = Vector3.one;

            FitHeight(newBody, TargetHeight);
            SnapFeetToOrigin(newBody);

            if (bodyMat != null)
            {
                foreach (Renderer r in newBody.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = bodyMat;
                    r.sharedMaterials = mats;
                }
            }

            ConfigureAnimator(newBody, controller, label);

            foreach (Renderer r in newBody.GetComponentsInChildren<Renderer>(true))
            {
                Debug.Log($"[EnemyModelSetup] {label} renderer '{r.gameObject.name}' bounds={r.bounds.size}");
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            Debug.Log($"[EnemyModelSetup] Prefab guardado: {prefabPath}");
        }

        private static void ConfigureAnimator(GameObject body, RuntimeAnimatorController controller, string label)
        {
            Animator animator = body.GetComponent<Animator>();
            if (animator == null)
                animator = body.AddComponent<Animator>();

            if (controller != null)
                animator.runtimeAnimatorController = controller;

            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Fixed;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            Debug.Log($"[EnemyModelSetup] {label}: Animator listo (ctrl={(controller != null ? controller.name : "null")})");
        }

        private static void FitHeight(GameObject root, float targetHeight)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            if (b.size.y < 0.001f) return;

            float scale = targetHeight / b.size.y;
            root.transform.localScale = new Vector3(scale, scale, scale);
        }

        private static void SnapFeetToOrigin(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            Vector3 worldFeet = new Vector3(root.transform.position.x, b.min.y, root.transform.position.z);
            Vector3 localOffset = root.transform.InverseTransformPoint(worldFeet);
            root.transform.localPosition -= new Vector3(0f, localOffset.y, 0f);
        }
    }
}
