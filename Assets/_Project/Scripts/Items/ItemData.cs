using UnityEngine;

namespace RPGAuto.Items
{
    public enum ItemCategory
    {
        /// <summary>사용 가능 (물약 등)</summary>
        Consumable,
        /// <summary>재료/전리품 (사용 불가, 이후 상점 판매·제작용)</summary>
        Material
    }

    public enum ConsumableEffect
    {
        RestoreHp,
        RestoreMp
    }

    /// <summary>아이템 데이터 (소모품/재료)</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Items/Item", fileName = "IT_NewItem")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string itemId = "I000";
        [SerializeField] private string displayName = "Potion";
        [SerializeField] private string shortLabel = "POT";
        [SerializeField] private Color slotColor = new(0.8f, 0.2f, 0.2f);
        [SerializeField] private ItemCategory category = ItemCategory.Consumable;
        [SerializeField, TextArea(2, 4)] private string description;
        [Tooltip("상점 판매 가격 (0 = 판매 불가)")]
        [SerializeField] private long sellPrice;

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
        public string Description => description;
        public bool IsUsable => category == ItemCategory.Consumable;
        public long SellPrice => sellPrice;
        public bool IsSellable => sellPrice > 0;
        public ConsumableEffect Effect => effect;
        public float RestorePercent => restorePercent;
        public float RestoreFlat => restoreFlat;
        public float Cooldown => cooldown;
    }
}
