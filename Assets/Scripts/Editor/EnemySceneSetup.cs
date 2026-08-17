using PrisaControlada.Core;
using PrisaControlada.Enemies;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrisaControlada.Editor
{
    public static class EnemySceneSetup
    {
        public static void AddTestEnemies()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Add Test Enemies", "Save the scene first.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            Vector3 basePos = player != null ? player.transform.position : Vector3.zero;

            EnsureNavigation();

            EnemyFactory.Create(EnemyType.Zombie, basePos + new Vector3(4f, 0.05f, 4f));
            EnemyFactory.Create(EnemyType.Runner, basePos + new Vector3(-4f, 0.05f, 4f));
            EnemyFactory.Create(EnemyType.Blind, basePos + new Vector3(4f, 0.05f, -4f));
            EnemyFactory.Create(EnemyType.Nemesis, basePos + new Vector3(-4f, 0.05f, -4f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[EnemySceneSetup] 4 test enemies spawned (red=zombie, black=runner, blue=blind, yellow=nemesis).");
        }

        public static void AddEnemySpawner()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Add Enemy Spawner", "Save the scene first.", "OK");
                return;
            }

            GameObject spawnerGo = new GameObject("EnemySpawner");
            EnemySpawner spawner = spawnerGo.AddComponent<EnemySpawner>();

            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty points = so.FindProperty("spawnPoints");
            points.arraySize = 4;

            GameObject player = GameObject.Find("Player");
            Vector3 basePos = player != null ? player.transform.position : Vector3.zero;

            EnsureNavigation();

            SetSpawnPoint(points, 0, EnemyType.Zombie, basePos + new Vector3(4f, 0.05f, 4f));
            SetSpawnPoint(points, 1, EnemyType.Runner, basePos + new Vector3(-4f, 0.05f, 4f));
            SetSpawnPoint(points, 2, EnemyType.Blind, basePos + new Vector3(4f, 0.05f, -4f));
            SetSpawnPoint(points, 3, EnemyType.Nemesis, basePos + new Vector3(-4f, 0.05f, -4f));
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[EnemySceneSetup] EnemySpawner added with 4 spawn points.");
        }

        public static void SetupNavigation()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Setup Navigation", "Save the scene first.", "OK");
                return;
            }

            if (EnsureNavigation())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[EnemySceneSetup] Navigation ready (NavMeshSurface + RuntimeNavMesh).");
            }
            else
            {
                Debug.Log("[EnemySceneSetup] Navigation already exists.");
            }
        }

        private static bool EnsureNavigation()
        {
            if (GameObject.Find("Navigation") != null)
                return false;

            GameObject nav = new GameObject("Navigation");
            NavMeshSurface surface = nav.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            nav.AddComponent<RuntimeNavMesh>();

            return true;
        }

        private static void SetSpawnPoint(SerializedProperty points, int index, EnemyType type, Vector3 position)
        {
            SerializedProperty element = points.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("type").enumValueIndex = (int)type;
            element.FindPropertyRelative("position").vector3Value = position;
            element.FindPropertyRelative("spawnOnStart").boolValue = true;
        }
    }
}