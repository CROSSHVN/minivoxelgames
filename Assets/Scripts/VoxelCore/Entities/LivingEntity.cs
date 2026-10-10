using System;
using UnityEngine;
using VoxelCore.Physics;

namespace VoxelCore.Entities
{
    /// <summary>
    /// Базовый POCO-класс для одушевленных сущностей (Игрок, Мобы, Животные).
    /// Добавляет углы взгляда (Yaw/Pitch), ускорение по направлению взгляда (MoveRelative),
    /// прыжок (Jump), гравитацию (0.08) и трение с учетом поверхности блоков.
    /// </summary>
    public abstract class LivingEntity : Entity
    {
        /// <summary>
        /// Поворот тела вокруг вертикальной оси Y (в градусах).
        /// 0 = направление вперед по оси +Z, 90 = вправо по оси +X.
        /// </summary>
        public float Yaw { get; set; }

        /// <summary>
        /// Наклон головы/взгляда по вертикали (в градусах).
        /// Отрицательные углы — взгляд вверх, положительные — вниз.
        /// </summary>
        public float Pitch { get; set; }

        /// <summary>
        /// Начальный импульс вертикальной скорости при прыжке (в блоках за тик).
        /// 0.42 в Minecraft позволяет преодолеть высоту в 1.25 блока.
        /// </summary>
        public float JumpVelocity { get; set; } = 0.42f;

        /// <summary>
        /// Гравитация живой сущности в блоках за тик^2 (0.08 в Minecraft).
        /// </summary>
        public float Gravity { get; set; } = 0.08f;

        /// <summary>
        /// Сопротивление воздуха по горизонтали (0.91 в Minecraft).
        /// </summary>
        public float AirDrag { get; set; } = 0.91f;

        /// <summary>
        /// Сопротивление воздуха по вертикали (0.98 в Minecraft, термальная скорость ~3.92 б/т).
        /// </summary>
        public float VerticalDrag { get; set; } = 0.98f;

        /// <summary>
        /// Базовый коэффициент скольжения блоков (0.6 для большинства твердых блоков, 0.98 для льда).
        /// </summary>
        public float DefaultGroundFriction { get; set; } = 0.6f;

        /// <summary>
        /// Базовая скорость ускорения при ходьбе за 1 тик.
        /// </summary>
        public float BaseMovementSpeed { get; set; } = 0.1f;

        /// <summary>
        /// Вектор управления движением:
        /// X = стрейф (влево -1, вправо +1), Y = вперед (+1, назад -1).
        /// </summary>
        public Vector2 MovementInput { get; set; }

        protected LivingEntity(uint id, Vector3 position, float width, float height)
            : base(id, position, width, height)
        {
            // Живые сущности умеют преодолевать ступени и плиты до 0.6 блока без прыжка
            StepHeight = 0.6f;
        }

        protected int _jumpTicks;

        /// <summary>
        /// Счетчик тиков задержки между прыжками (в Minecraft 1.8.9 jumpTicks = 10, т.е. 0.5 сек).
        /// Предотвращает спам-прыжки при удержании пробела на ступенях и уступах.
        /// </summary>
        public int JumpTicks => _jumpTicks;

        /// <summary>
        /// Сбрасывает задержку прыжка (вызывается при отпускании клавиши прыжка).
        /// </summary>
        public void ResetJumpTicks()
        {
            _jumpTicks = 0;
        }

        /// <summary>
        /// Совершает прыжок, если сущность стоит на твердой поверхности и истекла задержка jumpTicks.
        /// </summary>
        public virtual void Jump()
        {
            if (OnGround && _jumpTicks == 0)
            {
                Velocity = new Vector3(Velocity.x, JumpVelocity, Velocity.z);
                _jumpTicks = 10;
            }
        }

