using System;
using RPGAuto.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.UI
{
    /// <summary>스킬 창 목록 한 줄 (Prefab: UI_SkillRow)</summary>
    [RequireComponent(typeof(Button))]
    public class SkillRowView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text infoText;
        [SerializeField] private Text markText;
        [SerializeField] private GameObject selectedFrame;

        public SkillSlot Skill { get; private set; }
        public event Action<SkillRowView> OnClicked;

        private void Awake() => GetComponent<Button>().onClick.AddListener(() => OnClicked?.Invoke(this));

        public void Bind(SkillSlot skill, bool selected, string mark)
        {
            Skill = skill;
            var d = skill.Data;
            if (icon != null) icon.color = d.SlotColor;
            if (iconLabel != null) iconLabel.text = d.ShortLabel;
            if (nameText != null) nameText.text = d.DisplayName;
            if (infoText != null)
                infoText.text = $"{(d.RequiresTarget ? "Attack" : "Self")}  ·  MP {d.MpCost:0}  ·  CD {d.Cooldown:0.#}s";
            if (markText != null) markText.text = mark ?? string.Empty;
            if (selectedFrame != null) selectedFrame.SetActive(selected);
        }
    }
}
