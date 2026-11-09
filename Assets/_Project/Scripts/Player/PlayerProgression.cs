using System;
using RPGAuto.Combat;
using RPGAuto.Data;
using RPGAuto.Monster;
using UnityEngine;

namespace RPGAuto.Player
{
    /// <summary>플레이어 레벨/경험치. 레벨별 스탯은 엑셀 PlayerLevel 시트에서 가져온다.</summary>
    [RequireComponent(typeof(CombatStats))]
    public class PlayerProgression : MonoBehaviour
    {
        [SerializeField] private StatDatabase database;
        [SerializeField] private int startLevel = 1;

        private CombatStats _stats;

        public int Level { get; private set; }
        public long Exp { get; private set; }
        public long RequiredExp { get; private set; }
        public bool IsMaxLevel => RequiredExp <= 0;
        /// <summary>현재 레벨 진행률 0~1</summary>
        public float ExpRatio => IsMaxLevel ? 1f : Mathf.Clamp01((float)((double)Exp / RequiredExp));

        // Observer
        public event Action<long, long> OnExpChanged;  // (exp, required)
        public event Action<int> OnLevelChanged;

        private void Awake()
        {
            _stats = GetComponent<CombatStats>();
            SetLevel(startLevel, refill: true);
        }

        private void OnEnable() => MonsterController.OnAnyKilled += HandleMonsterKilled;
        private void OnDisable() => MonsterController.OnAnyKilled -= HandleMonsterKilled;

        private void HandleMonsterKilled(MonsterController monster, CombatStats killer)
        {
            if (killer == _stats) AddExp(monster.ExpReward);
        }

        public void AddExp(long amount)
        {
            if (amount <= 0 || IsMaxLevel) return;

            Exp += amount;
            while (!IsMaxLevel && Exp >= RequiredExp)
            {
                Exp -= RequiredExp;
                SetLevel(Level + 1, refill: true);
            }
            if (IsMaxLevel) Exp = 0;
            OnExpChanged?.Invoke(Exp, RequiredExp);
        }

        /// <summary>저장 데이터 복원용: 레벨/경험치를 지정 값으로 맞춘다</summary>
        public void Restore(int level, long exp)
        {
            Exp = 0;
            SetLevel(level, refill: true);
            Exp = IsMaxLevel ? 0 : Math.Clamp(exp, 0, Math.Max(0, RequiredExp - 1));
            OnExpChanged?.Invoke(Exp, RequiredExp);
        }

        private void SetLevel(int level, bool refill)
        {
            int maxLevel = database != null ? database.MaxPlayerLevel : level;
            Level = Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));

            var row = database != null ? database.GetPlayerLevel(Level) : null;
            if (row != null)
            {
                RequiredExp = row.requiredExp;
                _stats.ApplyStats(Level, row.stats, database.StatFormula, database.CombatFormula, refill);
            }
            else
            {
                RequiredExp = 0;
                Debug.LogWarning("[PlayerProgression] StatDatabase가 없거나 레벨 데이터가 없습니다.");
            }

            OnLevelChanged?.Invoke(Level);
            OnExpChanged?.Invoke(Exp, RequiredExp);
        }
    }
}
