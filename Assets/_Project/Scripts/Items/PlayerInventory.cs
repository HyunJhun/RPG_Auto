using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>획득 가능 여부</summary>
    public enum AcceptResult
    {
        Ok,
        Invalid,
        /// <summary>칸 부족</summary>
        NoSlot,
        /// <summary>무게 초과 상태라 획득 불가</summary>
        Overweight
    }

    /// <summary>인벤토리 한 칸 (소모품/재료 묶음 또는 장비 1개)</summary>
    public class InventoryEntry
    {
        public ItemData Item { get; }
        public int Count { get; internal set; }
        /// <summary>장비 칸이면 개체, 아니면 null</summary>
        public EquipmentInstance Equipment { get; }

        public bool IsEquipment => Equipment != null;
        /// <summary>잠금: 판매·버리기·분해·자동 담기에서 제외</summary>
        public bool IsLocked { get; internal set; }
        public float Weight => Item.Weight * Count;

        internal InventoryEntry(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }

        internal InventoryEntry(EquipmentInstance equipment)
        {
            Item = equipment.Data;
            Count = 1;
            Equipment = equipment;
        }
    }

    /// <summary>
    /// 아이템 보관. 칸 수(capacity)와 무게(maxWeight) 두 가지 한도를 가진다.
    /// - 무게가 최대치를 넘은 상태면 새 아이템을 얻을 수 없다 (넘는 순간의 1회 획득은 허용)
    /// - 칸/무게는 골드로 확장 (InventoryExpansionService)
    /// </summary>
    [RequireComponent(typeof(CombatStats))]
    public class PlayerInventory : MonoBehaviour
    {
        [Serializable]
        public struct ItemStack
        {
            public ItemData item;
            public int count;
            [Tooltip("장비일 때 시작 강화 수치")]
            public int enhanceLevel;
        }

        [SerializeField] private List<ItemStack> startItems = new();
        [Tooltip("인벤토리 칸 수")]
        [SerializeField, Min(1)] private int capacity = 60;
        [Tooltip("최대 무게")]
        [SerializeField, Min(1f)] private float maxWeight = 1200f;

        private readonly List<InventoryEntry> _entries = new();
        private readonly Dictionary<ItemData, InventoryEntry> _stacks = new();
        private readonly Dictionary<ItemData, float> _cooldownEnd = new();
        private CombatStats _stats;
        private float _weight;

        public IReadOnlyList<InventoryEntry> Entries => _entries;
        public int Capacity => capacity;
        public int UsedSlots => _entries.Count;
        public int FreeSlots => Mathf.Max(0, capacity - _entries.Count);
        public bool IsFull => _entries.Count >= capacity;
        public float MaxWeight => maxWeight;
        public float CurrentWeight => _weight;
        public bool IsOverweight => _weight > maxWeight + 0.0001f;

        // Observer
        /// <summary>아이템별 수량 변경 (아이템, 현재 수량)</summary>
        public event Action<ItemData, int> OnItemCountChanged;
        /// <summary>칸 구성 변경 (추가/삭제/수량)</summary>
        public event Action OnInventoryChanged;
        /// <summary>무게 변경 (현재, 최대)</summary>
        public event Action<float, float> OnWeightChanged;
        /// <summary>칸 수 변경 (확장)</summary>
        public event Action<int> OnCapacityChanged;
        public event Action<EquipmentInstance> OnEquipmentRemoved;
        public event Action<ItemData> OnItemUsed;

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
            foreach (var s in startItems)
            {
                if (s.item == null || s.count <= 0) continue;
                if (s.item is EquipmentData eq)
                    for (int i = 0; i < s.count; i++) Insert(new InventoryEntry(new EquipmentInstance(eq, s.enhanceLevel)));
                else
                    AddStack(s.item, s.count);
            }
            RecalculateWeight();
        }

        // ---------------- 조회 ----------------

        /// <summary>소모품/재료: 보유 수량, 장비: 같은 종류 개수</summary>
        public int GetCount(ItemData item)
        {
            if (item == null) return 0;
            if (item.IsStackable) return _stacks.TryGetValue(item, out var e) ? e.Count : 0;
            int n = 0;
            foreach (var e in _entries) if (e.Item == item) n++;
            return n;
        }

        public bool Contains(EquipmentInstance equipment) => FindEntry(equipment) != null;

        /// <summary>이 칸이 아직 인벤토리에 있는지 (UI가 들고 있는 참조 확인용)</summary>
        public bool HasEntry(InventoryEntry entry) => entry != null && _entries.Contains(entry);

        public InventoryEntry FindEntry(EquipmentInstance equipment)
        {
            if (equipment == null) return null;
            foreach (var e in _entries) if (e.Equipment == equipment) return e;
            return null;
        }

        /// <summary>count개를 얻을 때 필요한 새 칸 수</summary>
        public int SlotsNeeded(ItemData item, int count)
        {
            if (item == null || count <= 0) return 0;
            return item.IsStackable ? (_stacks.ContainsKey(item) ? 0 : 1) : count;
        }

        public AcceptResult CheckAccept(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return AcceptResult.Invalid;
            if (IsOverweight) return AcceptResult.Overweight;
            if (UsedSlots + SlotsNeeded(item, count) > capacity) return AcceptResult.NoSlot;
            return AcceptResult.Ok;
        }

        public bool CanAccept(ItemData item, int count = 1) => CheckAccept(item, count) == AcceptResult.Ok;

        public float GetCooldownRemaining(ItemData item) =>
            item != null && _cooldownEnd.TryGetValue(item, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;

        // ---------------- 추가/삭제 ----------------

        /// <summary>
        /// 아이템 획득(양수) 또는 소모(음수). 장비는 개수만큼 새 개체(+0)를 만든다.
        /// 획득 시 CheckAccept를 통과하지 못하면 false.
        /// </summary>
        public bool AddItem(ItemData item, int amount)
        {
            if (item == null || amount == 0) return false;
            if (amount < 0) return RemoveItem(item, -amount);
            if (!CanAccept(item, amount)) return false;

            if (item is EquipmentData eq)
                for (int i = 0; i < amount; i++) Insert(new InventoryEntry(new EquipmentInstance(eq)));
            else
                AddStack(item, amount);

            NotifyChanged(item);
            return true;
        }

        /// <summary>
        /// 무게 검사 없이 지급 (칸 수만 확인). 여러 개를 한 번에 지급할 때 중간에 무게를 넘어도 끊기지 않게 하기 위함.
        /// 호출 측에서 미리 CheckAccept/칸 수 검사를 해야 한다.
        /// </summary>
        public bool ForceAdd(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return false;
            if (UsedSlots + SlotsNeeded(item, amount) > capacity) return false;
            if (item is EquipmentData eq)
                for (int i = 0; i < amount; i++) Insert(new InventoryEntry(new EquipmentInstance(eq)));
            else
                AddStack(item, amount);
            NotifyChanged(item);
            return true;
        }

        /// <summary>장비 개체 획득 (강화된 장비 등)</summary>
        public bool AddEquipment(EquipmentInstance equipment)
        {
            if (equipment == null || Contains(equipment)) return false;
            if (!CanAccept(equipment.Data)) return false;
            Insert(new InventoryEntry(equipment));
            NotifyChanged(equipment.Data);
            return true;
        }

        /// <summary>소모품/재료 수량 차감. 부족하면 false</summary>
        public bool RemoveItem(ItemData item, int amount)
        {
            if (item == null || amount <= 0 || !item.IsStackable) return false;
            if (!_stacks.TryGetValue(item, out var entry) || entry.Count < amount) return false;

            entry.Count -= amount;
            if (entry.Count == 0)
            {
                _stacks.Remove(item);
                _entries.Remove(entry);
            }
            NotifyChanged(item);
            return true;
        }

        /// <summary>장비 개체 제거 (판매/버리기). 장착 중이면 PlayerEquipment가 해제한다.</summary>
        public bool RemoveEquipment(EquipmentInstance equipment)
        {
            var entry = FindEntry(equipment);
            if (entry == null) return false;
            _entries.Remove(entry);
            OnEquipmentRemoved?.Invoke(equipment);
            NotifyChanged(equipment.Data);
            return true;
        }

        /// <summary>칸 단위 제거 (버리기 등). 묶음이면 count만큼, 장비면 1개</summary>
        public bool RemoveFromEntry(InventoryEntry entry, int count)
        {
            if (entry == null) return false;
            return entry.IsEquipment ? RemoveEquipment(entry.Equipment) : RemoveItem(entry.Item, count);
        }

        private void AddStack(ItemData item, int amount)
        {
            if (_stacks.TryGetValue(item, out var entry)) entry.Count += amount;
            else
            {
                entry = new InventoryEntry(item, amount);
                _stacks[item] = entry;
                _entries.Add(entry);
            }
        }

        private void Insert(InventoryEntry entry) => _entries.Add(entry);

        // ---------------- 잠금 / 정렬 ----------------

        /// <summary>묶음 아이템의 칸 (없으면 null)</summary>
        public InventoryEntry FindStack(ItemData item) =>
            item != null && _stacks.TryGetValue(item, out var e) ? e : null;

        public void SetLocked(InventoryEntry entry, bool locked)
        {
            if (entry == null || !_entries.Contains(entry) || entry.IsLocked == locked) return;
            entry.IsLocked = locked;
            OnInventoryChanged?.Invoke();
        }

        /// <summary>칸 순서를 정렬 기준(Strategy: IComparer)대로 바꾼다. 같은 값이면 기존 순서 유지</summary>
        public void Sort(IComparer<InventoryEntry> comparer)
        {
            if (comparer == null || _entries.Count < 2) return;
            var sorted = new List<InventoryEntry>(_entries);
            // List.Sort는 불안정 정렬이라 기존 순서를 마지막 기준으로 써서 안정 정렬로 만든다
            var order = new Dictionary<InventoryEntry, int>();
            for (int i = 0; i < _entries.Count; i++) order[_entries[i]] = i;
            sorted.Sort((a, b) =>
            {
                int c = comparer.Compare(a, b);
                return c != 0 ? c : order[a].CompareTo(order[b]);
            });
            _entries.Clear();
            _entries.AddRange(sorted);
            OnInventoryChanged?.Invoke();
        }

        // ---------------- 세이브 복원 ----------------

        /// <summary>저장 데이터 복원 전: 모든 칸을 비우고 칸 수/최대 무게를 저장값으로 맞춘다.</summary>
        public void ClearForRestore(int newCapacity, float newMaxWeight)
        {
            var removedItems = new List<ItemData>();
            foreach (var e in _entries)
            {
                if (e.IsEquipment) OnEquipmentRemoved?.Invoke(e.Equipment);
                if (!removedItems.Contains(e.Item)) removedItems.Add(e.Item);
            }
            _entries.Clear();
            _stacks.Clear();
            _cooldownEnd.Clear();
            if (newCapacity > 0) capacity = newCapacity;
            if (newMaxWeight > 0f) maxWeight = newMaxWeight;

            _weight = -1f; // 강제로 무게 알림
            RecalculateWeight();
            foreach (var item in removedItems) OnItemCountChanged?.Invoke(item, 0);
            OnCapacityChanged?.Invoke(capacity);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>저장 데이터 복원: 묶음 아이템 (한도 검사 없음)</summary>
        public InventoryEntry RestoreStack(ItemData item, int count)
        {
            if (item == null || count <= 0 || item is EquipmentData) return null;
            AddStack(item, count);
            NotifyChanged(item);
            return FindStack(item);
        }

        /// <summary>저장 데이터 복원: 장비 개체 (한도 검사 없음)</summary>
        public InventoryEntry RestoreEquipment(EquipmentInstance equipment)
        {
            if (equipment == null || Contains(equipment)) return null;
            var entry = new InventoryEntry(equipment);
            Insert(entry);
            NotifyChanged(equipment.Data);
            return entry;
        }

        // ---------------- 확장 ----------------

        public void ExpandSlots(int amount)
        {
            if (amount <= 0) return;
            capacity += amount;
            OnCapacityChanged?.Invoke(capacity);
            OnInventoryChanged?.Invoke();
        }

        public void ExpandWeight(float amount)
        {
            if (amount <= 0f) return;
            maxWeight += amount;
            OnWeightChanged?.Invoke(_weight, maxWeight);
        }

        // ---------------- 사용 ----------------

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
            RemoveItem(item, 1);
            OnItemUsed?.Invoke(item);
            return true;
        }

        // ---------------- 내부 ----------------

        private void NotifyChanged(ItemData item)
        {
            RecalculateWeight();
            OnItemCountChanged?.Invoke(item, GetCount(item));
            OnInventoryChanged?.Invoke();
        }

        private void RecalculateWeight()
        {
            float w = 0f;
            foreach (var e in _entries) w += e.Weight;
            if (Mathf.Approximately(w, _weight) && _weight != 0f) return;
            _weight = w;
            OnWeightChanged?.Invoke(_weight, maxWeight);
        }
    }
}
