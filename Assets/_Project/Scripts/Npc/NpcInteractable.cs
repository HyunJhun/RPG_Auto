using System.Collections.Generic;
using RPGAuto.Combat.Targeting;
using RPGAuto.Player;
using UnityEngine;

namespace RPGAuto.Npc
{
    /// <summary>
    /// 상호작용 가능한 NPC 공통 기반. 활성 목록(이름표·가장 가까운 NPC 찾기용)을 관리한다.
    /// 구체적인 동작(상점 열기 등)은 하위 클래스가 Interact에서 정한다.
    /// </summary>
    public abstract class NpcInteractable : MonoBehaviour
    {
        private static readonly List<NpcInteractable> s_active = new();
        public static IReadOnlyList<NpcInteractable> Active => s_active;

        [SerializeField] private string displayName = "NPC";
        [Tooltip("이름표 앞에 붙는 역할 (예: Merchant)")]
        [SerializeField] private string title = "";
        [SerializeField] private float interactRange = 2.5f;
        [Tooltip("이름표 높이 (발밑 기준)")]
        [SerializeField] private float headHeight = 2.4f;

        public string DisplayName => displayName;
        public string Title => title;
        public float InteractRange => interactRange;
        public Vector3 Position => transform.position;
        public Vector3 HeadPosition => new(transform.position.x, transform.position.y - transform.lossyScale.y + headHeight, transform.position.z);

        protected virtual void OnEnable() => s_active.Add(this);
        protected virtual void OnDisable() => s_active.Remove(this);

        public bool IsInRange(Vector3 position) => TargetQuery.FlatDistance(position, transform.position) <= interactRange;

        /// <summary>플레이어가 상호작용 범위 안에서 말을 걸었을 때</summary>
        public abstract void Interact(PlayerController player);

        /// <summary>범위 안에서 가장 가까운 NPC (없으면 null)</summary>
        public static NpcInteractable FindNearestInRange(Vector3 position)
        {
            NpcInteractable best = null;
            float bestDist = float.MaxValue;
            foreach (var npc in s_active)
            {
                float d = TargetQuery.FlatDistance(position, npc.transform.position);
                if (d > npc.InteractRange || d >= bestDist) continue;
                bestDist = d;
                best = npc;
            }
            return best;
        }
    }
}
