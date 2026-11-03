using RPGAuto.Combat;
using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>좌상단 레벨 배지 + HP/MP (CombatStats·PlayerProgression 이벤트 구독)</summary>
    public class PlayerStatusView : MonoBehaviour
    {
        [SerializeField] private CombatStats stats;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private StatBarView hpBar;
        [SerializeField] private StatBarView mpBar;
        [SerializeField] private Text levelText;

        private void OnEnable()
        {
            if (stats != null)
            {
                stats.OnHpChanged += HandleHp;
                stats.OnMpChanged += HandleMp;
            }
            if (progression != null) progression.OnLevelChanged += HandleLevel;
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.OnHpChanged -= HandleHp;
                stats.OnMpChanged -= HandleMp;
            }
            if (progression != null) progression.OnLevelChanged -= HandleLevel;
        }

        private void Start()
        {
            if (stats != null)
            {
                HandleHp(stats.Hp, stats.MaxHp);
                HandleMp(stats.Mp, stats.MaxMp);
            }
            if (progression != null) HandleLevel(progression.Level);
        }

        private void HandleHp(float current, float max) => hpBar?.SetValue(current, max);
        private void HandleMp(float current, float max) => mpBar?.SetValue(current, max);

        private void HandleLevel(int level)
        {
            if (levelText != null) levelText.text = level.ToString();
        }
    }
}
