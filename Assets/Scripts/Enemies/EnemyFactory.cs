using System;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Enemies
{
    public enum EnemyType
    {
        Zombie,
        Runner,
        Blind,
        Nemesis
    }

    public static class EnemyFactory
    {
        private static Mesh _capsuleMesh;

        // Prototipos de personaje (sin animaciones aun) para Blind y Runner.
        private const string BlindModelPath = "Assets/MyAssets/blindPrototipo/Idle.fbx";
        // Modelos FBX: Blind (prototipo) y Runner (modelo real con UVs + esqueleto).
        private const string RunnerModelPath = "Assets/MyAssets/runer/Runner40k_Rigged.fbx";
        private const float ModelTargetHeight = 2f;

        public static EnemyBase Create(EnemyType type, Vector3 position, string name = null, Quaternion? rotation = null)
        {
            // Pivot en los pies: transform.position = contacto con el suelo.
            GameObject go = new GameObject(name ?? $"{type}Enemy");
            go.transform.position = position;
            go.transform.rotation = rotation ?? Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            if (!TryAttachModel(go, type))
            {
                // Fallback: capsula + lentes (solo tipos sin modelo de prototipo).
                GameObject body = new GameObject("Body");
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = new Vector3(0f, 1f, 0f);

                MeshFilter mf = body.AddComponent<MeshFilter>();
                mf.sharedMesh = GetCapsuleMesh();

                MeshRenderer mr = body.AddComponent<MeshRenderer>();
                mr.sharedMaterial = CreateMaterial(GetColor(type));

                AddLenses(go);
            }

            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            agent.speed = 2f;
            agent.acceleration = 8f;
            agent.angularSpeed = 720f;
            agent.updateRotation = false;

            // Solid collider so the player cannot walk through the enemy.
            CapsuleCollider bodyCollider = go.AddComponent<CapsuleCollider>();
            bodyCollider.radius = 0.45f;
            bodyCollider.height = 2f;
            bodyCollider.center = new Vector3(0f, 1f, 0f);

            Type componentType = type switch
            {
                EnemyType.Zombie => typeof(ZombieEnemy),
                EnemyType.Runner => typeof(RunnerEnemy),
                EnemyType.Blind => typeof(BlindEnemy),
                _ => typeof(NemesisEnemy)
            };

            EnemyBase enemy = (EnemyBase)go.AddComponent(componentType);
            enemy.ApplyDefaultStats();

            return enemy;
        }

        // Devuelve true si se adjunto el mesh FBX del prototipo (sin capsula ni lentes).
        private static bool TryAttachModel(GameObject go, EnemyType type)
        {
            string fbxPath = type switch
            {
                EnemyType.Blind => BlindModelPath,
                EnemyType.Runner => RunnerModelPath,
                _ => null
            };

            if (string.IsNullOrEmpty(fbxPath)) return false;

            GameObject fbx = LoadModelAsset(fbxPath);
            if (fbx == null) return false;

            GameObject body = UnityEngine.Object.Instantiate(fbx, go.transform, false);
            body.name = "Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            FitHeight(body, ModelTargetHeight);
            SnapFeetToOrigin(body);
            ApplyBodyMaterial(body, type);
            ConfigureAnimator(body, type);

            return true;
        }

        private static void ConfigureAnimator(GameObject body, EnemyType type)
        {
            Animator animator = body.GetComponent<Animator>();
            if (animator == null)
                animator = body.AddComponent<Animator>();

            RuntimeAnimatorController controller = LoadController(type);
            if (controller != null)
                animator.runtimeAnimatorController = controller;

            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Fixed;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        private static RuntimeAnimatorController LoadController(EnemyType type)
        {
            string resName = type switch
            {
                EnemyType.Blind => "BlindAnimator",
                EnemyType.Runner => "RunnerAnimator",
                _ => null
            };

            if (string.IsNullOrEmpty(resName)) return null;

            RuntimeAnimatorController fromRes = Resources.Load<RuntimeAnimatorController>(resName);
            if (fromRes != null) return fromRes;

#if UNITY_EDITOR
            string assetPath = type switch
            {
                EnemyType.Blind => "Assets/Resources/BlindAnimator.controller",
                EnemyType.Runner => "Assets/Resources/RunnerAnimator.controller",
                _ => null
            };
            if (!string.IsNullOrEmpty(assetPath))
                return UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(assetPath);
#endif
            return null;
        }

        private static GameObject LoadModelAsset(string fbxPath)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
#else
            return null;
#endif
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

        private static void ApplyBodyMaterial(GameObject body, EnemyType type)
        {
            Material mat = null;
#if UNITY_EDITOR
            string matPath = type switch
            {
                EnemyType.Blind => "Assets/Materials/Enemies/BlindBody.mat",
                EnemyType.Runner => "Assets/Materials/Enemies/Runner_Lit.mat",
                _ => null
            };
            if (!string.IsNullOrEmpty(matPath))
                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
#endif
            if (mat == null)
                mat = CreateMaterial(GetColor(type));

            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void AddLenses(GameObject parent)
        {
            GameObject lenses = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lenses.name = "Lenses";
            lenses.transform.SetParent(parent.transform, false);
            lenses.transform.localPosition = new Vector3(0f, 1.55f, 0.55f);
            lenses.transform.localScale = new Vector3(0.18f, 0.08f, 0.08f);

            Collider collider = lenses.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            lenses.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(1f, 1f, 1f));
        }

        private static Mesh GetCapsuleMesh()
        {
            if (_capsuleMesh != null) return _capsuleMesh;

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _capsuleMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temp);

            return _capsuleMesh;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        public static Color GetColor(EnemyType type)
        {
            return type switch
            {
                EnemyType.Zombie => new Color(0.8f, 0.1f, 0.1f),
                EnemyType.Runner => new Color(0.1f, 0.1f, 0.1f),
                EnemyType.Blind => new Color(0.15f, 0.35f, 0.9f),
                _ => new Color(0.9f, 0.8f, 0.1f)
            };
        }
    }
}
