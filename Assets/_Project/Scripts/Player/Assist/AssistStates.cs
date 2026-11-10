using System.Collections.Generic;
using RPGAuto.Core;
using RPGAuto.Shop;
using UnityEngine;
using UnityEngine.AI;

namespace RPGAuto.Player.Assist
{
    /// <summary>감시: 자동 전투 중 물약/가방 상태를 주기적으로 검사해 귀환을 시작한다.</summary>
    public class AssistMonitorState : IState
    {
        private readonly AssistController _a;
        private float _nextCheck;
        private float _suppressUntil;

        public AssistMonitorState(AssistController assist) => _a = assist;

        public void SuppressFor(float seconds) => _suppressUntil = Time.time + seconds;

        public void Enter() => _nextCheck = Time.time + _a.CheckInterval;

        public void Tick(float deltaTime)
        {
            _a.TickFlash();
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + _a.CheckInterval;

            var p = _a.Player;
            if (!_a.IsAssistEnabled || !p.Combat.IsAutoEnabled || p.IsDead || Time.time < _suppressUntil) return;

            string reason = _a.GetTripReason();
            if (reason == null) return;

            var merchant = AssistController.FindNearestMerchant(p.transform.position);
            if (merchant == null)
            {
                _a.Flash("근처에 상인이 없습니다");
                SuppressFor(_a.CooldownAfterTrip);
                return;
            }

            _a.Merchant = merchant;
            _a.HuntAnchor = p.Combat.Anchor;
            _a.StateMachine.ChangeState(_a.ToMerchantState);
            _a.Flash(reason + " - 상인에게 이동합니다", 2f);
        }

        public void Exit() { }
    }

    /// <summary>
    /// 길찾기 이동 공통: PlayerController의 AutoMoveState로 목적지까지 걷는다.
    /// 플레이어가 직접 이동하면 왕복을 취소한다.
    /// </summary>
    public abstract class AssistTravelState : IState
    {
        protected readonly AssistController A;
        private float _repathAt;

        protected AssistTravelState(AssistController assist) => A = assist;

        protected abstract Vector3 Destination { get; }
        protected abstract string StatusText { get; }
        protected abstract bool HasArrived(Vector3 position);
        protected abstract void OnArrived();

        public void Enter()
        {
            var p = A.Player;
            p.Combat.SetTarget(null);
            p.Combat.SetAutoCombat(false);
            A.SetStatus(StatusText);
            MoveTo();
        }

        public void Tick(float deltaTime)
        {
            A.TickFlash();
            if (string.IsNullOrEmpty(A.Status)) A.SetStatus(StatusText);

            var p = A.Player;
            if (HasArrived(p.transform.position)) { OnArrived(); return; }

            if (p.HasMoveInput || p.StateMachine.Current == p.ManualMoveState) { A.Cancel("보조 모드 이동 취소"); return; }

            // 다른 행동(탭 이동 도착, 피격 대응 등)으로 이동이 끊겼으면 다시 걷는다
            if (p.StateMachine.Current != p.AutoMoveState && Time.time >= _repathAt) MoveTo();
        }

        public void Exit() { }

        private void MoveTo()
        {
            _repathAt = Time.time + 0.5f;
            var p = A.Player;
            if (!NavMesh.SamplePosition(Destination, out var hit, 4f, NavMesh.AllAreas)) { A.Cancel("길을 찾을 수 없습니다"); return; }
            p.AutoMoveState.SetDestination(hit.position);
            if (p.StateMachine.Current != p.AutoMoveState) p.StateMachine.ChangeState(p.AutoMoveState);
        }
    }

    /// <summary>상인에게 이동</summary>
    public class AssistToMerchantState : AssistTravelState
    {
        public AssistToMerchantState(AssistController assist) : base(assist) { }

        protected override Vector3 Destination => A.Merchant.Position;
        protected override string StatusText => $"{A.Merchant.DisplayName}에게 이동 중...";
        protected override bool HasArrived(Vector3 position) => A.Merchant.IsInRange(position);
        protected override void OnArrived() => A.StateMachine.ChangeState(A.TradeState);
    }

