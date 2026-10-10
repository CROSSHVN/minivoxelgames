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
            IsSprinting = input.Sprint;
            MovementInput = input.Movement;

            if (input.Jump)
            {
                Jump();
            }
        }

        protected override float GetCurrentSpeed()
        {
            float speed = BaseMovementSpeed;
            if (IsSprinting)
            {
                speed *= SprintMultiplier;
            }
            else if (IsSneaking)
            {
                speed *= SneakMultiplier;
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

            // 1. Проверка движения по оси X: если под ногами в новой точке пропасть, уменьшаем смещение
            while (Mathf.Abs(dx) > AABB.Epsilon)
            {
                AABB testBox = BoundingBox.Offset(new Vector3(dx, -StepHeight, 0f));
                _sneakGroundBuffer.Clear();
                world.GetCollidingBoxes(testBox, _sneakGroundBuffer);
                if (_sneakGroundBuffer.Count > 0)
                {
                    break;
                }

                if (dx > 0f)
                {
                    dx -= step;
                    if (dx < 0f) dx = 0f;
                }
                else
                {
                    dx += step;
                    if (dx > 0f) dx = 0f;
                }
            }

            // 2. Проверка движения по оси Z
            while (Mathf.Abs(dz) > AABB.Epsilon)
            {
                AABB testBox = BoundingBox.Offset(new Vector3(0f, -StepHeight, dz));
                _sneakGroundBuffer.Clear();
                world.GetCollidingBoxes(testBox, _sneakGroundBuffer);
                if (_sneakGroundBuffer.Count > 0)
                {
                    break;
                }

                if (dz > 0f)
                {
                    dz -= step;
                    if (dz < 0f) dz = 0f;
                }
                else
                {
                    dz += step;
                    if (dz > 0f) dz = 0f;
                }
            }

            // 3. Проверка одновременного диагонального смещения
            while (Mathf.Abs(dx) > AABB.Epsilon && Mathf.Abs(dz) > AABB.Epsilon)
            {
                AABB testBox = BoundingBox.Offset(new Vector3(dx, -StepHeight, dz));
                _sneakGroundBuffer.Clear();
                world.GetCollidingBoxes(testBox, _sneakGroundBuffer);
                if (_sneakGroundBuffer.Count > 0)
                {
                    break;
                }

                if (dx > 0f)
                {
                    dx -= step;
                    if (dx < 0f) dx = 0f;
                }
                else
                {
                    dx += step;
                    if (dx > 0f) dx = 0f;
                }

                if (dz > 0f)
                {
                    dz -= step;
                    if (dz < 0f) dz = 0f;
                }
                else
                {
                    dz += step;
                    if (dz > 0f) dz = 0f;
                }
            }

            return new Vector3(dx, motion.y, dz);
        }
    }
}
