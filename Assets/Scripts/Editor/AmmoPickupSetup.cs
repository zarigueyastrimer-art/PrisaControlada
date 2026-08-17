using System.IO;
using PrisaControlada.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrisaControlada.EditorTools
{
    // Crea el prefab del item de municion (una caja/paquete simple) y lo instancia
    // en la escena activa frente a la camara. Al agarrarlo desaparece y suma balas externas.
    public static class AmmoPickupSetup
    {
        private const string AmmoPickupPrefabPath = "Assets/Prefabs/items/AmmoPickup.prefab";

        [MenuItem("Tools/PrisaControlada/Items/Setup Ammo Pickup")]
        public static void SetupPickup()
        {
            var root = new GameObject("AmmoPickup");

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.22f, 0.22f, 0.22f);

            var pickup = root.AddComponent<AmmoPickup>();

            // Visual simple: una caja de municion (cubo pequeño).
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "AmmoBox";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            var meshCol = visual.GetComponent<Collider>();
            if (meshCol != null) Object.DestroyImmediate(meshCol);

            EnsureFolder("Assets/Prefabs/items");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, AmmoPickupPrefabPath);
            Object.DestroyImmediate(root);

            if (saved == null)
            {
                Debug.LogError("[AmmoPickupSetup] No se pudo guardar el prefab: " + AmmoPickupPrefabPath);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(saved);
            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                instance.transform.position = cam.transform.position + cam.transform.forward * 1.5f + Vector3.up * 0.3f;
                instance.transform.rotation = Quaternion.identity;
            }
            else
            {
                instance.transform.position = Vector3.zero;
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[AmmoPickupSetup] Item de municion creado: " + AmmoPickupPrefabPath + " e instanciado en la escena.");
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