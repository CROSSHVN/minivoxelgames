using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelCore.Physics;

namespace VoxelCore.Entities
{
    /// <summary>
    /// Сущность игрока.
    /// Реализует ввод пользователя (PlayerInput), режимы Sneak и Sprint,
    /// изменение высоты хитбокса при приседании (1.8 -> 1.5) и защиту от падения с обрыва (Safe Walk).
    /// </summary>
    public class PlayerEntity : LivingEntity
    {
        public const float PlayerWidth = 0.6f;
        public const float StandingHeight = 1.8f;
        public const float SneakingHeight = 1.5f;

        private readonly List<AABB> _sneakGroundBuffer = new List<AABB>(8);
        private bool _isSneaking;

        public override EntityType Type => EntityType.Player;

        /// <summary>
        /// Режим крадущейся походки (Shift).
        /// При включении уменьшает высоту AABB до 1.5 блоков и активирует защиту от падений с уступов.
        /// </summary>
        public bool IsSneaking
        {
            get => _isSneaking;
            set
            {
                if (_isSneaking != value)
                {
                    _isSneaking = value;
                    SetDimensions(PlayerWidth, value ? SneakingHeight : StandingHeight);
                }
            }
        }

        /// <summary>
        /// Режим быстрого бега (Sprint).
        /// </summary>
        public bool IsSprinting { get; set; }

        /// <summary>
        /// Множитель скорости при спринте (по умолчанию 1.3x).
        /// </summary>
        public float SprintMultiplier { get; set; } = 1.3f;

        /// <summary>
        /// Множитель скорости при крадущейся походке (по умолчанию 0.3x).
        /// </summary>
        public float SneakMultiplier { get; set; } = 0.3f;

        public PlayerEntity(uint id, Vector3 position)
            : base(id, position, PlayerWidth, StandingHeight)
        {
        }

        /// <summary>
        /// Применяет пользовательский ввод клиента за текущий тик.
        /// </summary>
        public void ApplyInput(in PlayerInput input)
        {
            Yaw = input.Yaw;
            Pitch = input.Pitch;
            IsSneaking = input.Sneak;
            // Бег (Sprint) возможен только при движении вперед и блокируется приседанием (Sneak), как в Minecraft Java
            IsSprinting = input.Sprint && !input.Sneak && input.Movement.y > 0f;
            MovementInput = input.Movement;

            if (input.Jump)
            {
                Jump();
            }
        }

        /// <summary>
        /// Дополнительный горизонтальный импульс вперед при прыжке во время спринта (блоков/тик).
        /// По умолчанию 0.05f (мягкий толчок вперед). Можно установить в 0.0f для чистого сохранения инерции.
        /// </summary>
        public float SprintJumpBoost { get; set; } = 0.05f;

        /// <summary>
        /// Совершает прыжок с учетом спринтерского горизонтального ускорения (SprintJumpBoost).
        /// Импульс вперед добавляется только при реальном беге вперед, прыжок на месте не дает смещения.
        /// </summary>
        public override void Jump()
        {
            if (OnGround)
            {
                base.Jump();

                if (IsSprinting && MovementInput.y > 0f && SprintJumpBoost > 0f)
                {
                    float rad = Yaw * Mathf.Deg2Rad;
                    Velocity += new Vector3(Mathf.Sin(rad) * SprintJumpBoost, 0f, Mathf.Cos(rad) * SprintJumpBoost);
                }
            }
        }

        protected override float GetCurrentSpeed()
        {
            float speed = BaseMovementSpeed;
            // Приседание имеет абсолютный приоритет над бегом
            if (IsSneaking)
            {
                speed *= SneakMultiplier;
            }
            else if (IsSprinting)
            {
                speed *= SprintMultiplier;
            }
            return speed;
        }

        /// <summary>
        /// Защита от падения с края блока в режиме крадущейся походки (Minecraft Safe Walk).
        /// </summary>
        protected override Vector3 PreProcessMotion(World world, Vector3 desiredMotion)
        {
            if (OnGround && IsSneaking)
            {
                return ApplySneakEdgeProtection(world, desiredMotion);
            }

            return desiredMotion;
        }

        private Vector3 ApplySneakEdgeProtection(World world, Vector3 motion)
        {
            float dx = motion.x;
            float dz = motion.z;
            const float step = 0.05f;

            static float StepDown(float val, float s) =>
                val > 0f ? Mathf.Max(0f, val - s) : Mathf.Min(0f, val + s);

            bool HasGround(float offsetX, float offsetZ)
            {
                _sneakGroundBuffer.Clear();
                world.GetCollidingBoxes(BoundingBox.Offset(new Vector3(offsetX, -StepHeight, offsetZ)), _sneakGroundBuffer);
                return _sneakGroundBuffer.Count > 0;
            }

            // 1. Проверка движения по оси X
            while (Mathf.Abs(dx) > AABB.Epsilon && !HasGround(dx, 0f))
            {
                dx = StepDown(dx, step);
            }

            // 2. Проверка движения по оси Z
            while (Mathf.Abs(dz) > AABB.Epsilon && !HasGround(0f, dz))
            {
                dz = StepDown(dz, step);
            }

            // 3. Проверка одновременного диагонального смещения
            while (Mathf.Abs(dx) > AABB.Epsilon && Mathf.Abs(dz) > AABB.Epsilon && !HasGround(dx, dz))
            {
                dx = StepDown(dx, step);
                dz = StepDown(dz, step);
            }

            return new Vector3(dx, motion.y, dz);
        }
    }
}
