using UnityEngine;

namespace PrisaControlada.Player
{
    public class WeaponSway : MonoBehaviour
    {
        [Header("Rotation Lag")]
        [SerializeField, Range(0f, 1f)] private float rotationStrength = 0.15f;
        [SerializeField, Range(0f, 20f)] private float rotationRecovery = 8f;
        [SerializeField, Range(0f, 15f)] private float maxRotationLag = 6f;

        [Header("Position Sway")]
        [SerializeField] private Vector3 positionSwayScale = new Vector3(0.02f, 0.015f, 0.01f);
        [SerializeField, Range(0f, 20f)] private float positionRecovery = 6f;

        private Quaternion _lagRot = Quaternion.identity;
        private Quaternion _prevCamRot = Quaternion.identity;
        private Vector3 _lagPos = Vector3.zero;
        private Vector3 _prevCamPos;
        private Transform _cam;
        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;

        void Start()
        {
            var cam = GetComponentInParent<Camera>();
            if (cam != null)
            {
                _cam = cam.transform;
                _prevCamRot = _cam.rotation;
                _prevCamPos = _cam.localPosition;
            }
            else
            {
                enabled = false;
                return;
            }
            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;
        }

        void LateUpdate()
        {
            if (_cam == null) return;

            UpdateRotationLag();
            UpdatePositionSway();

            transform.localPosition = _baseLocalPos + _lagPos;
            transform.localRotation = _lagRot * _baseLocalRot;
        }

        void UpdateRotationLag()
        {
            Quaternion camRot = _cam.rotation;
            Quaternion delta = camRot * Quaternion.Inverse(_prevCamRot);
            _prevCamRot = camRot;

            Vector3 eul = delta.eulerAngles;
            Vector3 d = new Vector3(
                Mathf.DeltaAngle(0f, eul.x),
                Mathf.DeltaAngle(0f, eul.y),
                Mathf.DeltaAngle(0f, eul.z));

            _lagRot = Quaternion.Euler(-d * rotationStrength) * _lagRot;
            _lagRot = Quaternion.RotateTowards(Quaternion.identity, _lagRot, maxRotationLag);
            _lagRot = Quaternion.Slerp(_lagRot, Quaternion.identity, rotationRecovery * Time.deltaTime);
        }

        void UpdatePositionSway()
        {
            Vector3 camPos = _cam.localPosition;
            Vector3 deltaPos = camPos - _prevCamPos;
            _prevCamPos = camPos;

            _lagPos += new Vector3(
                -deltaPos.x * positionSwayScale.x,
                -deltaPos.y * positionSwayScale.y,
                -deltaPos.z * positionSwayScale.z);

            _lagPos = Vector3.Lerp(_lagPos, Vector3.zero, positionRecovery * Time.deltaTime);
        }
    }
}
