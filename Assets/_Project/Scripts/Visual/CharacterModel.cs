using System.Collections.Generic;
using RPGAuto.Combat;
using RPGAuto.Monster;
using RPGAuto.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace RPGAuto.Visual
{
    /// <summary>모델 애니메이션 방식 (Strategy 선택)</summary>
    public enum ModelAnimationMode
    {
        /// <summary>움직임 없음 (NPC 등)</summary>
        None,
        /// <summary>Animator 상태 이름으로 재생 (애니메이션 클립이 있는 모델)</summary>
        MecanimStates,
        /// <summary>휴머노이드 근육 값을 코드로 움직임 (클립이 없는 휴머노이드 모델)</summary>
        ProceduralHumanoid
    }

    /// <summary>
    /// 캐릭터/몬스터 외형. 판정(Capsule 콜라이더·NavMeshAgent)은 루트가 그대로 담당하고,
    /// 자식으로 붙은 모델만 보여 준다. 이동 속도·공격·피격·사망을 구독(Observer)해
    /// 애니메이션 전략(Strategy: IModelAnimationDriver)에 전달하고, 피격 시 붉게 깜빡이게 한다.
    /// </summary>
    public class CharacterModel : MonoBehaviour
    {
        [Header("Model")]
        [SerializeField] private Transform model;
        [SerializeField] private ModelAnimationMode mode = ModelAnimationMode.ProceduralHumanoid;
        [Tooltip("이 속도(m/s)에서 걷기 애니메이션이 최대가 된다")]
        [SerializeField] private float fullSpeed = 5f;

        [Header("Mecanim States")]
        [SerializeField] private string idleState = "Idle";
        [SerializeField] private string moveState = "Walk";
        [SerializeField] private string[] attackStates = { "Attack" };
        [SerializeField] private string hitState = "GetHitFront";
        [SerializeField] private string deathState = "Death";
        [Tooltip("이동 애니메이션 재생 속도 배율 (모델 크기를 줄였을 때 발이 미끄러지지 않게)")]
        [SerializeField] private float moveAnimSpeed = 1f;

        [Header("Procedural Humanoid")]
        [SerializeField] private ProceduralHumanoidDriver.Settings procedural = ProceduralHumanoidDriver.Settings.Default;

        [Header("Tint")]
        [SerializeField] private Color hitTint = new(1f, 0.45f, 0.45f);
        [SerializeField] private float hitTintDuration = 0.12f;
        [SerializeField] private bool tintOnDeath = true;
        [SerializeField] private Color deathTint = new(0.45f, 0.45f, 0.45f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private readonly List<Renderer> _renderers = new();
        private MaterialPropertyBlock _mpb;
        private IModelAnimationDriver _driver;
        private NavMeshAgent _agent;
        private CombatStats _stats;
        private PlayerSkillController _skills;
        private MonsterController _monster;
        private float _tintTimer;
        private bool _dead;

        public Transform Model => model;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<CombatStats>();
            _skills = GetComponent<PlayerSkillController>();
            _monster = GetComponent<MonsterController>();
            _mpb = new MaterialPropertyBlock();
            if (model != null) model.GetComponentsInChildren(true, _renderers);
            _driver = CreateDriver();
        }

        /// <summary>Strategy 선택: 모델 종류에 맞는 애니메이션 방식</summary>
        private IModelAnimationDriver CreateDriver()
        {
            var animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
            switch (mode)
            {
                case ModelAnimationMode.MecanimStates when animator != null:
                    return new MecanimStateDriver(animator, idleState, moveState, attackStates, hitState, deathState, moveAnimSpeed);
                case ModelAnimationMode.ProceduralHumanoid when animator != null && animator.avatar != null && animator.avatar.isHuman:
                    return new ProceduralHumanoidDriver(animator, model, procedural);
                default:
                    return new NullModelDriver();
            }
        }

        private void OnEnable()
        {
            if (_stats != null)
            {
                _stats.OnDamaged += HandleDamaged;
                _stats.OnDied += HandleDied;
            }
            if (_skills != null) _skills.OnSkillUsed += HandleSkillUsed;
            if (_monster != null) _monster.OnAttack += HandleAttack;
        }

        private void OnDisable()
        {
            if (_stats != null)
            {
                _stats.OnDamaged -= HandleDamaged;
                _stats.OnDied -= HandleDied;
            }
            if (_skills != null) _skills.OnSkillUsed -= HandleSkillUsed;
            if (_monster != null) _monster.OnAttack -= HandleAttack;
        }

        private void LateUpdate()
        {
            // 부활 / 풀에서 재사용 → 원래 모습으로
            if (_dead && _stats != null && !_stats.IsDead) SetDead(false);

            float speed = 0f;
            if (_agent != null && _agent.enabled)
            {
                var v = _agent.velocity;
                v.y = 0f;
                speed = v.magnitude;
            }
            _driver.Tick(Time.deltaTime, Mathf.Clamp01(speed / Mathf.Max(0.01f, fullSpeed)));

            if (_tintTimer > 0f)
            {
                _tintTimer -= Time.deltaTime;
                if (_tintTimer <= 0f) ApplyTint(_dead && tintOnDeath ? deathTint : (Color?)null);
            }
        }

        private void HandleSkillUsed(SkillSlot slot, MonsterController target)
        {
            if (slot != null && slot.Data != null && slot.Data.RequiresTarget) _driver.PlayAttack();
        }

        private void HandleAttack() => _driver.PlayAttack();

        private void HandleDamaged(float amount, CombatStats attacker)
        {
            if (_dead) return;
            _driver.PlayHit();
            _tintTimer = hitTintDuration;
            ApplyTint(hitTint);
        }

        private void HandleDied() => SetDead(true);

        private void SetDead(bool dead)
        {
            _dead = dead;
            _driver.SetDead(dead);
            _tintTimer = 0f;
            ApplyTint(dead && tintOnDeath ? deathTint : (Color?)null);
        }

        /// <summary>모든 모델 렌더러에 색을 곱한다 (null이면 원래 색)</summary>
        private void ApplyTint(Color? tint)
        {
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (tint == null) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, tint.Value);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
