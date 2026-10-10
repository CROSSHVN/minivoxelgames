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
        private readonly List<AABB> _standTestBuffer = new List<AABB>(8);
        private bool _isSneaking;
        private bool _wantsToSneak;

        public override EntityType Type => EntityType.Player;

        /// <summary>
        /// Желание игрока находиться в приседе (зажата ли клавиша Shift).
        /// </summary>
        public bool WantsToSneak => _wantsToSneak;

        /// <summary>
        /// Режим крадущейся походки (Shift).
        /// В Minecraft 1.14+ уменьшает высоту AABB до 1.5 блоков и активирует защиту от падений с уступов (Safe Walk).
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
        /// Проверяет возможность выпрямиться (встать в полный рост 1.8 блока) без столкновения с блоками над головой.
        /// В Minecraft 1.14+ игрок не может встать из приседа, если пространство над головой заблокировано.
        /// </summary>
        public bool CanStandUp(World world)
        {
            if (world == null)
            {
                return true;
            }

            float halfWidth = PlayerWidth * 0.5f;
            AABB standingBox = new AABB(
                new Vector3(Position.x - halfWidth, Position.y, Position.z - halfWidth),
                new Vector3(Position.x + halfWidth, Position.y + StandingHeight, Position.z + halfWidth)
            );

            _standTestBuffer.Clear();
            world.GetCollidingBoxes(standingBox, _standTestBuffer);
            return _standTestBuffer.Count == 0;
        }

        /// <summary>
        /// Применяет пользовательский ввод клиента за текущий тик.
        /// </summary>
        public void ApplyInput(in PlayerInput input) => ApplyInput(input, null);

        /// <summary>
        /// Применяет пользовательский ввод клиента с учетом геометрии мира (для 1.14 проверки потолка при вставании).
        /// </summary>
        public void ApplyInput(in PlayerInput input, World world)
        {
            Yaw = input.Yaw;
            Pitch = input.Pitch;
            _wantsToSneak = input.Sneak;

            if (input.Sneak)
            {
                IsSneaking = true;
            }
            else
            {
                // В Minecraft 1.14+ игрок не может встать из приседа, если над головой есть блоки
                if (world == null || CanStandUp(world))
                {
                    IsSneaking = false;
                }
                else
                {
                    IsSneaking = true;
                }
            }

            // Бег (Sprint) возможен только при движении вперед и блокируется приседанием (Sneak), как в Minecraft Java
            IsSprinting = input.Sprint && !IsSneaking && input.Movement.y > 0f;
            MovementInput = input.Movement;

            if (input.Jump)
            {
                Jump();
            }
            else
            {
                ResetJumpTicks();
            }
        }

        /// <summary>
        /// Полный шаг тика симуляции игрока.
        /// </summary>
        public override void Tick(World world, float dt)
        {
            base.Tick(world, dt);

            // 1.14+: Если игрок отпустил клавишу Shift, но не мог встать из-за потолка,
            // и теперь вышел в свободное пространство — автоматически выпрямляемся
            if (!_wantsToSneak && IsSneaking && CanStandUp(world))
            {
                IsSneaking = false;
            }

            // Сброс спринта при столкновении со стеной по горизонтали (как в Minecraft EntityPlayerSP)
            if (IsSprinting && IsCollidedHorizontally)
            {
                IsSprinting = false;
            }

            // Выталкивание из твердых блоков (как в Minecraft EntityPlayerSP pushOutOfBlocks)
            PushOutOfBlocks(world);
        }

        /// <summary>
        /// Дополнительный горизонтальный импульс вперед при прыжке во время спринта (блоков/тик).
        /// В Minecraft 1.8.9 равен 0.2f (с учетом трения земли 0.546 в кадре прыжка результирующий импульс равен ~0.109).
        /// </summary>
        public float SprintJumpBoost { get; set; } = 0.2f;

        /// <summary>
        /// Совершает прыжок с учетом спринтерского горизонтального ускорения (SprintJumpBoost).
        /// Импульс вперед добавляется только при реальном беге вперед, прыжок на месте не дает смещения.
        /// </summary>
        public override void Jump()
        {
            if (OnGround && _jumpTicks == 0)
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
        /// Выталкивает игрока из непрозрачных блоков, если углы основания оказались внутри геометрии
        /// (по аналогии с pushOutOfBlocks в Minecraft EntityPlayerSP).
        /// </summary>
        public void PushOutOfBlocks(World world)
        {
            if (world == null)
            {
                return;
            }

            float offset = Width * 0.35f;
            float testY = BoundingBox.Min.y + 0.5f;

            PushOutOfBlockAt(world, Position.x - offset, testY, Position.z + offset);
            PushOutOfBlockAt(world, Position.x - offset, testY, Position.z - offset);
            PushOutOfBlockAt(world, Position.x + offset, testY, Position.z - offset);
            PushOutOfBlockAt(world, Position.x + offset, testY, Position.z + offset);
        }

        private void PushOutOfBlockAt(World world, float x, float y, float z)
        {
            int blockX = Mathf.FloorToInt(x);
            int blockY = Mathf.FloorToInt(y);
            int blockZ = Mathf.FloorToInt(z);

            if (world.GetVoxel(blockX, blockY, blockZ).IsSolid)
            {
                float dx = x - blockX;
                float dz = z - blockZ;

                int bestDir = -1;
                float minDist = 9999f;

                // West (-X)
                if (!world.GetVoxel(blockX - 1, blockY, blockZ).IsSolid && dx < minDist)
                {
                    minDist = dx;
                    bestDir = 0;
                }

                // East (+X)
                if (!world.GetVoxel(blockX + 1, blockY, blockZ).IsSolid && (1f - dx) < minDist)
                {
                    minDist = 1f - dx;
                    bestDir = 1;
                }

                // North (-Z)
                if (!world.GetVoxel(blockX, blockY, blockZ - 1).IsSolid && dz < minDist)
                {
                    minDist = dz;
                    bestDir = 4;
                }

                // South (+Z)
                if (!world.GetVoxel(blockX, blockY, blockZ + 1).IsSolid && (1f - dz) < minDist)
                {
                    minDist = 1f - dz;
                    bestDir = 5;
                }

                const float pushSpeed = 0.1f;
                Vector3 vel = Velocity;
                if (bestDir == 0) vel.x = -pushSpeed;
                if (bestDir == 1) vel.x = pushSpeed;
                if (bestDir == 4) vel.z = -pushSpeed;
                if (bestDir == 5) vel.z = pushSpeed;
                Velocity = vel;
            }
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
                world.GetCollidingBoxes(BoundingBox.Offset(new Vector3(offsetX, -1.0f, offsetZ)), _sneakGroundBuffer);
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
