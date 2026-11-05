using System;
using System.Collections.Generic;
using RPGAuto.Items;
using UnityEngine;

namespace RPGAuto.Shop
{
    /// <summary>상점 판매 목록</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Shop/Shop Data", fileName = "SHOP_New")]
    public class ShopData : ScriptableObject
    {
        [Serializable]
        public struct Product
        {
            public ItemData item;
            public long price;
        }

        [SerializeField] private string shopName = "General Store";
        [SerializeField] private List<Product> products = new();

        public string ShopName => shopName;
        public IReadOnlyList<Product> Products => products;
    }
}
