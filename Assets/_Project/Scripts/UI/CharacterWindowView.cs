using System;
using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 캐릭터 창 (단축키 C / STAT 버튼) — 프라시아 전기 장비창 형식.
    /// 이름·레벨, 공격/방어/명중 요약, [장비] 탭(장착 슬롯 12칸), [능력치] 탭(상세 스탯).
    /// </summary>
    public class CharacterWindowView : MonoBehaviour, IGameWindow
    {
        private static readonly string[] SlotLabels =
            { "무기", "투구", "갑옷", "망토", "장갑", "바지", "신발", "허리띠", "목걸이", "귀걸이", "반지", "반지" };

        [Header("Data")]
        [SerializeField] private CombatStats stats;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private string characterName = "모험가";

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text attackText;
        [SerializeField] private Text defenseText;
        [SerializeField] private Text accuracyText;

        [Header("Tabs")]
        [SerializeField] private Button equipmentTabButton;
        [SerializeField] private Button statsTabButton;
        [SerializeField] private GameObject equipmentTab;
        [SerializeField] private GameObject statsTab;

        [Header("Equipment Tab")]
        [Tooltip("EquipPosition 순서 (Weapon ~ Ring2)")]
        [SerializeField] private InventorySlotView[] equipSlots = new InventorySlotView[12];
        [SerializeField] private Text bonusText;
        [SerializeField] private ItemDetailPopup detailPopup;

        [Header("Stats Tab")]
        [SerializeField] private RectTransform statRowContainer;
        [SerializeField] private GameObject statRowPrefab;

        [Header("Style")]
        [SerializeField] private Color tabOn = new(0.85f, 0.65f, 0.2f, 1f);
        [SerializeField] private Color tabOff = new(0.2f, 0.22f, 0.28f, 1f);

        private readonly List<(Text value, Func<string> getter)> _rows = new();
        private EquipmentInstance _selected;
        private bool _showEquipment = true;
        private float _refreshTimer;

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            BuildStatRows();
            for (int i = 0; i < equipSlots.Length; i++)
                if (equipSlots[i] != null) equipSlots[i].OnClicked += HandleSlotClicked;

            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            equipmentTabButton?.onClick.AddListener(() => SelectTab(true));
            statsTabButton?.onClick.AddListener(() => SelectTab(false));
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (input != null) input.OnStatWindowTogglePressed += Toggle;
            if (equipment != null) equipment.OnEquipmentChanged += Refresh;
        }

        private void OnDisable()
        {
            if (input != null) input.OnStatWindowTogglePressed -= Toggle;
            if (equipment != null) equipment.OnEquipmentChanged -= Refresh;
        }

        private void Update()
        {
            // HP/MP·레벨 등 수시로 바뀌는 값은 주기적으로 갱신
            if (!IsOpen) return;
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            Refresh();
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
            if (open) Refresh();
            else if (detailPopup != null && detailPopup.IsOpen) detailPopup.SetOpen(false);
        }

        private void SelectTab(bool showEquipment)
        {
            _showEquipment = showEquipment;
            Refresh();
        }

        private void HandleSlotClicked(InventorySlotView slot)
        {
            if (slot.Equipment == null) return;
            _selected = slot.Equipment;
            detailPopup?.ShowEquipment(slot.Equipment, ItemDetailPopup.Dock.RightOfLeftPanel);
            Refresh();
        }

        private void Refresh()
        {
            _refreshTimer = 0.25f;
            if (nameText != null) nameText.text = characterName;
            if (levelText != null) levelText.text = $"Lv.{progression.Level}";
            if (attackText != null) attackText.text = StatText.Format(Mathf.Round(stats.PhysicalAttack));
            if (defenseText != null) defenseText.text = StatText.Format(Mathf.Round(stats.PhysicalDefense));
            if (accuracyText != null) accuracyText.text = StatText.Format(Mathf.Round(stats.Accuracy));

            if (equipmentTab != null) equipmentTab.SetActive(_showEquipment);
            if (statsTab != null) statsTab.SetActive(!_showEquipment);
            if (equipmentTabButton != null) equipmentTabButton.targetGraphic.color = _showEquipment ? tabOn : tabOff;
            if (statsTabButton != null) statsTabButton.targetGraphic.color = _showEquipment ? tabOff : tabOn;

            if (_showEquipment)
            {
                var positions = PlayerEquipment.AllPositions;
                for (int i = 0; i < equipSlots.Length && i < positions.Length; i++)
                {
                    if (equipSlots[i] == null) continue;
                    var eq = equipment.Get(positions[i]);
                    equipSlots[i].BindEquipment(eq, SlotLabels[i], eq != null && eq == _selected);
                }
                if (bonusText != null) bonusText.text = "장비 보너스\n" + StatText.Summary(equipment.TotalBonus, 6);
            }
            else
            {
                foreach (var (value, getter) in _rows) value.text = getter();
            }
        }

        // ---------------- Stats Tab ----------------

        private void BuildStatRows()
        {
            if (statRowContainer == null || statRowPrefab == null) return;
            AddHeader("캐릭터");
            AddRow("레벨", () => progression.Level.ToString());
            AddRow("경험치", () => progression.IsMaxLevel
                ? "최대"
                : $"{progression.Exp:N0} / {progression.RequiredExp:N0} ({progression.ExpRatio * 100f:0.000}%)");
            AddRow("HP", () => $"{Mathf.CeilToInt(stats.Hp)} / {Mathf.CeilToInt(stats.MaxHp)}");
            AddRow("MP", () => $"{Mathf.CeilToInt(stats.Mp)} / {Mathf.CeilToInt(stats.MaxMp)}");
            AddHeader("기본 능력치");
            AddRow("힘", () => StatText.Format(stats.Str));
            AddRow("민첩", () => StatText.Format(stats.Dex));
            AddRow("행운", () => StatText.Format(stats.Luk));
            AddRow("지능", () => StatText.Format(stats.Int));
            AddHeader("전투 능력치");
            AddRow("물리 공격력", () => WithBonus(stats.PhysicalAttack, equipment.TotalBonus.physicalAttack));
            AddRow("마법 공격력", () => WithBonus(stats.MagicAttack, equipment.TotalBonus.magicAttack));
            AddRow("HP 회복", () => $"{stats.HpRegen:0.##}/초");
            AddRow("MP 회복", () => $"{stats.MpRegen:0.##}/초");
            AddRow("명중", () => WithBonus(stats.Accuracy, equipment.TotalBonus.accuracy));
            AddRow("물리 방어력", () => WithBonus(stats.PhysicalDefense, equipment.TotalBonus.physicalDefense));
            AddRow("마법 방어력", () => WithBonus(stats.MagicDefense, equipment.TotalBonus.magicDefense));
        }

        /// <summary>최종값 + 장비로 오른 양 표시 (예: 120 (+23))</summary>
        private static string WithBonus(float total, float bonus) =>
            Mathf.Approximately(bonus, 0f) ? StatText.Format(total) : $"{StatText.Format(total)}  <color=#7CFC7C>(+{StatText.Format(bonus)})</color>";

        private void AddRow(string label, Func<string> getter)
        {
            var row = Instantiate(statRowPrefab, statRowContainer);
            row.name = "UI_StatRow_" + label.Replace(" ", "");
            row.transform.Find("UI_StatRow_Label").GetComponent<Text>().text = label;
            var value = row.transform.Find("UI_StatRow_Value").GetComponent<Text>();
            value.supportRichText = true;
            _rows.Add((value, getter));
        }

        private void AddHeader(string title)
        {
            var row = Instantiate(statRowPrefab, statRowContainer);
            row.name = "UI_StatRow_Header_" + title;
            var label = row.transform.Find("UI_StatRow_Label").GetComponent<Text>();
            label.text = title;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.82f, 0.4f);
            row.transform.Find("UI_StatRow_Value").GetComponent<Text>().text = string.Empty;
        }
    }
}
