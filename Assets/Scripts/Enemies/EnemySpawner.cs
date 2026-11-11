using System;
using System.Collections.Generic;
using UnityEngine;
using PrisaControlada.Map;

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
        private Transform _roomRoot;

        public IReadOnlyList<EnemyBase> Spawned => _spawned;

        private void Awake()
        {
            // Cachear la raíz de la sala (el objeto con RoomDefinition)
            _roomRoot = GetComponentInParent<RoomDefinition>()?.transform ?? transform.root;
        }

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
            
            // Parentear el enemigo a la sala para que se destruya con ella
            if (_roomRoot != null && enemy != null)
                enemy.transform.SetParent(_roomRoot, true);
            
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