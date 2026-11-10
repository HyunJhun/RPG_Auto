using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>분해 보상표: 장비 등급·강화 수치에 따라 재료(강화 주문서)를 돌려준다</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Items/Disassemble Table", fileName = "DisassembleTable")]
    public class DisassembleTable : ScriptableObject
    {
        [SerializeField] private ItemData rewardItem;
        [Tooltip("등급별 기본 보상 수량 (일반, 고급, 희귀, 영웅, 전설)")]
        [SerializeField] private int[] baseByGrade = { 1, 2, 4, 8, 16 };
        [Tooltip("강화 1단계당 추가 보상")]
        [SerializeField] private int perEnhanceLevel = 1;

        public ItemData RewardItem => rewardItem;

        public int RewardCount(EquipmentInstance equipment)
        {
            if (equipment == null) return 0;
            int g = Mathf.Clamp((int)equipment.Data.Grade, 0, baseByGrade.Length - 1);
            return baseByGrade[g] + perEnhanceLevel * equipment.EnhanceLevel;
        }
    }

    public enum DisassembleResult
    {
        Ok,
        Invalid,
        Locked,
        Equipped
    }

    /// <summary>장비 분해 규칙 (UI와 분리된 순수 로직)</summary>
    public static class DisassembleService
    {
        public static DisassembleResult Check(InventoryEntry entry, PlayerInventory inventory, PlayerEquipment equipment)
        {
            if (entry == null || !entry.IsEquipment || !inventory.HasEntry(entry)) return DisassembleResult.Invalid;
            if (entry.IsLocked) return DisassembleResult.Locked;
            if (equipment != null && equipment.IsEquipped(entry.Equipment)) return DisassembleResult.Equipped;
            return DisassembleResult.Ok;
        }

        /// <summary>분해 실행. 장비를 없애고 보상을 지급한다 (장비 칸이 비므로 칸 부족은 생기지 않는다)</summary>
        public static DisassembleResult Disassemble(InventoryEntry entry, PlayerInventory inventory, PlayerEquipment equipment,
            DisassembleTable table, out int rewardCount)
        {
            rewardCount = 0;
            var check = Check(entry, inventory, equipment);
            if (check != DisassembleResult.Ok || table == null || table.RewardItem == null)
                return check != DisassembleResult.Ok ? check : DisassembleResult.Invalid;

            rewardCount = table.RewardCount(entry.Equipment);
            inventory.RemoveEquipment(entry.Equipment);
            if (rewardCount > 0) inventory.ForceAdd(table.RewardItem, rewardCount);
            return DisassembleResult.Ok;
        }

        public static string ToMessage(DisassembleResult result) => result switch
        {
            DisassembleResult.Locked => "잠긴 아이템은 분해할 수 없습니다",
            DisassembleResult.Equipped => "장착을 해제한 뒤 분해하세요",
            DisassembleResult.Ok => string.Empty,
            _ => "분해할 수 없습니다"
        };
    }
}
