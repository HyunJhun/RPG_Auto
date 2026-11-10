using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RPGAuto.EditorTools
{
    /// <summary>에디터 빌더 공용 헬퍼 (UGUI 생성, 에셋 생성, SerializedObject 값 설정)</summary>
    public static class UIBuildKit
    {
        public const string Root = "Assets/_Project";

        private static Font _font;
        private static Sprite _uiSprite;

        public const string FontPath = Root + "/Font/NotoSansKR-Bold.ttf";
        /// <summary>한글 폰트 (없으면 기본 폰트)</summary>
        public static Font DefaultFont => _font != null ? _font
            : (_font = AssetDatabase.LoadAssetAtPath<Font>(FontPath) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        public static Sprite UISprite => _uiSprite != null ? _uiSprite : (_uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"));

        public static readonly Color PanelColor = new(0.06f, 0.06f, 0.08f, 1f);
        public static readonly Color FrameColor = new(0.11f, 0.11f, 0.14f, 1f);
        public static readonly Color TitleColor = new(1f, 0.85f, 0.5f);
        public static readonly Color CloseColor = new(0.5f, 0.15f, 0.15f, 1f);
        public static readonly Color ButtonColor = new(0.2f, 0.22f, 0.28f, 1f);

        // ================= Rect =================

        public static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, anchor, pivot, pos, size);
            return rt;
        }

        /// <summary>좌상단 기준 배치 (창 내부 레이아웃용)</summary>
        public static RectTransform CreateTopLeft(string name, Transform parent, float x, float y, float w, float h) =>
            CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));

        public static RectTransform CreateStretch(string name, Transform parent)
        {
            var rt = CreateRect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(rt, 0f);
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h) =>
            Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));

        public static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        // ================= Graphics =================

        public static Image AddImage(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UISprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Text CreateText(string name, Transform parent, string text, int size, TextAnchor align, Color color)
        {
            var rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 30f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Outline AddOutline(Graphic g, float alpha = 0.85f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, alpha);
            return o;
        }

        public static Button CreateButton(string name, Transform parent, string label, int fontSize, Color color)
        {
            var rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 50f));
            var img = AddImage(rt, color);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var text = CreateText(name + "_Label", rt, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform, 0f);
            text.raycastTarget = false;
            return button;
        }

        /// <summary>창 공통 골격: 전체 화면 루트 + 가운데 패널 + 제목 + 닫기 버튼</summary>
        public static RectTransform CreateWindow(Transform canvas, string name, string title, Vector2 size, out RectTransform panel, out Button close)
        {
            var root = CreateStretch(name, canvas);
            panel = CreateRect(name + "_Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            AddImage(panel, PanelColor);

            var t = CreateText(name + "_Title", panel, title, 28, TextAnchor.MiddleCenter, TitleColor);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(480f, 40f));

            close = CreateButton(name + "_Button_Close", panel, "X", 26, CloseColor);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(48f, 48f));
            return root;
        }

        /// <summary>화면 왼쪽/오른쪽에 붙는 세로 창 (프라시아 인벤토리·장비창 형식)</summary>
        public static RectTransform CreateSideWindow(Transform canvas, string name, string title, bool right, Vector2 size,
                                                     out RectTransform panel, out Button close, float yOffset = -40f)
        {
            var root = CreateStretch(name, canvas);
            var anchor = new Vector2(right ? 1f : 0f, 0.5f);
            panel = CreateRect(name + "_Panel", root, anchor, anchor, new Vector2(right ? -20f : 20f, yOffset), size);
            AddImage(panel, PanelColor);

            var t = CreateText(name + "_Title", panel, title, 26, TextAnchor.MiddleCenter, TitleColor);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(size.x - 140f, 40f));

            close = CreateButton(name + "_Button_Close", panel, "X", 26, CloseColor);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(48f, 48f));
            return root;
        }

        /// <summary>세로 스크롤 목록: Frame(ScrollRect) > Viewport(RectMask2D) > Content. 레이아웃 그룹은 호출 측에서 붙인다.</summary>
        public static RectTransform CreateScrollArea(string name, Transform parent, out RectTransform frame, float padding = 6f)
        {
            frame = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            AddImage(frame, FrameColor);

            var viewport = CreateStretch(name + "_Viewport", frame);
            Stretch(viewport, padding);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect(name + "_Content", viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        /// <summary>가로 슬라이더 (배경 + 채움 + 손잡이)</summary>
        public static Slider CreateSlider(string name, Transform parent, Color fillColor)
        {
            var root = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 24f));
            var bg = CreateStretch(name + "_Background", root);
            bg.offsetMin = new Vector2(0f, 7f);
            bg.offsetMax = new Vector2(0f, -7f);
            AddImage(bg, new Color(0.05f, 0.05f, 0.07f, 1f));

            var fillArea = CreateStretch(name + "_FillArea", root);
            fillArea.offsetMin = new Vector2(2f, 9f);
            fillArea.offsetMax = new Vector2(-12f, -9f);
            var fill = CreateStretch(name + "_Fill", fillArea);
            fill.offsetMax = new Vector2(10f, 0f);
            AddImage(fill, fillColor);

            var handleArea = CreateStretch(name + "_HandleArea", root);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = CreateRect(name + "_Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 0f));
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            handleImg.color = Color.white;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;
            return slider;
        }

        /// <summary>아이템 아이콘 묶음: 등급 배경 + 안쪽 색 사각형 + 짧은 이름</summary>
        public static (Image bg, Image icon, Text label) CreateItemIcon(string name, Transform parent, float x, float y, float size, int fontSize)
        {
            var bgRt = CreateTopLeft(name, parent, x, y, size, size);
            var bg = AddImage(bgRt, new Color(0.22f, 0.22f, 0.24f, 1f));
            bg.raycastTarget = false;
            var iconRt = CreateStretch(name + "_Icon", bgRt);
            Stretch(iconRt, size * 0.12f);
            var icon = AddImage(iconRt, Color.white);
            icon.raycastTarget = false;
            var label = CreateText(name + "_Label", bgRt, "", fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch(label.rectTransform, 0f);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            AddOutline(label);
            return (bg, icon, label);
        }

        /// <summary>팝업들이 다른 창 위에 그려지도록 맨 뒤로 보낸다</summary>
        public static void RaisePopups(Transform canvas)
        {
            if (canvas == null) return;
            foreach (var n in new[] { "UI_AutoTradePopup", "UI_ItemDetailPopup", "UI_EnhancePopup", "UI_ExpansionPopup" })
                canvas.Find(n)?.SetAsLastSibling();
        }

        /// <summary>메뉴바 STAT 버튼을 복제해 같은 스타일의 버튼을 만든다 (index: STAT 왼쪽으로 몇 번째인지)</summary>
        public static Button CloneMenuButton(Transform menuBar, string id, string label, string actionName, int index)
        {
            var statButton = menuBar != null ? menuBar.Find("UI_MenuBar_Button_Stat") : null;
            if (statButton == null) { Debug.LogWarning("[UIBuildKit] 메뉴바 STAT 버튼이 없음"); return null; }

            string name = "UI_MenuBar_Button_" + id;
            DestroyChild(menuBar, name);
            var go = Object.Instantiate(statButton.gameObject, menuBar);
            go.name = name;
            ((RectTransform)go.transform).anchoredPosition = ((RectTransform)statButton).anchoredPosition + new Vector2(-120f * index, 0f);

            var l = go.transform.Find("UI_MenuBar_Button_Stat_Label");
            l.name = name + "_Label";
            l.GetComponent<Text>().text = label;
            var hint = go.transform.Find("UI_MenuBar_Button_Stat_KeyHint");
            hint.name = name + "_KeyHint";
            Set(hint.GetComponent<RPGAuto.UI.KeyHintText>(), ("actionName", actionName));

            var button = go.GetComponent<Button>();
            while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);

            // 메뉴바 폭을 버튼 수에 맞춘다
            var bar = (RectTransform)menuBar;
            bar.sizeDelta = new Vector2(Mathf.Max(bar.sizeDelta.x, 120f * (index + 1)), bar.sizeDelta.y);
            return button;
        }

        // ================= Assets =================

        public static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static T SavePrefab<T>(GameObject root, string path) where T : Component
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<T>();
        }

        public static void DestroyByName(string name)
        {
            GameObject go;
            while ((go = GameObject.Find(name)) != null) Object.DestroyImmediate(go);
        }

        public static void DestroyChild(Transform parent, string name)
        {
            if (parent == null) return;
            Transform t;
            while ((t = parent.Find(name)) != null) Object.DestroyImmediate(t.gameObject);
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        public static void Set(Object target, params (string path, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (path, value) in values)
            {
                var p = so.FindProperty(path);
                if (p == null)
                {
                    Debug.LogError($"[UIBuildKit] {target.GetType().Name}.{path} 를 찾을 수 없음");
                    continue;
                }
                switch (value)
                {
                    case float f: p.floatValue = f; break;
                    case int i when p.propertyType == SerializedPropertyType.Enum: p.enumValueIndex = i; break;
                    case int i: p.intValue = i; break;
                    case long l: p.longValue = l; break;
                    case bool b: p.boolValue = b; break;
                    case string s: p.stringValue = s; break;
                    case Color c: p.colorValue = c; break;
                    case Object o: p.objectReferenceValue = o; break;
                    case null: p.objectReferenceValue = null; break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        /// <summary>오브젝트 참조 배열 설정</summary>
        public static void SetArray(Object target, string path, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(path);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
