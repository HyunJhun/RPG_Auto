using System;
using System.Collections.Generic;
using System.Globalization;
using RPGAuto.Data;
using RPGAuto.Monster;
using UnityEditor;
using UnityEngine;

namespace RPGAuto.EditorTools.Data
{
    /// <summary>
    /// StatTable.xlsx → StatDatabase.asset 변환.
    /// 1행 = 키(열 이름), 2행 = 설명(무시), 3행~ = 데이터. 첫 칸이 비었거나 '#'이면 무시.
    /// 엑셀을 저장하면 AssetPostprocessor가 자동으로 다시 임포트한다.
    /// </summary>
    public static class StatTableImporter
    {
        public const string ExcelPath = "Assets/_Project/Data/Excel/StatTable.xlsx";
        public const string DatabasePath = "Assets/_Project/Data/StatDatabase.asset";

        [MenuItem("RPGAuto/Import Stat Table")]
        public static void ImportMenu() => Import(true);

        public static StatDatabase Import(bool log)
        {
            var sheets = XlsxReader.ReadAllSheets(ExcelPath);
            var errors = new List<string>();

            var levels = ParsePlayerLevels(Require(sheets, "PlayerLevel"), errors);
            var monsters = ParseMonsters(Require(sheets, "Monster"), errors);
            var statFormula = ParseStatFormula(Require(sheets, "StatFormula"), errors);
            var combatFormula = ParseCombatFormula(Require(sheets, "CombatFormula"), errors);

            var db = AssetDatabase.LoadAssetAtPath<StatDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<StatDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.EditorSetData(levels, monsters, statFormula, combatFormula, ExcelPath);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            foreach (var e in errors) Debug.LogWarning("[StatTable] " + e);
            if (log)
                Debug.Log($"[StatTable] 임포트 완료: 플레이어 레벨 {levels.Count}개, 몬스터 {monsters.Count}종 (경고 {errors.Count}건)");
            return AssetDatabase.LoadAssetAtPath<StatDatabase>(DatabasePath);
        }

        // ---------------- Sheets ----------------

        private static List<PlayerLevelRow> ParsePlayerLevels(Table t, List<string> errors)
        {
            var list = new List<PlayerLevelRow>();
            foreach (var r in t.Rows)
            {
                var row = new PlayerLevelRow
                {
                    level = t.Int(r, "Level"),
                    requiredExp = t.Long(r, "RequiredExp"),
                    stats = t.Stats(r),
                };
                list.Add(row);
            }
            list.Sort((a, b) => a.level.CompareTo(b.level));
            for (int i = 0; i < list.Count; i++)
                if (list[i].level != i + 1) errors.Add($"PlayerLevel: 레벨이 1부터 연속되지 않습니다 (행 레벨 {list[i].level}).");
            return list;
        }

        private static List<MonsterRow> ParseMonsters(Table t, List<string> errors)
        {
            var list = new List<MonsterRow>();
            var ids = new HashSet<string>();
            foreach (var r in t.Rows)
            {
                string id = t.Str(r, "MonsterId");
                if (!ids.Add(id)) errors.Add($"Monster: MonsterId '{id}' 중복");

                string gradeText = t.Str(r, "Grade");
                if (!Enum.TryParse(gradeText, true, out MonsterGrade grade))
                {
                    errors.Add($"Monster {id}: Grade '{gradeText}' 를 알 수 없어 Normal로 처리");
                    grade = MonsterGrade.Normal;
                }

                list.Add(new MonsterRow
                {
                    monsterId = id,
                    displayName = t.Str(r, "Name"),
                    grade = grade,
                    level = t.Int(r, "Level"),
                    stats = t.Stats(r),
                    moveSpeed = t.Float(r, "MoveSpeed", 3.5f),
                    attackRange = t.Float(r, "AttackRange", 1.8f),
                    attackInterval = t.Float(r, "AttackInterval", 1.6f),
                    expReward = t.Long(r, "ExpReward"),
                });
            }
            return list;
        }

