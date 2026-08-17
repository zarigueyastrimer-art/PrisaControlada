using System.IO;
using PrisaControlada.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrisaControlada.EditorTools
{
    // Crea el prefab del arma como OBJETO recogible (solo el arma, sin manos) y
    // lo instancia en la escena activa frente a la camara. Requiere haber corrido
    // antes "Setup Revolver SPLIT" para que exista gun.fbx.
    public static class RevolverPickupSetup
    {
        private const string GunFbxPath = "Assets/MyAssets/guns/revolver/gun.fbx";
        private const string PickupPrefabPath = "Assets/Prefabs/items/RevolverPickup.prefab";

        [MenuItem("Tools/PrisaControlada/Weapons/Setup Revolver Pickup")]
        public static void SetupPickup()
        {
            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunFbxPath);
            if (gunPrefab == null)
            {
                Debug.LogError("[RevolverPickupSetup] No existe gun.fbx. Corri primero 'Setup Revolver SPLIT'.");
                return;
            }

            var root = new GameObject("RevolverPickup");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.sleepThreshold = 0f;

            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.25f;

            root.AddComponent<WeaponPickup>();

            // El arma sola (mesh) como hijo, a escala real (la misma del viewmodel: 0.01).
            var gun = (GameObject)PrefabUtility.InstantiatePrefab(gunPrefab);
            gun.name = "RevolverGunMesh";
            gun.transform.SetParent(root.transform, false);
            gun.transform.localPosition = Vector3.zero;
            gun.transform.localRotation = Quaternion.identity;
            gun.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            // En el suelo no debe animar ni colisionar por mesh: se quita el Animator
            // y se apagan colliders propios del modelo (colisiona el SphereCollider del root).
            var anim = gun.GetComponent<Animator>();
            if (anim != null) Object.DestroyImmediate(anim);
            foreach (var c in gun.GetComponentsInChildren<Collider>())
                c.enabled = false;

            EnsureFolder("Assets/Prefabs/items");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PickupPrefabPath);
            Object.DestroyImmediate(root);

            if (saved == null)
            {
                Debug.LogError("[RevolverPickupSetup] No se pudo guardar el prefab: " + PickupPrefabPath);
                return;
            }

            // Instancia en la escena, frente a la camara principal.
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
            Debug.Log("[RevolverPickupSetup] Pickup creado: " + PickupPrefabPath + " e instanciado en la escena. Ajusta localScale/orientacion del hijo RevolverGunMesh si el arma se ve mal en el suelo.");
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