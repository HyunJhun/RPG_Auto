using RPGAuto.Player;
using RPGAuto.Player.Assist;
using RPGAuto.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static RPGAuto.EditorTools.UIBuildKit;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 보조 모드 구성: Player에 AssistController, HUD에 ASSIST 버튼(AUTO 버튼 위)과 상태 문구.
    /// 메뉴: RPGAuto > Build Assist Mode  (TestMapBuilder 실행 시 자동 호출, NpcBuilder 이후)
    /// </summary>
    public static class AssistBuilder
    {
        [MenuItem("RPGAuto/Build Assist Mode")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[AssistBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var assist = player.GetComponent<AssistController>();
            if (assist == null) assist = player.gameObject.AddComponent<AssistController>();
            Set(assist, ("checkInterval", 1f), ("cooldownAfterTrip", 60f), ("fullBagFreeSlots", 2), ("tradeDuration", 0.8f));

            var canvas = GameObject.Find("UI_Canvas")?.transform;
            var auto = canvas != null ? canvas.Find("UI_QuickSlotPanel/UI_Button_AutoCombat") as RectTransform : null;
            if (auto == null) throw new System.Exception("UI_Button_AutoCombat이 없습니다.");

            // ---------- ASSIST 버튼: AUTO 버튼 바로 위 ----------
            DestroyChild(auto.parent, "UI_Button_AssistMode");
            var button = CreateButton("UI_Button_AssistMode", auto.parent, "ASSIST\nOFF", 18, new Color(0.25f, 0.25f, 0.28f, 1f));
            var rt = (RectTransform)button.transform;
            rt.anchorMin = auto.anchorMin;
            rt.anchorMax = auto.anchorMax;
            rt.pivot = auto.pivot;
            rt.sizeDelta = auto.sizeDelta;
            rt.anchoredPosition = auto.anchoredPosition + new Vector2(0f, auto.sizeDelta.y + 10f);
            var label = button.GetComponentInChildren<Text>();
            label.fontStyle = FontStyle.Bold;
            label.lineSpacing = 0.9f;

            // ---------- 상태 문구: 화면 위쪽 가운데 ----------
            DestroyChild(canvas, "UI_AssistStatus");
            var status = CreateRect("UI_AssistStatus", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(560f, 44f));
            AddImage(status, new Color(0.08f, 0.12f, 0.2f, 0.85f)).raycastTarget = false;
            var text = CreateText("UI_AssistStatus_Text", status, "", 20, TextAnchor.MiddleCenter, new Color(0.75f, 0.88f, 1f));
            Stretch(text.rectTransform, 8f);
            text.raycastTarget = false;
            var stat = canvas.Find("UI_QuickSlotPanel");
            if (stat != null) status.SetSiblingIndex(stat.GetSiblingIndex() + 1);

            var view = button.gameObject.AddComponent<AssistButtonView>();
            Set(view, ("assist", assist), ("button", button), ("background", button.GetComponent<Image>()), ("label", label),
                ("statusRoot", status.gameObject), ("statusText", text));
            status.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }
    }
}
