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

        /// <summary>0이 아닌 능력치만 줄로 만든다. compare가 있으면 차이를 괄호로 표시.</summary>
        public static string Lines(in StatBlock stats, StatBlock? compare = null, string diffColor = "#7CFC7C")
        {
            var sb = new StringBuilder();
            foreach (var (label, get) in Fields)
            {
                float v = get(stats);
                if (Mathf.Approximately(v, 0f)) continue;
                sb.Append(label).Append("  +").Append(Format(v));
                if (compare.HasValue)
                {
                    float d = v - get(compare.Value);
                    if (!Mathf.Approximately(d, 0f)) sb.Append($"  <color={diffColor}>({(d > 0 ? "+" : "")}{Format(d)})</color>");
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
