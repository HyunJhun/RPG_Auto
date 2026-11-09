using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Player;
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
    /// 인벤토리 창(프라시아 형식) + 아이템 상세/강화/확장 팝업을 현재 씬에 구성한다.
    /// 메뉴: RPGAuto > Build Inventory UI  (TestMapBuilder 실행 시 자동 호출, EquipmentBuilder 이후)
    /// </summary>
    public static class InventoryUIBuilder
    {
        public const float PanelWidth = 520f;
        public const float PanelHeight = 900f;

        [MenuItem("RPGAuto/Build Inventory UI")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            RaisePopups(GameObject.Find("UI_Canvas")?.transform);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[InventoryUIBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var go = player.gameObject;
            var inventory = go.GetComponent<PlayerInventory>();
            var equipment = go.GetComponent<PlayerEquipment>();
            var wallet = go.GetComponent<PlayerWallet>();
            var quickSlots = go.GetComponent<QuickSlotBar>();
            var input = Object.FindFirstObjectByType<PlayerInputHandler>();
            var table = AssetDatabase.LoadAssetAtPath<EnhanceTable>(EquipmentBuilder.EnhanceTablePath);
            var config = AssetDatabase.LoadAssetAtPath<InventoryExpansionConfig>(EquipmentBuilder.ExpansionConfigPath);
            if (equipment == null || table == null || config == null)
                throw new System.Exception("장비 데이터가 없습니다. RPGAuto > Build Equipment Data 를 먼저 실행하세요.");

            var canvas = GameObject.Find("UI_Canvas")?.transform;
            if (canvas == null) throw new System.Exception("UI_Canvas가 없습니다.");
            foreach (var n in new[] { "UI_InventoryWindow", "UI_ItemDetailPopup", "UI_EnhancePopup", "UI_ExpansionPopup" })
                DestroyChild(canvas, n);

            var openButton = canvas.Find("UI_MenuBar/UI_MenuBar_Button_Inventory")?.GetComponent<Button>();
            var slotPrefab = CreateSlotPrefab();

            var enhance = BuildEnhancePopup(canvas, table, inventory, wallet);
            var expansion = BuildExpansionPopup(canvas, config, inventory, wallet);
            var detail = BuildDetailPopup(canvas, inventory, equipment, quickSlots, enhance);
            BuildInventoryWindow(canvas, inventory, equipment, quickSlots, input, openButton, slotPrefab, detail, expansion);

            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        // ================= Inventory =================

        private static void BuildInventoryWindow(Transform canvas, PlayerInventory inventory, PlayerEquipment equipment,
            QuickSlotBar quickSlots, PlayerInputHandler input, Button openButton, InventorySlotView slotPrefab,
            ItemDetailPopup detail, ExpansionPopup expansion)
        {
            var root = CreateSideWindow(canvas, "UI_InventoryWindow", "INVENTORY", true, new Vector2(PanelWidth, PanelHeight), out var panel, out var close);

            // 좌측 분류 필터
            string[] filters = { "ALL", "EQUIP", "ACC", "USE", "MISC" };
            var filterButtons = new Object[filters.Length];
            for (int i = 0; i < filters.Length; i++)
            {
                var b = CreateButton("UI_InventoryWindow_Filter_" + filters[i], panel, filters[i], 15, ButtonColor);
                PlaceTopLeft((RectTransform)b.transform, 12f, 72f + i * 64f, 64f, 56f);
                filterButtons[i] = b;
            }

            // 칸 목록 (스크롤, 4열)
            var content = CreateScrollArea("UI_InventoryWindow_Grid", panel, out var gridFrame);
            gridFrame.anchorMin = Vector2.zero;
            gridFrame.anchorMax = Vector2.one;
            gridFrame.pivot = new Vector2(0.5f, 0.5f);
            gridFrame.offsetMin = new Vector2(88f, 64f);
            gridFrame.offsetMax = new Vector2(-12f, -72f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(96f, 96f);
            grid.spacing = new Vector2(6f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            // 상점 모드 버튼 줄
            var shopBar = CreateRect("UI_InventoryWindow_ShopBar", panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(88f, 66f), new Vector2(PanelWidth - 100f, 52f));
            var autoFill = CreateButton("UI_InventoryWindow_Button_AutoFill", shopBar, "AUTO FILL", 18, new Color(0.45f, 0.3f, 0.15f, 1f));
            PlaceTopLeft((RectTransform)autoFill.transform, 0f, 0f, 200f, 52f);
            var clear = CreateButton("UI_InventoryWindow_Button_ClearBasket", shopBar, "CLEAR", 18, ButtonColor);
            PlaceTopLeft((RectTransform)clear.transform, 212f, 0f, PanelWidth - 100f - 212f, 52f);

            // 하단: 무게 / 칸 수 / 확장
            var weight = CreateText("UI_InventoryWindow_WeightText", panel, "WEIGHT 0 / 0", 18, TextAnchor.MiddleLeft, Color.white);
            Place(weight.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 12f), new Vector2(300f, 40f));
            AddOutline(weight);
            var slots = CreateText("UI_InventoryWindow_SlotText", panel, "0 / 0", 18, TextAnchor.MiddleRight, Color.white);
            Place(slots.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-68f, 12f), new Vector2(140f, 40f));
            AddOutline(slots);
            var expand = CreateButton("UI_InventoryWindow_Button_Expand", panel, "+", 26, new Color(0.25f, 0.45f, 0.3f, 1f));
            Place((RectTransform)expand.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 12f), new Vector2(44f, 40f));

            var view = root.gameObject.AddComponent<InventoryWindowView>();
            Set(view, ("inventory", inventory), ("equipment", equipment), ("quickSlots", quickSlots), ("input", input),
                ("window", panel.gameObject), ("openButton", openButton), ("closeButton", close),
                ("slotContainer", content), ("gridFrame", gridFrame), ("slotPrefab", slotPrefab),
                ("weightText", weight), ("slotText", slots), ("expandButton", expand),
                ("shopBar", shopBar.gameObject), ("autoFillButton", autoFill), ("clearBasketButton", clear),
                ("detailPopup", detail), ("expansionPopup", expansion));
            SetArray(view, "filterButtons", filterButtons);
            root.SetAsLastSibling();
        }

        /// <summary>아이템 칸 프리팹 (인벤토리·장비창 공용)</summary>
        public static InventorySlotView CreateSlotPrefab()
        {
            const float size = 96f;
            var root = CreateRect("UI_InventorySlot", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var bg = AddImage(root, new Color(0.1f, 0.1f, 0.12f, 0.85f));
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            var iconRt = CreateStretch("UI_InventorySlot_Icon", root);
            Stretch(iconRt, 14f);
            var icon = AddImage(iconRt, Color.white);
            icon.raycastTarget = false;

            var label = CreateText("UI_InventorySlot_Label", root, "", 15, TextAnchor.MiddleCenter, Color.white);
            Stretch(label.rectTransform, 0f);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            AddOutline(label);

            var placeholder = CreateText("UI_InventorySlot_Placeholder", root, "", 13, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.55f));
            Stretch(placeholder.rectTransform, 2f);
            placeholder.raycastTarget = false;

            Text Corner(string name, TextAnchor align, int fontSize, Color color)
            {
                var t = CreateText(name, root, "", fontSize, align, color);
                Stretch(t.rectTransform, 5f);
                t.raycastTarget = false;
                AddOutline(t, 0.95f);
                return t;
            }
            var level = Corner("UI_InventorySlot_Level", TextAnchor.UpperRight, 14, Color.white);
            var enhance = Corner("UI_InventorySlot_Enhance", TextAnchor.LowerLeft, 17, new Color(1f, 0.9f, 0.5f));
            enhance.fontStyle = FontStyle.Bold;
            var count = Corner("UI_InventorySlot_Count", TextAnchor.LowerRight, 16, Color.white);
            var mark = Corner("UI_InventorySlot_QuickSlotMark", TextAnchor.LowerLeft, 14, new Color(0.45f, 0.9f, 1f));

            // 장착 배지 "E" (좌상단)
            var badge = CreateTopLeft("UI_InventorySlot_EquippedBadge", root, 4f, 4f, 22f, 22f);
            AddImage(badge, new Color(0.75f, 0.62f, 0.35f, 1f)).raycastTarget = false;
            var e = CreateText("UI_InventorySlot_EquippedBadge_Label", badge, "E", 15, TextAnchor.MiddleCenter, new Color(0.15f, 0.1f, 0.05f));
            Stretch(e.rectTransform, 0f);
            e.fontStyle = FontStyle.Bold;
            e.raycastTarget = false;

            var selected = CreateStretch("UI_InventorySlot_Selected", root);
            Stretch(selected, -2f);
            var selImg = AddImage(selected, new Color(1f, 0.82f, 0.3f, 1f));
            selImg.fillCenter = false;
            selImg.raycastTarget = false;

            var view = root.gameObject.AddComponent<InventorySlotView>();
            Set(view, ("background", bg), ("icon", icon), ("label", label), ("levelText", level), ("enhanceText", enhance),
                ("countText", count), ("quickSlotMark", mark), ("equippedBadge", badge.gameObject),
                ("selectedFrame", selected.gameObject), ("placeholderText", placeholder));
            return SavePrefab<InventorySlotView>(root.gameObject, Root + "/Prefabs/UI/UI_InventorySlot.prefab");
        }

        // ================= Item Detail =================

        private static ItemDetailPopup BuildDetailPopup(Transform canvas, PlayerInventory inventory, PlayerEquipment equipment,
            QuickSlotBar quickSlots, EnhancePopup enhance)
        {
            const float w = 440f, h = 660f;
            var root = CreateStretch("UI_ItemDetailPopup", canvas);
            var panel = CreateRect("UI_ItemDetailPopup_Panel", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(w, h));
            AddImage(panel, new Color(0.08f, 0.08f, 0.1f, 1f)); // 불투명 (뒤 화면이 비치지 않게)
            var close = CreateButton("UI_ItemDetailPopup_Button_Close", panel, "X", 22, CloseColor);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(40f, 40f));

            var (iconBg, icon, iconLabel) = CreateItemIcon("UI_ItemDetailPopup_Icon", panel, 20f, 20f, 88f, 16);
            float tx = 124f, tw = w - tx - 56f;
            var name = CreateText("UI_ItemDetailPopup_Name", panel, "", 23, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(name.rectTransform, tx, 20f, tw, 32f);
            name.fontStyle = FontStyle.Bold;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            var type = CreateText("UI_ItemDetailPopup_Type", panel, "", 16, TextAnchor.UpperLeft, new Color(0.65f, 0.75f, 0.9f));
            PlaceTopLeft(type.rectTransform, tx, 58f, w - tx - 20f, 24f);
            var level = CreateText("UI_ItemDetailPopup_Level", panel, "", 16, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(level.rectTransform, tx, 84f, w - tx - 20f, 24f);

            float cw = w - 40f;
            var stats = CreateText("UI_ItemDetailPopup_Stats", panel, "", 18, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(stats.rectTransform, 20f, 124f, cw, 150f);
            stats.supportRichText = true;
            var desc = CreateText("UI_ItemDetailPopup_Description", panel, "", 16, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.85f));
            PlaceTopLeft(desc.rectTransform, 20f, 282f, cw, 64f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var info = CreateText("UI_ItemDetailPopup_Info", panel, "", 15, TextAnchor.UpperLeft, new Color(0.75f, 0.8f, 0.9f));
            PlaceTopLeft(info.rectTransform, 20f, 352f, cw, 44f);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            var message = CreateText("UI_ItemDetailPopup_Message", panel, "", 17, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(message.rectTransform, 20f, 400f, cw, 26f);

            // 퀵슬롯 등록 (소모품)
            var qsGroup = CreateTopLeft("UI_ItemDetailPopup_QuickSlotGroup", panel, 20f, 432f, cw, 76f);
            var qsTitle = CreateText("UI_ItemDetailPopup_QuickSlotTitle", qsGroup, "Register to Quick Slot", 15, TextAnchor.UpperLeft, new Color(0.7f, 0.7f, 0.75f));
            PlaceTopLeft(qsTitle.rectTransform, 0f, 0f, cw, 22f);
            string[] keys = { "7", "8", "9", "0" };
            var qs = new Object[keys.Length];
            float qw = (cw - 3 * 8f) / 4f;
            for (int i = 0; i < keys.Length; i++)
            {
                var b = CreateButton($"UI_ItemDetailPopup_Button_QuickSlot_{keys[i]}", qsGroup, keys[i], 20, ButtonColor);
                PlaceTopLeft((RectTransform)b.transform, i * (qw + 8f), 26f, qw, 46f);
                qs[i] = b;
            }

            // 버리기 수량
            var optionPrefab = AssetDatabase.LoadAssetAtPath<OptionRowView>(Root + "/Prefabs/UI/UI_OptionRow.prefab");
            OptionRowView qty = null;
            if (optionPrefab != null)
            {
                qty = ((GameObject)PrefabUtility.InstantiatePrefab(optionPrefab.gameObject, panel)).GetComponent<OptionRowView>();
                qty.name = "UI_ItemDetailPopup_DiscardQuantity";
                PlaceTopLeft((RectTransform)qty.transform, 20f, 516f, cw, 40f);
            }

            // 하단 버튼
            float by = h - 20f - 56f;
            var use = CreateButton("UI_ItemDetailPopup_Button_Use", panel, "USE", 20, new Color(0.2f, 0.5f, 0.3f, 1f));
            PlaceTopLeft((RectTransform)use.transform, 20f, by, 130f, 56f);
            var equip = CreateButton("UI_ItemDetailPopup_Button_Equip", panel, "EQUIP", 20, new Color(0.2f, 0.4f, 0.6f, 1f));
            PlaceTopLeft((RectTransform)equip.transform, 20f, by, 130f, 56f);
            var enh = CreateButton("UI_ItemDetailPopup_Button_Enhance", panel, "ENHANCE", 20, new Color(0.55f, 0.4f, 0.15f, 1f));
            PlaceTopLeft((RectTransform)enh.transform, 160f, by, 130f, 56f);
            var discard = CreateButton("UI_ItemDetailPopup_Button_Discard", panel, "DISCARD", 18, new Color(0.45f, 0.18f, 0.18f, 1f));
            PlaceTopLeft((RectTransform)discard.transform, 300f, by, w - 320f, 56f);

            var view = root.gameObject.AddComponent<ItemDetailPopup>();
            Set(view, ("inventory", inventory), ("equipment", equipment), ("quickSlots", quickSlots), ("enhancePopup", enhance),
                ("window", panel), ("closeButton", close), ("rightPanelWidth", PanelWidth), ("leftPanelWidth", CharacterWindowBuilder.PanelWidth),
                ("iconBackground", iconBg), ("icon", icon), ("iconLabel", iconLabel), ("nameText", name), ("typeText", type),
                ("levelText", level), ("statsText", stats), ("descriptionText", desc), ("infoText", info), ("messageText", message),
                ("useButton", use), ("equipButton", equip), ("equipButtonText", equip.GetComponentInChildren<Text>()),
                ("enhanceButton", enh), ("discardButton", discard), ("quantityRow", qty), ("quickSlotGroup", qsGroup.gameObject));
            SetArray(view, "quickSlotButtons", qs);
            return view;
        }

        // ================= Enhance =================

        private static EnhancePopup BuildEnhancePopup(Transform canvas, EnhanceTable table, PlayerInventory inventory, PlayerWallet wallet)
        {
            const float w = 520f, h = 560f;
            var root = CreateWindow(canvas, "UI_EnhancePopup", "ENHANCE", new Vector2(w, h), out var panel, out var close);
            var (iconBg, icon, iconLabel) = CreateItemIcon("UI_EnhancePopup_Icon", panel, 24f, 72f, 88f, 16);
            var title = CreateText("UI_EnhancePopup_ItemTitle", panel, "", 22, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(title.rectTransform, 128f, 72f, w - 152f, 88f);
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;

            float cw = w - 48f;
            var rate = CreateText("UI_EnhancePopup_Rate", panel, "", 19, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(rate.rectTransform, 24f, 176f, cw, 56f);
            rate.supportRichText = true;
            var stats = CreateText("UI_EnhancePopup_Stats", panel, "", 18, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(stats.rectTransform, 24f, 240f, cw, 140f);
            stats.supportRichText = true;
            var cost = CreateText("UI_EnhancePopup_Cost", panel, "", 18, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(cost.rectTransform, 24f, 388f, cw, 56f);
            cost.supportRichText = true;
            var result = CreateText("UI_EnhancePopup_Result", panel, "", 21, TextAnchor.MiddleCenter, Color.white);
            PlaceTopLeft(result.rectTransform, 24f, 448f, cw, 30f);
            result.fontStyle = FontStyle.Bold;
            var button = CreateButton("UI_EnhancePopup_Button_Enhance", panel, "ENHANCE", 24, new Color(0.55f, 0.4f, 0.15f, 1f));
            PlaceTopLeft((RectTransform)button.transform, 24f, h - 24f - 56f, cw, 56f);

            var view = root.gameObject.AddComponent<EnhancePopup>();
            Set(view, ("table", table), ("inventory", inventory), ("wallet", wallet), ("window", panel.gameObject), ("closeButton", close),
                ("iconBackground", iconBg), ("icon", icon), ("iconLabel", iconLabel), ("titleText", title), ("rateText", rate),
                ("statsText", stats), ("costText", cost), ("resultText", result), ("enhanceButton", button));
            return view;
        }

        // ================= Expansion =================

        private static ExpansionPopup BuildExpansionPopup(Transform canvas, InventoryExpansionConfig config, PlayerInventory inventory, PlayerWallet wallet)
        {
            const float w = 560f, h = 500f;
            var root = CreateWindow(canvas, "UI_ExpansionPopup", "EXPAND INVENTORY", new Vector2(w, h), out var panel, out var close);
            float cw = w - 48f;

            (Slider slider, Text label, Text cost) Section(string id, float y)
            {
                var label = CreateText($"UI_ExpansionPopup_{id}Text", panel, "", 19, TextAnchor.MiddleLeft, Color.white);
                PlaceTopLeft(label.rectTransform, 24f, y, cw, 30f);
                var slider = CreateSlider($"UI_ExpansionPopup_{id}Slider", panel, new Color(0.85f, 0.65f, 0.2f, 1f));
                PlaceTopLeft((RectTransform)slider.transform, 24f, y + 36f, cw - 130f, 30f);
                var cost = CreateText($"UI_ExpansionPopup_{id}Cost", panel, "0 G", 19, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.35f));
                PlaceTopLeft(cost.rectTransform, w - 24f - 120f, y + 32f, 120f, 36f);
                return (slider, label, cost);
            }

            var slots = Section("Slot", 72f);
            var weight = Section("Weight", 160f);
            var hint = CreateText("UI_ExpansionPopup_Hint", panel,
                $"Slot: {config.GoldPerSlot:N0} G each   ·   Weight: {config.GoldPerWeightStep:N0} G per {config.WeightStep:0}",
                15, TextAnchor.MiddleLeft, new Color(0.6f, 0.6f, 0.65f));
            PlaceTopLeft(hint.rectTransform, 24f, 252f, cw, 24f);
            var total = CreateText("UI_ExpansionPopup_Total", panel, "", 20, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(total.rectTransform, 24f, 300f, cw, 32f);
            total.fontStyle = FontStyle.Bold;
            var result = CreateText("UI_ExpansionPopup_Result", panel, "", 18, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(result.rectTransform, 24f, 340f, cw, 28f);
            var confirm = CreateButton("UI_ExpansionPopup_Button_Confirm", panel, "EXPAND", 22, new Color(0.25f, 0.45f, 0.3f, 1f));
            PlaceTopLeft((RectTransform)confirm.transform, 24f, h - 24f - 56f, cw, 56f);

            var view = root.gameObject.AddComponent<ExpansionPopup>();
            Set(view, ("config", config), ("inventory", inventory), ("wallet", wallet), ("window", panel.gameObject), ("closeButton", close),
                ("slotSlider", slots.slider), ("slotText", slots.label), ("slotCostText", slots.cost),
                ("weightSlider", weight.slider), ("weightText", weight.label), ("weightCostText", weight.cost),
                ("totalText", total), ("resultText", result), ("confirmButton", confirm));
            return view;
        }
    }
}
