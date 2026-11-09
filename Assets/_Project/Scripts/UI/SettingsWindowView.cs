using System;
using RPGAuto.Combat;
using RPGAuto.InputHandling;
using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 전투 설정 창 (프라시아 전기 "환경설정 > 전투 > 탐지" + "조작 > 편의" 대응).
    /// 값은 바뀔 때마다 CombatSettingsSaver로 저장된다.
    /// Esc(뒤로 가기): 열린 창이 있으면 가장 위 창을 닫고, 없으면 이 창을 연다.
    /// </summary>
    public class SettingsWindowView : MonoBehaviour, IGameWindow
    {
        private static readonly string[] PriorityLabels = { "Nearest", "Elite+", "Low HP" };

        [Header("Data")]
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private CombatSettingsSaver saver;
        [SerializeField] private PlayerInputHandler input;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resetButton;

        [Header("Detection")]
        [SerializeField] private OptionRowView targetNormalRow;
        [SerializeField] private OptionRowView targetEliteRow;
        [SerializeField] private OptionRowView targetBossRow;
        [SerializeField] private OptionRowView priorityRow;
        [SerializeField] private OptionRowView detectRadiusRow;
        [SerializeField] private OptionRowView returnDistanceRow;

        [Header("Skill / Potion / Loot")]
        [SerializeField] private OptionRowView skillMpLimitRow;
        [SerializeField] private OptionRowView hpPotionRow;
        [SerializeField] private OptionRowView mpPotionRow;
        [SerializeField] private OptionRowView autoLootRow;
        [SerializeField] private OptionRowView autoLootRadiusRow;

        [Header("Assist")]
        [SerializeField] private OptionRowView assistModeRow;
        [SerializeField] private OptionRowView assistPotionRow;
        [SerializeField] private OptionRowView assistBagRow;

        private AutoCombatSettings S => combat.Settings;

        private void Awake()
        {
            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            resetButton?.onClick.AddListener(() => { saver?.ResetToDefault(); Refresh(); });

            Bind(targetNormalRow, () => S.targetNormal = !S.targetNormal);
            Bind(targetEliteRow, () => S.targetElite = !S.targetElite);
            Bind(targetBossRow, () => S.targetBoss = !S.targetBoss);
            Bind(autoLootRow, () => S.autoLoot = !S.autoLoot);
            Bind(assistModeRow, () => S.assistMode = !S.assistMode);
            Bind(assistBagRow, () => S.assistOnFullBag = !S.assistOnFullBag);
            Bind(assistPotionRow, null, () => S.assistPotionMin = Mathf.Clamp(S.assistPotionMin - 5, 0, 100), () => S.assistPotionMin = Mathf.Clamp(S.assistPotionMin + 5, 0, 100));

            Bind(priorityRow, null, () => CyclePriority(-1), () => CyclePriority(1));
            Bind(detectRadiusRow, null, () => S.detectRadius = Step(S.detectRadius, -1f, 5f, 30f), () => S.detectRadius = Step(S.detectRadius, 1f, 5f, 30f));
            Bind(returnDistanceRow, null, () => S.returnDistance = Step(S.returnDistance, -1f, 5f, 40f), () => S.returnDistance = Step(S.returnDistance, 1f, 5f, 40f));
            Bind(skillMpLimitRow, null, () => S.skillMpLimitPercent = Step(S.skillMpLimitPercent, -5f, 0f, 100f), () => S.skillMpLimitPercent = Step(S.skillMpLimitPercent, 5f, 0f, 100f));
            Bind(hpPotionRow, null, () => S.hpPotionPercent = Step(S.hpPotionPercent, -5f, 5f, 95f), () => S.hpPotionPercent = Step(S.hpPotionPercent, 5f, 5f, 95f));
            Bind(mpPotionRow, null, () => S.mpPotionPercent = Step(S.mpPotionPercent, -5f, 5f, 95f), () => S.mpPotionPercent = Step(S.mpPotionPercent, 5f, 5f, 95f));
            Bind(autoLootRadiusRow, null, () => S.autoLootRadius = Step(S.autoLootRadius, -1f, 2f, 20f), () => S.autoLootRadius = Step(S.autoLootRadius, 1f, 2f, 20f));

            SetOpen(false);
        }

        private void OnEnable() { if (input != null) input.OnCancelPressed += HandleCancel; }
        private void OnDisable() { if (input != null) input.OnCancelPressed -= HandleCancel; }

        private void HandleCancel()
        {
            if (!WindowManager.CloseTop()) SetOpen(true);
        }

        public bool IsOpen => window != null && window.activeSelf;
        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
            if (open) Refresh();
            else saver?.Save();
        }

        /// <summary>행 이벤트 연결: 값을 바꾼 뒤 저장 + 갱신</summary>
        private void Bind(OptionRowView row, Action onToggle, Action onMinus = null, Action onPlus = null)
        {
            if (row == null) return;
            if (onToggle != null) row.OnToggle += () => Apply(onToggle);
            if (onMinus != null) row.OnMinus += () => Apply(onMinus);
            if (onPlus != null) row.OnPlus += () => Apply(onPlus);
        }

        private void Apply(Action change)
        {
            change();
            saver?.Save();
            Refresh();
        }

        private void CyclePriority(int dir)
        {
            int count = Enum.GetValues(typeof(TargetPriorityMode)).Length;
            S.priorityMode = (TargetPriorityMode)(((int)S.priorityMode + dir + count) % count);
        }

        private static float Step(float value, float step, float min, float max) =>
            Mathf.Clamp(Mathf.Round(value / Mathf.Abs(step)) * Mathf.Abs(step) + step, min, max);

        private void Refresh()
        {
            targetNormalRow?.Show("Normal Monsters", S.targetNormal, null);
            targetEliteRow?.Show("Elite Monsters", S.targetElite, null);
            targetBossRow?.Show("Boss Monsters", S.targetBoss, null);
            priorityRow?.Show("Target Priority", null, PriorityLabels[(int)S.priorityMode]);
            detectRadiusRow?.Show("Detect Radius", null, $"{S.detectRadius:0}m");
            returnDistanceRow?.Show("Max Distance from Start", null, $"{S.returnDistance:0}m");
            skillMpLimitRow?.Show("Skill Auto MP Limit", null, $"{S.skillMpLimitPercent:0}%");
            hpPotionRow?.Show("HP Potion when HP ≤", null, $"{S.hpPotionPercent:0}%");
            mpPotionRow?.Show("MP Potion when MP ≤", null, $"{S.mpPotionPercent:0}%");
            autoLootRow?.Show("Auto Pickup (Auto Combat)", S.autoLoot, null);
            autoLootRadiusRow?.Show("Auto Pickup Radius", null, $"{S.autoLootRadius:0}m", S.autoLoot);
            assistModeRow?.Show("Assist Mode (shop round trip)", S.assistMode, null);
            assistPotionRow?.Show("Go shopping when HP Potions ≤", null, $"{S.assistPotionMin}", S.assistMode);
            assistBagRow?.Show("Go shopping when bag is full", S.assistOnFullBag, null);
        }
    }
}
