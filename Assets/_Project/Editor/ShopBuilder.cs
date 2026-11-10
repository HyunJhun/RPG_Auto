using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Player;
using RPGAuto.Shop;
using RPGAuto.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static RPGAuto.EditorTools.UIBuildKit;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 상점 데이터 + 상인 창(프라시아 형식, 인벤토리와 나란히 표시)을 현재 씬에 구성한다.
    /// 메뉴: RPGAuto > Build Shop  (TestMapBuilder 실행 시 자동 호출, InventoryUIBuilder 이후)
    /// </summary>
    public static class ShopBuilder
    {
        private const string ItemFolder = Root + "/Data/Items";
        private const string ShopFolder = Root + "/Data/Shops";
        private const float PanelWidth = 560f;
        private const float PanelHeight = 900f;

        [MenuItem("RPGAuto/Build Shop")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            RaisePopups(GameObject.Find("UI_Canvas")?.transform);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[ShopBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            EnsureFolder(ShopFolder);
            var shop = CreateShopData();

            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var canvas = GameObject.Find("UI_Canvas")?.transform;
            if (canvas == null) throw new System.Exception("UI_Canvas가 없습니다.");
            var inventoryWindow = canvas.Find("UI_InventoryWindow")?.GetComponent<InventoryWindowView>();
            if (inventoryWindow == null) throw new System.Exception("인벤토리 UI가 없습니다. Build Inventory UI를 먼저 실행하세요.");
            DestroyChild(canvas, "UI_ShopWindow");

            var go = player.gameObject;
            var openButton = CloneMenuButton(canvas.Find("UI_MenuBar"), "Shop", "상점", "ToggleShop", 3);
            BuildWindow(canvas, shop, go.GetComponent<PlayerInventory>(), go.GetComponent<PlayerEquipment>(), go.GetComponent<PlayerWallet>(),
                Object.FindFirstObjectByType<PlayerInputHandler>(), inventoryWindow, openButton);

            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        // ================= Data =================

        private static ShopData CreateShopData()
        {
            ItemData Item(string n) => AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemFolder}/{n}.asset");
            var hp = Item("IT_HpPotion");
            var mp = Item("IT_MpPotion");
            var ear = Item("IT_GoblinEar");
            var seal = Item("IT_ChiefSeal");
            var scroll = Item("IT_EnhanceScroll");
            if (hp == null || mp == null || ear == null || seal == null)
                throw new System.Exception("아이템 데이터가 없습니다. Build Test Map → Build Loot & Inventory 순서로 먼저 실행하세요.");

            Set(hp, ("sellPrice", 10L));
            Set(mp, ("sellPrice", 12L));
            Set(ear, ("sellPrice", 5L));
            Set(seal, ("sellPrice", 250L));

            var greater = LoadOrCreate<ItemData>(ItemFolder + "/IT_HpPotionGreater.asset");
            Set(greater, ("itemId", "I003"), ("displayName", "고급 HP 물약"), ("shortLabel", "HP+"),
                ("slotColor", new Color(0.55f, 0.05f, 0.15f)), ("category", (int)ItemCategory.Consumable),
                ("description", "최대 HP의 50%를 회복합니다."), ("sellPrice", 40L),
                ("effect", (int)ConsumableEffect.RestoreHp), ("restorePercent", 0.5f), ("cooldown", 1.5f));

            var shop = LoadOrCreate<ShopData>(ShopFolder + "/SHOP_GeneralStore.asset");
            var so = new SerializedObject(shop);
            so.FindProperty("shopName").stringValue = "잡화 상인";
            so.FindProperty("taxRate").floatValue = 0.2f;
            var list = so.FindProperty("products");
            // (아이템, 가격, 일괄 구매 목표 수량)
            var products = new System.Collections.Generic.List<(ItemData item, long price, int restock)>
            {
                (hp, 50, 100), (mp, 60, 50), (greater, 200, 20)
            };
            if (scroll != null) products.Add((scroll, 300, 0));
            list.arraySize = products.Count;
            for (int i = 0; i < products.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("item").objectReferenceValue = products[i].item;
                e.FindPropertyRelative("price").longValue = products[i].price;
                e.FindPropertyRelative("restockTarget").intValue = products[i].restock;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);
            return shop;
        }

        // ================= UI =================

        private static void BuildWindow(Transform canvas, ShopData shop, PlayerInventory inventory, PlayerEquipment equipment,
            PlayerWallet wallet, PlayerInputHandler input, InventoryWindowView inventoryWindow, Button openButton)
        {
            const float w = PanelWidth, h = PanelHeight, cw = w - 40f;
            var root = CreateSideWindow(canvas, "UI_ShopWindow", "상점", false, new Vector2(w, h), out var panel, out var close);
            var title = panel.Find("UI_ShopWindow_Title").GetComponent<Text>();

            // 탭
            var buyTab = CreateButton("UI_ShopWindow_Tab_Buy", panel, "구매", 20, ButtonColor);
            PlaceTopLeft((RectTransform)buyTab.transform, 20f, 64f, 150f, 44f);
            var sellTab = CreateButton("UI_ShopWindow_Tab_Sell", panel, "판매", 20, ButtonColor);
            PlaceTopLeft((RectTransform)sellTab.transform, 178f, 64f, 150f, 44f);

            // 세율 / 비우기 / 일괄 구매
            var tax = CreateText("UI_ShopWindow_TaxText", panel, "현재 세율 20%", 17, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.35f));
            PlaceTopLeft(tax.rectTransform, 20f, 118f, 280f, 40f);
            var clear = CreateButton("UI_ShopWindow_Button_Clear", panel, "비우기", 16, ButtonColor);
            PlaceTopLeft((RectTransform)clear.transform, w - 20f - 132f - 8f - 84f, 118f, 84f, 40f);
            var bulk = CreateButton("UI_ShopWindow_Button_BulkBuy", panel, "일괄 구매", 16, new Color(0.45f, 0.3f, 0.15f, 1f));
            PlaceTopLeft((RectTransform)bulk.transform, w - 20f - 132f, 118f, 132f, 40f);

            // 거래 목록 (스크롤)
            var content = CreateScrollArea("UI_ShopWindow_List", panel, out var listFrame);
            PlaceTopLeft(listFrame, 20f, 168f, cw, 500f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var empty = CreateText("UI_ShopWindow_List_EmptyHint", listFrame, "", 18, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.65f));
            Stretch(empty.rectTransform, 12f);
            empty.raycastTarget = false;

            // 요약
            var weight = CreateText("UI_ShopWindow_ExpectedWeight", panel, "", 18, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(weight.rectTransform, 20f, 678f, cw, 28f);
            var payment = CreateText("UI_ShopWindow_Payment", panel, "", 22, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.35f));
            PlaceTopLeft(payment.rectTransform, 20f, 708f, cw, 32f);
            payment.fontStyle = FontStyle.Bold;
            var gold = CreateText("UI_ShopWindow_GoldText", panel, "", 17, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));
            PlaceTopLeft(gold.rectTransform, 20f, 742f, cw, 26f);
            var message = CreateText("UI_ShopWindow_Message", panel, "", 17, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(message.rectTransform, 20f, 772f, cw, 26f);

            // 하단 버튼
            var testGold = CreateButton("UI_ShopWindow_Button_TestGold", panel, "+10,000 G (테스트)", 15, new Color(0.35f, 0.25f, 0.5f, 1f));
            PlaceTopLeft((RectTransform)testGold.transform, 20f, h - 20f - 56f, 180f, 56f);
            var done = CreateButton("UI_ShopWindow_Button_Done", panel, "구매", 24, new Color(0.2f, 0.5f, 0.3f, 1f));
            PlaceTopLeft((RectTransform)done.transform, 212f, h - 20f - 56f, w - 232f, 56f);

            var view = root.gameObject.AddComponent<ShopWindowView>();
            Set(view, ("shop", shop), ("inventory", inventory), ("equipment", equipment), ("wallet", wallet), ("input", input),
                ("inventoryWindow", inventoryWindow), ("window", panel.gameObject), ("openButton", openButton), ("closeButton", close),
                ("titleText", title), ("buyTabButton", buyTab), ("sellTabButton", sellTab), ("taxText", tax),
                ("bulkBuyButton", bulk), ("clearButton", clear), ("listContent", content),
                ("rowPrefab", CreateTradeRowPrefab(cw - 12f)), ("listEmptyHint", empty),
                ("weightText", weight), ("paymentText", payment), ("goldText", gold), ("messageText", message),
                ("doneButton", done), ("doneText", done.GetComponentInChildren<Text>()), ("testGoldButton", testGold));
            root.SetAsLastSibling();
        }

        private static ShopTradeRowView CreateTradeRowPrefab(float width)
        {
            const float h = 76f;
            var root = CreateRect("UI_ShopTradeRow", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, h));
            AddImage(root, new Color(0.16f, 0.16f, 0.2f, 1f));

            var (iconBg, icon, iconLabel) = CreateItemIcon("UI_ShopTradeRow_Icon", root, 8f, 8f, 60f, 13);
            var name = CreateText("UI_ShopTradeRow_Name", root, "", 19, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(name.rectTransform, 78f, 8f, width - 78f - 130f, 26f);
            name.fontStyle = FontStyle.Bold;
            var sub = CreateText("UI_ShopTradeRow_Sub", root, "", 14, TextAnchor.UpperLeft, new Color(0.7f, 0.7f, 0.75f));
            PlaceTopLeft(sub.rectTransform, 78f, 40f, 110f, 22f);
            var price = CreateText("UI_ShopTradeRow_Price", root, "", 18, TextAnchor.UpperRight, new Color(1f, 0.85f, 0.35f));
            Place(price.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(120f, 26f));

            var slider = CreateSlider("UI_ShopTradeRow_Slider", root, new Color(0.85f, 0.65f, 0.2f, 1f));
            PlaceTopLeft((RectTransform)slider.transform, 190f, 40f, width - 190f - 70f, 26f);
            var qty = CreateText("UI_ShopTradeRow_Quantity", root, "0", 18, TextAnchor.MiddleRight, Color.white);
            Place(qty.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -38f), new Vector2(56f, 30f));

            // 판매 바구니에서 빼기 (아이콘 좌상단)
            var remove = CreateButton("UI_ShopTradeRow_Button_Remove", root, "x", 14, CloseColor);
            PlaceTopLeft((RectTransform)remove.transform, 2f, 2f, 22f, 22f);

            var view = root.gameObject.AddComponent<ShopTradeRowView>();
            Set(view, ("iconBackground", iconBg), ("icon", icon), ("iconLabel", iconLabel), ("nameText", name), ("subText", sub),
                ("priceText", price), ("quantityText", qty), ("slider", slider), ("removeButton", remove));
            return SavePrefab<ShopTradeRowView>(root.gameObject, Root + "/Prefabs/UI/UI_ShopTradeRow.prefab");
        }
    }
}
