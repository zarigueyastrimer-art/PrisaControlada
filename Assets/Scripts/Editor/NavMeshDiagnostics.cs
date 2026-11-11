using System.Collections.Generic;
using System.IO;
using System.Text;
using PrisaControlada.Items;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Editor
{
    // Diagnostico de solo lectura del NavMesh horneado.
    // Mide si cada boca de puerta queda transitable y lista fuentes de bake problematicas.
    // Correr en Edit Mode (bake guardado) y en Play Mode (bake de RuntimeNavMesh) para comparar.
    public static class NavMeshDiagnostics
    {
        private const int DoorLayer = 3; // TagManager: capa 3 = DOOR
        private const float SampleRadius = 0.4f;

        [MenuItem("Tools/PrisaControlada/Diagnostics/Report Door NavMesh Gaps")]
        public static void ReportDoorNavMeshGaps()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"==== NavMeshDiag {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} playMode={EditorApplication.isPlaying} ====");

            int mask = CollectSurfaceInfo(sb, out NavMeshSurface mainSurface);
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            bool navOk = tri.vertices != null && tri.vertices.Length > 0 && tri.indices != null && tri.indices.Length >= 3;
            sb.AppendLine(navOk
                ? $"[NAVMESH] {tri.vertices.Length} vertices / {tri.indices.Length / 3} triangulos cargados."
                : "[NAVMESH] NO hay NavMesh cargado (triangulacion vacia).");

            ReportDoors(sb, mask, navOk, tri);
            ReportBakeSources(sb, mainSurface, mask);
            ReportSceneHealth(sb);

            string text = sb.ToString();
            string path = WriteReport(text);
            Debug.Log(text);
            if (!string.IsNullOrEmpty(path))
                Debug.Log($"[NavMeshDiag] Reporte en disco: {path}");
        }

        private static int CollectSurfaceInfo(StringBuilder sb, out NavMeshSurface mainSurface)
        {
            mainSurface = null;
            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (surfaces.Length == 0)
            {
                sb.AppendLine("[SUPERFICIE] NO hay NavMeshSurface en la escena.");
                return ~0;
            }

            mainSurface = surfaces[0];
            int unionMask = 0;
            foreach (NavMeshSurface s in surfaces)
            {
                int m = s.layerMask;
                unionMask |= m;
                sb.AppendLine(
                    $"[SUPERFICIE] '{s.name}' mascara={m} ({LayerList(m)}) geometria={s.useGeometry} coleccion={s.collectObjects} " +
                    $"voxel={s.voxelSize} minRegionArea={s.minRegionArea} datos={(s.navMeshData == null ? "NINGUNO" : "ok")} " +
                    $"ignoreAgent={s.ignoreNavMeshAgent} ignoreObstacle={s.ignoreNavMeshObstacle}");
                if ((m & (1 << DoorLayer)) != 0)
                    sb.AppendLine($"[SUPERFICIE] AVISO: la mascara incluye la capa {DoorLayer} (DOOR) -> los paneles de puerta en esa capa entran al bake y recortan el NavMesh.");
            }
            return unionMask;
        }

        private static void ReportDoors(StringBuilder sb, int mask, bool navOk, NavMeshTriangulation tri)
        {
            DoorController[] doors = Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"[PUERTAS] {doors.Length} DoorController en la escena.");

            int okCount = 0;
            int bloqueadas = 0;
            int sinNav = 0;
            int sinPanel = 0;

            foreach (DoorController door in doors)
            {
                Transform panel = ResolvePanel(door);
                if (panel == null)
                {
                    sinPanel++;
                    sb.AppendLine($"[PUERTA] '{FullPath(door.transform)}' SIN PANEL asignado -> no hay geometria de puerta en el bake.");
                    continue;
                }

                Renderer rend = panel.GetComponentInChildren<Renderer>();
                Bounds bounds;
                if (rend != null)
                {
                    bounds = rend.bounds;
                }
                else
                {
                    Vector3 p = panel.position;
                    bounds = new Bounds(p + Vector3.up * 1.1f, new Vector3(1.2f, 2.2f, 0.05f));
                }

                Vector3 mouth = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
                Vector3 crossDir = GetCrossAxis(panel, bounds.size, out float halfWidth);
                float offset = Mathf.Max(0.75f, halfWidth + 0.55f);

                bool boca = SamplePos(mouth, out float dBoca);
                bool izq = SamplePos(mouth - crossDir * offset, out float dIzq);
                bool der = SamplePos(mouth + crossDir * offset, out float dDer);
                int trisEnPanel = CountTrisInside(tri, bounds, navOk);

                int layer = panel.gameObject.layer;
                bool enMascara = (mask & (1 << layer)) != 0;

                string verdict;
                if (!navOk)
                {
                    verdict = "SIN DATOS";
                }
                else if (boca)
                {
                    verdict = "OK (boca transitable)";
                    okCount++;
                }
                else if (izq || der)
                {
                    verdict = "BLOQUEADA (hueco non-walkable en la boca)";
                    bloqueadas++;
                }
                else
                {
                    verdict = "SIN NAVMESH CERCA (zona no horneada)";
                    sinNav++;
                }

                sb.AppendLine(
                    $"[PUERTA] '{FullPath(door.transform)}' panel='{panel.name}' capa={layer}({LayerMask.LayerToName(layer)}) " +
                    $"enMascara={enMascara} boca={(boca ? $"SI({dBoca:F2})" : "NO")} " +
                    $"izq={(izq ? $"SI({dIzq:F2})" : "NO")} der={(der ? $"SI({dDer:F2})" : "NO")} " +
                    $"trisEnPanel={trisEnPanel} -> {verdict}");
            }

            sb.AppendLine($"[PUERTAS] resumen: {okCount} OK, {bloqueadas} BLOQUEADAS, {sinNav} sin navmesh cerca, {sinPanel} sin panel.");
        }

        private static void ReportBakeSources(StringBuilder sb, NavMeshSurface mainSurface, int mask)
        {
            bool porColision = mainSurface != null && mainSurface.useGeometry == NavMeshCollectGeometry.PhysicsColliders;
            int fuentes = 0;
            Dictionary<string, string> noLegibles = new Dictionary<string, string>();

            if (porColision)
            {
                foreach (MeshCollider col in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if ((mask & (1 << col.gameObject.layer)) == 0) continue;
                    Mesh mesh = col.sharedMesh;
                    if (mesh == null) continue;
                    fuentes++;
                    if (!mesh.isReadable && !noLegibles.ContainsKey(mesh.name))
                        noLegibles.Add(mesh.name, FullPath(col.transform));
                }
            }
            else
            {
                foreach (MeshRenderer mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if ((mask & (1 << mr.gameObject.layer)) == 0) continue;
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    Mesh mesh = mf.sharedMesh;
                    fuentes++;
                    if (!mesh.isReadable && !noLegibles.ContainsKey(mesh.name))
                        noLegibles.Add(mesh.name, FullPath(mr.transform));
                }
            }

            sb.AppendLine($"[BAKE] {fuentes} fuentes ({(porColision ? "MeshColliders" : "MeshRenderers")}) en capas de la mascara; {noLegibles.Count} meshes NO legibles (isReadable=0).");
            if (noLegibles.Count > 0)
            {
                sb.AppendLine("[BAKE] AVISO: los meshes no legibles NO se hornean en Play Mode ni en el player build (RuntimeNavMesh los omite) -> el NavMesh del juego difiere del guardado.");
                foreach (KeyValuePair<string, string> kv in noLegibles)
                    sb.AppendLine($"[BAKE]   noLegible '{kv.Key}' ej: {kv.Value}");
            }
        }

        private static void ReportSceneHealth(StringBuilder sb)
        {
            int negativos = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Vector3 s = t.localScale;
                if (s.x < 0f || s.y < 0f || s.z < 0f)
                {
                    negativos++;
                    sb.AppendLine($"[SALUD] escala negativa en '{FullPath(t)}' localScale=({s.x:F3}, {s.y:F3}, {s.z:F3}) -> colliders espejados y bounds incorrectos.");
                }
            }

            int obstacles = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            sb.AppendLine($"[SALUD] escalas negativas={negativos} NavMeshObstacle={obstacles} NavMeshAgent={agents}");
        }

        private static Transform ResolvePanel(DoorController door)
        {
            SerializedObject so = new SerializedObject(door);
            SerializedProperty prop = so.FindProperty("doorPanel");
            if (prop != null && prop.objectReferenceValue is Transform assigned)
                return assigned;

            foreach (Renderer r in door.GetComponentsInChildren<Renderer>())
            {
                if (r.name.Contains("Panel"))
                    return r.transform;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.ToLower().Contains("door"))
                    return r.transform;
            }
            return null;
        }

        // Eje perpendicular al plano de la puerta (el mas delgado) + semiancho en ese eje.
        private static Vector3 GetCrossAxis(Transform panel, Vector3 fallbackSize, out float halfWidth)
        {
            Vector3 localSize = fallbackSize;
            MeshFilter mf = panel.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.bounds.size.sqrMagnitude > 0f)
                localSize = mf.sharedMesh.bounds.size;

            Vector3 ls = panel.lossyScale;
            Vector3 worldSize = new Vector3(
                Mathf.Abs(ls.x) * localSize.x,
                Mathf.Abs(ls.y) * localSize.y,
                Mathf.Abs(ls.z) * localSize.z);

            int axis = 0;
            if (worldSize.y < worldSize.x && worldSize.y < worldSize.z) axis = 1;
            else if (worldSize.z < worldSize.x && worldSize.z < worldSize.y) axis = 2;

            halfWidth = worldSize[axis] * 0.5f;

            Vector3 dir = axis == 0 ? panel.right : axis == 1 ? panel.up : panel.forward;
            dir = new Vector3(dir.x, 0f, dir.z);
            return dir.sqrMagnitude < 1e-6f ? Vector3.right : dir.normalized;
        }

        private static bool SamplePos(Vector3 pos, out float distance)
        {
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, SampleRadius, NavMesh.AllAreas))
            {
                distance = hit.distance;
                return true;
            }
            distance = -1f;
            return false;
        }

        private static int CountTrisInside(NavMeshTriangulation tri, Bounds bounds, bool navOk)
        {
            if (!navOk) return 0;
            Bounds probe = bounds;
            probe.Expand(0.15f);
            int count = 0;
            for (int i = 0; i + 2 < tri.indices.Length; i += 3)
            {
                Vector3 a = tri.vertices[tri.indices[i]];
                Vector3 b = tri.vertices[tri.indices[i + 1]];
                Vector3 c = tri.vertices[tri.indices[i + 2]];
                if (probe.Contains((a + b + c) / 3f))
                    count++;
            }
            return count;
        }

        private static string LayerList(int mask)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                string n = LayerMask.LayerToName(i);
                names.Add(string.IsNullOrEmpty(n) ? i.ToString() : $"{i}:{n}");
            }
            return string.Join(",", names);
        }

        private static string FullPath(Transform t)
        {
            string result = t.name;
            Transform p = t.parent;
            while (p != null)
            {
                result = p.name + "/" + result;
                p = p.parent;
            }
            return result;
        }

        private static string WriteReport(string text)
        {
            try
            {
                string dir = Path.Combine(Application.dataPath, "..", "Temp");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "navmesh_diag.txt");
                File.WriteAllText(path, text, Encoding.UTF8);
                return Path.GetFullPath(path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NavMeshDiag] No pude escribir el reporte: {e.Message}");
                return string.Empty;
            }
        }
    }
}
