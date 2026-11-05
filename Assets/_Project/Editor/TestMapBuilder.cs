using RPGAuto.CameraSystem;
using RPGAuto.Combat;
using RPGAuto.Data;
using RPGAuto.EditorTools.Data;
using RPGAuto.InputHandling;
using RPGAuto.Items;
using RPGAuto.Monster;
using RPGAuto.Player;
using RPGAuto.QuickSlots;
using RPGAuto.Skills;
using RPGAuto.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 조작/전투/성장 테스트용 맵(TestMap.unity)을 기본 도형으로 생성한다.
    /// 메뉴: RPGAuto > Build Test Map (다시 실행하면 씬을 새로 만든다)
    /// 스탯은 엑셀(StatTable.xlsx)을 먼저 임포트해서 사용한다.
    /// </summary>
    public static class TestMapBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = "Assets/Scenes/TestMap.unity";
        private const string NavMeshDataPath = "Assets/Scenes/TestMap/NavMesh-TestMap.asset";
        private const string SkillDataFolder = Root + "/Data/Skills";
        private const string ItemDataFolder = Root + "/Data/Items";
        private const string JoystickBgSprite = "Assets/Joystick Pack/Sprites/All Axis Backgrounds/AllAxis_Outline_Arrows.png";
        private const string JoystickHandleSprite = "Assets/Joystick Pack/Sprites/Handles/Handle_Plain.png";
        private const int NotWalkableArea = 1;

        private static Font _font;
        private static Sprite _uiSprite;
        private static Sprite _knobSprite;

        [MenuItem("RPGAuto/Build Test Map")]
        public static void Build()
        {
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs/UI");
            EnsureFolder(Root + "/Prefabs/World");
            EnsureFolder(Root + "/Prefabs/Monster");
            EnsureFolder(SkillDataFolder + "/Effects");
            EnsureFolder(ItemDataFolder);
            EnsureFolder("Assets/Scenes/TestMap");

            // 이전 버전 산출물 정리
            AssetDatabase.DeleteAsset(Root + "/Prefabs/UI/UI_SkillSlot.prefab");
            AssetDatabase.DeleteAsset(Root + "/Prefabs/Monster/Monster_Normal.prefab");
            AssetDatabase.DeleteAsset(Root + "/Prefabs/Monster/Monster_Elite.prefab");

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            StatTableImporter.Import(true);
            AssetDatabase.Refresh();
            // 임포트 중 에셋이 다시 로드될 수 있어 참조를 새로 가져온다
            var database = AssetDatabase.LoadAssetAtPath<StatDatabase>(StatTableImporter.DatabasePath);
            if (database == null) throw new System.Exception("StatDatabase 임포트 실패");

            int groundLayer = EnsureLayer("Ground");
            int obstacleLayer = EnsureLayer("Obstacle");
            int playerLayer = EnsureLayer("Player");
            int monsterLayer = EnsureLayer("Monster");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // 새 씬(Single) 생성 시 사용되지 않는 에셋이 언로드되므로 씬 생성 후에 다시 로드한다
            database = AssetDatabase.LoadAssetAtPath<StatDatabase>(StatTableImporter.DatabasePath);

            // ---------- Materials ----------
            var matGround = CreateLitMaterial("M_Ground", new Color(0.36f, 0.52f, 0.32f));
            var matWall = CreateLitMaterial("M_Wall", new Color(0.35f, 0.35f, 0.38f));
            var matObstacle = CreateLitMaterial("M_Obstacle", new Color(0.62f, 0.5f, 0.38f));
            var matPlayer = CreateLitMaterial("M_Player", new Color(0.2f, 0.45f, 0.9f));
            var matFacing = CreateLitMaterial("M_PlayerFacing", Color.white);
            var matGoblin = CreateLitMaterial("M_Monster_Normal", new Color(0.8f, 0.3f, 0.25f));
            var matWarrior = CreateLitMaterial("M_Monster_Warrior", new Color(0.85f, 0.55f, 0.15f));
            var matChief = CreateLitMaterial("M_Monster_Elite", new Color(0.55f, 0.25f, 0.8f));
            var matMarker = CreateUnlitMaterial("M_DestinationMarker", new Color(1f, 0.85f, 0.2f));
            var matTarget = CreateUnlitMaterial("M_TargetIndicator", new Color(1f, 0.15f, 0.15f));

            // ---------- Environment ----------
            var env = new GameObject("Environment");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(env.transform);
            ground.transform.localScale = new Vector3(6f, 1f, 6f); // 60 x 60
            ground.layer = groundLayer;
            ground.GetComponent<Renderer>().sharedMaterial = matGround;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

            var walls = new GameObject("Walls_Boundary");
            walls.transform.SetParent(env.transform);
            CreateBlock("Wall_Boundary_North", walls.transform, new Vector3(0f, 1.5f, 30.5f), new Vector3(62f, 3f, 1f), matWall, obstacleLayer);
            CreateBlock("Wall_Boundary_South", walls.transform, new Vector3(0f, 1.5f, -30.5f), new Vector3(62f, 3f, 1f), matWall, obstacleLayer);
            CreateBlock("Wall_Boundary_East", walls.transform, new Vector3(30.5f, 1.5f, 0f), new Vector3(1f, 3f, 60f), matWall, obstacleLayer);
            CreateBlock("Wall_Boundary_West", walls.transform, new Vector3(-30.5f, 1.5f, 0f), new Vector3(1f, 3f, 60f), matWall, obstacleLayer);

            var obstacles = new GameObject("Obstacles");
            obstacles.transform.SetParent(env.transform);
            CreateBlock("Obstacle_Wall_01", obstacles.transform, new Vector3(-6f, 1f, 0f), new Vector3(36f, 2f, 1f), matWall, obstacleLayer);
            CreateBlock("Obstacle_Wall_02", obstacles.transform, new Vector3(8f, 1f, 14f), new Vector3(1f, 2f, 20f), matWall, obstacleLayer);
            CreateBlock("Obstacle_Box_01", obstacles.transform, new Vector3(-14f, 1f, -12f), new Vector3(3f, 2f, 3f), matObstacle, obstacleLayer);
            CreateBlock("Obstacle_Box_02", obstacles.transform, new Vector3(13f, 1f, -10f), new Vector3(4f, 2f, 2f), matObstacle, obstacleLayer);
            CreateBlock("Obstacle_Box_03", obstacles.transform, new Vector3(-14f, 1f, 15f), new Vector3(4f, 2f, 4f), matObstacle, obstacleLayer);
            CreateBlock("Obstacle_Box_04", obstacles.transform, new Vector3(21f, 1f, 18f), new Vector3(2f, 2f, 6f), matObstacle, obstacleLayer);
            CreateBlock("Obstacle_Box_05", obstacles.transform, new Vector3(3f, 1f, -15f), new Vector3(2f, 2f, 2f), matObstacle, obstacleLayer);

            // ---------- NavMesh ----------
            var navGo = new GameObject("NavMeshSurface_TestMap");
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << groundLayer) | (1 << obstacleLayer);
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshDataPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);

            // ---------- Data ----------
            var basicAttack = CreateSkills(out var skillList);
            var hpPotion = LoadOrCreate<ItemData>(ItemDataFolder + "/IT_HpPotion.asset");
            Set(hpPotion, ("itemId", "I001"), ("displayName", "HP Potion"), ("shortLabel", "HP"),
                ("slotColor", new Color(0.75f, 0.15f, 0.2f)), ("effect", (int)ConsumableEffect.RestoreHp),
                ("restorePercent", 0.25f), ("cooldown", 1f));
            var mpPotion = LoadOrCreate<ItemData>(ItemDataFolder + "/IT_MpPotion.asset");
            Set(mpPotion, ("itemId", "I002"), ("displayName", "MP Potion"), ("shortLabel", "MP"),
                ("slotColor", new Color(0.2f, 0.35f, 0.85f)), ("effect", (int)ConsumableEffect.RestoreMp),
                ("restorePercent", 0.3f), ("cooldown", 1f));

            // ---------- Player ----------
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.layer = playerLayer;
            player.transform.position = new Vector3(0f, 1f, -20f);
            player.GetComponent<Renderer>().sharedMaterial = matPlayer;

            var facing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            facing.name = "Player_FacingIndicator";
            facing.transform.SetParent(player.transform, false);
            facing.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);
            facing.transform.localScale = Vector3.one * 0.3f;
            facing.layer = playerLayer;
            facing.GetComponent<Renderer>().sharedMaterial = matFacing;
            Object.DestroyImmediate(facing.GetComponent<Collider>());

            var agent = player.AddComponent<NavMeshAgent>();
            agent.baseOffset = 1f; // Capsule 피벗이 중앙이므로
            agent.radius = 0.5f;
            agent.height = 2f;

            var playerStats = player.AddComponent<CombatStats>();
            var progression = player.AddComponent<PlayerProgression>();
            Set(progression, ("database", database), ("startLevel", 1));

            var skillController = player.AddComponent<PlayerSkillController>();
            Set(skillController, ("basicAttack", basicAttack), ("skills", skillList));

            var inventory = player.AddComponent<PlayerInventory>();
            {
                var so = new SerializedObject(inventory);
                var list = so.FindProperty("startItems");
                list.arraySize = 2;
                list.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = hpPotion;
                list.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 50;
                list.GetArrayElementAtIndex(1).FindPropertyRelative("item").objectReferenceValue = mpPotion;
                list.GetArrayElementAtIndex(1).FindPropertyRelative("count").intValue = 30;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var quickSlotBar = player.AddComponent<QuickSlotBar>();
            {
                var so = new SerializedObject(quickSlotBar);
                var skills = so.FindProperty("skillEntries");
                skills.arraySize = QuickSlotBar.SkillSlotCount;
                for (int i = 0; i < QuickSlotBar.SkillSlotCount; i++)
                {
                    bool has = i < skillList.Length;
                    skills.GetArrayElementAtIndex(i).FindPropertyRelative("skillIndex").intValue = has ? i : -1;
                    skills.GetArrayElementAtIndex(i).FindPropertyRelative("autoUse").boolValue = has;
                }
                var items = so.FindProperty("itemEntries");
                items.arraySize = QuickSlotBar.ItemSlotCount;
                var potions = new Object[] { hpPotion, mpPotion, null, null };
                for (int i = 0; i < QuickSlotBar.ItemSlotCount; i++)
                {
                    items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = potions[i];
                    items.GetArrayElementAtIndex(i).FindPropertyRelative("autoUse").boolValue = potions[i] != null;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var playerCombat = player.AddComponent<PlayerCombat>();
            var inputHandler = player.AddComponent<PlayerInputHandler>();
            var controller = player.AddComponent<PlayerController>();

            // ---------- Camera ----------
            var cam = Camera.main;
            cam.name = "Main Camera";
            cam.fieldOfView = 45f;
            var follow = cam.gameObject.AddComponent<TopViewCameraFollow>();
            Set(follow, ("target", player.transform));
            follow.SetTarget(player.transform);

            // ---------- Monsters (스탯은 엑셀 Monster 시트) ----------
            var goblin = CreateMonsterPrefab("Monster_Goblin", "M001", 1f, matGoblin, monsterLayer, database);
            var warrior = CreateMonsterPrefab("Monster_GoblinWarrior", "M002", 1.1f, matWarrior, monsterLayer, database);
            var chief = CreateMonsterPrefab("Monster_GoblinChief", "M003", 1.3f, matChief, monsterLayer, database);

            var spawners = new GameObject("MonsterSpawners");
            CreateSpawner("MonsterSpawner_Goblin_South", spawners.transform, new Vector3(-10f, 0f, -18f), new Vector2(14f, 10f), goblin, 5);
            CreateSpawner("MonsterSpawner_GoblinWarrior_North", spawners.transform, new Vector3(-4f, 0f, 18f), new Vector2(16f, 8f), warrior, 5);
            CreateSpawner("MonsterSpawner_GoblinChief_East", spawners.transform, new Vector3(20f, 0f, -16f), new Vector2(12f, 10f), chief, 2);

            // ---------- World Markers ----------
            var markerPrefab = CreateFlatMarkerPrefab("DestinationMarker_Visual", matMarker, 0.9f);
            var markerPresenter = new GameObject("DestinationMarker");
            Set(markerPresenter.AddComponent<DestinationMarker>(), ("player", controller), ("markerPrefab", markerPrefab));

            var targetRingPrefab = CreateFlatMarkerPrefab("TargetIndicator_Visual", matTarget, 1.5f);
            var targetIndicatorGo = new GameObject("TargetIndicator");
            Set(targetIndicatorGo.AddComponent<TargetIndicator>(), ("combat", playerCombat), ("visualPrefab", targetRingPrefab));

            // ---------- UI ----------
            var canvasGo = new GameObject("UI_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = canvasGo.transform;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // 그리는 순서 = 하이라키 순서 (이름표가 가장 뒤)
            BuildNameplateLayer(canvas, progression, cam);
            BuildDamageTextLayer(canvas, playerStats, cam);

            var joystickGo = (GameObject)PrefabUtility.InstantiatePrefab(CreateJoystickPrefab(), canvas);
            Set(joystickGo.GetComponent<VirtualJoystick>(), ("input", inputHandler));

            var statBarPrefab = CreateStatBarPrefab();
            BuildPlayerStatusUI(canvas, statBarPrefab, playerStats, progression);
            BuildTargetInfoUI(canvas, statBarPrefab, playerCombat, progression);
            BuildExpBarUI(canvas, progression);
            BuildQuickSlotPanelUI(canvas, inputHandler, playerCombat, quickSlotBar);
            var statButton = BuildMenuBarUI(canvas, inputHandler);
            BuildStatWindowUI(canvas, playerStats, progression, inputHandler, statButton);

            // ---------- Wiring ----------
            Set(inputHandler, ("viewCamera", cam),
                ("groundMask", (1 << groundLayer) | (1 << obstacleLayer)), ("targetMask", 1 << monsterLayer));
            Set(controller, ("input", inputHandler), ("viewCamera", cam), ("bodyRenderer", player.GetComponent<Renderer>()));

            // ---------- Loot & Inventory ----------
            LootInventoryBuilder.ApplyToOpenScene();
            SkillWindowBuilder.ApplyToOpenScene();
            ShopBuilder.ApplyToOpenScene();
            GachaBuilder.ApplyToOpenScene();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TestMapBuilder] TestMap 생성 완료: " + ScenePath);
        }

        // ================= Skills =================

        private static SkillData CreateSkills(out Object[] skillList)
        {
            var eBasic = LoadOrCreate<DamageSkillEffect>(SkillDataFolder + "/Effects/E_Damage_Basic.asset");
            Set(eBasic, ("damageType", (int)DamageType.Physical), ("damageMultiplier", 1f), ("areaRadius", 0f));
            var eSlash = LoadOrCreate<DamageSkillEffect>(SkillDataFolder + "/Effects/E_Damage_Slash.asset");
            Set(eSlash, ("damageType", (int)DamageType.Physical), ("damageMultiplier", 2.5f), ("areaRadius", 0f));
            var eWhirl = LoadOrCreate<DamageSkillEffect>(SkillDataFolder + "/Effects/E_Damage_Whirlwind.asset");
            Set(eWhirl, ("damageType", (int)DamageType.Physical), ("damageMultiplier", 1.6f), ("areaRadius", 4f), ("areaCenteredOnCaster", true));
            var eHeal = LoadOrCreate<HealSkillEffect>(SkillDataFolder + "/Effects/E_Heal_SecondWind.asset");
            Set(eHeal, ("healPercentOfMaxHp", 0.3f));

            var basic = LoadOrCreate<SkillData>(SkillDataFolder + "/SK_BasicAttack.asset");
            Set(basic, ("displayName", "Basic Attack"), ("shortLabel", "ATK"), ("slotColor", new Color(0.6f, 0.2f, 0.2f)),
                ("targetType", (int)SkillTargetType.Enemy), ("cooldown", 0.9f), ("mpCost", 0f), ("range", 2.2f),
                ("actionLock", 0.45f), ("effect", eBasic));

            var slash = LoadOrCreate<SkillData>(SkillDataFolder + "/SK_Slash.asset");
            Set(slash, ("displayName", "Slash"), ("shortLabel", "SLASH"), ("slotColor", new Color(0.75f, 0.35f, 0.2f)),
                ("targetType", (int)SkillTargetType.Enemy), ("cooldown", 4f), ("mpCost", 10f), ("range", 2.5f),
                ("actionLock", 0.6f), ("effect", eSlash),
                ("defaultAutoUse.autoUse", true), ("defaultAutoUse.applyMpLimit", true), ("defaultAutoUse.rangeFirst", true),
                ("defaultAutoUse.useHpMin", false), ("defaultAutoUse.useHpMax", false));

            var whirl = LoadOrCreate<SkillData>(SkillDataFolder + "/SK_Whirlwind.asset");
            Set(whirl, ("displayName", "Whirlwind"), ("shortLabel", "WHIRL"), ("slotColor", new Color(0.25f, 0.5f, 0.75f)),
                ("targetType", (int)SkillTargetType.Enemy), ("cooldown", 8f), ("mpCost", 20f), ("range", 3f),
                ("actionLock", 0.8f), ("effect", eWhirl),
                ("defaultAutoUse.autoUse", true), ("defaultAutoUse.applyMpLimit", true), ("defaultAutoUse.rangeFirst", true),
                ("defaultAutoUse.useHpMin", true), ("defaultAutoUse.hpMinPercent", 50f), ("defaultAutoUse.useHpMax", false));

            var heal = LoadOrCreate<SkillData>(SkillDataFolder + "/SK_SecondWind.asset");
            Set(heal, ("displayName", "Second Wind"), ("shortLabel", "HEAL"), ("slotColor", new Color(0.25f, 0.65f, 0.35f)),
                ("targetType", (int)SkillTargetType.Self), ("cooldown", 15f), ("mpCost", 15f), ("range", 0f),
                ("actionLock", 0.5f), ("effect", eHeal),
                ("defaultAutoUse.autoUse", true), ("defaultAutoUse.applyMpLimit", false),
                ("defaultAutoUse.useHpMin", false), ("defaultAutoUse.useHpMax", true), ("defaultAutoUse.hpMaxPercent", 40f));

            skillList = new Object[] { slash, whirl, heal };
            return basic;
        }

        // ================= Monsters =================

        private static MonsterController CreateMonsterPrefab(string prefabName, string monsterId, float scale,
            Material mat, int layer, StatDatabase database)
        {
            string path = $"{Root}/Prefabs/Monster/{prefabName}.prefab";
            var row = database.GetMonster(monsterId);

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = prefabName;
            go.layer = layer;
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var agent = go.AddComponent<NavMeshAgent>();
            agent.baseOffset = 1f;
            agent.radius = 0.5f;
            agent.height = 2f;

            go.AddComponent<CombatStats>();
            var monster = go.AddComponent<MonsterController>();
            Set(monster, ("database", database), ("monsterId", monsterId),
                ("displayName", row != null ? row.displayName : prefabName),
                ("grade", row != null ? (int)row.grade : 0),
                ("bodyRenderer", go.GetComponent<Renderer>()));

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<MonsterController>();
        }

        private static void CreateSpawner(string name, Transform parent, Vector3 pos, Vector2 area, MonsterController prefab, int maxAlive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            Set(go.AddComponent<MonsterSpawner>(), ("prefab", prefab), ("areaSize", area), ("maxAlive", maxAlive), ("respawnDelay", 5f));
        }

        // ================= UI: HUD =================

        private static void BuildNameplateLayer(Transform canvas, PlayerProgression progression, Camera cam)
        {
            string path = Root + "/Prefabs/UI/UI_Nameplate.prefab";
            var plateRt = CreateRect("UI_Nameplate", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(320f, 30f));
            var plateText = AddText(plateRt.gameObject, "Lv.1 Monster", 20, TextAnchor.LowerCenter, Color.white);
            var outline = plateRt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var platePrefab = PrefabUtility.SaveAsPrefabAsset(plateRt.gameObject, path);
            Object.DestroyImmediate(plateRt.gameObject);

            var layer = CreateStretch("UI_NameplateLayer", canvas, 0f);
            var manager = layer.gameObject.AddComponent<NameplateManager>();
            Set(manager, ("progression", progression), ("viewCamera", cam), ("nameplatePrefab", platePrefab.GetComponent<Text>()));
        }

        private static void BuildDamageTextLayer(Transform canvas, CombatStats player, Camera cam)
        {
            string path = Root + "/Prefabs/UI/UI_DamageText.prefab";
            var rt = CreateRect("UI_DamageText", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(240f, 40f));
            var text = AddText(rt.gameObject, "0", 30, TextAnchor.LowerCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(rt.gameObject, path);
            Object.DestroyImmediate(rt.gameObject);

            var layer = CreateStretch("UI_DamageTextLayer", canvas, 0f);
            Set(layer.gameObject.AddComponent<DamageTextManager>(), ("player", player), ("viewCamera", cam), ("textPrefab", prefab.GetComponent<Text>()));
        }

        private static GameObject CreateStatBarPrefab()
        {
            string path = Root + "/Prefabs/UI/UI_StatBar.prefab";

            var root = CreateRect("UI_StatBar", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 30f));
            AddImage(root.gameObject, new Color(0f, 0f, 0f, 0.65f), _uiSprite, false).type = Image.Type.Sliced;

            var fillRt = CreateStretch("UI_StatBar_Fill", root, 3f);
            var fill = AddImage(fillRt.gameObject, Color.red, _uiSprite, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;

            var text = AddText(CreateStretch("UI_StatBar_Text", root, 0f).gameObject, "0 / 0", 18, TextAnchor.MiddleCenter, Color.white);

            Set(root.gameObject.AddComponent<StatBarView>(), ("fill", fill), ("valueText", text));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static StatBarView InstantiateStatBar(GameObject prefab, Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size, Color fillColor)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.transform.Find("UI_StatBar_Fill").GetComponent<Image>().color = fillColor;
            return go.GetComponent<StatBarView>();
        }

        private static void BuildPlayerStatusUI(Transform canvas, GameObject statBarPrefab, CombatStats stats, PlayerProgression progression)
        {
            var panel = CreateRect("UI_PlayerStatus", canvas, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(530f, 100f));

            var badge = CreateRect("UI_PlayerStatus_LevelBadge", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(96f, 96f));
            AddImage(badge.gameObject, new Color(0.85f, 0.65f, 0.25f), _knobSprite, false);
            var inner = CreateStretch("UI_PlayerStatus_LevelBadge_Inner", badge, 5f);
            AddImage(inner.gameObject, new Color(0.08f, 0.08f, 0.1f, 0.95f), _knobSprite, false);
            var levelText = AddText(CreateStretch("UI_PlayerStatus_LevelText", badge, 0f).gameObject, "1", 40, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.55f));
            levelText.fontStyle = FontStyle.Bold;

            var hp = InstantiateStatBar(statBarPrefab, panel, "UI_PlayerStatus_HPBar", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(108f, -14f), new Vector2(410f, 32f), new Color(0.85f, 0.2f, 0.2f));
            var mp = InstantiateStatBar(statBarPrefab, panel, "UI_PlayerStatus_MPBar", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(108f, -54f), new Vector2(410f, 24f), new Color(0.2f, 0.45f, 0.9f));

            Set(panel.gameObject.AddComponent<PlayerStatusView>(), ("stats", stats), ("progression", progression),
                ("hpBar", hp), ("mpBar", mp), ("levelText", levelText));
        }

        private static void BuildTargetInfoUI(Transform canvas, GameObject statBarPrefab, PlayerCombat combat, PlayerProgression progression)
        {
            var panel = CreateRect("UI_TargetInfo", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(480f, 80f));
            var content = CreateStretch("UI_TargetInfo_Content", panel, 0f);

            var nameRt = CreateRect("UI_TargetInfo_Name", content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(480f, 36f));
            var nameText = AddText(nameRt.gameObject, "Target", 26, TextAnchor.MiddleCenter, Color.white);
            nameRt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            var hp = InstantiateStatBar(statBarPrefab, content, "UI_TargetInfo_HPBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(480f, 26f), new Color(0.85f, 0.2f, 0.2f));

            Set(panel.gameObject.AddComponent<TargetInfoView>(), ("combat", combat), ("progression", progression),
                ("content", content.gameObject), ("nameText", nameText), ("hpBar", hp));
        }

        private static void BuildExpBarUI(Transform canvas, PlayerProgression progression)
        {
            var bar = CreateRect("UI_ExpBar", canvas, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.offsetMin = new Vector2(0f, 0f);
            bar.offsetMax = new Vector2(0f, 12f);
            AddImage(bar.gameObject, new Color(0f, 0f, 0f, 0.75f), null, false);

            var fillRt = CreateStretch("UI_ExpBar_Fill", bar, 2f);
            var fill = AddImage(fillRt.gameObject, new Color(0.95f, 0.75f, 0.25f), _uiSprite, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            var textRt = CreateRect("UI_ExpBar_Text", canvas, Vector2.zero, Vector2.zero, new Vector2(16f, 16f), new Vector2(300f, 26f));
            var text = AddText(textRt.gameObject, "EXP 0.000%", 20, TextAnchor.LowerLeft, new Color(1f, 0.9f, 0.6f));
            textRt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            Set(bar.gameObject.AddComponent<ExpBarView>(), ("progression", progression), ("fill", fill), ("percentText", text));
        }

        private static Button BuildMenuBarUI(Transform canvas, PlayerInputHandler input)
        {
            var bar = CreateRect("UI_MenuBar", canvas, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(400f, 70f));
            var statRt = CreateRect("UI_MenuBar_Button_Stat", bar, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(110f, 70f));
            AddImage(statRt.gameObject, new Color(0.15f, 0.15f, 0.18f, 0.9f), _uiSprite).type = Image.Type.Sliced;
            var button = statRt.gameObject.AddComponent<Button>();
            AddText(CreateRect("UI_MenuBar_Button_Stat_Label", statRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(110f, 30f)).gameObject, "STAT", 22, TextAnchor.MiddleCenter, Color.white);
            AddKeyHint(statRt, "UI_MenuBar_Button_Stat_KeyHint", new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(100f, 20f), 15, TextAnchor.LowerCenter, input, "ToggleStatWindow");
            return button;
        }

        private static void BuildStatWindowUI(Transform canvas, CombatStats stats, PlayerProgression progression,
            PlayerInputHandler input, Button openButton)
        {
            // 행 프리팹
            string rowPath = Root + "/Prefabs/UI/UI_StatRow.prefab";
            var rowRt = CreateRect("UI_StatRow", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 30f));
            rowRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            var lab = CreateStretch("UI_StatRow_Label", rowRt, 0f);
            AddText(lab.gameObject, "Label", 20, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.9f));
            var val = CreateStretch("UI_StatRow_Value", rowRt, 0f);
            AddText(val.gameObject, "0", 20, TextAnchor.MiddleRight, Color.white);
            var rowPrefab = PrefabUtility.SaveAsPrefabAsset(rowRt.gameObject, rowPath);
            Object.DestroyImmediate(rowRt.gameObject);

            var root = CreateStretch("UI_StatWindow", canvas, 0f);
            var window = CreateRect("UI_StatWindow_Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 760f));
            AddImage(window.gameObject, new Color(0.06f, 0.06f, 0.08f, 0.94f), _uiSprite).type = Image.Type.Sliced;

            var title = CreateRect("UI_StatWindow_Title", window, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(480f, 40f));
            AddText(title.gameObject, "STAT", 28, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.5f)).fontStyle = FontStyle.Bold;

            var closeRt = CreateRect("UI_StatWindow_Button_Close", window, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(48f, 48f));
            AddImage(closeRt.gameObject, new Color(0.5f, 0.15f, 0.15f), _uiSprite).type = Image.Type.Sliced;
            var closeButton = closeRt.gameObject.AddComponent<Button>();
            AddText(CreateStretch("UI_StatWindow_Button_Close_Label", closeRt, 0f).gameObject, "X", 26, TextAnchor.MiddleCenter, Color.white);

            var container = CreateRect("UI_StatWindow_Rows", window, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(460f, 670f));
            var layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;

            Set(root.gameObject.AddComponent<StatWindowView>(), ("stats", stats), ("progression", progression), ("input", input),
                ("window", window.gameObject), ("rowContainer", container), ("rowPrefab", rowPrefab),
                ("openButton", openButton), ("closeButton", closeButton));
        }

        // ================= UI: Quick Slots =================

        private static void BuildQuickSlotPanelUI(Transform canvas, PlayerInputHandler input, PlayerCombat combat, QuickSlotBar bar)
        {
            var panel = CreateRect("UI_QuickSlotPanel", canvas, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 16f), new Vector2(900f, 700f));
            Vector2 br = new(1f, 0f);
            Vector2 attackCenter = new(-140f, 200f);

            // 공격 버튼
            var attackRt = CreateRect("UI_Button_Attack", panel, br, new Vector2(0.5f, 0.5f), attackCenter, new Vector2(170f, 170f));
            AddImage(attackRt.gameObject, new Color(0.7f, 0.2f, 0.2f, 0.92f), _knobSprite);
            var attackButton = attackRt.gameObject.AddComponent<Button>();
            AddText(CreateRect("UI_Button_Attack_Label", attackRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(160f, 50f)).gameObject, "ATK", 38, TextAnchor.MiddleCenter, Color.white);
            AddKeyHint(attackRt, "UI_Button_Attack_KeyHint", new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(120f, 24f), 18, TextAnchor.LowerCenter, input, "Attack");

            // 자동 전투 버튼
            var autoRt = CreateRect("UI_Button_AutoCombat", panel, br, new Vector2(0.5f, 0.5f), new Vector2(-70f, 470f), new Vector2(110f, 60f));
            var autoBg = AddImage(autoRt.gameObject, new Color(0.25f, 0.25f, 0.28f), _uiSprite);
            autoBg.type = Image.Type.Sliced;
            var autoButton = autoRt.gameObject.AddComponent<Button>();
            var autoLabel = AddText(CreateStretch("UI_Button_AutoCombat_Label", autoRt, 0f).gameObject, "AUTO\nOFF", 22, TextAnchor.MiddleCenter, Color.white);
            AddKeyHint(autoRt, "UI_Button_AutoCombat_KeyHint", Vector2.zero, new Vector2(5f, 3f), new Vector2(60f, 20f), 15, TextAnchor.LowerLeft, input, "ToggleAutoCombat");

            Set(panel.gameObject.AddComponent<CombatButtonsView>(), ("input", input), ("combat", combat), ("attackButton", attackButton),
                ("autoButton", autoButton), ("autoBackground", autoBg), ("autoLabel", autoLabel));

            var slotPrefab = CreateQuickSlotPrefab();

            // 스킬 퀵슬롯 6칸: 공격 버튼을 감싸는 2겹 호 (안쪽 1~3, 바깥쪽 4~6)
            (float radius, float deg)[] arc = { (165f, 100f), (165f, 140f), (165f, 180f), (285f, 100f), (285f, 135f), (285f, 170f) };
            for (int i = 0; i < QuickSlotBar.SkillSlotCount; i++)
            {
                float rad = arc[i].deg * Mathf.Deg2Rad;
                Vector2 pos = attackCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * arc[i].radius;
                CreateQuickSlot(slotPrefab, panel, $"UI_QuickSlot_Skill_{i + 1}", pos, 96f, bar, input, i, (i + 1).ToString());
            }

            // 물약 퀵슬롯 4칸: 하단 가로줄
            string[] itemKeys = { "7", "8", "9", "0" };
            for (int i = 0; i < QuickSlotBar.ItemSlotCount; i++)
            {
                var pos = new Vector2(-872f + i * 104f, 60f);
                CreateQuickSlot(slotPrefab, panel, $"UI_QuickSlot_Item_{i + 1}", pos, 92f, bar, input,
                    QuickSlotBar.SkillSlotCount + i, itemKeys[i]);
            }
        }

        private static void CreateQuickSlot(GameObject prefab, Transform parent, string name, Vector2 pos, float size,
            QuickSlotBar bar, PlayerInputHandler input, int index, string key)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            Set(go.GetComponent<QuickSlotView>(), ("bar", bar), ("input", input), ("slotIndex", index));
            Set(go.transform.Find("UI_QuickSlot_KeyHint").GetComponent<KeyHintText>(), ("input", input), ("actionName", $"QuickSlot{index + 1}"));
        }

        private static GameObject CreateQuickSlotPrefab()
        {
            string path = Root + "/Prefabs/UI/UI_QuickSlot.prefab";

            var root = CreateRect("UI_QuickSlot", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            AddImage(root.gameObject, new Color(1f, 1f, 1f, 0f), null); // 포인터 입력 수신용(투명)

            // 자동 사용 표시 1: 맥동하는 금색 테두리 (프레임 뒤)
            var glowRt = CreateStretch("UI_QuickSlot_AutoGlow", root, -7f);
            var glow = AddImage(glowRt.gameObject, new Color(1f, 0.8f, 0.25f, 1f), _uiSprite, false);
            glow.type = Image.Type.Sliced;

            var frameRt = CreateStretch("UI_QuickSlot_Frame", root, 0f);
            AddImage(frameRt.gameObject, new Color(0.08f, 0.08f, 0.1f, 0.95f), _uiSprite, false).type = Image.Type.Sliced;
            frameRt.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var iconRt = CreateStretch("UI_QuickSlot_Icon", frameRt, 5f);
            var icon = AddImage(iconRt.gameObject, Color.gray, _uiSprite, false);
            icon.type = Image.Type.Sliced;

            var label = AddText(CreateStretch("UI_QuickSlot_Label", frameRt, 0f).gameObject, "", 20, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;

            // 자동 사용 표시 2: 슬롯 안을 도는 반짝임 (Mask로 잘림)
            var shineRt = CreateRect("UI_QuickSlot_AutoShine", frameRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 240f));
            var shine = AddImage(shineRt.gameObject, new Color(1f, 1f, 0.85f, 0.45f), _uiSprite, false);
            shine.type = Image.Type.Filled;
            shine.fillMethod = Image.FillMethod.Radial360;
            shine.fillAmount = 0.1f;

            var cdRt = CreateStretch("UI_QuickSlot_Cooldown", frameRt, 0f);
            var cd = AddImage(cdRt.gameObject, new Color(0f, 0f, 0f, 0.65f), _uiSprite, false);
            cd.type = Image.Type.Filled;
            cd.fillMethod = Image.FillMethod.Radial360;
            cd.fillOrigin = (int)Image.Origin360.Top;
            cd.fillClockwise = false;
            cd.fillAmount = 0f;

            var cdText = AddText(CreateStretch("UI_QuickSlot_CooldownText", frameRt, 0f).gameObject, "", 30, TextAnchor.MiddleCenter, Color.white);

            var count = AddText(CreateRect("UI_QuickSlot_Count", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-6f, 3f), new Vector2(60f, 24f)).gameObject,
                "", 18, TextAnchor.LowerRight, Color.white);
            count.gameObject.AddComponent<Outline>().effectColor = Color.black;

            var key = AddText(CreateRect("UI_QuickSlot_KeyHint", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -3f), new Vector2(30f, 22f)).gameObject,
                "1", 16, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
            key.gameObject.AddComponent<Outline>().effectColor = Color.black;

            // 자동 사용 표시 3: AUTO 배지
            var badgeRt = CreateRect("UI_QuickSlot_AutoBadge", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(56f, 20f));
            AddImage(badgeRt.gameObject, new Color(1f, 0.8f, 0.25f), _uiSprite, false).type = Image.Type.Sliced;
            AddText(CreateStretch("UI_QuickSlot_AutoBadge_Label", badgeRt, 0f).gameObject, "AUTO", 14, TextAnchor.MiddleCenter, Color.black).fontStyle = FontStyle.Bold;

            key.gameObject.AddComponent<KeyHintText>();
            Set(root.gameObject.AddComponent<QuickSlotView>(), ("icon", icon), ("label", label), ("countText", count),
                ("cooldownOverlay", cd), ("cooldownText", cdText), ("autoGlow", glow), ("autoShine", shineRt),
                ("autoBadge", badgeRt.gameObject));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static GameObject CreateJoystickPrefab()
        {
            string path = Root + "/Prefabs/UI/UI_MoveJoystick.prefab";

            var bgRect = CreateRect("UI_MoveJoystick", null, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(280f, 270f), new Vector2(300f, 300f));
            AddImage(bgRect.gameObject, new Color(1f, 1f, 1f, 0.75f), LoadSprite(JoystickBgSprite));

            var handleRect = CreateRect("UI_MoveJoystick_Handle", bgRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
            AddImage(handleRect.gameObject, Color.white, LoadSprite(JoystickHandleSprite), false);

            Set(bgRect.gameObject.AddComponent<VirtualJoystick>(), ("background", bgRect), ("handle", handleRect));

            var prefab = PrefabUtility.SaveAsPrefabAsset(bgRect.gameObject, path);
            Object.DestroyImmediate(bgRect.gameObject);
            return prefab;
        }

        // ================= World helpers =================

        private static GameObject CreateBlock(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.layer = layer;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NotWalkableArea;
            return go;
        }

        private static GameObject CreateFlatMarkerPrefab(string name, Material mat, float diameter)
        {
            string path = $"{Root}/Prefabs/World/{name}.prefab";
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.localScale = new Vector3(diameter, 0.02f, diameter);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ================= UI helpers =================

        /// <summary>현재 입력 방식에 맞는 키를 표시하는 텍스트 (KeyHintText)</summary>
        private static Text AddKeyHint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize,
            TextAnchor align, PlayerInputHandler input, string actionName)
        {
            var pivot = new Vector2(anchor.x, anchor.y);
            var rt = CreateRect(name, parent, anchor, pivot, pos, size);
            var text = AddText(rt.gameObject, string.Empty, fontSize, align, new Color(1f, 1f, 1f, 0.8f));
            Set(rt.gameObject.AddComponent<KeyHintText>(), ("input", input), ("actionName", actionName));
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform CreateStretch(string name, Transform parent, float padding)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            return rt;
        }

        private static Image AddImage(GameObject go, Color color, Sprite sprite, bool raycastTarget = true)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycastTarget;
            return img;
        }

        private static Text AddText(GameObject go, string text, int size, TextAnchor align, Color color)
        {
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ================= Asset helpers =================

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Sprite LoadSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning("[TestMapBuilder] 스프라이트를 찾을 수 없음: " + path);
            return sprite;
        }

        private static Material CreateLitMaterial(string name, Color color) =>
            CreateMaterial(name, "Universal Render Pipeline/Lit", color);

        private static Material CreateUnlitMaterial(string name, Color color) =>
            CreateMaterial(name, "Universal Render Pipeline/Unlit", color);

        private static Material CreateMaterial(string name, string shaderName, Color color)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>SerializedObject로 private [SerializeField] 값 설정 (중첩 경로 "a.b" 지원)</summary>
        private static void Set(Object target, params (string path, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (path, value) in values)
            {
                var p = so.FindProperty(path);
                if (p == null)
                {
                    Debug.LogError($"[TestMapBuilder] {target.GetType().Name}.{path} 를 찾을 수 없음");
                    continue;
                }

                switch (value)
                {
                    case float f: p.floatValue = f; break;
                    case int i when p.propertyType == SerializedPropertyType.Enum: p.enumValueIndex = i; break;
                    case int i: p.intValue = i; break;
                    case bool b: p.boolValue = b; break;
                    case string s: p.stringValue = s; break;
                    case Color c: p.colorValue = c; break;
                    case Vector2 v2: p.vector2Value = v2; break;
                    case Vector3 v3: p.vector3Value = v3; break;
                    case Object[] arr:
                        p.arraySize = arr.Length;
                        for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                        break;
                    case Object o: p.objectReferenceValue = o; break;
                    case null: p.objectReferenceValue = null; break;
                    default: Debug.LogError($"[TestMapBuilder] 지원하지 않는 타입: {value.GetType()}"); break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 6; i < 32; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(p.stringValue)) continue;
                p.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new System.Exception("빈 레이어 슬롯이 없습니다: " + name);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void AddSceneToBuild(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
