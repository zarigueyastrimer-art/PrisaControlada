using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Core
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMesh : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 2f;

        private NavMeshSurface _surface;
        private float _nextUpdateTime;
        private AsyncOperation _pendingUpdate;
        private int _lastWorldHash;

        private void Awake()
        {
            _surface = GetComponent<NavMeshSurface>();

            _surface.collectObjects = CollectObjects.All;
            _surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
            _surface.ignoreNavMeshAgent = true;
            _surface.ignoreNavMeshObstacle = true;

            _lastWorldHash = ComputeWorldHash();
            _surface.BuildNavMesh();
            _nextUpdateTime = Time.time + updateInterval;
        }

        private void Update()
        {
            if (_surface == null || _surface.navMeshData == null) return;

            if (_pendingUpdate != null && !_pendingUpdate.isDone) return;

            if (Time.time < _nextUpdateTime) return;
            _nextUpdateTime = Time.time + updateInterval;

            // Solo se reconstruye si la geometria que el NavMeshSurface hornea
            // (renderers que no son agentes) cambio. Asi los agentes no se
            // desplazan mientras el mundo esta quieto.
            int hash = ComputeWorldHash();
            if (hash == _lastWorldHash) return;
            _lastWorldHash = hash;

            _pendingUpdate = _surface.UpdateNavMesh(_surface.navMeshData);
        }

        private int ComputeWorldHash()
        {
            int hash = 17;

            MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
            foreach (MeshRenderer r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.GetComponentInParent<NavMeshAgent>() != null) continue;
                if (!r.gameObject.isStatic) continue;

                Transform t = r.transform;
                hash = hash * 31 + t.position.GetHashCode();
                hash = hash * 31 + t.rotation.GetHashCode();
                hash = hash * 31 + t.lossyScale.GetHashCode();
                hash = hash * 31 + r.bounds.GetHashCode();
            }

            return hash;
        }
    }
}