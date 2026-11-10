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
    /// 자동 거래 설정 창 + Player의 PlayerTradeSettings 구성, 상점·인벤토리에 연결.
    /// 메뉴: RPGAuto > Build Auto Trade Settings  (TestMapBuilder 실행 시 자동 호출, ShopBuilder 이후)
    /// </summary>
    public static class TradeSettingsBuilder
    {
        private static readonly string[] CategoryIds = { "Equipment", "Accessory", "Consumable", "Material" };

        [MenuItem("RPGAuto/Build Auto Trade Settings")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            RaisePopups(GameObject.Find("UI_Canvas")?.transform);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[TradeSettingsBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var canvas = GameObject.Find("UI_Canvas")?.transform;
            var shopView = canvas != null ? canvas.Find("UI_ShopWindow")?.GetComponent<ShopWindowView>() : null;
            var inventoryView = canvas != null ? canvas.Find("UI_InventoryWindow")?.GetComponent<InventoryWindowView>() : null;
            if (player == null || shopView == null || inventoryView == null) throw new System.Exception("Player / 상점 / 인벤토리 UI가 없습니다.");

            var trade = player.GetComponent<PlayerTradeSettings>();
            if (trade == null) trade = player.gameObject.AddComponent<PlayerTradeSettings>();

            var shopData = new SerializedObject(shopView).FindProperty("shop").objectReferenceValue as ShopData;
            var view = BuildWindow(canvas, trade, shopData);

            Set(shopView, ("tradeSettings", trade));
            Set(inventoryView, ("tradeSettingsView", view));
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        private static AutoTradeSettingsView BuildWindow(Transform canvas, PlayerTradeSettings trade, ShopData shop)
        {
            DestroyChild(canvas, "UI_AutoTradePopup");
            const float w = 640f, h = 940f, top = 64f, listH = 790f;
            var root = CreateWindow(canvas, "UI_AutoTradePopup", "자동 거래 설정", new Vector2(w, h), out var panel, out var close);

            var list = CreateTopLeft("UI_AutoTradePopup_List", panel, 24f, top, w - 48f, listH);
            AddImage(list, FrameColor);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.spacing = 4f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var rowPrefab = AssetDatabase.LoadAssetAtPath<OptionRowView>(Root + "/Prefabs/UI/UI_OptionRow.prefab");
            if (rowPrefab == null) throw new System.Exception("UI_OptionRow 프리팹이 없습니다. Build Skill Window를 먼저 실행하세요.");

            OptionRowView Row(string id)
            {
                var row = ((GameObject)PrefabUtility.InstantiatePrefab(rowPrefab.gameObject, list)).GetComponent<OptionRowView>();
                row.name = "UI_AutoTradePopup_Row_" + id;
                return row;
            }

            void Header(string id, string text, string sub)
            {
                var t = CreateText("UI_AutoTradePopup_Header_" + id, list, text, 18, TextAnchor.LowerLeft, TitleColor);
                t.rectTransform.sizeDelta = new Vector2(0f, 30f);
                t.fontStyle = FontStyle.Bold;
                var s = CreateText("UI_AutoTradePopup_HeaderHint_" + id, list, sub, 14, TextAnchor.UpperLeft, new Color(0.6f, 0.6f, 0.65f));
                s.rectTransform.sizeDelta = new Vector2(0f, 22f);
            }

            // ---------- 판매 ----------
            Header("Sell", "판매 (자동 담기)", "등급 칸을 눌러 개별 선택  ·  -/+ 로 'N등급 이하' 한 번에 지정  ·  잠금·장착 아이템은 항상 제외");
            var categoryRows = new Object[CategoryIds.Length];
            var chips = new Object[CategoryIds.Length * 5];
            for (int c = 0; c < CategoryIds.Length; c++)
            {
                categoryRows[c] = Row("Sell_" + CategoryIds[c]);

                var chipRow = CreateRect("UI_AutoTradePopup_GradeChips_" + CategoryIds[c], list, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 34f));
                var chipLayout = chipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                chipLayout.spacing = 6f;
                chipLayout.padding = new RectOffset(0, 0, 0, 6);
                chipLayout.childControlWidth = true;
                chipLayout.childControlHeight = true;
                chipLayout.childForceExpandWidth = true;
                for (int g = 0; g < 5; g++)
                {
                    var grade = (ItemGrade)g;
                    var chip = CreateButton($"UI_AutoTradePopup_GradeChip_{CategoryIds[c]}_{grade}", chipRow, GameText.Grade(grade), 15, ButtonColor);
                    chips[c * 5 + g] = chip;
                }
            }
            var keepEnhanced = Row("KeepEnhanced");
            var keepShop = Row("KeepShopProducts");

            // ---------- 구매 (상품별 행은 실행 시 상점 목록으로 만든다) ----------
            Header("Buy", "구매 (일괄 구매)", "상품마다 이 개수까지 채웁니다  ·  보조 모드도 같은 기준으로 구매");

            var reset = CreateButton("UI_AutoTradePopup_Button_Reset", panel, "기본값으로", 18, new Color(0.45f, 0.2f, 0.2f, 1f));
            PlaceTopLeft((RectTransform)reset.transform, 24f, top + listH + 14f, 200f, 48f);
            var hint = CreateText("UI_AutoTradePopup_SaveHint", panel, "변경 사항은 자동 저장됩니다", 15, TextAnchor.MiddleRight, new Color(0.6f, 0.6f, 0.65f));
            PlaceTopLeft(hint.rectTransform, w - 24f - 320f, top + listH + 14f, 320f, 48f);

            var view = root.gameObject.AddComponent<AutoTradeSettingsView>();
            Set(view, ("trade", trade), ("shop", shop), ("window", panel.gameObject), ("closeButton", close), ("resetButton", reset),
                ("keepEnhancedRow", keepEnhanced), ("keepShopProductsRow", keepShop), ("buyList", list), ("buyRowPrefab", rowPrefab));
            SetArray(view, "categoryRows", categoryRows);
            SetArray(view, "gradeChips", chips);
            return view;
        }
    }
}
