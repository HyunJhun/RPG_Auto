using RPGAuto.Gacha;
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
    /// 뽑기 확률표 + 뽑기 창을 현재 씬에 구성한다.
    /// 메뉴: RPGAuto > Build Gacha  (TestMapBuilder 실행 시 자동 호출, ShopBuilder 이후)
    /// </summary>
    public static class GachaBuilder
    {
        private const string ItemFolder = Root + "/Data/Items";
        private const string GachaFolder = Root + "/Data/Gacha";

        [MenuItem("RPGAuto/Build Gacha")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[GachaBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            EnsureFolder(GachaFolder);
            var gacha = CreateGachaData();

            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var canvas = GameObject.Find("UI_Canvas")?.transform;
            if (canvas == null) throw new System.Exception("UI_Canvas가 없습니다.");
            DestroyChild(canvas, "UI_GachaWindow");

            var openButton = CloneMenuButton(canvas.Find("UI_MenuBar"), "Gacha", "GACHA", "ToggleGacha", 4);
            BuildWindow(canvas, gacha, player.GetComponent<PlayerInventory>(), player.GetComponent<PlayerWallet>(),
                Object.FindFirstObjectByType<PlayerInputHandler>(), openButton);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        // ================= Data =================

        private static ItemData Item(string name)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemFolder}/{name}.asset");
            if (item == null) throw new System.Exception($"{name} 이 없습니다. Build Loot & Inventory / Build Shop 을 먼저 실행하세요.");
            return item;
        }

        private static GachaData CreateGachaData()
        {
            var hp = Item("IT_HpPotion");
            var mp = Item("IT_MpPotion");
            var greater = Item("IT_HpPotionGreater");
            var ear = Item("IT_GoblinEar");
            var seal = Item("IT_ChiefSeal");
            var scroll = Item("IT_EnhanceScroll");
            static ItemData Eq(string id) => EquipmentBuilder.Load(id) ?? throw new System.Exception($"EQ_{id} 없음 — Build Equipment Data 먼저 실행");

            var relic = LoadOrCreate<ItemData>(ItemFolder + "/IT_AncientRelic.asset");
            Set(relic, ("itemId", "I104"), ("displayName", "Ancient Relic"), ("shortLabel", "RELIC"),
                ("slotColor", new Color(0.95f, 0.6f, 0.15f)), ("category", (int)ItemCategory.Material),
                ("description", "A relic from a lost age. Obtainable only from summons."), ("sellPrice", 5000L));

            var data = LoadOrCreate<GachaData>(GachaFolder + "/GACHA_Supply.asset");
            var so = new SerializedObject(data);
            so.FindProperty("displayName").stringValue = "Supply Summon";
            so.FindProperty("description").stringValue = "Summon supplies and rare materials. A 10x summon guarantees at least one Rare or better.";
            so.FindProperty("singleCost").longValue = 300;
            so.FindProperty("tenCost").longValue = 2700;
            so.FindProperty("tenPullGuarantee").enumValueIndex = (int)GachaGrade.Rare;

            (GachaGrade grade, float weight, (ItemData item, int count)[] rewards)[] tables =
            {
                (GachaGrade.Common, 70f, new[] { (hp, 5), (mp, 5), (ear, 10), (scroll, 1) }),
                (GachaGrade.Rare, 22f, new[] { (greater, 3), (hp, 20), (scroll, 3), (Eq("IronSword"), 1), (Eq("IronArmor"), 1), (Eq("CopperRing"), 1) }),
                (GachaGrade.Epic, 7f, new[] { (seal, 1), (scroll, 8), (Eq("KnightSword"), 1), (Eq("KnightArmor"), 1), (Eq("SilverRing"), 1), (Eq("SilverNecklace"), 1) }),
                (GachaGrade.Legendary, 1f, new[] { (relic, 1), (Eq("AncientBlade"), 1), (Eq("ChiefAxe"), 1) }),
            };
            var grades = so.FindProperty("grades");
            grades.arraySize = tables.Length;
            for (int i = 0; i < tables.Length; i++)
            {
                var g = grades.GetArrayElementAtIndex(i);
                g.FindPropertyRelative("grade").enumValueIndex = (int)tables[i].grade;
                g.FindPropertyRelative("weight").floatValue = tables[i].weight;
                var rewards = g.FindPropertyRelative("rewards");
                rewards.arraySize = tables[i].rewards.Length;
                for (int j = 0; j < tables[i].rewards.Length; j++)
                {
                    var r = rewards.GetArrayElementAtIndex(j);
                    r.FindPropertyRelative("item").objectReferenceValue = tables[i].rewards[j].item;
                    r.FindPropertyRelative("count").intValue = tables[i].rewards[j].count;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        // ================= UI =================

        private static void BuildWindow(Transform canvas, GachaData gacha, PlayerInventory inventory, PlayerWallet wallet,
            PlayerInputHandler input, Button openButton)
        {
            const float w = 960f, h = 620f, bannerW = 340f, top = 64f;
            const float rightX = 24f + bannerW + 20f, rightW = w - rightX - 24f;
            float bannerH = h - top - 24f;

            var root = CreateWindow(canvas, "UI_GachaWindow", "SUMMON", new Vector2(w, h), out var panel, out var close);

            // ---- 배너 (좌측) ----
            var banner = CreateTopLeft("UI_GachaWindow_Banner", panel, 24f, top, bannerW, bannerH);
            AddImage(banner, new Color(0.14f, 0.11f, 0.2f, 1f));
            float bw = bannerW - 32f;
            var name = CreateText("UI_GachaWindow_Banner_Name", banner, "", 26, TextAnchor.UpperLeft, TitleColor);
            PlaceTopLeft(name.rectTransform, 16f, 16f, bw, 36f);
            name.fontStyle = FontStyle.Bold;
            var desc = CreateText("UI_GachaWindow_Banner_Description", banner, "", 17, TextAnchor.UpperLeft, new Color(0.88f, 0.88f, 0.9f));
            PlaceTopLeft(desc.rectTransform, 16f, 58f, bw, 72f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var rates = CreateText("UI_GachaWindow_Banner_Rates", banner, "", 18, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(rates.rectTransform, 16f, 142f, bw, 220f);
            rates.supportRichText = true;

            var gold = CreateText("UI_GachaWindow_GoldText", banner, "Gold  0", 24, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.35f));
            PlaceTopLeft(gold.rectTransform, 16f, bannerH - 112f, bw, 40f);
            AddOutline(gold);
            var testGold = CreateButton("UI_GachaWindow_Button_TestGold", banner, "+10,000 G (TEST)", 16, new Color(0.35f, 0.25f, 0.5f, 1f));
            PlaceTopLeft((RectTransform)testGold.transform, 16f, bannerH - 64f, bw, 48f);

            // ---- 결과 (우측) ----
            var results = CreateTopLeft("UI_GachaWindow_Results", panel, rightX, top, rightW, 380f);
            AddImage(results, FrameColor);
            var grid = results.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(96f, 120f);
            grid.spacing = new Vector2(12f, 16f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.MiddleCenter;
            var hint = CreateText("UI_GachaWindow_Results_EmptyHint", results, "Press Summon to begin", 20, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.65f));
            hint.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(hint.rectTransform, 12f);

            var message = CreateText("UI_GachaWindow_Message", panel, "", 20, TextAnchor.MiddleCenter, Color.white);
            PlaceTopLeft(message.rectTransform, rightX, top + 392f, rightW, 32f);
            message.fontStyle = FontStyle.Bold;

            float btnW = (rightW - 16f) / 2f;
            var single = CreateButton("UI_GachaWindow_Button_Single", panel, "Summon x1", 22, new Color(0.25f, 0.4f, 0.65f, 1f));
            PlaceTopLeft((RectTransform)single.transform, rightX, h - 24f - 100f, btnW, 100f);
            var ten = CreateButton("UI_GachaWindow_Button_Ten", panel, "Summon x10", 22, new Color(0.6f, 0.35f, 0.15f, 1f));
            PlaceTopLeft((RectTransform)ten.transform, rightX + btnW + 16f, h - 24f - 100f, btnW, 100f);

            // ---- 바인딩 ----
            var view = root.gameObject.AddComponent<GachaWindowView>();
            Set(view, ("gacha", gacha), ("inventory", inventory), ("wallet", wallet), ("input", input),
                ("window", panel.gameObject), ("openButton", openButton), ("closeButton", close),
                ("bannerName", name), ("bannerDescription", desc), ("ratesText", rates),
                ("singleButton", single), ("singleText", single.GetComponentInChildren<Text>()),
                ("tenButton", ten), ("tenText", ten.GetComponentInChildren<Text>()),
                ("goldText", gold), ("messageText", message), ("testGoldButton", testGold),
                ("resultContainer", results), ("resultPrefab", CreateResultSlotPrefab()), ("resultEmptyHint", hint.gameObject));

            root.SetAsLastSibling();
        }

        private static GachaResultSlotView CreateResultSlotPrefab()
        {
            var root = CreateRect("UI_GachaResultSlot", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 120f));
            AddImage(root, new Color(0.08f, 0.08f, 0.1f, 1f)).raycastTarget = false;

            var frame = CreateStretch("UI_GachaResultSlot_Frame", root);
            var frameImg = AddImage(frame, Color.white);
            frameImg.fillCenter = false;
            frameImg.raycastTarget = false;

            var icon = CreateTopLeft("UI_GachaResultSlot_Icon", root, 12f, 10f, 72f, 72f);
            var iconImg = AddImage(icon, Color.white);
            iconImg.raycastTarget = false;
            var label = CreateText("UI_GachaResultSlot_Label", icon, "", 15, TextAnchor.MiddleCenter, Color.white);
            Stretch(label.rectTransform, 0f);
            label.fontStyle = FontStyle.Bold;
            AddOutline(label);
            var count = CreateText("UI_GachaResultSlot_Count", icon, "", 15, TextAnchor.LowerRight, Color.white);
            Stretch(count.rectTransform, 3f);
            AddOutline(count, 0.95f);

            var grade = CreateText("UI_GachaResultSlot_Grade", root, "", 14, TextAnchor.MiddleCenter, Color.white);
            PlaceTopLeft(grade.rectTransform, 0f, 88f, 96f, 26f);
            grade.fontStyle = FontStyle.Bold;

            var view = root.gameObject.AddComponent<GachaResultSlotView>();
            Set(view, ("frame", frameImg), ("icon", iconImg), ("label", label), ("countText", count), ("gradeText", grade));
            return SavePrefab<GachaResultSlotView>(root.gameObject, Root + "/Prefabs/UI/UI_GachaResultSlot.prefab");
        }
    }
}
