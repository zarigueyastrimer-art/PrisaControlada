using System.IO;
using UnityEditor;
using UnityEngine;

namespace PrisaControlada.Editor
{
    // Crea un material URP/Lit nuevo con las texturas del runer y lo aplica
    // al prefab RunnerEnemy (al child "Body").
    //
    // Uso: Tools > PrisaControlada > Create Runner Material & Apply to Prefab
    public static class CreateRunnerMaterial
    {
        private const string NewMatPath = "Assets/Materials/Enemies/Runner_Lit.mat";
        private const string TexFolder = "Assets/MyAssets/runer/Texturas";
        private const string PrefabPath = "Assets/Prefabs/Enemies/RunnerEnemy.prefab";

        private static Texture2D Load(string fileName)
        {
            var path = Path.Combine(TexFolder, fileName);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                Debug.LogWarning($"[CreateRunnerMaterial] No se encontr� la textura: {path}");
            return tex;
        }

        [MenuItem("Tools/PrisaControlada/Create Runner Material & Apply to Prefab", false, 202)]
        public static void CreateAndApply()
        {
            // 1. Crear material nuevo desde cero
            if (AssetDatabase.LoadAssetAtPath<Material>(NewMatPath) != null)
                AssetDatabase.DeleteAsset(NewMatPath);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null)
            {
                Debug.LogError("[CreateRunnerMaterial] No se encontr� el shader Universal Render Pipeline/Lit");
                return;
            }

            mat.name = "Runner_Lit";

            // 2. Cargar texturas
            var baseMap = Load("Runner40k_tripo_mat_84201abf-4b52-47dc-a5e6-b1c29f3929b0_BaseMap.png");
            var normalMap = Load("Runner40k_tripo_mat_84201abf-4b52-47dc-a5e6-b1c29f3929b0_Normal.png");
            var emissiveMap = Load("Runner40k_tripo_mat_84201abf-4b52-47dc-a5e6-b1c29f3929b0_Emissive.png");
            var maskMap = Load("Runner40k_tripo_mat_84201abf-4b52-47dc-a5e6-b1c29f3929b0_MaskMap.png");

            // 3. Configurar URP/Lit
            mat.SetFloat("_Surface", 0);          // Opaque
            mat.SetFloat("_WorkflowMode", 1);     // Metallic
            mat.SetFloat("_BlendModePreserveSpecular", 1);
            mat.SetFloat("_Cull", 2);
            mat.SetFloat("_ZWrite", 1);
            mat.SetFloat("_ReceiveShadows", 1);
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_BumpScale", 1f);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);

            // 4. Asignar texturas
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetTexture("_MainTex", baseMap);
            mat.SetTexture("_BumpMap", normalMap);
            mat.SetTexture("_EmissionMap", emissiveMap);
            mat.SetTexture("_OcclusionMap", maskMap);
            mat.SetTexture("_MetallicGlossMap", maskMap);

            // 5. Guardar material
            AssetDatabase.CreateAsset(mat, NewMatPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CreateRunnerMaterial] Material creado: {NewMatPath}");

            // 6. Aplicar al prefab RunnerEnemy
            ApplyToPrefab(mat);
        }

        private static void ApplyToPrefab(Material mat)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[CreateRunnerMaterial] No se encontr� el prefab: {PrefabPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // Buscar el child "Body" (el modelo del runer que pusimos antes)
            var body = instance.transform.Find("Body");
            if (body == null)
            {
                Debug.LogWarning("[CreateRunnerMaterial] No se encontr� el child 'Body' en el prefab.");
                Object.DestroyImmediate(instance);
                return;
            }

            int applied = 0;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                applied++;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CreateRunnerMaterial] Material aplicado a {applied} renderer(es) en {PrefabPath}");
        }
    }
}
