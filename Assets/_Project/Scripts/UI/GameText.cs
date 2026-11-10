using RPGAuto.Gacha;
using RPGAuto.Items;
using RPGAuto.Monster;

namespace RPGAuto.UI
{
    /// <summary>
    /// 화면에 표시하는 enum 값의 한글 이름.
    /// enum 이름(코드 식별자)을 그대로 출력하지 않고 반드시 여기를 거친다.
    /// </summary>
    public static class GameText
    {
        public static string Grade(ItemGrade grade) => grade switch
        {
            ItemGrade.Uncommon => "고급",
            ItemGrade.Rare => "희귀",
            ItemGrade.Epic => "영웅",
            ItemGrade.Legendary => "전설",
            _ => "일반"
        };

        public static string Grade(GachaGrade grade) => grade switch
        {
            GachaGrade.Rare => "희귀",
            GachaGrade.Epic => "영웅",
            GachaGrade.Legendary => "전설",
            _ => "일반"
        };

        public static string Grade(MonsterGrade grade) => grade switch
        {
            MonsterGrade.Elite => "정예",
            MonsterGrade.Boss => "보스",
            _ => "일반"
        };

        public static string Slot(EquipSlotType slot) => slot switch
        {
            EquipSlotType.Weapon => "무기",
            EquipSlotType.Helmet => "투구",
            EquipSlotType.Armor => "갑옷",
            EquipSlotType.Cloak => "망토",
            EquipSlotType.Gloves => "장갑",
            EquipSlotType.Pants => "바지",
            EquipSlotType.Boots => "신발",
            EquipSlotType.Belt => "허리띠",
            EquipSlotType.Necklace => "목걸이",
            EquipSlotType.Earring => "귀걸이",
            EquipSlotType.Ring => "반지",
            _ => slot.ToString()
        };

        public static string Category(ItemCategory category) => category switch
        {
            ItemCategory.Consumable => "소모품",
            ItemCategory.Material => "재료",
            ItemCategory.Equipment => "장비",
            _ => category.ToString()
        };
    }
}
