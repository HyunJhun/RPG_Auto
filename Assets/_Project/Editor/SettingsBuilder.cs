using RPGAuto.InputHandling;
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
    /// 전투 설정 창을 현재 씬에 구성한다.
    /// 메뉴: RPGAuto > Build Settings Window  (TestMapBuilder 실행 시 자동 호출, GachaBuilder 이후)
    /// </summary>
    public static class SettingsBuilder
    {
        [MenuItem("RPGAuto/Build Settings Window")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[SettingsBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var saver = player.GetComponent<CombatSettingsSaver>();
            if (saver == null) saver = player.gameObject.AddComponent<CombatSettingsSaver>();

            var canvas = GameObject.Find("UI_Canvas")?.transform;
            if (canvas == null) throw new System.Exception("UI_Canvas가 없습니다.");
            DestroyChild(canvas, "UI_SettingsWindow");

            var openButton = CloneMenuButton(canvas.Find("UI_MenuBar"), "Settings", "OPTION", "Cancel", 5);
            BuildWindow(canvas, player.GetComponent<PlayerCombat>(), saver, Object.FindFirstObjectByType<PlayerInputHandler>(), openButton);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        private static void BuildWindow(Transform canvas, PlayerCombat combat, CombatSettingsSaver saver, PlayerInputHandler input, Button openButton)
        {
            const float w = 640f, h = 980f, top = 64f, listH = 830f;
            var root = CreateWindow(canvas, "UI_SettingsWindow", "SETTINGS", new Vector2(w, h), out var panel, out var close);

            var list = CreateTopLeft("UI_SettingsWindow_List", panel, 24f, top, w - 48f, listH);
            AddImage(list, FrameColor);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 4f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var prefab = AssetDatabase.LoadAssetAtPath<OptionRowView>(Root + "/Prefabs/UI/UI_OptionRow.prefab");
            if (prefab == null) throw new System.Exception("UI_OptionRow 프리팹이 없습니다. Build Skill Window를 먼저 실행하세요.");

            OptionRowView Row(string id)
            {
                var row = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, list)).GetComponent<OptionRowView>();
                row.name = "UI_SettingsWindow_Option_" + id;
                return row;
            }

            void Header(string id, string text)
            {
                var t = CreateText("UI_SettingsWindow_Header_" + id, list, text, 18, TextAnchor.LowerLeft, TitleColor);
                t.rectTransform.sizeDelta = new Vector2(0f, 32f);
                t.fontStyle = FontStyle.Bold;
            }

            Header("Detection", "DETECTION");
            var normal = Row("TargetNormal");
            var elite = Row("TargetElite");
            var boss = Row("TargetBoss");
            var priority = Row("Priority");
            var radius = Row("DetectRadius");
            var ret = Row("ReturnDistance");
            Header("Skill", "SKILL");
            var mpLimit = Row("SkillMpLimit");
            Header("Potion", "POTION  (quick slot AUTO only)");
            var hpPot = Row("HpPotion");
            var mpPot = Row("MpPotion");
            Header("Loot", "LOOT");
            var loot = Row("AutoLoot");
            var lootRadius = Row("AutoLootRadius");
            Header("Assist", "ASSIST MODE");
            var assistMode = Row("AssistMode");
            var assistPotion = Row("AssistPotionMin");
            var assistBag = Row("AssistFullBag");

            var reset = CreateButton("UI_SettingsWindow_Button_Reset", panel, "Reset to Default", 18, new Color(0.45f, 0.2f, 0.2f, 1f));
            PlaceTopLeft((RectTransform)reset.transform, 24f, top + listH + 16f, 220f, 50f);
            var hint = CreateText("UI_SettingsWindow_SaveHint", panel, "Changes are saved automatically", 16, TextAnchor.MiddleRight, new Color(0.6f, 0.6f, 0.65f));
            PlaceTopLeft(hint.rectTransform, w - 24f - 320f, top + listH + 16f, 320f, 50f);

            var view = root.gameObject.AddComponent<SettingsWindowView>();
            Set(view, ("combat", combat), ("saver", saver), ("input", input),
                ("window", panel.gameObject), ("openButton", openButton), ("closeButton", close), ("resetButton", reset),
                ("targetNormalRow", normal), ("targetEliteRow", elite), ("targetBossRow", boss), ("priorityRow", priority),
                ("detectRadiusRow", radius), ("returnDistanceRow", ret), ("skillMpLimitRow", mpLimit),
                ("hpPotionRow", hpPot), ("mpPotionRow", mpPot), ("autoLootRow", loot), ("autoLootRadiusRow", lootRadius),
                ("assistModeRow", assistMode), ("assistPotionRow", assistPotion), ("assistBagRow", assistBag));

            root.SetAsLastSibling();
        }
    }
}
