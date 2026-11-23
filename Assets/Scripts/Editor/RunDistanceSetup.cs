using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    // Coloca el origen de la medicion de distancia en la escena activa.
    //
    // Crea (o reubica) el objeto vacio "OrigenRecorrido" justo en el spawn del
    // jugador y le agrega el RunDistanceTracker. Corre una vez por escena:
    // hay que hacerlo en Prototype y en Prototype Old.
    public static class RunDistanceSetup
    {
        private const string NombreOrigen = "OrigenRecorrido";
        private const string MenuPath = "Tools/PrisaControlada/Distancia Recorrida: Setup (origen en el spawn)";

        [MenuItem(MenuPath, false, 310)]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Distancia Recorrida", "Guarda la escena antes de hacer el setup.", "OK");
                return;
            }

            PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (health == null)
            {
                EditorUtility.DisplayDialog("Distancia Recorrida",
                    "No hay un PlayerHealth en la escena activa (" + scene.name + ").\n" +
                    "Abrí Prototype o Prototype Old y volvé a correr el setup.", "OK");
                return;
            }

            Transform player = health.transform;

            // Si ya existe lo reubico en el spawn; si no, lo creo.
            GameObject go = GameObject.Find(NombreOrigen);
            if (go == null)
                go = new GameObject(NombreOrigen);

            go.transform.SetPositionAndRotation(player.position, Quaternion.identity);

            RunDistanceTracker tracker = go.GetComponent<RunDistanceTracker>();
            if (tracker == null)
                tracker = go.AddComponent<RunDistanceTracker>();

            // El origen es el propio objeto y el jugador queda cableado a mano.
            // El resto de los campos (ignorarAltura, etc.) no se tocan para no
            // pisar ajustes hechos por la interfaz.
            SerializedObject so = new SerializedObject(tracker);
            so.FindProperty("origen").objectReferenceValue = null;
            so.FindProperty("jugador").objectReferenceValue = player;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Distancia] Setup listo en '{scene.name}': '{NombreOrigen}' en " +
                      $"{player.position} midiendo a '{player.name}'.", go);
        }
    }
}
