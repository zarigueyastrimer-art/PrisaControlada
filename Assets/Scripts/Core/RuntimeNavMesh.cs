using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Core
{
    // Horno de NavMesh en runtime.
    //
    // Se hornea al iniciar (solo lo que hay en escena: el hub) y despues lo
    // vuelve a llamar ProceduralMapGenerator.RebakeGlobal() cada vez que la
    // ventana de salas cambia (spawn sincrono / despawn al frame siguiente),
    // para que salas + losas de umbral de puertas queden en UNA sola malla.
    // Antes se rehorneaba cada 2 segundos aunque nada se hubiera movido: eso
    // generaba ~34.000 warnings de "does not allow read access" por Play y
    // tirones mientras se jugaba.
    //
    // La capa DOOR (3) no esta en la mascara del NavMeshSurface (1335), asi
    // que abrir o cerrar puertas no necesita rehornear. Si en el futuro algo
    // si lo necesita, llamar a Rebake() desde ese script.
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMesh : MonoBehaviour
    {
        private NavMeshSurface _surface;

        private void Awake()
        {
            _surface = GetComponent<NavMeshSurface>();

            _surface.collectObjects = CollectObjects.All;
            _surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            _surface.ignoreNavMeshAgent = true;
            _surface.ignoreNavMeshObstacle = true;

            Rebake();
        }

        // Rehornea con la geometria actual de la escena.
        public void Rebake()
        {
            if (_surface == null)
                _surface = GetComponent<NavMeshSurface>();

            if (_surface == null)
                return;

            _surface.BuildNavMesh();
        }
    }
}
