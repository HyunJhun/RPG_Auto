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
            [Tooltip("세금 적용 전 가격")]
            public long price;
            [Tooltip("일괄 구매 시 이 수량까지 채운다 (0 = 일괄 구매 제외)")]
            public int restockTarget;
        }

        [SerializeField] private string shopName = "잡화 상인";
        [Tooltip("구매 가격에 붙는 세율 (0.2 = 20%)")]
        [SerializeField, Range(0f, 1f)] private float taxRate = 0.2f;
        [SerializeField] private List<Product> products = new();

        public string ShopName => shopName;
        public float TaxRate => taxRate;
        public IReadOnlyList<Product> Products => products;
    }
}
