using RPGAuto.Combat;
using RPGAuto.Items;
using RPGAuto.Player;
using RPGAuto.Skills;
using UnityEngine;

namespace RPGAuto.QuickSlots
{
    /// <summary>
    /// 퀵슬롯에 장착되는 행동 (Strategy Pattern)
    /// 스킬/물약 등 종류에 따라 사용·자동 사용 방식이 달라진다.
    /// </summary>
    public interface IQuickSlotAction
    {
        string Label { get; }
        Color Color { get; }
        float CooldownRemaining { get; }
        float CooldownNormalized { get; }
        /// <summary>수량 표시 (스킬은 null)</summary>
        string CountText { get; }

        /// <summary>퀵슬롯 자동 사용 ON/OFF가 바뀌었을 때</summary>
        void OnAutoUseChanged(bool enabled);

        /// <summary>수동 사용 (탭/단축키)</summary>
        void Activate(PlayerController player);

        /// <summary>자동 사용 판단 (자동 ON일 때 매 프레임 호출)</summary>
        void TickAuto(PlayerController player);
    }

    /// <summary>스킬 퀵슬롯: 자동 사용은 전투 상태(공격/자기 스킬)에서 SkillSlot.AutoUse를 보고 판단</summary>
    public class SkillQuickAction : IQuickSlotAction
    {
        public SkillSlot Skill { get; }
        public SkillQuickAction(SkillSlot skill) => Skill = skill;

        public string Label => Skill.Data.ShortLabel;
        public Color Color => Skill.Data.SlotColor;
        public float CooldownRemaining => Skill.CooldownRemaining;
        public float CooldownNormalized => Skill.CooldownNormalized;
        public string CountText => null;

        public void OnAutoUseChanged(bool enabled) => Skill.SetAutoUse(enabled);
        public void Activate(PlayerController player) => player.RequestSkill(Skill);
        public void TickAuto(PlayerController player) { }
    }

    /// <summary>물약 퀵슬롯: 자동 ON이면 HP/MP가 설정 비율 이하일 때 사용 (자동 전투 여부와 무관)</summary>
    public class ItemQuickAction : IQuickSlotAction
    {
        private readonly PlayerInventory _inventory;
        public ItemData Item { get; }

        public ItemQuickAction(ItemData item, PlayerInventory inventory)
        {
            Item = item;
            _inventory = inventory;
        }

        public string Label => Item.ShortLabel;
        public Color Color => Item.SlotColor;
        public float CooldownRemaining => _inventory.GetCooldownRemaining(Item);
        public float CooldownNormalized => Item.Cooldown > 0f ? CooldownRemaining / Item.Cooldown : 0f;
        public string CountText => _inventory.GetCount(Item).ToString();

        public void OnAutoUseChanged(bool enabled) { }
        public void Activate(PlayerController player) => _inventory.TryUse(Item);

        public void TickAuto(PlayerController player)
        {
            if (player.IsDead || !_inventory.CanUse(Item)) return;

            var stats = player.Stats;
            var settings = player.Combat.Settings;
            bool need = Item.Effect switch
            {
                ConsumableEffect.RestoreHp => stats.HpPercent * 100f <= settings.hpPotionPercent,
                ConsumableEffect.RestoreMp => stats.MpPercent * 100f <= settings.mpPotionPercent,
                _ => false
            };
            if (need) _inventory.TryUse(Item);
        }
    }
}
