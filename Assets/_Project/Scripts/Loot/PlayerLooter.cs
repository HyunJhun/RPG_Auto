using System;
using RPGAuto.Combat;
using RPGAuto.Combat.Targeting;
using RPGAuto.Items;
using RPGAuto.Player;
using UnityEngine;

namespace RPGAuto.Loot
{
    /// <summary>
    /// 전리품 수령/줍기. 일반 몬스터 드랍은 LootManager가 바로 지급(Receive)하고,
    /// 바닥 전리품은 GroundLoot가 날아와 지급한다. 가방이 차서 바닥에 남은 것만 반경 판정으로 직접 줍는다.
    /// 자동 전투 중 주변 전리품 탐색(TryFindAutoLoot)은 PlayerAutoSearchState에서 사용한다.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerWallet), typeof(CombatStats))]
    public class PlayerLooter : MonoBehaviour
    {
        [SerializeField] private float pickupRadius = 1.2f;
        [SerializeField] private float pickupCheckInterval = 0.1f;
        [Tooltip("몸 중심 높이 (Transform 위치 기준). Capsule은 피벗이 중심이라 0")]
        [SerializeField] private float bodyCenterOffset = 0f;

        [Header("Notice")]
        [SerializeField] private float fullNoticeCooldown = 3f;

        private PlayerInventory _inventory;
        private PlayerWallet _wallet;
        private CombatStats _stats;
        private PlayerCombat _combat;
        private float _checkTimer;
        private float _nextFullNotice;

        public PlayerInventory Inventory => _inventory;
        public PlayerWallet Wallet => _wallet;
        /// <summary>자동 줍기 설정은 전투 설정(AutoCombatSettings)에 있다</summary>
        public bool AutoLootEnabled => _combat == null || _combat.Settings.autoLoot;
        private float AutoLootRadius => _combat != null ? _combat.Settings.autoLootRadius : 10f;
        public float PickupRadius => pickupRadius;
        public bool IsDead => _stats != null && _stats.IsDead;
        /// <summary>자동 획득 전리품이 날아와 닿는 지점 (캐릭터 몸 중심)</summary>
        public Vector3 BodyCenter => transform.position + Vector3.up * bodyCenterOffset;

        /// <summary>전리품 획득 알림 (Observer) — 획득 로그 UI가 구독</summary>
        public event Action<LootEntry> OnLootAcquired;
        /// <summary>칸 부족/무게 초과로 받지 못했을 때 (사유)</summary>
        public event Action<AcceptResult> OnAcquireBlocked;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _wallet = GetComponent<PlayerWallet>();
            _stats = GetComponent<CombatStats>();
            _combat = GetComponent<PlayerCombat>();
        }

        public bool CanReceive(in LootEntry entry) => entry.IsGold || _inventory.CanAccept(entry.Item, entry.Count);

        /// <summary>전리품 지급. 인벤토리가 가득 차 받지 못하면 false</summary>
        public bool Receive(LootEntry entry)
        {
            if (entry.IsGold)
            {
                _wallet.AddGold(entry.Gold);
            }
            else
            {
                var accept = _inventory.CheckAccept(entry.Item, entry.Count);
                if (accept != AcceptResult.Ok || !_inventory.AddItem(entry.Item, entry.Count))
                {
                    NotifyBlocked(accept == AcceptResult.Ok ? AcceptResult.NoSlot : accept);
                    return false;
                }
            }

            OnLootAcquired?.Invoke(entry);
            return true;
        }

        private void NotifyBlocked(AcceptResult reason)
        {
            if (Time.time < _nextFullNotice) return;
            _nextFullNotice = Time.time + fullNoticeCooldown;
            OnAcquireBlocked?.Invoke(reason);
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
            if (!AutoLootEnabled) return false;

            float best = AutoLootRadius;
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
