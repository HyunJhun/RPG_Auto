using System.Collections.Generic;
using RPGAuto.Items;
using UnityEngine;

namespace RPGAuto.Gacha
{
    public readonly struct GachaResult
    {
        public readonly GachaGrade Grade;
        public readonly ItemData Item;
        public readonly int Count;

        public GachaResult(GachaGrade grade, ItemData item, int count)
        {
            Grade = grade;
            Item = item;
            Count = count;
        }
    }

    public enum GachaPullStatus
    {
        Ok,
        Invalid,
        NotEnoughGold,
        InventoryFull
    }

    /// <summary>뽑기 규칙 (UI와 분리된 순수 로직)</summary>
    public static class GachaService
    {
        private static readonly HashSet<ItemData> s_newKinds = new();

        public static long CostOf(GachaData data, int pulls) => pulls >= 10 ? data.TenCost : data.SingleCost * pulls;

        /// <summary>
        /// 뽑기 실행. 먼저 결과를 굴린 뒤 골드·인벤토리 칸을 검사하고, 통과하면 결제·지급한다
        /// (실패 시 아무것도 차감되지 않는다).
        /// </summary>
        public static GachaPullStatus Pull(GachaData data, int pulls, PlayerWallet wallet, PlayerInventory inventory, List<GachaResult> results)
        {
            results.Clear();
            if (data == null || pulls <= 0 || data.TotalWeight() <= 0f) return GachaPullStatus.Invalid;

            long cost = CostOf(data, pulls);
            if (!wallet.CanAfford(cost)) return GachaPullStatus.NotEnoughGold;

            for (int i = 0; i < pulls; i++) results.Add(Roll(data, GachaGrade.Common));

            // 10회 뽑기 보장: 보장 등급 이상이 없으면 마지막 결과를 보장 풀에서 다시 뽑는다
            if (pulls >= 10 && data.TotalWeight(data.TenPullGuarantee) > 0f)
            {
                bool hit = false;
                foreach (var r in results) hit |= r.Grade >= data.TenPullGuarantee;
                if (!hit) results[^1] = Roll(data, data.TenPullGuarantee);
            }

            // 새로 칸을 차지할 아이템 종류 수 검사
            s_newKinds.Clear();
            foreach (var r in results)
                if (inventory.GetCount(r.Item) == 0) s_newKinds.Add(r.Item);
            if (inventory.Items.Count + s_newKinds.Count > inventory.Capacity)
            {
                results.Clear();
                return GachaPullStatus.InventoryFull;
            }

            wallet.TrySpend(cost);
            foreach (var r in results) inventory.AddItem(r.Item, r.Count);
            return GachaPullStatus.Ok;
        }

        private static GachaResult Roll(GachaData data, GachaGrade minGrade)
        {
            float pick = Random.value * data.TotalWeight(minGrade);
            GachaData.GradeTable chosen = null;
            foreach (var g in data.Grades)
            {
                if (g.grade < minGrade || g.rewards.Count == 0) continue;
                chosen = g;
                pick -= Mathf.Max(0f, g.weight);
                if (pick <= 0f) break;
            }

            var reward = chosen.rewards[Random.Range(0, chosen.rewards.Count)];
            return new GachaResult(chosen.grade, reward.item, Mathf.Max(1, reward.count));
        }

        public static Color GradeColor(GachaGrade grade) => grade switch
        {
            GachaGrade.Rare => new Color(0.3f, 0.6f, 1f),
            GachaGrade.Epic => new Color(0.7f, 0.35f, 1f),
            GachaGrade.Legendary => new Color(1f, 0.6f, 0.15f),
            _ => new Color(0.6f, 0.6f, 0.62f)
        };

        public static string ToMessage(GachaPullStatus status) => status switch
        {
            GachaPullStatus.NotEnoughGold => "Not enough gold",
            GachaPullStatus.InventoryFull => "Not enough inventory space",
            GachaPullStatus.Invalid => "Summon is not available",
            _ => string.Empty
        };
    }
}
