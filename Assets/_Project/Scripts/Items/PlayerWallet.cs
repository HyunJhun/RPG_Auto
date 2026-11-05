using System;
using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>플레이어 재화(골드). 상점/뽑기에서도 이 컴포넌트를 사용한다.</summary>
    public class PlayerWallet : MonoBehaviour
    {
        [SerializeField] private long startGold;

        public long Gold { get; private set; }

        /// <summary>골드 변경 알림 (현재 골드, 변화량) — Observer</summary>
        public event Action<long, long> OnGoldChanged;

        private void Awake() => Gold = Math.Max(0, startGold);

        public void AddGold(long amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnGoldChanged?.Invoke(Gold, amount);
        }

        public bool CanAfford(long amount) => amount >= 0 && Gold >= amount;

        public bool TrySpend(long amount)
        {
            if (!CanAfford(amount)) return false;
            if (amount == 0) return true;
            Gold -= amount;
            OnGoldChanged?.Invoke(Gold, -amount);
            return true;
        }
    }
}
