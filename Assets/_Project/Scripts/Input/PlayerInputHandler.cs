using System;
using System.Collections.Generic;
using RPGAuto.Monster;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPGAuto.InputHandling
{
    /// <summary>
    /// Input Action(RPGAutoInput.inputactions)을 읽어 게임 이벤트로 바꿔 알린다 (Observer).
    /// 키보드·게임패드·가상 조이스틱·마우스·터치는 모두 액션 바인딩에서 합쳐지므로
    /// 이 클래스는 장치를 직접 알지 않는다. UI 버튼도 Press* 메서드로 같은 경로를 탄다.
    /// </summary>
    // EventSystem(UI 입력 모듈)이 이번 프레임 포인터를 처리한 뒤에 실행되도록 늦춘다
    [DefaultExecutionOrder(100)]
    public class PlayerInputHandler : MonoBehaviour
    {
        public const int QuickSlotCount = 10;

        [Header("References")]
        [SerializeField] private Camera viewCamera;

        [Header("Pointer")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private float rayDistance = 500f;
        [Tooltip("몬스터 탭 판정: 누른 지점에서 이만큼(px) 이상 움직이면 탭이 아닌 지면 드래그로 전환")]
        [SerializeField] private float tapMaxMovePixels = 25f;
        [SerializeField] private float tapMaxDuration = 0.5f;

        [Header("Hold To Move")]
        [Tooltip("터치 유지 중 목적지 갱신 주기(초)")]
        [SerializeField] private float holdUpdateInterval = 0.1f;
        [Tooltip("목적지가 이 거리(m) 이상 바뀌었을 때만 갱신")]
        [SerializeField] private float holdMinMoveDistance = 0.3f;

        /// <summary>정규화된 이동 입력 (x: 좌우, y: 앞뒤), 크기 0~1</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>지면을 누른 채 유지 중 (자동 전투 재개를 미룸)</summary>
        public bool IsHoldingGround => _pressing && _isGroundHold;

        /// <summary>입력 액션 (키 변경·바인딩 확인 등 외부에서 접근할 때 사용)</summary>
        public RPGAutoInputActions Actions => _actions;

        /// <summary>마지막으로 입력이 들어온 장치 기준의 입력 방식</summary>
        public InputScheme CurrentScheme { get; private set; }

        /// <summary>이동 입력이 실제 게임패드에서 오는 중 (화면 조이스틱 핸들 표시용)</summary>
        public bool IsMoveFromGamepad { get; private set; }

        /// <summary>입력 방식 변경 알림 (Observer) — 키 표시 UI 등이 구독</summary>
        public event Action<InputScheme> OnInputSchemeChanged;

        /// <summary>지면 이동 명령 — 누르는 순간 + 누른 채 위치가 바뀔 때마다 발생</summary>
        public event Action<Vector3> OnGroundTapped;
        public event Action<MonsterController> OnTargetTapped;
        public event Action OnAttackPressed;
        /// <summary>퀵슬롯 사용 (0~5 스킬, 6~9 물약)</summary>
        public event Action<int> OnQuickSlotPressed;
        public event Action OnStatWindowTogglePressed;
        public event Action OnInventoryTogglePressed;
        public event Action OnAutoCombatTogglePressed;

        private RPGAutoInputActions _actions;
        private InputAction[] _quickSlotActions;
        private Action<InputAction.CallbackContext>[] _quickSlotHandlers;

        private bool _pressing;
        private bool _pressStartedOverUI;
        private Vector2 _pressPosition;
        private float _pressTime;
        private MonsterController _pressedMonster;
        private bool _isGroundHold;
        private Vector3 _lastHoldPoint;
        private float _holdTimer;

        private PointerEventData _pointerEventData;
        private readonly List<RaycastResult> _uiRaycastResults = new();

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;

            _actions = new RPGAutoInputActions();
            CurrentScheme = DetectInitialScheme();
            var p = _actions.Player;
            _quickSlotActions = new[]
            {
                p.QuickSlot1, p.QuickSlot2, p.QuickSlot3, p.QuickSlot4, p.QuickSlot5,
                p.QuickSlot6, p.QuickSlot7, p.QuickSlot8, p.QuickSlot9, p.QuickSlot10
            };
            _quickSlotHandlers = new Action<InputAction.CallbackContext>[QuickSlotCount];
            for (int i = 0; i < QuickSlotCount; i++)
            {
                int index = i; // 클로저 캡처용
                _quickSlotHandlers[i] = _ => PressQuickSlot(index);
            }
        }

        private void OnEnable()
        {
            var p = _actions.Player;
            p.Attack.performed += HandleAttack;
            p.ToggleAutoCombat.performed += HandleToggleAuto;
            p.ToggleStatWindow.performed += HandleToggleStat;
            p.ToggleInventory.performed += HandleToggleInventory;
            for (int i = 0; i < QuickSlotCount; i++) _quickSlotActions[i].performed += _quickSlotHandlers[i];
            p.Get().actionTriggered += HandleAnyAction;
            p.Enable();
        }

        private void OnDisable()
        {
            var p = _actions.Player;
            p.Disable();
            p.Attack.performed -= HandleAttack;
            p.ToggleAutoCombat.performed -= HandleToggleAuto;
            p.ToggleStatWindow.performed -= HandleToggleStat;
            p.ToggleInventory.performed -= HandleToggleInventory;
            for (int i = 0; i < QuickSlotCount; i++) _quickSlotActions[i].performed -= _quickSlotHandlers[i];
            p.Get().actionTriggered -= HandleAnyAction;

            _pressing = false;
            _isGroundHold = false;
            MoveInput = Vector2.zero;
        }

        private void OnDestroy() => _actions?.Dispose();

        private void Update()
        {
            var move = _actions.Player.Move;
            MoveInput = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            var moveControl = move.activeControl;
            IsMoveFromGamepad = moveControl != null && IsPhysicalGamepad(moveControl.device);
            ReadPointer();
        }

        // ---------------- 입력 방식 ----------------

        /// <summary>현재 입력 방식 기준으로 액션의 키 이름 (없으면 빈 문자열)</summary>
        public string GetBindingLabel(string actionName) => GetBindingLabel(actionName, CurrentScheme);

        public string GetBindingLabel(string actionName, InputScheme scheme) =>
            InputBindingLabels.Get(_actions.asset.FindAction(actionName), scheme);

        private void HandleAnyAction(InputAction.CallbackContext ctx)
        {
            var device = ctx.control?.device;
            if (device == null) return;

            InputScheme? scheme = device switch
            {
                Touchscreen => InputScheme.Touch,
                Gamepad => IsPhysicalGamepad(device) ? InputScheme.Gamepad : (InputScheme?)null, // 화면 조이스틱의 가상 패드는 무시
                Keyboard or Mouse or Pen => InputScheme.KeyboardMouse,
                _ => null
            };
            if (scheme.HasValue) SetScheme(scheme.Value);
        }

        private void SetScheme(InputScheme scheme)
        {
            if (CurrentScheme == scheme) return;
            CurrentScheme = scheme;
            OnInputSchemeChanged?.Invoke(scheme);
        }

        /// <summary>실제 연결된 게임패드인지 (OnScreenControl이 만든 가상 패드는 native가 아님)</summary>
        private static bool IsPhysicalGamepad(InputDevice device) => device is Gamepad && device.native;

        private static InputScheme DetectInitialScheme()
        {
            if (Application.isMobilePlatform) return InputScheme.Touch;
            foreach (var device in InputSystem.devices)
                if (IsPhysicalGamepad(device) && Keyboard.current == null) return InputScheme.Gamepad;
            return InputScheme.KeyboardMouse;
        }

        // 액션 콜백
        private void HandleAttack(InputAction.CallbackContext _) => PressAttack();
        private void HandleToggleAuto(InputAction.CallbackContext _) => PressAutoCombatToggle();
        private void HandleToggleStat(InputAction.CallbackContext _) => PressStatWindowToggle();
        private void HandleToggleInventory(InputAction.CallbackContext _) => PressInventoryToggle();

        // UI 버튼/액션 공용 진입점
        public void PressAttack() => OnAttackPressed?.Invoke();
        public void PressQuickSlot(int index) => OnQuickSlotPressed?.Invoke(index);
        public void PressStatWindowToggle() => OnStatWindowTogglePressed?.Invoke();
        public void PressInventoryToggle() => OnInventoryTogglePressed?.Invoke();
        public void PressAutoCombatToggle() => OnAutoCombatTogglePressed?.Invoke();

        private void ReadPointer()
        {
            var p = _actions.Player;
            Vector2 screenPos = p.Point.ReadValue<Vector2>();

            if (p.Press.WasPressedThisFrame()) BeginPress(screenPos);

            if (_pressing && !_pressStartedOverUI) UpdatePress(screenPos);

            if (_pressing && p.Press.WasReleasedThisFrame()) EndPress(screenPos);
        }

        private void BeginPress(Vector2 screenPos)
        {
            _pressing = true;
            _pressPosition = screenPos;
            _pressTime = Time.unscaledTime;
            _pressStartedOverUI = IsOverUI(screenPos);
            _pressedMonster = null;
            _isGroundHold = false;

            if (_pressStartedOverUI) return;
            if (!RaycastWorld(screenPos, groundMask | targetMask, out var hit)) return;

            var monster = hit.collider.GetComponentInParent<MonsterController>();
            if (monster != null)
            {
                _pressedMonster = monster; // 떼는 순간 탭이면 타겟 지정
                return;
            }

            StartGroundHold(hit.point);
        }

        private void UpdatePress(Vector2 screenPos)
        {
            // 몬스터를 누른 뒤 손가락을 끌면 지면 이동으로 전환
            if (_pressedMonster != null)
            {
                if ((screenPos - _pressPosition).sqrMagnitude <= tapMaxMovePixels * tapMaxMovePixels) return;
                _pressedMonster = null;
                if (RaycastWorld(screenPos, groundMask, out var groundHit)) StartGroundHold(groundHit.point);
                return;
            }

            if (!_isGroundHold) return;

            _holdTimer -= Time.unscaledDeltaTime;
            if (_holdTimer > 0f) return;
            _holdTimer = holdUpdateInterval;

            // 유지 중에는 지면만 검사 (몬스터 위를 지나가도 끊기지 않게)
            if (RaycastWorld(screenPos, groundMask, out var hit)) UpdateHoldPoint(hit.point);
        }

        private void EndPress(Vector2 screenPos)
        {
            _pressing = false;
            if (_pressStartedOverUI) return;

            if (_isGroundHold)
            {
                if (RaycastWorld(screenPos, groundMask, out var hit)) UpdateHoldPoint(hit.point);
                _isGroundHold = false;
                return;
            }

            if (_pressedMonster != null)
            {
                bool isTap = (screenPos - _pressPosition).sqrMagnitude <= tapMaxMovePixels * tapMaxMovePixels
                             && Time.unscaledTime - _pressTime <= tapMaxDuration;
                if (isTap && _pressedMonster.IsAlive) OnTargetTapped?.Invoke(_pressedMonster);
                _pressedMonster = null;
            }
        }

        private void StartGroundHold(Vector3 point)
        {
            _isGroundHold = true;
            _holdTimer = holdUpdateInterval;
            _lastHoldPoint = point;
            OnGroundTapped?.Invoke(point);
        }

        private void UpdateHoldPoint(Vector3 point)
        {
            if ((point - _lastHoldPoint).sqrMagnitude < holdMinMoveDistance * holdMinMoveDistance) return;
            _lastHoldPoint = point;
            OnGroundTapped?.Invoke(point);
        }

        /// <summary>화면 좌표 단발 탭 처리 (테스트/외부 호출용): 몬스터면 타겟 지정, 아니면 지면 이동</summary>
        public void HandleWorldTap(Vector2 screenPosition)
        {
            if (!RaycastWorld(screenPosition, groundMask | targetMask, out var hit)) return;

            var monster = hit.collider.GetComponentInParent<MonsterController>();
            if (monster != null)
            {
                if (monster.IsAlive) OnTargetTapped?.Invoke(monster);
                return;
            }
            OnGroundTapped?.Invoke(hit.point);
        }

        private bool RaycastWorld(Vector2 screenPos, LayerMask mask, out RaycastHit hit)
        {
            hit = default;
            if (viewCamera == null) return false;
            Ray ray = viewCamera.ScreenPointToRay(screenPos);
            return Physics.Raycast(ray, out hit, rayDistance, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 포인터가 UI 위인지 판정. UI 입력 모듈의 판정을 우선 사용하고(마우스/펜, 터치 ID별),
        /// 보조로 현재 좌표 레이캐스트도 확인한다.
        /// </summary>
        private bool IsOverUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            if (eventSystem.IsPointerOverGameObject()) return true;

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    if (eventSystem.IsPointerOverGameObject(touch.touchId.ReadValue())) return true;
                }
            }

            _pointerEventData ??= new PointerEventData(eventSystem);
            _pointerEventData.position = screenPosition;
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }
    }
}
