using System;
using System.Collections.Generic;
using RPGAuto.Items;

namespace RPGAuto.Shop
{
    public enum ShopResult
    {
        Ok,
        Invalid,
        Nothing,
        NotEnoughGold,
        InventoryFull,
        Overweight,
        NotEnoughItems,
        NotSellable,
        Equipped,
        Locked
    }

    /// <summary>구매 주문 한 줄</summary>
    public struct BuyLine
    {
        public ShopData.Product Product;
        public int Quantity;
    }

    /// <summary>판매 바구니 한 줄 (인벤토리 칸 기준)</summary>
    public struct SellLine
    {
        public InventoryEntry Entry;
        public int Quantity;
    }

    /// <summary>구매/판매 규칙 (UI와 분리된 순수 로직). 여러 줄을 한 번에 검사한 뒤 한꺼번에 처리한다.</summary>
    public static class ShopService
    {
        public const int MaxQuantity = 999;

        /// <summary>세금이 붙은 1개 구매 가격</summary>
        /// <remarks>float 세율(0.2f = 0.2000000476…)을 그대로 곱하면 올림 시 1G가 더 붙으므로 decimal로 계산</remarks>
        public static long UnitBuyPrice(in ShopData.Product product, float taxRate) =>
            (long)Math.Ceiling(product.price * (1m + (decimal)taxRate));

        public static int MaxAffordable(long unitPrice, PlayerWallet wallet) =>
            unitPrice <= 0 ? MaxQuantity : (int)Math.Clamp(wallet.Gold / unitPrice, 0, MaxQuantity);

        public static long TotalCost(IReadOnlyList<BuyLine> lines, float taxRate)
        {
            long total = 0;
            foreach (var l in lines) total += UnitBuyPrice(l.Product, taxRate) * l.Quantity;
            return total;
        }

        public static float TotalWeight(IReadOnlyList<BuyLine> lines)
        {
            float w = 0f;
            foreach (var l in lines) if (l.Product.item != null) w += l.Product.item.Weight * l.Quantity;
            return w;
        }

        public static ShopResult CheckBuy(IReadOnlyList<BuyLine> lines, float taxRate, PlayerWallet wallet, PlayerInventory inventory)
        {
            int slots = 0, count = 0;
            var newKinds = new HashSet<ItemData>();
            foreach (var l in lines)
            {
                if (l.Quantity <= 0) continue;
                if (l.Product.item == null || l.Product.price < 0) return ShopResult.Invalid;
                count += l.Quantity;
                if (!l.Product.item.IsStackable) slots += l.Quantity;
                else if (inventory.GetCount(l.Product.item) == 0 && newKinds.Add(l.Product.item)) slots++;
            }
            if (count == 0) return ShopResult.Nothing;
            if (inventory.IsOverweight) return ShopResult.Overweight;
            if (slots > inventory.FreeSlots) return ShopResult.InventoryFull;
            if (!wallet.CanAfford(TotalCost(lines, taxRate))) return ShopResult.NotEnoughGold;
            return ShopResult.Ok;
        }

        public static ShopResult Buy(IReadOnlyList<BuyLine> lines, float taxRate, PlayerWallet wallet, PlayerInventory inventory)
        {
            var check = CheckBuy(lines, taxRate, wallet, inventory);
            if (check != ShopResult.Ok) return check;

            wallet.TrySpend(TotalCost(lines, taxRate));
            foreach (var l in lines)
                if (l.Quantity > 0) inventory.ForceAdd(l.Product.item, l.Quantity);
            return ShopResult.Ok;
        }

        public static long TotalSell(IReadOnlyList<SellLine> lines)
        {
            long total = 0;
            foreach (var l in lines) if (l.Entry != null) total += l.Entry.Item.SellPrice * l.Quantity;
            return total;
        }

        public static float TotalWeight(IReadOnlyList<SellLine> lines)
        {
            float w = 0f;
            foreach (var l in lines) if (l.Entry != null) w += l.Entry.Item.Weight * l.Quantity;
            return w;
        }

        public static ShopResult CheckSell(in SellLine line, PlayerInventory inventory, PlayerEquipment equipment)
        {
            var e = line.Entry;
            if (e == null || line.Quantity <= 0) return ShopResult.Invalid;
            if (!e.Item.IsSellable) return ShopResult.NotSellable;
            if (e.IsLocked) return ShopResult.Locked;
            if (e.IsEquipment)
            {
                if (!inventory.Contains(e.Equipment)) return ShopResult.NotEnoughItems;
                if (equipment != null && equipment.IsEquipped(e.Equipment)) return ShopResult.Equipped;
            }
            else if (inventory.GetCount(e.Item) < line.Quantity) return ShopResult.NotEnoughItems;
            return ShopResult.Ok;
        }

        public static ShopResult Sell(IReadOnlyList<SellLine> lines, PlayerWallet wallet, PlayerInventory inventory, PlayerEquipment equipment)
        {
            int count = 0;
            foreach (var l in lines)
            {
                if (l.Quantity <= 0) continue;
                count++;
                var check = CheckSell(l, inventory, equipment);
                if (check != ShopResult.Ok) return check;
            }
            if (count == 0) return ShopResult.Nothing;

            long total = TotalSell(lines);
            foreach (var l in lines)
                if (l.Quantity > 0) inventory.RemoveFromEntry(l.Entry, l.Quantity);
            wallet.AddGold(total);
            return ShopResult.Ok;
        }

        public static string ToMessage(ShopResult result) => result switch
        {
            ShopResult.Ok => string.Empty,
            ShopResult.Nothing => "선택한 항목이 없습니다",
            ShopResult.NotEnoughGold => "골드가 부족합니다",
            ShopResult.InventoryFull => "인벤토리 칸이 부족합니다",
            ShopResult.Overweight => "무게가 초과되었습니다. 먼저 무게를 줄이세요",
            ShopResult.NotEnoughItems => "아이템 수량이 부족합니다",
            ShopResult.NotSellable => "판매할 수 없는 아이템입니다",
            ShopResult.Equipped => "장착을 해제한 뒤 판매하세요",
            ShopResult.Locked => "잠긴 아이템은 판매할 수 없습니다",
            _ => "거래를 완료할 수 없습니다"
        };
    }
}
