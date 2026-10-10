using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelCore.Physics;

namespace VoxelCore.Entities
{
    /// <summary>
    /// Базовый POCO-класс сущности воксельного мира.
    /// Реализует тиковую физику перемещения, осевое разрешение коллизий (Minecraft swept AABB)
    /// и авто-подъем на ступени/полублоки (StepHeight) без единой аллокации памяти в куче (Zero GC).
    /// </summary>
    public abstract class Entity
    {
        private AABB _boundingBox;
        private readonly List<AABB> _collisionBuffer = new List<AABB>(32);

        public uint Id { get; set; }
        public abstract EntityType Type { get; }

        /// <summary>
        /// Позиция сущности в мировых координатах.
        /// Соответствует точке в центре основания ног:
        /// X = центр, Y = уровень подошвы (Min.y габаритного ящика), Z = центр.
        /// </summary>
        public Vector3 Position { get; protected set; }

        /// <summary>
        /// Вектор текущей скорости (в блоках за тик).
        /// </summary>
        public Vector3 Velocity { get; set; }

        /// <summary>
        /// Ширина основания сущности (размер по горизонтальным осям X и Z).
        /// Например, 0.6 блока для игрока.
        /// </summary>
        public float Width { get; protected set; }

        /// <summary>
        /// Высота сущности (размер по вертикальной оси Y).
        /// Например, 1.8 блока для игрока.
        /// </summary>
        public float Height { get; protected set; }

        /// <summary>
        /// Актуальный ограничивающий параллелепипед (AABB).
        /// </summary>
        public AABB BoundingBox => _boundingBox;

        /// <summary>
        /// Максимальная высота препятствия, на которое сущность может плавно зашагнуть
        /// без прыжка (по умолчанию 0; для прямоходящих сущностей, например, игрока — 0.6).
        /// </summary>
        public float StepHeight { get; set; } = 0.0f;

        /// <summary>
        /// Находится ли сущность на твердой поверхности (земле/полу).
        /// </summary>
        public bool OnGround { get; protected set; }

        /// <summary>
        /// Было ли столкновение по горизонтали (оси X или Z) на текущем шаге.
        /// </summary>
        public bool IsCollidedHorizontally { get; protected set; }

        /// <summary>
        /// Было ли столкновение по вертикали (пол или потолок) на текущем шаге.
        /// </summary>
        public bool IsCollidedVertically { get; protected set; }

        protected Entity(uint id, Vector3 position, float width, float height)
        {
            Id = id;
            Width = width;
            Height = height;
            SetPosition(position);
        }

        /// <summary>
        /// Телепортация или явное задание позиции сущности с обновлением AABB.
        /// </summary>
        public virtual void SetPosition(Vector3 position)
        {
            Position = position;
            _boundingBox = ComputeBoundingBox(position, Width, Height);
        }

        /// <summary>
        /// Изменение размеров сущности (например, при приседании).
        /// </summary>
        public virtual void SetDimensions(float width, float height)
        {
            Width = width;
            Height = height;
            _boundingBox = ComputeBoundingBox(Position, width, height);
        }

        /// <summary>
        /// Шаг симуляции жизненного цикла сущности за время <paramref name="dt"/> (в секундах).
        /// Реализуется в конкретных типах сущностей (гравитация, AI, инпуты, приземление).
        /// </summary>
        public abstract void Tick(World world, float dt);

        /// <summary>
        /// Предварительная обработка желаемого вектора движения перед выполнением коллизий.
        /// Переопределяется в наследниках (например, защита от падения с края блока при приседании).
        /// </summary>
        protected virtual Vector3 PreProcessMotion(World world, Vector3 desiredMotion)
        {
            return desiredMotion;
        }

        /// <summary>
        /// Перемещение сущности на вектор <paramref name="desiredMotion"/> с осевым разрешением
        /// столкновений и авто-подъемом на ступени (Minecraft Swept AABB + StepHeight).
        /// </summary>
        public void Move(World world, Vector3 desiredMotion)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            Vector3 originalDesired = desiredMotion;
            desiredMotion = PreProcessMotion(world, desiredMotion);

            AABB origBox = _boundingBox;
            bool wasOnGround = OnGround;

