using System;
using UnityEngine;

namespace RPGAuto.Data
{
    /// <summary>모든 유닛(플레이어/몬스터) 공통 스탯 묶음</summary>
    [Serializable]
    public struct StatBlock
    {
        [Header("Resource")]
        public float maxHp;
        public float maxMp;
        public float hpRegen;
        public float mpRegen;

        [Header("Primary")]
        public float str;
        public float dex;
        public float luk;
        public float intelligence;

        [Header("Derived")]
        public float physicalAttack;
        public float magicAttack;
        public float accuracy;
        public float physicalDefense;
        public float magicDefense;

        public static StatBlock operator +(StatBlock a, StatBlock b) => new()
        {
            maxHp = a.maxHp + b.maxHp, maxMp = a.maxMp + b.maxMp,
            hpRegen = a.hpRegen + b.hpRegen, mpRegen = a.mpRegen + b.mpRegen,
            str = a.str + b.str, dex = a.dex + b.dex, luk = a.luk + b.luk, intelligence = a.intelligence + b.intelligence,
            physicalAttack = a.physicalAttack + b.physicalAttack, magicAttack = a.magicAttack + b.magicAttack,
            accuracy = a.accuracy + b.accuracy,
            physicalDefense = a.physicalDefense + b.physicalDefense, magicDefense = a.magicDefense + b.magicDefense,
        };

        /// <summary>모든 값에 배율을 곱한 값 (장비 강화 보너스 등)</summary>
        public StatBlock Scaled(float k) => new()
        {
            maxHp = maxHp * k, maxMp = maxMp * k, hpRegen = hpRegen * k, mpRegen = mpRegen * k,
            str = str * k, dex = dex * k, luk = luk * k, intelligence = intelligence * k,
            physicalAttack = physicalAttack * k, magicAttack = magicAttack * k, accuracy = accuracy * k,
            physicalDefense = physicalDefense * k, magicDefense = magicDefense * k,
        };

        /// <summary>1차 스탯(STR/DEX/LUK/INT)을 공식에 따라 2차 스탯에 반영한 최종값</summary>
        public StatBlock WithPrimaryBonus(StatFormula formula)
        {
            if (formula == null) return this;
            var r = this;
            formula.Apply(ref r, StatFormula.Primary.Str, str);
            formula.Apply(ref r, StatFormula.Primary.Dex, dex);
            formula.Apply(ref r, StatFormula.Primary.Luk, luk);
            formula.Apply(ref r, StatFormula.Primary.Int, intelligence);
            return r;
        }
    }
}
