using System;
using PrisaControlada.Enemies;
using UnityEditor;
using UnityEngine;

namespace PrisaControlada.Editor
{
    public static class EnemyPrefabSetup
    {
        private const string EnemyFolder = "Assets/Prefabs/Enemies";
        private const string MaterialFolder = "Assets/Materials/Enemies";

        public static void CreateEnemyPrefabs()
        {
            EnsureFolder(EnemyFolder);
            EnsureFolder(MaterialFolder);

            foreach (EnemyType type in Enum.GetValues(typeof(EnemyType)))
            {
                EnemyBase enemy = EnemyFactory.Create(type, Vector3.zero, $"{type}Enemy");
                FixMaterials(enemy.gameObject, type);
                PrefabUtility.SaveAsPrefabAsset(enemy.gameObject, $"{EnemyFolder}/{type}Enemy.prefab");
                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }

            GameObject spawner = new GameObject("EnemySpawner");
            spawner.AddComponent<EnemySpawner>();
            PrefabUtility.SaveAsPrefabAsset(spawner, $"{EnemyFolder}/EnemySpawner.prefab");
            UnityEngine.Object.DestroyImmediate(spawner);

            AssetDatabase.SaveAssets();
            Debug.Log($"[EnemyPrefabSetup] Prefabs de enemigos + spawner creados en {EnemyFolder}");
        }

        private static void FixMaterials(GameObject enemy, EnemyType type)
        {
            Material bodyMat = GetOrCreateMaterial($"{MaterialFolder}/{type}Body.mat", EnemyFactory.GetColor(type));
            Material lensMat = GetOrCreateMaterial($"{MaterialFolder}/{type}Lens.mat", Color.white);

            // Blind y Runner ya no usan capsula ni Lenses (mesh FBX del prototipo).
            bool usesPrototypeModel = type == EnemyType.Blind || type == EnemyType.Runner;

            foreach (MeshRenderer renderer in enemy.GetComponentsInChildren<MeshRenderer>())
            {
                if (!usesPrototypeModel && renderer.name == "Lenses")
                    renderer.sharedMaterial = lensMat;
                else
                    renderer.sharedMaterial = bodyMat;
            }
        }

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
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