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
