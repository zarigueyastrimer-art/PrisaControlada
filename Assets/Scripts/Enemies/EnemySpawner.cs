using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrisaControlada.Enemies
{
    [Serializable]
    public class EnemySpawnPoint
    {
        public EnemyType type = EnemyType.Zombie;
        public Vector3 position = Vector3.zero;
        public bool spawnOnStart = true;
    }

    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private EnemySpawnPoint[] spawnPoints = Array.Empty<EnemySpawnPoint>();

        private readonly List<EnemyBase> _spawned = new List<EnemyBase>();

        public IReadOnlyList<EnemyBase> Spawned => _spawned;

        private void Start()
        {
            if (spawnPoints == null) return;

            foreach (EnemySpawnPoint point in spawnPoints)
            {
                if (point != null && point.spawnOnStart)
                    _spawned.Add(Spawn(point.type, point.position));
            }
        }

        public EnemyBase Spawn(EnemyType type, Vector3 position)
        {
            EnemyBase enemy = EnemyFactory.Create(type, position);
            _spawned.Add(enemy);
            return enemy;
        }

        public void DespawnAll()
        {
            foreach (EnemyBase enemy in _spawned)
            {
                if (enemy != null)
                    Destroy(enemy.gameObject);
            }
            _spawned.Clear();
        }
    }
}