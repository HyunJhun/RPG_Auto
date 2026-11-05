using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Combat.Targeting;
using RPGAuto.Monster;
using RPGAuto.Skills;
using UnityEngine;

namespace RPGAuto.Player
{
    /// <summary>플레이어 전투 상태 데이터: 자동 전투 ON/OFF, 현재 타겟, 사냥 기준점, 예약 스킬</summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private AutoCombatSettings settings = new();
        [SerializeField] private bool autoCombatOnStart;

        private readonly Dictionary<TargetPriorityMode, ITargetSelector> _selectors = new()
        {
            { TargetPriorityMode.Nearest, new NearestTargetSelector() },
            { TargetPriorityMode.ElitePriority, new ElitePriorityTargetSelector() },
            { TargetPriorityMode.LowestHp, new LowestHpTargetSelector() },
        };

        public AutoCombatSettings Settings => settings;
        public bool IsAutoEnabled { get; private set; }
        public MonsterController Target { get; private set; }
        public bool HasValidTarget => Target != null && Target.IsAlive;

        /// <summary>자동 전투 기준점 (켠 위치 / 직접 이동을 마친 위치)</summary>
        public Vector3 Anchor { get; set; }

        /// <summary>수동으로 누른 스킬 (사거리에 들어가면 사용)</summary>
        public SkillSlot QueuedSkill { get; set; }

        // Observer
        public event Action<bool> OnAutoCombatChanged;
        public event Action<MonsterController> OnTargetChanged;

        private void Start()
        {
            if (autoCombatOnStart) SetAutoCombat(true);
        }

        public void SetAutoCombat(bool enabled)
        {
            if (IsAutoEnabled == enabled) return;
            IsAutoEnabled = enabled;
            if (enabled) Anchor = transform.position;
            OnAutoCombatChanged?.Invoke(enabled);
        }

        public void ToggleAutoCombat() => SetAutoCombat(!IsAutoEnabled);

        public void SetTarget(MonsterController target)
        {
            if (Target == target) return;
            Target = target;
            OnTargetChanged?.Invoke(target);
        }

        public void SetPriorityMode(TargetPriorityMode mode) => settings.priorityMode = mode;

        /// <summary>현재 탐지 설정·우선순위 전략으로 대상 검색</summary>
        public MonsterController FindTarget(Vector3 origin, bool useLeash)
        {
            var query = new TargetQuery(origin, settings.detectRadius, settings.targetNormal, settings.targetElite,
                                        useLeash, Anchor, settings.returnDistance, settings.targetBoss);
            return _selectors[settings.priorityMode].Select(query, MonsterController.Active);
        }
    }
}
