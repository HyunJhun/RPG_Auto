using RPGAuto.Combat.Targeting;
using RPGAuto.Player;
using RPGAuto.UI;
using UnityEngine;

namespace RPGAuto.Npc
{
    /// <summary>상인 NPC: 말을 걸면 상점 창을 열고, 플레이어가 멀어지면 닫는다.</summary>
    public class MerchantNpc : NpcInteractable
    {
        [SerializeField] private ShopWindowView shop;
        [Tooltip("상호작용 범위 + 이 거리만큼 멀어지면 상점을 닫는다")]
        [SerializeField] private float closeMargin = 3f;

        private PlayerController _customer;

        public ShopWindowView Shop => shop;

        public override void Interact(PlayerController player)
        {
            if (shop == null || player == null || player.IsDead) return;
            _customer = player;
            if (!shop.IsOpen) shop.SetOpen(true);

            // 상인을 바라보게
            Vector3 to = transform.position - player.transform.position;
            player.RotateTowards(to, 1f);
        }

        private void Update()
        {
            if (_customer == null) return;
            if (shop == null || !shop.IsOpen)
            {
                _customer = null;
                return;
            }
            if (TargetQuery.FlatDistance(_customer.transform.position, transform.position) > InteractRange + closeMargin)
            {
                shop.SetOpen(false);
                _customer = null;
            }
        }
    }
}
