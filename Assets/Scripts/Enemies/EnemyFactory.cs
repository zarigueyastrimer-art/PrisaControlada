using System;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Enemies
{
    public enum EnemyType
    {
        Zombie,
        Runner,
        Blind,
        Nemesis
    }

    public static class EnemyFactory
    {
        private static Mesh _capsuleMesh;

        public static EnemyBase Create(EnemyType type, Vector3 position, string name = null)
        {
            // Pivot en los pies: transform.position = contacto con el suelo.
            GameObject go = new GameObject(name ?? $"{type}Enemy");
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            // Cuerpo visual en un hijo elevado para que apoye en el pivot.
            GameObject body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);

            MeshFilter mf = body.AddComponent<MeshFilter>();
            mf.sharedMesh = GetCapsuleMesh();

            MeshRenderer mr = body.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreateMaterial(GetColor(type));

            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            agent.speed = 2f;
            agent.acceleration = 8f;
            agent.angularSpeed = 720f;
            agent.updateRotation = false;

            // Solid collider so the player cannot walk through the enemy.
            CapsuleCollider bodyCollider = go.AddComponent<CapsuleCollider>();
            bodyCollider.radius = 0.45f;
            bodyCollider.height = 2f;
            bodyCollider.center = new Vector3(0f, 1f, 0f);

            AddLenses(go);

            Type componentType = type switch
            {
                EnemyType.Zombie => typeof(ZombieEnemy),
                EnemyType.Runner => typeof(RunnerEnemy),
                EnemyType.Blind => typeof(BlindEnemy),
                _ => typeof(NemesisEnemy)
            };

            EnemyBase enemy = (EnemyBase)go.AddComponent(componentType);
            enemy.ApplyDefaultStats();

            return enemy;
        }

        private static void AddLenses(GameObject parent)
        {
            GameObject lenses = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lenses.name = "Lenses";
            lenses.transform.SetParent(parent.transform, false);
            lenses.transform.localPosition = new Vector3(0f, 1.55f, 0.55f);
            lenses.transform.localScale = new Vector3(0.18f, 0.08f, 0.08f);

            Collider collider = lenses.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            lenses.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(1f, 1f, 1f));
        }

        private static Mesh GetCapsuleMesh()
        {
            if (_capsuleMesh != null) return _capsuleMesh;

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _capsuleMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temp);

            return _capsuleMesh;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        public static Color GetColor(EnemyType type)
        {
            return type switch
            {
                EnemyType.Zombie => new Color(0.8f, 0.1f, 0.1f),
                EnemyType.Runner => new Color(0.1f, 0.1f, 0.1f),
                EnemyType.Blind => new Color(0.15f, 0.35f, 0.9f),
                _ => new Color(0.9f, 0.8f, 0.1f)
            };
        }
    }
}