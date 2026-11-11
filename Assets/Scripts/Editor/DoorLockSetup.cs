using PrisaControlada.Items;
using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrisaControlada.Editor
{
    // Menues para montar las puertas con llave / codigo y el HUD numerico.
    public static class DoorLockSetup
    {
        private const string KeyPrefabPath = "Assets/Prefabs/items/KeyPickup.prefab";

        [MenuItem("Tools/PrisaControlada/Setup Code UI + Prompt")]
        public static void SetupCodeUi()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Setup Code UI", "No se encontro Player en la escena.", "OK");
                return;
            }

            if (player.GetComponent<CodeUI>() == null)
                player.AddComponent<CodeUI>();
            if (player.GetComponent<InteractionPrompt>() == null)
                player.AddComponent<InteractionPrompt>();

            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorUtility.SetDirty(player);
            Debug.Log("[DoorLockSetup] CodeUI + InteractionPrompt agregados al Player.");
        }

        [MenuItem("Tools/PrisaControlada/Create Key Pickup")]
        public static void CreateKeyPickup()
        {
            GameObject key = new GameObject("KeyPickup");
            string matPath = "Assets/Materials/Items/Key_Gold.mat";
            if (!System.IO.Directory.Exists("Assets/Materials/Items"))
                System.IO.Directory.CreateDirectory("Assets/Materials/Items");

            // Visual simple: anillo + diente (primitivas).
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Shaft";
            shaft.transform.SetParent(key.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 0f, 0.04f);
            shaft.transform.localScale = new Vector3(0.04f, 0.04f, 0.16f);
            Object.DestroyImmediate(shaft.GetComponent<Collider>());

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "Head";
            head.transform.SetParent(key.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            head.transform.localScale = new Vector3(0.1f, 0.01f, 0.1f);
            Object.DestroyImmediate(head.GetComponent<Collider>());

            GameObject tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = "Tooth";
            tooth.transform.SetParent(key.transform, false);
            tooth.transform.localPosition = new Vector3(0.03f, 0f, 0.1f);
            tooth.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            Object.DestroyImmediate(tooth.GetComponent<Collider>());

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = "Key_Gold";
                mat.SetColor("_BaseColor", new Color(0.9f, 0.75f, 0.2f));
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Smoothness", 0.6f);
                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
            }

            foreach (Renderer r in key.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;

            SphereCollider trigger = key.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.12f;

            Rigidbody rb = key.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.mass = 0.3f;

            key.AddComponent<KeyItem>();

            // Enfoque inventario: pickupMask usa Collide, ok.
            PrefabUtility.SaveAsPrefabAsset(key, KeyPrefabPath);
            Object.DestroyImmediate(key);

            AssetDatabase.Refresh();
            Debug.Log($"[DoorLockSetup] KeyPickup creado en {KeyPrefabPath}");
        }

        [MenuItem("Tools/PrisaControlada/Fix Doors Without Panel")]
        public static void FixDoorsWithoutPanel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Fix Doors",
                    "Estas en Play Mode. Los cambios se pierden al salir.\n\nSalí de Play (Play button) y volvé a correr el menú.",
                    "OK");
                return;
            }

            DoorController[] doors = Object.FindObjectsByType<DoorController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int fixedCount = 0;
            int alreadyOk = 0;

            Debug.Log($"[DoorLockSetup] Encontradas {doors.Length} puerta(s) en la escena.");

            foreach (DoorController door in doors)
            {
                SerializedObject so = new SerializedObject(door);
                Transform panel = so.FindProperty("doorPanel").objectReferenceValue as Transform;
                if (panel != null)
                {
                    alreadyOk++;
                    Debug.Log($"[DoorLockSetup] OK (ya tiene panel): '{door.name}'", door);
                    continue;
                }

                Transform root = door.transform;
                Transform hinge = so.FindProperty("hinge").objectReferenceValue as Transform;

                // Panel nuevo bajo el Hinge (misma logica que BuildExampleDoor).
                GameObject panelGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panelGo.name = "DoorPanel";
                panelGo.transform.localScale = new Vector3(1.2f, 2.2f, 0.05f);

                if (hinge == null)
                {
                    Transform found = root.Find("Hinge");
                    if (found == null)
                    {
                        GameObject hingeGo = new GameObject("Hinge");
                        found = hingeGo.transform;
                        found.SetParent(root, false);
                        found.localPosition = new Vector3(0.6f, 1.1f, 0f);
                    }
                    hinge = found;
                    so.FindProperty("hinge").objectReferenceValue = hinge;
                }

                panelGo.transform.SetParent(hinge, false);
                panelGo.transform.localPosition = new Vector3(-0.6f, 0f, 0f);
                panelGo.transform.localRotation = Quaternion.identity;

                so.FindProperty("doorPanel").objectReferenceValue = panelGo.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(door);
                EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
                fixedCount++;
                Debug.Log($"[DoorLockSetup] REPARADA '{root.name}' -> panel creado y asignado.", door);
            }

            if (fixedCount == 0)
            {
                EditorUtility.DisplayDialog(
                    "Fix Doors",
                    alreadyOk > 0
                        ? $"Las {alreadyOk} puerta(s) ya tenían panel.\nSi en Play no abren, revisá que doorPanel apunte al collider correcto en el Inspector."
                        : "No se encontró ninguna puerta en la escena abierta.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Fix Doors",
                    $"{fixedCount} puerta(s) reparada(s), {alreadyOk} ya OK.\n\nGUARDÁ LA ESCENA (Ctrl+S) ahora, antes de volver a Play.",
                    "OK");
            }
        }

        [MenuItem("Tools/PrisaControlada/Convert Selected Door to Locked (Key)")]
        public static void ConvertSelectedToLockedDoor()
        {
            if (!TryGetSelectedDoor(out GameObject root, out DoorController existing))
            {
                EditorUtility.DisplayDialog("Locked Door", "Selecciona una puerta (objeto con DoorController o DoorPanel).", "OK");
                return;
            }

            LockedDoor locked = root.GetComponent<LockedDoor>();
            if (locked == null)
            {
                DoorSnapshot snap = DoorSnapshot.Capture(existing);
                if (existing != null)
                    Object.DestroyImmediate(existing);
                locked = root.AddComponent<LockedDoor>();
                snap.Apply(locked);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorUtility.SetDirty(root);
            Debug.Log("[DoorLockSetup] Puerta convertida a LockedDoor (requiredKeyID=1).", root);
        }

        [MenuItem("Tools/PrisaControlada/Convert Selected Door to Code")]
        public static void ConvertSelectedToCodeDoor()
        {
            if (!TryGetSelectedDoor(out GameObject root, out DoorController existing))
            {
                EditorUtility.DisplayDialog("Code Door", "Selecciona una puerta (objeto con DoorController o DoorPanel).", "OK");
                return;
            }

            CodeDoor code = root.GetComponent<CodeDoor>();
            if (code == null)
            {
                DoorSnapshot snap = DoorSnapshot.Capture(existing);
                if (existing != null)
                    Object.DestroyImmediate(existing);
                code = root.AddComponent<CodeDoor>();
                snap.Apply(code);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorUtility.SetDirty(root);
            Debug.Log("[DoorLockSetup] Puerta convertida a CodeDoor. Codigo por defecto: 1234.", root);
        }

        // Copia doorPanel/hinge/openSpeed/etc del DoorController viejo al nuevo.
        private struct DoorSnapshot
        {
            private Transform _doorPanel;
            private Transform _hinge;
            private Transform _player;
            private float _openAngle;
            private float _openSpeed;
            private float _hingeSide;
            private bool _valid;

            public static DoorSnapshot Capture(DoorController source)
            {
                DoorSnapshot snap = default;
                if (source == null) return snap;

                SerializedObject so = new SerializedObject(source);
                snap._doorPanel = so.FindProperty("doorPanel").objectReferenceValue as Transform;
                snap._hinge = so.FindProperty("hinge").objectReferenceValue as Transform;
                snap._player = so.FindProperty("player").objectReferenceValue as Transform;
                snap._openAngle = so.FindProperty("openAngle").floatValue;
                snap._openSpeed = so.FindProperty("openSpeed").floatValue;
                snap._hingeSide = so.FindProperty("hingeSide").floatValue;
                snap._valid = true;
                return snap;
            }

            public void Apply(DoorController target)
            {
                if (!_valid || target == null) return;

                SerializedObject so = new SerializedObject(target);
                so.FindProperty("doorPanel").objectReferenceValue = _doorPanel;
                so.FindProperty("hinge").objectReferenceValue = _hinge;
                so.FindProperty("player").objectReferenceValue = _player;
                so.FindProperty("openAngle").floatValue = _openAngle;
                so.FindProperty("openSpeed").floatValue = _openSpeed;
                so.FindProperty("hingeSide").floatValue = _hingeSide;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool TryGetSelectedDoor(out GameObject root, out DoorController existing)
        {
            root = null;
            existing = null;

            if (Selection.activeGameObject == null) return false;

            root = Selection.activeGameObject;
            existing = root.GetComponentInParent<DoorController>();
            if (existing != null)
            {
                root = existing.gameObject;
                return true;
            }

            // Tambien aceptar seleccion del DoorPanel y subir al padre con Hinge.
            if (root.name.Contains("Door") || root.name.Contains("Panel"))
            {
                if (root.transform.parent != null)
                {
                    root = root.transform.parent.gameObject;
                    existing = root.GetComponent<DoorController>();
                    return true;
                }
            }

            return false;
        }
    }
}
