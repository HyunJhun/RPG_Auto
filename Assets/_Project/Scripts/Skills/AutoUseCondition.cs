using System;
using UnityEngine;

namespace RPGAuto.Skills
{
    /// <summary>
    /// 스킬 자동 사용 조건 (프라시아 전기 코어/베이스 스킬 설정 대응)
    /// MP 제한 적용 / 사거리 기준 / HP 최소·최대 조건 / 사용 주기
    /// </summary>
    [Serializable]
    public class AutoUseCondition
    {
        [Tooltip("자동 전투 시 이 스킬을 자동 사용")]
        public bool autoUse = true;

        [Tooltip("전역 '스킬 자동 사용 MP 제한'의 영향을 받음")]
        public bool applyMpLimit = true;

        [Tooltip("ON: 사거리 안일 때만 사용 / OFF: 사용 가능해지면 사거리까지 접근해서 사용")]
        public bool rangeFirst = true;

        [Tooltip("내 HP가 일정 비율 '이상'일 때만 사용")]
        public bool useHpMin;
        [Range(1f, 100f)] public float hpMinPercent = 50f;

        [Tooltip("내 HP가 일정 비율 '이하'일 때만 사용")]
        public bool useHpMax;
        [Range(1f, 100f)] public float hpMaxPercent = 40f;

        [Tooltip("자동 사용 주기 직접 지정 (기본값 = 재사용 대기시간)")]
        public bool useCustomCycle;
        public float cycleSeconds;

        public AutoUseCondition Clone() => (AutoUseCondition)MemberwiseClone();
    }
}
