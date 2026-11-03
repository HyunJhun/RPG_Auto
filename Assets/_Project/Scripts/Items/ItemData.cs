using UnityEngine;

namespace RPGAuto.Items
{
    public enum ConsumableEffect
    {
        RestoreHp,
        RestoreMp
    }

    /// <summary>소모품(물약) 데이터</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Items/Consumable", fileName = "IT_NewPotion")]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string itemId = "I000";
        [SerializeField] private string displayName = "Potion";
        [SerializeField] private string shortLabel = "POT";
        [SerializeField] private Color slotColor = new(0.8f, 0.2f, 0.2f);

        [Header("Effect")]
        [SerializeField] private ConsumableEffect effect = ConsumableEffect.RestoreHp;
        [Tooltip("최대치 대비 회복 비율")]
        [SerializeField, Range(0f, 1f)] private float restorePercent = 0.2f;
        [SerializeField] private float restoreFlat;
        [SerializeField] private float cooldown = 1f;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string ShortLabel => shortLabel;
        public Color SlotColor => slotColor;
        public ConsumableEffect Effect => effect;
        public float RestorePercent => restorePercent;
        public float RestoreFlat => restoreFlat;
        public float Cooldown => cooldown;
    }
}
