using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace PrisaControlada.EditorTools
{
    // Reemplaza los modelos prototipo (primitivas) de BandagePickup y KeyPickup
    // por los modelos OBJ nuevos de Assets/MyAssets/.
    public static class ItemModelReplacer
    {
        private const string BandagePrefabPath = "Assets/Prefabs/items/BandagePickup.prefab";
        private const string KeyPrefabPath = "Assets/Prefabs/items/KeyPickup.prefab";
        private const string BandageObjPath = "Assets/MyAssets/meds/source/Bandage.obj";
        private const string KeyObjPath = "Assets/MyAssets/key/source/keys.obj";
        private const string BandageDiffusePath = "Assets/MyAssets/meds/textures/Bandage_diffuse.png";
        private const string KeyMatPath = "Assets/Materials/Items/Key_Gold.mat";
        private const string OutMatFolder = "Assets/Materials/Items";

        [MenuItem("PrisaControlada/Reemplazar Modelos de Items")]
        public static void ReplaceAll()
        {
            ReplaceBandage();
            ReplaceKey();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Items",
                "Modelos reemplazados:\n- BandagePickup -> Bandage.obj\n- KeyPickup -> keys.obj", "OK");
        }

        private static void ReplaceBandage()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BandageObjPath);
            if (mesh == null)
            {
                Debug.LogError("[ItemReplacer] No se encontro la malla: " + BandageObjPath);
                return;
            }

            Material mat = GetOrCreateBandageMaterial();

            // Hijos a eliminar (primitivas prototipo)
            string[] oldChildren = { "Roll", "CrossH", "CrossV" };

            ReplaceModel(BandagePrefabPath, mesh, mat, oldChildren, targetSize: 0.12f, "BandageModel");
        }

        private static void ReplaceKey()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(KeyObjPath);
            if (mesh == null)
            {
                Debug.LogError("[ItemReplacer] No se encontro la malla: " + KeyObjPath);
                return;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(KeyMatPath);
            if (mat == null)
            {
                mat = GetOrCreateKeyMaterial();
            }

            string[] oldChildren = { "Shaft", "Head", "Tooth" };

            ReplaceModel(KeyPrefabPath, mesh, mat, oldChildren, targetSize: 0.2f, "KeyModel");
        }

        private static void ReplaceModel(string prefabPath, Mesh mesh, Material mat,
            string[] oldChildNames, float targetSize, string newName)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError("[ItemReplacer] No se pudo abrir: " + prefabPath);
                return;
            }

            // Eliminar hijos viejos por nombre
            var toDelete = new List<Transform>();
            foreach (Transform child in prefabRoot.transform)
            {
                foreach (string name in oldChildNames)
                {
                    if (child.name == name)
                    {
                        toDelete.Add(child);
                        break;
                    }
                }
            }
            foreach (Transform t in toDelete)
                Object.DestroyImmediate(t.gameObject);

            // Crear nuevo hijo con el modelo
            GameObject modelGo = new GameObject(newName);
            modelGo.transform.SetParent(prefabRoot.transform, false);
            modelGo.transform.localPosition = Vector3.zero;
            modelGo.transform.localRotation = Quaternion.identity;

            MeshFilter mf = modelGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = modelGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            // Escalar para que el modelo encaje en el tamano del collider
            FitToBounds(modelGo, targetSize);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Debug.Log($"[ItemReplacer] {prefabPath} actualizado con {newName}.");
        }

        // Centra y escala el modelo para que su bounding box maximo sea targetSize.
        private static void FitToBounds(GameObject go, float targetSize)
        {
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null) return;

            Bounds b = mesh.bounds;
            float maxSize = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxSize < 0.0001f) return;

            float scale = targetSize / maxSize;
            go.transform.localScale = Vector3.one * scale;

            // Centrar: mover para que el centro del mesh quede en el origen
            Vector3 meshCenter = b.center * scale;
            go.transform.localPosition = -meshCenter;
        }

        private static Material GetOrCreateBandageMaterial()
        {
            string path = OutMatFolder + "/Bandage_New.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BandageDiffusePath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.SetTexture("_BaseMap", tex);
            }
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);

            if (!AssetDatabase.IsValidFolder(OutMatFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    AssetDatabase.CreateFolder("Assets", "Materials");
                AssetDatabase.CreateFolder("Assets/Materials", "Items");
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material GetOrCreateKeyMaterial()
        {
            string path = OutMatFolder + "/Key_New.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.85f, 0.7f, 0.2f));
            mat.SetColor("_Color", new Color(0.85f, 0.7f, 0.2f));
            mat.SetFloat("_Metallic", 0.8f);
            mat.SetFloat("_Glossiness", 0.6f);

            if (!AssetDatabase.IsValidFolder(OutMatFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    AssetDatabase.CreateFolder("Assets", "Materials");
                AssetDatabase.CreateFolder("Assets/Materials", "Items");
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
