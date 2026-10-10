using System;
using UnityEngine;

namespace VoxelCore
{
    /// <summary>
    /// Вертикальная колонна чанка размером 16x128x16.
    /// Состоит из 8 подчанков SubChunk размером 16x16x16 (8 * 16 = 128 по высоте).
    /// </summary>
    public sealed class Chunk
    {
        public const int Width = 16;
        public const int Height = 128;
        public const int SubChunksCount = Height / SubChunk.Size; // 8

        public Vector2Int Position { get; }

        private readonly SubChunk[] _subChunks;

        public Chunk(Vector2Int position)
        {
            Position = position;
            _subChunks = new SubChunk[SubChunksCount];

            for (int i = 0; i < SubChunksCount; i++)
            {
                _subChunks[i] = new SubChunk();
            }
        }

        /// <summary>
        /// Возвращает воксель по локальным координатам чанка (X: 0..15, Y: 0..127, Z: 0..15).
        /// </summary>
        public Voxel GetVoxel(int localX, int localY, int localZ)
        {
            int subChunkIndex = localY / SubChunk.Size;
            int subLocalY = localY % SubChunk.Size;

            return _subChunks[subChunkIndex].GetVoxel(localX, subLocalY, localZ);
        }

        /// <summary>
        /// Устанавливает воксель по локальным координатам чанка (X: 0..15, Y: 0..127, Z: 0..15).
        /// </summary>
        public void SetVoxel(int localX, int localY, int localZ, Voxel voxel)
        {
            int subChunkIndex = localY / SubChunk.Size;
            int subLocalY = localY % SubChunk.Size;

            _subChunks[subChunkIndex].SetVoxel(localX, subLocalY, localZ, voxel);
        }

        /// <summary>
        /// Получение подчанка по вертикальному индексу (0..7).
        /// </summary>
        public SubChunk GetSubChunk(int index)
        {
            if (index < 0 || index >= SubChunksCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Индекс подчанка должен быть в пределах [0..{SubChunksCount - 1}].");
            }

            return _subChunks[index];
        }
    }
}
