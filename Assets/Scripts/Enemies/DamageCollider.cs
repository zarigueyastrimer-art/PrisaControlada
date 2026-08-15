using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Enemies
{
    public class DamageCollider : MonoBehaviour
    {
        private PlayerHealth _target;
        private int _damage;
        private bool _hasDealt;

        public void Setup(PlayerHealth target, int damage)
        {
            _target = target;
            _damage = damage;
        }

        private void OnEnable()
        {
            _hasDealt = false;
        }

        public void CheckOverlap()
        {
            if (_hasDealt || _target == null) return;

            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null) return;

            float worldRadius = sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            Collider[] hits = Physics.OverlapSphere(transform.position, worldRadius, ~0, QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                if (IsTarget(hit.transform))
                {
                    ApplyDamage();
                    return;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasDealt || _target == null) return;

            if (IsTarget(other.transform))
                ApplyDamage();
        }

        private bool IsTarget(Transform t)
        {
            return _target != null
                && (t == _target.transform || t.IsChildOf(_target.transform));
        }

        private void ApplyDamage()
        {
            if (_hasDealt || _target == null) return;

            _hasDealt = true;
            _target.TakeDamage(_damage);
        }
    }
}