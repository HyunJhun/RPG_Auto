using System;
using System.Collections.Generic;
using System.IO;
using RPGAuto.Combat;
using RPGAuto.Items;
using RPGAuto.Player;
using RPGAuto.QuickSlots;
using RPGAuto.Skills;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace RPGAuto.Save
{
    /// <summary>
    /// 플레이어 진행 데이터(레벨/경험치/골드/인벤토리/장착/퀵슬롯/위치)를 JSON 파일로 저장·불러오기.
    /// - 시작 시: 다른 컴포넌트가 시작 아이템/장비를 지급한 뒤(Start 이후) 저장 파일이 있으면 덮어쓴다
    /// - 자동 저장: 일정 주기 + 백그라운드 전환/종료 시
    /// 전투 설정은 기존 CombatSettingsSaver(PlayerPrefs)가 따로 저장한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerSaveSystem : MonoBehaviour
    {
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private string fileName = "save_slot0.json";
        [Tooltip("자동 저장 주기(초). 0이면 주기 저장 안 함")]
        [SerializeField, Min(0f)] private float autoSaveInterval = 30f;

        private PlayerProgression _progression;
        private PlayerWallet _wallet;
        private PlayerInventory _inventory;
        private PlayerEquipment _equipment;
        private QuickSlotBar _quickSlots;
        private PlayerSkillController _skills;
        private CombatStats _stats;
        private float _nextAutoSave;
        private bool _resetting;

        public string FilePath => Path.Combine(Application.persistentDataPath, fileName);
        public bool HasSave => File.Exists(FilePath);
        public DateTime? LastSavedAt { get; private set; }

        /// <summary>저장/불러오기 결과 알림 (Observer) — (성공 여부, 메시지)</summary>
        public event Action<bool, string> OnSaveStatus;

        private void Awake()
        {
            _progression = GetComponent<PlayerProgression>();
            _wallet = GetComponent<PlayerWallet>();
            _inventory = GetComponent<PlayerInventory>();
            _equipment = GetComponent<PlayerEquipment>();
            _quickSlots = GetComponent<QuickSlotBar>();
            _skills = GetComponent<PlayerSkillController>();
            _stats = GetComponent<CombatStats>();
        }

        private void Start()
        {
            if (HasSave) Load();
            _nextAutoSave = Time.unscaledTime + autoSaveInterval;
        }

        private void Update()
        {
            if (autoSaveInterval <= 0f || Time.unscaledTime < _nextAutoSave) return;
            _nextAutoSave = Time.unscaledTime + autoSaveInterval;
            Save();
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();

        // ---------------- 저장 ----------------

        public bool Save()
        {
            if (_resetting) return false;
            try
            {
                var data = Capture();
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath); // 쓰는 도중 종료돼도 기존 파일이 깨지지 않게
                LastSavedAt = DateTime.Now;
                OnSaveStatus?.Invoke(true, $"Saved {LastSavedAt:HH:mm:ss}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerSaveSystem] 저장 실패: {e}");
                OnSaveStatus?.Invoke(false, "Save failed");
                return false;
            }
        }

        private SaveData Capture()
        {
            var data = new SaveData
            {
                savedAt = DateTime.Now.ToString("o"),
                level = _progression != null ? _progression.Level : 1,
                exp = _progression != null ? _progression.Exp : 0,
                gold = _wallet != null ? _wallet.Gold : 0,
                capacity = _inventory.Capacity,
                maxWeight = _inventory.MaxWeight,
                hasPosition = true,
                position = transform.position
            };

            foreach (var entry in _inventory.Entries)
            {
                var saved = new SavedEntry { itemId = entry.Item.ItemId, count = entry.Count };
                if (entry.IsEquipment)
                {
                    saved.enhance = entry.Equipment.EnhanceLevel;
                    var pos = _equipment != null ? _equipment.FindPosition(entry.Equipment) : null;
                    saved.equipPosition = pos.HasValue ? (int)pos.Value : -1;
                }
                data.entries.Add(saved);
            }

            if (_quickSlots != null)
            {
                foreach (var slot in _quickSlots.Slots)
                {
                    var saved = new SavedQuickSlot { index = slot.Index, autoUse = slot.IsAutoUse };
                    switch (slot.Action)
                    {
                        case SkillQuickAction s: saved.skillIndex = IndexOfSkill(s.Skill); break;
                        case ItemQuickAction i: saved.itemId = i.Item.ItemId; break;
                    }
                    data.quickSlots.Add(saved);
                }
            }
            return data;
        }

        // ---------------- 불러오기 ----------------

        public bool Load()
        {
            if (!HasSave) return false;
            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerSaveSystem] 저장 파일을 읽지 못했습니다: {e.Message}");
                OnSaveStatus?.Invoke(false, "Load failed");
                return false;
            }
            if (data == null || itemDatabase == null)
            {
                Debug.LogError("[PlayerSaveSystem] 저장 데이터 또는 ItemDatabase가 없습니다.");
                return false;
            }

            Apply(data);
            OnSaveStatus?.Invoke(true, "Loaded");
            return true;
        }

        private void Apply(SaveData data)
        {
            // 1) 레벨 (장비 착용 레벨 검사보다 먼저)
            _progression?.Restore(data.level, data.exp);
            _wallet?.SetGold(data.gold);

            // 2) 인벤토리 + 장착
            _equipment?.UnequipAll();
            _inventory.ClearForRestore(data.capacity, data.maxWeight);

            var toEquip = new List<(int pos, EquipmentInstance eq)>();
            foreach (var saved in data.entries)
            {
                var item = itemDatabase.Find(saved.itemId);
                if (item == null) { Debug.LogWarning($"[PlayerSaveSystem] 알 수 없는 아이템: {saved.itemId}"); continue; }

                if (item is EquipmentData eqData)
                {
                    var eq = new EquipmentInstance(eqData, saved.enhance);
                    _inventory.RestoreEquipment(eq);
                    if (saved.equipPosition >= 0) toEquip.Add((saved.equipPosition, eq));
                }
                else _inventory.RestoreStack(item, saved.count);
            }
            // 반지는 빈 칸 순서로 들어가므로 위치 순으로 장착한다
            toEquip.Sort((a, b) => a.pos.CompareTo(b.pos));
            foreach (var (_, eq) in toEquip) _equipment?.Equip(eq);

            // 3) 퀵슬롯
            if (_quickSlots != null && data.quickSlots.Count > 0)
            {
                foreach (var saved in data.quickSlots)
                {
                    var slot = _quickSlots.GetSlot(saved.index);
                    if (slot == null) continue;
                    IQuickSlotAction action = null;
                    if (saved.skillIndex >= 0 && _skills != null)
                    {
                        var skill = _skills.GetSlot(saved.skillIndex);
                        if (skill != null) action = new SkillQuickAction(skill);
                    }
                    else if (!string.IsNullOrEmpty(saved.itemId))
                    {
                        var item = itemDatabase.Find(saved.itemId);
                        if (item != null) action = new ItemQuickAction(item, _inventory);
                    }
                    slot.SetAction(action, saved.autoUse);
                }
            }

            // 4) 위치 (NavMesh 위로 보정)
            if (data.hasPosition && TryGetComponent(out NavMeshAgent agent)
                && NavMesh.SamplePosition(data.position, out var hit, 3f, NavMesh.AllAreas))
                agent.Warp(hit.position);

            _stats.ResetFull();
        }

        // ---------------- 초기화 ----------------

        /// <summary>저장 파일을 지우고 씬을 다시 불러와 처음 상태로 시작한다.</summary>
        public void DeleteSaveAndRestart()
        {
            _resetting = true; // 씬 언로드 중 OnApplicationQuit/자동 저장으로 다시 써지지 않게
            if (HasSave) File.Delete(FilePath);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private int IndexOfSkill(SkillSlot skill)
        {
            if (_skills == null || skill == null) return -1;
            var slots = _skills.Slots;
            for (int i = 0; i < slots.Count; i++) if (slots[i] == skill) return i;
            return -1;
        }
    }
}
