using UnityEngine;

namespace RPGAuto.Items
{
    public enum ItemCategory
    {
        /// <summary>사용 가능 (물약 등)</summary>
        Consumable,
        /// <summary>재료/전리품 (사용 불가, 상점 판매·강화 재료 등)</summary>
        Material,
        /// <summary>장비 (EquipmentData)</summary>
        Equipment
    }

    /// <summary>아이템 등급 (슬롯 배경색/이름 색)</summary>
    public enum ItemGrade
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum ConsumableEffect
    {
        RestoreHp,
        RestoreMp
    }

    /// <summary>아이템 데이터 (소모품/재료). 장비는 EquipmentData가 상속한다.</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Items/Item", fileName = "IT_NewItem")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string itemId = "I000";
        [SerializeField] private string displayName = "물약";
        [SerializeField] private string shortLabel = "물약";
        [SerializeField] private Color slotColor = new(0.8f, 0.2f, 0.2f);
        [SerializeField] private ItemCategory category = ItemCategory.Consumable;
        [SerializeField] private ItemGrade grade = ItemGrade.Common;
        [SerializeField, TextArea(2, 4)] private string description;
        [Tooltip("상점 판매 가격 (0 = 판매 불가)")]
        [SerializeField] private long sellPrice;
        [Tooltip("상점 [자동 담기] 대상 (팔기 위해 모으는 전리품)")]
        [SerializeField] private bool autoSell;
        [Tooltip("1개당 무게")]
        [SerializeField, Min(0f)] private float weight = 1f;

        [Header("Effect (Consumable)")]
        [SerializeField] private ConsumableEffect effect = ConsumableEffect.RestoreHp;
        [Tooltip("최대치 대비 회복 비율")]
        [SerializeField, Range(0f, 1f)] private float restorePercent = 0.2f;
        [SerializeField] private float restoreFlat;
        [SerializeField] private float cooldown = 1f;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string ShortLabel => shortLabel;
        public Color SlotColor => slotColor;
        public ItemCategory Category => category;
        public ItemGrade Grade => grade;
        public string Description => description;
        public bool IsUsable => category == ItemCategory.Consumable;
        public long SellPrice => sellPrice;
        public bool IsSellable => sellPrice > 0;
        public bool AutoSell => autoSell && sellPrice > 0;
        public float Weight => weight;
        /// <summary>한 칸에 여러 개 쌓이는지 (장비는 1개 = 1칸)</summary>
        public virtual bool IsStackable => true;
        public ConsumableEffect Effect => effect;
        public float RestorePercent => restorePercent;
        public float RestoreFlat => restoreFlat;
        public float Cooldown => cooldown;
    }

    /// <summary>등급별 표시 색상</summary>
    public static class ItemGradeColors
    {
        /// <summary>슬롯 배경 (어두운 톤)</summary>
        public static Color Background(ItemGrade grade) => grade switch
        {
            ItemGrade.Uncommon => new Color(0.17f, 0.3f, 0.17f, 1f),
            ItemGrade.Rare => new Color(0.14f, 0.24f, 0.42f, 1f),
            ItemGrade.Epic => new Color(0.3f, 0.16f, 0.42f, 1f),
            ItemGrade.Legendary => new Color(0.45f, 0.28f, 0.08f, 1f),
            _ => new Color(0.22f, 0.22f, 0.24f, 1f)
        };

        /// <summary>테두리/이름 (밝은 톤)</summary>
        public static Color Text(ItemGrade grade) => grade switch
        {
            ItemGrade.Uncommon => new Color(0.45f, 0.85f, 0.45f),
            ItemGrade.Rare => new Color(0.4f, 0.65f, 1f),
            ItemGrade.Epic => new Color(0.75f, 0.45f, 1f),
            ItemGrade.Legendary => new Color(1f, 0.65f, 0.2f),
            _ => new Color(0.85f, 0.85f, 0.85f)
        };
    }
}
