using UnityEngine;

namespace VoxelCore
{
    /// <summary>
    /// Кэш 4 горизонтальных соседних чанков (North, South, East, West).
    /// Позволяет мгновенно получать SubChunkNeighbors для всех 8 подчанков
    /// колонны без повторных поисков по словарю мира (всего 4 TryGetChunk на весь чанк).
    /// </summary>
    public readonly struct ChunkNeighbors
    {
        public readonly Chunk Center;
        public readonly Chunk North;
        public readonly Chunk South;
        public readonly Chunk East;
        public readonly Chunk West;

        public ChunkNeighbors(World world, Vector2Int chunkCoord)
        {
            if (world == null)
            {
                Center = null;
                North = null;
                South = null;
                East = null;
                West = null;
                return;
            }

            world.TryGetChunk(chunkCoord, out Center);
            world.TryGetChunk(new Vector2Int(chunkCoord.x, chunkCoord.y + 1), out North);
            world.TryGetChunk(new Vector2Int(chunkCoord.x, chunkCoord.y - 1), out South);
            world.TryGetChunk(new Vector2Int(chunkCoord.x + 1, chunkCoord.y), out East);
            world.TryGetChunk(new Vector2Int(chunkCoord.x - 1, chunkCoord.y), out West);
        }

        public ChunkNeighbors(World world, Chunk centerChunk)
        {
            Center = centerChunk;
            if (world != null && centerChunk != null)
            {
                Vector2Int pos = centerChunk.Position;
                world.TryGetChunk(new Vector2Int(pos.x, pos.y + 1), out North);
                world.TryGetChunk(new Vector2Int(pos.x, pos.y - 1), out South);
                world.TryGetChunk(new Vector2Int(pos.x + 1, pos.y), out East);
                world.TryGetChunk(new Vector2Int(pos.x - 1, pos.y), out West);
            }
            else
            {
                North = null;
                South = null;
                East = null;
                West = null;
            }
        }

        /// <summary>
        /// Мгновенное получение кэша соседей для подчанка с индексом subChunkIndex (0..7).
        /// Нулевое количество обращений к словарю мира!
        /// </summary>
        public SubChunkNeighbors GetSubChunkNeighbors(int subChunkIndex)
        {
            if (Center == null || subChunkIndex < 0 || subChunkIndex >= Chunk.SubChunksCount)
            {
                return default;
            }

            SubChunk center = Center.GetSubChunk(subChunkIndex);
            if (center == null)
            {
                return default;
            }

            SubChunk up = (subChunkIndex < Chunk.SubChunksCount - 1) ? Center.GetSubChunk(subChunkIndex + 1) : null;
            SubChunk down = (subChunkIndex > 0) ? Center.GetSubChunk(subChunkIndex - 1) : null;
            SubChunk north = North != null ? North.GetSubChunk(subChunkIndex) : null;
            SubChunk south = South != null ? South.GetSubChunk(subChunkIndex) : null;
            SubChunk east = East != null ? East.GetSubChunk(subChunkIndex) : null;
            SubChunk west = West != null ? West.GetSubChunk(subChunkIndex) : null;

            return new SubChunkNeighbors(center, up, down, north, south, east, west);
        }
    }
}
