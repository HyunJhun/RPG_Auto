using System;
using UnityEngine;

namespace RPGAuto.Visual
{
    /// <summary>모델 애니메이션 전략 (Strategy). CharacterModel이 모델 종류에 맞게 하나를 고른다.</summary>
    public interface IModelAnimationDriver
    {
        /// <summary>매 프레임. speed01 = 이동 속도 (0 = 정지, 1 = 최고 속도)</summary>
        void Tick(float deltaTime, float speed01);
        void PlayAttack();
        void PlayHit();
        void SetDead(bool dead);
    }

    /// <summary>움직이지 않는 모델</summary>
    public class NullModelDriver : IModelAnimationDriver
    {
        public void Tick(float deltaTime, float speed01) { }
        public void PlayAttack() { }
        public void PlayHit() { }
        public void SetDead(bool dead) { }
    }

    /// <summary>
    /// Animator 상태를 이름으로 재생한다 (전이·파라미터가 없는 컨트롤러용).
    /// 공격/피격 동작이 끝날 때까지는 이동/대기 상태로 덮어쓰지 않는다.
    /// </summary>
    public class MecanimStateDriver : IModelAnimationDriver
    {
        private const float MoveThreshold = 0.08f;
        private readonly Animator _animator;
        private readonly string _idle, _move, _hit, _death;
        private readonly string[] _attacks;
        private readonly float _moveAnimSpeed;
        private string _current;
        private float _lockTimer;
        private int _attackIndex;
        private bool _dead;

        public MecanimStateDriver(Animator animator, string idle, string move, string[] attacks, string hit, string death, float moveAnimSpeed)
        {
            _animator = animator;
            _animator.applyRootMotion = false;
            _animator.enabled = true;
            _idle = Valid(idle);
            _move = Valid(move);
            _hit = Valid(hit);
            _death = Valid(death);
            _attacks = Array.FindAll(attacks ?? Array.Empty<string>(), s => Valid(s) != null);
            _moveAnimSpeed = moveAnimSpeed;
        }

        private string Valid(string state) =>
            !string.IsNullOrEmpty(state) && _animator.HasState(0, Animator.StringToHash(state)) ? state : null;

        public void Tick(float deltaTime, float speed01)
        {
            if (_dead) return;
            if (_lockTimer > 0f) { _lockTimer -= deltaTime; return; }

            bool moving = speed01 > MoveThreshold && _move != null;
            _animator.speed = moving ? Mathf.Lerp(0.6f, 1f, speed01) * _moveAnimSpeed : 1f;
            Play(moving ? _move : _idle, 0.15f);
        }

        public void PlayAttack()
        {
            if (_dead || _attacks.Length == 0) return;
            string state = _attacks[_attackIndex++ % _attacks.Length];
            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(state, 0.08f, 0, 0f);
            _current = state;
            _lockTimer = Mathf.Min(ClipLength(state), 1.4f);
        }

        public void PlayHit()
        {
            // 공격 동작 중에는 끊지 않는다
            if (_dead || _hit == null || _lockTimer > 0f) return;
            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(_hit, 0.05f, 0, 0f);
            _current = _hit;
            _lockTimer = Mathf.Min(ClipLength(_hit), 0.5f);
        }

        public void SetDead(bool dead)
        {
            _dead = dead;
            _lockTimer = 0f;
            _animator.speed = 1f;
            if (dead && _death != null)
            {
                _animator.CrossFadeInFixedTime(_death, 0.1f, 0, 0f);
                _current = _death;
            }
            else if (!dead && _idle != null)
            {
                _animator.Play(_idle, 0, 0f);
                _current = _idle;
            }
        }

        private void Play(string state, float fade)
        {
            if (state == null || state == _current) return;
            _animator.CrossFadeInFixedTime(state, fade);
            _current = state;
        }

        private float ClipLength(string state)
        {
            var controller = _animator.runtimeAnimatorController;
            if (controller != null)
                foreach (var clip in controller.animationClips)
                    if (clip != null && clip.name == state) return clip.length;
            return 0.6f;
        }
    }

