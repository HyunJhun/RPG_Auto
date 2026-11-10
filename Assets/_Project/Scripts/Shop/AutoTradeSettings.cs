using System;
using System.Collections.Generic;
using RPGAuto.Items;
using UnityEngine;

namespace RPGAuto.Shop
{
    /// <summary>자동 담기 분류 (인벤토리 필터와 같은 구성)</summary>
    public enum TradeCategory
    {
        Equipment,
        Accessory,
        Consumable,
        Material
    }

    /// <summary>분류 하나의 자동 판매 규칙: 켜기/끄기 + 판매할 등급(비트마스크)</summary>
    [Serializable]
    public class SellRule
    {
        public bool enabled;
        [Tooltip("비트 0 = 일반, 1 = 고급, 2 = 희귀, 3 = 영웅, 4 = 전설")]
        public int gradeMask;

        public bool Includes(ItemGrade grade) => (gradeMask & (1 << (int)grade)) != 0;
        public void Toggle(ItemGrade grade) => gradeMask ^= 1 << (int)grade;
    }

    /// <summary>상품 하나의 일괄 구매 목표 수량</summary>
    [Serializable]
    public class BuyTarget
    {
        public string itemId;
        public int count;
    }

    /// <summary>
    /// 상점 자동 거래 설정. 상점의 [자동 담기]·[일괄 구매]와 보조 모드가 같은 설정을 쓴다.
    /// </summary>
    [Serializable]
    public class AutoTradeSettings
    {
        public const int GradeCount = 5;

        public SellRule[] sellRules =
        {
            new() { enabled = false, gradeMask = MaskUpTo(ItemGrade.Common) },  // 장비
            new() { enabled = false, gradeMask = MaskUpTo(ItemGrade.Common) },  // 장신구
            new() { enabled = false, gradeMask = MaskUpTo(ItemGrade.Common) },  // 소모품
            new() { enabled = true, gradeMask = MaskUpTo(ItemGrade.Rare) }      // 기타 (전리품)
        };

        [Tooltip("강화한 장비는 판매하지 않음")]
        public bool keepEnhanced = true;
        [Tooltip("상점에서 파는 아이템(물약·주문서 등)은 판매하지 않음")]
        public bool keepShopProducts = true;

        [Tooltip("상품별 구매 목표 (없으면 상점 기본값 사용)")]
        public List<BuyTarget> buyTargets = new();

        public SellRule Rule(TradeCategory category)
        {
            int i = (int)category;
            if (sellRules == null || sellRules.Length != 4) sellRules = new AutoTradeSettings().sellRules;
            return sellRules[i];
        }

        // ---------------- 등급 마스크 ----------------

        /// <summary>해당 등급 이하 전부</summary>
        public static int MaskUpTo(ItemGrade grade) => (1 << ((int)grade + 1)) - 1;

        /// <summary>마스크가 '어떤 등급 이하'와 정확히 같으면 그 등급 (아니면 null)</summary>
        public static ItemGrade? UpToGrade(int mask)
        {
            for (int g = 0; g < GradeCount; g++)
                if (mask == MaskUpTo((ItemGrade)g)) return (ItemGrade)g;
            return null;
        }

        // ---------------- 판매 ----------------

        public static TradeCategory CategoryOf(ItemData item)
        {
            if (item is EquipmentData eq) return eq.IsAccessory ? TradeCategory.Accessory : TradeCategory.Equipment;
            return item.Category == ItemCategory.Consumable ? TradeCategory.Consumable : TradeCategory.Material;
        }

        /// <summary>이 칸을 자동 판매 대상으로 담을지</summary>
        public bool ShouldSell(InventoryEntry entry, PlayerEquipment equipment, ShopData shop)
        {
            if (entry == null || entry.IsLocked || !entry.Item.IsSellable) return false;
            if (entry.IsEquipment)
            {
                if (equipment != null && equipment.IsEquipped(entry.Equipment)) return false;
                if (keepEnhanced && entry.Equipment.EnhanceLevel > 0) return false;
            }
            if (keepShopProducts && shop != null && Sells(shop, entry.Item)) return false;

            var rule = Rule(CategoryOf(entry.Item));
            return rule.enabled && rule.Includes(entry.Item.Grade);
        }

        private static bool Sells(ShopData shop, ItemData item)
        {
            foreach (var p in shop.Products) if (p.item == item) return true;
            return false;
        }

        // ---------------- 구매 ----------------

        public int GetBuyTarget(in ShopData.Product product)
        {
            if (product.item == null) return 0;
            foreach (var t in buyTargets)
                if (t.itemId == product.item.ItemId) return t.count;
            return Mathf.Max(0, product.restockTarget);
        }

        public void SetBuyTarget(ItemData item, int count)
        {
            if (item == null) return;
            count = Mathf.Max(0, count);
            foreach (var t in buyTargets)
                if (t.itemId == item.ItemId) { t.count = count; return; }
            buyTargets.Add(new BuyTarget { itemId = item.ItemId, count = count });
        }
    }

    /// <summary>자동 거래 설정으로 판매·구매 목록을 만든다 (상점 UI·보조 모드 공용)</summary>
    public static class AutoTradeService
    {
        public static List<SellLine> SelectSell(AutoTradeSettings settings, PlayerInventory inventory, PlayerEquipment equipment, ShopData shop)
        {
            var lines = new List<SellLine>();
            if (settings == null || inventory == null) return lines;
            foreach (var e in inventory.Entries)
                if (settings.ShouldSell(e, equipment, shop))
                    lines.Add(new SellLine { Entry = e, Quantity = e.Count });
            return lines;
        }

        /// <summary>목표 수량까지 더 사야 할 개수</summary>
        public static int NeedToBuy(AutoTradeSettings settings, in ShopData.Product product, PlayerInventory inventory)
        {
            if (product.item == null) return 0;
            int target = settings != null ? settings.GetBuyTarget(product) : product.restockTarget;
            return Mathf.Max(0, target - inventory.GetCount(product.item));
        }
    }
}
