using System.Collections.Generic;
using RPGAuto.Monster;

namespace RPGAuto.Combat.Targeting
{
    /// <summary>타겟 선정 전략 (Strategy Pattern)</summary>
    public interface ITargetSelector
    {
        MonsterController Select(in TargetQuery query, IReadOnlyList<MonsterController> candidates);
    }

    /// <summary>점수가 가장 낮은 후보를 고르는 공통 구현</summary>
    public abstract class ScoredTargetSelector : ITargetSelector
    {
        protected abstract float Score(MonsterController monster, in TargetQuery query);

        public MonsterController Select(in TargetQuery query, IReadOnlyList<MonsterController> candidates)
        {
            MonsterController best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                var m = candidates[i];
                if (!query.IsCandidate(m)) continue;

                float score = Score(m, query);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = m;
                }
            }
            return best;
        }

        protected static float SqrDistance(MonsterController m, in TargetQuery q) =>
            TargetQuery.FlatSqrDistance(m.transform.position, q.Origin);
    }

    public class NearestTargetSelector : ScoredTargetSelector
    {
        protected override float Score(MonsterController m, in TargetQuery q) => SqrDistance(m, q);
    }

    public class ElitePriorityTargetSelector : ScoredTargetSelector
    {
        private const float GradePenalty = 1_000_000f;

        /// <summary>보스(0) → 정예(1) → 일반(2) 순, 같은 등급이면 거리</summary>
        protected override float Score(MonsterController m, in TargetQuery q) =>
            (MonsterGrade.Boss - m.Grade) * GradePenalty + SqrDistance(m, q);
    }

    public class LowestHpTargetSelector : ScoredTargetSelector
    {
        protected override float Score(MonsterController m, in TargetQuery q) =>
            m.Stats.HpPercent * 1_000_000f + SqrDistance(m, q);
    }
}
