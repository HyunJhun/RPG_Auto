using System.Collections.Generic;
using System.Text;
using RPGAuto.Data;
using UnityEngine;

namespace RPGAuto.UI
{
    /// <summary>StatBlock을 "Physical ATK +28" 같은 줄로 바꿔 주는 표시용 헬퍼</summary>
    public static class StatText
    {
        private static readonly (string label, System.Func<StatBlock, float> get)[] Fields =
        {
            ("Physical ATK", s => s.physicalAttack),
            ("Magic ATK", s => s.magicAttack),
            ("Physical DEF", s => s.physicalDefense),
            ("Magic DEF", s => s.magicDefense),
            ("Accuracy", s => s.accuracy),
            ("Max HP", s => s.maxHp),
            ("Max MP", s => s.maxMp),
            ("HP Regen", s => s.hpRegen),
            ("MP Regen", s => s.mpRegen),
            ("STR", s => s.str),
            ("DEX", s => s.dex),
            ("LUK", s => s.luk),
            ("INT", s => s.intelligence),
        };

        private const string UpColor = "#7CFC7C";
        private const string DownColor = "#FF7366";

        /// <summary>
        /// 능력치를 줄로 만든다. compare가 있으면 차이를 괄호로 표시(증가 초록, 감소 빨강)하고,
        /// 비교 대상에만 있는 능력치도 +0 (-N)으로 보여 준다.
        /// </summary>
        public static string Lines(in StatBlock stats, StatBlock? compare = null)
        {
            var sb = new StringBuilder();
            foreach (var (label, get) in Fields)
            {
                float v = get(stats);
                float c = compare.HasValue ? get(compare.Value) : 0f;
                if (Mathf.Approximately(v, 0f) && Mathf.Approximately(c, 0f)) continue;
                sb.Append(label).Append("  +").Append(Format(v));
                if (compare.HasValue)
                {
                    float d = v - c;
                    if (!Mathf.Approximately(d, 0f))
                        sb.Append($"  <color={(d > 0 ? UpColor : DownColor)}>({(d > 0 ? "+" : "")}{Format(d)})</color>");
                }
                sb.Append('\n');
            }
            return sb.Length > 0 ? sb.ToString(0, sb.Length - 1) : string.Empty;
        }

        /// <summary>"ATK +23, DEF +15" 처럼 한 줄 요약</summary>
        public static string Summary(in StatBlock stats, int max = 4)
        {
            var parts = new List<string>();
            foreach (var (label, get) in Fields)
            {
                float v = get(stats);
                if (Mathf.Approximately(v, 0f)) continue;
                parts.Add($"{label} +{Format(v)}");
                if (parts.Count >= max) break;
            }
            return parts.Count > 0 ? string.Join("   ", parts) : "None";
        }

        public static string Format(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#");
    }
}
