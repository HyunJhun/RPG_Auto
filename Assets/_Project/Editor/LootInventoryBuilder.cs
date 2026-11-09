using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Loot;
using RPGAuto.Monster;
using RPGAuto.QuickSlots;
using RPGAuto.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static RPGAuto.EditorTools.UIBuildKit;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 드랍/전리품/인벤토리를 현재 씬(TestMap)에 구성한다.
    /// 메뉴: RPGAuto > Build Loot &amp; Inventory  (TestMapBuilder 실행 시 자동 호출)
    /// 다시 실행하면 기존 오브젝트를 지우고 새로 만든다.
    /// </summary>
    public static class LootInventoryBuilder
    {
        private const string ItemFolder = Root + "/Data/Items";
        private const string DropFolder = Root + "/Data/DropTables";
        private const string MonsterPrefabFolder = Root + "/Prefabs/Monster";

        [MenuItem("RPGAuto/Build Loot & Inventory")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[LootInventoryBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            EnsureFolder(DropFolder);
            EnsureFolder(Root + "/Prefabs/World");
            EnsureFolder(Root + "/Prefabs/UI");

            // ---------- Items ----------
            var hp = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/IT_HpPotion.asset");
            var mp = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/IT_MpPotion.asset");
            if (hp == null || mp == null) throw new System.Exception("물약 데이터가 없습니다. 먼저 RPGAuto > Build Test Map 을 실행하세요.");
            Set(hp, ("category", (int)ItemCategory.Consumable), ("description", "Restores 25% of max HP."));
            Set(mp, ("category", (int)ItemCategory.Consumable), ("description", "Restores 30% of max MP."));

            var ear = LoadOrCreate<ItemData>(ItemFolder + "/IT_GoblinEar.asset");
            Set(ear, ("itemId", "I101"), ("displayName", "Goblin Ear"), ("shortLabel", "EAR"),
                ("slotColor", new Color(0.55f, 0.65f, 0.3f)), ("category", (int)ItemCategory.Material),
                ("description", "A trophy cut from a goblin. Can be sold to merchants."));
            var seal = LoadOrCreate<ItemData>(ItemFolder + "/IT_ChiefSeal.asset");
            Set(seal, ("itemId", "I102"), ("displayName", "Chief's Seal"), ("shortLabel", "SEAL"),
                ("slotColor", new Color(0.6f, 0.3f, 0.85f)), ("category", (int)ItemCategory.Material),
                ("description", "Proof of defeating a Goblin Chief. Rare crafting material."));

            // ---------- Drop Tables ----------
            var dtGoblin = CreateDropTable("DT_Goblin", 3, 8,
                (hp, 0.15f, 1, 1), (ear, 0.4f, 1, 2));
            var dtWarrior = CreateDropTable("DT_GoblinWarrior", 10, 22,
                (hp, 0.2f, 1, 1), (mp, 0.15f, 1, 1), (ear, 0.5f, 1, 3));
            var dtChief = CreateDropTable("DT_GoblinChief", 80, 150,
                (hp, 1f, 2, 3), (mp, 0.6f, 1, 2), (ear, 1f, 3, 5), (seal, 0.5f, 1, 1));

            AssignDropTable("Monster_Goblin", dtGoblin);
            AssignDropTable("Monster_GoblinWarrior", dtWarrior);
            AssignDropTable("Monster_GoblinChief", dtChief);

            // ---------- Player ----------
            var player = Object.FindFirstObjectByType<Player.PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var go = player.gameObject;
            var wallet = go.GetComponent<PlayerWallet>();
            if (wallet == null) wallet = go.AddComponent<PlayerWallet>();
            var looter = go.GetComponent<PlayerLooter>();
            if (looter == null) looter = go.AddComponent<PlayerLooter>();
            var inventory = go.GetComponent<PlayerInventory>();
            var quickSlots = go.GetComponent<QuickSlotBar>();
            var input = Object.FindFirstObjectByType<PlayerInputHandler>();

            // ---------- Loot Manager ----------
            DestroyByName("LootManager");
            var managerGo = new GameObject("LootManager");
            Set(managerGo.AddComponent<LootManager>(), ("groundLootPrefab", CreateGroundLootPrefab()));

            // ---------- UI ----------
            var canvas = GameObject.Find("UI_Canvas")?.transform;
            if (canvas == null) throw new System.Exception("UI_Canvas가 없습니다.");
            DestroyChild(canvas, "UI_LootLog");
            var menuBar = canvas.Find("UI_MenuBar");

            BuildLootLog(canvas, looter);
            BuildMenuButton(menuBar);
            // 인벤토리 창은 InventoryUIBuilder가 만든다 (BAG 버튼 연결 포함)

            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        // ================= Data =================

        private static DropTable CreateDropTable(string name, int goldMin, int goldMax,
            params (ItemData item, float chance, int min, int max)[] drops)
        {
            var dt = LoadOrCreate<DropTable>($"{DropFolder}/{name}.asset");
            var so = new SerializedObject(dt);
            so.FindProperty("goldChance").floatValue = 1f;
            so.FindProperty("goldMin").intValue = goldMin;
            so.FindProperty("goldMax").intValue = goldMax;
            var list = so.FindProperty("items");
            list.arraySize = drops.Length;
            for (int i = 0; i < drops.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("item").objectReferenceValue = drops[i].item;
                e.FindPropertyRelative("chance").floatValue = drops[i].chance;
                e.FindPropertyRelative("minCount").intValue = drops[i].min;
                e.FindPropertyRelative("maxCount").intValue = drops[i].max;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dt);
            return dt;
        }

        private static void AssignDropTable(string prefabName, DropTable table)
        {
            string path = $"{MonsterPrefabFolder}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var monster = prefab != null ? prefab.GetComponent<MonsterController>() : null;
            if (monster == null) { Debug.LogWarning($"[LootInventoryBuilder] {path} 없음"); return; }
            Set(monster, ("dropTable", table));
            PrefabUtility.SavePrefabAsset(prefab);
        }

        // ================= World =================

        private static GroundLoot CreateGroundLootPrefab()
        {
            var matGold = CreateLitMaterial("M_Loot_Gold", new Color(1f, 0.78f, 0.15f), 0.8f, 0.7f);
            var matItem = CreateLitMaterial("M_Loot_Item", Color.white, 0f, 0.6f);

            var root = new GameObject("GroundLoot");
            var visual = new GameObject("GroundLoot_Visual").transform;
            visual.SetParent(root.transform, false);

            // 골드: 세워진 동전(Cylinder)이 회전
            var gold = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gold.name = "GroundLoot_Gold";
            Object.DestroyImmediate(gold.GetComponent<Collider>());
            gold.transform.SetParent(visual, false);
            gold.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            gold.transform.localScale = new Vector3(0.45f, 0.05f, 0.45f);
            gold.GetComponent<Renderer>().sharedMaterial = matGold;

            // 아이템: 아이템 색상의 구(Sphere)
            var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            item.name = "GroundLoot_Item";
            Object.DestroyImmediate(item.GetComponent<Collider>());
            item.transform.SetParent(visual, false);
            item.transform.localScale = Vector3.one * 0.38f;
            item.GetComponent<Renderer>().sharedMaterial = matItem;

            var loot = root.AddComponent<GroundLoot>();
            Set(loot, ("visualRoot", visual), ("goldVisual", gold.GetComponent<Renderer>()), ("itemVisual", item.GetComponent<Renderer>()));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/World/GroundLoot.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<GroundLoot>();
        }

        // ================= UI =================

        private static void BuildLootLog(Transform canvas, PlayerLooter looter)
        {
            // 좌측 상단, 플레이어 상태 아래
            var root = CreateRect("UI_LootLog", canvas, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -140f), new Vector2(420f, 220f));
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 2f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            // 줄 프리팹
            var line = CreateText("UI_LootLog_Line", null, "Gold +0", 22, TextAnchor.MiddleLeft, Color.white);
            line.rectTransform.sizeDelta = new Vector2(420f, 30f);
            line.raycastTarget = false;
            line.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            var linePrefab = PrefabUtility.SaveAsPrefabAsset(line.gameObject, Root + "/Prefabs/UI/UI_LootLog_Line.prefab");
            Object.DestroyImmediate(line.gameObject);

            Set(root.gameObject.AddComponent<LootLogView>(), ("looter", looter), ("lineContainer", root), ("linePrefab", linePrefab.GetComponent<Text>()));
            // 창(StatWindow)보다 아래에 그려지도록
            var stat = canvas.Find("UI_StatWindow");
            if (stat != null) root.SetSiblingIndex(stat.GetSiblingIndex());
        }

        private static Button BuildMenuButton(Transform menuBar) =>
            CloneMenuButton(menuBar, "Inventory", "BAG", "ToggleInventory", 1);

    }
}
