namespace VoxelCore
{
    /// <summary>
    /// Подчанк фиксированного размера 16x16x16 (4096 вокселей).
    /// Базовый строительный блок для мешинга и сериализации.
    /// </summary>
    public sealed class SubChunk
    {
        public const int Size = 16;
        public const int Volume = Size * Size * Size; // 4096 вокселей

        private readonly Voxel[] _voxels;

        public SubChunk()
        {
            _voxels = new Voxel[Volume];
        }

        /// <summary>
        /// Возвращает воксель по локальным координатам внутри подчанка (0..15).
        /// </summary>
        public Voxel GetVoxel(int x, int y, int z)
        {
            return _voxels[GetIndex(x, y, z)];
        }

        /// <summary>
        /// Устанавливает воксель по локальным координатам внутри подчанка (0..15).
        /// </summary>
        public void SetVoxel(int x, int y, int z, Voxel voxel)
        {
            _voxels[GetIndex(x, y, z)] = voxel;
        }

        /// <summary>
        /// Вычисление одномерного индекса в массиве по координатам (x, y, z).
        /// Порядок: X (горизонталь), Z (глубина), Y (вертикальный слой).
        /// </summary>
        private static int GetIndex(int x, int y, int z)
        {
            return x + (z * Size) + (y * Size * Size);
        }
    }
}
