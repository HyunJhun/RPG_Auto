using System;
using RPGAuto.Combat;
using RPGAuto.Core;
using RPGAuto.Items;
using RPGAuto.Npc;
using UnityEngine;

namespace RPGAuto.Player.Assist
{
    /// <summary>
    /// 보조 모드 (프라시아 전기 '보조 모드' 대응): 자동 전투 중 HP 물약이 부족하거나 가방이 차면
    /// 상인에게 걸어가 전리품 판매 + 물약 보충 후, 사냥하던 자리로 돌아와 자동 전투를 다시 켠다.
    /// 왕복 과정은 State 패턴(감시 → 상인 이동 → 거래 → 복귀)으로 나눈다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class AssistController : MonoBehaviour
    {
        [Tooltip("조건 검사 주기(초)")]
        [SerializeField] private float checkInterval = 1f;
        [Tooltip("귀환 후 이 시간(초) 동안은 다시 귀환하지 않는다 (골드 부족 시 무한 왕복 방지)")]
        [SerializeField] private float cooldownAfterTrip = 60f;
        [Tooltip("빈 칸이 이 수 이하이면 가방이 찬 것으로 본다")]
        [SerializeField] private int fullBagFreeSlots = 2;
        [Tooltip("상인 앞에서 거래하는 연출 시간(초)")]
        [SerializeField] private float tradeDuration = 0.8f;

        public PlayerController Player { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public PlayerEquipment Equipment { get; private set; }
        public PlayerWallet Wallet { get; private set; }
        public RPGAuto.Shop.PlayerTradeSettings TradeSettings { get; private set; }
        public AutoCombatSettings Settings => (_combat != null ? _combat : _combat = GetComponent<PlayerCombat>()).Settings;

        public float CheckInterval => checkInterval;
        public float CooldownAfterTrip => cooldownAfterTrip;
        public float TradeDuration => tradeDuration;

        public StateMachine StateMachine { get; } = new();
        public AssistMonitorState MonitorState { get; private set; }
        public AssistToMerchantState ToMerchantState { get; private set; }
        public AssistTradeState TradeState { get; private set; }
        public AssistReturnState ReturnState { get; private set; }

        /// <summary>왕복 중 사용할 상인과 돌아올 사냥 기준점</summary>
        public MerchantNpc Merchant { get; internal set; }
        public Vector3 HuntAnchor { get; internal set; }

        public bool IsOnTrip => StateMachine.Current != null && StateMachine.Current != MonitorState;
        public bool IsAssistEnabled => Settings.assistMode;
        public string Status { get; private set; } = "";

        // Observer
        /// <summary>상태 문구 변경 (빈 문자열 = 표시 안 함)</summary>
        public event Action<string> OnStatusChanged;
        /// <summary>보조 모드 켜짐/꺼짐</summary>
        public event Action<bool> OnAssistModeChanged;

        private float _flashUntil;
        // PlayerController.Awake보다 먼저 쓰일 수 있으므로 직접 가져온다
        private PlayerCombat _combat;
        private CombatStats _stats;

        private void Awake()
        {
            Player = GetComponent<PlayerController>();
            Inventory = GetComponent<PlayerInventory>();
            Equipment = GetComponent<PlayerEquipment>();
            Wallet = GetComponent<PlayerWallet>();
            TradeSettings = GetComponent<RPGAuto.Shop.PlayerTradeSettings>();
            _combat = GetComponent<PlayerCombat>();
            _stats = GetComponent<CombatStats>();

            MonitorState = new AssistMonitorState(this);
            ToMerchantState = new AssistToMerchantState(this);
            TradeState = new AssistTradeState(this);
            ReturnState = new AssistReturnState(this);
        }

        private void Start() => StateMachine.ChangeState(MonitorState);

        private void OnEnable() => _stats.OnDied += HandleDied;
        private void OnDisable() => _stats.OnDied -= HandleDied;

        private void Update() => StateMachine.Tick(Time.deltaTime);

        // ---------------- 외부 조작 ----------------

        public void SetAssistMode(bool enabled)
        {
            if (Settings.assistMode == enabled) return;
            Settings.assistMode = enabled;
            if (!enabled && IsOnTrip) Cancel("보조 모드 꺼짐");
            GetComponent<CombatSettingsSaver>()?.Save();
            OnAssistModeChanged?.Invoke(enabled);
        }

        public void ToggleAssistMode() => SetAssistMode(!Settings.assistMode);

        /// <summary>왕복 중단 (수동 이동, 사망, 보조 모드 끄기 등)</summary>
        public void Cancel(string reason)
        {
            MonitorState.SuppressFor(0f);
            StateMachine.ChangeState(MonitorState);
            Flash(reason);
        }

        // ---------------- 조건 ----------------

        /// <summary>인벤토리의 HP 회복 소모품 총 개수</summary>
        public int CountHpPotions()
        {
            int total = 0;
            foreach (var e in Inventory.Entries)
                if (!e.IsEquipment && e.Item.IsUsable && e.Item.Effect == ConsumableEffect.RestoreHp) total += e.Count;
            return total;
        }

        public bool IsBagFull => Inventory.IsOverweight || Inventory.FreeSlots <= fullBagFreeSlots;

        /// <summary>귀환이 필요한 이유 (없으면 null)</summary>
        public string GetTripReason()
        {
            if (CountHpPotions() <= Settings.assistPotionMin) return "물약 부족";
            if (Settings.assistOnFullBag && IsBagFull) return "가방이 가득 참";
            return null;
        }

        public static MerchantNpc FindNearestMerchant(Vector3 from)
        {
            MerchantNpc best = null;
            float bestSqr = float.MaxValue;
            foreach (var npc in NpcInteractable.Active)
            {
                if (npc is not MerchantNpc m || m.Shop == null || m.Shop.Data == null) continue;
                float d = (m.Position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = m; }
            }
            return best;
        }

        // ---------------- 상태 문구 ----------------

        internal void SetStatus(string text)
        {
            _flashUntil = 0f;
            if (Status == text) return;
            Status = text;
            OnStatusChanged?.Invoke(text);
        }

        /// <summary>잠깐 보여 주고 사라지는 문구</summary>
        internal void Flash(string text, float seconds = 3f)
        {
            Status = text;
            _flashUntil = Time.time + seconds;
            OnStatusChanged?.Invoke(text);
        }

        internal void TickFlash()
        {
            if (_flashUntil > 0f && Time.time > _flashUntil) SetStatus("");
        }

        private void HandleDied()
        {
            if (IsOnTrip) Cancel("보조 모드 이동 취소");
        }
    }
}
