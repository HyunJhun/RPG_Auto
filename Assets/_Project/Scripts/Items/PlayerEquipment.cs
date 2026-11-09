using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Data;
using RPGAuto.Player;
using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>장착 위치 (반지 2칸)</summary>
    public enum EquipPosition
    {
        Weapon,
        Helmet,
        Armor,
        Cloak,
        Gloves,
        Pants,
        Boots,
        Belt,
        Necklace,
        Earring,
        Ring1,
        Ring2
    }

    public enum EquipResult
    {
        Ok,
        Invalid,
        NotInInventory,
        LevelTooLow
    }

    /// <summary>
    /// 장착 장비 관리. 장착한 장비도 인벤토리 칸에 남아 있으며(E 표시), 합산 능력치를 CombatStats에 반영한다.
    /// 장비 강화(EquipmentInstance.OnChanged)·인벤토리 제거를 구독(Observer)해 자동으로 재계산/해제한다.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(CombatStats))]
    public class PlayerEquipment : MonoBehaviour
    {
        public static readonly EquipPosition[] AllPositions = (EquipPosition[])Enum.GetValues(typeof(EquipPosition));

        [Tooltip("시작 시 인벤토리에 넣고 장착할 장비")]
        [SerializeField] private List<EquipmentData> startEquipment = new();

        private readonly Dictionary<EquipPosition, EquipmentInstance> _equipped = new();
        private PlayerInventory _inventory;
        private CombatStats _stats;
        private PlayerProgression _progression;

        public StatBlock TotalBonus { get; private set; }

        /// <summary>장착 구성 변경 알림 (Observer)</summary>
        public event Action OnEquipmentChanged;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _stats = GetComponent<CombatStats>();
            _progression = GetComponent<PlayerProgression>();
            _inventory.OnEquipmentRemoved += HandleRemoved;
        }

        private void Start()
        {
            foreach (var data in startEquipment)
            {
                if (data == null) continue;
                var eq = new EquipmentInstance(data);
                if (_inventory.AddEquipment(eq)) Equip(eq);
            }
            if (startEquipment.Count > 0) _stats.ResetFull(); // 장비 능력치가 붙은 최대치로 채운다
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.OnEquipmentRemoved -= HandleRemoved;
            foreach (var eq in _equipped.Values) eq.OnChanged -= HandleEnhanced;
        }

        // ---------------- 조회 ----------------

        public EquipmentInstance Get(EquipPosition position) => _equipped.TryGetValue(position, out var eq) ? eq : null;

        public bool IsEquipped(EquipmentInstance equipment) => equipment != null && FindPosition(equipment).HasValue;

        public EquipPosition? FindPosition(EquipmentInstance equipment)
        {
            foreach (var pair in _equipped)
                if (pair.Value == equipment) return pair.Key;
            return null;
        }

        public int PlayerLevel => _progression != null ? _progression.Level : _stats.Level;

        public bool MeetsLevel(EquipmentData data) => data != null && PlayerLevel >= data.RequiredLevel;

        /// <summary>
        /// 이 장비와 비교할 장착 장비. 반지는 빈 칸이 있으면 null(빈 칸과 비교), 둘 다 차 있으면 약한 쪽.
        /// </summary>
        public EquipmentInstance GetComparisonTarget(EquipmentData data)
        {
            if (data == null) return null;
            if (data.SlotType != EquipSlotType.Ring) return Get(ChoosePosition(data.SlotType));
            var r1 = Get(EquipPosition.Ring1);
            var r2 = Get(EquipPosition.Ring2);
            if (r1 == null || r2 == null) return null;
            return EquipmentPower.Score(r1) <= EquipmentPower.Score(r2) ? r1 : r2;
        }

        /// <summary>장착 가능하고, 비교 대상(또는 빈 칸)보다 전투력 점수가 높은 장비인지</summary>
        public bool IsUpgrade(EquipmentInstance candidate)
        {
            if (candidate == null || IsEquipped(candidate) || !MeetsLevel(candidate.Data)) return false;
            var target = GetComparisonTarget(candidate.Data);
            return target == null || EquipmentPower.Score(candidate) > EquipmentPower.Score(target) + 0.01f;
        }

        // ---------------- 장착/해제 ----------------

        public EquipResult Equip(EquipmentInstance equipment)
        {
            if (equipment == null) return EquipResult.Invalid;
            if (!_inventory.Contains(equipment)) return EquipResult.NotInInventory;
            if (!MeetsLevel(equipment.Data)) return EquipResult.LevelTooLow;
            if (IsEquipped(equipment)) return EquipResult.Ok;

            var position = ChoosePosition(equipment.Data.SlotType);
            RemoveAt(position);
            _equipped[position] = equipment;
            equipment.OnChanged += HandleEnhanced;
            Recalculate();
            return EquipResult.Ok;
        }

        public void Unequip(EquipmentInstance equipment)
        {
            var pos = FindPosition(equipment);
            if (pos.HasValue) Unequip(pos.Value);
        }

        public void Unequip(EquipPosition position)
        {
            if (!RemoveAt(position)) return;
            Recalculate();
        }

        private bool RemoveAt(EquipPosition position)
        {
            if (!_equipped.TryGetValue(position, out var old)) return false;
            old.OnChanged -= HandleEnhanced;
            _equipped.Remove(position);
            return true;
        }

        /// <summary>반지는 빈 칸 우선, 둘 다 차 있으면 첫 번째 칸 교체</summary>
        private EquipPosition ChoosePosition(EquipSlotType type)
        {
            if (type != EquipSlotType.Ring) return (EquipPosition)Enum.Parse(typeof(EquipPosition), type.ToString());
            if (!_equipped.ContainsKey(EquipPosition.Ring1)) return EquipPosition.Ring1;
            if (!_equipped.ContainsKey(EquipPosition.Ring2)) return EquipPosition.Ring2;
            return EquipPosition.Ring1;
        }

        private void HandleRemoved(EquipmentInstance equipment) => Unequip(equipment);
        private void HandleEnhanced(EquipmentInstance _) => Recalculate();

        private void Recalculate()
        {
            var total = new StatBlock();
            foreach (var eq in _equipped.Values) total += eq.Stats;
            TotalBonus = total;
            _stats.SetBonus(total);
            OnEquipmentChanged?.Invoke();
        }
    }
}
