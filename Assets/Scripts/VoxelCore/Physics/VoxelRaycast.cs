using System;
using UnityEngine;

namespace VoxelCore.Physics
{
    /// <summary>
    /// Высокопроизводительный воксельный рейкастинг по алгоритму DDA (Amanatides & Woo / Minecraft 1.8.9 rayTraceBlocks).
    /// Обходит строго воксели, пересекаемые лучом, гарантирует 0 GC аллокаций
    /// и мгновенно определяет затронутый блок, сторону удара (BlockFace) и позицию для установки нового блока.
    /// </summary>
    public static class VoxelRaycast
    {
        /// <summary>
        /// Трассирует луч в воксельном мире от точки origin в направлении direction на расстояние maxDistance.
        /// </summary>
        /// <param name="world">Воксельный мир.</param>
        /// <param name="origin">Точка старта луча (например, позиция глаз камеры).</param>
        /// <param name="direction">Вектор направления взгляда.</param>
        /// <param name="maxDistance">Максимальная дистанция луча в блоках (по умолчанию 4.5f, как в Minecraft).</param>
        /// <returns>Результат столкновения VoxelHitResult.</returns>
        public static VoxelHitResult Raycast(World world, Vector3 origin, Vector3 direction, float maxDistance = 4.5f)
        {
            if (world == null || maxDistance <= 0f)
            {
                return VoxelHitResult.Miss;
            }

            float dirLengthSq = direction.sqrMagnitude;
            if (dirLengthSq < 1e-8f)
            {
                return VoxelHitResult.Miss;
            }

            Vector3 dir = direction.normalized;

            // Начальная воксельная координата
            int currentX = Mathf.FloorToInt(origin.x);
            int currentY = Mathf.FloorToInt(origin.y);
            int currentZ = Mathf.FloorToInt(origin.z);

            // Проверка: находится ли начальная точка луча уже внутри твердого блока
            Voxel startVoxel = world.GetVoxel(currentX, currentY, currentZ);
            if (startVoxel.IsSolid)
            {
                Vector3Int pos = new Vector3Int(currentX, currentY, currentZ);
                return new VoxelHitResult(true, pos, pos, BlockFace.Up, origin, 0f, startVoxel);
            }

            // Направление шага по каждой оси (-1, 0, +1)
            int stepX = dir.x > 0f ? 1 : (dir.x < 0f ? -1 : 0);
            int stepY = dir.y > 0f ? 1 : (dir.y < 0f ? -1 : 0);
            int stepZ = dir.z > 0f ? 1 : (dir.z < 0f ? -1 : 0);

            // tDelta: длина пути вдоль луча для пересечения 1 блока по соответствующей оси
            float tDeltaX = stepX != 0 ? Mathf.Abs(1.0f / dir.x) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1.0f / dir.y) : float.PositiveInfinity;
            float tDeltaZ = stepZ != 0 ? Mathf.Abs(1.0f / dir.z) : float.PositiveInfinity;

            // tMax: расстояние вдоль луча от origin до первого пересечения границы воксельной сетки
            float tMaxX;
            if (stepX > 0)
            {
                tMaxX = ((currentX + 1) - origin.x) * tDeltaX;
            }
            else if (stepX < 0)
            {
                tMaxX = (origin.x - currentX) * tDeltaX;
            }
            else
            {
                tMaxX = float.PositiveInfinity;
            }

            float tMaxY;
            if (stepY > 0)
            {
                tMaxY = ((currentY + 1) - origin.y) * tDeltaY;
            }
            else if (stepY < 0)
            {
                tMaxY = (origin.y - currentY) * tDeltaY;
            }
            else
            {
                tMaxY = float.PositiveInfinity;
            }

            float tMaxZ;
            if (stepZ > 0)
            {
                tMaxZ = ((currentZ + 1) - origin.z) * tDeltaZ;
            }
            else if (stepZ < 0)
            {
                tMaxZ = (origin.z - currentZ) * tDeltaZ;
            }
            else
            {
                tMaxZ = float.PositiveInfinity;
            }

            // Ограничение максимального количества шагов (в 3D не более ~3 * maxDistance + 2)
            int maxSteps = Mathf.CeilToInt(maxDistance * 3.5f) + 4;
            int stepCount = 0;

            while (stepCount++ < maxSteps)
            {
                float hitDist;
                BlockFace hitFace;
                Vector3Int adjacentPos;

                // Выбираем ось с наименьшим расстоянием до следующей границы вокселя
                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        hitDist = tMaxX;
                        if (hitDist > maxDistance) break;

                        currentX += stepX;
                        hitFace = stepX > 0 ? BlockFace.West : BlockFace.East;
                        adjacentPos = new Vector3Int(currentX - stepX, currentY, currentZ);
                        tMaxX += tDeltaX;
                    }
                    else
                    {
                        hitDist = tMaxZ;
                        if (hitDist > maxDistance) break;

                        currentZ += stepZ;
                        hitFace = stepZ > 0 ? BlockFace.South : BlockFace.North;
                        adjacentPos = new Vector3Int(currentX, currentY, currentZ - stepZ);
                        tMaxZ += tDeltaZ;
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        hitDist = tMaxY;
                        if (hitDist > maxDistance) break;

                        currentY += stepY;
                        hitFace = stepY > 0 ? BlockFace.Down : BlockFace.Up;
                        adjacentPos = new Vector3Int(currentX, currentY - stepY, currentZ);
                        tMaxY += tDeltaY;
                    }
                    else
                    {
                        hitDist = tMaxZ;
                        if (hitDist > maxDistance) break;

                        currentZ += stepZ;
                        hitFace = stepZ > 0 ? BlockFace.South : BlockFace.North;
                        adjacentPos = new Vector3Int(currentX, currentY, currentZ - stepZ);
                        tMaxZ += tDeltaZ;
                    }
                }

                // Проверяем воксель в найденной ячейке сетки
                Voxel voxel = world.GetVoxel(currentX, currentY, currentZ);
                if (voxel.IsSolid)
                {
                    Vector3 hitPoint = origin + dir * hitDist;
                    return new VoxelHitResult(
                        true,
                        new Vector3Int(currentX, currentY, currentZ),
                        adjacentPos,
                        hitFace,
                        hitPoint,
                        hitDist,
                        voxel
                    );
                }
            }

            return VoxelHitResult.Miss;
        }
    }
}
