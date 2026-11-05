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
            DestroyChild(canvas, "UI_InventoryWindow");
            var menuBar = canvas.Find("UI_MenuBar");

            BuildLootLog(canvas, looter);
            var invButton = BuildMenuButton(menuBar);
            BuildInventoryWindow(canvas, inventory, wallet, quickSlots, input, invButton);

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

        private static void BuildInventoryWindow(Transform canvas, PlayerInventory inventory, PlayerWallet wallet,
            QuickSlotBar quickSlots, PlayerInputHandler input, Button openButton)
        {
            const float panelW = 900f, panelH = 640f;
            const float cell = 80f, gap = 8f;
            const int columns = 6;

            var root = CreateStretch("UI_InventoryWindow", canvas);
            var panel = CreateRect("UI_InventoryWindow_Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelW, panelH));
            AddImage(panel, new Color(0.06f, 0.06f, 0.08f, 0.95f));

            var title = CreateText("UI_InventoryWindow_Title", panel, "INVENTORY", 28, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.5f));
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(480f, 40f));

            var close = CreateButton("UI_InventoryWindow_Button_Close", panel, "X", 26, new Color(0.5f, 0.15f, 0.15f, 1f));
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(48f, 48f));

            // ---- 칸 그리드 (좌측) ----
            float gridW = columns * cell + (columns - 1) * gap;
            int rows = Mathf.CeilToInt(inventory.Capacity / (float)columns);
            float gridH = rows * cell + (rows - 1) * gap;
            var grid = CreateRect("UI_InventoryWindow_Grid", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -72f), new Vector2(gridW, gridH));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cell, cell);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;

            var gold = CreateText("UI_InventoryWindow_GoldText", panel, "Gold  0", 24, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.35f));
            Place(gold.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(320f, 40f));
            gold.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            var capacity = CreateText("UI_InventoryWindow_CapacityText", panel, "0 / 0", 20, TextAnchor.MiddleRight, new Color(0.8f, 0.8f, 0.85f));
            Place(capacity.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f + gridW - 160f, 22f), new Vector2(160f, 40f));

            // ---- 상세 정보 (우측) ----
            float detailX = 24f + gridW + 24f;
            float detailW = panelW - detailX - 24f;
            var detailFrame = CreateRect("UI_InventoryWindow_Detail", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(detailX, -72f), new Vector2(detailW, gridH));
            AddImage(detailFrame, new Color(0.11f, 0.11f, 0.14f, 1f));

            var emptyHint = CreateText("UI_InventoryWindow_Detail_EmptyHint", detailFrame, "Select an item", 20, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.65f));
            Stretch(emptyHint.rectTransform, 12f);

            var content = CreateStretch("UI_InventoryWindow_Detail_Content", detailFrame);

            var iconBg = CreateRect("UI_InventoryWindow_Detail_Icon", content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(72f, 72f));
            var iconImg = AddImage(iconBg, Color.white);
            var iconLabel = CreateText("UI_InventoryWindow_Detail_IconLabel", iconBg, "", 18, TextAnchor.MiddleCenter, Color.white);
            Stretch(iconLabel.rectTransform, 0f);
            iconLabel.fontStyle = FontStyle.Bold;
            iconLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            float textX = 100f, textW = detailW - textX - 12f;
            var name = CreateText("UI_InventoryWindow_Detail_Name", content, "", 24, TextAnchor.UpperLeft, Color.white);
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textX, -16f), new Vector2(textW, 32f));
            name.fontStyle = FontStyle.Bold;
            var category = CreateText("UI_InventoryWindow_Detail_Category", content, "", 17, TextAnchor.UpperLeft, new Color(0.65f, 0.75f, 0.9f));
            Place(category.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textX, -50f), new Vector2(textW, 22f));
            var count = CreateText("UI_InventoryWindow_Detail_Count", content, "", 17, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.85f));
            Place(count.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textX, -72f), new Vector2(textW, 22f));

            var desc = CreateText("UI_InventoryWindow_Detail_Description", content, "", 18, TextAnchor.UpperLeft, new Color(0.88f, 0.88f, 0.9f));
            Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -108f), new Vector2(detailW - 32f, 150f));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;

            // 퀵슬롯 등록 (물약 퀵슬롯 7~0)
            var qsGroup = CreateRect("UI_InventoryWindow_Detail_QuickSlotGroup", content, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 84f), new Vector2(detailW - 32f, 80f));
            var qsTitle = CreateText("UI_InventoryWindow_Detail_QuickSlotTitle", qsGroup, "Register to Quick Slot", 16, TextAnchor.UpperLeft, new Color(0.7f, 0.7f, 0.75f));
            Place(qsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(detailW - 32f, 22f));
            string[] keys = { "7", "8", "9", "0" };
            var qsButtons = new Button[QuickSlotBar.ItemSlotCount];
            float qsW = (detailW - 32f - 3 * 8f) / 4f;
            for (int i = 0; i < qsButtons.Length; i++)
            {
                qsButtons[i] = CreateButton($"UI_InventoryWindow_Detail_Button_QuickSlot_{keys[i]}", qsGroup, keys[i], 22, new Color(0.2f, 0.22f, 0.28f, 1f));
                Place((RectTransform)qsButtons[i].transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(i * (qsW + 8f), 0f), new Vector2(qsW, 50f));
            }

            var use = CreateButton("UI_InventoryWindow_Detail_Button_Use", content, "USE", 24, new Color(0.2f, 0.5f, 0.3f, 1f));
            Place((RectTransform)use.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(detailW - 32f, 56f));

            // ---- 바인딩 ----
            var view = root.gameObject.AddComponent<InventoryWindowView>();
            var so = new SerializedObject(view);
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.FindProperty("wallet").objectReferenceValue = wallet;
            so.FindProperty("quickSlots").objectReferenceValue = quickSlots;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("window").objectReferenceValue = panel.gameObject;
            so.FindProperty("openButton").objectReferenceValue = openButton;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("slotContainer").objectReferenceValue = grid;
            so.FindProperty("slotPrefab").objectReferenceValue = CreateInventorySlotPrefab(cell);
            so.FindProperty("goldText").objectReferenceValue = gold;
            so.FindProperty("capacityText").objectReferenceValue = capacity;
            so.FindProperty("detailRoot").objectReferenceValue = content.gameObject;
            so.FindProperty("detailEmptyHint").objectReferenceValue = emptyHint.gameObject;
            so.FindProperty("detailIcon").objectReferenceValue = iconImg;
            so.FindProperty("detailIconLabel").objectReferenceValue = iconLabel;
            so.FindProperty("detailName").objectReferenceValue = name;
            so.FindProperty("detailCategory").objectReferenceValue = category;
            so.FindProperty("detailCount").objectReferenceValue = count;
            so.FindProperty("detailDescription").objectReferenceValue = desc;
            so.FindProperty("useButton").objectReferenceValue = use;
            so.FindProperty("quickSlotGroup").objectReferenceValue = qsGroup.gameObject;
            var arr = so.FindProperty("quickSlotButtons");
            arr.arraySize = qsButtons.Length;
            for (int i = 0; i < qsButtons.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = qsButtons[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // 다른 창들 위에 그린다
            root.SetAsLastSibling();
        }

        private static InventorySlotView CreateInventorySlotPrefab(float size)
        {
            var root = CreateRect("UI_InventorySlot", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var bg = AddImage(root, new Color(0.16f, 0.16f, 0.2f, 1f));
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            var icon = CreateRect("UI_InventorySlot_Icon", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - 14f, size - 14f));
            var iconImg = AddImage(icon, Color.white);
            iconImg.raycastTarget = false;

            var label = CreateText("UI_InventorySlot_Label", root, "", 18, TextAnchor.MiddleCenter, Color.white);
            Stretch(label.rectTransform, 0f);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            var count = CreateText("UI_InventorySlot_Count", root, "", 17, TextAnchor.LowerRight, Color.white);
            Stretch(count.rectTransform, 5f);
            count.raycastTarget = false;
            count.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            var mark = CreateText("UI_InventorySlot_QuickSlotMark", root, "", 14, TextAnchor.UpperLeft, new Color(0.45f, 0.9f, 1f));
            Stretch(mark.rectTransform, 5f);
            mark.raycastTarget = false;
            mark.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            var selected = CreateRect("UI_InventorySlot_Selected", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(selected, -2f);
            var selImg = AddImage(selected, new Color(1f, 0.82f, 0.3f, 1f));
            selImg.fillCenter = false;
            selImg.raycastTarget = false;

            var view = root.gameObject.AddComponent<InventorySlotView>();
            Set(view, ("icon", iconImg), ("label", label), ("countText", count), ("quickSlotMark", mark), ("selectedFrame", selected.gameObject));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Root + "/Prefabs/UI/UI_InventorySlot.prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<InventorySlotView>();
        }
    }
}
