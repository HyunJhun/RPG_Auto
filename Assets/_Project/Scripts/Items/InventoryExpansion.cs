using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>인벤토리 칸/무게 확장 비용 설정</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Items/Inventory Expansion Config", fileName = "INV_ExpansionConfig")]
    public class InventoryExpansionConfig : ScriptableObject
    {
        [Header("Slots")]
        [SerializeField, Min(1)] private int maxCapacity = 200;
        [SerializeField, Min(0)] private long goldPerSlot = 500;

        [Header("Weight")]
        [Tooltip("무게 확장 단위 (1단계당 증가량)")]
        [SerializeField, Min(1f)] private float weightStep = 100f;
        [SerializeField, Min(1f)] private float maxWeight = 10000f;
        [SerializeField, Min(0)] private long goldPerWeightStep = 1000;

        public int MaxCapacity => maxCapacity;
        public long GoldPerSlot => goldPerSlot;
        public float WeightStep => weightStep;
        public float MaxWeight => maxWeight;
        public long GoldPerWeightStep => goldPerWeightStep;

        public int MaxAddSlots(PlayerInventory inv) => Mathf.Max(0, maxCapacity - inv.Capacity);
        public int MaxAddWeightSteps(PlayerInventory inv) => Mathf.Max(0, Mathf.FloorToInt((maxWeight - inv.MaxWeight) / weightStep));
        public long Cost(int addSlots, int addWeightSteps) => addSlots * goldPerSlot + addWeightSteps * goldPerWeightStep;
    }

    public enum ExpansionResult
    {
        Ok,
        Nothing,
        OverLimit,
        NotEnoughGold
    }

    /// <summary>확장 규칙 (UI와 분리)</summary>
    public static class InventoryExpansionService
    {
        public static ExpansionResult Expand(InventoryExpansionConfig config, PlayerInventory inventory, PlayerWallet wallet,
                                             int addSlots, int addWeightSteps)
        {
            if (addSlots <= 0 && addWeightSteps <= 0) return ExpansionResult.Nothing;
            if (addSlots < 0 || addWeightSteps < 0 ||
                addSlots > config.MaxAddSlots(inventory) || addWeightSteps > config.MaxAddWeightSteps(inventory))
                return ExpansionResult.OverLimit;

            long cost = config.Cost(addSlots, addWeightSteps);
            if (!wallet.TrySpend(cost)) return ExpansionResult.NotEnoughGold;

            inventory.ExpandSlots(addSlots);
            inventory.ExpandWeight(addWeightSteps * config.WeightStep);
            return ExpansionResult.Ok;
        }
    }
}
