using System;
using UnityEngine;

namespace RPGAuto.Combat
{
    public enum TargetPriorityMode
    {
        Nearest,        // 가장 가까운 대상
        ElitePriority,  // 보스 → 정예 → 일반 순, 같은 등급은 거리
        LowestHp        // HP 비율 낮은 대상 우선 → 거리
    }

    /// <summary>프라시아 전기 "환경설정 > 전투 > 탐지" + "조작 > 편의" 대응 설정</summary>
    [Serializable]
    public class AutoCombatSettings
    {
        [Tooltip("자동 전투 시 대상을 찾는 반경 (캐릭터 기준)")]
        public float detectRadius = 12f;

        [Tooltip("자동 전투 시작 지점에서 벗어날 수 있는 최대 거리 (장르 관례로 보완)")]
        public float returnDistance = 15f;

        [Tooltip("탐지 우선 순위 (Strategy 교체)")]
        public TargetPriorityMode priorityMode = TargetPriorityMode.ElitePriority;

        [Header("탐지 대상")]
        public bool targetNormal = true;
        public bool targetElite = true;
        public bool targetBoss = true;

        [Header("전리품")]
        [Tooltip("자동 전투 중 주변 바닥 전리품을 먼저 줍는다")]
        public bool autoLoot = true;
        public float autoLootRadius = 10f;

        [Tooltip("MP가 이 비율(%) 이하이면 'MP 제한 적용' 스킬은 자동 사용하지 않음")]
        [Range(0f, 100f)] public float skillMpLimitPercent = 20f;

        [Header("물약 자동 사용 (퀵슬롯 자동 ON인 물약만)")]
        [Tooltip("HP가 이 비율(%) 이하이면 HP 물약 자동 사용")]
        [Range(1f, 100f)] public float hpPotionPercent = 70f;
        [Tooltip("MP가 이 비율(%) 이하이면 MP 물약 자동 사용")]
        [Range(1f, 100f)] public float mpPotionPercent = 40f;
    }
}
