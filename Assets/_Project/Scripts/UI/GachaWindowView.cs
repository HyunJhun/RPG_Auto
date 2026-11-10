using System.Collections.Generic;
using System.Text;
using RPGAuto.Gacha;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 뽑기 창 (단축키 G / 메뉴 버튼). 1회/10회 뽑기, 확률 표시, 결과 순차 공개, 테스트용 골드 지급.
    /// </summary>
    public class GachaWindowView : MonoBehaviour, IGameWindow
    {
        [Header("Data")]
        [SerializeField] private GachaData gacha;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInputHandler input;

        [Header("Window")]
        [SerializeField] private GameObject window;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text bannerName;
        [SerializeField] private Text bannerDescription;
        [SerializeField] private Text ratesText;
        [SerializeField] private Button singleButton;
        [SerializeField] private Text singleText;
        [SerializeField] private Button tenButton;
        [SerializeField] private Text tenText;
        [SerializeField] private Text goldText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button testGoldButton;
        [SerializeField] private long testGoldAmount = 10000;

        [Header("Results")]
        [SerializeField] private RectTransform resultContainer;
        [SerializeField] private GachaResultSlotView resultPrefab;
        [SerializeField] private GameObject resultEmptyHint;
        [SerializeField] private float revealInterval = 0.12f;

        private readonly List<GachaResult> _results = new();
        private readonly List<GachaResultSlotView> _slots = new();

        private void Awake()
        {
            for (int i = 0; i < 10; i++)
            {
                var slot = Instantiate(resultPrefab, resultContainer);
                slot.name = $"UI_GachaResultSlot_{i + 1:00}";
                slot.gameObject.SetActive(false);
                _slots.Add(slot);
            }

            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(() => SetOpen(false));
            singleButton?.onClick.AddListener(() => Pull(1));
            tenButton?.onClick.AddListener(() => Pull(10));
            testGoldButton?.onClick.AddListener(() =>
            {
                wallet.AddGold(testGoldAmount);
                ShowMessage($"[테스트] 골드 +{testGoldAmount:N0}", false);
            });

            if (gacha != null)
            {
                if (bannerName != null) bannerName.text = gacha.DisplayName;
                if (bannerDescription != null) bannerDescription.text = gacha.Description;
                if (singleText != null) singleText.text = $"1회 소환\n{gacha.SingleCost:N0} G";
                if (tenText != null) tenText.text = $"10회 소환\n{gacha.TenCost:N0} G";
                if (ratesText != null) ratesText.text = BuildRatesText();
            }
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (input != null) input.OnGachaTogglePressed += Toggle;
            if (wallet != null) wallet.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDisable()
        {
            if (input != null) input.OnGachaTogglePressed -= Toggle;
            if (wallet != null) wallet.OnGoldChanged -= HandleGoldChanged;
        }

        private void HandleGoldChanged(long gold, long delta) => RefreshGold();

        public bool IsOpen => window != null && window.activeSelf;
        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (window == null) return;
            window.SetActive(open);
            WindowManager.NotifyChanged(this, open);
            if (!open) return;
            if (messageText != null) messageText.text = string.Empty;
            RefreshGold();
            if (resultEmptyHint != null) resultEmptyHint.SetActive(_results.Count == 0);
        }

        private void Pull(int count)
        {
            var status = GachaService.Pull(gacha, count, wallet, inventory, _results);
            if (status != GachaPullStatus.Ok)
            {
                ShowMessage(GachaService.ToMessage(status), true);
                return;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < _results.Count) _slots[i].Bind(_results[i], i * revealInterval);
                else _slots[i].gameObject.SetActive(false);
            }
            if (resultEmptyHint != null) resultEmptyHint.SetActive(false);

            var best = GachaGrade.Common;
            foreach (var r in _results) if (r.Grade > best) best = r.Grade;
            ShowMessage(best >= GachaGrade.Epic ? $"{GameText.Grade(best)} 등급 보상 획득!" : $"{count}회 소환 완료", false, GachaService.GradeColor(best));
        }

        private void ShowMessage(string text, bool error, Color? color = null)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.color = color ?? (error ? new Color(1f, 0.45f, 0.4f) : new Color(0.55f, 0.95f, 0.55f));
        }

        private void RefreshGold()
        {
            if (goldText != null && wallet != null) goldText.text = $"골드  {wallet.Gold:N0}";
        }

        private string BuildRatesText()
        {
            var sb = new StringBuilder("등급별 확률\n");
            foreach (var table in gacha.Grades)
            {
                if (table.rewards.Count == 0) continue;
                string hex = ColorUtility.ToHtmlStringRGB(GachaService.GradeColor(table.grade));
                sb.Append($"<color=#{hex}>{GameText.Grade(table.grade),-6}</color> {gacha.GetRatePercent(table.grade):0.##}%\n");
            }
            sb.Append($"\n10회 소환 시 {GameText.Grade(gacha.TenPullGuarantee)} 이상 1개 보장");
            return sb.ToString();
        }
    }
}
