using System;
using UnityEngine;

namespace RPGAuto.Data
{
    /// <summary>1차 스탯 1포인트당 2차 스탯 증가량 (엑셀 StatFormula 시트)</summary>
    [Serializable]
    public class StatFormula
    {
        public enum Primary { Str, Dex, Luk, Int }

        [Serializable]
        public struct Coefficients
        {
            public float maxHp, maxMp, hpRegen, mpRegen;
            public float physicalAttack, magicAttack, accuracy, physicalDefense, magicDefense;
        }

        public Coefficients str;
        public Coefficients dex;
        public Coefficients luk;
        public Coefficients intelligence;

        public ref Coefficients Get(Primary p)
        {
            switch (p)
            {
                case Primary.Str: return ref str;
                case Primary.Dex: return ref dex;
                case Primary.Luk: return ref luk;
                default: return ref intelligence;
            }
        }

        public void Apply(ref StatBlock block, Primary primary, float points)
        {
            if (Mathf.Approximately(points, 0f)) return;
            var c = Get(primary);
            block.maxHp += c.maxHp * points;
            block.maxMp += c.maxMp * points;
            block.hpRegen += c.hpRegen * points;
            block.mpRegen += c.mpRegen * points;
            block.physicalAttack += c.physicalAttack * points;
            block.magicAttack += c.magicAttack * points;
            block.accuracy += c.accuracy * points;
            block.physicalDefense += c.physicalDefense * points;
            block.magicDefense += c.magicDefense * points;
        }
    }

    /// <summary>명중/방어/편차 등 전투 공식 계수 (엑셀 CombatFormula 시트)</summary>
    [Serializable]
    public class CombatFormula
    {
        public float hitBase = 0.9f;
        public float accuracyPerLevel = 5f;
        public float hitPerAccuracy = 0.005f;
        public float hitMin = 0.3f;
        public float hitMax = 0.99f;
        public float defenseConstant = 100f;
        public float damageVariance = 0.1f;

        public static readonly CombatFormula Default = new();
    }
}
