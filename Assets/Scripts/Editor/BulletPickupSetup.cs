using PrisaControlada.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrisaControlada.EditorTools
{
    // Crea el prefab de UNA bala suelta recogible (suma 1 bala externa al agarrarla).
    // Tiene 2 colliders: uno real del tamaño de la bala (fisico) y otro mas grande
    // como trigger de agarre, para que sea facil de agarrar aunque la bala sea chica.
    public static class BulletPickupSetup
    {
        private const string BulletPickupPrefabPath = "Assets/Resources/Items/BulletPickup.prefab";

        [MenuItem("Tools/PrisaControlada/Items/Setup Bullet Pickup")]
        public static void SetupPickup()
        {
            var root = new GameObject("BulletPickup");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.angularDamping = 0.8f;
            rb.sleepThreshold = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Collider de agarre (trigger, mas grande que la bala).
            var grabCol = root.AddComponent<SphereCollider>();
            grabCol.radius = 0.15f;
            grabCol.isTrigger = true;

            var pickup = root.AddComponent<AmmoPickup>();
            var amountField = typeof(AmmoPickup).GetField("amount",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (amountField != null) amountField.SetValue(pickup, 1);

            // Visual de la bala: vaina (cilindro) + punta (esfera), parada en vertical.
            var bullet = new GameObject("Bullet");
            bullet.transform.SetParent(root.transform, false);
            bullet.transform.localPosition = Vector3.zero;
            bullet.transform.localRotation = Quaternion.identity;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(bullet.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.025f, 0f);
            body.transform.localScale = new Vector3(0.035f, 0.07f, 0.035f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            AssignMaterial(body, new Color(0.9f, 0.75f, 0.25f), "BodyMat");

            var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "Nose";
            nose.transform.SetParent(bullet.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            nose.transform.localScale = new Vector3(0.035f, 0.05f, 0.035f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            AssignMaterial(nose, new Color(0.75f, 0.75f, 0.75f), "NoseMat");

            // Collider real del tamaño de la bala (fisico, no trigger). Un poco mayor
            // que lo visual para evitar tunneling al rebotar/caer.
            var realCol = bullet.AddComponent<CapsuleCollider>();
            realCol.radius = 0.03f;
            realCol.height = 0.16f;
            realCol.direction = 1; // Y (vertical)
            realCol.center = Vector3.zero;

            EnsureFolder("Assets/Resources/Items");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BulletPickupPrefabPath);
            Object.DestroyImmediate(root);

            if (saved == null)
            {
                Debug.LogError("[BulletPickupSetup] No se pudo guardar el prefab: " + BulletPickupPrefabPath);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(saved);
            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                instance.transform.position = cam.transform.position + cam.transform.forward * 1.5f + Vector3.up * 0.2f;
                instance.transform.rotation = Quaternion.identity;
            }
            else
            {
                instance.transform.position = Vector3.zero;
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[BulletPickupSetup] Bala recogible creada en Resources/Items (para Resources.Load desde WeaponController): " + BulletPickupPrefabPath + " e instanciada en la escena. Collider real (bala) + trigger de agarre.");
        }

        private static void AssignMaterial(GameObject go, Color color, string name)
        {
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.name = name;
            mr.sharedMaterial = mat;
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