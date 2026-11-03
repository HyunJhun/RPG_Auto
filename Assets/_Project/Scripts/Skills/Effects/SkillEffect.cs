using RPGAuto.Combat;
using RPGAuto.Monster;
using UnityEngine;

namespace RPGAuto.Skills
{
    public readonly struct SkillContext
    {
        public readonly CombatStats Caster;
        public readonly Transform CasterTransform;
        public readonly MonsterController Target;
        public readonly SkillData Skill;

        public SkillContext(CombatStats caster, Transform casterTransform, MonsterController target, SkillData skill)
        {
            Caster = caster;
            CasterTransform = casterTransform;
            Target = target;
            Skill = skill;
        }
    }

    /// <summary>스킬 효과 (Strategy Pattern) — 에셋으로 만들어 SkillData에 꽂아 교체</summary>
    public abstract class SkillEffect : ScriptableObject
    {
        public abstract void Execute(in SkillContext context);
    }
}
