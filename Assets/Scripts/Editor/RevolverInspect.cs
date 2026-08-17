using System.Text;
using UnityEditor;
using UnityEngine;

namespace PrisaControlada.EditorTools
{
    public static class RevolverInspect
    {
        private const string RevolverPath = "Assets/MyAssets/guns/revolver/revolver.fbx";

        [MenuItem("Tools/PrisaControlada/Weapons/Inspect Revolver FBX")]
        public static void InspectRevolver()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(RevolverPath);
            if (importer == null)
            {
                Debug.LogError($"[RevolverInspect] No se encontro el FBX en {RevolverPath}");
                return;
            }

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.SaveAndReimport();

            var all = AssetDatabase.LoadAllAssetsAtPath(RevolverPath);
            var sb = new StringBuilder();
            sb.AppendLine($"=== Assets en {RevolverPath} ({all.Length}) ===");

            foreach (var a in all)
            {
                if (a is AnimationClip clip)
                {
                    sb.AppendLine($"CLIP: {clip.name} | len={clip.length:0.00}s | loop={clip.isLooping}");
                }
                else if (a is GameObject go)
                {
                    sb.AppendLine($"GO: {go.name}");
                    foreach (Transform child in go.transform)
                        AppendHierarchy(child, 1, sb);
                }
                else if (a != null)
                {
                    sb.AppendLine($"OTHER: {a.name} ({a.GetType().Name})");
                }
            }

            string result = sb.ToString();
            Debug.Log("[RevolverInspect]\n" + result);
            System.IO.File.WriteAllText("Assets/MyAssets/guns/revolver/REVOLVER_INSPECT.txt", result);
            AssetDatabase.Refresh();
        }

        private static void AppendHierarchy(Transform t, int depth, StringBuilder sb)
        {
            sb.AppendLine($"{new string(' ', depth * 2)}- {t.name}");
            foreach (Transform child in t)
                AppendHierarchy(child, depth + 1, sb);
        }
    }
}