using RPGAuto.Combat;
using RPGAuto.Monster;
using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>상단 타겟 정보: "Lv.7 Goblin Warrior" (레벨 차이에 따라 색상) + HP</summary>
    public class TargetInfoView : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private GameObject content;
        [SerializeField] private Text nameText;
        [SerializeField] private StatBarView hpBar;

        private MonsterController _target;
        private CombatStats _bound;

        private void OnEnable()
        {
            if (combat != null)
            {
                combat.OnTargetChanged += Show;
                Show(combat.Target);
            }
            if (progression != null) progression.OnLevelChanged += HandlePlayerLevel;
        }

        private void OnDisable()
        {
            if (combat != null) combat.OnTargetChanged -= Show;
            if (progression != null) progression.OnLevelChanged -= HandlePlayerLevel;
            Unbind();
        }

        private void Show(MonsterController target)
        {
            Unbind();
            _target = target;
            if (content != null) content.SetActive(target != null);
            if (target == null) return;

            RefreshName();
            _bound = target.Stats;
            _bound.OnHpChanged += HandleHp;
            HandleHp(_bound.Hp, _bound.MaxHp);
        }

        private void HandlePlayerLevel(int level)
        {
            if (_target != null) RefreshName();
        }

        private void RefreshName()
        {
            string elite = _target.Grade == MonsterGrade.Elite ? " <size=18>[Elite]</size>" : string.Empty;
            nameText.text = $"Lv.{_target.Level} {_target.DisplayName}{elite}";
            int playerLevel = progression != null ? progression.Level : 1;
            nameText.color = LevelColorRule.Get(_target.Level, playerLevel);
        }

        private void Unbind()
        {
            if (_bound != null) _bound.OnHpChanged -= HandleHp;
            _bound = null;
            _target = null;
        }

        private void HandleHp(float current, float max) => hpBar?.SetValue(current, max);
    }
}
