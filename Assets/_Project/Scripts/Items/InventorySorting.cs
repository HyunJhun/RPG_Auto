using System.Collections.Generic;

namespace RPGAuto.Items
{
    public enum InventorySortMode
    {
        /// <summary>종류순: 장비 → 장신구 → 소모품 → 기타, 같은 종류는 부위 · 등급 · 강화 · 레벨 순</summary>
        Category,
        /// <summary>등급순: 높은 등급 먼저, 같은 등급은 종류순</summary>
        Grade,
        /// <summary>무게순: 칸 전체 무게가 무거운 것 먼저 (무게 정리용)</summary>
        Weight
    }

    /// <summary>
    /// 인벤토리 정렬 기준 (Strategy: IComparer 교체).
    /// 공통 마지막 기준은 아이템 ID라 같은 아이템끼리 모인다.
    /// </summary>
    public static class InventorySorters
    {
        public static readonly InventorySortMode[] All = { InventorySortMode.Category, InventorySortMode.Grade, InventorySortMode.Weight };

        public static IComparer<InventoryEntry> Get(InventorySortMode mode) => mode switch
        {
            InventorySortMode.Grade => GradeFirst.Instance,
            InventorySortMode.Weight => WeightFirst.Instance,
            _ => CategoryFirst.Instance
        };

        public static string Label(InventorySortMode mode) => mode switch
        {
            InventorySortMode.Grade => "등급순",
            InventorySortMode.Weight => "무게순",
            _ => "종류순"
        };

        /// <summary>종류 순위: 장비 0, 장신구 1, 소모품 2, 기타 3</summary>
        public static int CategoryRank(ItemData item)
        {
            if (item is EquipmentData eq) return eq.IsAccessory ? 1 : 0;
            return item.Category == ItemCategory.Consumable ? 2 : 3;
        }

        private static int SlotRank(ItemData item) => item is EquipmentData eq ? (int)eq.SlotType : 0;
        private static int Enhance(InventoryEntry e) => e.Equipment != null ? e.Equipment.EnhanceLevel : 0;
        private static int Level(ItemData item) => item is EquipmentData eq ? eq.RequiredLevel : 0;

        /// <summary>같은 종류 안에서의 세부 순서: 부위 → 등급(높은 순) → 강화(높은 순) → 레벨(높은 순) → ID</summary>
        private static int CompareDetail(InventoryEntry a, InventoryEntry b)
        {
            int c = SlotRank(a.Item).CompareTo(SlotRank(b.Item));
            if (c != 0) return c;
            c = b.Item.Grade.CompareTo(a.Item.Grade);
            if (c != 0) return c;
            c = Enhance(b).CompareTo(Enhance(a));
            if (c != 0) return c;
            c = Level(b.Item).CompareTo(Level(a.Item));
            if (c != 0) return c;
            return string.CompareOrdinal(a.Item.ItemId, b.Item.ItemId);
        }

        private sealed class CategoryFirst : IComparer<InventoryEntry>
        {
            public static readonly CategoryFirst Instance = new();
            public int Compare(InventoryEntry a, InventoryEntry b)
            {
                int c = CategoryRank(a.Item).CompareTo(CategoryRank(b.Item));
                return c != 0 ? c : CompareDetail(a, b);
            }
        }

        private sealed class GradeFirst : IComparer<InventoryEntry>
        {
            public static readonly GradeFirst Instance = new();
            public int Compare(InventoryEntry a, InventoryEntry b)
            {
                int c = b.Item.Grade.CompareTo(a.Item.Grade);
                if (c != 0) return c;
                c = CategoryRank(a.Item).CompareTo(CategoryRank(b.Item));
                return c != 0 ? c : CompareDetail(a, b);
            }
        }

        private sealed class WeightFirst : IComparer<InventoryEntry>
        {
            public static readonly WeightFirst Instance = new();
            public int Compare(InventoryEntry a, InventoryEntry b)
            {
                int c = b.Weight.CompareTo(a.Weight);
                if (c != 0) return c;
                return CategoryFirst.Instance.Compare(a, b);
            }
        }
    }
}
