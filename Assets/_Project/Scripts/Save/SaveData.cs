using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGAuto.Save
{
    /// <summary>JSON으로 저장되는 플레이어 진행 데이터 (JsonUtility 직렬화)</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string savedAt;

        public int level = 1;
        public long exp;
        public long gold;

        public int capacity;
        public float maxWeight;
        public List<SavedEntry> entries = new();
        public List<SavedQuickSlot> quickSlots = new();

        public bool hasPosition;
        public Vector3 position;
    }

    /// <summary>인벤토리 한 칸</summary>
    [Serializable]
    public class SavedEntry
    {
        public string itemId;
        public int count;
        /// <summary>장비 강화 수치</summary>
        public int enhance;
        /// <summary>장착 위치 (EquipPosition), 미장착이면 -1</summary>
        public int equipPosition = -1;
        public bool locked;
    }

    /// <summary>퀵슬롯 한 칸 (스킬이면 skillIndex, 아이템이면 itemId)</summary>
    [Serializable]
    public class SavedQuickSlot
    {
        public int index;
        public int skillIndex = -1;
        public string itemId;
        public bool autoUse;
    }
}
