using UnityEditor;
using UnityEngine;

namespace PrisaControlada.Editor
{
    // Asigna las texturas del runer real al material RunnerBody.mat.
    //
    // Uso: Tools > PrisaControlada > Assign Runner Textures
    public static class AssignRunnerTextures
    {
        private const string MatPath = "Assets/Materials/Enemies/RunnerBody.mat";
        private const string TexFolder = "Assets/MyAssets/runer/Texturas";

        [MenuItem("Tools/PrisaControlada/Assign Runner Textures", false, 201)]
        public static void Assign()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                Debug.LogError($"[AssignRunnerTextures] No se encontr� el material: {MatPath}");
                return;
            }

            Texture2D Load(string fileName)
            {
                var path = System.IO.Path.Combine(TexFolder, fileName);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                    Debug.LogWarning($"[AssignRunnerTextures] No se encontr� la textura: {path}");
                return tex;
            }

            var baseMap = Load("Runner40k_tripo_mat_84201abf-4b52-47dc-a5e6-b1c29f3929b0_BaseMap.png");
            var normalMap = Load("Runner40k_tripo_mat_84201abf-4b52-47dc-a5e6-b1c29f3929b0_Normal.png");
            var emissiveMap = Load("Runner40k_tripo_mat_84201abf-4b52-47dc-a5e6-b1c29f3929b0_Emissive.png");
            var maskMap = Load("Runner40k_tripo_mat_84201abf-4b52-47dc-a5e6-b1c29f3929b0_MaskMap.png");

            // URP Lit: _BaseMap = albedo, _BumpMap = normal, _EmissionMap = emissiva, _OcclusionMap = AO/mask.
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetTexture("_BumpMap", normalMap);
            mat.SetTexture("_EmissionMap", emissiveMap);
            mat.SetTexture("_OcclusionMap", maskMap);

            // También _MainTex por compatibilidad con shaders antiguos.
            mat.SetTexture("_MainTex", baseMap);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AssignRunnerTextures] Texturas asignadas a {MatPath}");
        }
    }
}
