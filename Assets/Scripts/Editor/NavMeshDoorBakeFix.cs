using System.Collections.Generic;
using PrisaControlada.Items;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Editor
{
    // Excluye los paneles de puerta del bake del NavMesh y vuelve a hornear.
    // Los paneles horneados (mascara con capa 3 DOOR + paneles en capa 0) recortan
    // huecos non-walkable en las bocas y los enemigos no pueden pasar.
    public static class NavMeshDoorBakeFix
    {
        private const int DoorLayer = 3; // TagManager: capa 3 = DOOR
        private const int BakeWatchTicks = 10000;
        private const float AgentRadiusDeseado = 0.3f;

        private static NavMeshSurface[] s_WatchedSurfaces;
        private static int s_WatchTicks;

        // CONFIGURACION COMPLETA: agentRadius 0.3 (pasillos/puertas de ~1.15 m),
        // puertas excluidas del bake y obstacle carve para que una puerta cerrada
        // corte el NavMesh y una abierta deje el pasillo azul.
        [MenuItem("Tools/PrisaControlada/NavMesh: Setup Tolerante + Puertas + Rebake", false, 0)]
        public static void SetupTolerantAndRebake()
        {
            if (!CanRun())
                return;

            List<GameObject> raices = CollectDoorRoots();
            int marcadas = 0;
            foreach (GameObject root in raices)
            {
                NavMeshModifier mod = root.GetComponent<NavMeshModifier>();
                if (mod == null)
                    mod = Undo.AddComponent<NavMeshModifier>(root);
                mod.ignoreFromBuild = true;
                mod.applyToChildren = true;
                EditorUtility.SetDirty(root);
                if (root.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(root.scene);
                marcadas++;
            }

            List<GameObject> paneles = CollectDoorPanels(true);
            int obstaculos = 0;
            foreach (GameObject panel in paneles)
            {
                if (EnsureNavMeshObstacle(panel))
                    obstaculos++;
            }

            int maskAntes = -1;
            int maskDespues = -1;
            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (NavMeshSurface s in surfaces)
            {
                int antes = s.layerMask;
                int despues = antes & ~(1 << DoorLayer);
                maskAntes = antes;
                maskDespues = despues;
                if (despues != antes)
                {
                    s.layerMask = despues;
                    EditorUtility.SetDirty(s);
                    EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
                }
            }

            if (surfaces.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "NavMesh Setup",
                    "No hay NavMeshSurface en la escena.\n\nAbrí la escena Prototype y volvé a correr el menú.",
                    "OK");
                return;
            }

            Debug.Log(
                $"[NavMeshSetup] Puertas excluidas del bake={marcadas}, obstacle carve={obstaculos}, " +
                $"mascara {(maskAntes < 0 ? "sin cambio" : maskAntes + "->" + maskDespues)}, " +
                $"agentRadius={AgentRadiusDeseado} (Project Settings > Navigation).");

            Rebake(surfaces);

            EditorUtility.DisplayDialog(
                "NavMesh Setup",
                $"{marcadas} puerta(s) excluida(s) del bake y {obstaculos} obstacle(s) carve agregados.\n\n" +
                "Bake en curso (barra de progreso de Unity).\n" +
                "CUANDO TERMINE corre solo el Diagnostics.\n\n" +
                "Después GUARDÁ LA ESCENA (Ctrl+S).",
                "OK");
        }

        [MenuItem("Tools/PrisaControlada/NavMesh: Exclude Doors From Bake + Rebake")]
        public static void ExcludeDoorsAndRebake()
        {
            if (!CanRun())
                return;

            List<GameObject> panels = CollectDoorPanels();
            int marcadas = 0;
            foreach (GameObject go in panels)
            {
                NavMeshModifier mod = go.GetComponent<NavMeshModifier>();
                if (mod == null)
                    mod = Undo.AddComponent<NavMeshModifier>(go);
                mod.ignoreFromBuild = true;
                mod.applyToChildren = true;
                EditorUtility.SetDirty(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                marcadas++;
            }

            int maskAntes = -1;
            int maskDespues = -1;
            int superficies = 0;
            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (NavMeshSurface s in surfaces)
            {
                int antes = s.layerMask;
                int despues = antes & ~(1 << DoorLayer);
                maskAntes = antes;
                maskDespues = despues;
                if (despues != antes)
                {
                    s.layerMask = despues;
                    EditorUtility.SetDirty(s);
                    EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
                }
                superficies++;
            }

            if (superficies == 0)
            {
                EditorUtility.DisplayDialog(
                    "NavMesh Fix",
                    "No hay NavMeshSurface en la escena.\n\nAbrí la escena correcta (Prototype) y volvé a correr el menú.",
                    "OK");
                return;
            }

            Debug.Log(
                $"[NavMeshDoorFix] Paneles marcados con NavMeshModifier.ignoreFromBuild={marcadas}, " +
                $"mascara {(maskAntes < 0 ? "sin cambio" : maskAntes + "->" + maskDespues)}, superficies={superficies}.");

            Rebake(surfaces);

            EditorUtility.DisplayDialog(
                "NavMesh Fix",
                $"{marcadas} panel(es) excluido(s) del bake y mascara corregida.\n\n" +
                "Bake en curso (barra de progreso de Unity).\n" +
                "CUANDO TERMINE te aviso por consola y se corre solo el Diagnostics.\n\n" +
                "Después GUARDÁ LA ESCENA (Ctrl+S).",
                "OK");
        }

        [MenuItem("Tools/PrisaControlada/NavMesh: Rebake Only")]
        public static void RebakeOnly()
        {
            if (!CanRun())
                return;

            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (surfaces.Length == 0)
            {
                EditorUtility.DisplayDialog("NavMesh Rebake", "No hay NavMeshSurface en la escena.", "OK");
                return;
            }
            Rebake(surfaces);
            Debug.Log("[NavMeshDoorFix] Rebake iniciado. Al terminar se corre el Diagnostics; después GUARDÁ la escena (Ctrl+S).");
        }

        private static bool CanRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "NavMesh Fix",
                    "Estas en Play Mode. Los cambios se pierden al salir.\n\nSalí de Play (Play button) y volvé a correr el menú.",
                    "OK");
                return false;
            }
            return true;
        }

        private static void Rebake(NavMeshSurface[] surfaces)
        {
            NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);
            s_WatchedSurfaces = surfaces;
            s_WatchTicks = 0;
            EditorApplication.update -= WatchBake;
            EditorApplication.update += WatchBake;
        }

        // Espera a que el bake asincronico del paquete termine y ahi verifica solo.
        private static void WatchBake()
        {
            s_WatchTicks++;

            bool stillBaking = false;
            if (s_WatchedSurfaces != null)
            {
                foreach (NavMeshSurface s in s_WatchedSurfaces)
                {
                    if (s != null && NavMeshAssetManager.instance.IsSurfaceBaking(s))
                    {
                        stillBaking = true;
                        break;
                    }
                }
            }

            if (stillBaking && s_WatchTicks < BakeWatchTicks)
                return;

            EditorApplication.update -= WatchBake;
            NavMeshSurface[] watched = s_WatchedSurfaces;
            s_WatchedSurfaces = null;

            if (stillBaking)
            {
                Debug.LogWarning("[NavMeshDoorFix] El bake sigue en curso despues del timeout; revisa la barra de progreso de Unity.");
                return;
            }

            Debug.Log("[NavMeshDoorFix] Bake terminado. Corriendo Diagnostics para verificar las bocas de puerta...");
            if (watched != null && watched.Length > 0)
                EditorSceneManager.MarkSceneDirty(watched[0].gameObject.scene);
            NavMeshDiagnostics.ReportDoorNavMeshGaps();
            Debug.Log("[NavMeshDoorFix] GUARDÁ LA ESCENA (Ctrl+S) para conservar el nuevo NavMesh.");
        }

        private static List<GameObject> CollectDoorPanels(bool soloPanelesReales = false)
        {
            Dictionary<int, GameObject> result = new Dictionary<int, GameObject>();

            foreach (DoorController door in Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject so = new SerializedObject(door);
                SerializedProperty prop = so.FindProperty("doorPanel");
                Transform panel = prop != null ? prop.objectReferenceValue as Transform : null;
                if (panel == null)
                {
                    foreach (Renderer r in door.GetComponentsInChildren<Renderer>())
                    {
                        if (r.name.Contains("Panel"))
                        {
                            panel = r.transform;
                            break;
                        }
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.ToLower().Contains("door"))
                        {
                            panel = r.transform;
                            break;
                        }
                    }
                }
                if (panel != null)
                    result[panel.gameObject.GetInstanceID()] = panel.gameObject;
            }

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "DoorPanel")
                    result[t.GetInstanceID()] = t.gameObject;
            }

            // Cualquier renderer con el mesh "door" (DoorBase.fbx), cubre instancias raras.
            // Solo sirve para excluir del bake: para el obstacle carve se usan los paneles reales.
            if (!soloPanelesReales)
            {
                foreach (MeshRenderer mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "door")
                        result[mr.gameObject.GetInstanceID()] = mr.gameObject;
                }
            }

            return new List<GameObject>(result.Values);
        }

        // Raices de todas las puertas de la escena (DoorController y sus subclases)
        // mas cualquier objeto que este en la capa DOOR.
        private static List<GameObject> CollectDoorRoots()
        {
            Dictionary<int, GameObject> result = new Dictionary<int, GameObject>();

            foreach (DoorController door in Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform root = door.transform;
                if (root.parent != null && root.parent.gameObject.layer == DoorLayer)
                    root = root.parent;
                result[root.gameObject.GetInstanceID()] = root.gameObject;
            }

            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.layer != DoorLayer)
                    continue;
                if (go.transform.parent != null && go.transform.parent.gameObject.layer == DoorLayer)
                    continue;
                result[go.GetInstanceID()] = go;
            }

            return new List<GameObject>(result.Values);
        }

        // Agrega (o ajusta) un NavMeshObstacle Box con carve del tamano del panel.
        // Es lo que recorta el pasillo cuando la puerta esta cerrada.
        private static bool EnsureNavMeshObstacle(GameObject panel)
        {
            NavMeshObstacle obs = panel.GetComponent<NavMeshObstacle>();
            if (obs == null)
                obs = Undo.AddComponent<NavMeshObstacle>(panel);

            BoxCollider box = panel.GetComponent<BoxCollider>();
            if (box != null)
            {
                obs.center = box.center;
                obs.size = box.size;
            }
            else
            {
                Renderer r = panel.GetComponent<Renderer>();
                if (r == null)
                {
                    Debug.LogWarning("[NavMeshSetup] '" + panel.name + "' sin BoxCollider ni Renderer; obstacle con tamano por defecto.", panel);
                    obs.center = Vector3.zero;
                    obs.size = new Vector3(1f, 2f, 0.1f);
                }
                else
                {
                    obs.center = r.localBounds.center;
                    obs.size = r.localBounds.size;
                }
            }

            obs.shape = NavMeshObstacleShape.Box;
            obs.carving = true;
            obs.carveOnlyStationary = true;
            obs.enabled = true;

            EditorUtility.SetDirty(panel);
            if (panel.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(panel.scene);
            return true;
        }
    }
}
