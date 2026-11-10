using System;
using System.Collections.Generic;
using RPGAuto.Items;
using UnityEngine;

namespace RPGAuto.Gacha
{
    public enum GachaGrade
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>뽑기 확률표 (등급 가중치 → 등급 내 보상 균등)</summary>
    [CreateAssetMenu(menuName = "RPGAuto/Gacha/Gacha Data", fileName = "GACHA_New")]
    public class GachaData : ScriptableObject
    {
        [Serializable]
        public struct Reward
        {
            public ItemData item;
            public int count;
        }

        [Serializable]
        public class GradeTable
        {
            public GachaGrade grade;
            [Tooltip("가중치 (전체 합 대비 비율이 확률)")]
            public float weight = 1f;
            public List<Reward> rewards = new();
        }

        [SerializeField] private string displayName = "보급품 소환";
        [SerializeField, TextArea(2, 3)] private string description;
        [SerializeField] private long singleCost = 300;
        [SerializeField] private long tenCost = 2700;
        [Tooltip("10회 뽑기 시 이 등급 이상 1회 보장")]
        [SerializeField] private GachaGrade tenPullGuarantee = GachaGrade.Rare;
        [SerializeField] private List<GradeTable> grades = new();

        public string DisplayName => displayName;
        public string Description => description;
        public long SingleCost => singleCost;
        public long TenCost => tenCost;
        public GachaGrade TenPullGuarantee => tenPullGuarantee;
        public IReadOnlyList<GradeTable> Grades => grades;

        public float TotalWeight(GachaGrade minGrade = GachaGrade.Common)
        {
            float sum = 0f;
            foreach (var g in grades)
                if (g.grade >= minGrade && g.rewards.Count > 0) sum += Mathf.Max(0f, g.weight);
            return sum;
        }

        /// <summary>등급 확률(%) — UI 표시용</summary>
        public float GetRatePercent(GachaGrade grade)
        {
            float total = TotalWeight();
            if (total <= 0f) return 0f;
            foreach (var g in grades)
                if (g.grade == grade && g.rewards.Count > 0) return Mathf.Max(0f, g.weight) / total * 100f;
            return 0f;
        }
    }
}
