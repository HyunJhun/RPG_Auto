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
        InventoryFull,
        Overweight
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

            // 무게 초과 상태면 획득 불가, 새로 차지할 칸 수 검사 (장비는 1개 = 1칸)
            if (inventory.IsOverweight)
            {
                results.Clear();
                return GachaPullStatus.Overweight;
            }
            s_newKinds.Clear();
            int slots = 0;
            foreach (var r in results)
            {
                if (!r.Item.IsStackable) slots += r.Count;
                else if (inventory.GetCount(r.Item) == 0 && s_newKinds.Add(r.Item)) slots++;
            }
            if (slots > inventory.FreeSlots)
            {
                results.Clear();
                return GachaPullStatus.InventoryFull;
            }

            wallet.TrySpend(cost);
            // 검사를 통과했으므로 칸 확인 없이 지급 (지급 도중 무게를 넘어도 이번 결과는 모두 받는다)
            foreach (var r in results) inventory.ForceAdd(r.Item, r.Count);
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
            GachaPullStatus.NotEnoughGold => "골드가 부족합니다",
            GachaPullStatus.InventoryFull => "인벤토리 공간이 부족합니다",
            GachaPullStatus.Overweight => "무게가 초과되었습니다. 먼저 무게를 줄이세요",
            GachaPullStatus.Invalid => "소환할 수 없습니다",
            _ => string.Empty
        };
    }
}
