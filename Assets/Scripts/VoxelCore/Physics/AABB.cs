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
        /// <summary>
        /// Минимальный порог допуска (Epsilon) для вычислений с плавающей точкой (float).
        /// Исключает артефакты застревания на стыках смежных вокселей и ложные блокировки.
        /// 1e-4f = 0.1 миллиметра в масштабе вокселей (1 блок = 1 метр).
        /// </summary>
        public const float Epsilon = 1e-4f;

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
        /// Проверка пересечения двух AABB с учетом Epsilon.
        /// </summary>
        public bool Intersects(in AABB other)
        {
            return Max.x > other.Min.x + Epsilon && Min.x < other.Max.x - Epsilon &&
                   Max.y > other.Min.y + Epsilon && Min.y < other.Max.y - Epsilon &&
                   Max.z > other.Min.z + Epsilon && Min.z < other.Max.z - Epsilon;
        }

        #region Minecraft Axial Collision Resolution

        /// <summary>
        /// Вычисляет допустимое смещение entity вдоль оси X с учетом препятствия this.
        /// </summary>
        public float CalculateXOffset(in AABB entity, float offsetX)
        {
            if (entity.Max.y <= Min.y + Epsilon || entity.Min.y >= Max.y - Epsilon ||
                entity.Max.z <= Min.z + Epsilon || entity.Min.z >= Max.z - Epsilon)
            {
                return offsetX;
            }

            if (offsetX > 0f && entity.Max.x <= Min.x + Epsilon)
            {
                float maxAllowed = Min.x - entity.Max.x;
                if (maxAllowed < 0f && maxAllowed >= -Epsilon)
                {
                    maxAllowed = 0f;
                }

                if (maxAllowed < offsetX)
                {
                    offsetX = maxAllowed;
                }
            }
            else if (offsetX < 0f && entity.Min.x >= Max.x - Epsilon)
            {
                float maxAllowed = Max.x - entity.Min.x;
                if (maxAllowed > 0f && maxAllowed <= Epsilon)
                {
                    maxAllowed = 0f;
                }

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
            if (entity.Max.x <= Min.x + Epsilon || entity.Min.x >= Max.x - Epsilon ||
                entity.Max.z <= Min.z + Epsilon || entity.Min.z >= Max.z - Epsilon)
            {
                return offsetY;
            }

            if (offsetY > 0f && entity.Max.y <= Min.y + Epsilon)
            {
                float maxAllowed = Min.y - entity.Max.y;
                if (maxAllowed < 0f && maxAllowed >= -Epsilon)
                {
                    maxAllowed = 0f;
                }

                if (maxAllowed < offsetY)
                {
                    offsetY = maxAllowed;
                }
            }
            else if (offsetY < 0f && entity.Min.y >= Max.y - Epsilon)
            {
                float maxAllowed = Max.y - entity.Min.y;
                if (maxAllowed > 0f && maxAllowed <= Epsilon)
                {
                    maxAllowed = 0f;
                }

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
            if (entity.Max.x <= Min.x + Epsilon || entity.Min.x >= Max.x - Epsilon ||
                entity.Max.y <= Min.y + Epsilon || entity.Min.y >= Max.y - Epsilon)
            {
                return offsetZ;
            }

            if (offsetZ > 0f && entity.Max.z <= Min.z + Epsilon)
            {
                float maxAllowed = Min.z - entity.Max.z;
                if (maxAllowed < 0f && maxAllowed >= -Epsilon)
                {
                    maxAllowed = 0f;
                }

                if (maxAllowed < offsetZ)
                {
                    offsetZ = maxAllowed;
                }
            }
            else if (offsetZ < 0f && entity.Min.z >= Max.z - Epsilon)
            {
                float maxAllowed = Max.z - entity.Min.z;
                if (maxAllowed > 0f && maxAllowed <= Epsilon)
                {
                    maxAllowed = 0f;
                }

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
