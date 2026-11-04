using RPGAuto.InputHandling;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>
    /// 지정한 액션의 키를 현재 입력 방식에 맞게 표시한다 (키보드: Space/F/1, 게임패드: A/Y/RB, 터치: 숨김).
    /// 입력 방식 변경 이벤트를 구독(Observer)해 자동으로 바뀐다.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class KeyHintText : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler input;
        [Tooltip("RPGAutoInput의 액션 이름 (예: Attack, QuickSlot1)")]
        [SerializeField] private string actionName;

        private Text _text;

        private void Awake() => _text = GetComponent<Text>();

        private void Start()
        {
            if (input == null) return;
            input.OnInputSchemeChanged += Refresh;
            Refresh(input.CurrentScheme);
        }

        private void OnDestroy()
        {
            if (input != null) input.OnInputSchemeChanged -= Refresh;
        }

        private void Refresh(InputScheme scheme)
        {
            string label = input.GetBindingLabel(actionName, scheme);
            _text.text = label;
            _text.enabled = !string.IsNullOrEmpty(label);
        }
    }
}