    /// <summary>
    /// 애니메이션 클립이 없는 휴머노이드 모델을 코드로 움직인다.
    /// 뼈 회전 대신 휴머노이드 '근육' 값(HumanPose)을 쓰므로 리그마다 축이 달라도 같은 동작이 나온다.
    /// 걷기(팔다리 교차) · 공격(오른팔 휘두르기) · 피격(뒤로 젖힘) · 사망(뒤로 쓰러짐)
    /// </summary>
    public class ProceduralHumanoidDriver : IModelAnimationDriver
    {
        [Serializable]
        public struct Settings
        {
            [Tooltip("팔 내림 (근육 값, -1 = 완전히 내림)")] public float armDown;
            [Tooltip("팔꿈치 굽힘 (근육 값)")] public float forearmBend;
            [Tooltip("걸음 주기 (초당 라디안)")] public float stepFrequency;
            [Tooltip("다리 앞뒤 흔들림")] public float legSwing;
            [Tooltip("팔 앞뒤 흔들림")] public float armSwing;
            [Tooltip("공격 동작 시간(초)")] public float attackDuration;

            public static Settings Default => new()
            {
                armDown = -0.55f, forearmBend = 0.45f, stepFrequency = 9f, legSwing = 0.3f, armSwing = 0.3f, attackDuration = 0.4f
            };
        }

        private readonly HumanPoseHandler _handler;
        private readonly Transform _modelRoot;
        private readonly Quaternion _baseRotation;
        private readonly Vector3 _basePosition;
        private readonly Settings _s;
        private HumanPose _pose;
        private readonly float[] _baseMuscles;
        private readonly Vector3 _bodyPosition;
        private readonly Quaternion _bodyRotation;

        private readonly int _armDownL, _armDownR, _armFrontL, _armFrontR, _foreL, _foreR;
        private readonly int _legFrontL, _legFrontR, _lowerL, _lowerR, _spine, _chest, _head;

        private float _phase;
        private float _attackTimer;
        private float _hitTimer;
        private float _deathBlend;
        private bool _dead;

        public ProceduralHumanoidDriver(Animator animator, Transform modelRoot, Settings settings)
        {
            _s = settings;
            _modelRoot = modelRoot;
            _baseRotation = modelRoot.localRotation;
            _basePosition = modelRoot.localPosition;
            animator.enabled = false; // 클립이 없으므로 Animator가 자세를 덮어쓰지 않게
            _handler = new HumanPoseHandler(animator.avatar, animator.transform);
            _pose = new HumanPose();
            _handler.GetHumanPose(ref _pose);
            _baseMuscles = (float[])_pose.muscles.Clone();
            _bodyPosition = _pose.bodyPosition;
            _bodyRotation = _pose.bodyRotation;

            _armDownL = Muscle("Left Arm Down-Up");
            _armDownR = Muscle("Right Arm Down-Up");
            _armFrontL = Muscle("Left Arm Front-Back");
            _armFrontR = Muscle("Right Arm Front-Back");
            _foreL = Muscle("Left Forearm Stretch");
            _foreR = Muscle("Right Forearm Stretch");
            _legFrontL = Muscle("Left Upper Leg Front-Back");
            _legFrontR = Muscle("Right Upper Leg Front-Back");
            _lowerL = Muscle("Left Lower Leg Stretch");
            _lowerR = Muscle("Right Lower Leg Stretch");
            _spine = Muscle("Spine Front-Back");
            _chest = Muscle("Chest Front-Back");
            _head = Muscle("Head Nod Down-Up");
        }

        private static int Muscle(string name) => Array.IndexOf(HumanTrait.MuscleName, name);

