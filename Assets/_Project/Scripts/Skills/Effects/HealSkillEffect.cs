using UnityEngine;

namespace RPGAuto.Skills
{
    [CreateAssetMenu(menuName = "RPGAuto/Skills/Effects/Heal", fileName = "E_Heal")]
    public class HealSkillEffect : SkillEffect
    {
        [SerializeField, Range(0f, 1f)] private float healPercentOfMaxHp = 0.3f;

        public override void Execute(in SkillContext ctx)
        {
            ctx.Caster.Heal(ctx.Caster.MaxHp * healPercentOfMaxHp);
        }
    }
}
