using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelCore.Physics;

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

        // L1-кэш последнего запрошенного чанка (Minecraft 1.8.9 pattern).
        // Обеспечивает O(1) доступ без обращения к хэш-таблице для локализованных запросов (физика, рейкаст, генерация).
        private int _cachedChunkX = int.MinValue;
        private int _cachedChunkZ = int.MinValue;
        private Chunk _cachedChunk;

        /// <summary>
        /// Количество загруженных в данный момент чанков.
        /// </summary>
        public int LoadedChunksCount => _chunks.Count;

        /// <summary>
        /// Сбрасывает L1-кэш чанка.
        /// </summary>
        public void InvalidateChunkCache()
        {
            _cachedChunkX = int.MinValue;
            _cachedChunkZ = int.MinValue;
            _cachedChunk = null;
        }

        /// <summary>
        /// Возвращает существующий чанк или создает новый, если его еще нет в мире.
        /// </summary>
        public Chunk GetOrCreateChunk(Vector2Int coord)
        {
            if (TryGetChunk(coord.x, coord.y, out Chunk chunk))
            {
                return chunk;
            }

            chunk = new Chunk(coord);
            _chunks.Add(coord, chunk);
            _cachedChunkX = coord.x;
            _cachedChunkZ = coord.y;
            _cachedChunk = chunk;
            return chunk;
        }

        /// <summary>
        /// Попытка получить чанк по координатам сетки чанков Vector2Int.
        /// Использует L1-кэш для быстрого доступа.
        /// </summary>
        public bool TryGetChunk(Vector2Int coord, out Chunk chunk)
        {
            return TryGetChunk(coord.x, coord.y, out chunk);
        }

        /// <summary>
        /// Попытка получить чанк по целочисленным координатам сетки (chunkX, chunkZ).
        /// Исключает создание структуры Vector2Int и использует быстрый L1-кэш.
        /// </summary>
        public bool TryGetChunk(int chunkX, int chunkZ, out Chunk chunk)
        {
            if (chunkX == _cachedChunkX && chunkZ == _cachedChunkZ && _cachedChunk != null)
            {
                chunk = _cachedChunk;
                return true;
            }

            Vector2Int coord = new Vector2Int(chunkX, chunkZ);
            if (_chunks.TryGetValue(coord, out chunk))
            {
                _cachedChunkX = chunkX;
                _cachedChunkZ = chunkZ;
                _cachedChunk = chunk;
                return true;
            }

            chunk = null;
            return false;
        }

        #region Voxel Access

        /// <summary>
        /// Чтение вокселя по глобальным координатам мира (X, Y, Z).
        /// Если чанк не загружен или Y выходит за пределы [0..127], возвращает Voxel.Air.
        /// Использует быстрый L1-кэш чанка (Minecraft 1.8.9).
        /// </summary>
        public Voxel GetVoxel(int worldX, int worldY, int worldZ)
        {
            // Проверка высоты мира
            if (worldY < 0 || worldY >= Chunk.Height)
            {
                return Voxel.Air;
            }

            int chunkX = worldX >> SubChunk.Shift;
            int chunkZ = worldZ >> SubChunk.Shift;

            if (!TryGetChunk(chunkX, chunkZ, out Chunk chunk))
            {
                return Voxel.Air;
            }

            int localX = worldX & SubChunk.Mask;
            int localZ = worldZ & SubChunk.Mask;

            return chunk.GetVoxel(localX, worldY, localZ);
        }

        /// <summary>
        /// Прямое чтение вокселя через Dictionary без использования L1-кэша.
        /// Используется для эталонных сравнительных замеров производительности (A/B бенчмарки).
        /// </summary>
        public Voxel GetVoxelLegacyDirect(int worldX, int worldY, int worldZ)
        {
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

            int chunkX = worldX >> SubChunk.Shift;
            int chunkZ = worldZ >> SubChunk.Shift;

            if (!TryGetChunk(chunkX, chunkZ, out Chunk chunk))
            {
                return false;
            }

            int localX = worldX & SubChunk.Mask;
            int localZ = worldZ & SubChunk.Mask;

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

        #region Physics Collision Queries

        /// <summary>
        /// Трассирует воксельный луч по алгоритму DDA (Amanatides-Woo / Minecraft 1.8.9).
        /// Находит первый твердый блок, сторону удара (BlockFace) и позицию для установки смежного блока.
        /// Не производит аллокаций памяти в куче (Zero GC).
        /// </summary>
        public VoxelHitResult Raycast(Vector3 origin, Vector3 direction, float maxDistance = 4.5f)
        {
            return VoxelRaycast.Raycast(this, origin, direction, maxDistance);
        }

        /// <summary>
        /// Находит все твердые воксели (AABB блоков), пересекающие заданную область.
        /// Очищает переданный список <paramref name="results"/> и заполняет его найденными AABB блоков.
        /// Не производит аллокаций памяти в куче (Zero GC).
        /// Использует кэширование активного чанка для минимизации поисков по словарю.
        /// Учитывает AABB.Epsilon для надежной работы с типом float на границах воксельной сетки.
        /// </summary>
        public void GetCollidingBoxes(in AABB box, List<AABB> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            int rawMinY = Mathf.FloorToInt(box.Min.y + AABB.Epsilon);
            int rawMaxY = Mathf.FloorToInt(box.Max.y - AABB.Epsilon);

            // Если область целиком выше или ниже допустимой высоты мира
            if (rawMaxY < 0 || rawMinY >= Chunk.Height)
            {
                return;
            }

            int minY = Math.Max(0, rawMinY);
            int maxY = Math.Min(Chunk.Height - 1, rawMaxY);

            int minX = Mathf.FloorToInt(box.Min.x + AABB.Epsilon);
            int maxX = Mathf.FloorToInt(box.Max.x - AABB.Epsilon);
            int minZ = Mathf.FloorToInt(box.Min.z + AABB.Epsilon);
            int maxZ = Mathf.FloorToInt(box.Max.z - AABB.Epsilon);

            if (minX > maxX || minZ > maxZ || minY > maxY)
            {
                return;
            }

            int lastChunkX = int.MinValue;
            int lastChunkZ = int.MinValue;
            Chunk cachedChunk = null;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int chunkX = x >> SubChunk.Shift;
                    int localX = x & SubChunk.Mask;

                    for (int z = minZ; z <= maxZ; z++)
                    {
                        int chunkZ = z >> SubChunk.Shift;

                        if (chunkX != lastChunkX || chunkZ != lastChunkZ)
                        {
                            lastChunkX = chunkX;
                            lastChunkZ = chunkZ;
                            TryGetChunk(chunkX, chunkZ, out cachedChunk);
                        }

                        if (cachedChunk != null)
                        {
                            int localZ = z & SubChunk.Mask;
                            Voxel voxel = cachedChunk.GetVoxel(localX, y, localZ);
                            if (voxel.IsSolid)
                            {
                                results.Add(AABB.FromBlock(x, y, z));
                            }
                        }
                    }
                }
            }
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
