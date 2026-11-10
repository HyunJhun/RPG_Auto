using System.Linq;
using RPGAuto.Monster;
using RPGAuto.Player;
using RPGAuto.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace RPGAuto.EditorTools
{
    /// <summary>
    /// 플레이어·NPC·몬스터 프리팹에 외부 모델을 입힌다 (Capsule 판정은 그대로, 메시만 숨김).
    /// 메뉴: RPGAuto > Build Character Models  (TestMapBuilder 실행 시 자동 호출, 마지막 단계)
    /// </summary>
    public static class ModelBuilder
    {
        private const string PrimitiveRoot = "Assets/Primitive_Characters_Pack";
        private const string ProtoRoot = "Assets/Protofactor/Sci Fi/SciFiCharactersMegaPackVol3/SciFiBossesVol2/Megaspikan";

        public const string PlayerModel = PrimitiveRoot + "/Primitive_01/Prefabs/Primitive_Character_01.prefab";
        public const string PlayerWeapon = PrimitiveRoot + "/Primitive_01/Prefabs/Primitive_Weapon_02.prefab";
        public const string MerchantModel = PrimitiveRoot + "/Primitive_03/Prefabs/Primitive_03_01.prefab";
        public const string WarriorModel = PrimitiveRoot + "/Primitive_04/Prefabs/Primitive_04_01.prefab";
        public const string WarriorWeapon = PrimitiveRoot + "/Primitive_04/Prefabs/Primitive_04_Weapon.prefab";
        public const string LarvaModel = ProtoRoot + "/MegaspikanLarvae/Prefab/MegaspikanLarva.prefab";
        public const string BossModel = ProtoRoot + "/Prefab/Megaspikan.prefab";

        private const string MonsterFolder = UIBuildKit.Root + "/Prefabs/Monster";

        [MenuItem("RPGAuto/Build Character Models")]
        public static void BuildMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[ModelBuilder] 완료");
        }

        public static void ApplyToOpenScene()
        {
            ConvertStandardMaterials(PrimitiveRoot);
            ConvertStandardMaterials("Assets/Protofactor");

            // ---------- 플레이어 ----------
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.Exception("씬에 Player가 없습니다.");
            var pm = Attach(player.gameObject, PlayerModel, 1.85f, ModelAnimationMode.ProceduralHumanoid);
            AttachWeapon(pm, PlayerWeapon, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
            var facing = player.transform.Find("Player_FacingIndicator");
            if (facing != null) facing.gameObject.SetActive(false);
            EditorUtility.SetDirty(player.gameObject);

            // ---------- 상인 NPC ----------
            var npc = GameObject.Find(NpcBuilder.MerchantName);
            if (npc != null) Attach(npc, MerchantModel, 1.85f, ModelAnimationMode.ProceduralHumanoid);

            // ---------- 몬스터 프리팹 ----------
            EditMonster("Monster_Goblin", go =>
            {
                Attach(go, LarvaModel, 1.3f, ModelAnimationMode.MecanimStates, m => SetStates(m, "Idle", "Walk", new[] { "JumpBite", "SpitAttack" }, "GetHitFront", "Death", 1.6f));
            });
            EditMonster("Monster_GoblinWarrior", go =>
            {
                var wm = Attach(go, WarriorModel, 1.9f, ModelAnimationMode.ProceduralHumanoid);
                AttachWeapon(wm, WarriorWeapon, Vector3.zero, Vector3.zero);
            });
            EditMonster("Monster_GoblinChief", go =>
            {
                Attach(go, BossModel, 1.9f, ModelAnimationMode.MecanimStates, m => SetStates(m, "Idle", "WalkForward",
                    new[] { "LeftClawsAttack", "RightClawsAttack", "LeftSmashAttack" }, "GetHitFront", "Death", 1.4f));
            });

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        // ================= 공통 =================

        private static void EditMonster(string prefabName, System.Action<GameObject> edit)
        {
            string path = $"{MonsterFolder}/{prefabName}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// host 아래에 모델을 붙인다. 높이(host 로컬 단위)에 맞춰 크기를 정하고 발을 바닥(NavMeshAgent 기준)에 맞춘다.
        /// </summary>
        private static Transform Attach(GameObject host, string modelPath, float height, ModelAnimationMode mode,
            System.Action<CharacterModel> configure = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null) throw new System.Exception("모델 없음: " + modelPath);

            var old = host.transform.Find("Model");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Capsule 메시는 숨기고 콜라이더·에이전트는 그대로 둔다
            var capsuleRenderer = host.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null) capsuleRenderer.enabled = false;

            var holder = new GameObject("Model").transform;
            holder.SetParent(host.transform, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            foreach (var c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = host.layer;
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; // 크기를 줄인 뒤 컬링 경계가 어긋나지 않게
            }
            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            // 크기: 모델 높이를 host 로컬 높이에 맞춤
            float hostScaleY = host.transform.lossyScale.y;
            var b = WorldBounds(instance);
            float localHeight = b.size.y / hostScaleY;
            float scale = localHeight > 0.0001f ? height / localHeight : 1f;
            holder.localScale = Vector3.one * scale;

            // 위치: 발을 바닥에 (NavMeshAgent baseOffset만큼 아래가 바닥)
            var agent = host.GetComponent<NavMeshAgent>();
            float footLocal = agent != null ? -agent.baseOffset : -1f;
            float footWorldY = host.transform.position.y + footLocal * hostScaleY;
            b = WorldBounds(instance);
            holder.position += Vector3.up * (footWorldY - b.min.y);
            // 모델 중심이 Capsule 중심에 오도록 수평 보정
            var center = b.center;
            var offset = host.transform.position - center;
            offset.y = 0f;
            holder.position += offset;

            // 다시 빌드할 때 이전 설정값이 남지 않도록 새로 붙인다
            var oldCm = host.GetComponent<CharacterModel>();
            if (oldCm != null) Object.DestroyImmediate(oldCm);
            var cm = host.AddComponent<CharacterModel>();
            UIBuildKit.Set(cm, ("model", instance.transform), ("mode", mode), ("fullSpeed", 5f));
            configure?.Invoke(cm);
            return instance.transform;
        }

        private static void SetStates(CharacterModel cm, string idle, string move, string[] attacks, string hit, string death, float moveAnimSpeed)
        {
            UIBuildKit.Set(cm, ("idleState", idle), ("moveState", move), ("hitState", hit), ("deathState", death), ("moveAnimSpeed", moveAnimSpeed));
            var so = new SerializedObject(cm);
            var arr = so.FindProperty("attackStates");
            arr.arraySize = attacks.Length;
            for (int i = 0; i < attacks.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = attacks[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>휴머노이드 오른손 뼈에 무기를 붙인다 (아바타 뼈 이름으로 찾음)</summary>
        private static void AttachWeapon(Transform model, string weaponPath, Vector3 localPos, Vector3 localEuler)
        {
            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return;
            var boneName = animator.avatar.humanDescription.human.FirstOrDefault(h => h.humanName == "RightHand").boneName;
            var hand = string.IsNullOrEmpty(boneName) ? null : model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == boneName);
            if (hand == null) { Debug.LogWarning("[ModelBuilder] 오른손 뼈를 찾지 못했습니다: " + model.name); return; }

            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath), hand);
            weapon.name = "Weapon_" + weapon.name;
            weapon.transform.localPosition = localPos;
            // 기본(T) 자세에서 무기 끝(+Y)이 캐릭터 정면을 향하게: 팔을 내려도(정면 축 회전) 앞을 겨누고, 공격 시 위아래로 휘둘러진다
            weapon.transform.rotation = Quaternion.FromToRotation(Vector3.up, model.forward) * Quaternion.Euler(localEuler);
            foreach (var c in weapon.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in weapon.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = model.gameObject.layer;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ================= 머티리얼 =================

        /// <summary>Built-in Standard 셰이더 머티리얼을 URP Lit으로 변환 (텍스처·색 유지)</summary>
        public static void ConvertStandardMaterials(string folder)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || !mat.shader.name.StartsWith("Standard")) continue;

                Texture main = Tex(mat, "_MainTex"), bump = Tex(mat, "_BumpMap"), metal = Tex(mat, "_MetallicGlossMap"),
                        occ = Tex(mat, "_OcclusionMap"), emis = Tex(mat, "_EmissionMap");
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Color emisColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                float smooth = mat.HasProperty("_GlossMapScale") && metal != null ? mat.GetFloat("_GlossMapScale")
                             : mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.4f;
                float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
                bool emissionOn = mat.IsKeywordEnabled("_EMISSION") || emis != null;

                mat.shader = lit;
                mat.shaderKeywords = new string[0];
                mat.SetTexture("_BaseMap", main);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smooth);
                if (bump != null) { mat.SetTexture("_BumpMap", bump); mat.SetFloat("_BumpScale", bumpScale); mat.EnableKeyword("_NORMALMAP"); }
                if (metal != null) { mat.SetTexture("_MetallicGlossMap", metal); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                if (occ != null) { mat.SetTexture("_OcclusionMap", occ); mat.EnableKeyword("_OCCLUSIONMAP"); }
                if (emissionOn)
                {
                    if (emis != null) mat.SetTexture("_EmissionMap", emis);
                    mat.SetColor("_EmissionColor", emisColor);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(mat);
            }
        }

        private static Texture Tex(Material m, string prop) => m.HasProperty(prop) ? m.GetTexture(prop) : null;
    }
}
