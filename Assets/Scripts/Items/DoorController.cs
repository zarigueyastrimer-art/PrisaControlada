using UnityEngine;

namespace PrisaControlada.Items
{
    public class DoorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform doorPanel;
        [SerializeField] private Transform hinge;
        [SerializeField] private Transform player;

        [Header("Settings")]
        [Tooltip("Angulo de apertura de la bisagra en grados.")]
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 5f;
        [Tooltip("Extremo del panel donde esta la bisagra: +1 = borde +X, -1 = borde -X. Fija, no cambia con el jugador.")]
        [SerializeField] private float hingeSide = 1f;

        private Quaternion _targetRotation = Quaternion.identity;
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (player == null)
            {
                GameObject playerGo = GameObject.Find("Player");
                if (playerGo != null)
                    player = playerGo.transform;
            }
        }

        public bool IsDoorHit(Collider hitCollider)
        {
            if (doorPanel == null) return false;

            Transform current = hitCollider.transform;
            while (current != null)
            {
                if (current == doorPanel) return true;
                current = current.parent;
            }

            return false;
        }

        public void Toggle()
        {
            if (doorPanel == null || hinge == null) return;

            if (!_isOpen)
            {
                float swingSide = ComputeSwingSide();
                _targetRotation = Quaternion.Euler(0f, -swingSide * openAngle, 0f);
            }
            else
            {
                _targetRotation = Quaternion.identity;
            }

            _isOpen = !_isOpen;
        }

        private float ComputeSwingSide()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.right;
            forward.Normalize();

            if (player == null)
                return -1f;

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;

            return Vector3.Dot(toPlayer, forward) >= 0f ? 1f : -1f;
        }

        private void Update()
        {
            if (hinge == null) return;

            hinge.localRotation = Quaternion.Slerp(hinge.localRotation, _targetRotation, openSpeed * Time.deltaTime);
        }

        [ContextMenu("Build Example Door")]
        private void BuildExampleDoor()
        {
#if UNITY_EDITOR
            Transform root = transform;

            Transform panelT = root.Find("DoorPanel");
            if (panelT == null)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "DoorPanel";
                panel.transform.SetParent(root, false);
                panel.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                panel.transform.localScale = new Vector3(1.2f, 2.2f, 0.05f);
                panelT = panel.transform;
            }

            float halfWidth = Mathf.Max(panelT.localScale.x * 0.5f, 0.05f);
            Vector3 panelCenterLocal = root.InverseTransformPoint(panelT.position);

            Transform hingeT = root.Find("Hinge");
            if (hingeT == null)
            {
                GameObject hingeGo = new GameObject("Hinge");
                hingeT = hingeGo.transform;
                hingeT.SetParent(root, false);
                hingeT.localPosition = new Vector3(hingeSide * halfWidth, panelCenterLocal.y, panelCenterLocal.z);
                hingeT.localRotation = Quaternion.identity;
            }

            panelT.SetParent(hingeT, true);
            panelT.localRotation = Quaternion.identity;

            BuildFramePart("JambL", new Vector3(0.7f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), root);
            BuildFramePart("JambR", new Vector3(-0.7f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), root);
            BuildFramePart("Lintel", new Vector3(0f, 2.25f, 0f), new Vector3(1.5f, 0.1f, 0.1f), root);

            if (root.position == Vector3.zero && root.rotation == Quaternion.identity)
                root.position = new Vector3(6f, 0f, 2f);

            if (doorPanel == null)
                doorPanel = panelT;
            if (hinge == null)
                hinge = hingeT;

            if (player == null)
            {
                GameObject playerGo = GameObject.Find("Player");
                if (playerGo != null)
                    player = playerGo.transform;
            }

            _targetRotation = Quaternion.identity;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[DoorController] Puerta de bisagra de ejemplo construida. Guarda la escena.", this);
#endif
        }

#if UNITY_EDITOR
        private static void BuildFramePart(string name, Vector3 localPosition, Vector3 localScale, Transform parent)
        {
            if (parent.Find(name) != null)
                return;

            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
        }
#endif
    }
}