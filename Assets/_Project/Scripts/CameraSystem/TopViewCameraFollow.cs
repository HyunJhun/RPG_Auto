using UnityEngine;

namespace RPGAuto.CameraSystem
{
    /// <summary>3D 탑뷰(쿼터뷰) 고정 각도 추적 카메라</summary>
    public class TopViewCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 16f;
        [SerializeField, Range(10f, 90f)] private float pitch = 55f;
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float smoothTime = 0.1f;

        private Vector3 _velocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }

        private void Start() => SnapToTarget();

        private void LateUpdate()
        {
            if (target == null) return;
            transform.rotation = Rotation;
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition, ref _velocity, smoothTime);
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.SetPositionAndRotation(DesiredPosition, Rotation);
            _velocity = Vector3.zero;
        }

        private Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);
        private Vector3 DesiredPosition => target.position - Rotation * Vector3.forward * distance;
    }
}
