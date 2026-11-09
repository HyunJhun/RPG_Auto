using System.Collections.Generic;
using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>
    /// 저장 데이터의 itemId → ItemData 조회용 목록. SaveBuilder가 프로젝트의 모든 ItemData를 모아 채운다.
    /// </summary>
    [CreateAssetMenu(menuName = "RPGAuto/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemData> items = new();

        private Dictionary<string, ItemData> _byId;

        public IReadOnlyList<ItemData> Items => items;

        public ItemData Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, ItemData>();
                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                    if (_byId.ContainsKey(item.ItemId)) Debug.LogWarning($"[ItemDatabase] itemId 중복: {item.ItemId}");
                    else _byId.Add(item.ItemId, item);
                }
            }
            return _byId.TryGetValue(itemId, out var found) ? found : null;
        }

        private void OnValidate() => _byId = null;
    }
}
