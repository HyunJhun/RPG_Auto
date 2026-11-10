using RPGAuto.Combat;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Player;
using RPGAuto.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static RPGAuto.EditorTools.UIBuildKit;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 캐릭터 창(장비/능력치, 프라시아 장비창 형식)을 현재 씬에 구성하고 STAT 버튼·C키에 연결한다.
    /// 기존 UI_StatWindow는 제거한다.
    /// 메뉴: RPGAuto > Build Character Window  (TestMapBuilder 실행 시 자동 호출, InventoryUIBuilder 이후)
    /// </summary>
    public static class CharacterWindowBuilder
    {
        public const float PanelWidth = 480f;
        public const float PanelHeight = 900f;

        [MenuItem("RPGAuto/Build Character Window")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            RaisePopups(GameObject.Find("UI_Canvas")?.transform);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterWindowBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var go = player.gameObject;
            var canvas = GameObject.Find("UI_Canvas")?.transform;
            if (canvas == null) throw new System.Exception("UI_Canvas가 없습니다.");
            var detail = canvas.Find("UI_ItemDetailPopup")?.GetComponent<ItemDetailPopup>();
            var slotPrefab = AssetDatabase.LoadAssetAtPath<InventorySlotView>(Root + "/Prefabs/UI/UI_InventorySlot.prefab");
            if (detail == null || slotPrefab == null) throw new System.Exception("인벤토리 UI가 없습니다. Build Inventory UI를 먼저 실행하세요.");

            DestroyChild(canvas, "UI_StatWindow");
            DestroyChild(canvas, "UI_CharacterWindow");
            var openButton = canvas.Find("UI_MenuBar/UI_MenuBar_Button_Stat")?.GetComponent<Button>();
            if (openButton != null)
            {
                var label = openButton.transform.Find("UI_MenuBar_Button_Stat_Label")?.GetComponent<Text>();
                if (label != null) label.text = "캐릭터";
            }

            BuildWindow(canvas, go.GetComponent<CombatStats>(), go.GetComponent<PlayerProgression>(), go.GetComponent<PlayerEquipment>(),
                Object.FindFirstObjectByType<PlayerInputHandler>(), openButton, slotPrefab, detail);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private static void BuildWindow(Transform canvas, CombatStats stats, PlayerProgression progression, PlayerEquipment equipment,
            PlayerInputHandler input, Button openButton, InventorySlotView slotPrefab, ItemDetailPopup detail)
        {
            const float w = PanelWidth, cw = w - 40f;
            var root = CreateSideWindow(canvas, "UI_CharacterWindow", "모험가", false, new Vector2(w, PanelHeight), out var panel, out var close);
            var name = panel.Find("UI_CharacterWindow_Title").GetComponent<Text>();

            // 레벨 배지 (좌상단)
            var lvBg = CreateTopLeft("UI_CharacterWindow_LevelBadge", panel, 16f, 12f, 72f, 48f);
            AddImage(lvBg, new Color(0.2f, 0.16f, 0.1f, 1f));
            var level = CreateText("UI_CharacterWindow_LevelText", lvBg, "Lv.1", 20, TextAnchor.MiddleCenter, TitleColor);
            Stretch(level.rectTransform, 0f);
            level.fontStyle = FontStyle.Bold;

            // 능력 지수 요약: 공격 / 방어 / 명중
            var summary = CreateTopLeft("UI_CharacterWindow_Summary", panel, 20f, 72f, cw, 84f);
            AddImage(summary, FrameColor);
            Text SummaryCell(string id, string title, int index)
            {
                float cell = cw / 3f;
                var t = CreateText($"UI_CharacterWindow_Summary_{id}Title", summary, title, 15, TextAnchor.UpperCenter, new Color(0.7f, 0.7f, 0.75f));
                PlaceTopLeft(t.rectTransform, index * cell, 10f, cell, 22f);
                var v = CreateText($"UI_CharacterWindow_Summary_{id}Value", summary, "0", 26, TextAnchor.UpperCenter, Color.white);
                PlaceTopLeft(v.rectTransform, index * cell, 36f, cell, 36f);
                v.fontStyle = FontStyle.Bold;
                return v;
            }
            var attack = SummaryCell("Attack", "공격력", 0);
            var defense = SummaryCell("Defense", "방어력", 1);
            var accuracy = SummaryCell("Accuracy", "명중", 2);

            // 탭
            var equipTab = CreateButton("UI_CharacterWindow_Tab_Equipment", panel, "장비", 18, ButtonColor);
            PlaceTopLeft((RectTransform)equipTab.transform, 20f, 168f, cw / 2f - 4f, 44f);
            var statTab = CreateButton("UI_CharacterWindow_Tab_Stats", panel, "능력치", 18, ButtonColor);
            PlaceTopLeft((RectTransform)statTab.transform, 20f + cw / 2f + 4f, 168f, cw / 2f - 4f, 44f);

            // [장비] 탭: 장착 슬롯 12칸 (EquipPosition 순서)
            var equipContent = CreateTopLeft("UI_CharacterWindow_EquipmentTab", panel, 20f, 224f, cw, PanelHeight - 244f);
            var grid = CreateTopLeft("UI_CharacterWindow_EquipmentGrid", equipContent, 0f, 0f, cw, 3 * 100f + 2 * 12f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(100f, 100f);
            layout.spacing = new Vector2((cw - 400f) / 3f, 12f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            var slots = new Object[PlayerEquipment.AllPositions.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = ((GameObject)PrefabUtility.InstantiatePrefab(slotPrefab.gameObject, grid)).GetComponent<InventorySlotView>();
                slot.name = "UI_CharacterWindow_EquipSlot_" + PlayerEquipment.AllPositions[i];
                slots[i] = slot;
            }
            var bonus = CreateText("UI_CharacterWindow_BonusText", equipContent, "", 17, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.95f));
            PlaceTopLeft(bonus.rectTransform, 0f, 3 * 100f + 2 * 12f + 20f, cw, 90f);
            bonus.horizontalOverflow = HorizontalWrapMode.Wrap;

            // [능력치] 탭
            var statContent = CreateTopLeft("UI_CharacterWindow_StatsTab", panel, 20f, 224f, cw, PanelHeight - 244f);
            AddImage(statContent, FrameColor);
            var rows = CreateStretch("UI_CharacterWindow_StatRows", statContent);
            Stretch(rows, 12f);
            var rowsLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.childControlHeight = false;
            rowsLayout.childControlWidth = true;
            rowsLayout.childForceExpandHeight = false;
            rowsLayout.spacing = 2f;

            var view = root.gameObject.AddComponent<CharacterWindowView>();
            Set(view, ("stats", stats), ("progression", progression), ("equipment", equipment), ("input", input),
                ("window", panel.gameObject), ("openButton", openButton), ("closeButton", close),
                ("nameText", name), ("levelText", level), ("attackText", attack), ("defenseText", defense), ("accuracyText", accuracy),
                ("equipmentTabButton", equipTab), ("statsTabButton", statTab),
                ("equipmentTab", equipContent.gameObject), ("statsTab", statContent.gameObject),
                ("bonusText", bonus), ("detailPopup", detail),
                ("statRowContainer", rows), ("statRowPrefab", CreateStatRowPrefab(cw - 24f)));
            SetArray(view, "equipSlots", slots);
            root.SetAsLastSibling();
        }

        private static GameObject CreateStatRowPrefab(float width)
        {
            var root = CreateRect("UI_StatRow", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 30f));
            var label = CreateText("UI_StatRow_Label", root, "항목", 18, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.88f));
            Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            var value = CreateText("UI_StatRow_Value", root, "0", 18, TextAnchor.MiddleRight, Color.white);
            Stretch(value.rectTransform, 0f);
            value.rectTransform.offsetMax = new Vector2(-8f, 0f);
            value.supportRichText = true;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Root + "/Prefabs/UI/UI_StatRow.prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }
    }
}
