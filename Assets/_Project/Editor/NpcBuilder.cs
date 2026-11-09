using RPGAuto.InputHandling;
using RPGAuto.Npc;
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
    /// 상인 NPC(Capsule) + 상호작용 버튼을 현재 씬에 배치한다.
    /// 메뉴: RPGAuto > Build NPC  (TestMapBuilder 실행 시 자동 호출, ShopBuilder 이후)
    /// </summary>
    public static class NpcBuilder
    {
        public const string MerchantName = "NPC_Merchant_GeneralGoods";
        public static readonly Vector3 MerchantPosition = new(6f, 1f, -24f);

        [MenuItem("RPGAuto/Build NPC")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            RaisePopups(GameObject.Find("UI_Canvas")?.transform);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[NpcBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var input = Object.FindFirstObjectByType<PlayerInputHandler>();
            var canvas = GameObject.Find("UI_Canvas")?.transform;
            var shop = canvas != null ? canvas.Find("UI_ShopWindow")?.GetComponent<ShopWindowView>() : null;
            if (player == null || input == null || shop == null) throw new System.Exception("Player / 상점 UI가 없습니다. Build Shop을 먼저 실행하세요.");

            int npcLayer = EnsureLayer("NPC");

            // ---------- 상인 NPC ----------
            DestroyByName(MerchantName);
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = MerchantName;
            npc.layer = npcLayer;
            npc.transform.position = MerchantPosition;
            // 플레이어 시작 지점을 바라보게
            Vector3 look = player.transform.position - MerchantPosition;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) npc.transform.rotation = Quaternion.LookRotation(look);
            npc.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial("M_Npc_Merchant", new Color(0.35f, 0.75f, 0.45f), 0f, 0.4f);

            var merchant = npc.AddComponent<MerchantNpc>();
            Set(merchant, ("displayName", "General Goods Merchant"), ("title", "Merchant"), ("interactRange", 2.5f),
                ("headHeight", 2.4f), ("shop", shop), ("closeMargin", 3f));

            // 탭 판정에 NPC 레이어 포함
            var so = new SerializedObject(input);
            var mask = so.FindProperty("targetMask");
            mask.intValue |= 1 << npcLayer;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 상호작용 버튼 (범위 안에서만 표시) ----------
            DestroyChild(canvas, "UI_InteractPrompt");
            var promptRoot = CreateRect("UI_InteractPrompt", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(260f, 70f));
            var button = CreateButton("UI_InteractPrompt_Button", promptRoot, "", 20, new Color(0.15f, 0.35f, 0.2f, 0.95f));
            Stretch((RectTransform)button.transform, 0f);
            var label = button.GetComponentInChildren<Text>();
            label.text = "TALK";
            label.alignment = TextAnchor.UpperCenter;
            label.rectTransform.offsetMax = new Vector2(0f, -6f);
            label.fontStyle = FontStyle.Bold;
            var name = CreateText("UI_InteractPrompt_Name", button.transform, "", 16, TextAnchor.LowerCenter, new Color(0.8f, 1f, 0.85f));
            Stretch(name.rectTransform, 6f);
            name.raycastTarget = false;
            var hint = CreateText("UI_InteractPrompt_KeyHint", button.transform, "", 14, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.8f));
            Stretch(hint.rectTransform, 6f);
            hint.raycastTarget = false;
            Set(hint.gameObject.AddComponent<KeyHintText>(), ("input", input), ("actionName", "Interact"));

            var view = promptRoot.gameObject.AddComponent<InteractPromptView>();
            Set(view, ("player", player), ("input", input), ("root", button.gameObject), ("button", button), ("nameText", name));
            // 창들보다 아래에 그린다
            var stat = canvas.Find("UI_QuickSlotPanel");
            if (stat != null) promptRoot.SetSiblingIndex(stat.GetSiblingIndex() + 1);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(p.stringValue)) continue;
                p.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new System.Exception("빈 레이어가 없습니다.");
        }
    }
}
