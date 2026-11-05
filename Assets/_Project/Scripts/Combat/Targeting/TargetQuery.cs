using RPGAuto.Monster;
using UnityEngine;

namespace RPGAuto.Combat.Targeting
{
    /// <summary>타겟 탐색 조건 (반경, 등급 필터, 리쉬)</summary>
    public readonly struct TargetQuery
    {
        public readonly Vector3 Origin;
        public readonly float Radius;
        public readonly bool AllowNormal;
        public readonly bool AllowElite;
        public readonly bool AllowBoss;
        public readonly bool UseLeash;
        public readonly Vector3 LeashCenter;
        public readonly float LeashRadius;

        public TargetQuery(Vector3 origin, float radius, bool allowNormal, bool allowElite,
                           bool useLeash = false, Vector3 leashCenter = default, float leashRadius = 0f, bool allowBoss = true)
        {
            Origin = origin;
            Radius = radius;
            AllowNormal = allowNormal;
            AllowElite = allowElite;
            AllowBoss = allowBoss;
            UseLeash = useLeash;
            LeashCenter = leashCenter;
            LeashRadius = leashRadius;
        }

        public bool IsCandidate(MonsterController monster)
        {
            if (monster == null || !monster.IsAlive) return false;
            if (monster.Grade == MonsterGrade.Normal && !AllowNormal) return false;
            if (monster.Grade == MonsterGrade.Elite && !AllowElite) return false;
            if (monster.Grade == MonsterGrade.Boss && !AllowBoss) return false;

            Vector3 pos = monster.transform.position;
            if (FlatSqrDistance(pos, Origin) > Radius * Radius) return false;
            if (UseLeash && FlatSqrDistance(pos, LeashCenter) > LeashRadius * LeashRadius) return false;
            return true;
        }

        public static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        public static float FlatDistance(Vector3 a, Vector3 b) => Mathf.Sqrt(FlatSqrDistance(a, b));
    }
}
