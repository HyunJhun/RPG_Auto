using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.InputHandling;
using RPGAuto.Player;
using RPGAuto.QuickSlots;
using RPGAuto.Skills;
using RPGAuto.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static RPGAuto.EditorTools.UIBuildKit;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 스킬 창 + 테스트 스킬(Pierce, Shockwave)을 현재 씬에 구성한다.
    /// 메뉴: RPGAuto > Build Skill Window  (TestMapBuilder 실행 시 자동 호출)
    /// </summary>
    public static class SkillWindowBuilder
    {
        private const string SkillFolder = Root + "/Data/Skills";

        [MenuItem("RPGAuto/Build Skill Window")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[SkillWindowBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var skills = player.GetComponent<PlayerSkillController>();

            CreateSkillData(skills);

            var canvas = GameObject.Find("UI_Canvas")?.transform;
            if (canvas == null) throw new System.Exception("UI_Canvas가 없습니다.");
            DestroyChild(canvas, "UI_SkillWindow");

            var openButton = CloneMenuButton(canvas.Find("UI_MenuBar"), "Skill", "스킬", "ToggleSkillWindow", 2);
            BuildWindow(canvas, skills, player.GetComponent<QuickSlotBar>(), player.GetComponent<PlayerCombat>(),
                Object.FindFirstObjectByType<PlayerInputHandler>(), openButton);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        // ================= Data =================

        private static void CreateSkillData(PlayerSkillController skills)
        {
            SetDescription("SK_BasicAttack", "적 하나를 빠르게 공격합니다.");
            SetDescription("SK_Slash", "대상에게 물리 피해 250%를 주는 묵직한 베기.");
            SetDescription("SK_Whirlwind", "몸을 회전해 주변 4m 안의 모든 적에게 물리 피해 160%를 줍니다.");
            SetDescription("SK_SecondWind", "숨을 고르며 최대 HP의 30%를 회복합니다.");

            var ePierce = LoadOrCreate<DamageSkillEffect>(SkillFolder + "/Effects/E_Damage_Pierce.asset");
            Set(ePierce, ("damageType", (int)DamageType.Physical), ("damageMultiplier", 3.2f), ("areaRadius", 0f));
            var eShock = LoadOrCreate<DamageSkillEffect>(SkillFolder + "/Effects/E_Damage_Shockwave.asset");
            Set(eShock, ("damageType", (int)DamageType.Physical), ("damageMultiplier", 1.4f), ("areaRadius", 3f), ("areaCenteredOnCaster", false));

            var pierce = LoadOrCreate<SkillData>(SkillFolder + "/SK_Pierce.asset");
            Set(pierce, ("displayName", "찌르기"), ("shortLabel", "찌르기"), ("slotColor", new Color(0.7f, 0.2f, 0.45f)),
                ("description", "대상에게 물리 피해 320%를 주는 정확한 찌르기."),
                ("targetType", (int)SkillTargetType.Enemy), ("cooldown", 10f), ("mpCost", 25f), ("range", 2.5f),
                ("actionLock", 0.7f), ("effect", ePierce),
                ("defaultAutoUse.autoUse", true), ("defaultAutoUse.applyMpLimit", true), ("defaultAutoUse.rangeFirst", true));

            var shock = LoadOrCreate<SkillData>(SkillFolder + "/SK_Shockwave.asset");
            Set(shock, ("displayName", "충격파"), ("shortLabel", "충격파"), ("slotColor", new Color(0.55f, 0.45f, 0.15f)),
                ("description", "멀리서 땅을 내려쳐 대상 주변 3m 안의 적에게 물리 피해 140%를 줍니다."),
                ("targetType", (int)SkillTargetType.Enemy), ("cooldown", 12f), ("mpCost", 30f), ("range", 6f),
                ("actionLock", 0.8f), ("effect", eShock),
                ("defaultAutoUse.autoUse", true), ("defaultAutoUse.applyMpLimit", true), ("defaultAutoUse.rangeFirst", false));

            // 플레이어 스킬 목록에 없으면 추가 (퀵슬롯에는 등록하지 않음 → 스킬 창에서 등록)
            var so = new SerializedObject(skills);
            var list = so.FindProperty("skills");
            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++) existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var s in new Object[] { pierce, shock })
            {
                if (existing.Contains(s)) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skills);
        }

        private static void SetDescription(string asset, string text)
        {
            var data = AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillFolder}/{asset}.asset");
            if (data != null) Set(data, ("description", text));
        }

        // ================= UI =================

        private static void BuildWindow(Transform canvas, PlayerSkillController skills, QuickSlotBar quickSlots,
            PlayerCombat combat, PlayerInputHandler input, Button openButton)
        {
            const float w = 980f, h = 680f;
            const float listW = 380f, listH = 520f, top = 72f;
            const float detailX = 24f + listW + 20f, detailW = w - detailX - 24f, detailH = h - top - 24f;

            var root = CreateWindow(canvas, "UI_SkillWindow", "스킬", new Vector2(w, h), out var panel, out var close);

            // ---- 목록 ----
            var listFrame = CreateTopLeft("UI_SkillWindow_List", panel, 24f, top, listW, listH);
            AddImage(listFrame, FrameColor);
            var layout = listFrame.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var optionPrefab = CreateOptionRowPrefab(listW);
            var globalRow = InstantiateOption(optionPrefab, panel, "UI_SkillWindow_Option_GlobalMpLimit", 24f, top + listH + 16f);

            // ---- 상세 ----
            var detail = CreateTopLeft("UI_SkillWindow_Detail", panel, detailX, top, detailW, detailH);
            AddImage(detail, FrameColor);
            var empty = CreateText("UI_SkillWindow_Detail_EmptyHint", detail, "스킬을 선택하세요", 20, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.65f));
            Stretch(empty.rectTransform, 12f);
            var content = CreateStretch("UI_SkillWindow_Detail_Content", detail);

            var icon = CreateTopLeft("UI_SkillWindow_Detail_Icon", content, 16f, 16f, 72f, 72f);
            var iconImg = AddImage(icon, Color.white);
            var iconLabel = CreateText("UI_SkillWindow_Detail_IconLabel", icon, "", 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(iconLabel.rectTransform, 0f);
            iconLabel.fontStyle = FontStyle.Bold;
            AddOutline(iconLabel);

            float tx = 104f, tw = detailW - tx - 16f;
            var name = CreateText("UI_SkillWindow_Detail_Name", content, "", 26, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(name.rectTransform, tx, 16f, tw, 32f);
            name.fontStyle = FontStyle.Bold;
            var type = CreateText("UI_SkillWindow_Detail_Type", content, "", 17, TextAnchor.UpperLeft, new Color(0.65f, 0.75f, 0.9f));
            PlaceTopLeft(type.rectTransform, tx, 50f, tw, 22f);
            var stats = CreateText("UI_SkillWindow_Detail_Stats", content, "", 17, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.85f));
            PlaceTopLeft(stats.rectTransform, tx, 72f, tw, 22f);

            var desc = CreateText("UI_SkillWindow_Detail_Description", content, "", 18, TextAnchor.UpperLeft, new Color(0.88f, 0.88f, 0.9f));
            PlaceTopLeft(desc.rectTransform, 16f, 104f, detailW - 32f, 56f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 퀵슬롯 1~6
            var qsTitle = CreateText("UI_SkillWindow_Detail_QuickSlotTitle", content, "퀵슬롯 등록", 16, TextAnchor.UpperLeft, new Color(0.7f, 0.7f, 0.75f));
            PlaceTopLeft(qsTitle.rectTransform, 16f, 170f, 300f, 22f);
            var qsButtons = new Object[QuickSlotBar.SkillSlotCount];
            float qw = (detailW - 32f - 5 * 8f) / 6f;
            for (int i = 0; i < qsButtons.Length; i++)
            {
                var b = CreateButton($"UI_SkillWindow_Detail_Button_QuickSlot_{i + 1}", content, (i + 1).ToString(), 22, ButtonColor);
                PlaceTopLeft((RectTransform)b.transform, 16f + i * (qw + 8f), 194f, qw, 46f);
                qsButtons[i] = b;
            }

            // 자동 사용 조건
            var autoTitle = CreateText("UI_SkillWindow_Detail_AutoTitle", content, "자동 사용 설정", 16, TextAnchor.UpperLeft, new Color(0.7f, 0.7f, 0.75f));
            PlaceTopLeft(autoTitle.rectTransform, 16f, 256f, 300f, 22f);
            string[] ids = { "AutoUse", "MpLimit", "RangeFirst", "HpMin", "HpMax", "Cycle" };
            var rows = new OptionRowView[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                rows[i] = InstantiateOption(optionPrefab, content, "UI_SkillWindow_Option_" + ids[i], 16f, 282f + i * 46f, detailW - 32f);

            // ---- 바인딩 ----
            var view = root.gameObject.AddComponent<SkillWindowView>();
            Set(view, ("skills", skills), ("quickSlots", quickSlots), ("combat", combat), ("input", input),
                ("window", panel.gameObject), ("openButton", openButton), ("closeButton", close),
                ("listContainer", listFrame), ("rowPrefab", CreateSkillRowPrefab(listW - 16f)),
                ("detailRoot", content.gameObject), ("detailEmptyHint", empty.gameObject),
                ("detailIcon", iconImg), ("detailIconLabel", iconLabel), ("detailName", name), ("detailType", type),
                ("detailStats", stats), ("detailDescription", desc),
                ("autoUseRow", rows[0]), ("mpLimitRow", rows[1]), ("rangeFirstRow", rows[2]),
                ("hpMinRow", rows[3]), ("hpMaxRow", rows[4]), ("cycleRow", rows[5]), ("globalMpLimitRow", globalRow));
            SetArray(view, "quickSlotButtons", qsButtons);

            root.SetAsLastSibling();
        }

        private static OptionRowView InstantiateOption(OptionRowView prefab, Transform parent, string name, float x, float y, float width = -1f)
        {
            var row = ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent)).GetComponent<OptionRowView>();
            row.name = name;
            var rt = (RectTransform)row.transform;
            PlaceTopLeft(rt, x, y, width > 0f ? width : rt.sizeDelta.x, rt.sizeDelta.y);
            return row;
        }

        private static OptionRowView CreateOptionRowPrefab(float width)
        {
            const float h = 40f;
            var root = CreateRect("UI_OptionRow", null, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, h));

            var label = CreateText("UI_OptionRow_Label", root, "옵션", 18, TextAnchor.MiddleLeft, new Color(0.9f, 0.9f, 0.92f));
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(4f, 0f);
            label.rectTransform.offsetMax = new Vector2(-250f, 0f);

            // 우측 정렬: [ON/OFF] [-] 값 [+]
            Button RightButton(string n, string text, float right, float bw)
            {
                var b = CreateButton(n, root, text, 18, ButtonColor);
                Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-right, 0f), new Vector2(bw, h - 4f));
                return b;
            }

            var plus = RightButton("UI_OptionRow_Button_Plus", "+", 0f, 40f);
            var value = CreateText("UI_OptionRow_Value", root, "0", 18, TextAnchor.MiddleCenter, Color.white);
            Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(64f, h));
            var minus = RightButton("UI_OptionRow_Button_Minus", "-", 112f, 40f);
            var toggle = RightButton("UI_OptionRow_Button_Toggle", "꺼짐", 162f, 84f);

            var view = root.gameObject.AddComponent<OptionRowView>();
            Set(view, ("label", label), ("toggleButton", toggle), ("toggleText", toggle.GetComponentInChildren<Text>()),
                ("minusButton", minus), ("plusButton", plus), ("valueText", value));
            return SavePrefab<OptionRowView>(root.gameObject, Root + "/Prefabs/UI/UI_OptionRow.prefab");
        }

        private static SkillRowView CreateSkillRowPrefab(float width)
        {
            const float h = 72f;
            var root = CreateRect("UI_SkillRow", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, h));
            var bg = AddImage(root, new Color(0.16f, 0.16f, 0.2f, 1f));
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            var icon = CreateTopLeft("UI_SkillRow_Icon", root, 8f, 8f, 56f, 56f);
            var iconImg = AddImage(icon, Color.white);
            iconImg.raycastTarget = false;
            var iconLabel = CreateText("UI_SkillRow_IconLabel", icon, "", 13, TextAnchor.MiddleCenter, Color.white);
            Stretch(iconLabel.rectTransform, 0f);
            iconLabel.fontStyle = FontStyle.Bold;
            iconLabel.raycastTarget = false;
            AddOutline(iconLabel);

            var name = CreateText("UI_SkillRow_Name", root, "", 21, TextAnchor.UpperLeft, Color.white);
            PlaceTopLeft(name.rectTransform, 76f, 10f, 180f, 28f);
            name.fontStyle = FontStyle.Bold;
            name.raycastTarget = false;
            var info = CreateText("UI_SkillRow_Info", root, "", 15, TextAnchor.UpperLeft, new Color(0.7f, 0.7f, 0.75f));
            PlaceTopLeft(info.rectTransform, 76f, 42f, width - 84f, 22f);
            info.raycastTarget = false;
            var mark = CreateText("UI_SkillRow_Mark", root, "", 15, TextAnchor.UpperRight, new Color(0.45f, 0.9f, 1f));
            Place(mark.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -12f), new Vector2(110f, 22f));
            mark.raycastTarget = false;

            var selected = CreateRect("UI_SkillRow_Selected", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(selected, -2f);
            var selImg = AddImage(selected, new Color(1f, 0.82f, 0.3f, 1f));
            selImg.fillCenter = false;
            selImg.raycastTarget = false;

            var view = root.gameObject.AddComponent<SkillRowView>();
            Set(view, ("icon", iconImg), ("iconLabel", iconLabel), ("nameText", name), ("infoText", info),
                ("markText", mark), ("selectedFrame", selected.gameObject));
            return SavePrefab<SkillRowView>(root.gameObject, Root + "/Prefabs/UI/UI_SkillRow.prefab");
        }
    }
}
