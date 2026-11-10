using RPGAuto.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>설정 창의 저장 / 저장 삭제 버튼과 저장 상태 표시</summary>
    public class SaveButtonsView : MonoBehaviour
    {
        [SerializeField] private PlayerSaveSystem saveSystem;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Text statusText;
        [Tooltip("삭제 버튼을 한 번 누르면 확인 문구로 바뀌고, 이 시간 안에 다시 눌러야 삭제된다")]
        [SerializeField] private float confirmWindow = 3f;

        private Text _deleteLabel;
        private string _deleteDefault;
        private float _confirmUntil = -1f;

        private void Awake()
        {
            _deleteLabel = deleteButton != null ? deleteButton.GetComponentInChildren<Text>() : null;
            _deleteDefault = _deleteLabel != null ? _deleteLabel.text : "";
            if (saveButton != null) saveButton.onClick.AddListener(() => saveSystem.Save());
            if (deleteButton != null) deleteButton.onClick.AddListener(HandleDelete);
        }

        private void OnEnable()
        {
            if (saveSystem != null) saveSystem.OnSaveStatus += HandleStatus;
            RefreshStatus();
        }

        private void OnDisable()
        {
            if (saveSystem != null) saveSystem.OnSaveStatus -= HandleStatus;
            ResetConfirm();
        }

        private void Update()
        {
            if (_confirmUntil > 0f && Time.unscaledTime > _confirmUntil) ResetConfirm();
        }

        private void HandleDelete()
        {
            if (_confirmUntil < 0f)
            {
                _confirmUntil = Time.unscaledTime + confirmWindow;
                if (_deleteLabel != null) _deleteLabel.text = "한 번 더 누르면 삭제";
                return;
            }
            saveSystem.DeleteSaveAndRestart();
        }

        private void ResetConfirm()
        {
            _confirmUntil = -1f;
            if (_deleteLabel != null) _deleteLabel.text = _deleteDefault;
        }

        private void HandleStatus(bool ok, string message)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = ok ? new Color(0.6f, 0.85f, 0.6f) : new Color(1f, 0.45f, 0.4f);
        }

        private void RefreshStatus()
        {
            if (statusText == null || saveSystem == null) return;
            statusText.color = new Color(0.6f, 0.6f, 0.65f);
            statusText.text = saveSystem.LastSavedAt.HasValue ? $"저장됨 {saveSystem.LastSavedAt:HH:mm:ss}"
                : saveSystem.HasSave ? "저장 파일 있음" : "저장 기록 없음";
        }
    }
}
