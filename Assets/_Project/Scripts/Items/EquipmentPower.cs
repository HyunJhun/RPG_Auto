using RPGAuto.Data;

namespace RPGAuto.Items
{
    /// <summary>
    /// 장비 비교용 전투력 점수. 능력치마다 가중치를 곱해 합산한다.
    /// (정확한 DPS가 아니라 "더 좋은 장비" 표시를 위한 근사치)
    /// </summary>
    public static class EquipmentPower
    {
        public static float Score(in StatBlock s) =>
            s.physicalAttack * 1f + s.magicAttack * 1f +
            s.physicalDefense * 0.8f + s.magicDefense * 0.6f +
            s.accuracy * 0.5f +
            s.maxHp * 0.1f + s.maxMp * 0.05f +
            (s.hpRegen + s.mpRegen) * 2f +
            (s.str + s.dex + s.luk + s.intelligence) * 1f;

        public static float Score(EquipmentInstance eq) => eq == null ? 0f : Score(eq.Stats);
    }
}
