using System;
using UnityEngine;

namespace RPGAuto.Shop
{
    /// <summary>
    /// 플레이어의 자동 거래 설정 보관 + PlayerPrefs 저장/불러오기.
    /// 값이 바뀌면 NotifyChanged()로 저장하고 알린다 (Observer).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerTradeSettings : MonoBehaviour
    {
        private const string Key = "RPGAuto.TradeSettings.v1";

        [SerializeField] private AutoTradeSettings settings = new();

        public AutoTradeSettings Settings => settings;

        public event Action OnChanged;

        private void Awake()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            try { JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(Key), settings); }
            catch (Exception e) { Debug.LogWarning($"[PlayerTradeSettings] 설정을 읽지 못해 기본값을 씁니다: {e.Message}"); }
        }

        public void NotifyChanged()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(settings));
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public void ResetToDefault()
        {
            settings = new AutoTradeSettings();
            NotifyChanged();
        }
    }
}