        /// <summary>
        /// Добавляет ускорение к текущей скорости относительно ориентации взгляда (Yaw).
        /// </summary>
        public void MoveRelative(Vector2 input, float speed)
        {
            float lenSq = input.x * input.x + input.y * input.y;
            if (lenSq < 1e-4f)
            {
                return;
            }

            // Защита от ускорения при движении по диагонали (W+D)
            if (lenSq > 1.0f)
            {
                float invLen = 1.0f / Mathf.Sqrt(lenSq);
                input.x *= invLen;
                input.y *= invLen;
            }

            float strafe = input.x * speed;
            float forward = input.y * speed;

            float rad = Yaw * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);

            // В Unity: Yaw = 0 -> +Z; Yaw = 90 -> +X
            float deltaX = strafe * cos + forward * sin;
            float deltaZ = -strafe * sin + forward * cos;

            Velocity = new Vector3(Velocity.x + deltaX, Velocity.y, Velocity.z + deltaZ);
        }

        /// <summary>
        /// Возвращает текущую скорость движения с учетом модификаторов (бег, присед).
        /// </summary>
        protected virtual float GetCurrentSpeed()
        {
            return BaseMovementSpeed;
        }

        /// <summary>
        /// Определяет коэффициент скольжения поверхности под ногами сущности.
        /// По умолчанию возвращает базовое трение (0.6). При добавлении льда/слизи будет расширено.
        /// </summary>
        protected virtual float GetGroundSlipperiness(World world)
        {
            return DefaultGroundFriction;
        }

        /// <summary>
        /// Полный шаг физической симуляции живой сущности (ускорение, перемещение, затухание).
        /// Рассчитывается с фиксированным дискретным шагом Minecraft (20 TPS) по формулам EntityLivingBase 1.8.9.
        /// </summary>
        public override void Tick(World world, float dt)
        {
            if (_jumpTicks > 0)
            {
                _jumpTicks--;
            }

            // 1. Определение коэффициента трения поверхности ДО перемещения (Minecraft 1.8.9 f5 / f6)
            float slipperiness = GetGroundSlipperiness(world);
            float friction = OnGround ? slipperiness * AirDrag : AirDrag;
            float frictionCompensation = 0.16277136f / (friction * friction * friction);

            // 2. Ускорение от вектора ввода (на земле с компенсацией трения, в воздухе фиксированный jumpMovementFactor 0.02)
            float speed = GetCurrentSpeed();
            float acceleration = OnGround ? speed * frictionCompensation : speed * 0.2f;
            MoveRelative(MovementInput, acceleration);

            // Фиксация горизонтального трения кадра ДО перемещения (как в Minecraft 1.8.9):
            // в кадре отрыва от земли (прыжка) к импульсу применяется трение земли (~0.546), а не воздуха (0.91)
            float dragXZ = OnGround ? slipperiness * AirDrag : AirDrag;

            // 3. Перемещение с осевым разрешением коллизий (выполняется ДО гравитации, как в Minecraft Java)
            Move(world, Velocity);

            // 4. Гравитация (применяется после перемещения, обеспечивая прыжок 1.25 блока)
            Velocity = new Vector3(Velocity.x, Velocity.y - Gravity, Velocity.z);

            // 5. Трение и сопротивление среды
            Velocity = new Vector3(Velocity.x * dragXZ, Velocity.y * VerticalDrag, Velocity.z * dragXZ);
        }

        public override EntityState CaptureState()
        {
            EntityFlags flags = EntityFlags.None;
            if (OnGround) flags |= EntityFlags.OnGround;
            if (IsCollidedHorizontally) flags |= EntityFlags.CollidedHorizontally;
            if (IsCollidedVertically) flags |= EntityFlags.CollidedVertically;

            return new EntityState(Id, Type, Position, Velocity, Yaw, Pitch, flags);
        }

        public override void ApplyState(in EntityState state)
        {
            base.ApplyState(state);
            Yaw = state.Yaw;
            Pitch = state.Pitch;
        }
    }
}
