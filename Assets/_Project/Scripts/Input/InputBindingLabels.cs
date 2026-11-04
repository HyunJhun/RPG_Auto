using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace RPGAuto.InputHandling
{
    /// <summary>현재 사용 중인 입력 방식 (UI 키 표시 기준)</summary>
    public enum InputScheme
    {
        KeyboardMouse,
        Gamepad,
        Touch
    }

    /// <summary>
    /// 액션 바인딩 → UI에 표시할 키 이름.
    /// 키 이름은 바인딩에서 읽으므로 에셋에서 키를 바꾸면 UI도 따라 바뀐다.
    /// </summary>
    public static class InputBindingLabels
    {
        // 게임패드는 Xbox 표기로 짧게
        private static readonly Dictionary<string, string> GamepadNames = new()
        {
            { "buttonSouth", "A" }, { "buttonEast", "B" }, { "buttonWest", "X" }, { "buttonNorth", "Y" },
            { "leftShoulder", "LB" }, { "rightShoulder", "RB" },
            { "leftTrigger", "LT" }, { "rightTrigger", "RT" },
            { "leftStick", "LS" }, { "rightStick", "RS" },
            { "leftStickPress", "LS" }, { "rightStickPress", "RS" },
            { "select", "SELECT" }, { "start", "START" },
            { "dpad/up", "D↑" }, { "dpad/down", "D↓" }, { "dpad/left", "D←" }, { "dpad/right", "D→" },
        };

        public static string Get(InputAction action, InputScheme scheme)
        {
            if (action == null || scheme == InputScheme.Touch) return string.Empty;

            string devicePrefix = scheme == InputScheme.Gamepad ? "<Gamepad>/" : "<Keyboard>/";
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite) continue;

                string path = binding.effectivePath; // 리바인딩(override) 반영
                if (string.IsNullOrEmpty(path) || !path.StartsWith(devicePrefix)) continue;

                if (scheme == InputScheme.Gamepad)
                {
                    string control = path.Substring(devicePrefix.Length);
                    return GamepadNames.TryGetValue(control, out var name)
                        ? name
                        : InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                }

                return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
            return string.Empty;
        }
    }
}