    /// <summary>상인 앞: 자동 판매(autoSell 전리품) → 일괄 구매(restockTarget까지)</summary>
    public class AssistTradeState : IState
    {
        private readonly AssistController _a;
        private float _doneAt;
        private bool _traded;

        public AssistTradeState(AssistController assist) => _a = assist;

        public void Enter()
        {
            var p = _a.Player;
            if (p.StateMachine.Current != p.IdleState) p.StateMachine.ChangeState(p.IdleState);
            _a.SetStatus("거래 중...");
            _doneAt = Time.time + _a.TradeDuration;
            _traded = false;
        }

        public void Tick(float deltaTime)
        {
            var p = _a.Player;
            p.RotateTowards(_a.Merchant.Position - p.transform.position, deltaTime);
            if (p.HasMoveInput) { _a.Cancel("보조 모드 이동 취소"); return; }
            if (_traded || Time.time < _doneAt) return;

            _traded = true;
            string summary = Trade();
            _a.StateMachine.ChangeState(_a.ReturnState);
            _a.Flash(summary, 3f);
        }

        public void Exit() { }

        private string Trade()
        {
            var shop = _a.Merchant.Shop.Data;
            var inv = _a.Inventory;
            var wallet = _a.Wallet;

            var settings = _a.TradeSettings != null ? _a.TradeSettings.Settings : null;

            // 1) 판매: 자동 거래 설정(분류·등급 규칙)에 맞는 아이템 전부
            var sells = new List<SellLine>();
            foreach (var line in AutoTradeService.SelectSell(settings, inv, _a.Equipment, shop))
                if (ShopService.CheckSell(line, inv, _a.Equipment) == ShopResult.Ok) sells.Add(line);
            long earned = ShopService.TotalSell(sells);
            if (sells.Count > 0 && ShopService.Sell(sells, wallet, inv, _a.Equipment) != ShopResult.Ok) earned = 0;

            // 2) 구매: 목표 수량까지, 살 수 있는 만큼 (골드/무게/칸 검사를 통과할 때까지 수량을 줄인다)
            long spent = 0;
            int bought = 0;
            var single = new BuyLine[1];
            foreach (var product in shop.Products)
            {
                // 2) 구매: 자동 거래 설정의 목표 수량까지
                int need = AutoTradeService.NeedToBuy(settings, product, inv);
                if (need <= 0) continue;

                long unit = ShopService.UnitBuyPrice(product, shop.TaxRate);
                int qty = Mathf.Min(need, ShopService.MaxAffordable(unit, wallet));
                while (qty > 0)
                {
                    single[0] = new BuyLine { Product = product, Quantity = qty };
                    if (ShopService.CheckBuy(single, shop.TaxRate, wallet, inv) == ShopResult.Ok) break;
                    qty = qty > 8 ? qty * 3 / 4 : qty - 1;
                }
                if (qty <= 0) continue;

                long cost = ShopService.TotalCost(single, shop.TaxRate);
                if (ShopService.Buy(single, shop.TaxRate, wallet, inv) == ShopResult.Ok) { spent += cost; bought += qty; }
            }

            if (earned == 0 && bought == 0) return "거래할 것이 없습니다 (골드 부족?)";
            return $"판매 +{earned:N0}G / 구매 {bought}개 -{spent:N0}G";
        }
    }

    /// <summary>사냥하던 자리로 돌아와 자동 전투 재개</summary>
    public class AssistReturnState : AssistTravelState
    {
        private const float ArriveRadius = 1.5f;

        public AssistReturnState(AssistController assist) : base(assist) { }

        protected override Vector3 Destination => A.HuntAnchor;
        protected override string StatusText => "사냥터로 복귀 중...";

        protected override bool HasArrived(Vector3 position)
        {
            var d = position - A.HuntAnchor;
            d.y = 0f;
            return d.sqrMagnitude <= ArriveRadius * ArriveRadius;
        }

        protected override void OnArrived()
        {
            A.MonitorState.SuppressFor(A.CooldownAfterTrip);
            A.StateMachine.ChangeState(A.MonitorState);
            A.Player.Combat.SetAutoCombat(true);
            A.Flash("자동 전투 재개", 2f);
        }
    }
}
