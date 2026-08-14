using UnityEngine;

namespace PrisaControlada.Items
{
    [RequireComponent(typeof(Rigidbody))]
    public class PickupItem : MonoBehaviour
    {
        [Header("Hold Lag")]
        [SerializeField] private float followSpeed = 12f;
        [SerializeField] private float maxHoldDistance = 5f;
        [SerializeField] private LayerMask wallMask = ~0;
        [SerializeField] private float wallDropOffset = 0.35f;

        private Rigidbody _rb;
        private Transform _holder;
        private Vector3 _holdOffset;
        private Vector3 _prevPlayerPos;
        private Transform _playerRoot;
        private float _objectRadius;

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _objectRadius = ComputeObjectRadius();
        }

        public void PickUp(Transform holder, Vector3 holdOffset)
        {
            IsHeld = true;
            _holder = holder;
            _holdOffset = holdOffset;
            _playerRoot = holder.root;
            _prevPlayerPos = _playerRoot.position;

            _rb.isKinematic = true;
            _rb.detectCollisions = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            transform.SetParent(null, true);
        }

        public void Drop()
        {
            Release();
        }

        public void Throw(Vector3 velocity)
        {
            Release();
            _rb.linearVelocity = velocity;
        }

        private void Release()
        {
            IsHeld = false;
            _holder = null;
            _playerRoot = null;
            _rb.isKinematic = false;
            _rb.detectCollisions = true;
        }

        private void LateUpdate()
        {
            if (!IsHeld || _holder == null) return;

            Vector3 playerPos = _playerRoot.position;
            Vector3 playerDelta = playerPos - _prevPlayerPos;
            _prevPlayerPos = playerPos;

            transform.position += playerDelta;

            Vector3 target = _holder.position + _holder.rotation * _holdOffset;
            Vector3 toTarget = target - transform.position;

            if (toTarget.magnitude > maxHoldDistance)
                transform.position = target - toTarget.normalized * maxHoldDistance;
            else
                transform.position = Vector3.Lerp(transform.position, target, followSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Slerp(transform.rotation, _holder.rotation, followSpeed * Time.deltaTime);

            ClampAgainstWalls();
        }

        private void ClampAgainstWalls()
        {
            Transform eye = _holder.parent != null ? _holder.parent : _holder;
            Vector3 target = _holder.position + _holder.rotation * _holdOffset;
            Vector3 dir = target - eye.position;
            float dist = dir.magnitude;
            if (dist < 0.01f) return;
            dir /= dist;

            RaycastHit[] hits = Physics.SphereCastAll(eye.position, _objectRadius, dir, dist, wallMask, QueryTriggerInteraction.Ignore);
            float minDist = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform == _playerRoot || hit.collider.transform.IsChildOf(_playerRoot))
                    continue;
                if (hit.distance < minDist)
                    minDist = hit.distance;
            }

            if (minDist < dist)
            {
                Vector3 rest = eye.position + dir * Mathf.Max(minDist, 0.01f) + Vector3.down * wallDropOffset;
                transform.position = Vector3.Lerp(transform.position, rest, followSpeed * Time.deltaTime);
            }
        }

        private float ComputeObjectRadius()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);

            float extent = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.y), bounds.extents.z);
            return Mathf.Max(extent, 0.05f);
        }
    }
}