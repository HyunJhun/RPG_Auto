using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Combat.Targeting;
using RPGAuto.Monster;
using UnityEngine;

namespace RPGAuto.Skills
{
    [RequireComponent(typeof(CombatStats))]
    public class PlayerSkillController : MonoBehaviour
    {
        [SerializeField] private SkillData basicAttack;
        [SerializeField] private List<SkillData> skills = new();

        private const float RangeTolerance = 0.3f;

        private readonly List<SkillSlot> _slots = new();
        private CombatStats _stats;
        private float _actionLock;

        public SkillSlot BasicAttack { get; private set; }
        public IReadOnlyList<SkillSlot> Slots => _slots;
        public bool IsActionLocked => _actionLock > 0f;

        /// <summary>스킬 사용 알림 (Observer) — 이펙트/사운드/애니메이션 연동용</summary>
        public event Action<SkillSlot, MonsterController> OnSkillUsed;

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
            if (basicAttack != null) BasicAttack = new SkillSlot(basicAttack);
            foreach (var data in skills)
                if (data != null) _slots.Add(new SkillSlot(data));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_actionLock > 0f) _actionLock -= dt;
            BasicAttack?.Tick(dt);
            for (int i = 0; i < _slots.Count; i++) _slots[i].Tick(dt);
        }

        public SkillSlot GetSlot(int index) => index >= 0 && index < _slots.Count ? _slots[index] : null;

        public bool CanUse(SkillSlot slot, MonsterController target)
        {
            if (slot == null || !slot.IsReady || IsActionLocked || _stats.IsDead) return false;
            if (_stats.Mp < slot.Data.MpCost) return false;

            if (slot.Data.RequiresTarget)
            {
                if (target == null || !target.IsAlive) return false;
                float dist = TargetQuery.FlatDistance(transform.position, target.transform.position);
                if (dist > slot.Data.Range + RangeTolerance) return false;
            }
            return true;
        }

        public bool TryUse(SkillSlot slot, MonsterController target)
        {
            if (!CanUse(slot, target)) return false;
            if (!_stats.TryConsumeMp(slot.Data.MpCost)) return false;

            if (slot.Data.Effect != null)
                slot.Data.Effect.Execute(new SkillContext(_stats, transform, target, slot.Data));

            slot.MarkUsed();
            _actionLock = slot.Data.ActionLock;
            OnSkillUsed?.Invoke(slot, target);
            return true;
        }

        /// <summary>자동 사용 조건(사거리 제외) 통과 여부</summary>
        public bool PassesAutoConditions(SkillSlot slot, float mpLimitPercent)
        {
            var c = slot.AutoUse;
            if (!c.autoUse) return false;

            if (c.applyMpLimit && _stats.MpPercent * 100f <= mpLimitPercent) return false;
            if (c.useHpMin && _stats.HpPercent * 100f < c.hpMinPercent) return false;
            if (c.useHpMax && _stats.HpPercent * 100f > c.hpMaxPercent) return false;

            if (c.useCustomCycle)
            {
                float cycle = Mathf.Max(c.cycleSeconds, slot.Data.Cooldown);
                if (Time.time - slot.LastUsedTime < cycle) return false;
            }
            return true;
        }

        /// <summary>대상 지정 스킬 중 지금 자동으로 쓸 스킬 (슬롯 순서 = 우선순위)</summary>
        public SkillSlot SelectAutoSkill(MonsterController target, float mpLimitPercent)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.Data.RequiresTarget) continue;
                if (PassesAutoConditions(slot, mpLimitPercent) && CanUse(slot, target)) return slot;
            }
            return null;
        }

        /// <summary>자기 대상 스킬(회복/버프) 자동 사용</summary>
        public bool TryAutoUseSelfSkills(float mpLimitPercent)
        {
            if (IsActionLocked) return false;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Data.RequiresTarget) continue;
                if (PassesAutoConditions(slot, mpLimitPercent) && TryUse(slot, null)) return true;
            }
            return false;
        }

        /// <summary>
        /// 자동 전투 시 접근할 거리. '사거리 기준'이 꺼진 스킬이 사용 가능하면 그 사거리까지 접근한다.
        /// </summary>
        public float GetAutoEngageRange(float mpLimitPercent)
        {
            float range = BasicAttack != null ? BasicAttack.Data.Range : 2f;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.Data.RequiresTarget || slot.AutoUse.rangeFirst) continue;
                if (!slot.IsReady || _stats.Mp < slot.Data.MpCost) continue;
                if (!PassesAutoConditions(slot, mpLimitPercent)) continue;
                range = Mathf.Max(range, slot.Data.Range);
            }
            return range;
        }
    }
}
