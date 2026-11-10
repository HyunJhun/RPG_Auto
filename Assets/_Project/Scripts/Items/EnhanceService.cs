using UnityEngine;

namespace RPGAuto.Items
{
    public enum EnhanceResult
    {
        Success,
        Failed,
        FailedDowngrade,
        MaxLevel,
        NotEnoughGold,
        NotEnoughMaterial,
        Invalid
    }

    /// <summary>장비 강화 규칙 (UI와 분리된 순수 로직). 비용은 시도할 때 차감된다.</summary>
    public static class EnhanceService
    {
        public static int MaxLevelOf(EquipmentInstance eq, EnhanceTable table) =>
            eq == null || table == null ? 0 : Mathf.Min(eq.Data.MaxEnhance, table.MaxLevel);

        /// <summary>시도 가능 여부 (비용/최대치 검사만, 확률 판정 전)</summary>
        public static EnhanceResult Check(EquipmentInstance eq, EnhanceTable table, PlayerWallet wallet, PlayerInventory inventory)
        {
            if (eq == null || table == null) return EnhanceResult.Invalid;
            if (eq.EnhanceLevel >= MaxLevelOf(eq, table) || !table.TryGetStep(eq.EnhanceLevel, out var step)) return EnhanceResult.MaxLevel;
            if (!wallet.CanAfford(step.goldCost)) return EnhanceResult.NotEnoughGold;
            if (step.scrollCost > 0 && inventory.GetCount(table.ScrollItem) < step.scrollCost) return EnhanceResult.NotEnoughMaterial;
            return EnhanceResult.Success;
        }

        public static EnhanceResult TryEnhance(EquipmentInstance eq, EnhanceTable table, PlayerWallet wallet, PlayerInventory inventory)
        {
            var check = Check(eq, table, wallet, inventory);
            if (check != EnhanceResult.Success) return check;

            table.TryGetStep(eq.EnhanceLevel, out var step);
            wallet.TrySpend(step.goldCost);
            if (step.scrollCost > 0) inventory.RemoveItem(table.ScrollItem, step.scrollCost);

            if (Random.value < step.successRate)
            {
                eq.SetEnhanceLevel(eq.EnhanceLevel + 1);
                return EnhanceResult.Success;
            }
            if (step.downgradeOnFail && eq.EnhanceLevel > 0)
            {
                eq.SetEnhanceLevel(eq.EnhanceLevel - 1);
                return EnhanceResult.FailedDowngrade;
            }
            return EnhanceResult.Failed;
        }

        public static string ToMessage(EnhanceResult result, EquipmentInstance eq) => result switch
        {
            EnhanceResult.Success => $"강화 성공! {eq.DisplayName}",
            EnhanceResult.Failed => "강화 실패",
            EnhanceResult.FailedDowngrade => $"강화 실패... +{eq.EnhanceLevel}(으)로 하락",
            EnhanceResult.MaxLevel => "이미 최대 강화 단계입니다",
            EnhanceResult.NotEnoughGold => "골드가 부족합니다",
            EnhanceResult.NotEnoughMaterial => "강화 주문서가 부족합니다",
            _ => "강화할 수 없습니다"
        };
    }
}
