using System.Collections.Generic;
using RPGAuto.Data;
using RPGAuto.Items;
using RPGAuto.Loot;
using RPGAuto.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static RPGAuto.EditorTools.UIBuildKit;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 장비/강화/무게 관련 데이터를 만들고 플레이어·드랍 테이블에 연결한다.
    /// 메뉴: RPGAuto > Build Equipment Data  (TestMapBuilder 실행 시 자동 호출, LootInventoryBuilder 이후)
    /// </summary>
    public static class EquipmentBuilder
    {
        public const string ItemFolder = Root + "/Data/Items";
        public const string EquipFolder = ItemFolder + "/Equipment";
        public const string EnhanceTablePath = ItemFolder + "/ENH_Default.asset";
        public const string ExpansionConfigPath = ItemFolder + "/INV_ExpansionConfig.asset";
        public const string ScrollPath = ItemFolder + "/IT_EnhanceScroll.asset";
        private const string DropFolder = Root + "/Data/DropTables";

        // id, 이름, 짧은 이름, 부위, 등급, 요구 레벨, 무게, 판매가, 능력치(ATK, DEF, ACC, HP, MP, MATK, MDEF)
        private static readonly (string id, string name, string label, EquipSlotType slot, ItemGrade grade, int lv, float w, long sell,
            float atk, float def, float acc, float hp, float mp, float matk, float mdef)[] Table =
        {
            ("WoodenSword", "Wooden Sword", "SWORD", EquipSlotType.Weapon, ItemGrade.Common, 1, 15f, 20, 6, 0, 1, 0, 0, 0, 0),
            ("ClothHood", "Cloth Hood", "HOOD", EquipSlotType.Helmet, ItemGrade.Common, 1, 5f, 10, 0, 2, 0, 10, 0, 0, 0),
            ("ClothRobe", "Cloth Robe", "ROBE", EquipSlotType.Armor, ItemGrade.Common, 1, 12f, 15, 0, 4, 0, 20, 0, 0, 1),
            ("ClothGloves", "Cloth Gloves", "GLOVE", EquipSlotType.Gloves, ItemGrade.Common, 1, 3f, 8, 0, 1, 2, 0, 0, 0, 0),
            ("ClothPants", "Cloth Pants", "PANTS", EquipSlotType.Pants, ItemGrade.Common, 1, 6f, 10, 0, 2, 0, 0, 0, 0, 0),
            ("ClothShoes", "Cloth Shoes", "SHOES", EquipSlotType.Boots, ItemGrade.Common, 1, 4f, 8, 0, 1, 0, 0, 0, 0, 0),
            ("TwineRing", "Twine Ring", "RING", EquipSlotType.Ring, ItemGrade.Common, 1, 0.2f, 10, 0, 0, 2, 0, 0, 0, 0),

            ("IronSword", "Iron Sword", "SWORD", EquipSlotType.Weapon, ItemGrade.Uncommon, 5, 20f, 120, 14, 0, 3, 0, 0, 0, 0),
            ("IronHelm", "Iron Helm", "HELM", EquipSlotType.Helmet, ItemGrade.Uncommon, 5, 12f, 80, 0, 5, 0, 25, 0, 0, 0),
            ("IronArmor", "Iron Armor", "ARMOR", EquipSlotType.Armor, ItemGrade.Uncommon, 5, 28f, 150, 0, 10, 0, 50, 0, 0, 2),
            ("IronGauntlets", "Iron Gauntlets", "GAUNT", EquipSlotType.Gloves, ItemGrade.Uncommon, 5, 8f, 70, 2, 3, 0, 0, 0, 0, 0),
            ("IronGreaves", "Iron Greaves", "GREAV", EquipSlotType.Pants, ItemGrade.Uncommon, 5, 14f, 80, 0, 5, 0, 0, 0, 0, 0),
            ("IronBoots", "Iron Boots", "BOOTS", EquipSlotType.Boots, ItemGrade.Uncommon, 5, 10f, 70, 0, 3, 0, 0, 0, 0, 0),
            ("WoolCloak", "Wool Cloak", "CLOAK", EquipSlotType.Cloak, ItemGrade.Uncommon, 5, 6f, 80, 0, 3, 0, 0, 0, 0, 3),
            ("CopperRing", "Copper Ring", "RING", EquipSlotType.Ring, ItemGrade.Uncommon, 5, 0.5f, 90, 0, 0, 4, 0, 0, 0, 0),
            ("LeatherBelt", "Leather Belt", "BELT", EquipSlotType.Belt, ItemGrade.Uncommon, 5, 3f, 60, 0, 0, 0, 30, 0, 0, 0),

            ("KnightSword", "Knight Sword", "SWORD", EquipSlotType.Weapon, ItemGrade.Rare, 10, 22f, 600, 26, 0, 6, 0, 0, 0, 0),
            ("KnightArmor", "Knight Armor", "ARMOR", EquipSlotType.Armor, ItemGrade.Rare, 10, 32f, 650, 0, 18, 0, 90, 0, 0, 4),
            ("SilverRing", "Silver Ring", "RING", EquipSlotType.Ring, ItemGrade.Rare, 10, 0.5f, 500, 4, 0, 6, 0, 0, 0, 0),
            ("SilverNecklace", "Silver Necklace", "NECK", EquipSlotType.Necklace, ItemGrade.Rare, 10, 1f, 500, 0, 0, 0, 0, 40, 5, 0),
            ("SilverEarring", "Silver Earring", "EARR", EquipSlotType.Earring, ItemGrade.Rare, 10, 0.3f, 450, 0, 0, 8, 0, 0, 0, 0),

            ("ChiefAxe", "Chief's Axe", "AXE", EquipSlotType.Weapon, ItemGrade.Epic, 12, 30f, 2000, 40, 0, 2, 0, 0, 0, 0),
            ("ShadowCloak", "Shadow Cloak", "CLOAK", EquipSlotType.Cloak, ItemGrade.Epic, 12, 8f, 1800, 0, 10, 0, 0, 0, 0, 10),

            ("AncientBlade", "Ancient Blade", "BLADE", EquipSlotType.Weapon, ItemGrade.Legendary, 15, 25f, 8000, 60, 0, 12, 0, 0, 0, 0),
        };

        [MenuItem("RPGAuto/Build Equipment Data")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[EquipmentBuilder] 완료");
        }

        public static EquipmentData Load(string id) => AssetDatabase.LoadAssetAtPath<EquipmentData>($"{EquipFolder}/EQ_{id}.asset");
        public static ItemData LoadItem(string asset) => AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemFolder}/{asset}.asset");

        public static void ApplyToOpenScene()
        {
            EnsureFolder(EquipFolder);

            // ---------- 기존 아이템: 등급/무게 ----------
            SetGradeWeight("IT_HpPotion", ItemGrade.Common, 1f);
            SetGradeWeight("IT_MpPotion", ItemGrade.Common, 1f);
            SetGradeWeight("IT_HpPotionGreater", ItemGrade.Uncommon, 1.5f);
            SetGradeWeight("IT_GoblinEar", ItemGrade.Common, 0.5f);
            SetGradeWeight("IT_ChiefSeal", ItemGrade.Rare, 2f);
            // 상점 [자동 담기] 대상: 팔기 위해 모으는 전리품
            foreach (var junk in new[] { "IT_GoblinEar", "IT_ChiefSeal" })
            {
                var item = LoadItem(junk);
                if (item != null) Set(item, ("autoSell", true));
            }
            SetGradeWeight("IT_AncientRelic", ItemGrade.Legendary, 5f);

            var scroll = LoadOrCreate<ItemData>(ScrollPath);
            Set(scroll, ("itemId", "I201"), ("displayName", "Enhancement Scroll"), ("shortLabel", "SCROLL"),
                ("slotColor", new Color(0.8f, 0.72f, 0.45f)), ("category", (int)ItemCategory.Material),
                ("grade", (int)ItemGrade.Uncommon), ("weight", 0.2f), ("sellPrice", 50L),
                ("description", "Used to enhance equipment. Higher enhancement levels require more scrolls."));

            // ---------- 장비 ----------
            var equips = new Dictionary<string, EquipmentData>();
            foreach (var e in Table)
            {
                var data = LoadOrCreate<EquipmentData>($"{EquipFolder}/EQ_{e.id}.asset");
                Set(data, ("itemId", "E" + e.id), ("displayName", e.name), ("shortLabel", e.label),
                    ("slotColor", SlotColor(e.slot)), ("category", (int)ItemCategory.Equipment), ("grade", (int)e.grade),
                    ("weight", e.w), ("sellPrice", e.sell), ("slotType", (int)e.slot), ("requiredLevel", e.lv),
                    ("enhanceBonusPerLevel", 0.1f), ("maxEnhance", 10),
                    ("description", $"{e.grade} {e.slot.ToString().ToLower()}. Each enhancement level adds 10% to its stats."),
                    ("stats.physicalAttack", e.atk), ("stats.physicalDefense", e.def), ("stats.accuracy", e.acc),
                    ("stats.maxHp", e.hp), ("stats.maxMp", e.mp), ("stats.magicAttack", e.matk), ("stats.magicDefense", e.mdef));
                equips[e.id] = data;
            }

            // ---------- 강화표 (+0 → +10) ----------
            var table = LoadOrCreate<EnhanceTable>(EnhanceTablePath);
            {
                (float rate, long gold, int scrolls, bool down)[] steps =
                {
                    (1f, 200, 1, false), (1f, 400, 1, false), (0.9f, 700, 1, false), (0.8f, 1000, 1, false),
                    (0.7f, 1500, 2, false), (0.6f, 2000, 2, false), (0.5f, 3000, 2, true), (0.4f, 4000, 3, true),
                    (0.3f, 6000, 3, true), (0.2f, 8000, 4, true),
                };
                var so = new SerializedObject(table);
                so.FindProperty("scrollItem").objectReferenceValue = scroll;
                var list = so.FindProperty("steps");
                list.arraySize = steps.Length;
                for (int i = 0; i < steps.Length; i++)
                {
                    var p = list.GetArrayElementAtIndex(i);
                    p.FindPropertyRelative("successRate").floatValue = steps[i].rate;
                    p.FindPropertyRelative("goldCost").longValue = steps[i].gold;
                    p.FindPropertyRelative("scrollCost").intValue = steps[i].scrolls;
                    p.FindPropertyRelative("downgradeOnFail").boolValue = steps[i].down;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(table);
            }

            // ---------- 확장 비용 ----------
            var expansion = LoadOrCreate<InventoryExpansionConfig>(ExpansionConfigPath);
            Set(expansion, ("maxCapacity", 200), ("goldPerSlot", 500L), ("weightStep", 100f), ("maxWeight", 10000f), ("goldPerWeightStep", 1000L));

            // ---------- 드랍 (장비/주문서 추가) ----------
            AppendDrops("DT_Goblin", (equips["ClothGloves"], 0.03f, 1, 1), (equips["ClothPants"], 0.03f, 1, 1),
                (equips["TwineRing"], 0.02f, 1, 1), (scroll, 0.05f, 1, 1));
            AppendDrops("DT_GoblinWarrior", (equips["IronHelm"], 0.03f, 1, 1), (equips["IronGauntlets"], 0.03f, 1, 1),
                (equips["IronGreaves"], 0.03f, 1, 1), (equips["IronBoots"], 0.03f, 1, 1), (equips["LeatherBelt"], 0.02f, 1, 1),
                (scroll, 0.1f, 1, 1));
            AppendDrops("DT_GoblinChief", (equips["IronSword"], 0.15f, 1, 1), (equips["IronArmor"], 0.1f, 1, 1),
                (equips["WoolCloak"], 0.1f, 1, 1), (equips["CopperRing"], 0.1f, 1, 1), (equips["KnightSword"], 0.03f, 1, 1),
                (equips["ChiefAxe"], 0.02f, 1, 1), (scroll, 0.6f, 1, 3));

            // ---------- 플레이어 ----------
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var go = player.gameObject;
            var inventory = go.GetComponent<PlayerInventory>();
            var equipment = go.GetComponent<PlayerEquipment>();
            if (equipment == null) equipment = go.AddComponent<PlayerEquipment>();

            var hp = LoadItem("IT_HpPotion");
            var mp = LoadItem("IT_MpPotion");
            var inv = new SerializedObject(inventory);
            inv.FindProperty("capacity").intValue = 60;
            inv.FindProperty("maxWeight").floatValue = 1200f;
            (Object item, int count)[] start = { (hp, 50), (mp, 30), (scroll, 10), (equips["IronSword"], 1), (equips["TwineRing"], 2) };
            var startList = inv.FindProperty("startItems");
            startList.arraySize = start.Length;
            for (int i = 0; i < start.Length; i++)
            {
                var e = startList.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("item").objectReferenceValue = start[i].item;
                e.FindPropertyRelative("count").intValue = start[i].count;
                e.FindPropertyRelative("enhanceLevel").intValue = 0;
            }
            inv.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(inventory);

            SetArray(equipment, "startEquipment", new Object[]
            {
                equips["WoodenSword"], equips["ClothHood"], equips["ClothRobe"],
                equips["ClothGloves"], equips["ClothPants"], equips["ClothShoes"]
            });

            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private static void SetGradeWeight(string asset, ItemGrade grade, float weight)
        {
            var item = LoadItem(asset);
            if (item != null) Set(item, ("grade", (int)grade), ("weight", weight));
        }

        /// <summary>부위별 아이콘 색 (기본 도형 대신 쓰는 임시 아이콘)</summary>
        private static Color SlotColor(EquipSlotType slot) => slot switch
        {
            EquipSlotType.Weapon => new Color(0.75f, 0.75f, 0.8f),
            EquipSlotType.Helmet or EquipSlotType.Armor or EquipSlotType.Pants => new Color(0.55f, 0.45f, 0.35f),
            EquipSlotType.Gloves or EquipSlotType.Boots or EquipSlotType.Belt => new Color(0.45f, 0.35f, 0.25f),
            EquipSlotType.Cloak => new Color(0.35f, 0.3f, 0.5f),
            _ => new Color(0.85f, 0.75f, 0.35f) // 장신구
        };

        /// <summary>드랍 테이블에 항목 추가 (같은 아이템이 있으면 값만 갱신)</summary>
        private static void AppendDrops(string table, params (ItemData item, float chance, int min, int max)[] drops)
        {
            var dt = AssetDatabase.LoadAssetAtPath<DropTable>($"{DropFolder}/{table}.asset");
            if (dt == null) { Debug.LogWarning($"[EquipmentBuilder] {table} 없음 — Build Loot & Inventory 먼저 실행"); return; }
            var so = new SerializedObject(dt);
            var list = so.FindProperty("items");
            foreach (var d in drops)
            {
                SerializedProperty target = null;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == d.item)
                        target = list.GetArrayElementAtIndex(i);
                if (target == null)
                {
                    list.arraySize++;
                    target = list.GetArrayElementAtIndex(list.arraySize - 1);
                }
                target.FindPropertyRelative("item").objectReferenceValue = d.item;
                target.FindPropertyRelative("chance").floatValue = d.chance;
                target.FindPropertyRelative("minCount").intValue = d.min;
                target.FindPropertyRelative("maxCount").intValue = d.max;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dt);
        }
    }
}
