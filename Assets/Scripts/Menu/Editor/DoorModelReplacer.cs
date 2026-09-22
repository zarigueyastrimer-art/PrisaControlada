using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrisaControlada.Items
{
    public static class DoorModelReplacer
    {
        [MenuItem("PrisaControlada/Reemplazar Puertas por Modelo")]
        public static void ReplaceDoors()
        {
            string fbxPath = "Assets/mapa/puertas/DoorBase.fbx";
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            System.Collections.Generic.List<Mesh> meshes = new System.Collections.Generic.List<Mesh>();
            foreach (Object o in allAssets)
                if (o is Mesh m) meshes.Add(m);
            Mesh[] allMeshes = meshes.ToArray();
            if (allMeshes == null || allMeshes.Length == 0)
            {
                Debug.LogError("[DoorReplacer] No meshes found in " + fbxPath);
                return;
            }

            Debug.Log($"[DoorReplacer] FBX meshes found: {allMeshes.Length}");
            foreach (var m in allMeshes)
                Debug.Log($"  - {m.name} (verts: {m.vertexCount})");

            Mesh doorMesh = allMeshes.Length > 0 ? allMeshes[0] : null;
            if (doorMesh == null)
            {
                Debug.LogError("[DoorReplacer] No valid mesh in FBX");
                return;
            }

            string texDir = "Assets/mapa/puertas/texturas/";
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Old_white_wooden_doort_BaseColor.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Old_white_wooden_door_Normal.png");
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Old_white_wooden_door_Metallic.png");
            Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Old_white_wooden_door_Roughness.png");

            string matPath = "Assets/mapa/puertas/Door_Wood_Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = "Door_Wood_Mat";
                if (baseColor != null) mat.SetTexture("_BaseMap", baseColor);
                if (normal != null) mat.SetTexture("_BumpMap", normal);
                if (metallic != null) mat.SetTexture("_MetallicGlossMap", metallic);
                if (roughness != null) mat.SetTexture("_SmoothnessTextureChannel", roughness);
                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
            }

            int replaced = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "DoorPanel") continue;

                    MeshFilter mf = t.GetComponent<MeshFilter>();
                    if (mf == null) continue;
                    if (mf.sharedMesh == null) continue;
                    if (mf.sharedMesh.name != "Cube") continue;

                    mf.sharedMesh = doorMesh;

                    MeshRenderer mr = t.GetComponent<MeshRenderer>();
                    if (mr != null)
                        mr.sharedMaterial = mat;

                    replaced++;
                    Debug.Log($"[DoorReplacer] Replaced: {GetPath(t)}");
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[DoorReplacer] Done. {replaced} door panels replaced.");
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