        private static StatFormula ParseStatFormula(Table t, List<string> errors)
        {
            var f = new StatFormula();
            foreach (var r in t.Rows)
            {
                string key = t.Str(r, "Primary").ToUpperInvariant();
                var c = new StatFormula.Coefficients
                {
                    maxHp = t.Float(r, "MaxHP"),
                    maxMp = t.Float(r, "MaxMP"),
                    hpRegen = t.Float(r, "HpRegen"),
                    mpRegen = t.Float(r, "MpRegen"),
                    physicalAttack = t.Float(r, "PhysicalAttack"),
                    magicAttack = t.Float(r, "MagicAttack"),
                    accuracy = t.Float(r, "Accuracy"),
                    physicalDefense = t.Float(r, "PhysicalDefense"),
                    magicDefense = t.Float(r, "MagicDefense"),
                };
                switch (key)
                {
                    case "STR": f.str = c; break;
                    case "DEX": f.dex = c; break;
                    case "LUK": f.luk = c; break;
                    case "INT": f.intelligence = c; break;
                    default: errors.Add($"StatFormula: 알 수 없는 1차 스탯 '{key}'"); break;
                }
            }
            return f;
        }

        private static CombatFormula ParseCombatFormula(Table t, List<string> errors)
        {
            var f = new CombatFormula();
            foreach (var r in t.Rows)
            {
                string key = t.Str(r, "Key");
                float v = t.Float(r, "Value");
                switch (key)
                {
                    case "HitBase": f.hitBase = v; break;
                    case "AccuracyPerLevel": f.accuracyPerLevel = v; break;
                    case "HitPerAccuracy": f.hitPerAccuracy = v; break;
                    case "HitMin": f.hitMin = v; break;
                    case "HitMax": f.hitMax = v; break;
                    case "DefenseConstant": f.defenseConstant = v; break;
                    case "DamageVariance": f.damageVariance = v; break;
                    default: errors.Add($"CombatFormula: 알 수 없는 키 '{key}'"); break;
                }
            }
            return f;
        }

        private static Table Require(Dictionary<string, List<string[]>> sheets, string name)
        {
            if (!sheets.TryGetValue(name, out var rows))
                throw new Exception($"[StatTable] '{name}' 시트가 없습니다: {ExcelPath}");
            return new Table(name, rows);
        }

        // ---------------- Table helper ----------------

        private class Table
        {
            private readonly string _name;
            private readonly Dictionary<string, int> _columns = new(StringComparer.OrdinalIgnoreCase);
            public readonly List<string[]> Rows = new();

            public Table(string name, List<string[]> raw)
            {
                _name = name;
                if (raw.Count == 0) return;
                var header = raw[0];
                for (int i = 0; i < header.Length; i++)
                    if (!string.IsNullOrWhiteSpace(header[i])) _columns[header[i].Trim()] = i;

                for (int i = 2; i < raw.Count; i++) // 0: 키, 1: 설명
                {
                    var r = raw[i];
                    if (r.Length == 0 || string.IsNullOrWhiteSpace(r[0]) || r[0].TrimStart().StartsWith("#")) continue;
                    Rows.Add(r);
                }
            }

            public string Str(string[] row, string column)
            {
                if (!_columns.TryGetValue(column, out int i)) throw new Exception($"[StatTable] {_name} 시트에 '{column}' 열이 없습니다.");
                return i < row.Length ? row[i].Trim() : string.Empty;
            }

            public float Float(string[] row, string column, float fallback = 0f)
            {
                if (!_columns.ContainsKey(column)) return fallback;
                string s = Str(row, column);
                return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
            }

            public int Int(string[] row, string column) => Mathf.RoundToInt(Float(row, column));
            public long Long(string[] row, string column) => (long)Math.Round(Float(row, column));

            public StatBlock Stats(string[] r) => new()
            {
                maxHp = Float(r, "MaxHP"),
                maxMp = Float(r, "MaxMP"),
                hpRegen = Float(r, "HpRegen"),
                mpRegen = Float(r, "MpRegen"),
                str = Float(r, "STR"),
                dex = Float(r, "DEX"),
                luk = Float(r, "LUK"),
                intelligence = Float(r, "INT"),
                physicalAttack = Float(r, "PhysicalAttack"),
                magicAttack = Float(r, "MagicAttack"),
                accuracy = Float(r, "Accuracy"),
                physicalDefense = Float(r, "PhysicalDefense"),
                magicDefense = Float(r, "MagicDefense"),
            };
        }
    }

    /// <summary>엑셀 저장 시 자동 재임포트</summary>
    public class StatTablePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!string.Equals(path, StatTableImporter.ExcelPath, StringComparison.OrdinalIgnoreCase)) continue;
                // 임포트 콜백 안에서 에셋을 만들지 않도록 다음 틱으로 미룬다
                EditorApplication.delayCall += () =>
                {
                    try { StatTableImporter.Import(true); }
                    catch (Exception e) { Debug.LogError("[StatTable] 자동 임포트 실패: " + e.Message); }
                };
                return;
            }
        }
    }
}
