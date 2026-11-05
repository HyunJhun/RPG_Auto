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
    /// 상점 데이터 + 상점 창을 현재 씬에 구성한다.
    /// 메뉴: RPGAuto > Build Shop  (TestMapBuilder 실행 시 자동 호출, SkillWindowBuilder 이후)
    /// </summary>
    public static class ShopBuilder
    {
        private const string ItemFolder = Root + "/Data/Items";
        private const string ShopFolder = Root + "/Data/Shops";

        [MenuItem("RPGAuto/Build Shop")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
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
            DestroyChild(canvas, "UI_ShopWindow");

            var openButton = CloneMenuButton(canvas.Find("UI_MenuBar"), "Shop", "SHOP", "ToggleShop", 3);
            BuildWindow(canvas, shop, player.GetComponent<PlayerInventory>(), player.GetComponent<PlayerWallet>(),
                Object.FindFirstObjectByType<PlayerInputHandler>(), openButton);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        // ================= Data =================

        private static ShopData CreateShopData()
        {
            var hp = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/IT_HpPotion.asset");
            var mp = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/IT_MpPotion.asset");
            var ear = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/IT_GoblinEar.asset");
            var seal = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/IT_ChiefSeal.asset");
            if (hp == null || mp == null || ear == null || seal == null)
                throw new System.Exception("아이템 데이터가 없습니다. Build Test Map → Build Loot & Inventory 순서로 먼저 실행하세요.");

            Set(hp, ("sellPrice", 10L));
            Set(mp, ("sellPrice", 12L));
            Set(ear, ("sellPrice", 5L));
            Set(seal, ("sellPrice", 250L));

            var greater = LoadOrCreate<ItemData>(ItemFolder + "/IT_HpPotionGreater.asset");
            Set(greater, ("itemId", "I003"), ("displayName", "Greater HP Potion"), ("shortLabel", "HP+"),
                ("slotColor", new Color(0.55f, 0.05f, 0.15f)), ("category", (int)ItemCategory.Consumable),
                ("description", "Restores 50% of max HP."), ("sellPrice", 40L),
                ("effect", (int)ConsumableEffect.RestoreHp), ("restorePercent", 0.5f), ("cooldown", 1.5f));

            var shop = LoadOrCreate<ShopData>(ShopFolder + "/SHOP_GeneralStore.asset");
            var so = new SerializedObject(shop);
            so.FindProperty("shopName").stringValue = "General Store";
            var list = so.FindProperty("products");
            (ItemData item, long price)[] products = { (hp, 50), (mp, 60), (greater, 200) };
            list.arraySize = products.Length;
            for (int i = 0; i < products.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("item").objectReferenceValue = products[i].item;
                e.FindPropertyRelative("price").longValue = products[i].price;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);
            return shop;
        }

        // ================= UI =================

        private static void BuildWindow(Transform canvas, ShopData shop, PlayerInventory inventory, PlayerWallet wallet,
            PlayerInputHandler input, Button openButton)
        {
            const float w = 900f, h = 640f, listW = 400f;
            const float detailX = 24f + listW + 20f, detailW = w - detailX - 24f, detailH = h - 64f - 24f;

            var root = CreateWindow(canvas, "UI_ShopWindow", "SHOP", new Vector2(w, h), out var panel, out var close);
            var title = panel.Find("UI_ShopWindow_Title").GetComponent<Text>();

            // ---- 탭 ----
            var buyTab = CreateButton("UI_ShopWindow_Tab_Buy", panel, "BUY", 20, ButtonColor);
            PlaceTopLeft((RectTransform)buyTab.transform, 24f, 64f, 120f, 44f);
            var sellTab = CreateButton("UI_ShopWindow_Tab_Sell", panel, "SELL", 20, ButtonColor);
            PlaceTopLeft((RectTransform)sellTab.transform, 152f, 64f, 120f, 44f);
            var sellAll = CreateButton("UI_ShopWindow_Button_SellAllMaterials", panel, "Sell All Materials", 15, new Color(0.45f, 0.3f, 0.15f, 1f));
            PlaceTopLeft((RectTransform)sellAll.transform, 280f, 64f, 144f, 44f);

            // ---- 목록 (ScrollRect) ----
            var listContent = CreateScrollList("UI_ShopWindow_List", panel, 24f, 120f, listW, 420f, out var listFrame);
            var listEmpty = CreateText("UI_ShopWindow_List_EmptyHint", listFrame, "Nothing to sell", 18, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.65f));
            Stretch(listEmpty.rectTransform, 12f);
            listEmpty.raycastTarget = false;

            // ---- 하단: 골드 + 테스트 ----
            var gold = CreateText("UI_ShopWindow_GoldText", panel, "Gold  0", 24, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.35f));
            PlaceTopLeft(gold.rectTransform, 24f, 556f, 220f, 44f);
            AddOutline(gold);
            var testGold = CreateButton("UI_ShopWindow_Button_TestGold", panel, "+10,000 G (TEST)", 16, new Color(0.35f, 0.25f, 0.5f, 1f));
            PlaceTopLeft((RectTransform)testGold.transform, 24f + listW - 180f, 556f, 180f, 44f);

            // ---- 상세 ----
            var detail = CreateTopLeft("UI_ShopWindow_Detail", panel, detailX, 64f, detailW, detailH);
            AddImage(detail, FrameColor);
            var empty = CreateText("UI_ShopWindow_Detail_EmptyHint", detail, "Select an item", 20, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.65f));
            Stretch(empty.rectTransform, 12f);
            var content = CreateStretch("UI_ShopWindow_Detail_Content", detail);

            var icon = CreateTopLeft("UI_ShopWindow_Detail_Icon", content, 16f, 16f, 72f, 72f);
            var iconImg = AddImage(icon, Color.white);
            var iconLabel = CreateText("UI_ShopWindow_Detail_IconLabel", icon, "", 18, TextAnchor.MiddleCenter, Color.white);
            Stretch(iconLabel.rectTransform, 0f);
            iconLabel.fontStyle = FontStyle.Bold;
            AddOutline(iconLabel);
            var name = CreateText("UI_ShopWindow_Detail_Name", content, "", 24, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(name.rectTransform, 104f, 16f, detailW - 120f, 72f);
            name.fontStyle = FontStyle.Bold;

            float cw = detailW - 32f;
            var desc = CreateText("UI_ShopWindow_Detail_Description", content, "", 18, TextAnchor.UpperLeft, new Color(0.88f, 0.88f, 0.9f));
            PlaceTopLeft(desc.rectTransform, 16f, 104f, cw, 70f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var info = CreateText("UI_ShopWindow_Detail_Info", content, "", 18, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.95f));
            PlaceTopLeft(info.rectTransform, 16f, 184f, cw, 52f);

            var optionPrefab = AssetDatabase.LoadAssetAtPath<OptionRowView>(Root + "/Prefabs/UI/UI_OptionRow.prefab");
            if (optionPrefab == null) throw new System.Exception("UI_OptionRow 프리팹이 없습니다. Build Skill Window를 먼저 실행하세요.");
            var qty = ((GameObject)PrefabUtility.InstantiatePrefab(optionPrefab.gameObject, content)).GetComponent<OptionRowView>();
            qty.name = "UI_ShopWindow_Detail_Quantity";
            PlaceTopLeft((RectTransform)qty.transform, 16f, 252f, cw, 40f);
            var max = CreateButton("UI_ShopWindow_Detail_Button_Max", content, "MAX", 18, ButtonColor);
            PlaceTopLeft((RectTransform)max.transform, 16f + cw - 152f, 300f, 152f, 40f);

            var total = CreateText("UI_ShopWindow_Detail_Total", content, "Total 0 G", 26, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(total.rectTransform, 16f, 356f, cw, 40f);
            total.fontStyle = FontStyle.Bold;
            var message = CreateText("UI_ShopWindow_Detail_Message", content, "", 18, TextAnchor.MiddleLeft, Color.white);
            PlaceTopLeft(message.rectTransform, 16f, 404f, cw, 30f);
            message.horizontalOverflow = HorizontalWrapMode.Wrap;

            var action = CreateButton("UI_ShopWindow_Detail_Button_Action", content, "BUY", 26, new Color(0.2f, 0.5f, 0.3f, 1f));
            PlaceTopLeft((RectTransform)action.transform, 16f, detailH - 76f, cw, 60f);

            // ---- 바인딩 ----
            var view = root.gameObject.AddComponent<ShopWindowView>();
            Set(view, ("shop", shop), ("inventory", inventory), ("wallet", wallet), ("input", input),
                ("window", panel.gameObject), ("openButton", openButton), ("closeButton", close), ("titleText", title),
                ("buyTabButton", buyTab), ("sellTabButton", sellTab), ("listContent", listContent),
                ("rowPrefab", CreateShopRowPrefab(listW - 16f)), ("listEmptyHint", listEmpty.gameObject),
                ("goldText", gold), ("messageText", message),
                ("detailRoot", content.gameObject), ("detailEmptyHint", empty.gameObject),
                ("detailIcon", iconImg), ("detailIconLabel", iconLabel), ("detailName", name),
                ("detailDescription", desc), ("detailInfo", info), ("quantityRow", qty), ("maxButton", max),
                ("totalText", total), ("actionButton", action), ("actionText", action.GetComponentInChildren<Text>()),
                ("testGoldButton", testGold), ("sellAllMaterialsButton", sellAll));

            root.SetAsLastSibling();
        }

        /// <summary>세로 스크롤 목록: Frame(ScrollRect) > Viewport(RectMask2D) > Content(VerticalLayout)</summary>
        private static RectTransform CreateScrollList(string name, Transform parent, float x, float y, float w, float h, out RectTransform frame)
        {
            frame = CreateTopLeft(name, parent, x, y, w, h);
            AddImage(frame, FrameColor);

            var viewport = CreateStretch(name + "_Viewport", frame);
            Stretch(viewport, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect(name + "_Content", viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        private static ShopRowView CreateShopRowPrefab(float width)
        {
            const float h = 64f;
            var root = CreateRect("UI_ShopRow", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, h));
            var bg = AddImage(root, new Color(0.16f, 0.16f, 0.2f, 1f));
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            var icon = CreateTopLeft("UI_ShopRow_Icon", root, 8f, 8f, 48f, 48f);
            var iconImg = AddImage(icon, Color.white);
            iconImg.raycastTarget = false;
            var iconLabel = CreateText("UI_ShopRow_IconLabel", icon, "", 14, TextAnchor.MiddleCenter, Color.white);
            Stretch(iconLabel.rectTransform, 0f);
            iconLabel.fontStyle = FontStyle.Bold;
            iconLabel.raycastTarget = false;
            AddOutline(iconLabel);

            var name = CreateText("UI_ShopRow_Name", root, "", 20, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(name.rectTransform, 66f, 8f, 200f, 26f);
            name.fontStyle = FontStyle.Bold;
            name.raycastTarget = false;
            var sub = CreateText("UI_ShopRow_Sub", root, "", 15, TextAnchor.UpperLeft, new Color(0.7f, 0.7f, 0.75f));
            PlaceTopLeft(sub.rectTransform, 66f, 36f, 200f, 22f);
            sub.raycastTarget = false;
            var price = CreateText("UI_ShopRow_Price", root, "", 20, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.35f));
            Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(120f, h));
            price.raycastTarget = false;

            var selected = CreateRect("UI_ShopRow_Selected", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(selected, -2f);
            var selImg = AddImage(selected, new Color(1f, 0.82f, 0.3f, 1f));
            selImg.fillCenter = false;
            selImg.raycastTarget = false;

            var view = root.gameObject.AddComponent<ShopRowView>();
            Set(view, ("icon", iconImg), ("iconLabel", iconLabel), ("nameText", name), ("subText", sub),
                ("priceText", price), ("selectedFrame", selected.gameObject));
            return SavePrefab<ShopRowView>(root.gameObject, Root + "/Prefabs/UI/UI_ShopRow.prefab");
        }
    }
}
