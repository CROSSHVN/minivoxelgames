using UnityEngine;

namespace VoxelCore.Physics
{
    /// <summary>
    /// Неизменяемый результат трассировки воксельного луча (DDA Raycast).
    /// Value Type на стеке, 0 GC аллокаций.
    /// </summary>
    public readonly struct VoxelHitResult
    {
        /// <summary>
        /// Было ли попадание в твердый блок.
        /// </summary>
        public readonly bool HasHit;

        /// <summary>
        /// Целочисленные координаты блока в мире, в который врезался луч (для разрушения блока).
        /// </summary>
        public readonly Vector3Int BlockPos;

        /// <summary>
        /// Координаты соседнего свободного блока со стороны пораженной грани (для установки нового блока).
        /// </summary>
        public readonly Vector3Int AdjacentPos;

        /// <summary>
        /// Грань блока, через которую луч вошел в препятствие.
        /// </summary>
        public readonly BlockFace Face;

        /// <summary>
        /// Точная точка пересечения луча с гранью блока в мировых координатах.
        /// </summary>
        public readonly Vector3 HitPoint;

        /// <summary>
        /// Расстояние от точки старта луча до точки столкновения.
        /// </summary>
        public readonly float Distance;

        /// <summary>
        /// Воксель, в который попал луч.
        /// </summary>
        public readonly Voxel Voxel;

        public VoxelHitResult(
            bool hasHit,
            Vector3Int blockPos,
            Vector3Int adjacentPos,
            BlockFace face,
            Vector3 hitPoint,
            float distance,
            Voxel voxel)
        {
            HasHit = hasHit;
            BlockPos = blockPos;
            AdjacentPos = adjacentPos;
            Face = face;
            HitPoint = hitPoint;
            Distance = distance;
            Voxel = voxel;
        }

        public static VoxelHitResult Miss => default;
    }
}
