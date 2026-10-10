using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelCore
{
    /// <summary>
    /// Фасад воксельного мира.
    /// Управляет загруженными чанками, координирует поиск и предоставляет
    /// строгий доступ к чтению и установке вокселей по мировым координатам.
    /// </summary>
    public sealed class World
    {
        private readonly Dictionary<Vector2Int, Chunk> _chunks = new Dictionary<Vector2Int, Chunk>();

        /// <summary>
        /// Количество загруженных в данный момент чанков.
        /// </summary>
        public int LoadedChunksCount => _chunks.Count;

        /// <summary>
        /// Возвращает существующий чанк или создает новый, если его еще нет в мире.
        /// </summary>
        public Chunk GetOrCreateChunk(Vector2Int coord)
        {
            if (_chunks.TryGetValue(coord, out Chunk chunk))
            {
                return chunk;
            }

            chunk = new Chunk(coord);
            _chunks.Add(coord, chunk);
            return chunk;
        }

        /// <summary>
        /// Попытка получить чанк по координатам сетки чанков.
        /// </summary>
        public bool TryGetChunk(Vector2Int coord, out Chunk chunk)
        {
            return _chunks.TryGetValue(coord, out chunk);
        }

        #region Voxel Access

        /// <summary>
        /// Чтение вокселя по глобальным координатам мира (X, Y, Z).
        /// Если чанк не загружен или Y выходит за пределы [0..127], возвращает Voxel.Air.
        /// </summary>
        public Voxel GetVoxel(int worldX, int worldY, int worldZ)
        {
            // Проверка высоты мира
            if (worldY < 0 || worldY >= Chunk.Height)
            {
                return Voxel.Air;
            }

            Vector2Int chunkCoord = WorldToChunkCoord(worldX, worldZ);
            if (!_chunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                return Voxel.Air;
            }

            int localX = WorldToLocalCoord(worldX);
            int localZ = WorldToLocalCoord(worldZ);

            return chunk.GetVoxel(localX, worldY, localZ);
        }

        /// <summary>
        /// Перегрузка чтения вокселя по Vector3Int.
        /// </summary>
        public Voxel GetVoxel(Vector3Int worldPos)
        {
            return GetVoxel(worldPos.x, worldPos.y, worldPos.z);
        }

        /// <summary>
        /// Установка вокселя по глобальным координатам мира (X, Y, Z).
        /// Строгий режим: блок устанавливается ТОЛЬКО если чанк уже загружен и Y в пределах [0..127].
        /// Возвращает true при успехе, иначе false.
        /// </summary>
        public bool SetVoxel(int worldX, int worldY, int worldZ, Voxel voxel)
        {
            if (worldY < 0 || worldY >= Chunk.Height)
            {
                return false;
            }

            Vector2Int chunkCoord = WorldToChunkCoord(worldX, worldZ);
            if (!_chunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                return false;
            }

            int localX = WorldToLocalCoord(worldX);
            int localZ = WorldToLocalCoord(worldZ);

            chunk.SetVoxel(localX, worldY, localZ, voxel);
            return true;
        }

        /// <summary>
        /// Перегрузка установки вокселя по Vector3Int.
        /// </summary>
        public bool SetVoxel(Vector3Int worldPos, Voxel voxel)
        {
            return SetVoxel(worldPos.x, worldPos.y, worldPos.z, voxel);
        }

        #endregion

        #region Coordinate Transformations

        /// <summary>
        /// Преобразование мировых координат (X, Z) в координаты чанка в сетке.
        /// Корректно работает с отрицательными числами (например, -1 переходит в чанк -1).
        /// </summary>
        public static Vector2Int WorldToChunkCoord(int worldX, int worldZ)
        {
            return new Vector2Int(
                worldX >> SubChunk.Shift,
                worldZ >> SubChunk.Shift
            );
        }

        /// <summary>
        /// Перегрузка преобразования позиции Vector3Int в координаты чанка.
        /// </summary>
        public static Vector2Int WorldToChunkCoord(Vector3Int worldPos)
        {
            return WorldToChunkCoord(worldPos.x, worldPos.z);
        }

        /// <summary>
        /// Преобразование мировой горизонтальной координаты в локальную координату внутри чанка [0..15].
        /// Корректно работает с отрицательными числами (например, worldX = -1 -> localX = 15).
        /// </summary>
        public static int WorldToLocalCoord(int worldCoord)
        {
            return worldCoord & SubChunk.Mask;
        }

        /// <summary>
        /// Преобразование глобальной позиции Vector3Int в локальную позицию внутри чанка (localX, worldY, localZ).
        /// </summary>
        public static Vector3Int WorldToLocalPos(Vector3Int worldPos)
        {
            return new Vector3Int(
                WorldToLocalCoord(worldPos.x),
                worldPos.y,
                WorldToLocalCoord(worldPos.z)
            );
        }

        #endregion
    }
}
