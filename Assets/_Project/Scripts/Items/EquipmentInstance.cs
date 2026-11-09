using System;
using RPGAuto.Data;

namespace RPGAuto.Items
{
    /// <summary>인벤토리에 들어 있는 장비 1개 (강화 수치는 개체마다 다르다)</summary>
    public class EquipmentInstance
    {
        private static int s_nextId = 1;

        public int Id { get; }
        public EquipmentData Data { get; }
        public int EnhanceLevel { get; private set; }

        /// <summary>강화 수치 변경 알림 (Observer) — 장착 중이면 능력치 재계산</summary>
        public event Action<EquipmentInstance> OnChanged;

        public EquipmentInstance(EquipmentData data, int enhanceLevel = 0)
        {
            Id = s_nextId++;
            Data = data;
            EnhanceLevel = Math.Clamp(enhanceLevel, 0, data.MaxEnhance);
        }

        public StatBlock Stats => Data.GetStats(EnhanceLevel);
        public bool IsMaxEnhance => EnhanceLevel >= Data.MaxEnhance;
        public string DisplayName => EnhanceLevel > 0 ? $"+{EnhanceLevel} {Data.DisplayName}" : Data.DisplayName;

        internal void SetEnhanceLevel(int level)
        {
            level = Math.Clamp(level, 0, Data.MaxEnhance);
            if (level == EnhanceLevel) return;
            EnhanceLevel = level;
            OnChanged?.Invoke(this);
        }
    }
}
