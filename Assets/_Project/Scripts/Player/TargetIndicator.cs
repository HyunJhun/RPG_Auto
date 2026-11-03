using RPGAuto.Monster;
using UnityEngine;

namespace RPGAuto.Player
{
    /// <summary>현재 타겟 발밑 표시. PlayerCombat.OnTargetChanged 구독 (Observer)</summary>
    public class TargetIndicator : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private float heightOffset = 0.04f;
        [SerializeField] private float spinSpeed = 90f;

        private GameObject _visual;
        private Vector3 _visualBaseScale;
        private MonsterController _target;

        private void Awake()
        {
            if (visualPrefab == null) return;
            _visual = Instantiate(visualPrefab, transform);
            _visualBaseScale = _visual.transform.localScale;
            _visual.SetActive(false);
        }

        private void OnEnable()
        {
            if (combat != null) combat.OnTargetChanged += HandleTargetChanged;
        }

        private void OnDisable()
        {
            if (combat != null) combat.OnTargetChanged -= HandleTargetChanged;
        }

        private void HandleTargetChanged(MonsterController target)
        {
            _target = target;
            if (_visual == null) return;
            _visual.SetActive(target != null);
            if (target != null)
            {
                float s = target.transform.lossyScale.x;
                _visual.transform.localScale = new Vector3(_visualBaseScale.x * s, _visualBaseScale.y, _visualBaseScale.z * s);
            }
        }

        private void LateUpdate()
        {
            if (_visual == null || !_visual.activeSelf) return;
            if (_target == null || !_target.IsAlive)
            {
                _visual.SetActive(false);
                return;
            }

            _visual.transform.position = _target.FootPosition + Vector3.up * heightOffset;
            _visual.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }
    }
}
