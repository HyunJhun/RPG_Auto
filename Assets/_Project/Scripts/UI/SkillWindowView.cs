using System.Collections.Generic;
using RPGAuto.InputHandling;
using RPGAuto.Player;
using RPGAuto.QuickSlots;
using RPGAuto.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 스킬 창 (단축키 K / 메뉴 버튼). 프라시아 전기 스킬 설정 대응:
    /// 스킬 퀵슬롯(1~6) 등록, 스킬별 자동 사용 조건(MP 제한/사거리 기준/HP 최소·최대/사용 주기),
    /// 전역 "스킬 자동 사용 MP 제한".
    /// </summary>
    public class SkillWindowView : MonoBehaviour, IGameWindow
    {
        private const float PercentStep = 5f;
        private const float CycleStep = 1f;

        [Header("Data")]
        [SerializeField] private PlayerSkillController skills;
        [SerializeField] private QuickSlotBar quickSlots;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerInputHandler input;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private SkillRowView rowPrefab;

        [Header("Detail")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private GameObject detailEmptyHint;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Text detailIconLabel;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailType;
        [SerializeField] private Text detailStats;
        [SerializeField] private Text detailDescription;
        [SerializeField] private Button[] quickSlotButtons = new Button[QuickSlotBar.SkillSlotCount];

        [Header("Auto Use Options")]
        [SerializeField] private OptionRowView autoUseRow;
        [SerializeField] private OptionRowView mpLimitRow;
        [SerializeField] private OptionRowView rangeFirstRow;
        [SerializeField] private OptionRowView hpMinRow;
        [SerializeField] private OptionRowView hpMaxRow;
        [SerializeField] private OptionRowView cycleRow;
        [SerializeField] private OptionRowView globalMpLimitRow;

        [Header("Style")]
        [SerializeField] private Color quickSlotNormal = new(0.2f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color quickSlotAssigned = new(0.85f, 0.65f, 0.2f, 1f);

        private readonly List<SkillRowView> _rows = new();
        private SkillSlot _selected;
        private float _refreshTimer;

        private void Start()
        {
            // 스킬 슬롯은 PlayerSkillController.Awake에서 만들어지므로 Start에서 목록 생성
            foreach (var slot in skills.Slots)
            {
                var row = Instantiate(rowPrefab, listContainer);
                row.name = "UI_SkillRow_" + slot.Data.ShortLabel;
                row.Bind(slot, false, null);
                row.OnClicked += r => { _selected = r.Skill; Refresh(); };
                _rows.Add(row);
            }
            if (skills.Slots.Count > 0) _selected = skills.Slots[0];

            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            for (int i = 0; i < quickSlotButtons.Length; i++)
            {
                int index = i; // 클로저 캡처용
                quickSlotButtons[i]?.onClick.AddListener(() => ToggleQuickSlot(index));
            }

            Bind(autoUseRow, ToggleAutoUse, null, null);
            Bind(mpLimitRow, () => Edit(c => c.applyMpLimit = !c.applyMpLimit), null, null);
            Bind(rangeFirstRow, () => Edit(c => c.rangeFirst = !c.rangeFirst), null, null);
            Bind(hpMinRow, () => Edit(c => c.useHpMin = !c.useHpMin),
                () => Edit(c => c.hpMinPercent = StepPercent(c.hpMinPercent, -1)),
                () => Edit(c => c.hpMinPercent = StepPercent(c.hpMinPercent, 1)));
            Bind(hpMaxRow, () => Edit(c => c.useHpMax = !c.useHpMax),
                () => Edit(c => c.hpMaxPercent = StepPercent(c.hpMaxPercent, -1)),
                () => Edit(c => c.hpMaxPercent = StepPercent(c.hpMaxPercent, 1)));
            Bind(cycleRow, () => Edit(c =>
                {
                    c.useCustomCycle = !c.useCustomCycle;
                    if (c.useCustomCycle && c.cycleSeconds < _selected.Data.Cooldown) c.cycleSeconds = Mathf.Ceil(_selected.Data.Cooldown);
                }),
                () => Edit(c => c.cycleSeconds = Mathf.Max(_selected.Data.Cooldown, c.cycleSeconds - CycleStep)),
                () => Edit(c => c.cycleSeconds = Mathf.Min(300f, c.cycleSeconds + CycleStep)));
            Bind(globalMpLimitRow, null,
                () => { combat.Settings.skillMpLimitPercent = Mathf.Clamp(combat.Settings.skillMpLimitPercent - PercentStep, 0f, 100f); Refresh(); },
                () => { combat.Settings.skillMpLimitPercent = Mathf.Clamp(combat.Settings.skillMpLimitPercent + PercentStep, 0f, 100f); Refresh(); });

            SetOpen(false);
        }

        private static void Bind(OptionRowView row, System.Action onToggle, System.Action onMinus, System.Action onPlus)
        {
            if (row == null) return;
            if (onToggle != null) row.OnToggle += onToggle;
            if (onMinus != null) row.OnMinus += onMinus;
            if (onPlus != null) row.OnPlus += onPlus;
        }

        private void OnEnable() { if (input != null) input.OnSkillWindowTogglePressed += Toggle; }
        private void OnDisable() { if (input != null) input.OnSkillWindowTogglePressed -= Toggle; }

        private void Update()
        {
            // 퀵슬롯에서 자동 ON/OFF를 바꾸는 등 외부 변경도 반영
            if (!IsOpen) return;
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            Refresh();
        }

        public bool IsOpen => window != null && window.activeSelf;
        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
            if (open) Refresh();
        }

        // ---------------- Actions ----------------

        private void ToggleQuickSlot(int index)
        {
            if (_selected == null) return;
            bool alreadyHere = quickSlots.FindSkillSlot(_selected) == index;
            quickSlots.AssignSkill(index, alreadyHere ? null : _selected);
            Refresh();
        }

        /// <summary>자동 사용 ON/OFF는 퀵슬롯이 가진다 (퀵슬롯 밖의 스킬은 자동 사용 안 함)</summary>
        private void ToggleAutoUse()
        {
            int slot = quickSlots.FindSkillSlot(_selected);
            if (slot < 0) return;
            quickSlots.GetSlot(slot).ToggleAutoUse();
            Refresh();
        }

        private void Edit(System.Action<AutoUseCondition> change)
        {
            if (_selected == null) return;
            change(_selected.AutoUse);
            Refresh();
        }

        private static float StepPercent(float value, int dir) => Mathf.Clamp(Mathf.Round(value / PercentStep) * PercentStep + dir * PercentStep, PercentStep, 100f);

        // ---------------- View ----------------

        private void Refresh()
        {
            _refreshTimer = 0.25f;
            foreach (var row in _rows) row.Bind(row.Skill, row.Skill == _selected, MarkOf(row.Skill));

            if (globalMpLimitRow != null)
                globalMpLimitRow.Show("Skill Auto MP Limit", null, $"{combat.Settings.skillMpLimitPercent:0}%");

            bool has = _selected != null;
            if (detailRoot != null) detailRoot.SetActive(has);
            if (detailEmptyHint != null) detailEmptyHint.SetActive(!has);
            if (!has) return;

            var d = _selected.Data;
            var c = _selected.AutoUse;
            if (detailIcon != null) detailIcon.color = d.SlotColor;
            if (detailIconLabel != null) detailIconLabel.text = d.ShortLabel;
            if (detailName != null) detailName.text = d.DisplayName;
            if (detailType != null) detailType.text = d.RequiresTarget ? "Attack Skill" : "Self Skill";
            if (detailStats != null)
                detailStats.text = d.RequiresTarget
                    ? $"MP {d.MpCost:0}   Cooldown {d.Cooldown:0.#}s   Range {d.Range:0.#}m"
                    : $"MP {d.MpCost:0}   Cooldown {d.Cooldown:0.#}s";
            if (detailDescription != null) detailDescription.text = d.Description;

            int assigned = quickSlots.FindSkillSlot(_selected);
            for (int i = 0; i < quickSlotButtons.Length; i++)
            {
                var b = quickSlotButtons[i];
                if (b != null && b.targetGraphic != null) b.targetGraphic.color = i == assigned ? quickSlotAssigned : quickSlotNormal;
            }

            bool registered = assigned >= 0;
            bool autoOn = registered && quickSlots.GetSlot(assigned).IsAutoUse;
            autoUseRow?.Show(registered ? "Auto Use" : "Auto Use (register to quick slot)", autoOn, null, registered);
            mpLimitRow?.Show("Apply MP Limit", c.applyMpLimit, null);
            if (d.RequiresTarget) rangeFirstRow?.Show("Range First (use only in range)", c.rangeFirst, null);
            else rangeFirstRow?.Hide();
            hpMinRow?.Show("Use when HP ≥", c.useHpMin, $"{c.hpMinPercent:0}%");
            hpMaxRow?.Show("Use when HP ≤", c.useHpMax, $"{c.hpMaxPercent:0}%");
            cycleRow?.Show("Custom Cycle", c.useCustomCycle, c.useCustomCycle ? $"{c.cycleSeconds:0}s" : $"{d.Cooldown:0.#}s");
        }

        private string MarkOf(SkillSlot skill)
        {
            int slot = quickSlots.FindSkillSlot(skill);
            if (slot < 0) return string.Empty;
            return quickSlots.GetSlot(slot).IsAutoUse ? $"Q{slot + 1}  AUTO" : $"Q{slot + 1}";
        }
    }
}
