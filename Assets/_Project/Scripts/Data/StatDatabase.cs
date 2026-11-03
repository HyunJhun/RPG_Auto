using System;
using System.Collections.Generic;
using RPGAuto.Monster;
using UnityEngine;

namespace RPGAuto.Data
{
    [Serializable]
    public class PlayerLevelRow
    {
        public int level;
        public long requiredExp;
        public StatBlock stats;
    }

    [Serializable]
    public class MonsterRow
    {
        public string monsterId;
        public string displayName;
        public MonsterGrade grade;
        public int level;
        public StatBlock stats;
        public float moveSpeed = 3.5f;
        public float attackRange = 1.8f;
        public float attackInterval = 1.6f;
        public long expReward;
    }

    /// <summary>
    /// 엑셀(StatTable.xlsx)에서 임포트된 스탯 데이터. 직접 수정하지 말고 엑셀을 수정한다.
    /// 메뉴: RPGAuto > Import Stat Table
    /// </summary>
    [CreateAssetMenu(menuName = "RPGAuto/Data/Stat Database", fileName = "StatDatabase")]
    public class StatDatabase : ScriptableObject
    {
        [SerializeField] private List<PlayerLevelRow> playerLevels = new();
        [SerializeField] private List<MonsterRow> monsters = new();
        [SerializeField] private StatFormula statFormula = new();
        [SerializeField] private CombatFormula combatFormula = new();
        [SerializeField] private string sourceFile;
        [SerializeField] private string importedAt;

        private Dictionary<int, PlayerLevelRow> _levelMap;
        private Dictionary<string, MonsterRow> _monsterMap;

        public IReadOnlyList<PlayerLevelRow> PlayerLevels => playerLevels;
        public IReadOnlyList<MonsterRow> Monsters => monsters;
        public StatFormula StatFormula => statFormula;
        public CombatFormula CombatFormula => combatFormula;
        public int MaxPlayerLevel => playerLevels.Count > 0 ? playerLevels[playerLevels.Count - 1].level : 1;

        public PlayerLevelRow GetPlayerLevel(int level)
        {
            BuildMaps();
            if (_levelMap.TryGetValue(level, out var row)) return row;
            return playerLevels.Count > 0 ? playerLevels[Mathf.Clamp(level - 1, 0, playerLevels.Count - 1)] : null;
        }

        public MonsterRow GetMonster(string monsterId)
        {
            BuildMaps();
            if (!string.IsNullOrEmpty(monsterId) && _monsterMap.TryGetValue(monsterId, out var row)) return row;
            Debug.LogWarning($"[StatDatabase] MonsterId '{monsterId}' 를 찾을 수 없습니다.");
            return null;
        }

        private void BuildMaps()
        {
            if (_levelMap != null && _monsterMap != null) return;
            _levelMap = new Dictionary<int, PlayerLevelRow>();
            foreach (var r in playerLevels) _levelMap[r.level] = r;
            _monsterMap = new Dictionary<string, MonsterRow>();
            foreach (var r in monsters) _monsterMap[r.monsterId] = r;
        }

        private void OnEnable() { _levelMap = null; _monsterMap = null; }

#if UNITY_EDITOR
        public void EditorSetData(List<PlayerLevelRow> levels, List<MonsterRow> monsterRows,
                                  StatFormula stat, CombatFormula combat, string source)
        {
            playerLevels = levels;
            monsters = monsterRows;
            statFormula = stat;
            combatFormula = combat;
            sourceFile = source;
            importedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _levelMap = null;
            _monsterMap = null;
        }
#endif
    }
}
