using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGAuto.Items
{
    /// <summary>
    /// 장비 강화표. steps[n] = +n → +(n+1) 강화 규칙 (성공률, 비용, 실패 시 하락 여부).
    /// </summary>
    [CreateAssetMenu(menuName = "RPGAuto/Items/Enhance Table", fileName = "ENH_Default")]
    public class EnhanceTable : ScriptableObject
    {
        [Serializable]
        public struct Step
        {
            [Range(0f, 1f)] public float successRate;
            public long goldCost;
            [Tooltip("소모되는 강화 재료(scrollItem) 개수")]
            public int scrollCost;
            [Tooltip("실패 시 강화 수치 1 하락")]
            public bool downgradeOnFail;
        }

        [SerializeField] private ItemData scrollItem;
        [SerializeField] private List<Step> steps = new();

        public ItemData ScrollItem => scrollItem;
        public int MaxLevel => steps.Count;

        public bool TryGetStep(int currentLevel, out Step step)
        {
            if (currentLevel >= 0 && currentLevel < steps.Count)
            {
                step = steps[currentLevel];
                return true;
            }
            step = default;
            return false;
        }
    }
}
