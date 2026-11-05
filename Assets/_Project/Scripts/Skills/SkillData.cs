using UnityEngine;

namespace RPGAuto.Skills
{
    public enum SkillTargetType
    {
        Enemy,
        Self
    }

    [CreateAssetMenu(menuName = "RPGAuto/Skills/Skill Data", fileName = "SK_NewSkill")]
    public class SkillData : ScriptableObject
    {
        [Header("Display")]
        [SerializeField] private string displayName = "Skill";
        [SerializeField] private string shortLabel = "SKILL";
        [SerializeField] private Color slotColor = Color.gray;
        [SerializeField, TextArea(2, 4)] private string description;

        [Header("Rule")]
        [SerializeField] private SkillTargetType targetType = SkillTargetType.Enemy;
        [SerializeField] private float cooldown = 1f;
        [SerializeField] private float mpCost;
        [SerializeField] private float range = 2f;
        [Tooltip("사용 후 다른 행동을 못하는 시간 (후딜)")]
        [SerializeField] private float actionLock = 0.5f;

        [Header("Effect (Strategy)")]
        [SerializeField] private SkillEffect effect;

        [Header("Auto Use Default")]
        [SerializeField] private AutoUseCondition defaultAutoUse = new();

        public string DisplayName => displayName;
        public string ShortLabel => shortLabel;
        public Color SlotColor => slotColor;
        public string Description => description;
        public SkillTargetType TargetType => targetType;
        public bool RequiresTarget => targetType == SkillTargetType.Enemy;
        public float Cooldown => cooldown;
        public float MpCost => mpCost;
        public float Range => range;
        public float ActionLock => actionLock;
        public SkillEffect Effect => effect;
        public AutoUseCondition DefaultAutoUse => defaultAutoUse;
    }
}
