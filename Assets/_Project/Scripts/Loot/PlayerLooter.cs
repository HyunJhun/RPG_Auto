using System;
using RPGAuto.Combat;
using RPGAuto.Combat.Targeting;
using RPGAuto.Items;
using UnityEngine;

namespace RPGAuto.Loot
{
    /// <summary>
    /// 전리품 수령/줍기. 지급(Receive)은 LootManager가, 바닥 줍기는 반경 판정으로 직접 처리한다.
    /// 자동 전투 중 주변 전리품 탐색(TryFindAutoLoot)은 PlayerAutoSearchState에서 사용한다.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerWallet), typeof(CombatStats))]
    public class PlayerLooter : MonoBehaviour
    {
        [SerializeField] private float pickupRadius = 1.2f;
        [SerializeField] private float pickupCheckInterval = 0.1f;

        [Header("Auto Combat")]
        [Tooltip("자동 전투 중 주변 바닥 전리품을 먼저 주우러 간다")]
        [SerializeField] private bool autoLootInAutoCombat = true;
        [SerializeField] private float autoLootRadius = 10f;

        [Header("Notice")]
        [SerializeField] private float fullNoticeCooldown = 3f;

        private PlayerInventory _inventory;
        private PlayerWallet _wallet;
        private CombatStats _stats;
        private float _checkTimer;
        private float _nextFullNotice;

        public PlayerInventory Inventory => _inventory;
        public PlayerWallet Wallet => _wallet;
        public bool AutoLootEnabled => autoLootInAutoCombat;
        public float PickupRadius => pickupRadius;

        /// <summary>전리품 획득 알림 (Observer) — 획득 로그 UI가 구독</summary>
        public event Action<LootEntry> OnLootAcquired;
        /// <summary>인벤토리가 가득 차 받지 못했을 때</summary>
        public event Action OnInventoryFull;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _wallet = GetComponent<PlayerWallet>();
            _stats = GetComponent<CombatStats>();
        }

        public bool CanReceive(in LootEntry entry) => entry.IsGold || _inventory.CanAccept(entry.Item);

        /// <summary>전리품 지급. 인벤토리가 가득 차 받지 못하면 false</summary>
        public bool Receive(LootEntry entry)
        {
            if (entry.IsGold)
            {
                _wallet.AddGold(entry.Gold);
            }
            else if (!_inventory.AddItem(entry.Item, entry.Count))
            {
                NotifyFull();
                return false;
            }

            OnLootAcquired?.Invoke(entry);
            return true;
        }

        private void NotifyFull()
        {
            if (Time.time < _nextFullNotice) return;
            _nextFullNotice = Time.time + fullNoticeCooldown;
            OnInventoryFull?.Invoke();
        }

        private void Update()
        {
            if (_stats.IsDead) return;
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = pickupCheckInterval;

            var list = GroundLoot.Active;
            Vector3 pos = transform.position;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var loot = list[i];
                if (!loot.IsAvailable) continue;
                if (TargetQuery.FlatDistance(pos, loot.GroundPosition) > pickupRadius) continue;
                if (Receive(loot.Entry)) loot.Collect();
            }
        }

        /// <summary>자동 전투용: 기준점 반경 안에서 가장 가까운, 받을 수 있는 바닥 전리품</summary>
        public bool TryFindAutoLoot(Vector3 from, Vector3 anchor, float anchorRadius, out GroundLoot result)
        {
            result = null;
            if (!autoLootInAutoCombat) return false;

            float best = autoLootRadius;
            foreach (var loot in GroundLoot.Active)
            {
                if (!loot.IsAvailable || !CanReceive(loot.Entry)) continue;
                if (TargetQuery.FlatDistance(anchor, loot.GroundPosition) > anchorRadius) continue;
                float d = TargetQuery.FlatDistance(from, loot.GroundPosition);
                if (d > best) continue;
                best = d;
                result = loot;
            }
            return result != null;
        }
    }
}
