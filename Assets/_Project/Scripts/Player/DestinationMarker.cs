using UnityEngine;

namespace RPGAuto.Player
{
    /// <summary>자동 이동 목적지 표시. PlayerController 이벤트를 구독한다 (Observer).</summary>
    public class DestinationMarker : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private float heightOffset = 0.03f;
        [SerializeField] private float pulseSpeed = 6f;
        [SerializeField] private float pulseAmount = 0.15f;

        private GameObject _marker;
        private Vector3 _baseScale;

        private void Awake()
        {
            if (markerPrefab == null) return;
            _marker = Instantiate(markerPrefab, transform);
            _baseScale = _marker.transform.localScale;
            _marker.SetActive(false);
        }

        private void OnEnable()
        {
            if (player == null) return;
            player.OnDestinationSet += Show;
            player.OnDestinationCleared += Hide;
        }

        private void OnDisable()
        {
            if (player == null) return;
            player.OnDestinationSet -= Show;
            player.OnDestinationCleared -= Hide;
        }

        private void Update()
        {
            if (_marker == null || !_marker.activeSelf) return;
            float s = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            _marker.transform.localScale = new Vector3(_baseScale.x * s, _baseScale.y, _baseScale.z * s);
        }

        private void Show(Vector3 position)
        {
            if (_marker == null) return;
            _marker.transform.position = position + Vector3.up * heightOffset;
            _marker.SetActive(true);
        }

        private void Hide()
        {
            if (_marker != null) _marker.SetActive(false);
        }
    }
}
