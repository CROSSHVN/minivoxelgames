using UnityEngine;

namespace VoxelCore.Physics
{
    /// <summary>
    /// Ограничивающий параллелепипед, выровненный по осям (Axis-Aligned Bounding Box).
    /// Базовая геометрическая структура воксельной физики для сущностей и блоков.
    /// Строго минималистичное ядро (YAGNI): только операции, необходимые для коллизий.
    /// </summary>
    public readonly struct AABB
    {
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public AABB(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public Vector3 Size => Max - Min;
        public Vector3 Center => (Min + Max) * 0.5f;

        /// <summary>
        /// Создает AABB единичного воксельного куба по целочисленным координатам блока.
        /// </summary>
        public static AABB FromBlock(int x, int y, int z)
        {
            return new AABB(new Vector3(x, y, z), new Vector3(x + 1, y + 1, z + 1));
        }

        /// <summary>
        /// Создает AABB по центральной точке и размерам (для сущностей, например, игрока 0.6 x 1.8).
        /// </summary>
        public static AABB FromCenterAndSize(Vector3 center, Vector3 size)
        {
            Vector3 half = size * 0.5f;
            return new AABB(center - half, center + half);
        }

        /// <summary>
        /// Смещение параллелепипеда на вектор движения.
        /// </summary>
        public AABB Offset(Vector3 offset)
        {
            return new AABB(Min + offset, Max + offset);
        }

        /// <summary>
        /// Расширение параллелепипеда вектором движения (Swept Box для сбора кандидатов-блоков).
        /// </summary>
        public AABB Expand(Vector3 delta)
        {
            float minX = delta.x < 0f ? Min.x + delta.x : Min.x;
            float maxX = delta.x > 0f ? Max.x + delta.x : Max.x;

            float minY = delta.y < 0f ? Min.y + delta.y : Min.y;
            float maxY = delta.y > 0f ? Max.y + delta.y : Max.y;

            float minZ = delta.z < 0f ? Min.z + delta.z : Min.z;
            float maxZ = delta.z > 0f ? Max.z + delta.z : Max.z;

            return new AABB(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }

        /// <summary>
        /// Проверка пересечения двух AABB.
        /// </summary>
        public bool Intersects(in AABB other)
        {
            return Max.x > other.Min.x && Min.x < other.Max.x &&
                   Max.y > other.Min.y && Min.y < other.Max.y &&
                   Max.z > other.Min.z && Min.z < other.Max.z;
        }

        #region Minecraft Axial Collision Resolution

        /// <summary>
        /// Вычисляет допустимое смещение entity вдоль оси X с учетом препятствия this.
        /// </summary>
        public float CalculateXOffset(in AABB entity, float offsetX)
        {
            if (entity.Max.y <= Min.y || entity.Min.y >= Max.y ||
                entity.Max.z <= Min.z || entity.Min.z >= Max.z)
            {
                return offsetX;
            }

            if (offsetX > 0f && entity.Max.x <= Min.x)
            {
                float maxAllowed = Min.x - entity.Max.x;
                if (maxAllowed < offsetX)
                {
                    offsetX = maxAllowed;
                }
            }
            else if (offsetX < 0f && entity.Min.x >= Max.x)
            {
                float maxAllowed = Max.x - entity.Min.x;
                if (maxAllowed > offsetX)
                {
                    offsetX = maxAllowed;
                }
            }

            return offsetX;
        }

        /// <summary>
        /// Вычисляет допустимое смещение entity вдоль оси Y с учетом препятствия this.
        /// </summary>
        public float CalculateYOffset(in AABB entity, float offsetY)
        {
            if (entity.Max.x <= Min.x || entity.Min.x >= Max.x ||
                entity.Max.z <= Min.z || entity.Min.z >= Max.z)
            {
                return offsetY;
            }

            if (offsetY > 0f && entity.Max.y <= Min.y)
            {
                float maxAllowed = Min.y - entity.Max.y;
                if (maxAllowed < offsetY)
                {
                    offsetY = maxAllowed;
                }
            }
            else if (offsetY < 0f && entity.Min.y >= Max.y)
            {
                float maxAllowed = Max.y - entity.Min.y;
                if (maxAllowed > offsetY)
                {
                    offsetY = maxAllowed;
                }
            }

            return offsetY;
        }

        /// <summary>
        /// Вычисляет допустимое смещение entity вдоль оси Z с учетом препятствия this.
        /// </summary>
        public float CalculateZOffset(in AABB entity, float offsetZ)
        {
            if (entity.Max.x <= Min.x || entity.Min.x >= Max.x ||
                entity.Max.y <= Min.y || entity.Min.y >= Max.y)
            {
                return offsetZ;
            }

            if (offsetZ > 0f && entity.Max.z <= Min.z)
            {
                float maxAllowed = Min.z - entity.Max.z;
                if (maxAllowed < offsetZ)
                {
                    offsetZ = maxAllowed;
                }
            }
            else if (offsetZ < 0f && entity.Min.z >= Max.z)
            {
                float maxAllowed = Max.z - entity.Min.z;
                if (maxAllowed > offsetZ)
                {
                    offsetZ = maxAllowed;
                }
            }

            return offsetZ;
        }

        #endregion

        public override string ToString()
        {
            return $"AABB[Min={Min:F2}, Max={Max:F2}]";
        }
    }
}
