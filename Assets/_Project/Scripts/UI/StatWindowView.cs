using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.InputHandling;
using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>스탯창 (단축키 C / 메뉴 버튼). 행은 UI_StatRow 프리팹으로 생성.</summary>
    public class StatWindowView : MonoBehaviour
    {
        [SerializeField] private CombatStats stats;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private GameObject window;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        private readonly List<(Text value, Func<string> getter)> _rows = new();
        private float _refreshTimer;

        private void Awake()
        {
            AddHeader("CHARACTER");
            AddRow("Level", () => progression.Level.ToString());
            AddRow("EXP", () => progression.IsMaxLevel
                ? "MAX"
                : $"{progression.Exp:N0} / {progression.RequiredExp:N0} ({progression.ExpRatio * 100f:0.000}%)");
            AddRow("HP", () => $"{Mathf.CeilToInt(stats.Hp)} / {Mathf.CeilToInt(stats.MaxHp)}");
            AddRow("MP", () => $"{Mathf.CeilToInt(stats.Mp)} / {Mathf.CeilToInt(stats.MaxMp)}");
            AddHeader("PRIMARY");
            AddRow("STR", () => F(stats.Str));
            AddRow("DEX", () => F(stats.Dex));
            AddRow("LUK", () => F(stats.Luk));
            AddRow("INT", () => F(stats.Int));
            AddHeader("COMBAT");
            AddRow("Physical ATK", () => F(stats.PhysicalAttack));
            AddRow("Magic ATK", () => F(stats.MagicAttack));
            AddRow("HP Regen", () => $"{stats.HpRegen:0.##}/s");
            AddRow("MP Regen", () => $"{stats.MpRegen:0.##}/s");
            AddRow("Accuracy", () => F(stats.Accuracy));
            AddRow("Physical DEF", () => F(stats.PhysicalDefense));
            AddRow("Magic DEF", () => F(stats.MagicDefense));

            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (input != null) input.OnStatWindowTogglePressed += Toggle;
        }

        private void OnDisable()
        {
            if (input != null) input.OnStatWindowTogglePressed -= Toggle;
        }

        private void Update()
        {
            if (window == null || !window.activeSelf) return;
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.2f;
            Refresh();
        }

        public void Toggle() => SetOpen(window != null && !window.activeSelf);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            if (open) Refresh();
        }

        private void Refresh()
        {
            foreach (var (value, getter) in _rows) value.text = getter();
        }

        private void AddRow(string label, Func<string> getter)
        {
            var row = Instantiate(rowPrefab, rowContainer);
            row.name = "UI_StatRow_" + label.Replace(" ", "");
            row.transform.Find("UI_StatRow_Label").GetComponent<Text>().text = label;
            var value = row.transform.Find("UI_StatRow_Value").GetComponent<Text>();
            _rows.Add((value, getter));
        }

        private void AddHeader(string title)
        {
            var row = Instantiate(rowPrefab, rowContainer);
            row.name = "UI_StatRow_Header_" + title;
            var label = row.transform.Find("UI_StatRow_Label").GetComponent<Text>();
            label.text = title;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.82f, 0.4f);
            row.transform.Find("UI_StatRow_Value").GetComponent<Text>().text = string.Empty;
        }

        private static string F(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#");
    }
}
