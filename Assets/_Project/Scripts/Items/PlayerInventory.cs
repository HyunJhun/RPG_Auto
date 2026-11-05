using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>
    /// 아이템 보유 수량/사용. 획득 순서대로 칸을 차지하며, 수량이 0이 되면 칸이 비워진다.
    /// </summary>
    [RequireComponent(typeof(CombatStats))]
    public class PlayerInventory : MonoBehaviour
    {
        [Serializable]
        public struct ItemStack
        {
            public ItemData item;
            public int count;
        }

        [SerializeField] private List<ItemStack> startItems = new();
        [Tooltip("인벤토리 칸 수 (아이템 종류 수)")]
        [SerializeField] private int capacity = 30;

        private readonly Dictionary<ItemData, int> _counts = new();
        private readonly List<ItemData> _order = new();
        private readonly Dictionary<ItemData, float> _cooldownEnd = new();
        private CombatStats _stats;

        public int Capacity => capacity;
        /// <summary>보유 중인 아이템 (칸 순서)</summary>
        public IReadOnlyList<ItemData> Items => _order;
        public bool IsFull => _order.Count >= capacity;

        /// <summary>수량 변경 알림 (Observer)</summary>
        public event Action<ItemData, int> OnItemCountChanged;
        public event Action<ItemData> OnItemUsed;

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
            foreach (var s in startItems)
                if (s.item != null && s.count > 0) Store(s.item, GetCount(s.item) + s.count);
        }

        public int GetCount(ItemData item) => item != null && _counts.TryGetValue(item, out int c) ? c : 0;

        /// <summary>칸이 꽉 차도 이미 가진 아이템은 더 받을 수 있다</summary>
        public bool CanAccept(ItemData item) => item != null && (GetCount(item) > 0 || !IsFull);

        public float GetCooldownRemaining(ItemData item) =>
            item != null && _cooldownEnd.TryGetValue(item, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;

        /// <summary>수량 증감. 칸이 부족해 받을 수 없으면 false</summary>
        public bool AddItem(ItemData item, int amount)
        {
            if (item == null || amount == 0) return false;
            if (amount > 0 && !CanAccept(item)) return false;

            int next = Mathf.Max(0, GetCount(item) + amount);
            Store(item, next);
            OnItemCountChanged?.Invoke(item, next);
            return true;
        }

        private void Store(ItemData item, int count)
        {
            if (count > 0)
            {
                if (!_counts.ContainsKey(item)) _order.Add(item);
                _counts[item] = count;
            }
            else if (_counts.Remove(item))
            {
                _order.Remove(item);
            }
        }

        public bool CanUse(ItemData item) =>
            item != null && item.IsUsable && GetCount(item) > 0 && GetCooldownRemaining(item) <= 0f && !_stats.IsDead;

        public bool TryUse(ItemData item)
        {
            if (!CanUse(item)) return false;

            switch (item.Effect)
            {
                case ConsumableEffect.RestoreHp:
                    _stats.Heal(_stats.MaxHp * item.RestorePercent + item.RestoreFlat);
                    break;
                case ConsumableEffect.RestoreMp:
                    _stats.RestoreMp(_stats.MaxMp * item.RestorePercent + item.RestoreFlat);
                    break;
            }

            _cooldownEnd[item] = Time.time + item.Cooldown;
            AddItem(item, -1);
            OnItemUsed?.Invoke(item);
            return true;
        }
    }
}
