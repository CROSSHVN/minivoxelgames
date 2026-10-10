using System.Runtime.CompilerServices;
using UnityEngine;

namespace VoxelCore
{
    /// <summary>
    /// Кэш 6 прямых соседей подчанка (фон Неймановское соседство).
    /// Устраняет повторные обращения к World и Dictionary при обработке границ подчанка.
    /// </summary>
    public readonly struct SubChunkNeighbors
    {
        public readonly SubChunk Center;
        public readonly SubChunk Up;    // +Y (subChunkIndex + 1 в том же чанке)
        public readonly SubChunk Down;  // -Y (subChunkIndex - 1 в том же чанке)
        public readonly SubChunk North; // +Z (соседний чанк (cx, cz + 1))
        public readonly SubChunk South; // -Z (соседний чанк (cx, cz - 1))
        public readonly SubChunk East;  // +X (соседний чанк (cx + 1, cz))
        public readonly SubChunk West;  // -X (соседний чанк (cx - 1, cz))

        public SubChunkNeighbors(
            SubChunk center,
            SubChunk up,
            SubChunk down,
            SubChunk north,
            SubChunk south,
            SubChunk east,
            SubChunk west)
        {
            Center = center;
            Up = up;
            Down = down;
            North = north;
            South = south;
            East = east;
            West = west;
        }

        /// <summary>
        /// Быстрое чтение вокселя грани Up (+Y) с мгновенным определением воздуха, если подчанк пуст (null).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Voxel GetUp(int x, int z) => Up != null ? Up.GetVoxel(x, 0, z) : Voxel.Air;

        /// <summary>
        /// Быстрое чтение вокселя грани Down (-Y) с мгновенным определением воздуха, если подчанк пуст (null).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Voxel GetDown(int x, int z) => Down != null ? Down.GetVoxel(x, 15, z) : Voxel.Air;

        /// <summary>
        /// Быстрое чтение вокселя грани North (+Z) с мгновенным определением воздуха, если подчанк пуст (null).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Voxel GetNorth(int x, int y) => North != null ? North.GetVoxel(x, y, 0) : Voxel.Air;

        /// <summary>
        /// Быстрое чтение вокселя грани South (-Z) с мгновенным определением воздуха, если подчанк пуст (null).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Voxel GetSouth(int x, int y) => South != null ? South.GetVoxel(x, y, 15) : Voxel.Air;

        /// <summary>
        /// Быстрое чтение вокселя грани East (+X) с мгновенным определением воздуха, если подчанк пуст (null).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Voxel GetEast(int y, int z) => East != null ? East.GetVoxel(0, y, z) : Voxel.Air;

        /// <summary>
        /// Быстрое чтение вокселя грани West (-X) с мгновенным определением воздуха, если подчанк пуст (null).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Voxel GetWest(int y, int z) => West != null ? West.GetVoxel(15, y, z) : Voxel.Air;

        /// <summary>
        /// Фабричный метод создания кэша соседей для указанного подчанка из мира.
        /// Выполняет поиск по словарю мира только для 4 соседних чанков.
        /// </summary>
        public static SubChunkNeighbors Create(World world, Vector2Int chunkCoord, int subChunkIndex)
        {
            if (world == null || !world.TryGetChunk(chunkCoord, out Chunk centerChunk))
            {
                return default;
            }

            return Create(world, centerChunk, subChunkIndex);
        }

        /// <summary>
        /// Создание кэша, когда центральный чанк уже известен.
        /// </summary>
        public static SubChunkNeighbors Create(World world, Chunk centerChunk, int subChunkIndex)
        {
            if (world == null || centerChunk == null || subChunkIndex < 0 || subChunkIndex >= Chunk.SubChunksCount)
            {
                return default;
            }

            SubChunk center = centerChunk.GetSubChunk(subChunkIndex);
            if (center == null)
            {
                return default;
            }

            SubChunk up = (subChunkIndex < Chunk.SubChunksCount - 1) ? centerChunk.GetSubChunk(subChunkIndex + 1) : null;
            SubChunk down = (subChunkIndex > 0) ? centerChunk.GetSubChunk(subChunkIndex - 1) : null;

            Vector2Int pos = centerChunk.Position;
            SubChunk north = world.TryGetChunk(new Vector2Int(pos.x, pos.y + 1), out Chunk nc) ? nc.GetSubChunk(subChunkIndex) : null;
            SubChunk south = world.TryGetChunk(new Vector2Int(pos.x, pos.y - 1), out Chunk sc) ? sc.GetSubChunk(subChunkIndex) : null;
            SubChunk east  = world.TryGetChunk(new Vector2Int(pos.x + 1, pos.y), out Chunk ec) ? ec.GetSubChunk(subChunkIndex) : null;
            SubChunk west  = world.TryGetChunk(new Vector2Int(pos.x - 1, pos.y), out Chunk wc) ? wc.GetSubChunk(subChunkIndex) : null;

            return new SubChunkNeighbors(center, up, down, north, south, east, west);
        }
    }
}
