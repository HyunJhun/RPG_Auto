using UnityEngine;

namespace RPGAuto.Player
{
    /// <summary>
    /// 전투 설정(AutoCombatSettings)을 PlayerPrefs에 저장/불러오기.
    /// 시작 시 인스펙터 값을 기본값으로 기억해 두고 초기화에 사용한다.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    [DefaultExecutionOrder(-50)] // 다른 컴포넌트의 Start 전에 불러오기
    public class CombatSettingsSaver : MonoBehaviour
    {
        private const string Key = "RPGAuto.CombatSettings.v1";

        private PlayerCombat _combat;
        private string _defaultJson;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
            _defaultJson = JsonUtility.ToJson(_combat.Settings);

            if (PlayerPrefs.HasKey(Key))
                JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(Key), _combat.Settings); // 참조 유지한 채 값만 덮어쓴다
        }

        public void Save()
        {
            if (_combat == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(_combat.Settings));
            PlayerPrefs.Save();
        }

        public void ResetToDefault()
        {
            JsonUtility.FromJsonOverwrite(_defaultJson, _combat.Settings);
            Save();
        }

        // 스킬 창 등 다른 곳에서 바꾼 값도 종료/백그라운드 전환 시 저장
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();
    }
}
