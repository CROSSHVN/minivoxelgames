using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VoxelCore;
using VoxelCore.Entities;
using PlayerInput = VoxelCore.Entities.PlayerInput;

namespace Client.Player
{
    /// <summary>
    /// Контроллер игрока для клиента Unity (WebGL / ПК).
    /// Связывает пользовательский ввод (Mouse/Keyboard), физическую симуляцию PlayerEntity (20 TPS)
    /// и плавный рендеринг камеры от первого лица с интерполяцией между тиками.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Камера и визуал")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private float _mouseSensitivity = 0.12f;
        [SerializeField] private float _eyeHeightStanding = 1.62f;
        [SerializeField] private float _eyeHeightSneaking = 1.27f;

        [Header("Отладка")]
        [SerializeField] private bool _showDebugUI = true;

        private const float FixedTimeStep = 0.05f; // 20 TPS тикрейт
        private float _tickAccumulator;
        private Vector3 _previousPosition;
        private float _currentEyeHeight;
        private PlayerInput _currentInput;

        public PlayerEntity Entity { get; private set; }
        public World World { get; private set; }

        private void Awake()
        {
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            _currentEyeHeight = _eyeHeightStanding;
        }

        private void Start()
        {
            LockCursor(true);

            // Если контроллер запущен без внешнего WorldManager, создаем автономный мир
            if (Entity == null)
            {
                var fallbackWorld = new World();
                fallbackWorld.GetOrCreateChunk(Vector2Int.zero);
                Initialize(fallbackWorld, new PlayerEntity(1, transform.position));
            }
        }

        /// <summary>
        /// Привязка контроллера к воксельному миру и экземпляру физической сущности PlayerEntity.
        /// </summary>
        public void Initialize(World world, PlayerEntity entity)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));

            _previousPosition = Entity.Position;
            transform.position = Entity.Position;
            transform.rotation = Quaternion.Euler(0f, Entity.Yaw, 0f);

            if (_playerCamera != null)
            {
                _playerCamera.transform.position = Entity.Position + Vector3.up * _currentEyeHeight;
                _playerCamera.transform.rotation = Quaternion.Euler(Entity.Pitch, Entity.Yaw, 0f);
            }
        }

        private void Update()
        {
            if (Entity == null || World == null)
            {
                return;
            }

            HandleCursorToggle();
            HandleMouseLook();
            _currentInput = ReadKeyboardInput();

            // Дискретная физическая симуляция (20 тиков в секунду, 50 мс)
            _tickAccumulator += Time.deltaTime;
            while (_tickAccumulator >= FixedTimeStep)
            {
                _previousPosition = Entity.Position;
                Entity.ApplyInput(_currentInput);
                Entity.Tick(World, FixedTimeStep);
                _tickAccumulator -= FixedTimeStep;
            }

            // Плавный рендеринг камеры с субтиковой интерполяцией (для 60/120+ FPS)
            UpdateCameraInterpolated();
        }

        private void HandleCursorToggle()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                LockCursor(false);
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 mouseDelta = mouse.delta.ReadValue();
            float yawDelta = mouseDelta.x * _mouseSensitivity;
            float pitchDelta = mouseDelta.y * _mouseSensitivity;

            float newYaw = (Entity.Yaw + yawDelta) % 360f;
            if (newYaw < 0f) newYaw += 360f;

            float newPitch = Mathf.Clamp(Entity.Pitch - pitchDelta, -89.5f, 89.5f);

            Entity.Yaw = newYaw;
            Entity.Pitch = newPitch;
        }

        private PlayerInput ReadKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return new PlayerInput(Vector2.zero, false, false, false, Entity.Yaw, Entity.Pitch);
            }

            float forward = 0f;
            if (keyboard.wKey.isPressed) forward += 1f;
            if (keyboard.sKey.isPressed) forward -= 1f;

            float strafe = 0f;
            if (keyboard.dKey.isPressed) strafe += 1f;
            if (keyboard.aKey.isPressed) strafe -= 1f;

            bool jump = keyboard.spaceKey.isPressed;
            bool sneak = keyboard.leftShiftKey.isPressed;
            bool sprint = keyboard.leftCtrlKey.isPressed;

            return new PlayerInput(
                movement: new Vector2(strafe, forward),
                jump: jump,
                sneak: sneak,
                sprint: sprint,
                yaw: Entity.Yaw,
                pitch: Entity.Pitch
            );
        }

        private void UpdateCameraInterpolated()
        {
            float alpha = Mathf.Clamp01(_tickAccumulator / FixedTimeStep);
            Vector3 smoothPos = Vector3.Lerp(_previousPosition, Entity.Position, alpha);

            transform.position = smoothPos;
            transform.rotation = Quaternion.Euler(0f, Entity.Yaw, 0f);

            if (_playerCamera != null)
            {
                float targetEyeHeight = Entity.IsSneaking ? _eyeHeightSneaking : _eyeHeightStanding;
                _currentEyeHeight = Mathf.Lerp(_currentEyeHeight, targetEyeHeight, Time.deltaTime * 15f);

                _playerCamera.transform.position = smoothPos + Vector3.up * _currentEyeHeight;
                _playerCamera.transform.rotation = Quaternion.Euler(Entity.Pitch, Entity.Yaw, 0f);
            }
        }

        private void OnGUI()
        {
            if (!_showDebugUI || Entity == null)
            {
                return;
            }

            // Прицел в центре экрана
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            GUI.Label(new Rect(centerX - 10, centerY - 12, 20, 20), "+");

            // Отладочная панель F3
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            float speedBps = new Vector2(Entity.Velocity.x, Entity.Velocity.z).magnitude * 20f;
            string status = $"XYZ: {Entity.Position.x:F2} / {Entity.Position.y:F2} / {Entity.Position.z:F2}\n" +
                            $"Facing: Yaw {Entity.Yaw:F1}° | Pitch {Entity.Pitch:F1}°\n" +
                            $"Speed: {speedBps:F1} blocks/s | VelY: {Entity.Velocity.y:F2}\n" +
                            $"OnGround: {Entity.OnGround} | Sneak: {Entity.IsSneaking} | Sprint: {Entity.IsSprinting}\n" +
                            $"StepHeight: {Entity.StepHeight:F1} | HorizCollide: {Entity.IsCollidedHorizontally}\n" +
                            $"[Esc] Разблокировать мышь | [ЛКМ] Заблокировать мышь";

            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.Box(new Rect(10, 10, 420, 140), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(15, 15, 410, 130), status, style);
        }
    }
}
