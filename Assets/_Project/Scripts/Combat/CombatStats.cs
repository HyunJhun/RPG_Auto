using System;
using RPGAuto.Data;
using UnityEngine;

namespace RPGAuto.Combat
{
    /// <summary>
    /// 모든 유닛(플레이어/몬스터) 공통 스탯 + HP/MP + 피격/사망 이벤트.
    /// 기본값은 인스펙터 값이며, 엑셀 데이터가 있으면 ApplyStats로 덮어쓴다.
    /// </summary>
    public class CombatStats : MonoBehaviour
    {
        [SerializeField] private int level = 1;
        [Tooltip("1차 스탯 보너스 적용 전 기본 스탯 (엑셀 미적용 시 사용)")]
        [SerializeField] private StatBlock baseStats = new() { maxHp = 100f, physicalAttack = 10f, accuracy = 10f };

        private StatBlock _final;
        private StatFormula _statFormula;
        private bool _initialized;

        public int Level => level;
        public StatBlock Final => _final;
        public CombatFormula CombatFormula { get; private set; } = CombatFormula.Default;

        public float Hp { get; private set; }
        public float Mp { get; private set; }
        public float MaxHp => _final.maxHp;
        public float MaxMp => _final.maxMp;
        public float HpRegen => _final.hpRegen;
        public float MpRegen => _final.mpRegen;
        public float Str => _final.str;
        public float Dex => _final.dex;
        public float Luk => _final.luk;
        public float Int => _final.intelligence;
        public float PhysicalAttack => _final.physicalAttack;
        public float MagicAttack => _final.magicAttack;
        public float Accuracy => _final.accuracy;
        public float PhysicalDefense => _final.physicalDefense;
        public float MagicDefense => _final.magicDefense;

        public float HpPercent => MaxHp > 0f ? Hp / MaxHp : 0f;
        public float MpPercent => MaxMp > 0f ? Mp / MaxMp : 0f;
        public bool IsDead => Hp <= 0f;
        public CombatStats LastAttacker { get; private set; }

        // Observer
        public event Action<float, float> OnHpChanged;      // (current, max)
        public event Action<float, float> OnMpChanged;      // (current, max)
        public event Action<float, CombatStats> OnDamaged;  // (amount, attacker)
        public event Action<CombatStats> OnMissed;          // (attacker) — 회피 표시용
        public event Action OnDied;
        public event Action OnStatsChanged;                 // 레벨/스탯 변경

        /// <summary>모든 유닛의 피격 알림 (대상, 피해량, 공격자) — 데미지 숫자 UI 등이 구독</summary>
        public static event Action<CombatStats, float, CombatStats> OnAnyDamaged;
        /// <summary>모든 유닛의 빗나감 알림 (대상, 공격자)</summary>
        public static event Action<CombatStats, CombatStats> OnAnyMissed;

        private void Awake()
        {
            if (!_initialized) ApplyStats(level, baseStats, null, null, refill: true);
        }

        private void Update()
        {
            if (IsDead) return;
            float dt = Time.deltaTime;
            if (HpRegen > 0f && Hp < MaxHp) SetHp(Hp + HpRegen * dt);
            if (MpRegen > 0f && Mp < MaxMp) SetMp(Mp + MpRegen * dt);
        }

        /// <summary>레벨과 기본 스탯을 적용하고 1차 스탯 보너스를 계산한다.</summary>
        public void ApplyStats(int newLevel, in StatBlock newBase, StatFormula statFormula, CombatFormula combatFormula, bool refill)
        {
            _initialized = true;
            level = Mathf.Max(1, newLevel);
            baseStats = newBase;
            _statFormula = statFormula;
            CombatFormula = combatFormula ?? CombatFormula.Default;
            _final = baseStats.WithPrimaryBonus(_statFormula);

            if (refill)
            {
                Hp = 0f; Mp = 0f; // 이벤트가 반드시 발생하도록
                SetHp(MaxHp);
                SetMp(MaxMp);
            }
            else
            {
                SetHp(Mathf.Min(Hp, MaxHp), force: true);
                SetMp(Mathf.Min(Mp, MaxMp), force: true);
            }
            OnStatsChanged?.Invoke();
        }

        public void ResetFull()
        {
            Hp = 0f; Mp = 0f;
            SetHp(MaxHp);
            SetMp(MaxMp);
        }

        public void TakeDamage(float amount, CombatStats attacker)
        {
            if (IsDead || amount <= 0f) return;

            LastAttacker = attacker;
            SetHp(Hp - amount);
            OnDamaged?.Invoke(amount, attacker);
            OnAnyDamaged?.Invoke(this, amount, attacker);
            if (IsDead) OnDied?.Invoke();
        }

        public void NotifyMissed(CombatStats attacker)
        {
            if (IsDead) return;
            LastAttacker = attacker;
            OnMissed?.Invoke(attacker);
            OnAnyMissed?.Invoke(this, attacker);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            SetHp(Hp + amount);
        }

        public void RestoreMp(float amount)
        {
            if (IsDead || amount <= 0f) return;
            SetMp(Mp + amount);
        }

        public bool TryConsumeMp(float amount)
        {
            if (amount <= 0f) return true;
            if (Mp < amount) return false;
            SetMp(Mp - amount);
            return true;
        }

        private void SetHp(float value, bool force = false)
        {
            float clamped = Mathf.Clamp(value, 0f, MaxHp);
            if (!force && Mathf.Approximately(clamped, Hp)) return;
            Hp = clamped;
            OnHpChanged?.Invoke(Hp, MaxHp);
        }

        private void SetMp(float value, bool force = false)
        {
            float clamped = Mathf.Clamp(value, 0f, MaxMp);
            if (!force && Mathf.Approximately(clamped, Mp)) return;
            Mp = clamped;
            OnMpChanged?.Invoke(Mp, MaxMp);
        }
    }
}
