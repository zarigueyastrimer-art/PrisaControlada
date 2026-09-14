using UnityEngine;
using UnityEditor;
using System.IO;

public class MapaAssetProcessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path.Contains("mapa") && path.EndsWith(".obj"))
            {
                Debug.Log("[Mapa] OBJ importado. Revisa el modelo en: " + path);
                CrearPrefab();
            }
        }
    }

    [MenuItem("Prisa/Crear Prefabo del Mapa")]
    static void CrearPrefab()
    {
        string objPath = "Assets/mapa/Centro_Comercial_LiminalOptimizao.obj";
        string prefabPath = "Assets/mapa/Centro_Comercial_LiminalOptimizao.prefab";

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
        if (model == null)
        {
            Debug.LogError("No se encontro el modelo en " + objPath);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log("Prefabo creado en: " + prefabPath);
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
}
