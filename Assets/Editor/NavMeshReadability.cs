using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PrisaControlada.EditorTools
{
    // El bake de NavMesh en runtime recorre TODOS los RenderMeshes de la escena.
    // Cada modelo importado sin "Read/Write Enabled" genera un warning
    //   "RuntimeNavMeshBuilder: Source mesh X does not allow read access"
    // y ademas, en un build, ese mesh no entra al NavMesh (queda incompleto).
    //
    // Esta herramienta busca los modelos de la escena abierta que estan sin
    // Read/Write y los activa de una sola vez. No toca materiales, ni escalas,
    // ni posiciones: solo la casilla del importador.
    public static class NavMeshReadability
    {
        private const string MenuReport = "PrisaControlada/NavMesh/1. Revisar modelos sin Read-Write";
        private const string MenuFix = "PrisaControlada/NavMesh/2. Hacer legibles los modelos del NavMesh";

        [MenuItem(MenuReport, false, 100)]
        private static void Report()
        {
            List<string> models = CollectModelsWithoutReadAccess(out int meshCount);

            if (models.Count == 0)
            {
                Debug.Log("[NavMesh] Todos los meshes de la escena ya tienen Read/Write. No hay warnings que evitar.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[NavMesh] {models.Count} modelo(s) sin Read/Write afectan a {meshCount} mesh(es) de la escena:");
            foreach (string path in models)
                sb.AppendLine("  - " + path);
            Debug.Log(sb.ToString());
        }

        [MenuItem(MenuFix, false, 101)]
        private static void Fix()
        {
            List<string> models = CollectModelsWithoutReadAccess(out int meshCount);

            if (models.Count == 0)
            {
                Debug.Log("[NavMesh] Nada que hacer: todos los meshes de la escena ya son legibles.");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "NavMesh: Read/Write",
                $"Se van a marcar {models.Count} modelo(s) como Read/Write.\n" +
                $"Afecta a {meshCount} mesh(es) de la escena.\n\n" +
                "Unity va a reimportarlos (puede tardar unos minutos).\n" +
                "Se puede desactivar despues desde cada modelo en el Inspector.",
                "Hacerlo", "Cancelar");

            if (!ok) return;

            int changed = 0;
            foreach (string path in models)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || importer.isReadable) continue;

                importer.isReadable = true;
                importer.SaveAndReimport();
                changed++;
                EditorUtility.DisplayProgressBar("NavMesh: Read/Write", path, changed / (float)models.Count);
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"[NavMesh] Listo: {changed} modelo(s) marcados como Read/Write. Entra en Play y la consola deberia quedar limpia.");
        }

        // Devuelve la lista de assets de modelo (rutas del proyecto) que tienen
        // meshes usados en la escena abierta y que no permiten lectura en CPU.
        private static List<string> CollectModelsWithoutReadAccess(out int meshCount)
        {
            HashSet<string> models = new HashSet<string>();
            HashSet<int> seenMeshes = new HashSet<int>();
            meshCount = 0;

            MeshFilter[] filters = Object.FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.isReadable) continue;
                if (!seenMeshes.Add(mesh.GetInstanceID())) continue;

                string path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path)) continue; // mesh interno de Unity

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                meshCount++;
                models.Add(path);
            }

            List<string> result = new List<string>(models);
            result.Sort();
            return result;
        }
    }
}