            // Broadphase: расширяем AABB вектором смещения и высотой шага для сбора всех блоков-препятствий
            AABB sweptBox = _boundingBox.Expand(desiredMotion);
            if (desiredMotion.y <= 0f && (wasOnGround || OnGround) && StepHeight > 0f)
            {
                sweptBox = sweptBox.Expand(new Vector3(0f, StepHeight, 0f));
            }

            world.GetCollidingBoxes(sweptBox, _collisionBuffer);

            // 1. Стандартное осевое разрешение (сначала Y, затем X, затем Z)
            float allowedDy = desiredMotion.y;
            for (int i = 0; i < _collisionBuffer.Count; i++)
            {
                allowedDy = _collisionBuffer[i].CalculateYOffset(_boundingBox, allowedDy);
            }
            _boundingBox = _boundingBox.Offset(new Vector3(0f, allowedDy, 0f));

            float allowedDx = desiredMotion.x;
            for (int i = 0; i < _collisionBuffer.Count; i++)
            {
                allowedDx = _collisionBuffer[i].CalculateXOffset(_boundingBox, allowedDx);
            }
            _boundingBox = _boundingBox.Offset(new Vector3(allowedDx, 0f, 0f));

            float allowedDz = desiredMotion.z;
            for (int i = 0; i < _collisionBuffer.Count; i++)
            {
                allowedDz = _collisionBuffer[i].CalculateZOffset(_boundingBox, allowedDz);
            }
            _boundingBox = _boundingBox.Offset(new Vector3(0f, 0f, allowedDz));

            bool collidedX = Mathf.Abs(desiredMotion.x - allowedDx) > AABB.Epsilon;
            bool collidedZ = Mathf.Abs(desiredMotion.z - allowedDz) > AABB.Epsilon;
            bool collidedHorizontally = collidedX || collidedZ;

            // 2. Симуляция авто-шага (Step-Height) при наличии горизонтального препятствия
            // Авто-шаг выполняется только при движении по горизонтали на земле (или падении),
            // и никогда не запускается во время активного прыжка вверх (desiredMotion.y > 0),
            // чтобы не гасить вертикальную скорость прыжка.
            bool canStep = desiredMotion.y <= 0f &&
                           (wasOnGround || Mathf.Abs(allowedDy - desiredMotion.y) > AABB.Epsilon) &&
                           collidedHorizontally &&
                           StepHeight > 0f;

            bool didStep = false;
            float stepUpY = 0f;
            float stepDownY = 0f;

            if (canStep)
            {
                AABB normalBox = _boundingBox;
                float normalDx = allowedDx;
                float normalDy = allowedDy;
                float normalDz = allowedDz;

                // А. Поднимаемся на StepHeight вверх от исходного ящика (с учетом потолка)
                _boundingBox = origBox;
                stepUpY = StepHeight;
                for (int i = 0; i < _collisionBuffer.Count; i++)
                {
                    stepUpY = _collisionBuffer[i].CalculateYOffset(_boundingBox, stepUpY);
                }
                _boundingBox = _boundingBox.Offset(new Vector3(0f, stepUpY, 0f));

                // Б. Двигаемся по горизонтали на приподнятой высоте
                float stepDx = desiredMotion.x;
                for (int i = 0; i < _collisionBuffer.Count; i++)
                {
                    stepDx = _collisionBuffer[i].CalculateXOffset(_boundingBox, stepDx);
                }
                _boundingBox = _boundingBox.Offset(new Vector3(stepDx, 0f, 0f));

                float stepDz = desiredMotion.z;
                for (int i = 0; i < _collisionBuffer.Count; i++)
                {
                    stepDz = _collisionBuffer[i].CalculateZOffset(_boundingBox, stepDz);
                }
                _boundingBox = _boundingBox.Offset(new Vector3(0f, 0f, stepDz));

                // В. Опускаемся обратно вниз до упора
                stepDownY = -stepUpY + (desiredMotion.y < 0f ? desiredMotion.y : 0f);
                for (int i = 0; i < _collisionBuffer.Count; i++)
                {
                    stepDownY = _collisionBuffer[i].CalculateYOffset(_boundingBox, stepDownY);
                }
                _boundingBox = _boundingBox.Offset(new Vector3(0f, stepDownY, 0f));

                // Г. Сравниваем пройденное горизонтальное расстояние
                float normalDistSq = normalDx * normalDx + normalDz * normalDz;
                float stepDistSq = stepDx * stepDx + stepDz * stepDz;

                if (normalDistSq >= stepDistSq)
                {
                    // Подъем не улучшил смещение — возвращаем плоское движение
                    _boundingBox = normalBox;
                    allowedDx = normalDx;
                    allowedDy = normalDy;
                    allowedDz = normalDz;
                }
                else
                {
                    // Подъем на ступень успешен!
                    didStep = true;
                    allowedDx = stepDx;
                    allowedDy = stepUpY + stepDownY;
                    allowedDz = stepDz;
                }
            }

