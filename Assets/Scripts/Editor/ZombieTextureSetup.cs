using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PrisaControlada.Editor
{
    public static class ZombieTextureSetup
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/ZombieEnemy.prefab";
        private const string FBXPath = "Assets/MyAssets/zombie/zombieEsqueleteado.fbx";
        private const string TexPath = "Assets/Resources/Zombie/Textures";
        private const string MatDir = "Assets/Materials/Enemies";
        private const string AnimDir = "Assets/MyAssets/zombie/animacion";
        private const string ControllerPath = "Assets/Animators/ZombieAnimator.controller";

        [MenuItem("Tools/PrisaControlada/Setup Zombie Textures")]
        public static void Setup()
        {
            Debug.Log("[ZombieSetup] Iniciando...");
            ConfigureTextures();
            CreateAnimatorController();
            ReplaceBodyInPrefab();
            SetupRagdoll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZombieSetup] Listo.");
        }

        [MenuItem("Tools/PrisaControlada/Log Bone Hierarchy")]
        public static void LogBoneHierarchy()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBXPath);
            if (fbx == null) { Debug.LogError("FBX not found"); return; }
            LogChildren(fbx.transform, 0);
        }

        private static void LogChildren(Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            Debug.Log($"{indent}{t.name}");
            foreach (Transform child in t)
                LogChildren(child, depth + 1);
        }

        private static void ConfigureTextures()
        {
            SetNormalMap($"{TexPath}/male_zbrush_material_Normal_OpenGL.png");
            SetLinear($"{TexPath}/male_zbrush_material_Metallic.png");
            SetLinear($"{TexPath}/male_zbrush_material_Roughness.png");
            SetLinear($"{TexPath}/male_zbrush_material_Mixed_AO.png");
            SetNormalMap($"{TexPath}/PM3D_Sphere3D1_material_Normal_OpenGL.png");
            SetNormalMap($"{TexPath}/PM3D_Sphere3D2_material_Normal_OpenGL.png");
            SetNormalMap($"{TexPath}/PM3D_Sphere3D1_1_material_Normal_OpenGL.png");
            SetNormalMap($"{TexPath}/PM3D_Cube3D1_material_Normal_OpenGL.png");
        }

        private static void CreateAnimatorController()
        {
            string dir = System.IO.Path.GetDirectoryName(ControllerPath);
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", "Animators");

            AnimationClip idleClip = LoadClip($"{AnimDir}/idel.fbx", "idel", true);
            AnimationClip runClip = LoadClip($"{AnimDir}/run1.fbx", "run1", true);
            AnimationClip attackClip = LoadClip($"{AnimDir}/attack.fbx", "attack", false);

            if (idleClip == null || runClip == null || attackClip == null)
            {
                Debug.LogError("[ZombieSetup] No se encontraron los clips de animacion.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CanExitAttack", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idleState = sm.AddState("Idle", new Vector3(0, 0, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState runState = sm.AddState("Run", new Vector3(250, 0, 0));
            runState.motion = runClip;

            AnimatorState attackState = sm.AddState("Attack", new Vector3(500, 0, 0));
            attackState.motion = attackClip;
            attackState.speed = 1f;

            // Idle <-> Run
            AnimatorStateTransition toRun = idleState.AddTransition(runState);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            toRun.hasExitTime = false;
            toRun.duration = 0.15f;

            AnimatorStateTransition toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;

            // Cualquier estado -> Attack (trigger)
            AnimatorStateTransition toAttack = idleState.AddTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;

            AnimatorStateTransition toAttackFromRun = runState.AddTransition(attackState);
            toAttackFromRun.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAttackFromRun.hasExitTime = false;
            toAttackFromRun.duration = 0.1f;

            // Attack -> Run (forzado por script via CanExitAttack)
            AnimatorStateTransition attackToRun = attackState.AddTransition(runState);
            attackToRun.AddCondition(AnimatorConditionMode.If, 0, "CanExitAttack");
            attackToRun.hasExitTime = false;
            attackToRun.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            Debug.Log("[ZombieSetup] Animator Controller creado (Idle, Run, Attack).");
        }

        private static AnimationClip LoadClip(string fbxPath, string clipName, bool loop)
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

                    Debug.Log($"[ZombieSetup] Clip encontrado: {clip.name} (loop={loop}) en {fbxPath}");
                    return clip;
                }
            }

            Debug.LogError($"[ZombieSetup] No se encontro clip en {fbxPath}");
            return null;
        }

        private static void ReplaceBodyInPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError($"[ZombieSetup] Prefab no encontrado: {PrefabPath}"); return; }

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBXPath);
            if (fbx == null) { Debug.LogError($"[ZombieSetup] FBX no encontrado: {FBXPath}"); return; }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // Destruir Body viejo
            Transform oldBody = instance.transform.Find("Body");
            if (oldBody != null)
            {
                Object.DestroyImmediate(oldBody.gameObject);
                Debug.Log("[ZombieSetup] Body viejo destruido.");
            }

            // Instanciar FBX como nuevo Body
            GameObject newBody = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            newBody.name = "Body";
            newBody.transform.SetParent(instance.transform, false);
            newBody.transform.localPosition = Vector3.zero;
            newBody.transform.localRotation = Quaternion.identity;
            newBody.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);

            // Agregar Animator al Body
            Animator animator = newBody.AddComponent<Animator>();
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Fixed;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // Materiales
            Material matBody = CreateMat("ZombieBody", "male_zbrush_material");
            Material matEyeL = CreateMat("ZombieEyeL", "PM3D_Sphere3D1_material");
            Material matEyeR = CreateMat("ZombieEyeR", "PM3D_Sphere3D2_material");
            Material matTongue = CreateMat("ZombieTongue", "PM3D_Sphere3D1_1_material");
            Material matTeeth = CreateMat("ZombieTeeth", "PM3D_Cube3D1_material");

            Renderer[] renderers = newBody.GetComponentsInChildren<Renderer>();
            Debug.Log($"[ZombieSetup] Renderers: {renderers.Length}");
            foreach (Renderer r in renderers)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                string meshName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "SkinnedMesh";
                Debug.Log($"[ZombieSetup]   - {r.gameObject.name} | Mesh: {meshName} | Slots: {r.sharedMaterials.Length}");
            }

            foreach (Renderer r in renderers)
            {
                string goName = r.gameObject.name.ToLower();
                string meshName = "";
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) meshName = mf.sharedMesh.name.ToLower();

                Material mat = matBody;

                if (goName.Contains("sphere3d1_1") || meshName.Contains("sphere3d1_1"))
                    mat = matTongue;
                else if (goName.Contains("sphere3d2") || meshName.Contains("sphere3d2"))
                    mat = matEyeR;
                else if (goName.Contains("sphere3d1") || meshName.Contains("sphere3d1"))
                    mat = matEyeL;
                else if (goName.Contains("cube3d1") || meshName.Contains("cube3d1"))
                    mat = matTeeth;

                int count = r.sharedMaterials.Length;
                Material[] mats = new Material[count];
                for (int i = 0; i < count; i++) mats[i] = mat;
                r.sharedMaterials = mats;

                Debug.Log($"[ZombieSetup]   -> {r.gameObject.name} = {mat.name}");
            }

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            Debug.Log($"[ZombieSetup] Prefab guardado: {PrefabPath}");
        }

        private static Material CreateMat(string matName, string texPrefix)
        {
            string matPath = $"{MatDir}/{matName}.mat";

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader) { name = matName };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.3f);
            mat.SetFloat("_Metallic", 0f);

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexPath}/{texPrefix}_Base_color.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexPath}/{texPrefix}_Normal_OpenGL.png");
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexPath}/{texPrefix}_Metallic.png");
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexPath}/{texPrefix}_Mixed_AO.png");

            if (baseColor != null) mat.SetTexture("_BaseMap", baseColor);
            if (normal != null) mat.SetTexture("_BumpMap", normal);
            if (metallic != null) mat.SetTexture("_MetallicGlossMap", metallic);
            if (ao != null) mat.SetTexture("_OcclusionMap", ao);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetNormalMap(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }

        private static void SetLinear(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }

        private static void SetupRagdoll()
        {
            Debug.Log("[ZombieSetup] SetupRagdoll via Ragdoll-Controller...");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("[ZombieSetup] Prefab no encontrado para ragdoll"); return; }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Transform body = instance.transform.Find("Body");
            if (body == null) { Debug.LogError("[ZombieSetup] Body no encontrado para ragdoll"); Object.DestroyImmediate(instance); return; }

            // Limpiar ragdoll existente
            foreach (var comp in body.GetComponentsInChildren<UnityEngine.Joint>())
                Object.DestroyImmediate(comp);
            foreach (var comp in body.GetComponentsInChildren<Rigidbody>())
                Object.DestroyImmediate(comp);
            foreach (var comp in body.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(comp);
            foreach (var comp in body.GetComponentsInChildren<RagdollComponent>())
                Object.DestroyImmediate(comp);
            foreach (var comp in body.GetComponentsInChildren<Ragdoll>())
                Object.DestroyImmediate(comp);

            RagdollCreator creator = body.gameObject.AddComponent<RagdollCreator>();
            creator.CreateRagdoll();
            Object.DestroyImmediate(creator);

            // Desactivar colliders y poner rigidbodies kinematic (para activar al morir)
            foreach (var rb in body.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            foreach (var col in body.GetComponentsInChildren<Collider>())
                col.enabled = false;

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            Debug.Log("[ZombieSetup] Ragdoll configurado via Ragdoll-Controller.");
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
