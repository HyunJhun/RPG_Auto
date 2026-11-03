using System;
using UnityEngine;

namespace RPGAuto.Skills
{
    /// <summary>런타임 스킬 슬롯 (쿨타임, 슬롯별 자동 사용 설정)</summary>
    public class SkillSlot
    {
        public SkillData Data { get; }
        public AutoUseCondition AutoUse { get; }
        public float CooldownRemaining { get; private set; }
        public float LastUsedTime { get; private set; } = float.NegativeInfinity;

        public bool IsReady => CooldownRemaining <= 0f;
        public float CooldownNormalized => Data.Cooldown > 0f ? CooldownRemaining / Data.Cooldown : 0f;

        // Observer
        public event Action<SkillSlot> OnUsed;
        public event Action<SkillSlot> OnAutoUseChanged;

        public SkillSlot(SkillData data)
        {
            Data = data;
            AutoUse = data.DefaultAutoUse != null ? data.DefaultAutoUse.Clone() : new AutoUseCondition();
        }

        public void Tick(float deltaTime)
        {
            if (CooldownRemaining > 0f) CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
        }

        internal void MarkUsed()
        {
            CooldownRemaining = Data.Cooldown;
            LastUsedTime = Time.time;
            OnUsed?.Invoke(this);
        }

        public void SetAutoUse(bool enabled)
        {
            if (AutoUse.autoUse == enabled) return;
            AutoUse.autoUse = enabled;
            OnAutoUseChanged?.Invoke(this);
        }
    }
}
