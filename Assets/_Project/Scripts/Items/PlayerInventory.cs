using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>
    /// 소모품 보유 수량/사용. (인벤토리 UI는 이후 단계에서 이 데이터를 사용)
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

        private readonly Dictionary<ItemData, int> _counts = new();
        private readonly Dictionary<ItemData, float> _cooldownEnd = new();
        private CombatStats _stats;

        /// <summary>수량 변경 알림 (Observer)</summary>
        public event Action<ItemData, int> OnItemCountChanged;
        public event Action<ItemData> OnItemUsed;

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
            foreach (var s in startItems)
                if (s.item != null) _counts[s.item] = GetCount(s.item) + s.count;
        }

        public int GetCount(ItemData item) => item != null && _counts.TryGetValue(item, out int c) ? c : 0;

        public float GetCooldownRemaining(ItemData item) =>
            item != null && _cooldownEnd.TryGetValue(item, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;

        public void AddItem(ItemData item, int amount)
        {
            if (item == null || amount == 0) return;
            int next = Mathf.Max(0, GetCount(item) + amount);
            _counts[item] = next;
            OnItemCountChanged?.Invoke(item, next);
        }

        public bool CanUse(ItemData item) =>
            item != null && GetCount(item) > 0 && GetCooldownRemaining(item) <= 0f && !_stats.IsDead;

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