            // 3. Обновление мировых координат из нового AABB
            Position = new Vector3(
                (_boundingBox.Min.x + _boundingBox.Max.x) * 0.5f,
                _boundingBox.Min.y,
                (_boundingBox.Min.z + _boundingBox.Max.z) * 0.5f
            );

            // 4. Обновление флагов
            bool hitGroundNormally = desiredMotion.y < 0f && Mathf.Abs(allowedDy - desiredMotion.y) > AABB.Epsilon;
            bool landedOnStep = didStep && (stepDownY > -stepUpY + AABB.Epsilon);

            bool stillOnGround = false;
            if (!hitGroundNormally && !landedOnStep && wasOnGround && Mathf.Abs(desiredMotion.y) <= AABB.Epsilon)
            {
                AABB groundTest = _boundingBox.Offset(new Vector3(0f, -0.05f, 0f));
                _collisionBuffer.Clear();
                world.GetCollidingBoxes(groundTest, _collisionBuffer);
                stillOnGround = _collisionBuffer.Count > 0;
            }

            OnGround = hitGroundNormally || landedOnStep || stillOnGround;
            IsCollidedHorizontally = Mathf.Abs(originalDesired.x - allowedDx) > AABB.Epsilon ||
                                     Mathf.Abs(originalDesired.z - allowedDz) > AABB.Epsilon;
            IsCollidedVertically = Mathf.Abs(originalDesired.y - allowedDy) > AABB.Epsilon;

            // 5. Обнуление скорости по осям, заблокированным коллизиями
            Vector3 newVel = Velocity;
            if (Mathf.Abs(originalDesired.x - allowedDx) > AABB.Epsilon) newVel.x = 0f;
            if (Mathf.Abs(originalDesired.z - allowedDz) > AABB.Epsilon) newVel.z = 0f;
            if (Mathf.Abs(originalDesired.y - allowedDy) > AABB.Epsilon) newVel.y = 0f;
            Velocity = newVel;
        }

        /// <summary>
        /// Создает компактный сетевой снимок текущего состояния сущности.
        /// </summary>
        public virtual EntityState CaptureState()
        {
            EntityFlags flags = EntityFlags.None;
            if (OnGround) flags |= EntityFlags.OnGround;
            if (IsCollidedHorizontally) flags |= EntityFlags.CollidedHorizontally;
            if (IsCollidedVertically) flags |= EntityFlags.CollidedVertically;

            return new EntityState(Id, Type, Position, Velocity, 0f, 0f, flags);
        }

        /// <summary>
        /// Восстанавливает состояние сущности из сетевого снимка (клиентское предсказание / реконсиляция).
        /// </summary>
        public virtual void ApplyState(in EntityState state)
        {
            SetPosition(state.Position);
            Velocity = state.Velocity;
            OnGround = state.OnGround;
            IsCollidedHorizontally = state.CollidedHorizontally;
            IsCollidedVertically = state.CollidedVertically;
        }

        private static AABB ComputeBoundingBox(Vector3 pos, float width, float height)
        {
            float halfWidth = width * 0.5f;
            return new AABB(
                new Vector3(pos.x - halfWidth, pos.y, pos.z - halfWidth),
                new Vector3(pos.x + halfWidth, pos.y + height, pos.z + halfWidth)
            );
        }
    }
}
