using UnityEngine;

namespace RPGAuto.Combat
{
    public enum DamageType
    {
        Physical,
        Magic
    }

    /// <summary>명중 판정 + 피해량 계산 (계수는 엑셀 CombatFormula 시트)</summary>
    public static class DamageCalculator
    {
        public static float HitChance(CombatStats attacker, CombatStats defender)
        {
            var f = attacker.CombatFormula;
            float chance = f.hitBase + (attacker.Accuracy - defender.Level * f.accuracyPerLevel) * f.hitPerAccuracy;
            return Mathf.Clamp(chance, f.hitMin, f.hitMax);
        }

        public static float RollDamage(CombatStats attacker, CombatStats defender, DamageType type, float multiplier)
        {
            var f = attacker.CombatFormula;
            float atk = type == DamageType.Physical ? attacker.PhysicalAttack : attacker.MagicAttack;
            float def = type == DamageType.Physical ? defender.PhysicalDefense : defender.MagicDefense;
            float raw = atk * multiplier * Random.Range(1f - f.damageVariance, 1f + f.damageVariance);
            float k = Mathf.Max(1f, f.defenseConstant);
            return Mathf.Max(1f, raw * k / (k + Mathf.Max(0f, def)));
        }

        /// <summary>명중 판정 후 피해 적용. 빗나가면 false.</summary>
        public static bool Attack(CombatStats attacker, CombatStats defender, DamageType type, float multiplier)
        {
            if (attacker == null || defender == null || defender.IsDead) return false;

            if (Random.value > HitChance(attacker, defender))
            {
                defender.NotifyMissed(attacker);
                return false;
            }
            defender.TakeDamage(RollDamage(attacker, defender, type, multiplier), attacker);
            return true;
        }
    }

    /// <summary>플레이어 대비 몬스터 레벨 차이에 따른 이름 색상</summary>
    public static class LevelColorRule
    {
        public static readonly Color Normal = Color.white;
        public static readonly Color Higher = new(1f, 0.6f, 0.15f);   // +5 이상: 주황
        public static readonly Color Danger = new(1f, 0.25f, 0.25f);  // +10 이상: 빨강

        public static Color Get(int unitLevel, int playerLevel)
        {
            int diff = unitLevel - playerLevel;
            if (diff >= 10) return Danger;
            if (diff >= 5) return Higher;
            return Normal;
        }
    }
}
