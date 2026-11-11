using System.Collections.Generic;
using System.Text;
using PrisaControlada.Items;
using PrisaControlada.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Editor
{
    // Herramientas del mapa procedural.
    //
    // 1) "Analizar Escena": recorre Prototype.unity, mide cada sala con
    //    Renderer.bounds (la geometria real esta en FBX anidados, asi que NO se
    //    puede saber desde los archivos) y reparte las puertas PuertaFuncional
    //    entre las salas. Solo imprime el reporte, no toca nada.
    //
    // 2) "Preparar Anclajes": hace lo mismo y ademas escribe en cada prefab de
    //    sala los objetos vacios Anchor_In / Anchor_Out_N en la posicion de sus
    //    puertas, mas el RoomDefinition (tipo + peso). LA PUERTA MANDA: si el
    //    ancla ya existe se reubica donde esta la puerta ahora, y los anclajes
    //    sobrantes (sin puerta) se borran. Por eso es repetible: ajustas las
    //    puertas, vuelves a correr y todo queda alineado. Si una sala no tiene
    //    puertas cerca crea anclajes de referencia para que se arrastren a mano.
    //
    // 3) "Agregar NavMeshObstacle": pone el obstacle carve en el prefab
    //    PuertaFuncional (misma logica que NavMeshDoorBakeFix.EnsureNavMeshObstacle)
    //    para que la puerta cerrada recorte el NavMesh.
    public static class MapAnchorTool
    {
        private const string RoomsFolder = "Assets/mapa/hospital";
        private const string DoorPrefabPath = "Assets/mapa/puertas/PuertaFuncional.prefab";
        private const string AnchorInName = "Anchor_In";
        private const string AnchorOutPrefix = "Anchor_Out_";

        // Puerta mas lejos que esto de la sala = no pertenece a esa sala.
        private const float MaxDoorDistance = 6f;

        private sealed class DoorHit
        {
            public Transform Door;
            public float DistToRoom;      // distancia de la puerta a los bounds de la sala
            public Vector3 LocalPos;      // posicion de la puerta en el espacio local de la sala
            public Vector3 LocalOutward;  // "hacia afuera" de la sala, en local
        }

        private sealed class AnchorSpec
        {
            public string Name;
            public Vector3 LocalPos;
            public Quaternion LocalRot;
        }

        private sealed class RoomInfo
        {
            public string Name;
            public string AssetPath;
            public GameObject Root;
            public Bounds Bounds;
            public RoomType Type;
            public readonly List<DoorHit> Doors = new List<DoorHit>();
            public readonly List<AnchorSpec> Anchors = new List<AnchorSpec>();
            public readonly List<string> Warnings = new List<string>();
        }

        // ------------------------------------------------------------------
        // Menus
        // ------------------------------------------------------------------

        [MenuItem("Tools/PrisaControlada/Mapa/Analizar Escena (solo reporte)", false, 19)]
        public static void AnalizarSoloReporte()
        {
            Correr(false);
        }

        [MenuItem("Tools/PrisaControlada/Mapa/Preparar Anclajes", false, 20)]
        public static void PrepararAnclajes()
        {
            Correr(true);
        }

        [MenuItem("Tools/PrisaControlada/Mapa/Agregar NavMeshObstacle a PuertaFuncional", false, 21)]
        public static void AgregarObstacleAPuerta()
        {
            if (!PuedeCorrer())
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath) == null)
            {
                EditorUtility.DisplayDialog("NavMesh Obstacle", "No existe " + DoorPrefabPath, "OK");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(DoorPrefabPath);
            try
            {
                // El panel real es el que referencia DoorController.doorPanel.
                Transform panel = null;
                DoorController dc = contents.GetComponentInChildren<DoorController>(true);
                if (dc != null)
                {
                    SerializedObject so = new SerializedObject(dc);
                    SerializedProperty prop = so.FindProperty("doorPanel");
                    if (prop != null)
                        panel = prop.objectReferenceValue as Transform;
                }

                if (panel == null)
                {
                    foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "DoorPanel")
                        {
                            panel = t;
                            break;
                        }
                    }
                }

                if (panel == null)
                {
                    EditorUtility.DisplayDialog("NavMesh Obstacle", "No encontre el panel de la puerta.", "OK");
                    return;
                }

                NavMeshObstacle obs = panel.GetComponent<NavMeshObstacle>();
                if (obs == null)
                    obs = panel.gameObject.AddComponent<NavMeshObstacle>();

                BoxCollider box = panel.GetComponent<BoxCollider>();
                if (box != null)
                {
                    obs.center = box.center;
                    obs.size = box.size;
                }
                else
                {
                    Renderer r = panel.GetComponent<Renderer>();
                    if (r != null)
                    {
                        obs.center = r.localBounds.center;
                        obs.size = r.localBounds.size;
                    }
                    else
                    {
                        obs.center = Vector3.zero;
                        obs.size = new Vector3(1f, 2f, 0.1f);
                    }
                }

                obs.shape = NavMeshObstacleShape.Box;
                obs.carving = true;
                obs.carveOnlyStationary = true;
                obs.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(contents, DoorPrefabPath);

                Debug.Log("[MapAnchor] NavMeshObstacle agregado al panel '" + panel.name + "' de " +
                          DoorPrefabPath + ". DoorController lo busca solo en el Awake y lo " +
                          "habilita/deshabilita al abrir y cerrar la puerta.");
                EditorUtility.DisplayDialog("NavMesh Obstacle",
                    "Obstacle carve agregado al panel '" + panel.name + "'.\n\n" +
                    "Las instancias de la escena se actualizan solas; guarda la escena (Ctrl+S).", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ------------------------------------------------------------------
        // Flujo principal
        // ------------------------------------------------------------------

        private static void Correr(bool escribir)
        {
            if (!PuedeCorrer())
                return;

            List<RoomInfo> salas = BuscarSalasDeEscena();
            if (salas.Count == 0)
            {
                EditorUtility.DisplayDialog("Mapa",
                    "No hay instancias de las salas de " + RoomsFolder + " en la escena.\n\n" +
                    "Abrí la escena Prototype y vuelve a correr el menu.", "OK");
                return;
            }

            List<Transform> puertas = BuscarPuertasDeEscena();

            foreach (RoomInfo r in salas)
            {
                r.Bounds = CalcularBounds(r.Root);
                r.Type = InferirTipo(r.Name);
            }

            // Reparte cada puerta entre todas las salas cuyos bounds la alcanzan.
            foreach (Transform p in puertas)
            {
                foreach (RoomInfo r in salas)
                {
                    float dist = DistanciaABounds(p.position, r.Bounds);
                    if (dist <= MaxDoorDistance)
                    {
                        r.Doors.Add(new DoorHit { Door = p, DistToRoom = dist });
                    }
                }
            }

            List<RoomInfo> cadena = ArmarCadena(salas);
            Clasificar(cadena);

            string reporte = ArmarReporte(cadena, puertas, salas);
            Debug.Log(reporte);

            if (!escribir)
            {
                EditorUtility.DisplayDialog("Analisis del mapa",
                    Resumen(cadena, puertas) + "\n\nDetalle en la consola (Window > General > Console).\n\n" +
                    "Este analisis no modifico nada. Corre 'Preparar Anclajes' para escribirlos.", "OK");
                return;
            }

            int conAnclajes = 0;
            foreach (RoomInfo r in cadena)
                if (r.Anchors.Count > 0)
                    conAnclajes++;

            bool ok = EditorUtility.DisplayDialog("Preparar Anclajes",
                Resumen(cadena, puertas) +
                "\n\nSe van a escribir Anchor_In / Anchor_Out_N y el RoomDefinition en " +
                conAnclajes + " prefab(s) de sala.\n\n" +
                "Los anclajes existentes se reubican en la posicion ACTUAL de su puerta " +
                "(la puerta manda) y los sobrantes se borran.\n\n" +
                "¿Escribir?", "Escribir", "Cancelar");
            if (!ok)
                return;

            int escritos = 0;
            foreach (RoomInfo r in cadena)
            {
                if (r.Anchors.Count == 0)
                    continue;
                try
                {
                    EscribirPrefab(r);
                    escritos++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[MapAnchor] Error escribiendo " + r.AssetPath + ": " + e.Message, r.Root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[MapAnchor] Anclajes escritos en " + escritos + " prefab(s). Revisa los 9 prefabs " +
                      "en Assets/mapa/hospital (sobre todo salaCombate).");
            EditorUtility.DisplayDialog("Preparar Anclajes",
                "Anclajes escritos en " + escritos + " prefab(s).\n\n" +
                "Revisa cada prefab: Anchor_In debe quedar en la boca por donde se ENTRA " +
                "y Anchor_Out en la boca por donde se SALE.\n\nDespués corre " +
                "'Agregar NavMeshObstacle a PuertaFuncional'.", "OK");
        }

        private static bool PuedeCorrer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Mapa",
                    "Estas en Play Mode. Los cambios se pierden al salir.\n\n" +
                    "Salí de Play y vuelve a correr el menu.", "OK");
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Escena
        // ------------------------------------------------------------------

        // Instancias de prefab de Assets/mapa/hospital que estan en la escena.
        private static List<RoomInfo> BuscarSalasDeEscena()
        {
            List<RoomInfo> res = new List<RoomInfo>();
            HashSet<string> vistos = new HashSet<string>();

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.parent != null)
                    continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    continue;

                GameObject src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (src == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(src);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(RoomsFolder))
                    continue;

                if (!vistos.Add(path))
                {
                    Debug.LogWarning("[MapAnchor] '" + path + "' esta instanciado mas de una vez en la " +
                                     "escena; se usa la primera instancia.");
                    continue;
                }

                res.Add(new RoomInfo
                {
                    Name = src.name,
                    AssetPath = path,
                    Root = t.gameObject
                });
            }

            return res;
        }

        // Todas las puertas PuertaFuncional de la escena.
        private static List<Transform> BuscarPuertasDeEscena()
        {
            List<Transform> res = new List<Transform>();
            foreach (DoorController dc in Object.FindObjectsByType<DoorController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (dc != null && dc.transform != null)
                    res.Add(dc.transform);
            }
            return res;
        }

        // Union de los Renderer.bounds de la sala (mundo).
        private static Bounds CalcularBounds(GameObject root)
        {
            bool set = false;
            Bounds b = new Bounds(root.transform.position, Vector3.one * 4f);

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer)
                    continue;
                if (!set)
                {
                    b = r.bounds;
                    set = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return b;
        }

        // Distancia de un punto a un AABB.
        private static float DistanciaABounds(Vector3 p, Bounds b)
        {
            float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
            float dy = Mathf.Max(b.min.y - p.y, 0f, p.y - b.max.y);
            float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // ------------------------------------------------------------------
        // Cadena y clasificacion
        // ------------------------------------------------------------------

        // Ordena las salas en cadena de vecino mas cercano empezando por el hub.
        private static List<RoomInfo> ArmarCadena(List<RoomInfo> salas)
        {
            List<RoomInfo> cadena = new List<RoomInfo>();
            HashSet<RoomInfo> pendientes = new HashSet<RoomInfo>(salas);

            RoomInfo hub = salas.Find(r => r.Type == RoomType.Hub);
            if (hub == null)
            {
                Debug.LogWarning("[MapAnchor] No encontre ninguna sala hub por nombre: el orden de la " +
                                 "cadena es arbitrario y las salidas/entradas pueden quedar mal.");
                cadena.AddRange(salas);
                return cadena;
            }

            RoomInfo actual = hub;
            pendientes.Remove(actual);
            cadena.Add(actual);

            while (pendientes.Count > 0)
            {
                RoomInfo mejor = null;
                float mejorD = float.MaxValue;
                foreach (RoomInfo r in pendientes)
                {
                    float d = DistanciaABounds(actual.Bounds.center, r.Bounds);
                    if (d < mejorD)
                    {
                        mejorD = d;
                        mejor = r;
                    }
                }
                cadena.Add(mejor);
                pendientes.Remove(mejor);
                actual = mejor;
            }

            return cadena;
        }

        // Decide la entrada y las salidas de cada sala y arma los AnchorSpec.
        private static void Clasificar(List<RoomInfo> cadena)
        {
            for (int k = 0; k < cadena.Count; k++)
            {
                RoomInfo r = cadena[k];
                Transform rt = r.Root.transform;

                // Pose local de cada puerta y direccion "hacia afuera" de la sala.
                foreach (DoorHit d in r.Doors)
                {
                    d.LocalPos = rt.InverseTransformPoint(d.Door.position);
                    d.LocalOutward = SalidaLocal(rt, d.Door.position - r.Bounds.center);
                }

                r.Doors.Sort((a, b) => a.DistToRoom.CompareTo(b.DistToRoom));

                // El hub (y cualquier sala sin anterior) solo tiene salidas.
                if (r.Type == RoomType.Hub || k == 0)
                {
                    int n = 1;
                    foreach (DoorHit d in r.Doors)
                        r.Anchors.Add(SalidaSpec(d, n++));

                    if (r.Doors.Count == 0)
                    {
                        r.Warnings.Add("Sin puertas en la escena: se creo un Anchor_Out de referencia.");
                        r.Anchors.Add(PlaceholderOut(r, k > 0 ? cadena[k - 1] : null));
                    }
                    continue;
                }

                if (r.Doors.Count == 0)
                {
                    RoomInfo prev = cadena[k - 1];
                    r.Warnings.Add("Sin puertas en la escena: se crearon Anchor_In y Anchor_Out de " +
                                   "referencia (movelos a las bocas).");
                    r.Anchors.Add(PlaceholderIn(r, prev));
                    r.Anchors.Add(PlaceholderOut(r, prev));
                    continue;
                }

                // Entrada = puerta mas cercana a la sala anterior de la cadena.
                RoomInfo anterior = cadena[k - 1];
                DoorHit mejor = null;
                float mejorD = float.MaxValue;
                foreach (DoorHit d in r.Doors)
                {
                    float dist = DistanciaABounds(d.Door.position, anterior.Bounds);
                    if (dist < mejorD)
                    {
                        mejorD = dist;
                        mejor = d;
                    }
                }

                r.Anchors.Add(new AnchorSpec
                {
                    Name = AnchorInName,
                    LocalPos = mejor.LocalPos,
                    LocalRot = Quaternion.LookRotation(mejor.LocalOutward, Vector3.up)
                });

                int m = 1;
                int salidas = 0;
                foreach (DoorHit d in r.Doors)
                {
                    if (d == mejor)
                        continue;
                    r.Anchors.Add(SalidaSpec(d, m++));
                    salidas++;
                }

                if (salidas == 0)
                {
                    r.Warnings.Add("Solo tiene la puerta de entrada en la escena: se creo un " +
                                   "Anchor_Out de referencia en el lado opuesto.");
                    r.Anchors.Add(PlaceholderOut(r, anterior));
                }
                else if (salidas > 1)
                {
                    r.Warnings.Add(salidas + " puertas de salida: el generador usa la primera. " +
                                   "Revisa que las demas tengan sala al otro lado.");
                }
            }
        }

        private static AnchorSpec SalidaSpec(DoorHit d, int n)
        {
            return new AnchorSpec
            {
                Name = AnchorOutPrefix + n,
                LocalPos = d.LocalPos,
                LocalRot = Quaternion.LookRotation(d.LocalOutward, Vector3.up)
            };
        }

        // Ancla de entrada de referencia: cara de los bounds que mira a la
        // sala anterior, con el forward apuntando hacia ella.
        private static AnchorSpec PlaceholderIn(RoomInfo r, RoomInfo prev)
        {
            Vector3 haciaPrev = Horizontal(prev.Bounds.center - r.Bounds.center);
            if (haciaPrev.sqrMagnitude < 1e-6f)
                haciaPrev = Horizontal(r.Root.transform.forward);

            return new AnchorSpec
            {
                Name = AnchorInName,
                LocalPos = PuntoDeCara(r, haciaPrev),
                LocalRot = Quaternion.LookRotation(LocalDir(r, haciaPrev), Vector3.up)
            };
        }

        // Ancla de salida de referencia: cara opuesta a la anterior.
        private static AnchorSpec PlaceholderOut(RoomInfo r, RoomInfo prev)
        {
            Vector3 haciaFuera;
            if (prev != null)
            {
                haciaFuera = Horizontal(prev.Bounds.center - r.Bounds.center);
                if (haciaFuera.sqrMagnitude < 1e-6f)
                    haciaFuera = Horizontal(r.Root.transform.forward);
                haciaFuera = -haciaFuera;
            }
            else
            {
                haciaFuera = Horizontal(r.Root.transform.forward);
            }

            return new AnchorSpec
            {
                Name = AnchorOutPrefix + 1,
                LocalPos = PuntoDeCara(r, haciaFuera),
                LocalRot = Quaternion.LookRotation(LocalDir(r, haciaFuera), Vector3.up)
            };
        }

        // Punto sobre la cara del AABB en la direccion d (y = piso).
        private static Vector3 PuntoDeCara(RoomInfo r, Vector3 dir)
        {
            Bounds b = r.Bounds;
            Vector3 p = b.center;
            if (dir.x > 0f) p.x = b.max.x;
            else if (dir.x < 0f) p.x = b.min.x;
            if (dir.z > 0f) p.z = b.max.z;
            else if (dir.z < 0f) p.z = b.min.z;
            p.y = b.min.y;
            return r.Root.transform.InverseTransformPoint(p);
        }

        private static Vector3 LocalDir(RoomInfo r, Vector3 worldDir)
        {
            return SalidaLocal(r.Root.transform, worldDir);
        }

        // Direccion "hacia afuera" en el espacio local de la sala: horizontal,
        // normalizada y nunca nula (si no, LookRotation se queja).
        private static Vector3 SalidaLocal(Transform rt, Vector3 worldOffset)
        {
            Vector3 local = Horizontal(rt.InverseTransformDirection(worldOffset));
            if (local.sqrMagnitude > 1e-6f)
                return local.normalized;

            Vector3 fwd = Horizontal(rt.forward);
            if (fwd.sqrMagnitude > 1e-6f)
                return fwd.normalized;

            return Vector3.forward;
        }

        private static Vector3 Horizontal(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // ------------------------------------------------------------------
        // Prefab
        // ------------------------------------------------------------------

        private static void EscribirPrefab(RoomInfo r)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(r.AssetPath);
            int creados = 0;
            int conservados = 0;
            int reubicados = 0;
            int borrados = 0;
            try
            {
                RoomDefinition def = root.GetComponent<RoomDefinition>();
                if (def == null)
                {
                    def = root.AddComponent<RoomDefinition>();
                    def.weight = PesoPorDefecto(r.Type);
                }
                def.type = r.Type;

                // Anclajes que ya estan en el prefab. Se reubican en su puerta
                // (la puerta manda); los que no tengan puerta se borran abajo.
                List<Transform> existentes = new List<Transform>();
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    Transform c = root.transform.GetChild(i);
                    if (c.name == AnchorInName || c.name.StartsWith(AnchorOutPrefix))
                        existentes.Add(c);
                }

                // --- Anchor_In ---
                AnchorSpec specIn = null;
                foreach (AnchorSpec a in r.Anchors)
                    if (a.Name == AnchorInName)
                        specIn = a;

                Transform inT = null;
                foreach (Transform c in existentes)
                    if (c.name == AnchorInName)
                        inT = c;

                if (specIn != null)
                {
                    if (inT != null)
                    {
                        // Se reubica en la puerta actual: la puerta de la escena
                        // es la fuente de verdad del ancla.
                        reubicados += ActualizarAncla(inT, specIn);
                        conservados++;
                    }
                    else
                    {
                        inT = CrearAncla(root.transform, specIn);
                        creados++;
                    }
                }

                // --- Anchor_Out_* ---
                List<Transform> outsExistentes = new List<Transform>();
                foreach (Transform c in existentes)
                    if (c.name.StartsWith(AnchorOutPrefix))
                        outsExistentes.Add(c);
                outsExistentes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

                List<AnchorSpec> specsOut = new List<AnchorSpec>();
                foreach (AnchorSpec a in r.Anchors)
                    if (a.Name != AnchorInName)
                        specsOut.Add(a);

                List<Transform> outs = new List<Transform>();
                for (int i = 0; i < specsOut.Count; i++)
                {
                    if (i < outsExistentes.Count)
                    {
                        Transform t = outsExistentes[i];
                        reubicados += ActualizarAncla(t, specsOut[i]);
                        conservados++;
                        outs.Add(t);
                    }
                    else
                    {
                        outs.Add(CrearAncla(root.transform, specsOut[i]));
                        creados++;
                    }
                }

                // Anclas de salida sin puerta que correspondan (sobrantes de una
                // corrida anterior): se borran para que el prefab no quede confuso.
                for (int i = specsOut.Count; i < outsExistentes.Count; i++)
                {
                    Transform extra = outsExistentes[i];
                    Debug.LogWarning("[MapAnchor] " + r.Name + ": se borra el ancla de salida sobrante '" +
                                     extra.name + "' (no tiene puerta en la escena).", extra.gameObject);
                    Object.DestroyImmediate(extra.gameObject);
                    borrados++;
                }

                def.anchorIn = inT;
                def.anchorsOut = outs.ToArray();
                EditorUtility.SetDirty(def);

                PrefabUtility.SaveAsPrefabAsset(root, r.AssetPath);

                Debug.Log("[MapAnchor] " + r.Name + ": anclajes creados=" + creados +
                          " conservados=" + conservados + " reubicados=" + reubicados +
                          " borrados=" + borrados +
                          " entrada=" + (inT != null ? inT.name : "(ninguna)") +
                          " salidas=" + outs.Count + " tipo=" + r.Type + " peso=" + def.weight,
                    r.Root);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform CrearAncla(Transform root, AnchorSpec spec)
        {
            GameObject go = new GameObject(spec.Name);
            Transform t = go.transform;
            t.SetParent(root, false);
            t.localPosition = spec.LocalPos;
            t.localRotation = spec.LocalRot;
            t.localScale = Vector3.one;
            return t;
        }

        // Mueve un ancla existente a la pose del spec. Devuelve 1 si se movio
        // mas de 5 cm (para contar "reubicados" en el log) o 0 si ya estaba.
        private static int ActualizarAncla(Transform t, AnchorSpec spec)
        {
            bool movio = Vector3.Distance(t.localPosition, spec.LocalPos) > 0.05f;
            t.name = spec.Name;
            t.localPosition = spec.LocalPos;
            t.localRotation = spec.LocalRot;
            t.localScale = Vector3.one;
            return movio ? 1 : 0;
        }

        private static RoomType InferirTipo(string nombre)
        {
            string n = nombre.ToLowerInvariant();
            if (n.Contains("hub")) return RoomType.Hub;
            if (n.Contains("historia")) return RoomType.Historia;
            if (n.Contains("loot")) return RoomType.Loot;
            if (n.Contains("combate")) return RoomType.Combate;
            if (n.Contains("puzzle")) return RoomType.Puzzle;
            if (n.Contains("pasillo")) return RoomType.Pasillo;
            Debug.LogWarning("[MapAnchor] No se pudo inferir el tipo de '" + nombre + "'; queda Pasillo.");
            return RoomType.Pasillo;
        }

        private static float PesoPorDefecto(RoomType t)
        {
            // Historia pesa poco a proposito: el generador ademas le aplica un
            // multiplicador aleatorio bajo en cada sorteo.
            return t == RoomType.Historia ? 0.35f : 1f;
        }

        // ------------------------------------------------------------------
        // Reporte
        // ------------------------------------------------------------------

        private static string Resumen(List<RoomInfo> cadena, List<Transform> puertas)
        {
            int sinPuertas = 0;
            int asignadas = 0;
            foreach (RoomInfo r in cadena)
            {
                if (r.Doors.Count == 0) sinPuertas++;
                asignadas += r.Doors.Count;
            }
            int sinAsignar = puertas.Count - asignadas;

            return "Salas=" + cadena.Count +
                   "  con puertas=" + (cadena.Count - sinPuertas) +
                   "  sin puertas=" + sinPuertas +
                   "  puertas en escena=" + puertas.Count +
                   "  asignadas=" + asignadas +
                   "  sin sala cerca (>" + MaxDoorDistance + " m)=" + Mathf.Max(0, sinAsignar);
        }

        private static string ArmarReporte(List<RoomInfo> cadena, List<Transform> puertas, List<RoomInfo> salas)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================ MAPA: analisis de salas y puertas ================");
            sb.AppendLine(Resumen(cadena, puertas));

            HashSet<Transform> usadas = new HashSet<Transform>();
            foreach (RoomInfo r in cadena)
                foreach (DoorHit d in r.Doors)
                    usadas.Add(d.Door);

            foreach (RoomInfo r in cadena)
            {
                sb.AppendLine();
                sb.AppendLine("SALA " + r.Name + "  [" + r.Type + "]");
                sb.AppendLine("   prefab: " + r.AssetPath);
                sb.AppendLine(string.Format("   bounds: centro=({0:0.0}, {1:0.0}, {2:0.0})  tam=({3:0.0}, {4:0.0}, {5:0.0})",
                    r.Bounds.center.x, r.Bounds.center.y, r.Bounds.center.z,
                    r.Bounds.size.x, r.Bounds.size.y, r.Bounds.size.z));
                sb.AppendLine("   puertas: " + r.Doors.Count);
                foreach (DoorHit d in r.Doors)
                    sb.AppendLine("      - " + d.Door.name + "  dist=" + d.DistToRoom.ToString("0.0") + " m  " +
                                  "pos local=(" + d.LocalPos.x.ToString("0.0") + ", " +
                                  d.LocalPos.y.ToString("0.0") + ", " + d.LocalPos.z.ToString("0.0") + ")");

                sb.Append("   anclajes: ");
                if (r.Anchors.Count == 0)
                    sb.AppendLine("(ninguno)");
                else
                {
                    for (int i = 0; i < r.Anchors.Count; i++)
                    {
                        AnchorSpec a = r.Anchors[i];
                        sb.Append(a.Name + "(" + a.LocalPos.x.ToString("0.0") + ", " +
                                  a.LocalPos.y.ToString("0.0") + ", " + a.LocalPos.z.ToString("0.0") + ")");
                        if (i < r.Anchors.Count - 1) sb.Append(", ");
                    }
                    sb.AppendLine();
                }

                foreach (string w in r.Warnings)
                    sb.AppendLine("   AVISO: " + w);
            }

            List<Transform> sinSala = new List<Transform>();
            foreach (Transform p in puertas)
                if (!usadas.Contains(p))
                    sinSala.Add(p);

            if (sinSala.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("PUERTAS SIN SALA CERCANA (mas de " + MaxDoorDistance + " m de cualquier sala): " +
                              sinSala.Count);
                foreach (Transform p in sinSala)
                    sb.AppendLine("   - " + p.name + " en (" + p.position.x.ToString("0.0") + ", " +
                                  p.position.y.ToString("0.0") + ", " + p.position.z.ToString("0.0") + ")");
            }

            sb.AppendLine();
            sb.AppendLine("Los anclajes siguen la posicion de las puertas: si alguno quedo en el");
            sb.AppendLine("lugar equivocado, move la puerta en la escena y vuelve a correr");
            sb.AppendLine("'Preparar Anclajes' (reubica los anclajes existentes en su puerta).");
            sb.AppendLine("Los ajustes que hagas a mano en el prefab se pierden si vuelves a correr");
            sb.AppendLine("la herramienta, asi que dejalos para el ultimo paso.");
            sb.AppendLine("==================================================================");
            return sb.ToString();
        }
    }
}
