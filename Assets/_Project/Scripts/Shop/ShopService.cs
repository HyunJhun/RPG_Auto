using System;
using RPGAuto.Items;

namespace RPGAuto.Shop
{
    public enum ShopResult
    {
        Ok,
        Invalid,
        NotEnoughGold,
        InventoryFull,
        NotEnoughItems,
        NotSellable
    }

    /// <summary>구매/판매 규칙 (UI와 분리된 순수 로직)</summary>
    public static class ShopService
    {
        public const int MaxQuantity = 999;

        /// <summary>현재 골드로 살 수 있는 최대 수량 (최소 1)</summary>
        public static int MaxAffordable(long price, PlayerWallet wallet) =>
            price <= 0 ? MaxQuantity : (int)Math.Clamp(wallet.Gold / price, 1, MaxQuantity);

        public static ShopResult Buy(in ShopData.Product product, int quantity, PlayerWallet wallet, PlayerInventory inventory)
        {
            if (product.item == null || quantity <= 0 || product.price < 0) return ShopResult.Invalid;

            long cost = product.price * quantity;
            if (!wallet.CanAfford(cost)) return ShopResult.NotEnoughGold;
            if (!inventory.CanAccept(product.item)) return ShopResult.InventoryFull;

            wallet.TrySpend(cost);
            inventory.AddItem(product.item, quantity);
            return ShopResult.Ok;
        }

        public static ShopResult Sell(ItemData item, int quantity, PlayerWallet wallet, PlayerInventory inventory)
        {
            if (item == null || quantity <= 0) return ShopResult.Invalid;
            if (!item.IsSellable) return ShopResult.NotSellable;
            if (inventory.GetCount(item) < quantity) return ShopResult.NotEnoughItems;

            inventory.AddItem(item, -quantity);
            wallet.AddGold(item.SellPrice * quantity);
            return ShopResult.Ok;
        }

        public static string ToMessage(ShopResult result) => result switch
        {
            ShopResult.Ok => string.Empty,
            ShopResult.NotEnoughGold => "Not enough gold",
            ShopResult.InventoryFull => "Inventory is full",
            ShopResult.NotEnoughItems => "Not enough items",
            ShopResult.NotSellable => "This item cannot be sold",
            _ => "Cannot complete the transaction"
        };
    }
}
