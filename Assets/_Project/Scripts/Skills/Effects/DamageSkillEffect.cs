using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Combat.Targeting;
using RPGAuto.Monster;
using UnityEngine;

namespace RPGAuto.Skills
{
    [CreateAssetMenu(menuName = "RPGAuto/Skills/Effects/Damage", fileName = "E_Damage")]
    public class DamageSkillEffect : SkillEffect
    {
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private float damageMultiplier = 1f;
        [Tooltip("0이면 단일 대상, 0보다 크면 광역 반경")]
        [SerializeField] private float areaRadius;
        [SerializeField] private bool areaCenteredOnCaster = true;

        private static readonly List<MonsterController> s_buffer = new();

        public override void Execute(in SkillContext ctx)
        {
            if (areaRadius <= 0f)
            {
                if (ctx.Target != null && ctx.Target.IsAlive)
                    DamageCalculator.Attack(ctx.Caster, ctx.Target.Stats, damageType, damageMultiplier);
                return;
            }

            Vector3 center = areaCenteredOnCaster || ctx.Target == null
                ? ctx.CasterTransform.position
                : ctx.Target.transform.position;

            // 피격 중 사망/풀 반환으로 목록이 바뀔 수 있어 복사 후 순회
            s_buffer.Clear();
            s_buffer.AddRange(MonsterController.Active);
            float sqrRadius = areaRadius * areaRadius;

            foreach (var m in s_buffer)
            {
                if (!m.IsAlive) continue;
                if (TargetQuery.FlatSqrDistance(m.transform.position, center) > sqrRadius) continue;
                DamageCalculator.Attack(ctx.Caster, m.Stats, damageType, damageMultiplier);
            }
            s_buffer.Clear();
        }
    }
}
