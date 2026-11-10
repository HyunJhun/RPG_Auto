using System.Linq;
using RPGAuto.Items;
using RPGAuto.Player;
using RPGAuto.Save;
using RPGAuto.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static RPGAuto.EditorTools.UIBuildKit;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 저장/불러오기 구성: ItemDatabase 자산 갱신, Player에 PlayerSaveSystem, 설정 창에 저장 버튼.
    /// 메뉴: RPGAuto > Build Save System  (TestMapBuilder 실행 시 자동 호출, SettingsBuilder 이후)
    /// </summary>
    public static class SaveBuilder
    {
        public const string DatabasePath = Root + "/Data/Items/ItemDatabase.asset";

        [MenuItem("RPGAuto/Build Save System")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[SaveBuilder] 완료");
        }

        [MenuItem("RPGAuto/Delete Save File")]
        public static void DeleteSaveFile()
        {
            var path = System.IO.Path.Combine(Application.persistentDataPath, "save_slot0.json");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            Debug.Log("[SaveBuilder] 저장 파일 삭제: " + path);
        }

        public static ItemDatabase BuildDatabase()
        {
            EnsureFolder(Root + "/Data/Items");
            var db = LoadOrCreate<ItemDatabase>(DatabasePath);
            var items = AssetDatabase.FindAssets("t:ItemData", new[] { Root })
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(i => i != null)
                .OrderBy(i => i.ItemId)
                .ToArray();
            SetArray(db, "items", items);
            EditorUtility.SetDirty(db);
            return db;
        }

        public static void ApplyToOpenScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var db = BuildDatabase();

            var save = player.GetComponent<PlayerSaveSystem>();
            if (save == null) save = player.gameObject.AddComponent<PlayerSaveSystem>();
            Set(save, ("itemDatabase", db), ("fileName", "save_slot0.json"), ("autoSaveInterval", 30f));

            // ---------- 설정 창 하단: [Reset to Default] [Save] [Delete Save] ----------
            var settings = GameObject.Find("UI_Canvas")?.transform.Find("UI_SettingsWindow");
            var panel = settings != null ? settings.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(r => r.name == "UI_SettingsWindow_Panel") : null;
            if (panel == null) panel = settings?.GetComponentInChildren<UnityEngine.UI.Image>(true)?.rectTransform;
            if (panel == null) throw new System.Exception("UI_SettingsWindow가 없습니다. Build Settings Window를 먼저 실행하세요.");

            var reset = panel.Find("UI_SettingsWindow_Button_Reset") as RectTransform;
            float y = reset != null ? -reset.anchoredPosition.y : 750f;
            float w = panel.rect.width > 0 ? panel.rect.width : 640f;

            DestroyChild(panel, "UI_SettingsWindow_Button_SaveGame");
            DestroyChild(panel, "UI_SettingsWindow_Button_DeleteSave");
            var saveButton = CreateButton("UI_SettingsWindow_Button_SaveGame", panel, "저장", 18, new Color(0.2f, 0.4f, 0.25f, 1f));
            PlaceTopLeft((RectTransform)saveButton.transform, 256f, y, 160f, 50f);
            var deleteButton = CreateButton("UI_SettingsWindow_Button_DeleteSave", panel, "저장 삭제", 18, new Color(0.35f, 0.15f, 0.15f, 1f));
            PlaceTopLeft((RectTransform)deleteButton.transform, 428f, y, w - 24f - 428f, 50f);

            // 기존 "자동 저장" 문구를 제목줄 왼쪽의 저장 상태 표시로 옮긴다
            var hint = panel.Find("UI_SettingsWindow_SaveHint")?.GetComponent<UnityEngine.UI.Text>();
            if (hint != null)
            {
                hint.alignment = TextAnchor.MiddleLeft;
                hint.fontSize = 14;
                PlaceTopLeft(hint.rectTransform, 24f, 14f, 220f, 36f);
            }

            var view = panel.GetComponent<SaveButtonsView>();
            if (view == null) view = panel.gameObject.AddComponent<SaveButtonsView>();
            Set(view, ("saveSystem", save), ("saveButton", saveButton), ("deleteButton", deleteButton), ("statusText", hint), ("confirmWindow", 3f));

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }
    }
}
