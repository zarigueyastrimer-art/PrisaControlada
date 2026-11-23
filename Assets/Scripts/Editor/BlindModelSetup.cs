using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    public static class BlindModelSetup
    {
        public const string FbxPath = "Assets/MyAssets/blind/1588e62274e84b78a9bade886f7c107f.fbx";
        public const string BaseTexturePath = "Assets/MyAssets/blind/textures/5010787806e84d0782efba570eb0e6a6_RGB_color1c.png";
        public const string AlphaMaskTexturePath = "Assets/MyAssets/blind/textures/40402b3845164edc9d4431f3d2008b23_A_color1c-alphamaskDef1.png";
        public const string EmissionTexturePath = "Assets/MyAssets/blind/textures/20a09f966bc44ceb950c8226ae8a8d6d_RGB_emit.png";

        private const string PrefabPath = "Assets/Prefabs/Enemies/BlindEnemy.prefab";
        private const string MaterialPath = "Assets/Materials/Enemies/BlindBody.mat";
        private const string ShaderPath = "Assets/Shaders/BlindBody.shader";
        private const string ControllerPath = "Assets/Resources/BlindAnimator.controller";
        private const string AnimationFolder = "Assets/Resources/BlindAnimations";
        private const string IdleClipPath = AnimationFolder + "/BlindStaticPose.anim";
        private const string HoverClipPath = AnimationFolder + "/BlindHovering.anim";
        private const string IdleSourceClipName = "Static Pose";
        private const string HoverSourceClipName = "hovering";
        private const float TargetHeight = 2f;

        [MenuItem("Tools/PrisaControlada/Setup Blind Model")]
        public static void Setup()
        {
            Debug.Log("[BlindSetup] Configurando modelo, texturas, material, Animator y prefab...");

            ConfigureTextures();
            Material bodyMaterial = ConfigureMaterial();
            AnimationClip idleClip = CreateWorkingClip(IdleSourceClipName, IdleClipPath, false);
            AnimationClip hoverClip = CreateWorkingClip(HoverSourceClipName, HoverClipPath, true);
            AnimatorController controller = ConfigureController(idleClip, hoverClip);

            if (bodyMaterial != null && controller != null)
                ReplaceBody(bodyMaterial, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BlindSetup] Listo. Nuevo modelo aplicado con Alpha Mask y emision.");
        }

        private static void ConfigureTextures()
        {
            ConfigureTexture(BaseTexturePath, false);
            ConfigureTexture(AlphaMaskTexturePath, false);
            ConfigureTexture(EmissionTexturePath, false);
        }

        private static void ConfigureTexture(string path, bool linear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[BlindSetup] No se encontro TextureImporter: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static Material ConfigureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Texture2D baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexturePath);
            Texture2D alphaMask = AssetDatabase.LoadAssetAtPath<Texture2D>(AlphaMaskTexturePath);
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionTexturePath);

            if (material == null)
            {
                Debug.LogError($"[BlindSetup] Material no encontrado: {MaterialPath}");
                return null;
            }

            if (shader == null)
            {
                Debug.LogError($"[BlindSetup] Shader no encontrado: {ShaderPath}");
                return null;
            }

            if (baseTexture == null || alphaMask == null || emission == null)
            {
                Debug.LogError("[BlindSetup] Faltan texturas del modelo Blind.");
                return null;
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", baseTexture);
            material.SetTexture("_AlphaMaskMap", alphaMask);
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_EmissionColor", Color.white);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Smoothness", 0.35f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            material.doubleSidedGI = false;
            EditorUtility.SetDirty(material);

            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            foreach (ShaderMessage message in messages)
            {
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    Debug.LogError($"[BlindSetup] Error de shader: {message.message} ({message.messageDetails})");
            }

            return material;
        }

        private static AnimationClip CreateWorkingClip(string sourceName, string assetPath, bool loop)
        {
            AnimationClip source = FindClip(sourceName);
            if (source == null)
            {
                Debug.LogError($"[BlindSetup] Clip no encontrado: {sourceName}");
                return null;
            }

            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(assetPath);

            AnimationClip copy = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                frameRate = source.frameRate,
                legacy = false
            };

            EditorUtility.CopySerialized(source, copy);
            copy.name = Path.GetFileNameWithoutExtension(assetPath);
            copy.hideFlags = HideFlags.None;

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, settings);

            AssetDatabase.CreateAsset(copy, assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[BlindSetup] Clip de trabajo creado: {assetPath} (loop={loop})");
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static AnimationClip FindClip(string clipName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                    return clip;
            }

            return null;
        }

        private static AnimatorController ConfigureController(AnimationClip idleClip, AnimationClip hoverClip)
        {
            if (idleClip == null || hoverClip == null)
            {
                Debug.LogError("[BlindSetup] No se pudieron crear los clips del Animator.");
                return null;
            }

            EnsureFolder("Assets/Resources");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            bool hasSpeedParameter = false;
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
            {
                AnimatorControllerParameter parameter = controller.parameters[i];
                if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasSpeedParameter = true;
                    continue;
                }

                controller.RemoveParameter(i);
            }

            if (!hasSpeedParameter)
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            if (controller.layers.Length == 0)
            {
                Debug.LogError("[BlindSetup] El controller no tiene capas.");
                return null;
            }

            controller.layers[0].name = "Base Layer";
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            AnimatorState idleState = FindOrAddState(stateMachine, "Idle");
            AnimatorState hoverState = FindOrAddState(stateMachine, "Run");
            if (hoverState == idleState)
                hoverState = FindOrAddState(stateMachine, "Hover");

            hoverState.name = "Hover";
            idleState.name = "Idle";
            idleState.motion = idleClip;
            hoverState.motion = hoverClip;
            idleState.speed = 1f;
            hoverState.speed = 1f;
            idleState.writeDefaultValues = true;
            hoverState.writeDefaultValues = true;

            idleState.transitions = Array.Empty<AnimatorStateTransition>();
            hoverState.transitions = Array.Empty<AnimatorStateTransition>();
            stateMachine.defaultState = idleState;

            AnimatorStateTransition toHover = idleState.AddTransition(hoverState);
            toHover.hasExitTime = false;
            toHover.duration = 0.15f;
            toHover.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition toIdle = hoverState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[BlindSetup] Animator actualizado: Idle (Static Pose) <-> Hover (hovering), parametro Speed.");
            return controller;
        }

        private static AnimatorState FindOrAddState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == name)
                    return child.state;
            }

            return stateMachine.AddState(name, new Vector3(0, 0, 0));
        }

        private static void ReplaceBody(Material bodyMaterial, RuntimeAnimatorController controller)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[BlindSetup] FBX no encontrado: {FbxPath}");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[BlindSetup] No se pudo abrir el prefab: {PrefabPath}");
                return;
            }

            try
            {
                Transform oldBody = prefabRoot.transform.Find("Body");
                if (oldBody != null)
                {
                    GameObject oldBodyObject = oldBody.gameObject;
                    if (PrefabUtility.IsPartOfPrefabInstance(oldBodyObject))
                        PrefabUtility.UnpackPrefabInstance(oldBodyObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    UnityEngine.Object.DestroyImmediate(oldBodyObject);
                }

                for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = prefabRoot.transform.GetChild(i);
                    string childName = child.name;
                    if (childName == "Lenses" || childName == "Eyes" || childName == "Ojos" || childName == "Anteojos")
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                GameObject newBody = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                newBody.name = "Body";
                newBody.transform.SetParent(prefabRoot.transform, false);
                newBody.transform.localPosition = Vector3.zero;
                newBody.transform.localRotation = Quaternion.identity;
                newBody.transform.localScale = Vector3.one;

                FitHeight(newBody, TargetHeight);
                SnapFeetToOrigin(newBody);
                ApplyMaterial(newBody, bodyMaterial);
                ConfigureAnimator(newBody, controller);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Debug.Log($"[BlindSetup] Prefab actualizado: {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ApplyMaterial(GameObject body, Material material)
        {
            Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        private static void ConfigureAnimator(GameObject body, RuntimeAnimatorController controller)
        {
            Animator animator = body.GetComponent<Animator>();
            if (animator == null)
                animator = body.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.avatar = null;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Fixed;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        private static void FitHeight(GameObject root, float targetHeight)
        {
            Bounds bounds = GetWorldBounds(root);
            if (bounds.size.y < 0.001f)
                return;

            float parentHeightScale = root.transform.parent == null
                ? 1f
                : Mathf.Abs(root.transform.parent.lossyScale.y);
            float localHeight = bounds.size.y / Mathf.Max(parentHeightScale, 0.0001f);
            float scale = targetHeight / Mathf.Max(localHeight, 0.0001f);
            root.transform.localScale = new Vector3(scale, scale, scale);
        }

        private static void SnapFeetToOrigin(GameObject root)
        {
            Bounds bounds = GetWorldBounds(root);
            Vector3 lowestWorld = new Vector3(root.transform.position.x, bounds.min.y, root.transform.position.z);
            Vector3 localLowest = root.transform.InverseTransformPoint(lowestWorld);
            root.transform.localPosition -= new Vector3(0f, localLowest.y, 0f);
        }

        private static Bounds GetWorldBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
