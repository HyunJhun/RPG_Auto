using System;
using System.Collections.Generic;
using RPGAuto.Items;
using RPGAuto.Player;
using RPGAuto.Skills;
using UnityEngine;

namespace RPGAuto.QuickSlots
{
    /// <summary>퀵슬롯 한 칸. 자동 사용 여부(true/false)는 슬롯이 가진다.</summary>
    public class QuickSlot
    {
        public int Index { get; }
        public IQuickSlotAction Action { get; private set; }
        public bool IsAutoUse { get; private set; }
        public bool IsEmpty => Action == null;

        // Observer
        public event Action<QuickSlot> OnAutoUseChanged;
        public event Action<QuickSlot> OnActionChanged;

        public QuickSlot(int index) => Index = index;

        public void SetAction(IQuickSlotAction action, bool autoUse)
        {
            Action = action;
            OnActionChanged?.Invoke(this);
            SetAutoUse(autoUse, force: true);
        }

        public void SetAutoUse(bool enabled, bool force = false)
        {
            if (IsEmpty) enabled = false;
            if (!force && IsAutoUse == enabled) return;
            IsAutoUse = enabled;
            Action?.OnAutoUseChanged(enabled);
            OnAutoUseChanged?.Invoke(this);
        }

        public void ToggleAutoUse() => SetAutoUse(!IsAutoUse);
    }

    /// <summary>
    /// 플레이어 퀵슬롯 바. 0~5 = 스킬 퀵슬롯(단축키 1~6), 6~9 = 물약 퀵슬롯(단축키 7~0)
    /// </summary>
    [RequireComponent(typeof(PlayerSkillController), typeof(PlayerInventory))]
    public class QuickSlotBar : MonoBehaviour
    {
        public const int SkillSlotCount = 6;
        public const int ItemSlotCount = 4;
        public const int TotalSlotCount = SkillSlotCount + ItemSlotCount;

        [Serializable]
        public struct SkillEntry
        {
            [Tooltip("PlayerSkillController 스킬 목록 인덱스 (-1 = 비어 있음)")]
            public int skillIndex;
            public bool autoUse;
        }

        [Serializable]
        public struct ItemEntry
        {
            public ItemData item;
            public bool autoUse;
        }

        [SerializeField] private SkillEntry[] skillEntries = new SkillEntry[SkillSlotCount];
        [SerializeField] private ItemEntry[] itemEntries = new ItemEntry[ItemSlotCount];

        private readonly List<QuickSlot> _slots = new();
        private PlayerController _player;
        private bool _built;

        public IReadOnlyList<QuickSlot> Slots { get { EnsureBuilt(); return _slots; } }

        public QuickSlot GetSlot(int index)
        {
            EnsureBuilt();
            return index >= 0 && index < _slots.Count ? _slots[index] : null;
        }

        private void Start() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _player = GetComponent<PlayerController>();
            var skills = GetComponent<PlayerSkillController>();
            var inventory = GetComponent<PlayerInventory>();

            // 퀵슬롯에 없는 스킬은 자동 사용하지 않는다
            foreach (var s in skills.Slots) s.SetAutoUse(false);

            for (int i = 0; i < TotalSlotCount; i++) _slots.Add(new QuickSlot(i));

            for (int i = 0; i < SkillSlotCount && i < skillEntries.Length; i++)
            {
                var skill = skills.GetSlot(skillEntries[i].skillIndex);
                if (skill != null) _slots[i].SetAction(new SkillQuickAction(skill), skillEntries[i].autoUse);
            }

            for (int i = 0; i < ItemSlotCount && i < itemEntries.Length; i++)
            {
                var item = itemEntries[i].item;
                if (item != null)
                    _slots[SkillSlotCount + i].SetAction(new ItemQuickAction(item, inventory), itemEntries[i].autoUse);
            }
        }

        private void Update()
        {
            if (_player == null) return;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsAutoUse) slot.Action?.TickAuto(_player);
            }
        }

        public void Activate(int index)
        {
            var slot = GetSlot(index);
            if (slot?.Action != null && _player != null) slot.Action.Activate(_player);
        }
    }
}
