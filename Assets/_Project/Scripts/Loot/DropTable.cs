using System;
using System.Collections.Generic;
using RPGAuto.Items;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RPGAuto.Loot
{
    /// <summary>드랍 결과 한 건 (골드 또는 아이템 묶음)</summary>
    public readonly struct LootEntry
    {
        public readonly ItemData Item;
        public readonly int Count;
        public readonly long Gold;

        private LootEntry(ItemData item, int count, long gold)
        {
            Item = item;
            Count = count;
            Gold = gold;
        }

        public bool IsGold => Item == null;
        public static LootEntry OfGold(long amount) => new(null, 0, amount);
        public static LootEntry OfItem(ItemData item, int count) => new(item, count, 0);

        public override string ToString() => IsGold ? $"골드 +{Gold:N0}" : $"{Item.DisplayName} x{Count}";
    }

    /// <summary>몬스터 드랍 테이블</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Loot/Drop Table", fileName = "DT_New")]
    public class DropTable : ScriptableObject
    {
        [Serializable]
        public struct ItemDrop
        {
            public ItemData item;
            [Range(0f, 1f)] public float chance;
            public int minCount;
            public int maxCount;
        }

        [Header("Gold")]
        [SerializeField, Range(0f, 1f)] private float goldChance = 1f;
        [SerializeField] private int goldMin = 1;
        [SerializeField] private int goldMax = 5;

        [Header("Items")]
        [SerializeField] private List<ItemDrop> items = new();

        public IReadOnlyList<ItemDrop> Items => items;

        /// <summary>확률 판정 후 결과를 results에 추가한다 (리스트는 호출 측에서 재사용)</summary>
        public void Roll(List<LootEntry> results)
        {
            if (goldMax > 0 && Random.value < goldChance)
                results.Add(LootEntry.OfGold(Random.Range(goldMin, goldMax + 1)));

            foreach (var d in items)
            {
                if (d.item == null || Random.value >= d.chance) continue;
                int count = Random.Range(Mathf.Max(1, d.minCount), Mathf.Max(1, d.maxCount) + 1);
                results.Add(LootEntry.OfItem(d.item, count));
            }
        }
    }
}