        public void Tick(float deltaTime, float speed01)
        {
            if (_attackTimer > 0f) _attackTimer -= deltaTime;
            if (_hitTimer > 0f) _hitTimer -= deltaTime;
            _deathBlend = Mathf.MoveTowards(_deathBlend, _dead ? 1f : 0f, deltaTime * 3f);
            _phase += deltaTime * _s.stepFrequency * Mathf.Lerp(0.6f, 1f, speed01);

            Array.Copy(_baseMuscles, _pose.muscles, _baseMuscles.Length);
            _pose.bodyPosition = _bodyPosition;
            _pose.bodyRotation = _bodyRotation;

            float swing = Mathf.Sin(_phase) * speed01;
            float breath = Mathf.Sin(Time.time * 2f) * 0.03f * (1f - speed01);

            // 기본 자세: 팔 내리고 팔꿈치 살짝 굽힘
            Set(_armDownL, _s.armDown);
            Set(_armDownR, _s.armDown);
            Set(_foreL, _s.forearmBend);
            Set(_foreR, _s.forearmBend);

            // 걷기: 다리 교차, 팔은 다리와 반대로 (리그마다 기본값이 달라 기본 자세에 '더한다')
            Add(_legFrontL, swing * _s.legSwing);
            Add(_legFrontR, -swing * _s.legSwing);
            Add(_lowerL, -Mathf.Max(0f, Mathf.Sin(_phase + 1.2f)) * 0.35f * speed01);
            Add(_lowerR, -Mathf.Max(0f, Mathf.Sin(_phase + Mathf.PI + 1.2f)) * 0.35f * speed01);
            Add(_armFrontL, -swing * _s.armSwing);
            Add(_armFrontR, swing * _s.armSwing);
            Add(_chest, breath);

            // 공격: 오른팔을 뒤로 들었다가 앞으로 내려친다
            if (_attackTimer > 0f)
            {
                float t = 1f - _attackTimer / _s.attackDuration;            // 0 → 1
                float windUp = Mathf.Clamp01(t / 0.35f);
                float strike = Mathf.Clamp01((t - 0.35f) / 0.3f);
                float recover = Mathf.Clamp01((t - 0.7f) / 0.3f);
                float raise = Mathf.Lerp(windUp, 0f, strike);
                Set(_armDownR, Mathf.Lerp(_s.armDown, 0.9f, raise) * (1f - recover) + _s.armDown * recover);
                Add(_armFrontR, Mathf.Lerp(-0.3f, 1f, strike) * (1f - recover));
                Set(_foreR, Mathf.Lerp(0.2f, 1f, strike));
                Add(_chest, Mathf.Lerp(-0.15f, 0.3f, strike) * (1f - recover));
                Add(_spine, Mathf.Lerp(-0.1f, 0.2f, strike) * (1f - recover));
            }

            // 피격: 뒤로 젖힘
            if (_hitTimer > 0f)
            {
                float k = _hitTimer / 0.25f;
                Add(_spine, -0.25f * k);
                Add(_head, -0.2f * k);
            }

            // 사망: 몸을 숙이고 뒤로 쓰러짐
            if (_deathBlend > 0f)
            {
                Add(_spine, 0.5f * _deathBlend);
                Set(_armDownL, Mathf.Lerp(Get(_armDownL), -0.2f, _deathBlend));
                Set(_armDownR, Mathf.Lerp(Get(_armDownR), -0.2f, _deathBlend));
                float e = _deathBlend * _deathBlend * (3f - 2f * _deathBlend);
                _modelRoot.localRotation = _baseRotation * Quaternion.Euler(-85f * e, 0f, 0f);
                _modelRoot.localPosition = _basePosition + Vector3.up * (0.25f * e);
            }
            else
            {
                _modelRoot.localRotation = _baseRotation;
                _modelRoot.localPosition = _basePosition;
            }

            _handler.SetHumanPose(ref _pose);
        }

        public void PlayAttack()
        {
            if (_dead) return;
            _attackTimer = _s.attackDuration;
        }

        public void PlayHit()
        {
            if (_dead) return;
            _hitTimer = 0.25f;
        }

        public void SetDead(bool dead)
        {
            _dead = dead;
            if (!dead) _deathBlend = 0f;
            _attackTimer = 0f;
            _hitTimer = 0f;
        }

        private void Set(int index, float value)
        {
            if (index >= 0) _pose.muscles[index] = Mathf.Clamp(value, -1f, 1f);
        }

        private float Get(int index) => index >= 0 ? _pose.muscles[index] : 0f;

        /// <summary>현재 값에 더한다 (기본 자세 기준 오프셋)</summary>
        private void Add(int index, float delta)
        {
            if (index >= 0) _pose.muscles[index] = Mathf.Clamp(_pose.muscles[index] + delta, -1f, 1f);
        }
    }
}
