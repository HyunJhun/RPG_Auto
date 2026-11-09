using RPGAuto.InputHandling;
using RPGAuto.Npc;
using RPGAuto.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 상호작용 버튼 (모바일 탭 / E키 안내). 범위 안에 NPC가 있을 때만 "TALK  NPC 이름"을 보여 준다.
    /// </summary>
    public class InteractPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private GameObject root;
        [SerializeField] private Button button;
        [SerializeField] private Text nameText;
        [SerializeField] private float checkInterval = 0.1f;

        private float _timer;

        private void Awake()
        {
            button?.onClick.AddListener(() => input?.PressInteract());
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = checkInterval;

            var npc = player != null && !player.IsDead ? NpcInteractable.FindNearestInRange(player.transform.position) : null;
            // 이미 창이 열려 있으면(상점 등) 버튼을 숨긴다
            bool show = npc != null && WindowManager.OpenCount == 0;
            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (show && nameText != null) nameText.text = npc.DisplayName;
        }
    }
}
