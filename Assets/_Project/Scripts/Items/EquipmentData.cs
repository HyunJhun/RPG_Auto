using RPGAuto.Data;
using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>장비 부위 (반지는 2개까지 장착)</summary>
    public enum EquipSlotType
    {
        Weapon,
        Helmet,
        Armor,
        Cloak,
        Gloves,
        Pants,
        Boots,
        Belt,
        Necklace,
        Earring,
        Ring
    }

    /// <summary>장비 데이터. 인벤토리에서는 EquipmentInstance(강화 수치 보유)로 1개 = 1칸을 차지한다.</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Items/Equipment", fileName = "EQ_NewEquipment")]
    public class EquipmentData : ItemData
    {
        [Header("Equipment")]
        [SerializeField] private EquipSlotType slotType = EquipSlotType.Weapon;
        [SerializeField, Min(1)] private int requiredLevel = 1;
        [Tooltip("장착 시 더해지는 기본 능력치")]
        [SerializeField] private StatBlock stats;
        [Tooltip("강화 1단계당 기본 능력치 증가 비율 (0.1 = +10%)")]
        [SerializeField] private float enhanceBonusPerLevel = 0.1f;
        [SerializeField, Min(0)] private int maxEnhance = 10;

        public EquipSlotType SlotType => slotType;
        public int RequiredLevel => requiredLevel;
        public StatBlock Stats => stats;
        public float EnhanceBonusPerLevel => enhanceBonusPerLevel;
        public int MaxEnhance => maxEnhance;
        public override bool IsStackable => false;

        /// <summary>장신구 (인벤토리 필터 구분용)</summary>
        public bool IsAccessory => slotType is EquipSlotType.Necklace or EquipSlotType.Earring
                                              or EquipSlotType.Ring or EquipSlotType.Belt;

        /// <summary>강화 단계를 반영한 능력치</summary>
        public StatBlock GetStats(int enhanceLevel) => stats.Scaled(1f + enhanceLevel * enhanceBonusPerLevel);
    }
}
